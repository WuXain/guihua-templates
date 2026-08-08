using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guihua.HostedService.Tests;

public sealed class HostedServiceModuleTests
{
    [Fact]
    public async Task StartAsync_validates_options_before_running_adapter()
    {
        using var host = CreateHost<TestAdapter>(
            new Dictionary<string, string?>
            {
                [$"{HostedServiceOptions.SectionName}:IntervalMilliseconds"] = "0"
            });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Null(host.Services.GetRequiredService<TestAdapter>().StartedToken);
    }

    [Fact]
    public async Task Worker_runs_periodically_with_registered_time_provider()
    {
        using var host = CreateHost<TestAdapter>(
            new Dictionary<string, string?>
            {
                [$"{HostedServiceOptions.SectionName}:IntervalMilliseconds"] = "10"
            });

        await host.StartAsync();
        var adapter = host.Services.GetRequiredService<TestAdapter>();
        var timeProvider = host.Services.GetRequiredService<TrackingTimeProvider>();

        await WaitUntilAsync(() => Volatile.Read(ref adapter.CallCount) >= 2);

        Assert.True(timeProvider.TimerCreated);
        await host.StopAsync();
    }

    [Fact]
    public async Task StopAsync_cancels_adapter_and_finishes_normally()
    {
        using var host = CreateHost<TestAdapter>();
        await host.StartAsync();

        var adapter = host.Services.GetRequiredService<TestAdapter>();
        await adapter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await host.StopAsync(TimeSpan.FromSeconds(2));

        Assert.True(adapter.StartedToken!.Value.IsCancellationRequested);
        Assert.Contains("Hosted service stopped", host.Services.GetRequiredService<TestLoggerProvider>().Messages);
    }

    [Fact]
    public async Task Continue_policy_logs_exception_and_runs_next_cycle()
    {
        using var host = CreateHost<ThrowOnceAdapter>(
            new Dictionary<string, string?>
            {
                [$"{HostedServiceOptions.SectionName}:IntervalMilliseconds"] = "1",
                [$"{HostedServiceOptions.SectionName}:FailurePolicy"] = nameof(HostedServiceFailurePolicy.Continue)
            });

        await host.StartAsync();
        var adapter = host.Services.GetRequiredService<ThrowOnceAdapter>();

        await WaitUntilAsync(() => Volatile.Read(ref adapter.CallCount) >= 2);

        Assert.Contains(
            "Hosted service cycle failed; continuing with the next cycle",
            host.Services.GetRequiredService<TestLoggerProvider>().Messages);
        await host.StopAsync();
    }

    [Fact]
    public async Task Stop_policy_logs_exception_and_exits_loop()
    {
        using var host = CreateHost<AlwaysFailingAdapter>(
            new Dictionary<string, string?>
            {
                [$"{HostedServiceOptions.SectionName}:IntervalMilliseconds"] = "1",
                [$"{HostedServiceOptions.SectionName}:FailurePolicy"] = nameof(HostedServiceFailurePolicy.Stop)
            });

        await host.StartAsync();
        var adapter = host.Services.GetRequiredService<AlwaysFailingAdapter>();

        await adapter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(1, Volatile.Read(ref adapter.CallCount));
        Assert.Contains(
            "Hosted service cycle failed; stopping the hosted service loop",
            host.Services.GetRequiredService<TestLoggerProvider>().Messages);
        await host.StopAsync();
    }

    [Fact]
    public async Task Cancellation_during_cycle_is_not_logged_as_an_error()
    {
        using var host = CreateHost<BlockingAdapter>();
        await host.StartAsync();

        var adapter = host.Services.GetRequiredService<BlockingAdapter>();
        await adapter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await host.StopAsync(TimeSpan.FromSeconds(2));

        Assert.DoesNotContain(
            host.Services.GetRequiredService<TestLoggerProvider>().Messages,
            message => message.Contains("cycle failed", StringComparison.Ordinal));
    }

    private static IHost CreateHost<TAdapter>(
        Action<IServiceCollection>? configureServices = null)
        where TAdapter : class, IHostedServiceAdapter
    {
        return CreateHost<TAdapter>(new Dictionary<string, string?>(), configureServices);
    }

    private static IHost CreateHost<TAdapter>(
        IDictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null)
        where TAdapter : class, IHostedServiceAdapter
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var loggerProvider = new TestLoggerProvider();

        return new HostBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(loggerProvider);
            })
            .ConfigureServices(services =>
            {
                var timeProvider = new TrackingTimeProvider();
                services.AddSingleton(loggerProvider);
                services.AddSingleton(timeProvider);
                services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<TrackingTimeProvider>());
                services.AddGuihuaHostedService<TAdapter>(configuration);
                configureServices?.Invoke(services);
            })
            .Build();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected hosted-service condition was not reached.");
    }

    private sealed class TrackingTimeProvider : TimeProvider
    {
        private readonly TimeProvider _system = TimeProvider.System;

        public bool TimerCreated { get; private set; }

        public override DateTimeOffset GetUtcNow() => _system.GetUtcNow();

        public override long GetTimestamp() => _system.GetTimestamp();

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            TimerCreated = true;
            return _system.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed class TestAdapter : IHostedServiceAdapter
    {
        public TaskCompletionSource<CancellationToken> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken? StartedToken { get; private set; }

        public int CallCount;

        public ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            StartedToken = cancellationToken;
            Started.TrySetResult(cancellationToken);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowOnceAdapter : IHostedServiceAdapter
    {
        public int CallCount;

        public ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref CallCount);
            if (call == 1)
            {
                throw new InvalidOperationException("test cycle failure");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class AlwaysFailingAdapter : IHostedServiceAdapter
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount;

        public ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            Started.TrySetResult(true);
            throw new InvalidOperationException("test cycle failure");
        }
    }

    private sealed class BlockingAdapter : IHostedServiceAdapter
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new TestLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class TestLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
