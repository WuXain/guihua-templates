using System.Net.Http.Json;

namespace Guihua.Integration.Http;

public interface IExternalStatusClient
{
    Task<ExternalStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed class ExternalStatusClient(HttpClient httpClient) : IExternalStatusClient
{
    public async Task<ExternalStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("status", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ExternalStatusResponse>(cancellationToken);
        return result ?? throw new InvalidOperationException("The external status response was empty.");
    }
}

public sealed record ExternalStatusResponse(string Status, string? Version = null);
