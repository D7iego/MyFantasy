namespace MyFantasy.Api.Domain;

/// <summary>
/// Jugador de LaLiga. Dato de "mercado" (igual para todos): el precio actual NO
/// se guarda aquí sino en <see cref="PriceSnapshot"/> (uno por día) para poder
/// calcular deltas diarios/semanales.
/// </summary>
public class Player
{
    public int Id { get; set; }

    /// <summary>ID del jugador en la API de LaLiga (playerMaster.id).</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Team { get; set; }

    public Position Position { get; set; } = Position.Unknown;

    /// <summary>Última URL de imagen conocida (opcional, para el frontend).</summary>
    public string? ImageUrl { get; set; }

    public ICollection<PriceSnapshot> PriceSnapshots { get; set; } = new List<PriceSnapshot>();
}
