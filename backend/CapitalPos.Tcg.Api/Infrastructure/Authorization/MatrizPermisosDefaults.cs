using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

public static class MatrizPermisosDefaults
{
    public static bool EscrituraPorDefecto(RolUsuario rol, string modulo) => rol switch
    {
        RolUsuario.ADMIN => true,
        RolUsuario.CAJERO => modulo is ModulosSistema.ProductosTcg
            or ModulosSistema.SubastasTcg
            or ModulosSistema.PedidosDigitales
            or ModulosSistema.Pagos
            or ModulosSistema.Caja
            or ModulosSistema.Entregas
            or ModulosSistema.Clientes
            or ModulosSistema.Reportes,
        RolUsuario.ALMACEN => modulo is ModulosSistema.ProductosTcg
            or ModulosSistema.Inventario
            or ModulosSistema.AperturasTcg
            or ModulosSistema.Entregas
            or ModulosSistema.Proveedores,
        _ => false
    };

    public static bool LecturaPorDefecto(RolUsuario rol, string modulo)
    {
        _ = modulo;
        return Enum.IsDefined(rol);
    }
}
