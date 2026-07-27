using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MyFantasy.Api.Fantasy;

/// <summary>
/// Gestión centralizada del token B2C. Mantiene el <c>id_token</c> (el bearer
/// que consume la API de datos) en memoria y lo renueva automáticamente con el
/// refresh token antes de que caduque. El resto de la app pide el bearer a
/// <see cref="GetBearerTokenAsync"/> sin preocuparse de la expiración.
/// </summary>
public interface IFantasyTokenManager
{
    Task<string> GetBearerTokenAsync(CancellationToken ct = default);
    Task<string> RefreshAsync(CancellationToken ct = default);
}

public class FantasyTokenManager : IFantasyTokenManager
{
    private readonly HttpClient _http;
    private readonly FantasyOptions _options;
    private readonly ILogger<FantasyTokenManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _bearer;
    private string? _refreshToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public FantasyTokenManager(
        HttpClient http,
        IOptions<FantasyOptions> options,
        ILogger<FantasyTokenManager> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _refreshToken = _options.Auth.RefreshToken;

        // Bearer fijo (id_token pegado a mano): úsalo tal cual, sin refresh.
        if (!string.IsNullOrWhiteSpace(_options.Auth.BearerToken))
        {
            _bearer = _options.Auth.BearerToken;
            _expiresAt = DateTimeOffset.MaxValue;
        }
    }

    public async Task<string> GetBearerTokenAsync(CancellationToken ct = default)
    {
        var skew = TimeSpan.FromMinutes(Math.Max(0, _options.Auth.RefreshSkewMinutes));
        if (_bearer is not null && DateTimeOffset.UtcNow < _expiresAt - skew)
        {
            return _bearer;
        }
        return await RefreshAsync(ct);
    }

    public async Task<string> RefreshAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Otro hilo pudo refrescar mientras esperábamos el lock.
            var skew = TimeSpan.FromMinutes(Math.Max(0, _options.Auth.RefreshSkewMinutes));
            if (_bearer is not null && DateTimeOffset.UtcNow < _expiresAt - skew)
            {
                return _bearer;
            }

            if (string.IsNullOrWhiteSpace(_refreshToken))
            {
                // Sin refresh token: si había un bearer fijo lo devolvemos; si no, error claro.
                if (_bearer is not null) return _bearer;
                throw new InvalidOperationException(
                    "No hay credenciales de LaLiga configuradas. Define Fantasy:Auth:RefreshToken " +
                    "(o Fantasy:Auth:BearerToken) en user-secrets. Ver README.");
            }

            var endpoint = $"{_options.Auth.TokenEndpoint}?p={_options.Auth.RefreshPolicy}";
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _refreshToken!,
                ["client_id"] = _options.Auth.ClientId,
                ["scope"] = _options.Auth.Scope
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form };
            req.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                // 400/401 => refresh token caducado o inválido.
                _logger.LogError("Refresh de token falló ({Status}): {Body}", (int)res.StatusCode, body);
                throw new InvalidOperationException(
                    (int)res.StatusCode is 400 or 401
                        ? "invalid_grant: el refresh token de LaLiga caducó o es inválido. Renuévalo en user-secrets."
                        : $"El refresh de token devolvió {(int)res.StatusCode}.");
            }

            var token = JsonSerializer.Deserialize<B2CTokenResponse>(body)
                        ?? throw new InvalidOperationException("Respuesta de token vacía.");

            // La API usa el id_token como bearer (ver refreshToken en authService.js).
            _bearer = token.IdToken ?? token.AccessToken
                ?? throw new InvalidOperationException("La respuesta de token no trae id_token ni access_token.");

            // B2C rota el refresh token: nos quedamos con el nuevo si viene.
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                _refreshToken = token.RefreshToken;
            }

            _expiresAt = ComputeExpiry(token);
            _logger.LogInformation("Token de LaLiga renovado, expira {ExpiresAt:u}.", _expiresAt);
            return _bearer;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static DateTimeOffset ComputeExpiry(B2CTokenResponse token)
    {
        if (token.ExpiresOn is > 0)
        {
            return DateTimeOffset.FromUnixTimeSeconds(token.ExpiresOn.Value);
        }
        // LaLiga prioriza id_token_expires_in sobre expires_in.
        var seconds = token.IdTokenExpiresIn ?? token.ExpiresIn ?? 3600;
        return DateTimeOffset.UtcNow.AddSeconds(seconds);
    }

    private sealed class B2CTokenResponse
    {
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public long? ExpiresIn { get; set; }
        [JsonPropertyName("id_token_expires_in")] public long? IdTokenExpiresIn { get; set; }
        [JsonPropertyName("expires_on")] public long? ExpiresOn { get; set; }
    }
}
