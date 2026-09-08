using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class CajaMovimiento : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid CajaSesionId { get; set; }
    public TipoCajaMovimiento Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset Fecha { get; set; }

    public CajaSesion Sesion { get; set; } = null!;
    public Usuario? Usuario { get; set; }
}
