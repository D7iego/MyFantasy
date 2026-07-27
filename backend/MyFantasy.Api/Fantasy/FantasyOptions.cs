namespace MyFantasy.Api.Fantasy;

/// <summary>
/// Configuración de la API (no oficial) de LaLiga Fantasy. Todo es configurable
/// desde appsettings.json / user-secrets para poder ajustar rutas o credenciales
/// sin tocar código. Los valores por defecto están CONFIRMADOS a partir de la
/// app de referencia (api.js / authService.js).
/// </summary>
public class FantasyOptions
{
    public const string SectionName = "Fantasy";

    // Las rutas de datos van bajo /api (el proxy de la app original reenvía
    // /api/... sin quitarlo, confirmado en index.js + api.js).
    public string BaseUrl { get; set; } = "https://fantasy-api.llt-services.com/api";

    /// <summary>Temporada 26/27: las rutas ganaron segmento de competición (1 = LaLiga).</summary>
    public string CompetitionId { get; set; } = "1";

    public string Language { get; set; } = "es";

    public int TimeoutSeconds { get; set; } = 20;

    public FantasyAuthOptions Auth { get; set; } = new();

    public FantasyEndpoints Endpoints { get; set; } = new();
}

/// <summary>
/// Autenticación OAuth2 contra el tenant Azure B2C de LaLiga. En este proyecto
/// (uso personal) NO hay UI de login: se coloca un <see cref="RefreshToken"/>
/// (o directamente un <see cref="BearerToken"/>) en user-secrets y el cliente
/// se encarga del refresh automático. El bearer real que consume la API de
/// datos es el <c>id_token</c> que devuelve B2C.
/// </summary>
public class FantasyAuthOptions
{
    public string TokenEndpoint { get; set; } =
        "https://login.laliga.es/laligadspprob2c.onmicrosoft.com/oauth2/v2.0/token";

    /// <summary>Policy del flujo que emite refresh tokens (sign-in parametrizado).</summary>
    public string RefreshPolicy { get; set; } = "B2C_1A_5ULAIP_PARAMETRIZED_SIGNIN";

    /// <summary>
    /// Client ID usado en el refresh. Debe ser el MISMO que emitió los tokens.
    /// El flujo interactivo/Google usa el cliente nativo af88bcff-…; el web usa
    /// 6457fa17-…. Se puede sobreescribir en user-secrets.
    /// </summary>
    public string ClientId { get; set; } = "6457fa17-1224-416a-b21a-ee6ce76e9bc0";

    public string Scope { get; set; } = "openid offline_access";

    // ---- Secretos: NO ponerlos en appsettings del repo, usar user-secrets ----

    /// <summary>Refresh token de larga vida. El cliente lo canjea por id_token.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Alternativa a <see cref="RefreshToken"/>: un id_token ya obtenido a mano
    /// (por ejemplo capturado de la app). Útil para pruebas rápidas; caduca a la
    /// hora y no se puede renovar sin refresh token.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>Minutos antes de la expiración en los que se fuerza el refresh.</summary>
    public int RefreshSkewMinutes { get; set; } = 5;
}

/// <summary>
/// Plantillas de ruta con placeholders {competitionId} {leagueId} {teamId}
/// {weekNumber}. Rellenables desde configuración. Confirmadas en api.js.
/// </summary>
public class FantasyEndpoints
{
    public string CurrentUser { get; set; } = "/v4/user/me";
    public string Leagues { get; set; } = "/v1/competition/{competitionId}/leagues";
    public string LeagueStanding { get; set; } = "/v1/competition/{competitionId}/leagues/{leagueId}/standing";
    public string TeamSquad { get; set; } = "/v1/competition/{competitionId}/leagues/{leagueId}/teams/{teamId}";
    public string TeamMoney { get; set; } = "/v1/competition/{competitionId}/teams/{teamId}/money";
    public string Market { get; set; } = "/v1/competition/{competitionId}/league/{leagueId}/market";
    public string Players { get; set; } = "/v1/competition/{competitionId}/players";
    public string TeamsMaster { get; set; } = "/v3/teams-master";
}
