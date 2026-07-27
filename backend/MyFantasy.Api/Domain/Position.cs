namespace MyFantasy.Api.Domain;

/// <summary>
/// Posición del jugador. Los IDs coinciden con los que usa la API de LaLiga
/// Fantasy (1=portero … 4=delantero), confirmados en los ficheros de referencia.
/// </summary>
public enum Position
{
    Unknown = 0,
    Goalkeeper = 1,
    Defender = 2,
    Midfielder = 3,
    Forward = 4
}

public static class PositionExtensions
{
    public static Position FromApiId(int? positionId) => positionId switch
    {
        1 => Position.Goalkeeper,
        2 => Position.Defender,
        3 => Position.Midfielder,
        4 => Position.Forward,
        _ => Position.Unknown
    };

    public static string ToSpanish(this Position position) => position switch
    {
        Position.Goalkeeper => "Portero",
        Position.Defender => "Defensa",
        Position.Midfielder => "Centrocampista",
        Position.Forward => "Delantero",
        _ => "Desconocida"
    };
}
