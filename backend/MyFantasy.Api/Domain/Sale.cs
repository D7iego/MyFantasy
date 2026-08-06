namespace MyFantasy.Api.Domain;

/// <summary>
/// Operación cerrada (jugador vendido). Guarda las stats CONGELADAS del momento
/// de la venta: precio de venta, G/P final y deltas de ese día. Estos valores
/// son fijos y NO se recalculan al cambiar el mercado.
/// </summary>
public class Sale
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int LeagueId { get; set; }
    public League? League { get; set; }

    public long PurchasePrice { get; set; }

    /// <summary>
    /// Precio de venta. Por defecto el último precio de mercado conocido
    /// (snapshot) al detectar la venta; editable si se vendió a otro precio.
    /// </summary>
    public long SalePrice { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public DateOnly SaleDate { get; set; }

    /// <summary>Temporada en que se cerró la operación ("2026/27").</summary>
    public string Season { get; set; } = string.Empty;

    // ---- Stats congeladas en el momento de la venta ----

    /// <summary>Ganancia/pérdida final (SalePrice - PurchasePrice).</summary>
    public long ProfitLoss { get; set; }

    /// <summary>Variación del precio respecto al día anterior a la venta.</summary>
    public long? DailyDelta { get; set; }

    /// <summary>Variación del precio respecto a 7 días antes de la venta.</summary>
    public long? WeeklyDelta { get; set; }

    /// <summary>true si el precio de venta se ajustó a mano.</summary>
    public bool SalePriceIsManual { get; set; }
}
