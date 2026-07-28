using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Data;
using MyFantasy.Api.Domain;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Services;

namespace MyFantasy.Api.Controllers;

/// <summary>
/// Pestaña Mercado — jugadores en venta hoy en la liga por defecto, con su
/// evolución de precio. El feed de venta viene de la API; los deltas se calculan
/// con nuestros PriceSnapshots (que ya guardan a TODOS los jugadores de la
/// competición en el sync), casando por id externo.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LeagueService _leagues;
    private readonly DeltaService _deltas;
    private readonly IFantasyApiClient _api;

    public MarketController(AppDbContext db, LeagueService leagues, DeltaService deltas, IFantasyApiClient api)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
        _api = api;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MarketRowResponse>>> Get(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null) return Ok(Array.Empty<MarketRowResponse>());

        var items = await _api.GetMarketAsync(league.ExternalId, ct);

        // Una entrada por jugador (por si el feed repite).
        var byExt = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ResolvedExternalId))
            .GroupBy(i => i.ResolvedExternalId!)
            .ToDictionary(g => g.Key, g => g.First());

        // Jugadores internos para resolver PlayerId + datos consistentes con el resto de la app.
        var extIds = byExt.Keys.ToList();
        var players = await _db.Players
            .Where(p => extIds.Contains(p.ExternalId))
            .ToListAsync(ct);
        var playerByExt = players.ToDictionary(p => p.ExternalId);

        var deltas = await _deltas.GetDeltasBulkAsync(players.Select(p => p.Id).ToList(), ct);

        var rows = byExt.Values.Select(item =>
        {
            var ext = item.ResolvedExternalId!;
            playerByExt.TryGetValue(ext, out var player);

            PriceDeltas? d = null;
            if (player is not null) deltas.TryGetValue(player.Id, out d);

            var name = player?.Name ?? item.ResolvedName ?? ext;
            var team = player?.Team ?? item.ResolvedTeamName;
            var position = (player?.Position ?? PositionExtensions.FromApiId(item.ResolvedPositionId)).ToSpanish();
            var current = d?.CurrentValue ?? item.ResolvedMarketValue;
            var image = player?.ImageUrl ?? item.ResolvedImageUrl;

            return new MarketRowResponse(
                PlayerId: player?.Id,
                ExternalId: ext,
                Name: name,
                Team: team,
                Position: position,
                CurrentValue: current,
                DailyDelta: d?.DailyDelta,
                WeeklyDelta: d?.WeeklyDelta,
                SalePrice: item.SalePrice,
                ImageUrl: image);
        })
        .OrderByDescending(r => r.DailyDelta ?? long.MinValue)
        .ToList();

        return Ok(rows);
    }
}
