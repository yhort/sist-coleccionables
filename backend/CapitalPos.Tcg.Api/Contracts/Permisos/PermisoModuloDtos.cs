namespace CapitalPos.Tcg.Api.Contracts.Permisos;

public sealed class PermisoModuloDto
{
    public required string Modulo { get; init; }
    public required bool Lectura { get; init; }
    public required bool Escritura { get; init; }
}

public sealed class MatrizPermisosResponse
{
    public required IReadOnlyList<PermisoModuloDto> Admin { get; init; }
    public required IReadOnlyList<PermisoModuloDto> Cajero { get; init; }
    public required IReadOnlyList<PermisoModuloDto> Almacen { get; init; }
}

public sealed class GuardarMatrizPermisosRequest
{
    public List<PermisoModuloItemRequest> Admin { get; set; } = [];
    public List<PermisoModuloItemRequest> Cajero { get; set; } = [];
    public List<PermisoModuloItemRequest> Almacen { get; set; } = [];
}

public sealed class PermisoModuloItemRequest
{
    public string Modulo { get; set; } = string.Empty;
    public bool Lectura { get; set; }
    public bool Escritura { get; set; }
}
