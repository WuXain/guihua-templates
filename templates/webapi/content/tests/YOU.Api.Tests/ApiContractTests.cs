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

    /// <summary>
    /// 验证项目前缀下的问候接口返回统一成功结果。
    /// </summary>
    [Fact]
    public async Task Success_is_http_200_with_numeric_success_code_and_result()
    {
        using var response = await _client.GetAsync("/YOU/demo/greeting");
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, body.RootElement.GetProperty("code").GetInt32());
        Assert.Equal(
            "Hello from the YOU Web API template.",
            body.RootElement.GetProperty("result").GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    /// <summary>
    /// 验证运行状态接口在项目前缀下调用核心用例。
    /// </summary>
    [Fact]
    public async Task Runtime_status_exercises_the_core_use_case_through_the_api()
    {
        using var response = await _client.GetAsync("/YOU/demo/runtime-status");
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, body.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("Ready", body.RootElement.GetProperty("result").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("result").GetProperty("observedAtUtc").ValueKind);
    }

    /// <summary>
    /// 验证项目前缀下的业务失败仍使用数值应用结果码。
    /// </summary>
    [Fact]
    public async Task Business_failure_is_http_200_with_numeric_application_code()
    {
        using var response = await _client.GetAsync("/YOU/demo/not-found");
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(404, body.RootElement.GetProperty("code").GetInt32());
    }

    /// <summary>
    /// 验证项目前缀下的请求模型校验响应。
    /// </summary>
    [Fact]
    public async Task Model_validation_is_http_200_with_field_errors()
    {
        using var response = await _client.PostAsJsonAsync("/YOU/demo/echo", new { });
        using var body = await ReadBody(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(400, body.RootElement.GetProperty("code").GetInt32());
        Assert.Contains(
            body.RootElement.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, nameof(DemoEchoRequest.Message), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证项目前缀下的异常示例不会泄露服务端异常信息。
    /// </summary>
    [Fact]
    public async Task Unhandled_exception_is_safe_and_keeps_http_200_contract()
    {
        using var response = await _client.GetAsync("/YOU/demo/exception");
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

    /// <summary>
    /// 验证旧的通用 api 前缀不再暴露同名接口，避免网关路由冲突。
    /// </summary>
    [Fact]
    public async Task Old_generic_api_prefix_is_not_registered()
    {
        using var response = await _client.GetAsync("/api/demo/greeting");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证 OpenAPI 文档呈现生成项目专属的接口路径。
    /// </summary>
    [Fact]
    public async Task OpenApi_json_describes_the_demo_controller()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/YOU/demo/greeting", json, StringComparison.Ordinal);
        Assert.Contains("ApiResponse", json, StringComparison.Ordinal);
        Assert.Contains("获取一条问候信息", json, StringComparison.Ordinal);
        Assert.Contains("要由服务端原样返回的文本", json, StringComparison.Ordinal);
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

    /// <summary>
    /// 验证开发环境专用的异常示例端点不会在生产环境注册。
    /// </summary>
    [Fact]
    public async Task Demo_exception_route_is_not_registered_in_production()
    {
        using var response = await _client.GetAsync("/YOU/demo/exception");

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
