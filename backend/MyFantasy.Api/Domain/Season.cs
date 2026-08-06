namespace MyFantasy.Api.Domain;

/// <summary>
/// Temporada de LaLiga Fantasy. Tabla auxiliar que marca los límites de cada
/// temporada y cuál está en curso, para separar y comparar datos entre años.
/// La clave es la etiqueta ("2026/27"), consistente con <c>SeasonUtil</c> y con
/// <see cref="PlayerMatchStat.Season"/> / <see cref="PlayerSeasonStat.Season"/>.
/// </summary>
public class Season
{
    /// <summary>Etiqueta y clave, p. ej. "2026/27".</summary>
    public string Label { get; set; } = string.Empty;

    public DateOnly StartsOn { get; set; }

    /// <summary>Fecha de cierre; null mientras la temporada está en curso.</summary>
    public DateOnly? EndsOn { get; set; }

    /// <summary>Solo una temporada es la actual en cada momento.</summary>
    public bool IsCurrent { get; set; }
}
