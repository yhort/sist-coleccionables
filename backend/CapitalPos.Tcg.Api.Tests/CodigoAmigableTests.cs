using CapitalPos.Tcg.Api.Application.Common;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

public sealed class CodigoAmigableTests
{
    [Fact]
    public void Pedido_sin_correlativo_usa_prefijo_y_seis_hex()
    {
        var id = Guid.Parse("a030ed28-39a3-4c3d-8f00-123456789abc");

        Assert.Equal("#PED-A030ED", CodigoAmigable.Pedido(id));
    }

    [Fact]
    public void Pedido_usa_codigo_propio_si_no_es_guid()
    {
        var id = Guid.Parse("a030ed28-39a3-4c3d-8f00-123456789abc");

        Assert.Equal("ped-062", CodigoAmigable.Pedido(id, codigo: "ped-062"));
        Assert.Equal("PED-00042", CodigoAmigable.Pedido(id, correlativo: 42));
        Assert.Equal("#PED-00042", CodigoAmigable.Pedido(id, numeroPedido: "#PED-00042"));
    }

    [Fact]
    public void Pedido_ignora_codigo_si_es_guid()
    {
        var id = Guid.Parse("a030ed28-39a3-4c3d-8f00-123456789abc");

        Assert.Equal("#PED-A030ED", CodigoAmigable.Pedido(id, codigo: id.ToString()));
    }

    [Fact]
    public void Subasta_y_venta_recortan_el_guid()
    {
        var id = Guid.Parse("af8e43c5-1111-4111-8111-123456789abc");

        Assert.Equal("#SUB-AF8E", CodigoAmigable.Subasta(id));
        Assert.Equal("#VEN-AF8E43", CodigoAmigable.Venta(id));
        Assert.Equal("#ITM-AF8E43", CodigoAmigable.Item(id));
    }
}
