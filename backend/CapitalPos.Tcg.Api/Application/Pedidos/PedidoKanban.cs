using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Application.Pedidos;

public static class PedidoKanban
{
    /// <summary>
    /// Flujo operativo. EnPreparacion = Empaquetado; ListoEntrega = PendienteEntrega.
    /// </summary>
    public static readonly EstadoPedidoDigital[] Flujo =
    [
        EstadoPedidoDigital.PendientePago,
        EstadoPedidoDigital.Pagado,
        EstadoPedidoDigital.Empaquetado,
        EstadoPedidoDigital.PendienteEntrega,
        EstadoPedidoDigital.Entregado
    ];

    public static bool PuedeTransicionar(
        EstadoPedidoDigital actual,
        EstadoPedidoDigital destino,
        bool esRecojoTienda)
    {
        if (actual is EstadoPedidoDigital.Entregado or EstadoPedidoDigital.Cancelado)
        {
            return false;
        }

        if (destino == EstadoPedidoDigital.Cancelado)
        {
            return actual != EstadoPedidoDigital.Entregado;
        }

        if (destino == EstadoPedidoDigital.Entregado)
        {
            // Recojo en mostrador: desde Pagado o Empaquetado (sin pasar por PendienteEntrega).
            return actual == EstadoPedidoDigital.PendienteEntrega
                || (esRecojoTienda
                    && actual is EstadoPedidoDigital.Pagado or EstadoPedidoDigital.Empaquetado);
        }

        var indice = Array.IndexOf(Flujo, actual);
        var siguiente = indice >= 0 && indice + 1 < Flujo.Length ? Flujo[indice + 1] : (EstadoPedidoDigital?)null;
        return siguiente == destino && destino != EstadoPedidoDigital.Entregado;
    }

    public static IndicadorReservaPedido IndicadorDe(EstadoPedidoDigital estado) => estado switch
    {
        EstadoPedidoDigital.Cancelado => IndicadorReservaPedido.Liberado,
        EstadoPedidoDigital.Entregado => IndicadorReservaPedido.Confirmado,
        _ => IndicadorReservaPedido.Reservado
    };

    public static string EtiquetaTransicion(EstadoPedidoDigital estado) => estado switch
    {
        EstadoPedidoDigital.Pagado => "Pago registrado. El pedido pasa a empaque.",
        EstadoPedidoDigital.Empaquetado => "Pedido empaquetado.",
        EstadoPedidoDigital.PendienteEntrega => "Listo para despacho o recojo.",
        EstadoPedidoDigital.Entregado => "Entrega confirmada. Se convierte a venta y se confirma la reserva.",
        EstadoPedidoDigital.Cancelado => "Pedido cancelado. Se libera la reserva de stock.",
        _ => $"Estado actualizado a {estado}."
    };

    public static MetodoEnvio MetodoDesdeSnapshot(bool esRecojoTienda, string? courier)
    {
        if (esRecojoTienda)
        {
            return MetodoEnvio.RECOJO_TIENDA;
        }

        var texto = (courier ?? string.Empty).ToLowerInvariant();
        if (texto.Contains("shalom"))
        {
            return MetodoEnvio.SHALOM;
        }

        if (texto.Contains("moto") || texto.Contains("delivery"))
        {
            return MetodoEnvio.DELIVERY_MOTO;
        }

        return MetodoEnvio.OLVA_COURIER;
    }

    public static string EtiquetaCourier(MetodoEnvio metodo) => metodo switch
    {
        MetodoEnvio.RECOJO_TIENDA => "Recojo en tienda",
        MetodoEnvio.OLVA_COURIER => "Olva Courier",
        MetodoEnvio.SHALOM => "Shalom",
        MetodoEnvio.DELIVERY_MOTO => "Delivery moto",
        _ => metodo.ToString()
    };
}
