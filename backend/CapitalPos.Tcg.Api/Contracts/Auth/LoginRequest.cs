using System.ComponentModel.DataAnnotations;

namespace CapitalPos.Tcg.Api.Contracts.Auth;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public Guid? EmpresaId { get; set; }
}
