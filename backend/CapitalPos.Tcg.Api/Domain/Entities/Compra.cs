namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Compra : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ProveedorId { get; set; }
    public Guid SedeId { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public string? Observacion { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Proveedor Proveedor { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public Usuario? Usuario { get; set; }
    public ICollection<CompraDetalle> Detalles { get; set; } = new List<CompraDetalle>();
}

public class CompraDetalle : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid CompraId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Total { get; set; }

    public Compra Compra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
