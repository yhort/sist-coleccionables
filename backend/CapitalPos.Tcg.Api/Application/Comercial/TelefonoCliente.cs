using System.Text.RegularExpressions;

namespace CapitalPos.Tcg.Api.Application.Comercial;

/// <summary>Teléfono de cliente / pedido: solo dígitos, máximo 9.</summary>
public static partial class TelefonoCliente
{
    public const int MaxDigitos = 9;

    public static string? Normalizar(string? valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            return null;
        }

        Validar(texto);
        return SoloDigitos(texto);
    }

    public static void Validar(string? valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            return;
        }

        if (DigitosRegex().IsMatch(texto))
        {
            throw new BusinessRuleException(
                "El teléfono solo admite números (dígitos) y un máximo de 9 caracteres.");
        }

        if (texto.Length > MaxDigitos)
        {
            throw new BusinessRuleException(
                "El teléfono no puede tener más de 9 dígitos.");
        }
    }

    private static string SoloDigitos(string valor) => DigitosRegex().Replace(valor, string.Empty);

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitosRegex();
}
