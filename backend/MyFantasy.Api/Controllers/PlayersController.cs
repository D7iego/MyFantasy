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

    public PlayersController(AppDbContext db, LeagueService leagues, DeltaService deltas)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
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
