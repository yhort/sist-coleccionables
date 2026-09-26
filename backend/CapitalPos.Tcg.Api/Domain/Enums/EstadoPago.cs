namespace CapitalPos.Tcg.Api.Domain.Enums;

public enum EstadoPago
{
    NOTIFICADO,
    ASOCIADO,
    CONFIRMADO,
    RECHAZADO,
    /// <summary>Anulación formal (soft). Conserva el registro para auditoría; no suma a caja ni cobertura.</summary>
    ANULADO
}
