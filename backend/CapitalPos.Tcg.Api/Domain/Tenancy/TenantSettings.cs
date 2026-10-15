namespace CapitalPos.Tcg.Api.Domain.Tenancy;

/// <summary>
/// Feature toggles por empresa (tenant). Persistidos en <see cref="Entities.Empresa"/>.
/// </summary>
public sealed class TenantSettings
{
    public bool PermitirSinglesTcg { get; set; } = true;
    public bool PermitirWooCommerceSync { get; set; } = true;
    public bool PermitirPosMostrador { get; set; }
    public bool PermitirSubastasLoteFB { get; set; }
    public bool PermitirCromosSinCodigo { get; set; }

    public static TenantSettings DefaultsTrunqi() => new()
    {
        PermitirSinglesTcg = true,
        PermitirWooCommerceSync = true,
        PermitirPosMostrador = false,
        PermitirSubastasLoteFB = false,
        PermitirCromosSinCodigo = false
    };

    public static TenantSettings DefaultsRcp() => new()
    {
        PermitirSinglesTcg = false,
        PermitirWooCommerceSync = false,
        PermitirPosMostrador = true,
        PermitirSubastasLoteFB = true,
        PermitirCromosSinCodigo = true
    };

    public bool IsEnabled(TenantFeature feature) => feature switch
    {
        TenantFeature.PermitirSinglesTcg => PermitirSinglesTcg,
        TenantFeature.PermitirWooCommerceSync => PermitirWooCommerceSync,
        TenantFeature.PermitirPosMostrador => PermitirPosMostrador,
        TenantFeature.PermitirSubastasLoteFB => PermitirSubastasLoteFB,
        TenantFeature.PermitirCromosSinCodigo => PermitirCromosSinCodigo,
        _ => false
    };
}

public enum TenantFeature
{
    PermitirSinglesTcg,
    PermitirWooCommerceSync,
    PermitirPosMostrador,
    PermitirSubastasLoteFB,
    PermitirCromosSinCodigo
}
