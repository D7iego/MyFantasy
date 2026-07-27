using Microsoft.AspNetCore.Mvc;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly SyncService _sync;
    public SyncController(SyncService sync) => _sync = sync;

    /// <summary>
    /// Dispara la sincronización con la API de LaLiga: snapshot de precios del
    /// día + diff de plantilla (fichajes/ventas automáticos). Se llama al
    /// iniciar/refrescar la app.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResult>> Post(CancellationToken ct)
    {
        var result = await _sync.SyncAsync(ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }
}
