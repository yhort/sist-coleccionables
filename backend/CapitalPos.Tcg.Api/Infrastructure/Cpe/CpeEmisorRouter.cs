using CapitalPos.Tcg.Api.Application.Cpe;
using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

/// <summary>
/// Si hay <c>CpeApi:BaseUrl</c> y <c>Modo</c> distinto de Local, envía a
/// <c>capitalpos-cpe-api</c>. Si no, firma UBL en proceso.
/// </summary>
public sealed class CpeEmisorRouter(IHttpClientFactory httpFactory, IOptions<CpeApiOptions> options) : ICpeEmisor
{
    private readonly LocalUblCpeEmisor _local = new();

    public string Nombre => UsaHttp ? "CpeApi" : "LocalUBL";

    private bool UsaHttp =>
        !string.IsNullOrWhiteSpace(options.Value.BaseUrl)
        && !string.Equals(options.Value.Modo, "Local", StringComparison.OrdinalIgnoreCase);

    public Task<Contracts.Cpe.CpeEmisionResultado> EmitirAsync(
        Contracts.Cpe.EmitirCpeRequest request,
        CancellationToken cancellationToken)
    {
        if (!UsaHttp)
        {
            return _local.EmitirAsync(request, cancellationToken);
        }

        var http = new CpeApiClient(httpFactory.CreateClient("CpeApi"), options);
        return http.EmitirAsync(request, cancellationToken);
    }
}
