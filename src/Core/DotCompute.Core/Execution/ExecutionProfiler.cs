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
    public ExecutionProfiler(ILogger logger)
    {
        _logger = logger;
        _profilingData = [];
    }

    public async ValueTask StartProfilingAsync(Guid executionId, ExecutionStrategyType strategy, CancellationToken cancellationToken)
    {
        _profilingData[executionId] = new ExecutionProfilingData
        {
            ExecutionId = executionId,
            Strategy = strategy,
            StartTime = DateTimeOffset.UtcNow,
            Events = []
        };

        _logger.LogTrace("Started profiling for execution {ExecutionId} with strategy {Strategy}", executionId, strategy);
        await ValueTask.CompletedTask;
    }

    public async ValueTask<ExecutionProfilingData> StopProfilingAsync(Guid executionId, CancellationToken cancellationToken)
    {
        if (_profilingData.TryGetValue(executionId, out var data))
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
