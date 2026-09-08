using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Application.Productos;

internal static class SkuVarianteTcg
{
    public static string Generar(
        string setCodigo,
        string numero,
        RarezaTcg rareza,
        bool esFoil,
        CondicionTcg condicion,
        IdiomaTcg idioma)
    {
        var set = Sanear(setCodigo, 12);
        var num = Sanear(numero, 12);
        return $"{set}-{num}-{CodigoRareza(rareza)}-{(esFoil ? "F" : "S")}-{condicion}-{idioma}";
    }

    public static string NombreComercial(Domain.Entities.TcgCarta ficha, bool esFoil, CondicionTcg condicion)
    {
        var foil = esFoil ? "Foil" : "Standard";
        var nombre = $"{ficha.Nombre} {ficha.Set.Codigo} #{ficha.Numero} {foil} {condicion}";
        return nombre.Length <= 200 ? nombre : nombre[..200];
    }

    private static string CodigoRareza(RarezaTcg rareza) => rareza switch
    {
        RarezaTcg.COMUN => "C",
        RarezaTcg.INFRECUENTE => "U",
        RarezaTcg.RARA => "R",
        RarezaTcg.RARA_HOLO => "RH",
        RarezaTcg.ULTRA => "UR",
        RarezaTcg.SECRETA => "SEC",
        RarezaTcg.ESPECIAL => "SP",
        RarezaTcg.DOBLE_RARA => "RR",
        RarezaTcg.ILUSTRACION_RARA => "IR",
        RarezaTcg.ILUSTRACION_ESPECIAL => "SIR",
        RarezaTcg.HIPER_RARA => "HR",
        RarezaTcg.MEGA_HIPER_RARA => "MHR",
        RarezaTcg.PROMO => "PR",
        _ => rareza.ToString()
    };

    private static string Sanear(string valor, int max)
    {
        var limpio = new string(valor.Trim().Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (limpio.Length == 0)
        {
            return "X";
        }

        return limpio.Length <= max ? limpio : limpio[..max];
    }
}
