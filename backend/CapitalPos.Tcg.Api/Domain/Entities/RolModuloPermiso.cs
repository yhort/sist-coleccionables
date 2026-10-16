using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Permiso de un rol sobre un módulo de la aplicación (matriz L/E por empresa).
/// </summary>
public class RolModuloPermiso : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public RolUsuario Rol { get; set; }

    /// <summary>Clave estable del módulo (ej. productos-tcg).</summary>
    public string Modulo { get; set; } = string.Empty;

    public bool Lectura { get; set; }
    public bool Escritura { get; set; }

    public Empresa Empresa { get; set; } = null!;
}
