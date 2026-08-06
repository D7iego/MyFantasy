using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Fantasy.Dtos;
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
    private readonly IFantasyApiClient _api;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(AppDbContext db, LeagueService leagues, DeltaService deltas,
        PlayerOverviewService overview, IFantasyApiClient api, ILogger<PlayersController> logger)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
        _overview = overview;
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HoldingResponse>>> Get(CancellationToken ct)
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

    /// <summary>
    /// Detalle de un jugador para el modal: histórico de precios (local, seguro)
    /// + parte deportiva (API, degradando con gracia si falla o está vacía).
    /// </summary>
    [HttpGet("{id:int}/detail")]
    public async Task<ActionResult<PlayerDetailResponse>> Detail(int id, CancellationToken ct)
    {
        var player = await _db.Players.FindAsync([id], ct);
        if (player is null) return NotFound(new { error = "Jugador no encontrado" });

        // Histórico de precios (desc: el más reciente primero) con delta día a día.
        var snaps = await _db.PriceSnapshots
            .Where(s => s.PlayerId == id)
            .OrderByDescending(s => s.Date)
            .Take(120)
            .Select(s => new { s.Date, s.MarketValue })
            .ToListAsync(ct);

        var history = new List<PriceHistoryPointResponse>(snaps.Count);
        for (var i = 0; i < snaps.Count; i++)
        {
            long? delta = i + 1 < snaps.Count ? snaps[i].MarketValue - snaps[i + 1].MarketValue : null;
            history.Add(new PriceHistoryPointResponse(snaps[i].Date, snaps[i].MarketValue, delta));
        }

        long? currentValue = snaps.Count > 0 ? snaps[0].MarketValue : null;
        long? todayDelta = history.Count > 0 ? history[0].Delta : null;

        // Parte deportiva: requiere liga; si algo falla, se degrada.
        PlayerDetailApiDto? detail = null;
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is not null)
        {
            try
            {
                detail = await _api.GetPlayerDetailAsync(player.ExternalId, league.ExternalId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Detalle deportivo no disponible para {ExternalId}.", player.ExternalId);
            }
        }

        // Persistimos en BD lo que traiga la API (UPSERT) para conservar histórico
        // entre temporadas y poder responder aunque la API falle en el futuro.
        var season = SeasonUtil.Current();
        var fresh = detail?.PlayerMaster?.PlayerStats ?? new();
        if (fresh.Count > 0)
        {
            await UpsertMatchStatsAsync(id, season, fresh, ct);
        }

        // Leemos de BD (fuente de verdad): jornadas de la temporada en curso, desc.
        var stored = await _db.PlayerMatchStats
            .Where(m => m.PlayerId == id && m.Season == season)
            .OrderByDescending(m => m.Week)
            .ToListAsync(ct);

        var matches = stored
            .Select(m => new MatchStatResponse(
                m.Week, m.Points, m.Goals, m.Assists, m.Minutes,
                m.HomeTeam, m.AwayTeam, m.HomeGoals, m.AwayGoals, m.IsHome))
            .ToList();

        // Cachea el resumen de temporada del jugador (identidad + agregados) para
        // poder comparar temporadas sin recomputar. El sync ya rellena equipo/valores;
        // aquí completamos los agregados de rendimiento cuando hay partidos.
        if (matches.Count > 0)
        {
            var ss = await _db.PlayerSeasonStats.FindAsync([id, season], ct);
            if (ss is null)
            {
                ss = new PlayerSeasonStat { PlayerId = id, Season = season };
                _db.PlayerSeasonStats.Add(ss);
            }
            ss.Team = player.Team;
            ss.TeamId = player.TeamId;
            ss.Position = player.Position;
            ss.Goals = matches.Sum(m => m.Goals ?? 0);
            ss.Assists = matches.Sum(m => m.Assists ?? 0);
            ss.Minutes = matches.Sum(m => m.Minutes ?? 0);
            ss.TotalPoints = detail?.PlayerMaster?.Points ?? matches.Sum(m => m.Points ?? 0);
            ss.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        // Marcas de compra/venta del usuario en la liga por defecto (dato local),
        // para señalarlas junto a la fecha del histórico de precios.
        var trades = new List<PlayerTradeMarkerResponse>();
        if (league is not null)
        {
            var holdings = await _db.Holdings
                .Where(h => h.PlayerId == id && h.LeagueId == league.Id && h.Status == HoldingStatus.Active)
                .Select(h => new { h.PurchaseDate, h.PurchasePrice })
                .ToListAsync(ct);
            var salesList = await _db.Sales
                .Where(s => s.PlayerId == id && s.LeagueId == league.Id)
                .Select(s => new { s.PurchaseDate, s.PurchasePrice, s.SaleDate, s.SalePrice })
                .ToListAsync(ct);

            foreach (var h in holdings)
                trades.Add(new PlayerTradeMarkerResponse(h.PurchaseDate, "buy", h.PurchasePrice));
            foreach (var s in salesList)
            {
                trades.Add(new PlayerTradeMarkerResponse(s.PurchaseDate, "buy", s.PurchasePrice));
                trades.Add(new PlayerTradeMarkerResponse(s.SaleDate, "sell", s.SalePrice));
            }
        }

        var pt = detail?.PlayerTeam;

        return Ok(new PlayerDetailResponse(
            ExternalId: player.ExternalId,
            Name: player.Name,
            Team: player.Team ?? detail?.PlayerMaster?.Team?.Name,
            Position: player.Position.ToSpanish(),
            ImageUrl: player.ImageUrl ?? detail?.PlayerMaster?.ResolvedImageUrl,
            CurrentValue: currentValue ?? detail?.PlayerMaster?.MarketValue,
            DailyDelta: todayDelta,
            BuyoutClause: pt?.BuyoutClause,
            BuyoutClauseLockedEndTime: pt?.BuyoutClauseLockedEndTime,
            IsShielded: pt?.IsShielded ?? false,
            Points: detail?.PlayerMaster?.Points,
            AveragePoints: detail?.PlayerMaster?.AveragePoints,
            PriceHistory: history,
            Matches: matches,
            SportsAvailable: matches.Count > 0,
            Season: season,
            Trades: trades));
    }

    /// <summary>UPSERT de las stats por jornada que trae la API en la BD local.</summary>
    private async Task UpsertMatchStatsAsync(int playerId, string season, List<PlayerStatDto> stats, CancellationToken ct)
    {
        var existing = await _db.PlayerMatchStats
            .Where(m => m.PlayerId == playerId && m.Season == season)
            .ToDictionaryAsync(m => m.Week, ct);

        var now = DateTime.UtcNow;
        foreach (var s in stats)
        {
            var week = s.ResolvedWeek;
            // Sin jornada no hay clave estable; y en pretemporada el array viene vacío.
            if (week is null) continue;

            if (!existing.TryGetValue(week.Value, out var row))
            {
                row = new PlayerMatchStat { PlayerId = playerId, Season = season, Week = week.Value };
                _db.PlayerMatchStats.Add(row);
                existing[week.Value] = row;
            }

            row.Points = s.ResolvedPoints;
            row.Goals = s.ResolvedGoals;
            row.Assists = s.Assists;
            row.Minutes = s.ResolvedMinutes;
            row.HomeTeam = s.ResolvedHomeTeam;
            row.AwayTeam = s.ResolvedAwayTeam;
            row.HomeGoals = s.ResolvedHomeGoals;
            row.AwayGoals = s.ResolvedAwayGoals;
            row.IsHome = s.ResolvedIsHome;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Editar manualmente el precio de compra (fallback si la API no lo trae).</summary>
    [HttpPut("holdings/{id:int}/purchase-price")]
    public async Task<IActionResult> UpdatePurchasePrice(int id, [FromBody] UpdatePurchasePriceRequest body, CancellationToken ct)
    {
        var holding = await _db.Holdings.FindAsync([id], ct);
        if (holding is null) return NotFound(new { error = "Holding no encontrado" });
        if (body.PurchasePrice < 0) return BadRequest(new { error = "El precio de compra no puede ser negativo" });

        holding.PurchasePrice = body.PurchasePrice;
        // Confirmado por el usuario: no volver a estimarlo ni pisarlo en el sync.
        holding.PurchasePriceIsManual = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
