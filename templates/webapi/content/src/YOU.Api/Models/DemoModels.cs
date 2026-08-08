using System.ComponentModel.DataAnnotations;

namespace YOU.Api.Models;

public sealed record DemoGreeting(string Message);

public sealed class DemoEchoRequest
{
    [Required(ErrorMessage = "Message 不能为空。")]
    [StringLength(100, ErrorMessage = "Message 长度不能超过 100 个字符。")]
    public string? Message { get; init; }
}

public sealed record DemoEcho(string Message);
