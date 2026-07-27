using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Data;

namespace MyFantasy.Api.Services;

public record PriceDeltas(long? CurrentValue, long? DailyDelta, long? WeeklyDelta, DateOnly? AsOf);

/// <summary>
/// Calcula variaciones de precio diarias/semanales a partir de los snapshots.
/// El "precio de ayer" solo existe si se capturó; por eso se compara contra el
/// snapshot más reciente ANTERIOR al último, y contra el más reciente con fecha
/// &lt;= (últimoDía - 7), no contra una fecha exacta (evita huecos).
/// </summary>
public class DeltaService
{
    private readonly AppDbContext _db;

    public DeltaService(AppDbContext db) => _db = db;

    /// <summary>Deltas para un solo jugador (por PlayerId interno).</summary>
    public async Task<PriceDeltas> GetDeltasAsync(int playerId, CancellationToken ct = default)
    {
        var snaps = await _db.PriceSnapshots
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.Date)
            .Take(60)
            .Select(s => new { s.Date, s.MarketValue })
            .ToListAsync(ct);

        return Compute(snaps.Select(s => (s.Date, s.MarketValue)).ToList());
    }

    /// <summary>Deltas en bloque para varios jugadores (una sola consulta).</summary>
    public async Task<Dictionary<int, PriceDeltas>> GetDeltasBulkAsync(
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
                g => Compute(g.OrderByDescending(x => x.Date).Select(x => (x.Date, x.MarketValue)).ToList()));
    }

    /// <summary>
    /// Núcleo puro y testeable: recibe snapshots ordenados por fecha DESC.
    /// </summary>
    public static PriceDeltas Compute(IReadOnlyList<(DateOnly Date, long Value)> snapshotsDesc)
    {
        if (snapshotsDesc.Count == 0) return new PriceDeltas(null, null, null, null);

        var latest = snapshotsDesc[0];

        // Diario: snapshot inmediatamente anterior al último.
        long? daily = snapshotsDesc.Count > 1
            ? latest.Value - snapshotsDesc[1].Value
            : null;

        // Semanal: primer snapshot con fecha <= último - 7 días.
        var weekTarget = latest.Date.AddDays(-7);
        long? weekly = null;
        foreach (var s in snapshotsDesc)
        {
            if (s.Date <= weekTarget)
            {
                weekly = latest.Value - s.Value;
                break;
            }
        }

        return new PriceDeltas(latest.Value, daily, weekly, latest.Date);
    }
}
