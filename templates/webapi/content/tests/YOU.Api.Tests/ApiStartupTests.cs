using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace YOU.Api.Tests;

public sealed class ApiStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiStartupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Application_starts()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/route-that-does-not-exist");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
