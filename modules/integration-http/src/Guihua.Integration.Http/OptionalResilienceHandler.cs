using Microsoft.Extensions.Options;

namespace Guihua.Integration.Http;

internal sealed class OptionalResilienceHandler(IOptions<HttpIntegrationOptions> options) : DelegatingHandler
{
    private readonly HttpIntegrationOptions options = options.Value;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!options.EnableResilience || !IsSafeToRetry(request.Method) || options.MaxRetries == 0)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt >= options.MaxRetries)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < options.MaxRetries)
            {
            }

            await Task.Delay(options.RetryDelay, cancellationToken);
        }
    }

    private static bool IsSafeToRetry(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        (int)statusCode == 408 || (int)statusCode == 429 || (int)statusCode >= 500;
}
