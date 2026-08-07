using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyFantasy.Api.Fantasy.Dtos;

namespace MyFantasy.Api.Fantasy;

public class FantasyApiClient : IFantasyApiClient
{
    private readonly HttpClient _http;
    private readonly IFantasyTokenManager _tokens;
    private readonly FantasyOptions _options;
    private readonly ILogger<FantasyApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        // La API mezcla números y cadenas en los mismos campos ("3" vs 3).
        Converters =
        {
            new Dtos.FlexibleNullableIntConverter(),
            new Dtos.FlexibleNullableLongConverter(),
            new Dtos.FlexibleNullableDoubleConverter(),
        }
    };

    public FantasyApiClient(
        HttpClient http,
        IFantasyTokenManager tokens,
        IOptions<FantasyOptions> options,
        ILogger<FantasyApiClient> logger)
    {
        _http = http;
        _tokens = tokens;
        _options = options.Value;
        _logger = logger;
    }

    public Task<UserMeDto?> GetCurrentUserAsync(CancellationToken ct = default)
        => GetObjectAsync<UserMeDto>(Route(_options.Endpoints.CurrentUser), ct);

    public Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken ct = default)
        => GetListAsync<LeagueDto>(Route(_options.Endpoints.Leagues), ct);

    public Task<IReadOnlyList<StandingEntryDto>> GetLeagueStandingAsync(string leagueId, CancellationToken ct = default)
        => GetListAsync<StandingEntryDto>(Route(_options.Endpoints.LeagueStanding, leagueId: leagueId), ct);

    public Task<TeamSquadDto?> GetTeamSquadAsync(string leagueId, string teamId, CancellationToken ct = default)
        => GetObjectAsync<TeamSquadDto>(Route(_options.Endpoints.TeamSquad, leagueId: leagueId, teamId: teamId), ct);

    public Task<IReadOnlyList<FantasyPlayerDto>> GetAllPlayersAsync(CancellationToken ct = default)
        => GetListAsync<FantasyPlayerDto>(Route(_options.Endpoints.Players), ct);

    public Task<TeamMoneyDto?> GetTeamMoneyAsync(string teamId, CancellationToken ct = default)
        => GetObjectAsync<TeamMoneyDto>(Route(_options.Endpoints.TeamMoney, teamId: teamId), ct);

    public Task<IReadOnlyList<MarketItemDto>> GetMarketAsync(string leagueId, CancellationToken ct = default)
        => GetListAsync<MarketItemDto>(Route(_options.Endpoints.Market, leagueId: leagueId), ct);

    public Task<IReadOnlyList<TeamRefDto>> GetTeamsMasterAsync(CancellationToken ct = default)
        => GetListAsync<TeamRefDto>(Route(_options.Endpoints.TeamsMaster), ct);

    public Task<PlayerDetailApiDto?> GetPlayerDetailAsync(string playerId, string leagueId, CancellationToken ct = default)
        => GetObjectAsync<PlayerDetailApiDto>(Route(_options.Endpoints.PlayerDetail, leagueId: leagueId, playerId: playerId), ct);

    public Task<IReadOnlyList<ActivityEntryDto>> GetLeagueActivityAsync(string leagueId, int index, CancellationToken ct = default)
        => GetListAsync<ActivityEntryDto>(Route(_options.Endpoints.LeagueActivity, leagueId: leagueId, index: index.ToString()), ct);

    public Task<IReadOnlyList<OfferDto>> GetPlayerOffersAsync(string leagueId, string playerTeamId, CancellationToken ct = default)
        => GetListAsync<OfferDto>(Route(_options.Endpoints.PlayerOffer, leagueId: leagueId, playerTeamId: playerTeamId), ct);

    public Task<LineupApiDto?> GetCurrentLineupAsync(string teamId, CancellationToken ct = default)
        => GetObjectAsync<LineupApiDto>(Route(_options.Endpoints.TeamLineup, teamId: teamId), ct);

    public Task<CurrentWeekDto?> GetCurrentWeekAsync(CancellationToken ct = default)
        => GetObjectAsync<CurrentWeekDto>(StatsRoute(_options.Endpoints.CurrentWeek), ct);

    public Task<IReadOnlyList<WeekStatDto>> GetWeekStatsAsync(int week, CancellationToken ct = default)
        => GetListAsync<WeekStatDto>(StatsRoute(_options.Endpoints.WeekStats, weekNumber: week.ToString()), ct);

    public Task<CurrentWeekDto?> GetCurrentWeekMainAsync(CancellationToken ct = default)
        => GetObjectAsync<CurrentWeekDto>(Route(_options.Endpoints.WeekCurrentMain), ct);

    public Task<IReadOnlyList<CalendarMatchDto>> GetCalendarAsync(int week, CancellationToken ct = default)
        => GetListAsync<CalendarMatchDto>(Route(_options.Endpoints.Calendar, weekNumber: week.ToString()), ct);

    // ---- Infra ----

    /// <summary>Como <see cref="Route"/> pero con el host de stats (StatsBaseUrl).</summary>
    private string StatsRoute(string template, string? weekNumber = null)
    {
        var path = template
            .Replace("{competitionId}", _options.CompetitionId)
            .Replace("{weekNumber}", weekNumber ?? string.Empty);
        var sep = path.Contains('?') ? '&' : '?';
        return $"{_options.StatsBaseUrl.TrimEnd('/')}{path}{sep}x-lang={_options.Language}";
    }

    private string Route(string template, string? leagueId = null, string? teamId = null, string? weekNumber = null, string? playerId = null, string? index = null, string? playerTeamId = null)
    {
        var path = template
            .Replace("{competitionId}", _options.CompetitionId)
            .Replace("{leagueId}", leagueId ?? string.Empty)
            .Replace("{teamId}", teamId ?? string.Empty)
            .Replace("{weekNumber}", weekNumber ?? string.Empty)
            .Replace("{playerId}", playerId ?? string.Empty)
            .Replace("{playerTeamId}", playerTeamId ?? string.Empty)
            .Replace("{index}", index ?? "0");

        var sep = path.Contains('?') ? '&' : '?';
        return $"{_options.BaseUrl.TrimEnd('/')}{path}{sep}x-lang={_options.Language}";
    }

    private async Task<JsonDocument> SendAsync(string url, CancellationToken ct)
    {
        var bearer = await _tokens.GetBearerTokenAsync(ct);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        req.Headers.TryAddWithoutValidation("x-lang", _options.Language);
        req.Headers.TryAddWithoutValidation("x-app", "2");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        // 401: intenta un refresh forzado una vez.
        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("401 en {Url}; forzando refresh de token y reintentando.", url);
            await _tokens.RefreshAsync(ct);
            return await SendOnceAsync(url, ct);
        }

        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("GET {Url} -> {Status}: {Body}", url, (int)res.StatusCode, Trunc(body));
            throw new FantasyApiException((int)res.StatusCode, $"LaLiga API {(int)res.StatusCode} en {url}");
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body);
    }

    private async Task<JsonDocument> SendOnceAsync(string url, CancellationToken ct)
    {
        var bearer = await _tokens.GetBearerTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        req.Headers.TryAddWithoutValidation("x-lang", _options.Language);
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new FantasyApiException((int)res.StatusCode, $"LaLiga API {(int)res.StatusCode} en {url} (tras refresh)");
        }
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body);
    }

    private async Task<T?> GetObjectAsync<T>(string url, CancellationToken ct)
    {
        using var doc = await SendAsync(url, ct);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Null) return default;

        // Algunas rutas envuelven el objeto en { data: {...} }.
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object)
        {
            return data.Deserialize<T>(JsonOpts);
        }
        return root.Deserialize<T>(JsonOpts);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string url, CancellationToken ct)
    {
        using var doc = await SendAsync(url, ct);
        var array = UnwrapArray(doc.RootElement);
        if (array is null) return Array.Empty<T>();

        var list = new List<T>();
        foreach (var el in array.Value.EnumerateArray())
        {
            var item = el.Deserialize<T>(JsonOpts);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// Localiza el array de elementos aceptando las formas conocidas: raíz array,
    /// { data: [...] }, { elements: [...] }, { leagues: [...] }, { data: { elements/leagues: [...] } }.
    /// </summary>
    private static JsonElement? UnwrapArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return null;

        foreach (var key in new[] { "elements", "leagues", "teams", "data" })
        {
            if (root.TryGetProperty(key, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Array) return prop;
                if (prop.ValueKind == JsonValueKind.Object)
                {
                    foreach (var inner in new[] { "elements", "leagues", "teams" })
                    {
                        if (prop.TryGetProperty(inner, out var innerProp) && innerProp.ValueKind == JsonValueKind.Array)
                            return innerProp;
                    }
                }
            }
        }
        return null;
    }

    private static string Trunc(string s) => s.Length > 500 ? s[..500] + "…" : s;
}

public class FantasyApiException : Exception
{
    public int StatusCode { get; }
    public FantasyApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}
