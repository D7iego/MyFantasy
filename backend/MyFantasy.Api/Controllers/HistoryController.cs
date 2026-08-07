using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

/// <summary>Pestaña 3 — Historial: "Sin vender" (activos) y "Vendidos" (congelados).</summary>
[ApiController]
[Route("api/[controller]")]
public class HistoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;
    private readonly DeltaService _deltas;

    public HistoryController(AppDbContext db, LeagueService leagues, DeltaService deltas)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
    }

    /// <summary>Sub-pestaña "Sin vender": holdings activos con stats en vivo.</summary>
    [HttpGet("holdings")]
    public async Task<ActionResult<IEnumerable<HoldingResponse>>> Holdings(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return Ok(Array.Empty<HoldingResponse>());

        var season = SeasonUtil.Current();
        var holdings = await _db.Holdings
            .Include(h => h.Player)
            .Where(h => h.LeagueId == league.Id && h.Status == HoldingStatus.Active && h.Season == season)
            .ToListAsync(ct);

        var deltas = await _deltas.GetDeltasBulkAsync(holdings.Select(h => h.PlayerId).ToList(), ct);

        var rows = holdings.Select(h =>
        {
            deltas.TryGetValue(h.PlayerId, out var d);
            var current = d?.CurrentValue;
            return new HoldingResponse(
                h.Id, h.PlayerId, h.Player!.ExternalId, h.Player.Name, h.Player.Team,
                h.Player.Position.ToSpanish(),
                current, d?.DailyDelta, d?.WeeklyDelta, h.PurchasePrice,
                current is null ? null : current - h.PurchasePrice,
                h.PurchasePriceIsManual, h.PurchaseDate, h.Status.ToString(), h.Player!.ImageUrl);
        });

        return Ok(rows);
    }

    /// <summary>Sub-pestaña "Vendidos": operaciones cerradas con valores congelados.</summary>
    [HttpGet("sales")]
    public async Task<ActionResult<IEnumerable<SaleResponse>>> Sales(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return Ok(Array.Empty<SaleResponse>());

        var season = SeasonUtil.Current();
        var sales = await _db.Sales
            .Include(s => s.Player)
            .Where(s => s.LeagueId == league.Id && s.Season == season)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id)
            .ToListAsync(ct);

        return Ok(sales.Select(ToResponse));
    }

    /// <summary>
    /// Ajustar el precio de venta (p.ej. venta a otro manager a precio distinto).
    /// Recalcula solo la G/P; los deltas congelados NO se tocan.
    /// </summary>
    [HttpPut("sales/{id:int}/sale-price")]
    public async Task<IActionResult> UpdateSalePrice(int id, [FromBody] UpdateSalePriceRequest body, CancellationToken ct)
    {
        var sale = await _db.Sales.FindAsync([id], ct);
        if (sale is null) return NotFound(new { error = "Venta no encontrada" });
        if (body.SalePrice < 0) return BadRequest(new { error = "El precio de venta no puede ser negativo" });

        sale.SalePrice = body.SalePrice;
        sale.ProfitLoss = body.SalePrice - sale.PurchasePrice;
        sale.SalePriceIsManual = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    internal static SaleResponse ToResponse(Sale s) => new(
        s.Id, s.PlayerId, s.Player?.Name ?? "?", s.Player?.Team,
        s.Player?.Position.ToSpanish() ?? "Desconocida",
        s.PurchasePrice, s.SalePrice, s.ProfitLoss, s.DailyDelta, s.WeeklyDelta,
        s.PurchaseDate, s.SaleDate, s.SalePriceIsManual);
}
