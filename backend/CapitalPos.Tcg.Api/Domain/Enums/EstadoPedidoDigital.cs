namespace CapitalPos.Tcg.Api.Domain.Enums;

public enum EstadoPedidoDigital
{
    PendientePago,
    Pagado,
    Empaquetado,
    PendienteEntrega,
    Entregado,
    Cancelado,
    /// <summary>Terminal tras NC por anulación (Cat. 09: 01, 02, …).</summary>
    Anulado,
    /// <summary>Terminal tras NC por devolución (Cat. 09: 06, 07).</summary>
    Devuelto
}
