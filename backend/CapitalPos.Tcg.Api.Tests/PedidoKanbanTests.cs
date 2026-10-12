using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Domain.Enums;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

public sealed class PedidoKanbanTests
{
    [Fact]
    public void Empaquetado_puede_pasar_a_en_punto_recojo()
    {
        Assert.True(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.Empaquetado,
            EstadoPedidoDigital.EnPuntoRecojo,
            esRecojoTienda: true));
        Assert.True(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.Empaquetado,
            EstadoPedidoDigital.EnPuntoRecojo,
            esRecojoTienda: false));
    }

    [Fact]
    public void Empaquetado_puede_pasar_a_pendiente_entrega_courier()
    {
        Assert.True(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.Empaquetado,
            EstadoPedidoDigital.PendienteEntrega,
            esRecojoTienda: false));
    }

    [Fact]
    public void En_punto_recojo_puede_entregarse_al_cliente()
    {
        Assert.True(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.EnPuntoRecojo,
            EstadoPedidoDigital.Entregado,
            esRecojoTienda: true));
    }

    [Fact]
    public void En_punto_recojo_admite_cancelacion_devolucion_tienda()
    {
        Assert.True(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.EnPuntoRecojo,
            EstadoPedidoDigital.Cancelado,
            esRecojoTienda: true));
    }

    [Fact]
    public void Entregado_es_terminal_y_no_admite_devolucion()
    {
        Assert.True(PedidoKanban.EsTerminal(EstadoPedidoDigital.Entregado));
        Assert.False(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.Entregado,
            EstadoPedidoDigital.Cancelado,
            esRecojoTienda: true));
        Assert.False(PedidoKanban.PuedeTransicionar(
            EstadoPedidoDigital.Entregado,
            EstadoPedidoDigital.EnPuntoRecojo,
            esRecojoTienda: true));
    }

    [Theory]
    [InlineData(MotivoDevolucionTienda.PlazoRetiroVencido, "Plazo de retiro vencido")]
    [InlineData(MotivoDevolucionTienda.ClienteConfirmoNoRetirara, "Cliente confirmó")]
    [InlineData(MotivoDevolucionTienda.CancelacionDevolucionSolicitada, "Cancelación")]
    public void Etiqueta_motivo_devolucion_tienda_es_legible(
        MotivoDevolucionTienda motivo,
        string fragmento)
    {
        Assert.Contains(
            fragmento,
            PedidoKanban.EtiquetaMotivoDevolucionTienda(motivo),
            StringComparison.Ordinal);
    }
}
