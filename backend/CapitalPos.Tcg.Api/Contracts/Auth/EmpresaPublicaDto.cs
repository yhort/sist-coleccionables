namespace CapitalPos.Tcg.Api.Contracts.Auth;

public sealed class EmpresaPublicaDto
{
    public required Guid Id { get; init; }
    public required string NombreComercial { get; init; }
    public required string RazonSocial { get; init; }
}

public sealed class EmpresasDisponiblesResponse
{
    public required IReadOnlyList<EmpresaPublicaDto> Empresas { get; init; }
    public required bool UnicoTenant { get; init; }
}
