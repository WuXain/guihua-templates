namespace YOU.Api.Models;

/// <summary>
/// 运行状态接口的对外响应模型。
/// </summary>
public sealed class RuntimeStatusResponse
{
    /// <summary>
    /// 当前运行状态。
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// 状态采集时的 UTC 时间。
    /// </summary>
    public DateTimeOffset ObservedAtUtc { get; init; }
}
