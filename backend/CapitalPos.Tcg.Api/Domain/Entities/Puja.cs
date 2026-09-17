namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Puja : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SubastaTcgId { get; set; }
    /// <summary>
    /// En modo INDIVIDUALES identifica la carta/línea pujada. Null en COMBO (puja al lote completo).
    /// </summary>
    public Guid? SubastaDetalleId { get; set; }
    public Guid? ClienteId { get; set; }
    public string NombrePostor { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public bool EsGanadora { get; set; }

    public SubastaTcg Subasta { get; set; } = null!;
    public SubastaDetalle? Detalle { get; set; }
}
