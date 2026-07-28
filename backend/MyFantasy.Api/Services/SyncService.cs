using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Fantasy.Dtos;

namespace MyFantasy.Api.Services;

public record SyncResult(
    bool Success,
    string? LeagueName,
    int PlayersSnapshotted,
    int NewHoldings,
    int NewSales,
    int ActiveHoldings,
    string? Warning = null,
    string? Error = null);

/// <summary>
/// Núcleo de la app: sincroniza con la API (fuente de verdad) y ejecuta el diff
/// de plantilla — jugador nuevo en la API = fichaje, jugador que desaparece =
/// venta (con stats congeladas). También guarda el snapshot diario de precios.
/// </summary>
public class SyncService
{
    private readonly AppDbContext _db;
    private readonly IFantasyApiClient _api;
    private readonly ILogger<SyncService> _logger;

    public SyncService(AppDbContext db, IFantasyApiClient api, ILogger<SyncService> logger)
    {
        _db = db;
        _api = api;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken ct = default)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // 1) Ligas + liga por defecto.
            await EnsureLeaguesAsync(ct);
            var league = await GetDefaultLeagueAsync(ct);
            if (league is null)
            {
                return new SyncResult(false, null, 0, 0, 0, 0,
                    Warning: "No hay ligas. ¿Está el token configurado y la cuenta tiene ligas?");
            }

            // 2) Feed de mercado -> upsert jugadores + snapshot de hoy.
            //    El feed no trae el equipo embebido: lo resolvemos con teams-master.
            var teamsMap = await LoadTeamsMapAsync(ct);
            var players = await _api.GetAllPlayersAsync(ct);
            var (playerByExt, snapshotted) = await UpsertPlayersAndSnapshotsAsync(players, today, teamsMap, ct);

            // 3) Resolver el equipo del usuario en la liga por defecto.
            var teamId = await ResolveUserTeamIdAsync(league.ExternalId, ct);
            if (teamId is null)
            {
                var active0 = await _db.Holdings.CountAsync(h => h.LeagueId == league.Id && h.Status == HoldingStatus.Active, ct);
                return new SyncResult(true, league.Name, snapshotted, 0, 0, active0,
                    Warning: "Precios guardados, pero no pude localizar tu equipo en la clasificación (¿token de otra cuenta?). Diff de plantilla omitido.");
            }

            // Cachea el equipo del usuario para consultas posteriores (dinero disponible).
            if (league.TeamId != teamId) league.TeamId = teamId;

            // 4) Plantilla actual + diff.
            var squad = await _api.GetTeamSquadAsync(league.ExternalId, teamId, ct);
            var (newHoldings, newSales) = await DiffSquadAsync(league, squad, playerByExt, today, ct);

            await _db.SaveChangesAsync(ct);

