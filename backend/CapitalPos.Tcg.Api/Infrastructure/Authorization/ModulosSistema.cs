namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

public static class ModulosSistema
{
    public const string Dashboard = "dashboard";
    public const string ProductosTcg = "productos-tcg";
    public const string Inventario = "inventario";
    public const string AperturasTcg = "aperturas-tcg";
    public const string SubastasTcg = "subastas-tcg";
    public const string PedidosDigitales = "pedidos-digitales";
    public const string Pagos = "pagos";
    public const string Caja = "caja";
    public const string Entregas = "entregas";
    public const string Clientes = "clientes";
    public const string Proveedores = "proveedores";
    public const string WooCommerce = "woocommerce";
    public const string Reportes = "reportes";
    public const string Ecosistema = "ecosistema";

    public static readonly IReadOnlyList<string> Todos =
    [
        Dashboard,
        ProductosTcg,
        Inventario,
        AperturasTcg,
        SubastasTcg,
        PedidosDigitales,
        Pagos,
        Caja,
        Entregas,
        Clientes,
        Proveedores,
        WooCommerce,
        Reportes,
        Ecosistema
    ];
}
