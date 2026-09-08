using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class AperturaTcg : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SedeId { get; set; }
    public Guid ProductoSelladoId { get; set; }
    public int CantidadSellados { get; set; }
    public EstadoAperturaTcg Estado { get; set; } = EstadoAperturaTcg.BORRADOR;
    public Guid UsuarioId { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? FechaConfirmacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public ProductoSellado ProductoSellado { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<AperturaTcgDetalle> Detalles { get; set; } = new List<AperturaTcgDetalle>();
}
