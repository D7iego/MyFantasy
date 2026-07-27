namespace MyFantasy.Api.Domain;

public enum HoldingStatus
{
    Active = 0,
    Sold = 1
}

/// <summary>
/// Jugador que tengo (o tuve) en mi plantilla. Dato "personal de trading". Se
/// crea automáticamente cuando el diff detecta un jugador nuevo en la API que
/// no tenía; pasa a <see cref="HoldingStatus.Sold"/> (y genera un
/// <see cref="Sale"/>) cuando desaparece de la API.
/// </summary>
public class Holding
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int LeagueId { get; set; }
    public League? League { get; set; }

    /// <summary>
    /// Precio de compra. Preferentemente de la API (buyoutClause/purchasePrice
    /// del registro de plantilla); si la API no lo trae, editable manualmente.
    /// </summary>
    public long PurchasePrice { get; set; }

    public DateOnly PurchaseDate { get; set; }

    public HoldingStatus Status { get; set; } = HoldingStatus.Active;

    /// <summary>true si el precio de compra se introdujo a mano (no vino de la API).</summary>
    public bool PurchasePriceIsManual { get; set; }
}
