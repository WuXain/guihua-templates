using Microsoft.Extensions.DependencyInjection;
using YOU.Core;
using YOU.Core.Features.RuntimeStatus;
using YOU.Infrastructure;
using Xunit;

namespace YOU.ArchitectureTests;

public sealed class ArchitectureBaselineTests
{
    [Fact]
    public void Core_has_no_forbidden_dependencies()
    {
        var forbiddenPrefixes = new[]
        {
            "YOU.Infrastructure",
            "Microsoft.AspNetCore",
            "FreeSql",
            "StackExchange.Redis",
            "System.Net.WebSockets"
        };

        var references = typeof(CoreMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            forbiddenPrefixes.Any(prefix =>
                reference.Equals(prefix, StringComparison.Ordinal) ||
                reference.StartsWith(prefix + ".", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Core_use_case_uses_replaceable_time_and_honors_cancellation()
    {
        var expectedTime = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var useCase = new GetRuntimeStatusUseCase(new FixedTimeProvider(expectedTime));

        var result = await useCase.ExecuteAsync();

        Assert.Equal("Ready", result.State);
        Assert.Equal(expectedTime, result.ObservedAtUtc);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(cancellation.Token).AsTask());
    }

    [Fact]
    public async Task Infrastructure_composition_is_explicit_and_does_not_connect_externally()
    {
        await using var provider = new ServiceCollection()
            .AddInfrastructure()
            .BuildServiceProvider();

        var useCase = provider.GetRequiredService<GetRuntimeStatusUseCase>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, timeProvider);
        Assert.Equal("Ready", (await useCase.ExecuteAsync()).State);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
