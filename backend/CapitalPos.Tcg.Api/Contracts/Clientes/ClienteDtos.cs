using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Clientes;

public sealed class UpsertClienteRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(160)]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Solo dígitos, máximo 9.</summary>
    [MaxLength(9)]
    [RegularExpression(@"^\d{0,9}$", ErrorMessage = "El teléfono solo admite números (máximo 9 dígitos).")]
    public string? Telefono { get; set; }

    [MaxLength(80)]
    public string? PuntoEntregaPreferido { get; set; }

    public CanalContactoCliente? CanalContacto { get; set; }

    [MaxLength(160)]
    public string? ContactoReferencia { get; set; }

    public TipoDocumentoIdentidad TipoDocumento { get; set; } = TipoDocumentoIdentidad.DNI;

    /// <summary>
    /// Opcional: vacío o nulo queda como SIN_DOCUMENTO con NumeroDocumento = NULL
    /// (válido para boletas ≤ S/ 700 y Cliente varios / Público general).
    /// No enviar valores placeholder como "00000000".
    /// </summary>
    [MaxLength(16)]
    public string? NumeroDocumento { get; set; }

    public bool EsPublicoGeneral { get; set; }
}

public sealed class ClienteResponse
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public string? Telefono { get; init; }
    public string? PuntoEntregaPreferido { get; init; }
    public CanalContactoCliente? CanalContacto { get; init; }
    public string? ContactoReferencia { get; init; }
    public required TipoDocumentoIdentidad TipoDocumento { get; init; }
    public string? NumeroDocumento { get; init; }
    public required bool EsPublicoGeneral { get; init; }
    public required bool Activo { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}
