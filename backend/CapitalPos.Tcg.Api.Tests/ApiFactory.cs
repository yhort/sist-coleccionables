using CapitalPos.Tcg.Api.Infrastructure.Subastas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CapitalPos.Tcg.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var contentRoot = FindApiContentRoot();
        if (contentRoot is not null)
        {
            builder.UseContentRoot(contentRoot);
        }

        builder.UseSetting("WooCommerce:Modo", "Local");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ImplementationType == typeof(SubastaCierreWorker)
                                     || (d.ServiceType == typeof(IHostedService)
                                         && d.ImplementationType == typeof(SubastaCierreWorker)))
                         .ToList())
            {
                services.Remove(descriptor);
            }
        });
    }

    private static string? FindApiContentRoot()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var candidato = Path.Combine(actual.FullName, "CapitalPos.Tcg.Api");
            if (File.Exists(Path.Combine(candidato, "appsettings.Development.json")))
            {
                return candidato;
            }

            actual = actual.Parent;
        }

        return null;
    }
}
