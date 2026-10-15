using CapitalPos.Tcg.Api.Contracts.Auth;
using CapitalPos.Tcg.Api.Domain.Entities;

namespace CapitalPos.Tcg.Api.Application.Tenancy;

public static class TenantSettingsMapper
{
    public static TenantSettingsDto ToDto(Empresa empresa) => new()
    {
        TenantId = empresa.Id.ToString(),
        NombreComercial = empresa.NombreComercial,
        PermitirSinglesTcg = empresa.Settings.PermitirSinglesTcg,
        PermitirWooCommerceSync = empresa.Settings.PermitirWooCommerceSync,
        PermitirPosMostrador = empresa.Settings.PermitirPosMostrador,
        PermitirSubastasLoteFB = empresa.Settings.PermitirSubastasLoteFB,
        PermitirCromosSinCodigo = empresa.Settings.PermitirCromosSinCodigo
    };
}
