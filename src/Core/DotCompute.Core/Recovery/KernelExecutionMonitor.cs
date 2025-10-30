// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Implementation of kernel execution monitoring
/// </summary>
public sealed class KernelExecutionMonitor(string kernelId, TimeSpan timeout, ILogger logger, string deviceId = "unknown") : IKernelExecutionMonitor
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly TimeSpan _timeout = timeout;
    private volatile bool _completed;
    private volatile bool _disposed;
    /// <summary>
    /// Gets or sets the kernel identifier.
    /// </summary>
    /// <value>The kernel id.</value>

    public string KernelId { get; } = kernelId ?? throw new ArgumentNullException(nameof(kernelId));
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; } = deviceId;
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime => DateTimeOffset.UtcNow - _startTime;
    /// <summary>
    /// Gets or sets a value indicating whether hanging.
    /// </summary>
    /// <value>The is hanging.</value>
    public bool IsHanging => !_completed && ExecutionTime > _timeout;
    /// <summary>
    /// Gets or sets a value indicating whether completed.
    /// </summary>
    /// <value>The is completed.</value>
    public bool IsCompleted => _completed;
    /// <summary>
    /// Determines whether cel async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

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
    /// <summary>
    /// Gets wait for completion asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (!_completed && !_disposed && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }
    /// <summary>
    /// Performs mark completed.
    /// </summary>

    public void MarkCompleted()
    {
        _completed = true;
        _logger.LogDebugMessage("Kernel {KernelId} execution completed in {KernelId, ExecutionTime.TotalMilliseconds}ms");
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
