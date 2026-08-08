using System.Net;
using System.Net.Http.Json;
using Guihua.Integration.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guihua.Integration.Http.Tests;

public sealed class HttpIntegrationTests
{
    [Fact]
    public async Task TypedClient_UsesConfiguredBaseAddressAndReturnsTypedResponse()
    {
        using var provider = CreateProvider(
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ExternalStatusResponse("ready", "test"))
            }),
            options =>
            {
                options.BaseAddress = new Uri("http://127.0.0.1/");
                options.Timeout = TimeSpan.FromSeconds(2);
            });

        var client = provider.GetRequiredService<IExternalStatusClient>();
        var result = await client.GetStatusAsync();

        Assert.Equal("ready", result.Status);
        Assert.Equal("test", result.Version);
        var handler = provider.GetRequiredService<StubHandler>();
        Assert.Equal(new Uri("http://127.0.0.1/status"), handler.LastRequestUri);
    }

    [Fact]
    public void InvalidOptions_FailWhenOptionsAreFirstResolved()
    {
        using var provider = CreateProvider(
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            options =>
            {
                options.BaseAddress = new Uri("file:///not-http");
                options.Timeout = TimeSpan.Zero;
            });

        var exception = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<HttpIntegrationOptions>>().Value);

        Assert.Contains(nameof(HttpIntegrationOptions.BaseAddress), exception.Message);
        Assert.Contains(nameof(HttpIntegrationOptions.Timeout), exception.Message);
    }

    [Fact]
    public async Task OptionalResilience_RetriesTransientGetOnly()
    {
        var requests = 0;
        using var provider = CreateProvider(
            new StubHandler(_ =>
            {
                requests++;
                return requests == 1
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new ExternalStatusResponse("ready"))
                    };
            }),
            options =>
            {
                options.BaseAddress = new Uri("http://127.0.0.1/");
                options.Timeout = TimeSpan.FromSeconds(2);
                options.EnableResilience = true;
                options.MaxRetries = 1;
                options.RetryDelay = TimeSpan.Zero;
            });

        var result = await provider.GetRequiredService<IExternalStatusClient>().GetStatusAsync();

        Assert.Equal("ready", result.Status);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task OptionalResilience_DoesNotRetryNonIdempotentRequest()
    {
        var requests = 0;
        var primaryHandler = new StubHandler(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        using var resilienceHandler = new OptionalResilienceHandler(
            Microsoft.Extensions.Options.Options.Create(new HttpIntegrationOptions
            {
                BaseAddress = new Uri("http://127.0.0.1/"),
                Timeout = TimeSpan.FromSeconds(2),
                EnableResilience = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.Zero
            }))
        {
            InnerHandler = primaryHandler
        };
        using var httpClient = new HttpClient(resilienceHandler)
        {
            BaseAddress = new Uri("http://127.0.0.1/")
        };
        using var response = await httpClient.PostAsync("status", JsonContent.Create(new { value = "once" }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, requests);
    }

    private static ServiceProvider CreateProvider(
        StubHandler handler,
        Action<HttpIntegrationOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        var builder = services.AddHttpIntegration(configure);
        builder.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            serviceProvider.GetRequiredService<StubHandler>());
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
