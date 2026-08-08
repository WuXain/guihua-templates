namespace YOU.Core.Features.RuntimeStatus;

/// <summary>
/// A small, transport-neutral snapshot that can be exposed by an API or a
/// background process without making Core depend on either host.
/// </summary>
public sealed record RuntimeStatus(string State, DateTimeOffset ObservedAtUtc);
