using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Sedes;

public sealed class SedeResponse
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public required TipoSede Tipo { get; init; }
    public string? Direccion { get; init; }
    public string? Distrito { get; init; }
    public string? Provincia { get; init; }
    public string? Departamento { get; init; }
    public string? Ubigeo { get; init; }
    public required bool EsAlmacenPrincipal { get; init; }
    public required bool Activa { get; init; }
}
