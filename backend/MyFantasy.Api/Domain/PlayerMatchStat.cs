namespace MyFantasy.Api.Domain;

/// <summary>
/// Rendimiento de un jugador en una jornada concreta (goles, asistencias, minutos,
/// puntos Fantasy) + datos del partido. La API de LaLiga solo expone la temporada
/// en curso, así que guardamos cada jornada (UPSERT) para conservar el histórico
/// entre temporadas y poder abrir el modal sin depender de la API.
/// PK compuesta (PlayerId, Season, Week).
/// </summary>
public class PlayerMatchStat
{
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Temporada en formato "2026/27" (ver <c>SeasonUtil</c>).</summary>
    public string Season { get; set; } = string.Empty;

    /// <summary>Jornada (weekNumber).</summary>
    public int Week { get; set; }

    public double? Points { get; set; }
    public int? Goals { get; set; }
    public int? Assists { get; set; }
    public int? Minutes { get; set; }

    // ---- Datos del partido (opcionales: la API puede no traerlos). ----
    /// <summary>Nombre/abreviatura del equipo local.</summary>
    public string? HomeTeam { get; set; }
    /// <summary>Nombre/abreviatura del equipo visitante.</summary>
    public string? AwayTeam { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    /// <summary>True si el jugador jugó como local (para resaltar su equipo).</summary>
    public bool? IsHome { get; set; }

    /// <summary>Última vez que se refrescó desde la API.</summary>
    public DateTime UpdatedAt { get; set; }
}
