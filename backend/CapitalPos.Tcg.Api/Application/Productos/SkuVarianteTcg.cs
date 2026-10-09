using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Application.Productos;

internal static class SkuVarianteTcg
{
    public static readonly string[] VariantesPreset =
    [
        "Básica",
        "Holo",
        "Reverse",
        "EX",
        "Jumbo",
        "Promo"
    ];

    public static string NormalizarVariante(string? variante, bool esFoilFallback)
    {
        var texto = (variante ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            return esFoilFallback ? "Holo" : "Básica";
        }

        foreach (var preset in VariantesPreset)
        {
            if (string.Equals(preset, texto, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return texto.Length <= 64 ? texto : texto[..64];
    }

    public static bool EsFoilDesdeVariante(string variante) =>
        !string.Equals(variante, "Básica", StringComparison.OrdinalIgnoreCase);

    public static string Generar(
        string setCodigo,
        string numero,
        RarezaTcg rareza,
        bool esFoil,
        CondicionTcg condicion,
        IdiomaTcg idioma) =>
        Generar(setCodigo, numero, rareza, esFoil ? "Holo" : "Básica", condicion, idioma);

    public static string Generar(
        string setCodigo,
        string numero,
        RarezaTcg rareza,
        string variante,
        CondicionTcg condicion,
        IdiomaTcg idioma)
    {
        var set = Sanear(setCodigo, 12);
        var num = Sanear(numero, 12);
        return $"{set}-{num}-{CodigoRareza(rareza)}-{CodigoVariante(variante)}-{condicion}-{idioma}";
    }

    public static string NombreComercial(Domain.Entities.TcgCarta ficha, bool esFoil, CondicionTcg condicion) =>
        NombreComercial(ficha, esFoil ? "Holo" : "Básica", condicion);

    public static string NombreComercial(Domain.Entities.TcgCarta ficha, string variante, CondicionTcg condicion)
    {
        var nombre = $"{ficha.Nombre} {ficha.Set.Codigo} #{ficha.Numero} {variante} {condicion}";
        return nombre.Length <= 200 ? nombre : nombre[..200];
    }

    private static string CodigoVariante(string variante)
    {
        var normalizada = NormalizarVariante(variante, esFoilFallback: false);
        return normalizada.ToLowerInvariant() switch
        {
            "básica" or "basica" => "BAS",
            "holo" => "HOL",
            "reverse" => "REV",
            "ex" => "EX",
            "jumbo" => "JMB",
            "promo" => "PRO",
            _ => Sanear(normalizada, 6).ToUpperInvariant()
        };
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
