using Microsoft.AspNetCore.DataProtection;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

public sealed class WooCredentialProtector(IDataProtectionProvider dataProtection)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("CapitalPos.WooCommerce.Credentials");

    public string Cifrar(string valorPlano) => _protector.Protect(valorPlano);

    public string Descifrar(string valorCifrado) => _protector.Unprotect(valorCifrado);

    public static string Enmascarar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length < 6)
        {
            return valor is { Length: > 0 } ? "****" : string.Empty;
        }

        return valor[..3] + new string('*', Math.Max(4, valor.Length - 5)) + valor[^2..];
    }
}
