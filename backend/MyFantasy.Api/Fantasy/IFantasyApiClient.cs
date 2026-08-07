using MyFantasy.Api.Fantasy.Dtos;

namespace MyFantasy.Api.Fantasy;

/// <summary>
/// Única puerta de salida hacia la API (no oficial) de LaLiga Fantasy. Toda la
/// comunicación externa pasa por aquí (evita CORS y centraliza token + rutas).
/// </summary>
public interface IFantasyApiClient
{
    Task<UserMeDto?> GetCurrentUserAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken ct = default);

    /// <summary>Clasificación de una liga (para localizar el equipo del usuario).</summary>
    Task<IReadOnlyList<StandingEntryDto>> GetLeagueStandingAsync(string leagueId, CancellationToken ct = default);

    /// <summary>Plantilla del equipo del usuario en una liga.</summary>
    Task<TeamSquadDto?> GetTeamSquadAsync(string leagueId, string teamId, CancellationToken ct = default);

    /// <summary>Feed de mercado: todos los jugadores con su valor actual.</summary>
    Task<IReadOnlyList<FantasyPlayerDto>> GetAllPlayersAsync(CancellationToken ct = default);

    Task<TeamMoneyDto?> GetTeamMoneyAsync(string teamId, CancellationToken ct = default);

    /// <summary>Jugadores en venta hoy en el mercado de una liga.</summary>
    Task<IReadOnlyList<MarketItemDto>> GetMarketAsync(string leagueId, CancellationToken ct = default);

    /// <summary>Registro maestro de equipos (id → nombre/escudo). El feed de
    /// jugadores no trae el equipo embebido; se resuelve con esto.</summary>
    Task<IReadOnlyList<TeamRefDto>> GetTeamsMasterAsync(CancellationToken ct = default);

    /// <summary>Detalle de un jugador en una liga: puntos, media, cláusula y
    /// <c>playerStats</c> por jornada (vacío en pretemporada).</summary>
    Task<PlayerDetailApiDto?> GetPlayerDetailAsync(string playerId, string leagueId, CancellationToken ct = default);

    /// <summary>Feed de actividad de la liga (página <paramref name="index"/>): fichajes
    /// y ventas con su importe. Fuente del precio de compra REAL.</summary>
    Task<IReadOnlyList<ActivityEntryDto>> GetLeagueActivityAsync(string leagueId, int index, CancellationToken ct = default);

    /// <summary>Ofertas recibidas por un jugador en venta (por playerTeamId).</summary>
    Task<IReadOnlyList<OfferDto>> GetPlayerOffersAsync(string leagueId, string playerTeamId, CancellationToken ct = default);

    /// <summary>Alineación oficial actual del equipo del usuario (para partir de ella).</summary>
    Task<LineupApiDto?> GetCurrentLineupAsync(string teamId, CancellationToken ct = default);

    /// <summary>Jornada actual de la competición (para calcular "las últimas N").</summary>
    Task<CurrentWeekDto?> GetCurrentWeekAsync(CancellationToken ct = default);

    /// <summary>Estadísticas (puntos Fantasy) de todos los jugadores en una jornada.</summary>
    Task<IReadOnlyList<WeekStatDto>> GetWeekStatsAsync(int week, CancellationToken ct = default);

    /// <summary>Jornada actual (host principal, /v1/.../week/current) para el calendario.</summary>
    Task<CurrentWeekDto?> GetCurrentWeekMainAsync(CancellationToken ct = default);

    /// <summary>Partidos del calendario de una jornada (con localId/visitorId).</summary>
    Task<IReadOnlyList<CalendarMatchDto>> GetCalendarAsync(int week, CancellationToken ct = default);
}
