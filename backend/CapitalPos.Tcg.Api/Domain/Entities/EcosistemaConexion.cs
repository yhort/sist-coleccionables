using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class EcosistemaConexion : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public TipoConexionEcosistema Tipo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public EstadoSaludConexion EstadoSalud { get; set; } = EstadoSaludConexion.DESCONOCIDO;
    public DateTimeOffset? UltimoPing { get; set; }
    public string? UltimoError { get; set; }
    public string? ConfiguracionJson { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
}
