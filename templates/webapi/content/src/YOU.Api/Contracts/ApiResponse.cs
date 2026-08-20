namespace YOU.Api.Contracts;

/// <summary>
/// 所有业务接口共用的响应包装结构。
/// </summary>
public sealed class ApiResponse<T>
{
    /// <summary>
    /// 业务操作是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 数值类型的业务结果码。
    /// </summary>
    public int Code { get; init; }

    /// <summary>
    /// 面向调用方的业务结果说明。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 成功时返回的业务数据。
    /// </summary>
    public T? Result { get; init; }

    /// <summary>
    /// 请求校验失败时按字段返回的错误信息。
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    /// 用于日志关联的请求追踪标识。
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    public static ApiResponse<T> Ok(T result, string message, string traceId)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = ResponseCodes.Ok,
            Message = message,
            Result = result,
            TraceId = traceId
        };
    }

    public static ApiResponse<T> Fail(
        int code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors,
        string traceId)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors,
            TraceId = traceId
        };
    }
}
