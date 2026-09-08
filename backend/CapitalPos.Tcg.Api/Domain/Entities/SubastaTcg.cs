using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class SubastaTcg : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SedeId { get; set; }
    public Guid ProductoId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public CanalSubastaTcg Canal { get; set; }
    public decimal PrecioBase { get; set; }
    public decimal IncrementoMinimo { get; set; }
    public decimal? PrecioReserva { get; set; }
    public DateTimeOffset FechaInicio { get; set; }
    public DateTimeOffset FechaCierre { get; set; }
    /// <summary>Instante real del cierre (manual o worker). No sustituye <see cref="FechaCierre"/> programada.</summary>
    public DateTimeOffset? FechaCierreReal { get; set; }
    public EstadoSubastaTcg Estado { get; set; } = EstadoSubastaTcg.BORRADOR;
    public Guid? PujaGanadoraId { get; set; }
    public Guid? PedidoDigitalId { get; set; }
    public string? Observacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    /// <summary>Producto principal (primera línea). Conservado por compatibilidad de listados/API.</summary>
    public Producto Producto { get; set; } = null!;
    public ICollection<SubastaDetalle> Detalles { get; set; } = new List<SubastaDetalle>();
    public ICollection<Puja> Pujas { get; set; } = new List<Puja>();
}
