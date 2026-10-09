using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CapitalPos.Tcg.Api.Tests;

/// <summary>
/// Prueba de integración E2E: devolución por no recojo tras 30 días.
/// </summary>
[Collection("SubastaE2E")]
public sealed class DevolverNoRecogidoE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public DevolverNoRecogidoE2ETests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Devolver_no_recogido_tras_35_dias_cancela_libera_stock_y_marca_fallida()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var sedeId = DevelopmentDataSeeder.SedeId;
        var ancla = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        Guid productoId = Guid.Empty;
        Guid pedidoId = Guid.Empty;
        Guid? entregaId = null;
        Guid? pagoId = null;

        try
        {
            // 1) Producto real con stock inicial
            productoId = await CrearCartaConStockAsync(client, sedeId, "Carta E2E no recogido", cantidad: 3m);
            Log($"Producto creado: {productoId}");

            // 2) Pedido recojo tienda → Pagado → Empaquetado → PendienteEntrega (+ entrega logística)
            pedidoId = await CrearPedidoAsync(client, sedeId, "Cliente No Recogido E2E", productoId, 25m);
            Log($"Pedido creado: {pedidoId}");

            pagoId = await PagarAsync(client, 25m, pedidoId);
            Log($"Pago confirmado: {pagoId}");

            var emp = await client.PostAsJsonAsync(
                "/api/entregas/empaquetar",
                new { pedidoDigitalId = pedidoId, notasEmpaque = "Empaque prueba E2E no recogido" },
                Json);
            Assert.True(emp.IsSuccessStatusCode, await emp.Content.ReadAsStringAsync());
            var entregaEmp = await emp.Content.ReadFromJsonAsync<JsonElement>(Json);
            entregaId = GuidDe(entregaEmp, "id");
            Assert.NotEqual(Guid.Empty, entregaId.Value);
            Log($"Entrega empaquetada: {entregaId}");

            var desp = await client.PostAsJsonAsync(
                $"/api/entregas/{entregaId}/despachar",
                new
                {
                    metodoEnvio = "RECOJO_TIENDA",
                    numeroTracking = (string?)null,
                    agencia = "Tienda Miraflores",
                    costoEnvio = 0m,
                    observacion = "Listo para recojo E2E"
                },
                Json);
            Assert.True(desp.IsSuccessStatusCode, await desp.Content.ReadAsStringAsync());

            var estadoAntes = await EstadoPedidoAsync(client, pedidoId);
            Assert.Equal("PendienteEntrega", estadoAntes);
            Log($"Estado pedido antes de antedatar: {estadoAntes}");

            // Antedatar creación / despacho a 2026-08-01 (~35+ días respecto a hoy)
            await AntedatarPedidoYEntregaAsync(pedidoId, entregaId.Value, ancla);
            Log($"Fechas antedatadas a {ancla:yyyy-MM-dd} (pedido + entrega.fechaDespacho/fechaProgramada)");

            // 3) Stock justo antes de la devolución (con reserva activa)
            var stockAntes = await StockAsync(client, productoId, sedeId);
            Log(
                $"Stock ANTES devolución → disponible={stockAntes.Disponible} reservada={stockAntes.Reservada} libre={stockAntes.Libre}");

            // 4) Invocar endpoint
            var devolver = await client.PostAsJsonAsync(
                "/api/entregas/devolver-no-recogido",
                new
                {
                    pedidoDigitalId = pedidoId,
                    observacion = "E2E: no recogido en punto de entrega tras 35 días"
                },
                Json);
            var cuerpoDevolver = await devolver.Content.ReadAsStringAsync();
            Log($"POST /api/entregas/devolver-no-recogido → {(int)devolver.StatusCode} {devolver.StatusCode}");
            Log($"Respuesta: {cuerpoDevolver}");
            Assert.True(devolver.IsSuccessStatusCode, cuerpoDevolver);

            var entregaResp = JsonSerializer.Deserialize<JsonElement>(cuerpoDevolver, Json);

            // 5) Verificaciones
            var estadoPedido = await EstadoPedidoAsync(client, pedidoId);
            var estadoLogistica = StringDe(entregaResp, "estado");
            var stockDespues = await StockAsync(client, productoId, sedeId);
            var liberaciones = await ContarKardexAsync(
                client,
                productoId,
                sedeId,
                "LIBERACION_RESERVA");

            Log("========== RESULTADO E2E DEVOLVER NO RECOGIDO ==========");
            Log($"Pedido.estado          = {estadoPedido}  (esperado: Cancelado)");
            Log($"Entrega.estado         = {estadoLogistica}  (esperado: FALLIDA)");
            Log(
                $"Stock DESPUÉS          → disponible={stockDespues.Disponible} reservada={stockDespues.Reservada} libre={stockDespues.Libre}");
            Log(
                $"Δ libre                = {stockDespues.Libre - stockAntes.Libre:+0;-0;0}  (esperado: +1 por liberación de reserva)");
            Log(
                $"Δ reservada            = {stockDespues.Reservada - stockAntes.Reservada:+0;-0;0}  (esperado: -1)");
            Log($"Kardex LIBERACION_RESERVA (conteo en sede/producto) = {liberaciones} (esperado ≥ 1)");
            Log("========================================================");

            Assert.Equal("Cancelado", estadoPedido);
            Assert.Equal("FALLIDA", estadoLogistica);
            Assert.Equal(stockAntes.Libre + 1m, stockDespues.Libre);
            Assert.Equal(stockAntes.Reservada - 1m, stockDespues.Reservada);
            Assert.True(liberaciones >= 1, "Debe existir movimiento kardex LIBERACION_RESERVA");

            Log("OK: todas las aserciones pasaron. Limpiando datos de prueba…");
        }
        finally
        {
            // 6) Limpieza (best-effort)
            await LimpiarAsync(pedidoId, pagoId, entregaId, productoId, sedeId);
            Log("Limpieza de registros de prueba finalizada.");
        }
    }

    private void Log(string mensaje)
    {
        var line = $"[DevolverNoRecogidoE2E] {mensaje}";
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private async Task AntedatarPedidoYEntregaAsync(Guid pedidoId, Guid entregaId, DateTimeOffset ancla)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenant.SetEmpresa(DevelopmentDataSeeder.EmpresaId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pedido = await db.PedidosDigitales
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == pedidoId);
        pedido.FechaPedido = ancla;

        var entrega = await db.Entregas
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == entregaId);
        entrega.FechaProgramada = ancla;
        entrega.FechaDespacho = ancla;
        entrega.FechaCreacion = ancla;

        await db.SaveChangesAsync();
    }

    private async Task LimpiarAsync(
        Guid pedidoId,
        Guid? pagoId,
        Guid? entregaId,
        Guid productoId,
        Guid sedeId)
    {
        if (pedidoId == Guid.Empty && productoId == Guid.Empty)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenant.SetEmpresa(DevelopmentDataSeeder.EmpresaId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (pedidoId != Guid.Empty)
        {
            var kardexPedido = await db.MovimientosInventario
                .IgnoreQueryFilters()
                .Where(k => k.ReferenciaTipo == "PEDIDO_DIGITAL" && k.ReferenciaId == pedidoId)
                .ToListAsync();
            db.MovimientosInventario.RemoveRange(kardexPedido);

            var pagos = await db.Pagos
                .IgnoreQueryFilters()
                .Where(p => p.PedidoDigitalId == pedidoId || (pagoId.HasValue && p.Id == pagoId.Value))
                .ToListAsync();
            db.Pagos.RemoveRange(pagos);

            var historial = await db.PedidoDigitalHistorialEstados
                .IgnoreQueryFilters()
                .Where(h => h.PedidoDigitalId == pedidoId)
                .ToListAsync();
            db.PedidoDigitalHistorialEstados.RemoveRange(historial);

            if (entregaId.HasValue)
            {
                var entrega = await db.Entregas.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == entregaId.Value);
                if (entrega is not null)
                {
                    db.Entregas.Remove(entrega);
                }
            }
            else
            {
                var entregas = await db.Entregas.IgnoreQueryFilters()
                    .Where(e => e.PedidoDigitalId == pedidoId)
                    .ToListAsync();
                db.Entregas.RemoveRange(entregas);
            }

            var detalles = await db.PedidoDigitalDetalles
                .IgnoreQueryFilters()
                .Where(d => d.PedidoDigitalId == pedidoId)
                .ToListAsync();
            db.PedidoDigitalDetalles.RemoveRange(detalles);

            var pedido = await db.PedidosDigitales.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == pedidoId);
            if (pedido is not null)
            {
                db.PedidosDigitales.Remove(pedido);
            }
        }

        if (productoId != Guid.Empty)
        {
            var kardexProducto = await db.MovimientosInventario
                .IgnoreQueryFilters()
                .Where(k => k.ProductoId == productoId)
                .ToListAsync();
            db.MovimientosInventario.RemoveRange(kardexProducto);

            var stocks = await db.StocksProductos
                .IgnoreQueryFilters()
                .Where(s => s.ProductoId == productoId)
                .ToListAsync();
            db.StocksProductos.RemoveRange(stocks);

            var carta = await db.ProductosCarta.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == productoId);
            if (carta is not null)
            {
                db.ProductosCarta.Remove(carta);
            }
            else
            {
                var producto = await db.Productos.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == productoId);
                if (producto is not null)
                {
                    db.Productos.Remove(producto);
                }
            }
        }

        await db.SaveChangesAsync();
        _ = sedeId; // reservado por si se amplía limpieza por sede
    }

    private async Task LoginAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "admin@trunqi.local",
                password = "Admin123!",
                empresaId = DevelopmentDataSeeder.EmpresaId
            },
            Json);
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = StringDe(body, "accessToken");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-CapitalPos-EmpresaId");
        client.DefaultRequestHeaders.Add("X-CapitalPos-EmpresaId", DevelopmentDataSeeder.EmpresaId.ToString());
    }

    private async Task<Guid> CrearCartaConStockAsync(
        HttpClient client,
        Guid sedeId,
        string nombre,
        decimal cantidad)
    {
        var sku = $"NR-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var crear = await client.PostAsJsonAsync(
            "/api/productos-tcg",
            new
            {
                tipoProducto = "CARTA",
                nombre,
                codigoSku = sku,
                precioVenta = 25m,
                costo = 10m,
                variante = "Básica",
                carta = new
                {
                    juego = "Pokemon",
                    setCodigo = "NR1",
                    setNombre = "No Recogido E2E",
                    numeroCarta = "035",
                    rareza = "COMUN",
                    idioma = "ES",
                    condicion = "NM",
                    esFoil = false,
                    variante = "Básica"
                }
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var producto = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productoId = GuidDe(producto, "id");

        var ajuste = await client.PutAsJsonAsync(
            "/api/inventario/ajustar",
            new
            {
                productoId,
                sedeId,
                cantidad,
                sentido = "ENTRADA",
                motivo = "Stock inicial E2E no recogido"
            },
            Json);
        Assert.True(ajuste.IsSuccessStatusCode, await ajuste.Content.ReadAsStringAsync());
        return productoId;
    }

    private async Task<Guid> CrearPedidoAsync(
        HttpClient client,
        Guid sedeId,
        string clienteNombre,
        Guid productoId,
        decimal precio)
    {
        var crear = await client.PostAsJsonAsync(
            "/api/pedidos-digitales",
            new
            {
                clienteNombre,
                sedeId,
                canalPedido = "FACEBOOK_SUBASTA",
                detalles = new[]
                {
                    new { productoId, cantidad = 1, precioUnitario = precio }
                },
                entrega = new
                {
                    destinatarioNombre = clienteNombre,
                    esRecojoTienda = true,
                    puntoEntrega = "Tienda Miraflores"
                }
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var pedido = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        return GuidDe(pedido, "id");
    }

    private async Task<Guid> PagarAsync(HttpClient client, decimal monto, Guid pedidoId)
    {
        var lote = await client.PostAsJsonAsync(
            "/api/pagos/lote",
            new
            {
                origen = "YAPE",
                monto,
                codigoOperacion = $"YAPE-NR-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                confirmar = true,
                pedidoDigitalIds = new[] { pedidoId }
            },
            Json);
        Assert.True(lote.IsSuccessStatusCode, await lote.Content.ReadAsStringAsync());
        var body = await lote.Content.ReadFromJsonAsync<JsonElement>(Json);
        if (body.ValueKind == JsonValueKind.Array && body.GetArrayLength() > 0)
        {
            return GuidDe(body[0], "id");
        }

        if (body.TryGetProperty("pagos", out var pagos) && pagos.GetArrayLength() > 0)
        {
            return GuidDe(pagos[0], "id");
        }

        return GuidDe(body, "id");
    }

    private async Task<string> EstadoPedidoAsync(HttpClient client, Guid pedidoId)
    {
        var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
        return StringDe(pedido, "estado");
    }

    private async Task<(decimal Disponible, decimal Reservada, decimal Libre)> StockAsync(
        HttpClient client,
        Guid productoId,
        Guid sedeId)
    {
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/stock/{productoId}?sedeId={sedeId}",
            Json);
        return (
            stock.GetProperty("cantidadDisponible").GetDecimal(),
            stock.GetProperty("cantidadReservada").GetDecimal(),
            stock.GetProperty("cantidadLibre").GetDecimal());
    }

    private async Task<int> ContarKardexAsync(
        HttpClient client,
        Guid productoId,
        Guid sedeId,
        string tipoMovimiento)
    {
        var kardex = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/kardex?productoId={productoId}&sedeId={sedeId}&tipoMovimiento={tipoMovimiento}",
            Json);
        return kardex.ValueKind == JsonValueKind.Array ? kardex.GetArrayLength() : 0;
    }

    private static Guid GuidDe(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var valor)
            || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Guid.Empty;
        }

        if (valor.ValueKind == JsonValueKind.String && Guid.TryParse(valor.GetString(), out var parsed))
        {
            return parsed;
        }

        return valor.GetGuid();
    }

    private static string StringDe(JsonElement element, string name) =>
        element.TryGetProperty(name, out var valor) ? valor.GetString() ?? string.Empty : string.Empty;
}
