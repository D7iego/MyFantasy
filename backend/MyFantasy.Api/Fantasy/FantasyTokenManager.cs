using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;

namespace MyFantasy.Api.Fantasy;

/// <summary>
/// Gestión centralizada del token B2C. Mantiene el <c>id_token</c> (el bearer
/// que consume la API de datos) en memoria y lo renueva automáticamente con el
/// refresh token antes de que caduque. El refresh token vive <b>cifrado en BD</b>
/// (Data Protection) para sobrevivir a reinicios y a la rotación de B2C; si no
/// hay ninguno se usa el de user-secrets como semilla. Cuando el refresh caduca
/// o es inválido se lanza <see cref="NeedsLoginException"/> para que la capa web
/// responda <c>401 { needsLogin: true }</c> y el frontend muestre el re-login.
/// </summary>
public interface IFantasyTokenManager
{
    Task<string> GetBearerTokenAsync(CancellationToken ct = default);
    Task<string> RefreshAsync(CancellationToken ct = default);

    /// <summary>Adopta un <c>refresh_token</c> nuevo (pantalla de re-login), lo
    /// valida haciendo un refresh y lo persiste cifrado. Lanza
    /// <see cref="NeedsLoginException"/> si el token no sirve.</summary>
    Task AdoptRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Adopta un <c>id_token</c> pegado a mano (sin refresh; caduca en
    /// ~1 h). Útil como parche rápido.</summary>
    Task AdoptBearerTokenAsync(string idToken, CancellationToken ct = default);
}

public class FantasyTokenManager : IFantasyTokenManager
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtector _protector;
    private readonly FantasyOptions _options;
    private readonly ILogger<FantasyTokenManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _bearer;
    private string? _refreshToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private bool _loaded;

    public FantasyTokenManager(
        IHttpClientFactory httpFactory,
        IServiceScopeFactory scopeFactory,
        IDataProtectionProvider protection,
        IOptions<FantasyOptions> options,
        ILogger<FantasyTokenManager> logger)
    {
        _httpFactory = httpFactory;
        _scopeFactory = scopeFactory;
        _protector = protection.CreateProtector("MyFantasy.Fantasy.RefreshToken.v1");
        _options = options.Value;
        _logger = logger;

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
            await EnsureLoadedAsync(ct);

            // Otro hilo pudo refrescar mientras esperábamos el lock.
            var skew = TimeSpan.FromMinutes(Math.Max(0, _options.Auth.RefreshSkewMinutes));
            if (_bearer is not null && DateTimeOffset.UtcNow < _expiresAt - skew)
            {
                return _bearer;
            }

            if (string.IsNullOrWhiteSpace(_refreshToken))
            {
                // Sin refresh token: si había un bearer fijo lo devolvemos; si no, re-login.
                if (_bearer is not null) return _bearer;
                throw new NeedsLoginException(
                    "No hay credenciales de LaLiga. Inicia sesión (POST /api/auth/login) " +
                    "o define Fantasy:Auth:RefreshToken en user-secrets.");
            }

            return await DoRefreshLocked(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AdoptRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = refreshToken?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new NeedsLoginException("El refresh_token aportado está vacío.");

        await _lock.WaitAsync(ct);
        try
        {
            _refreshToken = token;
            _loaded = true;
            _bearer = null;                          // fuerza un refresh con el nuevo token
            _expiresAt = DateTimeOffset.MinValue;
            await DoRefreshLocked(ct);               // valida + persiste (lanza NeedsLogin si no sirve)
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AdoptBearerTokenAsync(string idToken, CancellationToken ct = default)
    {
        var token = idToken?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new NeedsLoginException("El id_token aportado está vacío.");

        await _lock.WaitAsync(ct);
        try
        {
            _bearer = token;
            _expiresAt = ExpiryFromJwt(token) ?? DateTimeOffset.UtcNow.AddMinutes(55);
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Hace el POST de refresh. DEBE llamarse con <see cref="_lock"/> tomado
    /// y con <see cref="_refreshToken"/> ya cargado.</summary>
    private async Task<string> DoRefreshLocked(CancellationToken ct)
    {
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

        var http = _httpFactory.CreateClient("laliga-auth");
        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Refresh de token falló ({Status}): {Body}", (int)res.StatusCode, body);
            // 400/401 => refresh token caducado o inválido: hay que re-loguear.
            if ((int)res.StatusCode is 400 or 401)
                throw new NeedsLoginException(
                    "invalid_grant: el refresh token de LaLiga caducó o es inválido. Vuelve a iniciar sesión.");
            throw new InvalidOperationException($"El refresh de token devolvió {(int)res.StatusCode}.");
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
        await SaveRefreshTokenAsync(_refreshToken!, ct);
        _logger.LogInformation("Token de LaLiga renovado, expira {ExpiresAt:u}.", _expiresAt);
        return _bearer;
    }

    /// <summary>Carga el refresh token: primero de BD (cifrado), si no, de user-secrets.
    /// Solo la primera vez.</summary>
    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        var stored = await LoadRefreshTokenAsync(ct);
        _refreshToken = !string.IsNullOrWhiteSpace(stored) ? stored : _options.Auth.RefreshToken;
        _loaded = true;
    }

    private async Task<string?> LoadRefreshTokenAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.AuthStates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
            if (string.IsNullOrWhiteSpace(row?.RefreshTokenEnc)) return null;
            return _protector.Unprotect(row.RefreshTokenEnc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer/descifrar el refresh_token de BD; se usará user-secrets si existe.");
            return null;
        }
    }

    private async Task SaveRefreshTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.AuthStates.FirstOrDefaultAsync(x => x.Id == 1, ct);
            if (row is null)
            {
                row = new AuthState { Id = 1 };
                db.AuthStates.Add(row);
            }
            row.RefreshTokenEnc = _protector.Protect(token);
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // No es fatal: el token sigue en memoria para esta sesión.
            _logger.LogWarning(ex, "No se pudo persistir el refresh_token cifrado en BD.");
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

    /// <summary>Lee el claim <c>exp</c> de un JWT sin validar la firma.</summary>
    private static DateTimeOffset? ExpiryFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = (payload.Length % 4) switch
            {
                2 => payload + "==",
                3 => payload + "=",
                _ => payload
            };
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch
        {
            // JWT ilegible: caemos al fallback del llamador.
        }
        return null;
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
