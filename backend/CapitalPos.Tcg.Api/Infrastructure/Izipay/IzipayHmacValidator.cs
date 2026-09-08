using System.Security.Cryptography;
using System.Text;

namespace CapitalPos.Tcg.Api.Infrastructure.Izipay;

/// <summary>
/// Valida el IPN REST de Lyra/Izipay: HMAC-SHA-256 del JSON <c>kr-answer</c> crudo
/// con la clave HMAC-SHA-256 del Back Office. El hash recibido va en hexadecimal (o Base64).
/// </summary>
public sealed class IzipayHmacValidator
{
    public bool EsValida(string krAnswer, string? krHash, string hmacKey, string? algoritmo)
    {
        if (string.IsNullOrEmpty(krAnswer)
            || string.IsNullOrWhiteSpace(krHash)
            || string.IsNullOrWhiteSpace(hmacKey))
        {
            return false;
        }

        if (!AlgoritmoPermitido(algoritmo))
        {
            return false;
        }

        var computed = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(hmacKey),
            Encoding.UTF8.GetBytes(krAnswer));

        if (!TryDecodificarHash(krHash.Trim(), out var received) || received.Length != computed.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computed, received);
    }

    public static string CalcularHex(string krAnswer, string hmacKey)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(hmacKey),
            Encoding.UTF8.GetBytes(krAnswer));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool AlgoritmoPermitido(string? algoritmo)
    {
        if (string.IsNullOrWhiteSpace(algoritmo))
        {
            return true;
        }

        return algoritmo.Equals("sha256", StringComparison.OrdinalIgnoreCase)
            || algoritmo.Equals("sha-256", StringComparison.OrdinalIgnoreCase)
            || algoritmo.Equals("HMAC-SHA-256", StringComparison.OrdinalIgnoreCase)
            || algoritmo.Equals("HmacSHA256", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDecodificarHash(string hash, out byte[] bytes)
    {
        bytes = [];
        var hex = hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hash[2..] : hash;
        if (hex.Length % 2 == 0 && hex.All(EsHex))
        {
            try
            {
                bytes = Convert.FromHexString(hex);
                return bytes.Length > 0;
            }
            catch (FormatException)
            {
                // Se intenta Base64 más abajo.
            }
        }

        try
        {
            bytes = Convert.FromBase64String(hash);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool EsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
