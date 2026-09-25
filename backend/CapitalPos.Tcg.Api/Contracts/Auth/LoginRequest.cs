using System.ComponentModel.DataAnnotations;

namespace CapitalPos.Tcg.Api.Contracts.Auth;

public sealed class LoginRequest
{
    /// <summary>
    /// Credencial de acceso: correo, DNI o usuario (parte local del correo).
    /// Se mantiene el nombre <c>Email</c> por compatibilidad del contrato JSON.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public Guid? EmpresaId { get; set; }
}
