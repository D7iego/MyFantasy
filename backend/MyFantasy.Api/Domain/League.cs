namespace MyFantasy.Api.Domain;

/// <summary>
/// Liga del usuario (leída de la API de LaLiga y persistida localmente).
/// La liga por defecto es la marcada con <see cref="IsDefault"/>; si ninguna
/// está marcada y hay varias, la lógica de negocio usa la de menor
/// <see cref="CreatedAt"/> (la primera añadida).
/// </summary>
public class League
{
    public int Id { get; set; }

    /// <summary>ID de la liga en la API de LaLiga.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
