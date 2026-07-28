using MyFantasy.Api.Domain;

namespace MyFantasy.Api.Contracts;

public record LeagueResponse(int Id, string ExternalId, string Name, bool IsDefault, DateTime CreatedAt);

/// <summary>Fila de la pestaña "Jugadores" (plantilla activa) e Historial "Sin vender".</summary>
public record HoldingResponse(
    int HoldingId,
    int PlayerId,
    string ExternalId,
    string Name,
    string? Team,
    string Position,
    long? CurrentValue,
    long? DailyDelta,
    long? WeeklyDelta,
    long PurchasePrice,
    long? ProfitLoss,
    bool PurchasePriceIsManual,
    DateOnly PurchaseDate,
    string Status,
    string? ImageUrl);

/// <summary>Fila de la pestaña Historial "Vendidos" — stats congeladas.</summary>
public record SaleResponse(
    int Id,
    int PlayerId,
    string Name,
    string? Team,
    string Position,
    long PurchasePrice,
    long SalePrice,
    long ProfitLoss,
    long? DailyDelta,
    long? WeeklyDelta,
    DateOnly PurchaseDate,
    DateOnly SaleDate,
    bool SalePriceIsManual);

public record StatsResponse(
    long TotalProfitLoss,
    int TotalSales,
    int ProfitableSales,
    double ProfitableRate,
    SaleResponse? BestSale,
    SaleResponse? WorstSale,
    long ActivePortfolioValue,
    long ActiveUnrealizedProfitLoss,
    int ActiveHoldings,
    // Movimiento del valor de mercado de la plantilla hoy vs. ayer (NO plusvalía).
    long TodayMovement,
    // Dinero disponible en el equipo (de la API); null si no se pudo obtener.
    long? AvailableMoney);

/// <summary>Punto del gráfico de barras "movimiento diario" de la pestaña Stats.</summary>
public record DailyPnlResponse(DateOnly Fecha, long Movimiento);

/// <summary>Fila de la pestaña Mercado: jugador en venta hoy + deltas de precio.</summary>
public record MarketRowResponse(
    int? PlayerId,
    string ExternalId,
    string Name,
    string? Team,
    string Position,
    long? CurrentValue,
    long? DailyDelta,
    long? WeeklyDelta,
    long? SalePrice,
    string? ImageUrl);

/// <summary>Fila de la pestaña General: cualquier jugador de la competición con
/// precio, deltas y tendencia (alcista/bajista/estable).</summary>
public record PlayerRowResponse(
    int PlayerId,
    string ExternalId,
    string Name,
    string? Team,
    string? TeamId,
    string Position,
    long? CurrentValue,
    long? DailyDelta,
    long? WeeklyDelta,
    string Trend,
    string? ImageUrl);

/// <summary>Tendencia agregada de un equipo (media de sus jugadores).</summary>
public record TeamAggregateResponse(
    string TeamId,
    int PlayerCount,
    long AvgDailyDelta,
    long AvgWeeklyDelta,
    double AvgDailyPct,
    double AvgWeeklyPct);

/// <summary>Respuesta de <c>GET /api/players/all</c>: jugadores + (si se filtró
/// por equipo) el agregado del equipo.</summary>
public record PlayersOverviewResponse(
    IReadOnlyList<PlayerRowResponse> Players,
    TeamAggregateResponse? TeamAggregate);

/// <summary>Equipo de LaLiga con su escudo, para el filtro visual de la pestaña General.</summary>
public record TeamResponse(string Id, string Name, string? BadgeUrl);

public record UpdatePurchasePriceRequest(long PurchasePrice);
public record UpdateSalePriceRequest(long SalePrice);
