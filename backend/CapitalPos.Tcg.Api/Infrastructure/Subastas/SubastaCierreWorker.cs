using CapitalPos.Tcg.Api.Application.Subastas;

namespace CapitalPos.Tcg.Api.Infrastructure.Subastas;

public sealed class SubastaCierreWorker(
    IServiceScopeFactory scopes,
    ILogger<SubastaCierreWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SubastaCierreWorker activo. Intervalo {Intervalo}.", Intervalo);

        using var timer = new PeriodicTimer(Intervalo);
        await CerrarCicloAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CerrarCicloAsync(stoppingToken);
        }
    }

    private async Task CerrarCicloAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var subastas = scope.ServiceProvider.GetRequiredService<SubastasTcgService>();
            var resultado = await subastas.CerrarVencidasAsync(stoppingToken);
            if (resultado.Cerradas > 0)
            {
                logger.LogInformation(
                    "Cierre automático de subastas: {Cerradas} pasaron a CERRADA.",
                    resultado.Cerradas);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado ordenado
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falló el ciclo de cierre automático de subastas.");
        }
    }
}
