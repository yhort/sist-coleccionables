namespace CapitalPos.Tcg.Api.Domain.Entities;

public class StockProducto : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SedeId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal CantidadDisponible { get; set; }
    public decimal CantidadReservada { get; set; }

    /// <summary>Columna computada: disponible − reservada.</summary>
    public decimal CantidadLibre { get; private set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
