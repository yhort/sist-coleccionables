using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CapitalPos.Tcg.Api.Contracts.Izipay;

/// <summary>
/// Campos IPN Lyra/Izipay en <c>application/x-www-form-urlencoded</c>.
/// El HMAC se calcula sobre <see cref="KrAnswer"/> crudo (JSON decodificado del form).
/// </summary>
public sealed class IzipayIpnForm
{
    public string KrHash { get; init; } = string.Empty;
    public string KrHashAlgorithm { get; init; } = "sha256";
    public string KrHashKey { get; init; } = string.Empty;
    public string KrAnswer { get; init; } = string.Empty;

    public static IzipayIpnForm From(IFormCollection form) => new()
    {
        KrHash = form["kr-hash"].ToString(),
        KrHashAlgorithm = form["kr-hash-algorithm"].ToString(),
        KrHashKey = form["kr-hash-key"].ToString(),
        KrAnswer = form["kr-answer"].ToString()
    };
}

public sealed class IzipayKrAnswer
{
    public string? ShopId { get; set; }
    public string? OrderStatus { get; set; }
    public IzipayOrderDetails? OrderDetails { get; set; }
    public IzipayCustomer? Customer { get; set; }
    public IReadOnlyList<IzipayTransaction>? Transactions { get; set; }
}

public sealed class IzipayOrderDetails
{
    public string? OrderId { get; set; }
    public long? OrderTotalAmount { get; set; }
    public string? OrderCurrency { get; set; }
}

public sealed class IzipayCustomer
{
    public string? Email { get; set; }
    public IzipayBillingDetails? BillingDetails { get; set; }
}

public sealed class IzipayBillingDetails
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public sealed class IzipayTransaction
{
    public string? Uuid { get; set; }
    public string? Status { get; set; }
    public long? Amount { get; set; }
    public string? PaymentMethodType { get; set; }
    public string? PaymentMethodBrand { get; set; }
    public IzipayTransactionDetails? TransactionDetails { get; set; }
}

public sealed class IzipayTransactionDetails
{
    public IzipayCardDetails? CardDetails { get; set; }
}

public sealed class IzipayCardDetails
{
    public string? EffectiveBrand { get; set; }
}

public sealed class GuardarIntegracionIzipayRequest
{
    [Required]
    [MaxLength(32)]
    public string ShopId { get; set; } = string.Empty;

    /// <summary>Clave HMAC-SHA-256 del Back Office. Vacío conserva la ya guardada.</summary>
    [MaxLength(200)]
    public string? HmacSha256Clave { get; set; }

    public ModoIzipay Modo { get; set; } = ModoIzipay.TEST;

    public bool Activa { get; set; } = true;
}

public sealed class IntegracionIzipayResponse
{
    public required Guid Id { get; init; }
    public required string ShopId { get; init; }
    public required string HmacSha256Enmascarada { get; init; }
    public required bool TieneHmac { get; init; }
    public required ModoIzipay Modo { get; init; }
    public required bool Activa { get; init; }
}

public readonly record struct IzipayIpnResult(int StatusCode)
{
    public static IzipayIpnResult Ok { get; } = new(StatusCodes.Status200OK);
    public static IzipayIpnResult Unauthorized { get; } = new(StatusCodes.Status401Unauthorized);
}
