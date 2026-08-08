using Microsoft.AspNetCore.Mvc;
using YOU.Api.Contracts;

namespace YOU.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Success<T>(T result, string message = "操作成功。")
    {
        return Ok(ApiResponse<T>.Ok(result, message, TraceIds.Get(HttpContext)));
    }

    protected ActionResult<ApiResponse<T>> Failure<T>(int code, string message)
    {
        return Ok(ApiResponse<T>.Fail(code, message, errors: null, TraceIds.Get(HttpContext)));
    }

    protected ActionResult<ApiResponse<T>> ValidationFailure<T>(IReadOnlyDictionary<string, string[]> errors)
    {
        return Ok(ApiResponse<T>.Fail(
            ResponseCodes.BadRequest,
            "请求参数校验失败。",
            errors,
            TraceIds.Get(HttpContext)));
    }

    protected ActionResult<ApiResponse<T>> UnauthorizedResponse<T>(string message = "未通过身份验证。")
    {
        return Failure<T>(ResponseCodes.Unauthorized, message);
    }

    protected ActionResult<ApiResponse<T>> ForbiddenResponse<T>(string message = "没有执行此操作的权限。")
    {
        return Failure<T>(ResponseCodes.Forbidden, message);
    }
}
