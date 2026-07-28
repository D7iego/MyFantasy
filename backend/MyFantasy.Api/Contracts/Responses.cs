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

public record UpdatePurchasePriceRequest(long PurchasePrice);
public record UpdateSalePriceRequest(long SalePrice);
