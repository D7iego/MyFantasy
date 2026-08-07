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

    /// <summary>Host de las rutas de estadísticas por jornada (/stats/v1/...), que
    /// NO cuelgan de /api. SIN confirmar: ajústalo en appsettings si difiere.</summary>
    public string StatsBaseUrl { get; set; } = "https://fantasy-api.llt-services.com";

    /// <summary>Temporada 26/27: las rutas ganaron segmento de competición (1 = LaLiga).</summary>
    public string CompetitionId { get; set; } = "1";

    public string Language { get; set; } = "es";

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>activityTypeIds que representan una adquisición en el feed de
    /// actividad (de ahí sale el precio REAL pagado): 33 = compra de mercado,
    /// 31 = pago de cláusula. Confirmados sobre datos reales.</summary>
    public int[] PurchaseActivityTypeIds { get; set; } = { 33, 31 };

    /// <summary>Páginas del feed de actividad a recorrer al enriquecer precios de compra.</summary>
    public int ActivityPagesToScan { get; set; } = 15;

    // ---- Heurística de "puja sugerida" (pestaña Mercado). Constantes fáciles de tocar. ----

    /// <summary>Jornadas recientes a promediar para el rendimiento.</summary>
    public int BidRecentWeeks { get; set; } = 5;

    /// <summary>Rango de referencia (±%) de la tendencia reciente de precio para el
    /// price_trend_score. Más pequeño = subidas/bajadas moderadas pesan más.</summary>
    public double BidPriceTrendRangePct { get; set; } = 6.0;

    /// <summary>Máximo % POR DEBAJO del valor para un jugador flojo/en caída.</summary>
    public double BidMaxDrop { get; set; } = 0.10;

    /// <summary>Recompra de la liga al vender (hasta +10% del valor): fija el techo
    /// "seguro" de puja, porque hasta ahí se recupera revendiendo el jugador.</summary>
    public double BidSellBackPct { get; set; } = 0.10;

    /// <summary>Prima extra por encima del techo de reventa cuando el momentum es
    /// muy fuerte (apuesta a que el precio siga subiendo).</summary>
    public double BidMomentumPremium { get; set; } = 0.05;

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
    public string LeagueActivity { get; set; } = "/v1/competition/{competitionId}/leagues/{leagueId}/activity/{index}";
    public string TeamSquad { get; set; } = "/v1/competition/{competitionId}/leagues/{leagueId}/teams/{teamId}";
    public string TeamMoney { get; set; } = "/v1/competition/{competitionId}/teams/{teamId}/money";
    public string TeamLineup { get; set; } = "/v1/competition/{competitionId}/teams/{teamId}/lineup";
    public string Market { get; set; } = "/v1/competition/{competitionId}/league/{leagueId}/market";
    public string PlayerOffer { get; set; } = "/v1/competition/{competitionId}/league/{leagueId}/playerTeam/{playerTeamId}/offer";
    public string Players { get; set; } = "/v1/competition/{competitionId}/players";
    public string PlayerDetail { get; set; } = "/v1/competition/{competitionId}/player/{playerId}/league/{leagueId}";
    public string TeamsMaster { get; set; } = "/v3/teams-master";

    // Calendario (26/27: cada partido trae localId/visitorId) y jornada actual
    // (host principal, distinta de la de stats). Confirmados en los JS de referencia.
    public string WeekCurrentMain { get; set; } = "/v1/competition/{competitionId}/week/current";
    public string Calendar { get; set; } = "/v1/competition/{competitionId}/calendar?weekNumber={weekNumber}";

    // Rutas de stats por jornada (bajo StatsBaseUrl, no BaseUrl). SIN confirmar.
    public string CurrentWeek { get; set; } = "/stats/v1/competition/{competitionId}/stats/week/current";
    public string WeekStats { get; set; } = "/stats/v1/competition/{competitionId}/stats/week/{weekNumber}";
}
