namespace MyFantasy.Api.Domain;

/// <summary>
/// Estado de autenticación persistido (fila única, <c>Id = 1</c>). Guarda el
/// <c>refresh_token</c> de LaLiga <b>cifrado</b> — nunca en texto plano — para
/// sobrevivir a reinicios sin depender de user-secrets. Se actualiza en cada
/// rotación de B2C y desde la pantalla de re-login (<c>POST /api/auth/login</c>).
/// </summary>
public class AuthState
{
    public int Id { get; set; }

    /// <summary>refresh_token protegido con Data Protection (ilegible en BD).</summary>
    public string? RefreshTokenEnc { get; set; }

    public DateTime UpdatedAt { get; set; }
}
