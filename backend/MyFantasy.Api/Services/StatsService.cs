using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;

namespace MyFantasy.Api.Services;

/// <summary>Un punto de la serie: cuánto se movió el valor de mercado de la
/// plantilla ese día (Σ precio_hoy − precio_ayer de los jugadores que se tenían).</summary>
public record DailyPnlPoint(DateOnly Fecha, long Movimiento);

/// <summary>Posición en cartera para el cálculo histórico: un jugador que se
/// tuvo entre <see cref="PurchaseDate"/> y (si se vendió) <see cref="SaleDate"/>.</summary>
internal record PositionRow(int PlayerId, DateOnly PurchaseDate, DateOnly? SaleDate);

/// <summary>
/// Cálculo del "movimiento del día" de la plantilla: NO es plusvalía desde la
/// compra, sino cuánto subió/bajó el valor de mercado de los jugadores que se
/// tenían, de un día para otro. Para la serie de N días se respeta la plantilla
/// que se tenía realmente cada día (un jugador cuenta el día X si
/// <c>PurchaseDate ≤ X</c> y (<c>activo</c> o <c>SaleDate &gt; X</c>)).
/// </summary>
public class StatsService
{
    private readonly AppDbContext _db;

    public StatsService(AppDbContext db) => _db = db;

    /// <summary>Serie de movimiento diario de los últimos <paramref name="days"/> días
    /// (el más antiguo primero). Usa igualdad exacta de fecha (día X y X−1).</summary>
    public async Task<IReadOnlyList<DailyPnlPoint>> GetDailyMovementAsync(
        int leagueId, int days, DateOnly today, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 60);
        var result = new List<DailyPnlPoint>(days);

        var positions = await LoadPositionsAsync(leagueId, ct);
        if (positions.Count == 0)
        {
            for (var i = days - 1; i >= 0; i--)
                result.Add(new DailyPnlPoint(today.AddDays(-i), 0));
            return result;
        }

        // Necesitamos snapshots desde (today − days) [para el X−1 del primer día] hasta hoy.
        var from = today.AddDays(-days);
        var playerIds = positions.Select(p => p.PlayerId).Distinct().ToList();
        var snaps = await _db.PriceSnapshots
            .Where(s => playerIds.Contains(s.PlayerId) && s.Date >= from && s.Date <= today)
            .Select(s => new { s.PlayerId, s.Date, s.MarketValue })
            .ToListAsync(ct);
        var priceAt = snaps.ToDictionary(s => (s.PlayerId, s.Date), s => s.MarketValue);

        for (var i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var prev = day.AddDays(-1);
            long sum = 0;
            foreach (var p in positions)
            {
                if (p.PurchaseDate > day) continue;              // aún no lo tenía
                if (p.SaleDate is { } sd && sd <= day) continue;  // ya vendido ese día
                if (priceAt.TryGetValue((p.PlayerId, day), out var cur) &&
                    priceAt.TryGetValue((p.PlayerId, prev), out var before))
                {
                    sum += cur - before;
                }
            }
            result.Add(new DailyPnlPoint(day, sum));
        }
        return result;
    }

    /// <summary>Holdings activos (posición abierta) + ventas (posición cerrada con
    /// fecha de venta). Los holdings marcados como <c>Sold</c> se ignoran: su
    /// información vive en <see cref="Sale"/> con la <c>SaleDate</c>.</summary>
    private async Task<List<PositionRow>> LoadPositionsAsync(int leagueId, CancellationToken ct)
    {
        var active = await _db.Holdings
            .Where(h => h.LeagueId == leagueId && h.Status == HoldingStatus.Active)
            .Select(h => new PositionRow(h.PlayerId, h.PurchaseDate, (DateOnly?)null))
            .ToListAsync(ct);

        var sold = await _db.Sales
            .Where(s => s.LeagueId == leagueId)
            .Select(s => new PositionRow(s.PlayerId, s.PurchaseDate, (DateOnly?)s.SaleDate))
            .ToListAsync(ct);

        active.AddRange(sold);
        return active;
    }
}
