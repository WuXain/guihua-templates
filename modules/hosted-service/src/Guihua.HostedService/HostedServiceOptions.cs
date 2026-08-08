using System.ComponentModel.DataAnnotations;

namespace Guihua.HostedService;

public enum HostedServiceFailurePolicy
{
    Continue = 0,
    Stop = 1
}

public sealed class HostedServiceOptions
{
    public const string SectionName = "GuihuaHostedService";

    [Range(1, 86_400_000)]
    public int IntervalMilliseconds { get; init; } = 1_000;

    public HostedServiceFailurePolicy FailurePolicy { get; init; } = HostedServiceFailurePolicy.Continue;

    public TimeSpan Interval => TimeSpan.FromMilliseconds(IntervalMilliseconds);
}
