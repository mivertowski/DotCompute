// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Implementation of kernel execution monitoring
/// </summary>
public class KernelExecutionMonitor : IKernelExecutionMonitor
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly DateTimeOffset _startTime;
    private readonly TimeSpan _timeout;
    private volatile bool _completed;
    private volatile bool _disposed;

    public string KernelId { get; }
    public string DeviceId { get; }
    public TimeSpan ExecutionTime => DateTimeOffset.UtcNow - _startTime;
    public bool IsHanging => !_completed && ExecutionTime > _timeout;
    public bool IsCompleted => _completed;

    public KernelExecutionMonitor(string kernelId, TimeSpan timeout, ILogger logger, string deviceId = "unknown")
    {
        KernelId = kernelId ?? throw new ArgumentNullException(nameof(kernelId));
        DeviceId = deviceId;
        _timeout = timeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
        _startTime = DateTimeOffset.UtcNow;
    }

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
