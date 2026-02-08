// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using DotCompute.Abstractions.FaultTolerance;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.FaultTolerance;

/// <summary>
/// Default implementation of the chaos engine for fault injection testing.
/// </summary>
/// <remarks>
/// Provides controlled fault injection for testing fault tolerance and resilience.
/// </remarks>
public sealed partial class ChaosEngine : IChaosEngine
{
    private readonly ILogger<ChaosEngine> _logger;
    private readonly Random _random;
    private readonly List<FaultConfiguration> _faults = [];
    private readonly ConcurrentQueue<FaultInjectionRecord> _history = new();
    private readonly ConcurrentDictionary<FaultType, int> _countByType = new();
    private readonly ConcurrentDictionary<string, int> _countByComponent = new();
    private readonly object _lock = new();
    private DateTimeOffset _sessionStarted;
    private int _totalOpportunities;
    private int _faultsInjected;
    private int _faultsSkipped;
    private bool _isActive;
    private bool _disposed;

    // Event IDs: 9950-9999 for ChaosEngine
    [LoggerMessage(EventId = 9950, Level = LogLevel.Information,
        Message = "Chaos engine started with {FaultCount} fault configurations")]
    private static partial void LogChaosStarted(ILogger logger, int faultCount);

    [LoggerMessage(EventId = 9951, Level = LogLevel.Information,
        Message = "Chaos engine stopped")]
    private static partial void LogChaosStopped(ILogger logger);

    [LoggerMessage(EventId = 9952, Level = LogLevel.Debug,
        Message = "Injecting {FaultType} fault for component {ComponentId}")]
    private static partial void LogFaultInjecting(ILogger logger, FaultType faultType, string componentId);

    [LoggerMessage(EventId = 9953, Level = LogLevel.Warning,
        Message = "Fault injected: {FaultType} for {ComponentId}/{Operation}")]
    private static partial void LogFaultInjected(
        ILogger logger, FaultType faultType, string componentId, string? operation);

    [LoggerMessage(EventId = 9954, Level = LogLevel.Debug,
        Message = "Fault skipped (probability check) for component {ComponentId}")]
    private static partial void LogFaultSkipped(ILogger logger, string componentId);

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <inheritdoc />
    public IReadOnlyList<FaultConfiguration> ActiveFaults
    {
        get
        {
            lock (_lock)
            {
                return _faults.ToList();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<FaultInjectionEventArgs>? FaultInjecting;

    /// <inheritdoc />
    public event EventHandler<FaultInjectionEventArgs>? FaultInjected;

    /// <summary>
    /// Creates a new chaos engine.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="seed">Optional random seed for reproducibility.</param>
    public ChaosEngine(ILogger<ChaosEngine> logger, int? seed = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    /// <inheritdoc />
    public Task StartAsync(
        IEnumerable<FaultConfiguration> faults,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(faults);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _faults.Clear();
            _faults.AddRange(faults.Where(f => f.Enabled));
            _sessionStarted = DateTimeOffset.UtcNow;
            _totalOpportunities = 0;
            _faultsInjected = 0;
            _faultsSkipped = 0;
            _countByType.Clear();
            _countByComponent.Clear();
            _isActive = true;
        }

        LogChaosStarted(_logger, _faults.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _isActive = false;
        }

        LogChaosStopped(_logger);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AddFault(FaultConfiguration fault)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(fault);

        lock (_lock)
        {
            if (fault.Enabled)
            {
                _faults.Add(fault);
            }
        }
    }

    /// <inheritdoc />
    public void RemoveFault(FaultType faultType, string? targetPattern = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _faults.RemoveAll(f =>
                f.FaultType == faultType &&
                (targetPattern == null || f.TargetPattern == targetPattern));
        }
    }

    /// <inheritdoc />
    public void ClearFaults()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _faults.Clear();
        }
    }

    /// <inheritdoc />
    public Task<FaultConfiguration?> EvaluateFaultAsync(
        string componentId,
        string? operation = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(componentId);

        cancellationToken.ThrowIfCancellationRequested();

        if (!_isActive)
        {
            return Task.FromResult<FaultConfiguration?>(null);
        }

        Interlocked.Increment(ref _totalOpportunities);

        List<FaultConfiguration> matchingFaults;
        lock (_lock)
        {
            matchingFaults = _faults
                .Where(f => MatchesTarget(f.TargetPattern, componentId, operation))
                .ToList();
        }

        foreach (var fault in matchingFaults)
        {
            // Check probability - using Random for reproducibility in chaos testing (not security)
#pragma warning disable CA5394 // Random is used for reproducible chaos testing, not security
            if (_random.NextDouble() <= fault.Probability)
#pragma warning restore CA5394
            {
                return Task.FromResult<FaultConfiguration?>(fault);
            }

            LogFaultSkipped(_logger, componentId);
            Interlocked.Increment(ref _faultsSkipped);
        }

        return Task.FromResult<FaultConfiguration?>(null);
    }

