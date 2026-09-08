using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Asiento de kardex. Append-only: no se edita ni se borra; una anulación genera un movimiento inverso.
/// </summary>
public class MovimientoInventario : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SedeId { get; set; }
    public Guid ProductoId { get; set; }
    public TipoMovimientoInventario TipoMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public decimal StockAnterior { get; set; }
    public decimal StockPosterior { get; set; }
    public decimal ReservadoAnterior { get; set; }
    public decimal ReservadoPosterior { get; set; }
    public string? ReferenciaTipo { get; set; }
    public Guid? ReferenciaId { get; set; }
    public string? Motivo { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public Usuario? Usuario { get; set; }
}
