using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class VentaPago : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid VentaId { get; set; }
    public Guid? PagoId { get; set; }
    public OrigenPago Origen { get; set; }
    public decimal Monto { get; set; }
    public string? CodigoOperacion { get; set; }

    public Venta Venta { get; set; } = null!;
    public Pago? Pago { get; set; }
}
