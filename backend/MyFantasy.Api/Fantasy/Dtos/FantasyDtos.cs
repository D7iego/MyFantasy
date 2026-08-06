using System.Text.Json.Serialization;

namespace MyFantasy.Api.Fantasy.Dtos;

// ---------------------------------------------------------------------------
// DTOs de la API de LaLiga Fantasy. Permisivos a propósito: las formas reales
// tienen envoltorios variables (data / elements / leagues) y campos opcionales.
// Los envoltorios se resuelven en FantasyApiClient con ListEnvelope<T>.
// ---------------------------------------------------------------------------

public class TeamRefDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Slug { get; set; }
    public string? BadgeColor { get; set; }
    public string? Badge { get; set; }
    public string? Image { get; set; }

    /// <summary>URL del escudo. Prioriza los campos de imagen; usa BadgeColor solo
    /// si resulta ser una URL (a veces es un color hex, que se descarta).</summary>
    public string? ResolvedBadgeUrl
    {
        get
        {
            foreach (var c in new[] { Badge, Image, BadgeColor })
                if (!string.IsNullOrWhiteSpace(c) && c.StartsWith("http")) return c;
            return null;
        }
    }
}

public class ManagerDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? ManagerName { get; set; }
}

/// <summary>
/// Imágenes del jugador. La API las da bajo `images.transparent["256x256"]`
/// (recorte PNG) o un `image`/`images.player` plano (confirmado en la app de
/// referencia, responseAdapters.normalizePlayer).
/// </summary>
public class PlayerImagesDto
{
    public PlayerImageSizesDto? Transparent { get; set; }
    public string? Player { get; set; }

    public string? Best =>
        Transparent?.Size256 ?? Transparent?.Size512 ?? Transparent?.Size128 ?? Player;
}

public class PlayerImageSizesDto
{
    [JsonPropertyName("256x256")] public string? Size256 { get; set; }
    [JsonPropertyName("128x128")] public string? Size128 { get; set; }
    [JsonPropertyName("512x512")] public string? Size512 { get; set; }
}

/// <summary>Jugador del feed de mercado (/players).</summary>
public class FantasyPlayerDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public int? PositionId { get; set; }
    public long? MarketValue { get; set; }
    public double? Points { get; set; }
    public TeamRefDto? Team { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? TeamId { get; set; }
    public string? Image { get; set; }
    public PlayerImagesDto? Images { get; set; }

    public string? ResolvedTeamName => Team?.Name;
    public string? DisplayName => string.IsNullOrWhiteSpace(Name) ? Nickname : Name;
    public string? ResolvedImageUrl => Image ?? Images?.Best;
}

/// <summary>Núcleo de jugador embebido en la plantilla (playerMaster).</summary>
public class PlayerMasterDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public int? PositionId { get; set; }
    public long? MarketValue { get; set; }
    public TeamRefDto? Team { get; set; }
    public string? Image { get; set; }
    public PlayerImagesDto? Images { get; set; }

    public string? ResolvedImageUrl => Image ?? Images?.Best;
}

/// <summary>
/// Entrada de plantilla dentro del equipo. La API puede exponer el precio de
/// compra bajo varios nombres; los leemos todos y el servicio de sync decide.
/// </summary>
public class SquadPlayerDto
{
    public PlayerMasterDto? PlayerMaster { get; set; }
    public ManagerDto? Manager { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? ManagerId { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? PlayerTeamId { get; set; }

    /// <summary>Presente si el jugador está listado en el mercado por su dueño.</summary>
    public PlayerMarketDto? PlayerMarket { get; set; }

    // Candidatos a "precio de compra" (sin confirmar cuál trae la API real).
    public long? PurchasePrice { get; set; }
    public long? BuyPrice { get; set; }
    public long? BuyoutClause { get; set; }

    public bool? IsShielded { get; set; }
    public long? MarketValue { get; set; }
    public int? PositionId { get; set; }

    /// <summary>Fin del blindaje / bloqueo de cláusula (ISO 8601), confirmado en el
    /// detalle de jugador: <c>playerTeam.buyoutClauseLockedEndTime</c>.</summary>
    public string? BuyoutClauseLockedEndTime { get; set; }

    /// <summary>Primer precio de compra disponible de la API, o null si no viene.</summary>
    public long? ResolvedPurchasePrice => PurchasePrice ?? BuyPrice;
}

/// <summary>Datos del equipo del usuario en una liga (/leagues/{id}/teams/{teamId}).</summary>
public class TeamSquadDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public long? TeamValue { get; set; }
    public ManagerDto? Manager { get; set; }
    public List<SquadPlayerDto>? Players { get; set; }
}

public class LeagueDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool? IsDefault { get; set; }
}

