using CapitalPos.Tcg.Api.Domain.Tenancy;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Empresa
{
    public Guid Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }

    public bool PermitirSinglesTcg { get; set; } = true;
    public bool PermitirWooCommerceSync { get; set; } = true;
    public bool PermitirPosMostrador { get; set; }
    public bool PermitirSubastasLoteFB { get; set; }
    public bool PermitirCromosSinCodigo { get; set; }

    public TenantSettings Settings => new()
    {
        PermitirSinglesTcg = PermitirSinglesTcg,
        PermitirWooCommerceSync = PermitirWooCommerceSync,
        PermitirPosMostrador = PermitirPosMostrador,
        PermitirSubastasLoteFB = PermitirSubastasLoteFB,
        PermitirCromosSinCodigo = PermitirCromosSinCodigo
    };

    public void AplicarSettings(TenantSettings settings)
    {
        PermitirSinglesTcg = settings.PermitirSinglesTcg;
        PermitirWooCommerceSync = settings.PermitirWooCommerceSync;
        PermitirPosMostrador = settings.PermitirPosMostrador;
        PermitirSubastasLoteFB = settings.PermitirSubastasLoteFB;
        PermitirCromosSinCodigo = settings.PermitirCromosSinCodigo;
    }

    public ICollection<Sede> Sedes { get; set; } = new List<Sede>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
