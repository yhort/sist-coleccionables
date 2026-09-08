using CapitalPos.Tcg.Api.Application.WooCommerce;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

public sealed class WooCommerceWebhookRetryHostedService(
    IServiceScopeFactory scopes,
    ILogger<WooCommerceWebhookRetryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(45));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var pedidos = scope.ServiceProvider.GetRequiredService<WooCommercePedidoImportService>();
                var publisher = scope.ServiceProvider.GetRequiredService<WooStockPublisher>();
                var resultado = await pedidos.ReintentarPendientesAsync(stoppingToken);
                await publisher.FlushAsync(stoppingToken);
                if (resultado.Importados > 0 || resultado.Errores > 0)
                {
                    logger.LogInformation(
                        "Reintento Woo: {Importados} recuperados, {Errores} aún fallidos, {Omitidos} omitidos.",
                        resultado.Importados,
                        resultado.Errores,
                        resultado.Omitidos);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fallo el ciclo de reintento de webhooks WooCommerce.");
            }
        }
    }
}