/// <summary>Entrada de clasificación; se usa para localizar el teamId del usuario.</summary>
public class StandingEntryDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? UserId { get; set; }
    public TeamStandingDto? Team { get; set; }

    // Puntos del manager. Nombres SIN confirmar (array/forma varía en pretemporada);
    // se leen los más probables y el resolver casca al primero no nulo.
    public double? Points { get; set; }
    public double? TotalPoints { get; set; }

    public double? ResolvedPoints => Team?.ResolvedPoints ?? Points ?? TotalPoints;
    public long? ResolvedTeamValue => Team?.TeamValue;
}

public class TeamStandingDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? UserId { get; set; }
    public ManagerDto? Manager { get; set; }
    public long? TeamValue { get; set; }
    public double? Points { get; set; }
    public double? TotalPoints { get; set; }

    public double? ResolvedPoints => Points ?? TotalPoints;
}

public class UserMeDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? UserId { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? Username { get; set; }

    public string? AnyId => Id ?? UserId ?? ManagerId;
}

public class TeamMoneyDto
{
    public long? TeamMoney { get; set; }
    public long? Amount { get; set; }
    public long Value => TeamMoney ?? Amount ?? 0;
}

/// <summary>Estado de venta de un jugador listado por su dueño (playerMarket).</summary>
public class PlayerMarketDto
{
    public long? SalePrice { get; set; }
    public int? NumberOfOffers { get; set; }
    public string? ExpirationDate { get; set; }
}

/// <summary>Oferta recibida por un jugador en venta (/playerTeam/{id}/offer).</summary>
public class OfferDto
{
    public long? Money { get; set; }
    public string? Status { get; set; }
}

// ---- Once oficial (/teams/{teamId}/lineup) ----

public class LineupApiDto
{
    public FormationDto? Formation { get; set; }
}

public class FormationDto
{
    public List<LineupSlotDto>? Goalkeeper { get; set; }
    public List<LineupSlotDto>? Defender { get; set; }
    public List<LineupSlotDto>? Midfield { get; set; }
    public List<LineupSlotDto>? Striker { get; set; }
    public int[]? TacticalFormation { get; set; }
}

public class LineupSlotDto
{
    public PlayerMasterDto? PlayerMaster { get; set; }
}

/// <summary>Entrada del feed de actividad de la liga. Para un fichaje
/// (activityTypeId de compra), <c>amount</c> es el importe REAL pagado y
/// <c>user1Id</c> el manager comprador.</summary>
public class ActivityEntryDto
{
    public int? ActivityTypeId { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? User1Id { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? PlayerMasterId { get; set; }
    public long? Amount { get; set; }
    public string? CreatedAt { get; set; }
}

// ---- Detalle de jugador (/player/{id}/league/{leagueId}) ----

public class PlayerDetailApiDto
{
    public PlayerMasterDetailDto? PlayerMaster { get; set; }
    public PlayerTeamDetailDto? PlayerTeam { get; set; }
}

public class PlayerMasterDetailDto
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public int? PositionId { get; set; }
    public long? MarketValue { get; set; }
    public TeamRefDto? Team { get; set; }
    public string? Image { get; set; }
    public PlayerImagesDto? Images { get; set; }
    public double? Points { get; set; }
    public double? AveragePoints { get; set; }
    public List<PlayerStatDto>? PlayerStats { get; set; }

    public string? ResolvedImageUrl => Image ?? Images?.Best;
}

public class PlayerTeamDetailDto
{
    public long? BuyoutClause { get; set; }
    public string? BuyoutClauseLockedEndTime { get; set; }
    public bool? IsShielded { get; set; }
}

/// <summary>Stat de una jornada. Forma exacta SIN confirmar (array vacío en
/// pretemporada); campos permisivos con los nombres más probables.</summary>
public class PlayerStatDto
{
    public int? WeekNumber { get; set; }
    public int? Week { get; set; }
    public double? TotalPoints { get; set; }
    public double? Points { get; set; }
    public int? Goals { get; set; }
    public int? GoalScored { get; set; }
    public int? Assists { get; set; }
    public int? Minutes { get; set; }
    public int? MinutesPlayed { get; set; }

    // ---- Datos del partido. Forma SIN confirmar (array vacío en pretemporada):
    // se leen los nombres flat más probables y también un objeto anidado. Todo
    // opcional; si no viene nada, el frontend oculta el resultado. Al empezar la
    // liga se ajustan los nombres reales en un único sitio (aquí). ----
    public MatchInfoDto? Match { get; set; }
    public MatchInfoDto? MatchInfo { get; set; }
    public string? LocalTeam { get; set; }
    public string? VisitorTeam { get; set; }
    public int? LocalScore { get; set; }
    public int? VisitorScore { get; set; }
    public bool? IsLocal { get; set; }

