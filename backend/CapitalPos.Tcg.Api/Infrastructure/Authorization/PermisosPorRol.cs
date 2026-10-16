using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

/// <summary>
/// Mapeo de permisos operativos por rol, alineado con la matriz de módulos del Ecosistema:
/// CAJERO opera ventas (y escritura en Productos TCG vía endpoints que aceptan OperarVentas);
/// ALMACEN opera almacén/inventario; ADMIN tiene acceso total.
/// </summary>
public static class PermisosPorRol
{
    public static bool Tiene(RolUsuario rol, Permiso permiso) => rol switch
    {
        RolUsuario.ADMIN => true,
        RolUsuario.ALMACEN => permiso is Permiso.OperarAlmacen,
        RolUsuario.CAJERO => permiso is Permiso.OperarVentas,
        _ => false
    };
}