    /// <inheritdoc />
    public async Task InjectFaultAsync(
        FaultConfiguration fault,
        string componentId,
        string? operation = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(fault);
        ArgumentNullException.ThrowIfNull(componentId);

        cancellationToken.ThrowIfCancellationRequested();

        // Raise pre-injection event
        var eventArgs = new FaultInjectionEventArgs
        {
            Configuration = fault,
            ComponentId = componentId,
            Operation = operation
        };

        FaultInjecting?.Invoke(this, eventArgs);

        if (eventArgs.Cancel)
        {
            return;
        }

        LogFaultInjecting(_logger, fault.FaultType, componentId);

        TimeSpan? actualDelay = null;
        Exception? exception = null;

        switch (fault.FaultType)
        {
            case FaultType.Latency:
                actualDelay = await InjectLatencyAsync(fault, cancellationToken).ConfigureAwait(false);
                break;

            case FaultType.Exception:
                exception = ThrowException(fault);
                break;

            case FaultType.Timeout:
                throw new TimeoutException(
                    $"Chaos-injected timeout for {componentId}/{operation}");

            case FaultType.ResourceExhaustion:
                throw new InsufficientMemoryException(
                    $"Chaos-injected resource exhaustion for {componentId}/{operation}");

            case FaultType.GpuError:
                throw new InvalidOperationException(
                    $"Chaos-injected GPU error for {componentId}/{operation}");

            case FaultType.DataCorruption:
            case FaultType.PartialFailure:
            case FaultType.NetworkPartition:
            case FaultType.ProcessCrash:
            case FaultType.None:
                // These require specific handling by the caller
                break;
        }

        // Record the injection
        var record = new FaultInjectionRecord
        {
            FaultId = Guid.NewGuid(),
            Configuration = fault,
            TargetComponent = componentId,
            Operation = operation,
            WasApplied = true,
            ActualDelay = actualDelay,
            Exception = exception
        };

        RecordInjection(record, componentId);
        LogFaultInjected(_logger, fault.FaultType, componentId, operation);

        // Raise post-injection event
        FaultInjected?.Invoke(this, new FaultInjectionEventArgs
        {
            Configuration = fault,
            ComponentId = componentId,
            Operation = operation
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<FaultInjectionRecord> GetHistory(int limit = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _history
            .Reverse()
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    public ChaosStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new ChaosStatistics
        {
            TotalOpportunities = _totalOpportunities,
            FaultsInjected = _faultsInjected,
            FaultsSkipped = _faultsSkipped,
            ByFaultType = _countByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ByComponent = _countByComponent.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            SessionStarted = _sessionStarted
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _isActive = false;

        lock (_lock)
        {
            _faults.Clear();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<TimeSpan> InjectLatencyAsync(
        FaultConfiguration fault,
        CancellationToken cancellationToken)
    {
        TimeSpan delay;

        if (fault.Latency.HasValue)
        {
            delay = fault.Latency.Value;
        }
        else if (fault.MinLatency.HasValue && fault.MaxLatency.HasValue)
        {
            var minTicks = fault.MinLatency.Value.Ticks;
            var maxTicks = fault.MaxLatency.Value.Ticks;
#pragma warning disable CA5394 // Random is used for reproducible chaos testing, not security
            var randomTicks = minTicks + (long)(_random.NextDouble() * (maxTicks - minTicks));
#pragma warning restore CA5394
            delay = TimeSpan.FromTicks(randomTicks);
        }
        else
        {
            delay = TimeSpan.FromMilliseconds(100); // Default latency
        }

        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        return delay;
    }

    [UnconditionalSuppressMessage("AOT", "IL2072:Target method argument does not satisfy 'DynamicallyAccessedMembersAttribute'",
        Justification = "Exception types are known at compile time in chaos testing scenarios")]
    private static Exception ThrowException(FaultConfiguration fault)
    {
        if (fault.ExceptionFactory != null)
        {
            throw fault.ExceptionFactory();
        }

        var message = fault.ExceptionMessage ?? "Chaos-injected exception";

        if (fault.ExceptionType != null)
        {
            var exception = (Exception?)Activator.CreateInstance(fault.ExceptionType, message);
            if (exception != null)
            {
                throw exception;
            }
        }

        throw new InvalidOperationException(message);
    }

    private void RecordInjection(FaultInjectionRecord record, string componentId)
    {
        _history.Enqueue(record);

        // Trim history if too large
        while (_history.Count > 1000)
        {
            _history.TryDequeue(out _);
        }

        Interlocked.Increment(ref _faultsInjected);
        _countByType.AddOrUpdate(record.Configuration.FaultType, 1, (_, count) => count + 1);
        _countByComponent.AddOrUpdate(componentId, 1, (_, count) => count + 1);
    }

    private static bool MatchesTarget(string pattern, string componentId, string? operation)
    {
        if (pattern == "*")
        {
            return true;
        }

        var target = operation != null ? $"{componentId}/{operation}" : componentId;

        // Support simple wildcards
        if (pattern.Contains('*', StringComparison.Ordinal))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
            return Regex.IsMatch(target, regexPattern, RegexOptions.IgnoreCase);
        }

        return string.Equals(pattern, componentId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(pattern, target, StringComparison.OrdinalIgnoreCase);
    }
}
