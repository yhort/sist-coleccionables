using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Clientes;

public sealed class UpsertClienteRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(160)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Telefono { get; set; }

    [MaxLength(80)]
    public string? PuntoEntregaPreferido { get; set; }

    public CanalContactoCliente? CanalContacto { get; set; }

    [MaxLength(160)]
    public string? ContactoReferencia { get; set; }

    public TipoDocumentoIdentidad TipoDocumento { get; set; } = TipoDocumentoIdentidad.DNI;

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
    public required DateTimeOffset FechaCreacion { get; init; }
}
