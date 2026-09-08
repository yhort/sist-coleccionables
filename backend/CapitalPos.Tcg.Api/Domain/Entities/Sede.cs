using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Sede : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoSede Tipo { get; set; }
    public string? Direccion { get; set; }
    public string? Distrito { get; set; }
    public string? Provincia { get; set; }
    public string? Departamento { get; set; }
    public string? Ubigeo { get; set; }
    public bool EsPuntoPartidaGre { get; set; }
    public bool EsPuntoLlegadaGre { get; set; }
    public bool EsAlmacenPrincipal { get; set; }
    public bool Activa { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<StockProducto> Stocks { get; set; } = new List<StockProducto>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
