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

// ---- Pestaña Rivales (plantillas de otros managers) ----

/// <summary>Manager de la liga (para el selector de la pestaña Rivales).</summary>
public record RivalManagerResponse(string TeamId, string ManagerName, string? TeamName);

/// <summary>Jugador en la plantilla de un rival: valor, deltas y estado de cláusula.</summary>
public record RivalPlayerResponse(
    int? PlayerId,
    string ExternalId,
    string Name,
    string? Team,
    string Position,
    long? CurrentValue,
    long? DailyDelta,
    long? WeeklyDelta,
    long? BuyoutClause,
    string? BuyoutClauseLockedEndTime,
    bool IsShielded,
    string? ImageUrl);

public record RivalsResponse(
    IReadOnlyList<RivalManagerResponse> Managers,
    string? SelectedTeamId,
    IReadOnlyList<RivalPlayerResponse>? Squad);

// ---- Modal de detalle de jugador ----

/// <summary>Un día del histórico de precios + su variación respecto al día anterior.</summary>
public record PriceHistoryPointResponse(DateOnly Date, long Value, long? Delta);

/// <summary>Rendimiento en una jornada (de playerStats de la API). Campos opcionales
/// porque en pretemporada el array llega vacío y la forma exacta se confirmará en liga.</summary>
public record MatchStatResponse(int? Week, double? Points, int? Goals, int? Assists, int? Minutes);

public record PlayerDetailResponse(
    string ExternalId,
    string Name,
    string? Team,
    string Position,
    string? ImageUrl,
    long? CurrentValue,
    long? DailyDelta,
    long? BuyoutClause,
    string? BuyoutClauseLockedEndTime,
    bool IsShielded,
    double? Points,
    double? AveragePoints,
    IReadOnlyList<PriceHistoryPointResponse> PriceHistory,
    IReadOnlyList<MatchStatResponse> Matches,
    bool SportsAvailable);

public record UpdatePurchasePriceRequest(long PurchasePrice);
public record UpdateSalePriceRequest(long SalePrice);
