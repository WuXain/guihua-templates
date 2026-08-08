using Microsoft.AspNetCore.Mvc;
using YOU.Api.Contracts;
using YOU.Api.Models;
using YOU.Core.Features.RuntimeStatus;

namespace YOU.Api.Controllers;

[Route("api/demo")]
public sealed class DemoController : BaseController
{
    private readonly GetRuntimeStatusUseCase _getRuntimeStatus;

    public DemoController(GetRuntimeStatusUseCase getRuntimeStatus)
    {
        _getRuntimeStatus = getRuntimeStatus;
    }

    [HttpGet("greeting")]
    [ProducesResponseType(typeof(ApiResponse<DemoGreeting>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<DemoGreeting>> GetGreeting()
    {
        return Success(new DemoGreeting("Hello from the YOU Web API template."));
    }

    [HttpGet("runtime-status")]
    [ProducesResponseType(typeof(ApiResponse<RuntimeStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RuntimeStatus>>> GetRuntimeStatus(CancellationToken cancellationToken)
    {
        var status = await _getRuntimeStatus.ExecuteAsync(cancellationToken);
        return Success(status);
    }

    [HttpGet("not-found")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> GetMissingResource()
    {
        return Failure<object>(ResponseCodes.NotFound, "示例资源不存在。");
    }

    [HttpPost("echo")]
    [ProducesResponseType(typeof(ApiResponse<DemoEcho>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<DemoEcho>> Echo([FromBody] DemoEchoRequest request)
    {
        return Success(new DemoEcho(request.Message!), "请求处理成功。");
    }
}
