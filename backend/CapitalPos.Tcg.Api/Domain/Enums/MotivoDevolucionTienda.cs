namespace CapitalPos.Tcg.Api.Domain.Enums;

/// <summary>
/// Motivo operativo de la devolución de tienda (liberación de stock / no recojo).
/// </summary>
public enum MotivoDevolucionTienda
{
    /// <summary>Plazo de retiro vencido (Abandono automático).</summary>
    PlazoRetiroVencido,

    /// <summary>Cliente confirmó que no retirará (Abandono explícito).</summary>
    ClienteConfirmoNoRetirara,

    /// <summary>Cancelación / Devolución solicitada.</summary>
    CancelacionDevolucionSolicitada
}
