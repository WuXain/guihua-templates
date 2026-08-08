using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using YOU.Api.Contracts;
using YOU.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add(new ProducesAttribute("application/json"));
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = actionContext =>
        {
            var errors = actionContext.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => string.IsNullOrWhiteSpace(entry.Key) ? "$" : entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "请求值无效。"
                            : error.ErrorMessage)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var response = ApiResponse<object?>.Fail(
                ResponseCodes.BadRequest,
                "请求参数校验失败。",
                errors,
                TraceIds.Get(actionContext.HttpContext));

            return new OkObjectResult(response);
        };
    });
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "YOU Web API",
        Version = "v1",
        Description = "业务 JSON 结果统一使用 HTTP 200；code 是数值应用结果码。"
    });
});
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        app.Logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object?>.Fail(
            ResponseCodes.InternalServerError,
            "服务器处理请求时发生未预期错误。",
            errors: null,
            TraceIds.Get(context));

        await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    });
});

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.MapGet("/api/demo/exception", () =>
        ThrowDemoException());
}

app.MapControllers();

static IResult ThrowDemoException()
{
    throw new InvalidOperationException("This exception must stay on the server.");
}

app.Run();

public partial class Program;
