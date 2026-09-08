using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class CajaSesion : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SedeId { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid? UsuarioCierreId { get; set; }
    public decimal MontoApertura { get; set; }
    public DateTimeOffset FechaApertura { get; set; }
    public DateTimeOffset? FechaCierre { get; set; }
    public EstadoCajaSesion Estado { get; set; } = EstadoCajaSesion.ABIERTA;
    public decimal MontoEfectivoTeorico { get; set; }
    public decimal? MontoEfectivoReal { get; set; }
    public decimal? Diferencia { get; set; }
    public string? ObservacionApertura { get; set; }
    public string? ObservacionCierre { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Usuario? UsuarioCierre { get; set; }
    public ICollection<CajaMovimiento> Movimientos { get; set; } = new List<CajaMovimiento>();
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