    public int? ResolvedWeek => WeekNumber ?? Week;
    public double? ResolvedPoints => TotalPoints ?? Points;
    public int? ResolvedGoals => Goals ?? GoalScored;
    public int? ResolvedMinutes => Minutes ?? MinutesPlayed;

    private MatchInfoDto? M => Match ?? MatchInfo;
    public string? ResolvedHomeTeam => M?.ResolvedHomeTeam ?? LocalTeam;
    public string? ResolvedAwayTeam => M?.ResolvedAwayTeam ?? VisitorTeam;
    public int? ResolvedHomeGoals => M?.ResolvedHomeGoals ?? LocalScore;
    public int? ResolvedAwayGoals => M?.ResolvedAwayGoals ?? VisitorScore;
    public bool? ResolvedIsHome => M?.IsLocal ?? IsLocal;
}

/// <summary>Info del partido asociada a una jornada. Nombres SIN confirmar.</summary>
public class MatchInfoDto
{
    public TeamRefDto? Local { get; set; }
    public TeamRefDto? Visitor { get; set; }
    public TeamRefDto? Home { get; set; }
    public TeamRefDto? Away { get; set; }
    public string? LocalName { get; set; }
    public string? VisitorName { get; set; }
    public int? LocalScore { get; set; }
    public int? VisitorScore { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public bool? IsLocal { get; set; }

    public string? ResolvedHomeTeam => Local?.Name ?? Home?.Name ?? LocalName;
    public string? ResolvedAwayTeam => Visitor?.Name ?? Away?.Name ?? VisitorName;
    public int? ResolvedHomeGoals => LocalScore ?? HomeScore;
    public int? ResolvedAwayGoals => VisitorScore ?? AwayScore;
}

// ---- Stats por jornada (/stats/v1/.../stats/week/{n}) ----

/// <summary>Jornada actual. Forma SIN confirmar; campos permisivos.</summary>
public class CurrentWeekDto
{
    public int? WeekNumber { get; set; }
    public int? Week { get; set; }
    public int? Number { get; set; }

    public int? ResolvedWeek => WeekNumber ?? Week ?? Number;
}

/// <summary>Stat de un jugador en una jornada (puntos Fantasy). Forma SIN
/// confirmar; se leen los nombres más probables y se casa por id externo.</summary>
public class WeekStatDto
{
    public PlayerMasterDto? PlayerMaster { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? PlayerMasterId { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public double? Points { get; set; }
    public double? WeekPoints { get; set; }
    public double? TotalPoints { get; set; }

    public string? ResolvedPlayerId => PlayerMaster?.Id ?? PlayerMasterId ?? Id;
    public double? ResolvedPoints => Points ?? WeekPoints ?? TotalPoints;
}

/// <summary>
/// Entrada del mercado de una liga (/league/{id}/market). La forma real varía;
/// leemos el jugador embebido (<c>playerMaster</c>) o los campos planos, y
/// normalizamos con las propiedades <c>Resolved*</c>. Los deltas NO salen de
/// aquí: se calculan con nuestros PriceSnapshots casando por id externo.
/// </summary>
public class MarketItemDto
{
    /// <summary>Tipo de entrada: <c>marketPlayerLeague</c> (lo vende la liga /
    /// agente libre) o <c>marketPlayerTeam</c> (lo vende un manager/rival).</summary>
    public string? Discr { get; set; }

    /// <summary>true si lo vende un equipo (rival o yo), no la liga.</summary>
    public bool IsRivalSale => !string.IsNullOrEmpty(Discr) &&
        Discr.Contains("Team", StringComparison.OrdinalIgnoreCase);

    public PlayerMasterDto? PlayerMaster { get; set; }

    // Formas planas alternativas (algunos feeds no anidan playerMaster).
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? PlayerMasterId { get; set; }
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public int? PositionId { get; set; }
    public long? MarketValue { get; set; }
    public long? SalePrice { get; set; }
    public TeamRefDto? Team { get; set; }
    public string? Image { get; set; }
    public PlayerImagesDto? Images { get; set; }

    public string? ResolvedExternalId => PlayerMaster?.Id ?? PlayerMasterId ?? Id;
    public string? ResolvedName => PlayerMaster?.Nickname ?? PlayerMaster?.Name
        ?? (string.IsNullOrWhiteSpace(Nickname) ? Name : Nickname);
    public int? ResolvedPositionId => PlayerMaster?.PositionId ?? PositionId;
    public long? ResolvedMarketValue => PlayerMaster?.MarketValue ?? MarketValue ?? SalePrice;
    public string? ResolvedTeamName => PlayerMaster?.Team?.Name ?? Team?.Name;
    public string? ResolvedImageUrl => PlayerMaster?.ResolvedImageUrl ?? Image ?? Images?.Best;
}
