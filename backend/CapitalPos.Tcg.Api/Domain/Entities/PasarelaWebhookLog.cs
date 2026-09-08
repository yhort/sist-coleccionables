using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class PasarelaWebhookLog : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public ProveedorPasarela Proveedor { get; set; } = ProveedorPasarela.IZIPAY;
    public string TransaccionUuid { get; set; } = string.Empty;
    public bool FirmaValida { get; set; }
    public string PayloadRaw { get; set; } = string.Empty;
    public EstadoPasarelaWebhook Estado { get; set; }
    public Guid? PagoId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Pago? Pago { get; set; }
}
