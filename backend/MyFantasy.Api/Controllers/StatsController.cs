using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

/// <summary>Pestaña 4 — Stats: métricas globales sobre mis propios datos.</summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;
    private readonly DeltaService _deltas;
    private readonly StatsService _stats;
    private readonly IFantasyApiClient _api;
    private readonly ILogger<StatsController> _logger;

    public StatsController(
        AppDbContext db,
        LeagueService leagues,
        DeltaService deltas,
        StatsService stats,
        IFantasyApiClient api,
        ILogger<StatsController> logger)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
        _stats = stats;
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<StatsResponse>> Get(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null)
            return Ok(new StatsResponse(0, 0, 0, 0, null, null, 0, 0, 0, 0, null));

        // Operaciones cerradas.
        var sales = await _db.Sales
            .Include(s => s.Player)
            .Where(s => s.LeagueId == league.Id)
            .ToListAsync(ct);

        var totalPl = sales.Sum(s => s.ProfitLoss);
        var profitable = sales.Count(s => s.ProfitLoss > 0);
        var best = sales.OrderByDescending(s => s.ProfitLoss).FirstOrDefault();
        var worst = sales.OrderBy(s => s.ProfitLoss).FirstOrDefault();

        // Cartera activa (no realizado).
        var holdings = await _db.Holdings
            .Where(h => h.LeagueId == league.Id && h.Status == HoldingStatus.Active)
            .Select(h => new { h.PlayerId, h.PurchasePrice })
            .ToListAsync(ct);

        var deltas = await _deltas.GetDeltasBulkAsync(holdings.Select(h => h.PlayerId).ToList(), ct);
        long portfolioValue = 0, unrealized = 0, todayMovement = 0;
        foreach (var h in holdings)
        {
            deltas.TryGetValue(h.PlayerId, out var d);
            var current = d?.CurrentValue ?? 0;
            portfolioValue += current;
            unrealized += current - h.PurchasePrice;       // plusvalía acumulada desde compra
            todayMovement += d?.DailyDelta ?? 0;           // movimiento del día (hoy vs. ayer)
        }

        var availableMoney = await TryGetAvailableMoneyAsync(league, ct);

        return Ok(new StatsResponse(
            TotalProfitLoss: totalPl,
            TotalSales: sales.Count,
            ProfitableSales: profitable,
            ProfitableRate: sales.Count == 0 ? 0 : Math.Round((double)profitable / sales.Count, 4),
            BestSale: best is null ? null : HistoryController.ToResponse(best),
            WorstSale: worst is null ? null : HistoryController.ToResponse(worst),
            ActivePortfolioValue: portfolioValue,
            ActiveUnrealizedProfitLoss: unrealized,
            ActiveHoldings: holdings.Count,
            TodayMovement: todayMovement,
            AvailableMoney: availableMoney));
    }

    /// <summary>Serie diaria para el gráfico de barras (últimos <paramref name="days"/> días).</summary>
    [HttpGet("daily-pnl")]
    public async Task<ActionResult<IReadOnlyList<DailyPnlResponse>>> DailyPnl(
        [FromQuery] int days = 7, CancellationToken ct = default)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return Ok(Array.Empty<DailyPnlResponse>());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var series = await _stats.GetDailyMovementAsync(league.Id, days, today, ct);
        return Ok(series.Select(p => new DailyPnlResponse(p.Fecha, p.Movimiento)).ToList());
    }

    /// <summary>Dinero disponible del equipo. Es la única dependencia externa de
    /// Stats: un error puntual de la API no debe tumbar el resto (devuelve null),
    /// pero una sesión caducada sí burbujea para disparar el re-login.</summary>
    private async Task<long?> TryGetAvailableMoneyAsync(League league, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(league.TeamId)) return null;
        try
        {
            var money = await _api.GetTeamMoneyAsync(league.TeamId!, ct);
            return money?.Value;
        }
        catch (FantasyApiException ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener el dinero disponible del equipo {TeamId}.", league.TeamId);
            return null;
        }
    }
}
