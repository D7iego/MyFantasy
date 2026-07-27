namespace MyFantasy.Api.Domain;

/// <summary>
/// Precio de mercado de un jugador en un día concreto. La API solo devuelve el
/// precio de HOY, así que guardamos un snapshot diario (UPSERT) para poder
/// derivar el "precio de ayer" / "hace 7 días". PK compuesta (PlayerId, Date).
/// </summary>
public class PriceSnapshot
{
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Día del snapshot (sin hora). Los precios de LaLiga se recalculan una vez al día.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Valor de mercado en euros ese día.</summary>
    public long MarketValue { get; set; }
}
