namespace CapitalPos.Tcg.Api.Domain.Enums;

/// <summary>
/// COMBO = lote único (1 ganador / 1 pedido).
/// INDIVIDUALES = evento/sala única con N cartas subastadas y adjudicadas por separado.
/// </summary>
public enum ModoSubastaTcg
{
    COMBO = 0,
    INDIVIDUALES = 1
}
