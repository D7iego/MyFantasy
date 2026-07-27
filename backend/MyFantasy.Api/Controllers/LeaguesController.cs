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
}
