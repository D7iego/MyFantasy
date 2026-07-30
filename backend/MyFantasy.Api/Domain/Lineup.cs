namespace MyFantasy.Api.Domain;

/// <summary>
/// Alineación guardada por el usuario (planificador local). No toca el juego
/// oficial: es un tablero personal. La formación es "DEF-MID-DEL" (p. ej.
/// "4-3-3") y <see cref="Data"/> es un JSON opaco con las asignaciones de hueco
/// a jugador (por externalId) que gestiona el frontend.
/// </summary>
public class Lineup
{
    public int Id { get; set; }

    public int LeagueId { get; set; }
    public League? League { get; set; }

    public string Name { get; set; } = "Mi alineación";
    public string Formation { get; set; } = "4-3-3";

    /// <summary>JSON de asignaciones hueco→jugador (opaco para el backend).</summary>
    public string Data { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
