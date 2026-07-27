using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
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

    public StatsController(AppDbContext db, LeagueService leagues, DeltaService deltas)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
    }

    [HttpGet]
    public async Task<ActionResult<StatsResponse>> Get(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null)
            return Ok(new StatsResponse(0, 0, 0, 0, null, null, 0, 0, 0));

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
        long portfolioValue = 0, unrealized = 0;
        foreach (var h in holdings)
        {
            deltas.TryGetValue(h.PlayerId, out var d);
            var current = d?.CurrentValue ?? 0;
            portfolioValue += current;
            unrealized += current - h.PurchasePrice;
        }

        return Ok(new StatsResponse(
            TotalProfitLoss: totalPl,
            TotalSales: sales.Count,
            ProfitableSales: profitable,
            ProfitableRate: sales.Count == 0 ? 0 : Math.Round((double)profitable / sales.Count, 4),
            BestSale: best is null ? null : HistoryController.ToResponse(best),
            WorstSale: worst is null ? null : HistoryController.ToResponse(worst),
            ActivePortfolioValue: portfolioValue,
            ActiveUnrealizedProfitLoss: unrealized,
            ActiveHoldings: holdings.Count));
    }
}
