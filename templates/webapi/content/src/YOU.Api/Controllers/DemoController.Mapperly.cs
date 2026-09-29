using Microsoft.AspNetCore.Mvc;
using YOU.Api.Contracts;
using YOU.Api.Mapping;
using YOU.Api.Models;
using YOU.Core.Features.RuntimeStatus;

namespace YOU.Api.Controllers;

/// <summary>
/// 模板提供的接口文档、统一响应与 Mapperly 映射示例。
/// </summary>
[Route("YOU/demo")]
public sealed class DemoController : BaseController
{
    private readonly GetRuntimeStatusUseCase _getRuntimeStatus;

    /// <summary>
    /// 创建演示接口控制器。
    /// </summary>
    /// <param name="getRuntimeStatus">获取运行状态的用例。</param>
    public DemoController(GetRuntimeStatusUseCase getRuntimeStatus)
    {
        _getRuntimeStatus = getRuntimeStatus;
    }

    /// <summary>
    /// 获取一条问候信息。
    /// </summary>
    /// <response code="200">返回统一格式的问候信息。</response>
    [HttpGet("greeting")]
    [ProducesResponseType(typeof(ApiResponse<DemoGreeting>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<DemoGreeting>> GetGreeting()
    {
        return Success(new DemoGreeting { Message = "Hello from the YOU Web API template." });
    }

    /// <summary>
    /// 获取应用当前的运行状态，并映射为 API 专用响应模型。
    /// </summary>
    /// <param name="cancellationToken">请求中止时取消操作。</param>
    /// <response code="200">返回统一格式的运行状态。</response>
    [HttpGet("runtime-status")]
    [ProducesResponseType(typeof(ApiResponse<RuntimeStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RuntimeStatusResponse>>> GetRuntimeStatus(CancellationToken cancellationToken)
    {
        var status = await _getRuntimeStatus.ExecuteAsync(cancellationToken);
        return Success(DemoMapper.ToResponse(status));
    }

    /// <summary>
    /// 演示业务未找到时的统一响应。
    /// </summary>
    /// <response code="200">HTTP 状态为 200，业务结果码为未找到。</response>
    [HttpGet("not-found")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> GetMissingResource()
    {
        return Failure<object>(ResponseCodes.NotFound, "示例资源不存在。");
    }

    /// <summary>
    /// 校验并回显请求文本。
    /// </summary>
    /// <param name="request">包含待回显文本的请求体。</param>
    /// <response code="200">返回统一格式的回显结果或校验错误。</response>
    [HttpPost("echo")]
    [ProducesResponseType(typeof(ApiResponse<DemoEcho>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<DemoEcho>> Echo([FromBody] DemoEchoRequest request)
    {
        return Success(new DemoEcho { Message = request.Message! }, "请求处理成功。");
    }
}
