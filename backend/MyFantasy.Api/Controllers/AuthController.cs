using Microsoft.AspNetCore.Mvc;
using MyFantasy.Api.Fantasy;

namespace MyFantasy.Api.Controllers;

/// <summary>
/// Re-login manual (Opción B). Cuando el refresh_token caduca, la app responde
/// 401 { needsLogin: true } y el frontend abre una pantalla para pegar un token
/// recién capturado. Aquí NO se reciben credenciales de Google/LaLiga: solo el
/// token, que se valida contra B2C y se guarda cifrado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IFantasyTokenManager _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IFantasyTokenManager tokens, ILogger<AuthController> logger)
    {
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>Adopta un refresh_token nuevo (recomendado) o un id_token suelto.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest? req, CancellationToken ct)
    {
        if (req is null ||
            (string.IsNullOrWhiteSpace(req.RefreshToken) && string.IsNullOrWhiteSpace(req.BearerToken)))
        {
            return BadRequest(new { error = "Aporta 'refreshToken' o 'bearerToken'." });
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(req.RefreshToken))
                await _tokens.AdoptRefreshTokenAsync(req.RefreshToken!, ct);
            else
                await _tokens.AdoptBearerTokenAsync(req.BearerToken!, ct);

            return Ok(new { ok = true });
        }
        catch (NeedsLoginException ex)
        {
            _logger.LogWarning("Re-login rechazado: {Message}", ex.Message);
            return Unauthorized(new { needsLogin = true, error = "El token aportado no es válido o ha caducado." });
        }
    }

    /// <summary>¿Hay una sesión utilizable ahora mismo? El frontend lo usa al
    /// arrancar para decidir si mostrar la pantalla de login.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        try
        {
            await _tokens.GetBearerTokenAsync(ct);
            return Ok(new { authenticated = true });
        }
        catch (NeedsLoginException)
        {
            return Ok(new { authenticated = false });
        }
    }

    public record LoginRequest(string? RefreshToken, string? BearerToken);
}
