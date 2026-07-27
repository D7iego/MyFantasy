using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;

namespace MyFantasy.Api.Services;

public class LeagueService
{
    private readonly AppDbContext _db;
    public LeagueService(AppDbContext db) => _db = db;

    /// <summary>
    /// Liga por defecto: la marcada; si ninguna, la primera añadida (menor
    /// CreatedAt). Misma regla en toda la app.
    /// </summary>
    public async Task<League?> GetDefaultLeagueAsync(CancellationToken ct = default)
    {
        var flagged = await _db.Leagues.FirstOrDefaultAsync(l => l.IsDefault, ct);
        return flagged ?? await _db.Leagues.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>Marca una liga como la por defecto y desmarca el resto.</summary>
    public async Task<bool> SetDefaultAsync(int leagueId, CancellationToken ct = default)
    {
        var target = await _db.Leagues.FindAsync([leagueId], ct);
        if (target is null) return false;

        var flagged = await _db.Leagues.Where(l => l.IsDefault && l.Id != leagueId).ToListAsync(ct);
        foreach (var l in flagged) l.IsDefault = false;
        target.IsDefault = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
