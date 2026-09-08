using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

/// <summary>
/// Adjunta <c>X-API-KEY</c> a todas las peticiones del cliente nombrado CpeApi.
/// </summary>
public sealed class CpeApiKeyHandler(IOptions<CpeApiOptions> options) : DelegatingHandler
{
    public const string HeaderName = "X-API-KEY";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey) && !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
