using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Usuario : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }

    /// <summary>Documento de identidad (DNI) para auditoría formal.</summary>
    public string Dni { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo de display (JWT / auditoría). Se sincroniza desde Nombres + Apellidos.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Correo / usuario de acceso (login flexible: email, DNI o parte local).</summary>
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();

    public void SincronizarNombreCompleto()
    {
        var compuesto = $"{Nombres.Trim()} {Apellidos.Trim()}".Trim();
        Nombre = compuesto.Length > 0 ? compuesto : Nombre.Trim();
    }
}
