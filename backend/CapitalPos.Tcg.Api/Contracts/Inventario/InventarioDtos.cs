using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Inventario;

public enum SentidoAjuste
{
    ENTRADA,
    SALIDA
}

public sealed class AjustarStockRequest
{
    [Required]
    public Guid ProductoId { get; set; }

    [Required]
    public Guid SedeId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999")]
    public decimal Cantidad { get; set; }

    [Required]
    public SentidoAjuste Sentido { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(500)]
    public string Motivo { get; set; } = string.Empty;
}

public sealed class StockProductoResponse
{
    public Guid? Id { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required Guid ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required decimal CantidadDisponible { get; init; }
    public required decimal CantidadReservada { get; init; }
    public required decimal CantidadLibre { get; init; }
}

public sealed class KardexMovimientoResponse
{
    public required Guid Id { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required Guid ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required TipoMovimientoInventario TipoMovimiento { get; init; }
    public required decimal Cantidad { get; init; }
    public required decimal StockAnterior { get; init; }
    public required decimal StockPosterior { get; init; }
    public required decimal ReservadoAnterior { get; init; }
    public required decimal ReservadoPosterior { get; init; }
    public string? ReferenciaTipo { get; init; }
    public Guid? ReferenciaId { get; init; }
    public string? Motivo { get; init; }
    public Guid? UsuarioId { get; init; }
    public string? UsuarioNombre { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}

public sealed class CompraDetalleInput
{
    [Required]
    public Guid ProductoId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999")]
    public decimal Cantidad { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal CostoUnitario { get; set; }
}

public sealed class CrearCompraRequest
{
    [Required]
    public Guid ProveedorId { get; set; }

    [Required]
    public Guid SedeId { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }

    [Required]
    [MinLength(1)]
    public List<CompraDetalleInput> Detalles { get; set; } = [];
}

public sealed class CompraDetalleResponse
{
    public required Guid Id { get; init; }
    public required Guid ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required decimal Cantidad { get; init; }
    public required decimal CostoUnitario { get; init; }
    public required decimal Total { get; init; }
}

public sealed class CompraResponse
{
    public required Guid Id { get; init; }
    public required Guid ProveedorId { get; init; }
    public required string ProveedorRuc { get; init; }
    public required string ProveedorRazonSocial { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required DateTimeOffset Fecha { get; init; }
    public string? Observacion { get; init; }
    public required decimal Total { get; init; }
    public required IReadOnlyList<CompraDetalleResponse> Detalles { get; init; }
}
