namespace CapitalPos.Tcg.Api.Domain.Enums;

/// <summary>
/// Rarezas TCG. Los valores históricos se conservan (persistidos como string);
/// los de sets modernos (ME03+) se agregan al final.
/// </summary>
public enum RarezaTcg
{
    COMUN,
    INFRECUENTE,
    RARA,
    RARA_HOLO,
    ULTRA,
    SECRETA,
    ESPECIAL,
    DOBLE_RARA,
    ILUSTRACION_RARA,
    ILUSTRACION_ESPECIAL,
    HIPER_RARA,
    MEGA_HIPER_RARA,
    PROMO
}
