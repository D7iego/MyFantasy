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

    public int? ResolvedWeek => WeekNumber ?? Week;
    public double? ResolvedPoints => TotalPoints ?? Points;
    public int? ResolvedGoals => Goals ?? GoalScored;
    public int? ResolvedMinutes => Minutes ?? MinutesPlayed;
}

/// <summary>
/// Entrada del mercado de una liga (/league/{id}/market). La forma real varía;
/// leemos el jugador embebido (<c>playerMaster</c>) o los campos planos, y
/// normalizamos con las propiedades <c>Resolved*</c>. Los deltas NO salen de
/// aquí: se calculan con nuestros PriceSnapshots casando por id externo.
/// </summary>
public class MarketItemDto
{
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
