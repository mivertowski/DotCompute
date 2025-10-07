// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution;

/// <summary>
/// Provides execution profiling capabilities to track performance metrics during parallel execution.
/// </summary>
public class ExecutionProfiler : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ExecutionProfilingData> _profilingData;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ExecutionProfiler class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ExecutionProfiler(ILogger logger)
    {
        _logger = logger;
        _profilingData = [];
    }
    /// <summary>
    /// Gets start profiling asynchronously.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="strategy">The strategy.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
#pragma warning disable CA1822 // Mark members as static - uses instance fields _profilingData and _logger
    public async ValueTask StartProfilingAsync(Guid executionId, ExecutionStrategyType strategy, CancellationToken cancellationToken)
#pragma warning restore CA1822
    {
        _profilingData[executionId.ToString()] = new ExecutionProfilingData
        {
            ExecutionId = executionId,
            Strategy = strategy,
            StartTime = DateTimeOffset.UtcNow,
            Events = []
        };

        _logger.LogTrace("Started profiling for execution {ExecutionId} with strategy {Strategy}", executionId, strategy);
        await ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets stop profiling asynchronously.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask<ExecutionProfilingData> StopProfilingAsync(Guid executionId, CancellationToken cancellationToken)
    {
        if (_profilingData.TryGetValue(executionId.ToString(), out var data))
        {
            data.EndTime = DateTimeOffset.UtcNow;
            data.TotalDuration = data.EndTime - data.StartTime;

            _logger.LogTrace("Stopped profiling for execution {ExecutionId}, duration: {Duration:F2}ms",
                executionId, data.TotalDuration.TotalMilliseconds);

            return data;
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return new ExecutionProfilingData
        {
            ExecutionId = executionId,
            Strategy = ExecutionStrategyType.Single,
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow,
            Events = []
        };
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _profilingData.Clear();
        _disposed = true;
        await ValueTask.CompletedTask;
    }
}
