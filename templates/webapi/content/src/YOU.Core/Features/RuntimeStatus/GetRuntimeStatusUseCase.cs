namespace YOU.Core.Features.RuntimeStatus;

/// <summary>
/// Reads runtime state through framework-neutral platform abstractions.
/// TimeProvider is replaceable by simulations and deterministic tests.
/// </summary>
public sealed class GetRuntimeStatusUseCase
{
    private readonly TimeProvider _timeProvider;

    public GetRuntimeStatusUseCase(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public ValueTask<RuntimeStatus> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = new RuntimeStatus(
            State: "Ready",
            ObservedAtUtc: _timeProvider.GetUtcNow());

        return ValueTask.FromResult(status);
    }
}