            var active = await _db.Holdings.CountAsync(h => h.LeagueId == league.Id && h.Status == HoldingStatus.Active, ct);
            return new SyncResult(true, league.Name, snapshotted, newHoldings, newSales, active);
        }
        catch (FantasyApiException ex)
        {
            _logger.LogError(ex, "Sync falló por error de la API de LaLiga.");
            return new SyncResult(false, null, 0, 0, 0, 0, Error: ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync falló.");
            return new SyncResult(false, null, 0, 0, 0, 0, Error: ex.Message);
        }
    }

    // ---- Pasos ----

    private async Task EnsureLeaguesAsync(CancellationToken ct)
    {
        var apiLeagues = await _api.GetLeaguesAsync(ct);
        if (apiLeagues.Count == 0) return;

        var existing = await _db.Leagues.ToDictionaryAsync(l => l.ExternalId, ct);
        foreach (var dto in apiLeagues)
        {
            if (string.IsNullOrWhiteSpace(dto.Id)) continue;
            if (existing.TryGetValue(dto.Id!, out var league))
            {
                if (!string.IsNullOrWhiteSpace(dto.Name)) league.Name = dto.Name!;
            }
            else
            {
                _db.Leagues.Add(new League
                {
                    ExternalId = dto.Id!,
                    Name = dto.Name ?? dto.Id!,
                    IsDefault = dto.IsDefault ?? false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Liga marcada por defecto; si ninguna, la primera añadida (menor CreatedAt).</summary>
    private async Task<League?> GetDefaultLeagueAsync(CancellationToken ct)
    {
        var flagged = await _db.Leagues.FirstOrDefaultAsync(l => l.IsDefault, ct);
        return flagged ?? await _db.Leagues.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).FirstOrDefaultAsync(ct);
    }

    private async Task<Dictionary<string, string>> LoadTeamsMapAsync(CancellationToken ct)
    {
        try
        {
            var teams = await _api.GetTeamsMasterAsync(ct);
            var map = new Dictionary<string, string>();
            foreach (var t in teams)
            {
                if (!string.IsNullOrWhiteSpace(t.Id) && !string.IsNullOrWhiteSpace(t.Name))
                    map[t.Id!] = t.Name!;
            }
            return map;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar teams-master; los equipos quedarán sin resolver.");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Resuelve el nombre de equipo por id (del feed o extraído de la URL de foto /tNNN/).</summary>
    private static string? ResolveTeamName(FantasyPlayerDto dto, IReadOnlyDictionary<string, string> teamsMap)
    {
        var teamId = dto.TeamId ?? dto.Team?.Id ?? ExtractTeamIdFromImage(dto.ResolvedImageUrl);
        return teamId != null && teamsMap.TryGetValue(teamId, out var name) ? name : null;
    }

    private static string? ExtractTeamIdFromImage(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(url, @"/t(\d+)/");
        return m.Success ? m.Groups[1].Value : null;
    }

    private async Task<(Dictionary<string, Player> ByExt, int Snapshotted)> UpsertPlayersAndSnapshotsAsync(
        IReadOnlyList<FantasyPlayerDto> players, DateOnly today, IReadOnlyDictionary<string, string> teamsMap, CancellationToken ct)
    {
        var byExt = await _db.Players.ToDictionaryAsync(p => p.ExternalId, ct);

        // Alta/actualización de jugadores.
        foreach (var dto in players)
        {
            if (string.IsNullOrWhiteSpace(dto.Id)) continue;
            if (!byExt.TryGetValue(dto.Id!, out var player))
            {
                player = new Player { ExternalId = dto.Id! };
                _db.Players.Add(player);
                byExt[dto.Id!] = player;
            }
            player.Name = dto.DisplayName ?? player.Name;
            var resolvedTeam = dto.ResolvedTeamName ?? ResolveTeamName(dto, teamsMap);
            if (!string.IsNullOrWhiteSpace(resolvedTeam)) player.Team = resolvedTeam;
            player.Position = PositionExtensions.FromApiId(dto.PositionId);
            if (!string.IsNullOrWhiteSpace(dto.ResolvedImageUrl)) player.ImageUrl = dto.ResolvedImageUrl;
        }
        await _db.SaveChangesAsync(ct); // materializa ids de los nuevos jugadores

        // Snapshot de hoy (UPSERT por PK compuesta).
        var todaySnaps = await _db.PriceSnapshots
            .Where(s => s.Date == today)
            .ToDictionaryAsync(s => s.PlayerId, ct);

        int count = 0;
        foreach (var dto in players)
        {
            if (string.IsNullOrWhiteSpace(dto.Id) || dto.MarketValue is null) continue;
            if (!byExt.TryGetValue(dto.Id!, out var player)) continue;

            if (todaySnaps.TryGetValue(player.Id, out var snap))
            {
                snap.MarketValue = dto.MarketValue.Value;
            }
            else
            {
                var newSnap = new PriceSnapshot { PlayerId = player.Id, Date = today, MarketValue = dto.MarketValue.Value };
                _db.PriceSnapshots.Add(newSnap);
                todaySnaps[player.Id] = newSnap;
            }
            count++;
        }
        await _db.SaveChangesAsync(ct);

        return (byExt, count);
    }

    private async Task<string?> ResolveUserTeamIdAsync(string leagueExternalId, CancellationToken ct)
    {
        var me = await _api.GetCurrentUserAsync(ct);
        var myId = me?.AnyId;

        var standing = await _api.GetLeagueStandingAsync(leagueExternalId, ct);

        // Con userId: buscamos la entrada cuyo manager/usuario coincide.
        if (!string.IsNullOrWhiteSpace(myId))
        {
            foreach (var e in standing)
            {
                var entryUser = e.UserId ?? e.Team?.UserId ?? e.Team?.Manager?.Id;
                if (entryUser is not null && entryUser == myId)
                {
                    return e.Team?.Id ?? e.Id;
                }
            }
        }

        // Sin userId fiable y liga de un solo equipo: úsalo.
        if (standing.Count == 1)
        {
            return standing[0].Team?.Id ?? standing[0].Id;
        }

        _logger.LogWarning("No pude localizar el equipo del usuario (myId={MyId}, equipos={Count}).", myId, standing.Count);
        return null;
    }

    private async Task<(int NewHoldings, int NewSales)> DiffSquadAsync(
        League league, TeamSquadDto? squad, Dictionary<string, Player> playerByExt, DateOnly today, CancellationToken ct)
    {
        var squadPlayers = squad?.Players ?? new List<SquadPlayerDto>();
        var ownedExtIds = squadPlayers
            .Select(sp => sp.PlayerMaster?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet();

        var activeHoldings = await _db.Holdings
            .Include(h => h.Player)
            .Where(h => h.LeagueId == league.Id && h.Status == HoldingStatus.Active)
            .ToListAsync(ct);
        var activeByExt = activeHoldings
            .Where(h => h.Player is not null)
            .ToDictionary(h => h.Player!.ExternalId, h => h);

        // Últimos precios conocidos (para fallback de compra y precio de venta).
        var lastPrices = await LoadLatestPricesAsync(playerByExt.Values.Select(p => p.Id).ToList(), ct);

        int newHoldings = 0, newSales = 0;

        // A) Fichajes: en la API, no en holdings activos.
        foreach (var sp in squadPlayers)
        {
            var extId = sp.PlayerMaster?.Id;
            if (string.IsNullOrWhiteSpace(extId) || activeByExt.ContainsKey(extId!)) continue;

            var player = await EnsurePlayerAsync(sp, playerByExt, ct);
            var lastPrice = lastPrices.TryGetValue(player.Id, out var lp) ? lp.MarketValue : (long?)null;
            var apiPrice = sp.ResolvedPurchasePrice;
            var purchasePrice = apiPrice ?? sp.MarketValue ?? lastPrice ?? 0;

            _db.Holdings.Add(new Holding
            {
                PlayerId = player.Id,
                LeagueId = league.Id,
                PurchasePrice = purchasePrice,
                PurchaseDate = today,
                Status = HoldingStatus.Active,
                PurchasePriceIsManual = apiPrice is null
            });
            newHoldings++;
        }

        // B) Ventas: holdings activos que ya no están en la API.
        foreach (var holding in activeHoldings)
        {
            var extId = holding.Player?.ExternalId;
            if (extId is not null && ownedExtIds.Contains(extId)) continue;

            var deltas = DeltaService.Compute(
                (await GetSnapshotsDescAsync(holding.PlayerId, ct)));
            var salePrice = deltas.CurrentValue ?? holding.PurchasePrice;

            _db.Sales.Add(new Sale
            {
                PlayerId = holding.PlayerId,
                LeagueId = league.Id,
                PurchasePrice = holding.PurchasePrice,
                SalePrice = salePrice,
                PurchaseDate = holding.PurchaseDate,
                SaleDate = today,
                ProfitLoss = salePrice - holding.PurchasePrice,
                DailyDelta = deltas.DailyDelta,
                WeeklyDelta = deltas.WeeklyDelta,
                SalePriceIsManual = false
            });
            holding.Status = HoldingStatus.Sold;
            newSales++;
        }

        return (newHoldings, newSales);
    }

    private async Task<Player> EnsurePlayerAsync(SquadPlayerDto sp, Dictionary<string, Player> byExt, CancellationToken ct)
    {
        var extId = sp.PlayerMaster!.Id!;
        if (byExt.TryGetValue(extId, out var existing)) return existing;

        var player = new Player
        {
            ExternalId = extId,
            Name = sp.PlayerMaster.Nickname ?? sp.PlayerMaster.Name ?? extId,
            Team = sp.PlayerMaster.Team?.Name,
            Position = PositionExtensions.FromApiId(sp.PlayerMaster.PositionId ?? sp.PositionId),
            ImageUrl = sp.PlayerMaster.ResolvedImageUrl
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);
        byExt[extId] = player;
        return player;
    }

    private async Task<Dictionary<int, PriceSnapshot>> LoadLatestPricesAsync(IReadOnlyCollection<int> playerIds, CancellationToken ct)
    {
        if (playerIds.Count == 0) return new();
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
        var rows = await _db.PriceSnapshots
            .Where(s => playerIds.Contains(s.PlayerId) && s.Date >= cutoff)
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).First());
    }

    private async Task<List<(DateOnly Date, long Value)>> GetSnapshotsDescAsync(int playerId, CancellationToken ct)
    {
        var rows = await _db.PriceSnapshots
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.Date)
            .Take(60)
            .Select(s => new { s.Date, s.MarketValue })
            .ToListAsync(ct);
        return rows.Select(r => (r.Date, r.MarketValue)).ToList();
    }
}
