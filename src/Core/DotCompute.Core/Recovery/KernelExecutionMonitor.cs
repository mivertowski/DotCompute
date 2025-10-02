// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Implementation of kernel execution monitoring
/// </summary>
public class KernelExecutionMonitor(string kernelId, TimeSpan timeout, ILogger logger, string deviceId = "unknown") : IKernelExecutionMonitor
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly TimeSpan _timeout = timeout;
    private volatile bool _completed;
    private volatile bool _disposed;

    public string KernelId { get; } = kernelId ?? throw new ArgumentNullException(nameof(kernelId));
    public string DeviceId { get; } = deviceId;
    public TimeSpan ExecutionTime => DateTimeOffset.UtcNow - _startTime;
    public bool IsHanging => !_completed && ExecutionTime > _timeout;
    public bool IsCompleted => _completed;

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _completed)
        {
            return;
        }

        _logger.LogInfoMessage("Cancelling kernel execution {KernelId}");

        _cancellationTokenSource.Cancel();
        _completed = true;

        await Task.Delay(100, cancellationToken); // Allow cleanup time
    }

    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (!_completed && !_disposed && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    public void MarkCompleted()
    {
        _completed = true;
        _logger.LogDebugMessage("Kernel {KernelId} execution completed in {KernelId, ExecutionTime.TotalMilliseconds}ms");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Dispose();
            _completed = true;
            _disposed = true;
        }
    }
}
