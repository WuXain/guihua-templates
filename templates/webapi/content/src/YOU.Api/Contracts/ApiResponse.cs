namespace YOU.Api.Contracts;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public int Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Result { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

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
