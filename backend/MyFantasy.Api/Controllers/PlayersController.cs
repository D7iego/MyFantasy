using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

/// <summary>
/// Pestaña 2 — Jugadores (plantilla activa de la liga por defecto) con precio
/// actual, deltas diario/semanal, precio de compra y G/P.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;
    private readonly DeltaService _deltas;
    private readonly PlayerOverviewService _overview;

    public PlayersController(AppDbContext db, LeagueService leagues, DeltaService deltas, PlayerOverviewService overview)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
        _overview = overview;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HoldingResponse>>> Get(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return Ok(Array.Empty<HoldingResponse>());

        var holdings = await _db.Holdings
            .Include(h => h.Player)
            .Where(h => h.LeagueId == league.Id && h.Status == HoldingStatus.Active)
            .ToListAsync(ct);

        var deltas = await _deltas.GetDeltasBulkAsync(holdings.Select(h => h.PlayerId).ToList(), ct);

        var rows = holdings.Select(h =>
        {
            deltas.TryGetValue(h.PlayerId, out var d);
            var current = d?.CurrentValue;
            return new HoldingResponse(
                h.Id, h.PlayerId, h.Player!.ExternalId, h.Player.Name, h.Player.Team,
                h.Player.Position.ToSpanish(),
                current, d?.DailyDelta, d?.WeeklyDelta,
                h.PurchasePrice,
                ProfitLoss: current is null ? null : current - h.PurchasePrice,
                h.PurchasePriceIsManual, h.PurchaseDate, h.Status.ToString(), h.Player.ImageUrl);
        })
        .OrderByDescending(r => r.CurrentValue ?? 0);

        return Ok(rows);
    }

    /// <summary>
    /// Pestaña General: TODOS los jugadores de la competición (los tenga fichados
    /// o no), con precio, deltas y tendencia. Filtra por equipo con
    /// <paramref name="teamId"/>; si se filtra, añade el agregado del equipo.
    /// No hace llamadas a la API: se nutre de Players + PriceSnapshots.
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<PlayersOverviewResponse>> All([FromQuery] string? teamId, CancellationToken ct)
    {
        var query = _db.Players.AsQueryable();
        if (!string.IsNullOrWhiteSpace(teamId))
            query = query.Where(p => p.TeamId == teamId);

        var players = await query.ToListAsync(ct);
        var metrics = await _overview.GetMetricsBulkAsync(players.Select(p => p.Id).ToList(), ct);

        var rows = players.Select(p =>
        {
            metrics.TryGetValue(p.Id, out var m);
            return new PlayerRowResponse(
                p.Id, p.ExternalId, p.Name, p.Team, p.TeamId, p.Position.ToSpanish(),
                m?.CurrentValue, m?.DailyDelta, m?.WeeklyDelta, m?.Trend ?? "estable", p.ImageUrl);
        })
        .OrderByDescending(r => r.CurrentValue ?? 0)
        .ToList();

        var aggregate = string.IsNullOrWhiteSpace(teamId) || rows.Count == 0
            ? null
            : BuildTeamAggregate(teamId!, rows);

        return Ok(new PlayersOverviewResponse(rows, aggregate));
    }

    private static TeamAggregateResponse BuildTeamAggregate(string teamId, List<PlayerRowResponse> rows)
    {
        var daily = rows.Where(r => r.DailyDelta.HasValue).Select(r => (double)r.DailyDelta!.Value).ToList();
        var weekly = rows.Where(r => r.WeeklyDelta.HasValue).Select(r => (double)r.WeeklyDelta!.Value).ToList();

        return new TeamAggregateResponse(
            TeamId: teamId,
            PlayerCount: rows.Count,
            AvgDailyDelta: daily.Count == 0 ? 0 : (long)Math.Round(daily.Average()),
            AvgWeeklyDelta: weekly.Count == 0 ? 0 : (long)Math.Round(weekly.Average()),
            AvgDailyPct: AvgPct(rows, r => r.DailyDelta),
            AvgWeeklyPct: AvgPct(rows, r => r.WeeklyDelta));
    }

    /// <summary>Media de la variación porcentual (delta relativo al precio de partida).</summary>
    private static double AvgPct(List<PlayerRowResponse> rows, Func<PlayerRowResponse, long?> delta)
    {
        var pcts = new List<double>();
        foreach (var r in rows)
        {
            var d = delta(r);
            if (d is null || r.CurrentValue is null) continue;
            var start = r.CurrentValue.Value - d.Value; // precio antes de la variación
            if (start <= 0) continue;
            pcts.Add((double)d.Value / start * 100.0);
        }
        return pcts.Count == 0 ? 0 : Math.Round(pcts.Average(), 2);
    }

    /// <summary>Editar manualmente el precio de compra (fallback si la API no lo trae).</summary>
    [HttpPut("holdings/{id:int}/purchase-price")]
    public async Task<IActionResult> UpdatePurchasePrice(int id, [FromBody] UpdatePurchasePriceRequest body, CancellationToken ct)
    {
        var holding = await _db.Holdings.FindAsync([id], ct);
        if (holding is null) return NotFound(new { error = "Holding no encontrado" });
        if (body.PurchasePrice < 0) return BadRequest(new { error = "El precio de compra no puede ser negativo" });

        holding.PurchasePrice = body.PurchasePrice;
        holding.PurchasePriceIsManual = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
