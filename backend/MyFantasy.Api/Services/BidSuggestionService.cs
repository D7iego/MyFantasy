using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Fantasy;

namespace MyFantasy.Api.Services;

/// <summary>Jugador del mercado de hoy que entra en el cálculo de la puja.
/// <paramref name="RecentTrendPct"/> es la variación reciente de precio (%),
/// robusta a poco histórico (ver DeltaService).</summary>
public record MarketMember(string ExternalId, long? CurrentValue, double? RecentTrendPct);

/// <summary>
/// Heurística de "puja sugerida" (pestaña Mercado). NO es un dato de la API: es
/// una recomendación orientativa que combina, al 50/50:
///  - rendimiento reciente: media de puntos de las últimas N jornadas, normalizada
///    respecto al resto del mercado de HOY (performance_score, comparativo).
///  - tendencia de precio: el delta semanal % del propio jugador contra un rango
///    de referencia fijo (price_trend_score, NO comparativo).
/// La puja se ancla en la RECOMPRA de la liga: como al vender te dan hasta +10%
/// del valor, ese es el techo "seguro". combined 0..1 mapea de -BidMaxDrop (flojo)
/// a +BidSellBackPct+BidMomentumPremium (fuerte, apostando a que siga subiendo).
/// Se calcula para TODO el mercado a la vez (performance es comparativo) y se
/// cachea la parte de puntos por jornada, que solo cambia al cerrarse una jornada.
/// </summary>
public class BidSuggestionService
{
    private const string CacheKey = "bid:recent-avg-points";

    private readonly IFantasyApiClient _api;
    private readonly IMemoryCache _cache;
    private readonly FantasyOptions _options;
    private readonly ILogger<BidSuggestionService> _logger;

    public BidSuggestionService(
        IFantasyApiClient api, IMemoryCache cache, IOptions<FantasyOptions> options,
        ILogger<BidSuggestionService> logger)
    {
        _api = api;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Dictionary<string, BidSuggestionResponse>> BuildAsync(
        IReadOnlyList<MarketMember> market, CancellationToken ct)
    {
        var result = new Dictionary<string, BidSuggestionResponse>();
        if (market.Count == 0) return result;

        var avg = await GetRecentAvgPointsAsync(ct);   // extId -> (media, jornadas)

        // min/max de media SOLO entre los jugadores del mercado que tienen datos.
        double min = 0, max = 0;
        var haveRange = false;
        foreach (var m in market)
        {
            if (!avg.TryGetValue(m.ExternalId, out var a)) continue;
            if (!haveRange) { min = max = a.Avg; haveRange = true; }
            else { if (a.Avg < min) min = a.Avg; if (a.Avg > max) max = a.Avg; }
        }

        // Solo hay señal de rendimiento útil si hay partidos Y las medias difieren.
        // En pretemporada (sin partidos) o si todos empatan, la puja es SOLO económica.
        var haveSpread = haveRange && max > min;

        var range = _options.BidPriceTrendRangePct;
        // combined 0..1 mapea linealmente de -BidMaxDrop a +(reventa + prima momentum).
        var upside = _options.BidSellBackPct + _options.BidMomentumPremium;
        var span = _options.BidMaxDrop + upside;

        foreach (var m in market)
        {
            if (m.CurrentValue is not long price || price <= 0) continue;

            // price_trend_score: variación reciente de precio (%), robusta a poco
            // histórico. Sin dato => neutro (0.5).
            double? weeklyPct = m.RecentTrendPct;
            var trendScore = weeklyPct is double p
                ? Math.Clamp((p + range) / (2 * range), 0, 1)
                : 0.5;

            // performance_score: comparativo con el mercado. Solo cuenta si el
            // jugador tiene partidos Y hay dispersión de medias en el mercado.
            double? playerAvg = null;
            int? playerWeeks = null;
            if (avg.TryGetValue(m.ExternalId, out var a)) { playerAvg = a.Avg; playerWeeks = a.Weeks; }

            double? perfScore = haveSpread && playerAvg.HasValue
                ? (playerAvg.Value - min) / (max - min)
                : null;

            // Sin componente de rendimiento => la puja es 100% tendencia de precio.
            var combined = perfScore is double ps ? 0.5 * ps + 0.5 * trendScore : trendScore;
            var limited = perfScore is null || (playerWeeks is int w && w < _options.BidRecentWeeks);

            // Anclado en la reventa: -drop (flojo) .. +sellBack+prima (fuerte).
            var lerp = -_options.BidMaxDrop + combined * span;
            var bid = (long)Math.Round(price * (1 + lerp));

            result[m.ExternalId] = new BidSuggestionResponse(
                AvgPointsLast5: playerAvg is double pa ? Math.Round(pa, 1) : null,
                WeeklyPct: weeklyPct is double wp ? Math.Round(wp, 1) : null,
                PerformanceScore: perfScore is double x ? Math.Round(x, 3) : null,
                PriceTrendScore: Math.Round(trendScore, 3),
                CombinedScore: Math.Round(combined, 3),
                SuggestedBid: bid,
                LimitedData: limited);
        }

        return result;
    }

    /// <summary>Media de puntos por jugador en las últimas N jornadas disputadas.
    /// Cacheada (cambia solo al cerrarse una jornada). Degrada a vacío si la API
    /// de stats no está disponible o su forma no coincide.</summary>
    private async Task<Dictionary<string, (double Avg, int Weeks)>> GetRecentAvgPointsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, (double, int)>? cached) && cached is not null)
            return cached;

        var map = new Dictionary<string, (double Avg, int Weeks)>();
        try
        {
            var current = await _api.GetCurrentWeekAsync(ct);
            var currentWeek = current?.ResolvedWeek;
            if (currentWeek is > 1)
            {
                var acc = new Dictionary<string, List<double>>();
                var taken = 0;
                for (var w = currentWeek.Value - 1; w >= 1 && taken < _options.BidRecentWeeks; w--, taken++)
                {
                    var stats = await _api.GetWeekStatsAsync(w, ct);
                    foreach (var s in stats)
                    {
                        var id = s.ResolvedPlayerId;
                        if (id is null || s.ResolvedPoints is null) continue;
                        if (!acc.TryGetValue(id, out var list)) { list = new(); acc[id] = list; }
                        list.Add(s.ResolvedPoints.Value);
                    }
                }
                map = acc.ToDictionary(k => k.Key, k => (k.Value.Average(), k.Value.Count));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stats por jornada no disponibles; la puja usará solo la tendencia de precio.");
        }

        // Con datos, cachea 1h; vacío (posible fallo transitorio), reintenta antes.
        _cache.Set(CacheKey, map, TimeSpan.FromMinutes(map.Count > 0 ? 60 : 10));
        return map;
    }
}
