namespace CapitalPos.Tcg.Api.Domain.Enums;

public enum EstadoSubastaDetalle
{
    PENDIENTE = 0,
    ADJUDICADO = 1,
    /// <summary>Puja cerrada sin ganador ni pedido (declarada desierta).</summary>
    DESIERTA = 2
}
