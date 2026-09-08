using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

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
