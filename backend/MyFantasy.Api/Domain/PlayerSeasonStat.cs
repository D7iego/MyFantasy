namespace MyFantasy.Api.Domain;

/// <summary>
/// Resumen de un jugador en una temporada: su identidad de ese año (equipo,
/// posición — que pueden cambiar entre temporadas) y agregados de valor y
/// rendimiento. Es la "foto por temporada" lista para comparar años.
/// PK compuesta (PlayerId, Season). Valores en euros; agregados de rendimiento
/// derivables de <see cref="PlayerMatchStat"/> pero cacheados aquí para comparar
/// sin recomputar.
/// </summary>
public class PlayerSeasonStat
{
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Temporada, p. ej. "2026/27".</summary>
    public string Season { get; set; } = string.Empty;

    // ---- Identidad del jugador ESA temporada (puede diferir de la actual) ----
    public string? Team { get; set; }
    public string? TeamId { get; set; }
    public Position Position { get; set; } = Position.Unknown;

    // ---- Agregados de rendimiento (de PlayerMatchStat / PlayerMaster) ----
    public double? TotalPoints { get; set; }
    public int? Goals { get; set; }
    public int? Assists { get; set; }
    public int? Minutes { get; set; }

    // ---- Valor de mercado en la temporada (de PriceSnapshots) ----
    /// <summary>Valor al inicio de la temporada (primer snapshot conocido).</summary>
    public long? StartValue { get; set; }
    /// <summary>Último valor conocido en la temporada.</summary>
    public long? EndValue { get; set; }
    /// <summary>Valor máximo alcanzado en la temporada.</summary>
    public long? PeakValue { get; set; }

    public DateTime UpdatedAt { get; set; }
}
