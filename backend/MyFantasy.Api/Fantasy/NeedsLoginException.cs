namespace MyFantasy.Api.Fantasy;

/// <summary>
/// Se lanza cuando no se puede obtener un bearer válido porque el
/// <c>refresh_token</c> caducó/es inválido o no hay credenciales configuradas.
/// La capa web lo traduce en un <c>401 { needsLogin: true }</c> para disparar la
/// pantalla de re-login en el frontend.
/// </summary>
public class NeedsLoginException : Exception
{
    public NeedsLoginException(string message) : base(message) { }
}
