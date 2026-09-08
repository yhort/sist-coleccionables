using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Subastas;

public sealed class SubastaDetalleInput
{
    [Required]
    public Guid ProductoId { get; set; }

    [Range(typeof(decimal), "1", "999999")]
    public decimal Cantidad { get; set; } = 1;
}

public sealed class CrearSubastaTcgRequest
{
    [Required]
    public Guid SedeId { get; set; }

    /// <summary>
    /// Compatibilidad Single Hit: si no hay <see cref="Detalles"/>, se usa este producto con cantidad 1.
    /// </summary>
    public Guid? ProductoId { get; set; }

    /// <summary>
    /// 1..N líneas (Single Hit, Bulk o Combo Multi-SKU). Si está vacío, se deriva de <see cref="ProductoId"/>.
    /// </summary>
    public List<SubastaDetalleInput> Detalles { get; set; } = [];

    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    public CanalSubastaTcg Canal { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal PrecioBase { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal IncrementoMinimo { get; set; }

    public decimal? PrecioReserva { get; set; }

    [Required]
    public DateTimeOffset FechaInicio { get; set; }

    [Required]
    public DateTimeOffset FechaCierre { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class RegistrarPujaRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(160)]
    public string NombrePostor { get; set; } = string.Empty;

    public Guid? ClienteId { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Monto { get; set; }
}

public sealed class AdjudicarSubastaRequest
{
    public MetodoEnvio MetodoEnvio { get; set; } = MetodoEnvio.RECOJO_TIENDA;

    /// <summary>Preferencia de cobro local al generar el pedido en PendientePago.</summary>
    public OrigenPago? OrigenPagoPreferido { get; set; }

    [MaxLength(160)]
    public string? DestinatarioNombre { get; set; }

    [MaxLength(32)]
    public string? DestinatarioTelefono { get; set; }

    [MaxLength(300)]
    public string? EntregaDireccion { get; set; }

    [MaxLength(80)]
    public string? EntregaDistrito { get; set; }

    [MaxLength(80)]
    public string? EntregaProvincia { get; set; }

    [MaxLength(80)]
    public string? EntregaDepartamento { get; set; }

    [MaxLength(80)]
    public string? Agencia { get; set; }

    [MaxLength(80)]
    public string? PuntoEntrega { get; set; }

    public CanalContactoCliente? CanalContacto { get; set; }

    [MaxLength(160)]
    public string? ContactoReferencia { get; set; }

    /// <summary>Si true, actualiza punto/canal/teléfono/contacto de referencia predeterminados en la ficha del cliente ganador.</summary>
    public bool GuardarPuntoEnCliente { get; set; }
}

public sealed class MargenSubastaDto
{
    public required decimal PrecioBase { get; init; }
    public decimal? OfertaReferencia { get; init; }
    public decimal? Diferencia { get; init; }
    public decimal? Porcentaje { get; init; }
}

public sealed class PujaResponse
{
    public required Guid Id { get; init; }
    public required Guid SubastaTcgId { get; init; }
    public Guid? ClienteId { get; init; }
    public required string NombrePostor { get; init; }
    public required decimal Monto { get; init; }
    public required DateTimeOffset Fecha { get; init; }
    public required bool EsGanadora { get; init; }
}

public sealed class SubastaDetalleResponse
{
    public required Guid Id { get; init; }
    public required Guid ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required decimal Cantidad { get; init; }
    public required int Orden { get; init; }
}

public sealed class SubastaTcgResponse
{
    public required Guid Id { get; init; }
    public required string Codigo { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    /// <summary>Producto principal (primera línea), compatibilidad con clientes mono-SKU.</summary>
    public required Guid ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required IReadOnlyList<SubastaDetalleResponse> Detalles { get; init; }
    public required string Titulo { get; init; }
    public required CanalSubastaTcg Canal { get; init; }
    public required decimal PrecioBase { get; init; }
    public required decimal IncrementoMinimo { get; init; }
    public decimal? PrecioReserva { get; init; }
    public required DateTimeOffset FechaInicio { get; init; }
    public required DateTimeOffset FechaCierre { get; init; }
    public DateTimeOffset? FechaCierreReal { get; init; }
    public required EstadoSubastaTcg Estado { get; init; }
    public Guid? PujaGanadoraId { get; init; }
    public Guid? PedidoDigitalId { get; init; }
    public string? PedidoCodigo { get; init; }
    public string? Observacion { get; init; }
    public required IReadOnlyList<PujaResponse> Pujas { get; init; }
    public required MargenSubastaDto Margen { get; init; }
}
