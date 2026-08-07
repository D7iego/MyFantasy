namespace MyFantasy.Api.Services;

/// <summary>Etiqueta de temporada de LaLiga (arranca en agosto).</summary>
public static class SeasonUtil
{
    /// <summary>Temporada en curso para una fecha dada, formato "2026/27".</summary>
    public static string Current(DateOnly today)
    {
        var startYear = today.Month >= 7 ? today.Year : today.Year - 1;
        return $"{startYear}/{(startYear + 1) % 100:D2}";
    }

    public static string Current() => Current(DateOnly.FromDateTime(DateTime.UtcNow));

    /// <summary>Fecha de inicio (1 de julio del año de arranque) de la temporada
    /// que contiene <paramref name="today"/>. Sirve como límite para agrupar
    /// snapshots/operaciones por temporada.</summary>
    public static DateOnly StartDate(DateOnly today)
    {
        var startYear = today.Month >= 7 ? today.Year : today.Year - 1;
        return new DateOnly(startYear, 7, 1);
    }
}
