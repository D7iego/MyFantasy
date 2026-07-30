using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Fantasy.Dtos;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

/// <summary>
/// Pestaña Plantilla/Alineación — planificador local de onces. Guarda las
/// alineaciones del usuario en BD (por liga por defecto) y puede cargar el once
/// oficial de LaLiga como punto de partida. NO escribe en el juego oficial.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LineupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;
    private readonly IFantasyApiClient _api;
    private readonly ILogger<LineupsController> _logger;

    public LineupsController(AppDbContext db, LeagueService leagues, IFantasyApiClient api, ILogger<LineupsController> logger)
    {
        _db = db;
        _leagues = leagues;
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LineupResponse>>> Get(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return Ok(Array.Empty<LineupResponse>());

        var rows = await _db.Lineups
            .Where(l => l.LeagueId == league.Id)
            .OrderByDescending(l => l.UpdatedAt)
            .Select(l => new LineupResponse(l.Id, l.Name, l.Formation, l.Data, l.UpdatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<LineupResponse>> Create([FromBody] SaveLineupRequest body, CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return BadRequest(new { error = "No hay liga por defecto" });

        var lineup = new Lineup
        {
            LeagueId = league.Id,
            Name = string.IsNullOrWhiteSpace(body.Name) ? "Mi alineación" : body.Name!.Trim(),
            Formation = body.Formation,
            Data = body.Data,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Lineups.Add(lineup);
        await _db.SaveChangesAsync(ct);
        return Ok(new LineupResponse(lineup.Id, lineup.Name, lineup.Formation, lineup.Data, lineup.UpdatedAt));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveLineupRequest body, CancellationToken ct)
    {
        var lineup = await _db.Lineups.FindAsync([id], ct);
        if (lineup is null) return NotFound(new { error = "Alineación no encontrada" });

        if (!string.IsNullOrWhiteSpace(body.Name)) lineup.Name = body.Name!.Trim();
        lineup.Formation = body.Formation;
        lineup.Data = body.Data;
        lineup.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var lineup = await _db.Lineups.FindAsync([id], ct);
        if (lineup is null) return NotFound(new { error = "Alineación no encontrada" });
        _db.Lineups.Remove(lineup);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Once oficial actual de LaLiga (para cargarlo como punto de partida).</summary>
    [HttpGet("official")]
    public async Task<ActionResult<OfficialLineupResponse>> Official(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null || string.IsNullOrWhiteSpace(league.TeamId))
            return NotFound(new { error = "No hay equipo del usuario (sincroniza primero)" });

        LineupApiDto? lineup;
        try
        {
            lineup = await _api.GetCurrentLineupAsync(league.TeamId!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar el once oficial.");
            return StatusCode(502, new { error = "No se pudo cargar el once oficial de LaLiga" });
        }

        var f = lineup?.Formation;
        if (f is null) return NotFound(new { error = "El once oficial llegó vacío" });

        static IReadOnlyList<string> Ids(List<LineupSlotDto>? list) =>
            (list ?? new()).Select(s => s.PlayerMaster?.Id).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).ToList();

        var formation = f.TacticalFormation is { Length: >= 3 }
            ? string.Join('-', f.TacticalFormation)
            : "4-3-3";

        return Ok(new OfficialLineupResponse(
            formation,
            Ids(f.Goalkeeper), Ids(f.Defender), Ids(f.Midfield), Ids(f.Striker)));
    }
}
