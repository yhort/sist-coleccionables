using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Comprobante : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid VentaId { get; set; }
    public Guid? BoletaConsolidadaId { get; set; }
    public TipoComprobanteSunat Tipo { get; set; }
    public string TipoSunat { get; set; } = "03";
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }
    public EstadoEmisionSunat Estado { get; set; } = EstadoEmisionSunat.SIMULADO;
    public string? PayloadJson { get; set; }
    public string? Xml { get; set; }
    public string? Cdr { get; set; }
    public string? HashFirma { get; set; }
    public string? Mensaje { get; set; }
    public string? CodigoMotivo { get; set; }
    public string? DescripcionMotivo { get; set; }
    public string? DocumentoReferencia { get; set; }
    public DateTimeOffset FechaEmision { get; set; }
    public DateTimeOffset? FechaEnvioSunat { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Venta Venta { get; set; } = null!;
    public BoletaConsolidada? BoletaConsolidada { get; set; }
}
