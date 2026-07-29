using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaguesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;

    public LeaguesController(AppDbContext db, LeagueService leagues)
    {
        _db = db;
        _leagues = leagues;
    }

    /// <summary>Pestaña 1 — Ligas. Lista las ligas y marca la por defecto.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeagueResponse>>> Get(CancellationToken ct)
    {
        var leagues = await _db.Leagues.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).ToListAsync(ct);
        var defaultLeague = await _leagues.GetDefaultLeagueAsync(ct);

        return Ok(leagues.Select(l => new LeagueResponse(
            l.Id, l.ExternalId, l.Name,
            IsDefault: l.Id == defaultLeague?.Id,
            l.CreatedAt)));
    }

    /// <summary>Cambia la liga por defecto.</summary>
    [HttpPut("{id:int}/default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var ok = await _leagues.SetDefaultAsync(id, ct);
        return ok ? NoContent() : NotFound(new { error = "Liga no encontrada" });
    }

    /// <summary>
    /// Borra una liga (p. ej. una liga fantasma tras salirte de ella) y, en
    /// cascada, sus Holdings y Sales. NO toca PriceSnapshots (son por jugador,
    /// comunes a toda la competición). Si era la predeterminada, la regla de
    /// liga por defecto recae automáticamente en la siguiente disponible.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var league = await _db.Leagues.FindAsync([id], ct);
        if (league is null) return NotFound(new { error = "Liga no encontrada" });

        _db.Leagues.Remove(league);
        await _db.SaveChangesAsync(ct);

        // Si la borrada tenía la marca y quedan ligas, marca la más antigua para
        // que haya siempre una resaltada en la UI.
        if (league.IsDefault)
        {
            var next = await _leagues.GetDefaultLeagueAsync(ct);
            if (next is not null) await _leagues.SetDefaultAsync(next.Id, ct);
        }
        return NoContent();
    }
}
