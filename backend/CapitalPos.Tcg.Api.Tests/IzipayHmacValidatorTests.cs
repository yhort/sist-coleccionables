using CapitalPos.Tcg.Api.Infrastructure.Izipay;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

public sealed class IzipayHmacValidatorTests
{
    private readonly IzipayHmacValidator _validator = new();

    [Fact]
    public void Hex_del_kr_answer_crudo_es_valido()
    {
        const string answer = """{"shopId":"12345678","orderStatus":"PAID"}""";
        const string key = "test-hmac-sha256-key";
        var hash = IzipayHmacValidator.CalcularHex(answer, key);

        Assert.True(_validator.EsValida(answer, hash, key, "sha256"));
        Assert.True(_validator.EsValida(answer, hash.ToUpperInvariant(), key, "HMAC-SHA-256"));
    }

    [Fact]
    public void Hash_alterado_es_invalido()
    {
        const string answer = """{"orderStatus":"PAID"}""";
        const string key = "test-hmac-sha256-key";
        var hash = IzipayHmacValidator.CalcularHex(answer, key);

        Assert.False(_validator.EsValida(answer, "00" + hash[2..], key, "sha256"));
        Assert.False(_validator.EsValida(answer + " ", hash, key, "sha256"));
        Assert.False(_validator.EsValida(answer, hash, "otra-clave", "sha256"));
        Assert.False(_validator.EsValida(answer, hash, key, "sha1"));
    }

    [Fact]
    public void MapearOrigen_yape_plin_e_izipay()
    {
        Assert.Equal(
            Domain.Enums.OrigenPago.YAPE,
            Application.Izipay.IzipayIpnService.MapearOrigen(new Contracts.Izipay.IzipayTransaction
            {
                PaymentMethodType = "WALLET",
                PaymentMethodBrand = "YAPE"
            }));
        Assert.Equal(
            Domain.Enums.OrigenPago.PLIN,
            Application.Izipay.IzipayIpnService.MapearOrigen(new Contracts.Izipay.IzipayTransaction
            {
                PaymentMethodType = "PLIN"
            }));
        Assert.Equal(
            Domain.Enums.OrigenPago.IZIPAY,
            Application.Izipay.IzipayIpnService.MapearOrigen(new Contracts.Izipay.IzipayTransaction
            {
                TransactionDetails = new Contracts.Izipay.IzipayTransactionDetails
                {
                    CardDetails = new Contracts.Izipay.IzipayCardDetails { EffectiveBrand = "VISA" }
                }
            }));
    }
}
