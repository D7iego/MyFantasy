using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Data;

namespace MyFantasy.Api.Services;

/// <summary>Precio actual + deltas + tendencia de un jugador (pestaña General).</summary>
public record PlayerMetrics(long? CurrentValue, long? DailyDelta, long? WeeklyDelta, string Trend);

/// <summary>
/// Calcula, en bloque, precio/deltas/tendencia de varios jugadores a partir de
/// los PriceSnapshots (los mismos datos que ya guarda el sync para TODA la
/// competición). Reutiliza <see cref="DeltaService.Compute"/> para los deltas.
/// </summary>
public class PlayerOverviewService
{
    private readonly AppDbContext _db;

    public PlayerOverviewService(AppDbContext db) => _db = db;

    public async Task<Dictionary<int, PlayerMetrics>> GetMetricsBulkAsync(
        IReadOnlyCollection<int> playerIds, CancellationToken ct = default)
    {
        if (playerIds.Count == 0) return new();

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
        var rows = await _db.PriceSnapshots
            .Where(s => playerIds.Contains(s.PlayerId) && s.Date >= cutoff)
            .Select(s => new { s.PlayerId, s.Date, s.MarketValue })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.PlayerId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var desc = g.OrderByDescending(x => x.Date).Select(x => (x.Date, x.MarketValue)).ToList();
                    var d = DeltaService.Compute(desc);
                    return new PlayerMetrics(d.CurrentValue, d.DailyDelta, d.WeeklyDelta, ComputeTrend(desc));
                });
    }

    /// <summary>
    /// Tendencia: "alcista"/"bajista" si las 3 últimas variaciones diarias van
    /// TODAS en el mismo sentido; "estable" en cualquier otro caso (o si no hay
    /// aún suficiente histórico).
    /// </summary>
    public static string ComputeTrend(IReadOnlyList<(DateOnly Date, long Value)> snapshotsDesc)
    {
        const int need = 3;
        if (snapshotsDesc.Count < need + 1) return "estable";

        int up = 0, down = 0;
        for (var i = 0; i < need; i++)
        {
            var diff = snapshotsDesc[i].Value - snapshotsDesc[i + 1].Value;
            if (diff > 0) up++;
            else if (diff < 0) down++;
        }
        if (up == need) return "alcista";
        if (down == need) return "bajista";
        return "estable";
    }
}
