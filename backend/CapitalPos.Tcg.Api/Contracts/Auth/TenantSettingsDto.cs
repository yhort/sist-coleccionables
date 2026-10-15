namespace CapitalPos.Tcg.Api.Contracts.Auth;

public sealed class TenantSettingsDto
{
    public required string TenantId { get; init; }
    public required string NombreComercial { get; init; }
    public required bool PermitirSinglesTcg { get; init; }
    public required bool PermitirWooCommerceSync { get; init; }
    public required bool PermitirPosMostrador { get; init; }
    public required bool PermitirSubastasLoteFB { get; init; }
    public required bool PermitirCromosSinCodigo { get; init; }
}
