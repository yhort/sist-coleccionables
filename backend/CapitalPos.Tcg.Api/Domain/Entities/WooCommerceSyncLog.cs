using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class WooCommerceSyncLog : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid IntegracionId { get; set; }
    public TipoSyncWoo Tipo { get; set; }
    public ResultadoSyncWoo Estado { get; set; }
    public string PayloadResumen { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string? MensajeError { get; set; }
    public int Intentos { get; set; }
    public DateTimeOffset? ProximoReintento { get; set; }
    public DateTimeOffset Fecha { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public IntegracionWooCommerce Integracion { get; set; } = null!;
}
