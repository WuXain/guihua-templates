using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using YOU.Api.Models;
using Xunit;

namespace YOU.Api.Tests;

public sealed class ApiContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Success_is_http_200_with_numeric_success_code_and_result()
    {
        using var response = await _client.GetAsync("/api/demo/greeting");
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, body.RootElement.GetProperty("code").GetInt32());
        Assert.Equal(
            "Hello from the YOU Web API template.",
            body.RootElement.GetProperty("result").GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Runtime_status_exercises_the_core_use_case_through_the_api()
    {
        using var response = await _client.GetAsync("/api/demo/runtime-status");
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, body.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("Ready", body.RootElement.GetProperty("result").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("result").GetProperty("observedAtUtc").ValueKind);
    }

    [Fact]
    public async Task Business_failure_is_http_200_with_numeric_application_code()
    {
        using var response = await _client.GetAsync("/api/demo/not-found");
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(404, body.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Model_validation_is_http_200_with_field_errors()
    {
        using var response = await _client.PostAsJsonAsync("/api/demo/echo", new { });
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(400, body.RootElement.GetProperty("code").GetInt32());
        Assert.Contains(
            body.RootElement.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, nameof(DemoEchoRequest.Message), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Unhandled_exception_is_safe_and_keeps_http_200_contract()
    {
        using var response = await _client.GetAsync("/api/demo/exception");
        using var body = await ReadBody(response);
        var json = body.RootElement.GetRawText();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(500, body.RootElement.GetProperty("code").GetInt32());
        Assert.DoesNotContain("InvalidOperationException", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("This exception must stay on the server", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_route_remains_a_real_http_404()
    {
        using var response = await _client.GetAsync("/route-that-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_json_describes_the_demo_controller()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/demo/greeting", json, StringComparison.Ordinal);
        Assert.Contains("ApiResponse", json, StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadBody(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}

public sealed class ApiContractFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

public sealed class ProductionApiTests : IClassFixture<ProductionApiFactory>
{
    private readonly HttpClient _client;

    public ProductionApiTests(ProductionApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Demo_exception_route_is_not_registered_in_production()
    {
        using var response = await _client.GetAsync("/api/demo/exception");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_is_not_exposed_in_production()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class ProductionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }
}
