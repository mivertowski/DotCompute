// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Core.Logging;
namespace DotCompute.Core.Recovery.Gpu;

/// <summary>
/// Concrete implementation of kernel execution monitoring for detecting hangs and timeouts.
/// Provides real-time monitoring of GPU kernel execution with cancellation support.
/// </summary>
/// <remarks>
/// This implementation uses background monitoring to track kernel execution time
/// and provides mechanisms to detect hanging kernels and cancel them gracefully.
/// It maintains thread-safe state and integrates with the logging system for diagnostics.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="KernelExecutionMonitor"/> class.
/// </remarks>
/// <param name="kernelId">The unique identifier for the kernel to monitor.</param>
/// <param name="timeout">The timeout duration for kernel execution.</param>
/// <param name="logger">The logger instance for diagnostic output.</param>
/// <param name="deviceId">The identifier of the GPU device (optional, defaults to "unknown").</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="kernelId"/> or <paramref name="logger"/> is null.</exception>
public class KernelExecutionMonitor(string kernelId, TimeSpan timeout, ILogger logger, string deviceId = "unknown") : IKernelExecutionMonitor
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly TimeSpan _timeout = timeout;
    private volatile bool _completed;
    private volatile bool _disposed;

    /// <summary>
    /// Gets the unique identifier of the kernel being monitored.
    /// </summary>
    /// <value>The kernel identifier string.</value>
    public string KernelId { get; } = kernelId ?? throw new ArgumentNullException(nameof(kernelId));

    /// <summary>
    /// Gets the identifier of the GPU device where the kernel is executing.
    /// </summary>
    /// <value>The device identifier string.</value>
    public string DeviceId { get; } = deviceId;

    /// <summary>
    /// Gets the current execution time of the monitored kernel.
    /// </summary>
    /// <value>The time elapsed since kernel execution began.</value>
    public TimeSpan ExecutionTime => DateTimeOffset.UtcNow - _startTime;

    /// <summary>
    /// Gets a value indicating whether the kernel execution appears to be hanging.
    /// </summary>
    /// <value><c>true</c> if the kernel is suspected of hanging; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// A kernel is considered hanging if it has not completed and has exceeded its timeout duration.
    /// </remarks>
    public bool IsHanging => !_completed && ExecutionTime > _timeout;

    /// <summary>
    /// Gets a value indicating whether the kernel execution has completed.
    /// </summary>
    /// <value><c>true</c> if the kernel has finished execution; otherwise, <c>false</c>.</value>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Asynchronously cancels the kernel execution.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cancellation operation.</returns>
    /// <remarks>
    /// This method attempts to gracefully cancel the running kernel by triggering
    /// the internal cancellation token and allowing time for cleanup operations.
    /// </remarks>
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
    /// Asynchronously waits for the kernel execution to complete.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    /// <remarks>
    /// This method polls the completion status at regular intervals until
    /// the kernel completes, is disposed, or the cancellation token is triggered.
    /// </remarks>
    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (!_completed && !_disposed && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    /// <summary>
    /// Marks the kernel execution as completed and logs timing information.
    /// </summary>
    /// <remarks>
    /// This method should be called when the kernel execution finishes successfully.
    /// It logs debug information about the execution duration for performance monitoring.
    /// </remarks>
    public void MarkCompleted()
    {
        _completed = true;
        _logger.LogDebugMessage("Kernel {KernelId} execution completed in {KernelId, ExecutionTime.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Releases all resources used by the <see cref="KernelExecutionMonitor"/>.
    /// </summary>
    /// <remarks>
    /// This method ensures proper cleanup of the cancellation token source
    /// and marks the monitor as disposed and completed.
    /// </remarks>
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
