using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Entrega : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid PedidoDigitalId { get; set; }
    public Guid SedeOrigenId { get; set; }
    public MetodoEnvio MetodoEnvio { get; set; } = MetodoEnvio.RECOJO_TIENDA;
    public EstadoLogistica Estado { get; set; } = EstadoLogistica.PROGRAMADA;
    public string DestinatarioNombre { get; set; } = string.Empty;
    public string? DestinatarioTelefono { get; set; }
    public string? Direccion { get; set; }
    public string? Distrito { get; set; }
    public string? Provincia { get; set; }
    public string? Departamento { get; set; }
    public string? Agencia { get; set; }
    public string? PuntoEntrega { get; set; }
    public CanalContactoCliente? CanalContacto { get; set; }
    public string? ContactoReferencia { get; set; }
    public string? NumeroTracking { get; set; }
    public decimal CostoEnvio { get; set; }
    public string? NotasEmpaque { get; set; }
    public DateTimeOffset? FechaProgramada { get; set; }
    public DateTimeOffset? FechaDespacho { get; set; }
    public DateTimeOffset? FechaEntrega { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public PedidoDigital Pedido { get; set; } = null!;
    public Sede SedeOrigen { get; set; } = null!;
}
