using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Cliente : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    /// <summary>Punto de entrega predeterminado (Papurris, Dapkris, TCG House, etc.).</summary>
    public string? PuntoEntregaPreferido { get; set; }
    public CanalContactoCliente? CanalContacto { get; set; }
    /// <summary>Persona autorizada a recoger / contacto alternativo.</summary>
    public string? ContactoReferencia { get; set; }
    public TipoDocumentoIdentidad TipoDocumento { get; set; } = TipoDocumentoIdentidad.DNI;
    public string? NumeroDocumento { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
