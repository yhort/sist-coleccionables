namespace CapitalPos.Tcg.Api.Domain.Enums;

public enum EstadoPedidoDigital
{
    PendientePago,
    Pagado,
    Empaquetado,
    /// <summary>
    /// Paquete ya dejado en el local / punto de entrega (p. ej. tienda, Papurris, TCG Center),
    /// esperando retiro del cliente. Persistido como string; pedidos históricos sin este valor siguen válidos.
    /// </summary>
    EnPuntoRecojo,
    PendienteEntrega,
    Entregado,
    Cancelado,
    /// <summary>Terminal tras NC por anulación (Cat. 09: 01, 02, …).</summary>
    Anulado,
    /// <summary>Terminal tras NC por devolución (Cat. 09: 06, 07).</summary>
    Devuelto
}
