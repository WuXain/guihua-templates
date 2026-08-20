using System.ComponentModel.DataAnnotations;

namespace YOU.Api.Models;

/// <summary>
/// 演示问候接口的返回内容。
/// </summary>
public sealed class DemoGreeting
{
    /// <summary>
    /// 服务端生成的问候文本。
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// 回显接口的请求参数。
/// </summary>
public sealed class DemoEchoRequest
{
    /// <summary>
    /// 要由服务端原样返回的文本，最长 100 个字符。
    /// </summary>
    [Required(ErrorMessage = "Message 不能为空。")]
    [StringLength(100, ErrorMessage = "Message 长度不能超过 100 个字符。")]
    public string? Message { get; init; }
}

/// <summary>
/// 回显接口的返回内容。
/// </summary>
public sealed class DemoEcho
{
    /// <summary>
    /// 服务端原样返回的文本。
    /// </summary>
    public required string Message { get; init; }
}
