using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

/// <summary>
/// Pestaña Rivales — plantillas de los demás managers de la liga por defecto.
/// El selector de manager sale de la clasificación (1 llamada); al elegir uno,
/// se baja SU plantilla (1 llamada) y se enriquece con nuestros PriceSnapshots
/// (deltas gratis) y el estado de cláusula/blindaje.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RivalsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;
    private readonly IFantasyApiClient _api;
    private readonly DeltaService _deltas;

    public RivalsController(AppDbContext db, LeagueService leagues, IFantasyApiClient api, DeltaService deltas)
    {
        _db = db;
        _leagues = leagues;
        _api = api;
        _deltas = deltas;
    }

    [HttpGet]
    public async Task<ActionResult<RivalsResponse>> Get([FromQuery] string? teamId, CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null)
            return Ok(new RivalsResponse(Array.Empty<RivalManagerResponse>(), null, null));

        var standing = await _api.GetLeagueStandingAsync(league.ExternalId, ct);
        // El rank es la posición en la clasificación: la API la devuelve ya ordenada,
        // así que usamos el índice (1 = líder) sin depender de un campo de posición.
        var managers = standing
            .Select((e, i) => new
            {
                TeamId = e.Team?.Id ?? e.Id,
                Manager = e.Team?.Manager?.ManagerName ?? e.Name ?? "Manager",
                TeamName = e.Team?.Name ?? e.Name,
                Rank = i + 1,
                Points = e.ResolvedPoints,
                TeamValue = e.ResolvedTeamValue
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.TeamId))
            .Select(m => new RivalManagerResponse(m.TeamId!, m.Manager, m.TeamName, m.Rank, m.Points, m.TeamValue))
            .ToList();

        if (string.IsNullOrWhiteSpace(teamId))
            return Ok(new RivalsResponse(managers, null, null));

        var squad = await _api.GetTeamSquadAsync(league.ExternalId, teamId!, ct);
        var squadPlayers = squad?.Players ?? new();

        var extIds = squadPlayers
            .Select(sp => sp.PlayerMaster?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();

        var localPlayers = await _db.Players
            .Where(p => extIds.Contains(p.ExternalId))
            .ToDictionaryAsync(p => p.ExternalId, ct);

        var deltas = await _deltas.GetDeltasBulkAsync(localPlayers.Values.Select(p => p.Id).ToList(), ct);

        var rows = squadPlayers
            .Where(sp => !string.IsNullOrWhiteSpace(sp.PlayerMaster?.Id))
            .Select(sp =>
            {
                var extId = sp.PlayerMaster!.Id!;
                localPlayers.TryGetValue(extId, out var local);
                Services.PriceDeltas? d = null;
                if (local is not null) deltas.TryGetValue(local.Id, out d);

                var current = d?.CurrentValue ?? sp.MarketValue ?? sp.PlayerMaster.MarketValue;
                var posId = sp.PlayerMaster.PositionId ?? sp.PositionId;

                return new RivalPlayerResponse(
                    PlayerId: local?.Id,
                    ExternalId: extId,
                    Name: sp.PlayerMaster.Nickname ?? sp.PlayerMaster.Name ?? local?.Name ?? extId,
                    Team: sp.PlayerMaster.Team?.Name ?? local?.Team,
                    Position: PositionExtensions.FromApiId(posId).ToSpanish(),
                    CurrentValue: current,
                    DailyDelta: d?.DailyDelta,
                    WeeklyDelta: d?.WeeklyDelta,
                    BuyoutClause: sp.BuyoutClause,
                    BuyoutClauseLockedEndTime: sp.BuyoutClauseLockedEndTime,
                    IsShielded: sp.IsShielded ?? false,
                    ImageUrl: sp.PlayerMaster.ResolvedImageUrl ?? local?.ImageUrl);
            })
            .OrderByDescending(r => r.CurrentValue ?? 0)
            .ToList();

        return Ok(new RivalsResponse(managers, teamId, rows));
    }
}
