using MyFantasy.Api.Fantasy;

namespace MyFantasy.Api.Middleware;

/// <summary>
/// Traduce cualquier <see cref="NeedsLoginException"/> (o un 401 de la API de
/// LaLiga) que burbujee desde un endpoint en una respuesta uniforme
/// <c>401 { needsLogin: true }</c>, para que el interceptor del frontend abra la
/// pantalla de re-login. Debe registrarse DESPUÉS de UseCors para que la
/// respuesta lleve las cabeceras CORS y el navegador pueda leerla.
/// </summary>
public class NeedsLoginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<NeedsLoginMiddleware> _logger;

    public NeedsLoginMiddleware(RequestDelegate next, ILogger<NeedsLoginMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (NeedsLoginException ex)
        {
            await WriteNeedsLogin(ctx, ex.Message);
        }
        catch (FantasyApiException ex) when (ex.StatusCode == 401)
        {
            await WriteNeedsLogin(ctx, ex.Message);
        }
    }

    private async Task WriteNeedsLogin(HttpContext ctx, string reason)
    {
        _logger.LogWarning("Respondiendo 401 needsLogin: {Reason}", reason);
        if (ctx.Response.HasStarted)
        {
            _logger.LogError("La respuesta ya había comenzado; no se pudo emitir 401 needsLogin.");
            return;
        }
        ctx.Response.Clear();
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { needsLogin = true });
    }
}
