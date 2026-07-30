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
    private readonly BidSuggestionService _bids;

    public MarketController(AppDbContext db, LeagueService leagues, DeltaService deltas,
        IFantasyApiClient api, BidSuggestionService bids)
    {
        _db = db;
        _leagues = leagues;
        _deltas = deltas;
        _api = api;
        _bids = bids;
    }

    [HttpGet]
    public async Task<ActionResult<MarketResponse>> Get(CancellationToken ct)
    {
        var league = await _leagues.GetDefaultLeagueAsync(ct);
        if (league is null)
            return Ok(new MarketResponse(Array.Empty<MarketRowResponse>(), Array.Empty<ForSaleRowResponse>()));

        // 1) Mis jugadores listados (de mi plantilla), para el bloque "En venta"
        //    y para excluirlos del mercado general.
        var myListed = new List<SquadPlayerDto>();
        if (!string.IsNullOrWhiteSpace(league.TeamId))
        {
            var squad = await _api.GetTeamSquadAsync(league.ExternalId, league.TeamId!, ct);
            myListed = (squad?.Players ?? new())
                .Where(p => p.PlayerMarket?.SalePrice != null && !string.IsNullOrWhiteSpace(p.PlayerMaster?.Id))
                .ToList();
        }
        var myExtIds = myListed.Select(p => p.PlayerMaster!.Id!).ToHashSet();

        // 2) Mercado general: SOLO lo que vende la liga (agentes libres,
        //    marketPlayerLeague). Se excluyen los listados por managers/rivales
        //    (marketPlayerTeam) y, por tanto, también mis propios listados.
        var items = await _api.GetMarketAsync(league.ExternalId, ct);
        var byExt = items
            .Where(i => !i.IsRivalSale
                        && !string.IsNullOrWhiteSpace(i.ResolvedExternalId)
                        && !myExtIds.Contains(i.ResolvedExternalId!))
            .GroupBy(i => i.ResolvedExternalId!)
            .ToDictionary(g => g.Key, g => g.First());

        // 3) Jugadores internos + deltas para todo (mercado + mis ventas).
        var allExtIds = byExt.Keys.Concat(myExtIds).Distinct().ToList();
        var players = await _db.Players.Where(p => allExtIds.Contains(p.ExternalId)).ToListAsync(ct);
        var playerByExt = players.ToDictionary(p => p.ExternalId);
        var deltas = await _deltas.GetDeltasBulkAsync(players.Select(p => p.Id).ToList(), ct);

        PriceDeltas? DeltaFor(Player? p)
        {
            if (p is null) return null;
            deltas.TryGetValue(p.Id, out var d);
            return d;
        }

        var market = byExt.Values.Select(item =>
        {
            var ext = item.ResolvedExternalId!;
            playerByExt.TryGetValue(ext, out var player);
            var d = DeltaFor(player);
            return new MarketRowResponse(
                player?.Id, ext,
                player?.Name ?? item.ResolvedName ?? ext,
                player?.Team ?? item.ResolvedTeamName,
                (player?.Position ?? PositionExtensions.FromApiId(item.ResolvedPositionId)).ToSpanish(),
                d?.CurrentValue ?? item.ResolvedMarketValue,
                d?.DailyDelta, d?.WeeklyDelta, item.SalePrice,
                player?.ImageUrl ?? item.ResolvedImageUrl);
        })
        .OrderByDescending(r => r.DailyDelta ?? long.MinValue)
        .ToList();

        // 3b) Puja sugerida (heurística) para todo el mercado a la vez.
        var bids = await _bids.BuildAsync(
            market.Select(m => new MarketMember(m.ExternalId, m.CurrentValue, m.WeeklyDelta)).ToList(), ct);
        market = market
            .Select(m => bids.TryGetValue(m.ExternalId, out var b) ? m with { Bid = b } : m)
            .ToList();

        // 4) "En venta": mis listados con la mejor oferta actual y su %.
        var forSale = new List<ForSaleRowResponse>();
        foreach (var sp in myListed)
        {
            var ext = sp.PlayerMaster!.Id!;
            playerByExt.TryGetValue(ext, out var player);
            var d = DeltaFor(player);
            var salePrice = sp.PlayerMarket!.SalePrice!.Value;

            long? bestOffer = null;
            if (!string.IsNullOrWhiteSpace(sp.PlayerTeamId))
            {
                try
                {
                    var offers = await _api.GetPlayerOffersAsync(league.ExternalId, sp.PlayerTeamId!, ct);
                    bestOffer = offers
                        .Where(o => o.Money is not null && (o.Status is null || o.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)))
                        .Select(o => o.Money!.Value)
                        .DefaultIfEmpty()
                        .Max();
                    if (bestOffer == 0) bestOffer = null;
                }
                catch { /* degradar: sin oferta */ }
            }

            long? diff = bestOffer is null ? null : bestOffer - salePrice;
            double? pct = bestOffer is null || salePrice <= 0 ? null : Math.Round((double)(bestOffer.Value - salePrice) / salePrice * 100, 1);

            forSale.Add(new ForSaleRowResponse(
                player?.Id, ext,
                player?.Name ?? sp.PlayerMaster.Nickname ?? sp.PlayerMaster.Name ?? ext,
                player?.Team ?? sp.PlayerMaster.Team?.Name,
                (player?.Position ?? PositionExtensions.FromApiId(sp.PlayerMaster.PositionId ?? sp.PositionId)).ToSpanish(),
                d?.CurrentValue, salePrice, sp.PlayerMarket.NumberOfOffers ?? 0,
                bestOffer, diff, pct,
                player?.ImageUrl ?? sp.PlayerMaster.ResolvedImageUrl));
        }
        forSale = forSale.OrderByDescending(r => r.OfferPct ?? double.MinValue).ToList();

        return Ok(new MarketResponse(market, forSale));
    }
}
