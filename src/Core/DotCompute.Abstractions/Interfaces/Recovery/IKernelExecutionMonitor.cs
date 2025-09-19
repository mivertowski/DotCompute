// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Recovery;

/// <summary>
/// Defines the contract for monitoring GPU kernel execution to detect hangs and timeouts.
/// Provides methods to track, cancel, and wait for kernel completion.
/// </summary>
/// <remarks>
/// This interface is essential for implementing robust GPU kernel execution monitoring.
/// It allows the recovery system to detect when kernels are hanging or taking too long
/// to execute, enabling timely intervention to prevent system instability.
/// </remarks>
public interface IKernelExecutionMonitor : IDisposable
{
    /// <summary>
    /// Gets the unique identifier of the kernel being monitored.
    /// </summary>
    /// <value>A string identifier for the kernel, typically its name or generated ID.</value>
    public string KernelId { get; }

    /// <summary>
    /// Gets the identifier of the GPU device where the kernel is executing.
    /// </summary>
    /// <value>The device identifier string.</value>
    public string DeviceId { get; }

    /// <summary>
    /// Gets the current execution time of the monitored kernel.
    /// </summary>
    /// <value>The time elapsed since kernel execution began.</value>
    public TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Gets a value indicating whether the kernel execution appears to be hanging.
    /// </summary>
    /// <value><c>true</c> if the kernel is suspected of hanging (exceeded timeout and not completed); otherwise, <c>false</c>.</value>
    /// <remarks>
    /// A kernel is considered hanging if it has exceeded its timeout duration
    /// and has not been marked as completed.
    /// </remarks>
    public bool IsHanging { get; }

    /// <summary>
    /// Gets a value indicating whether the kernel execution has completed.
    /// </summary>
    /// <value><c>true</c> if the kernel has finished execution; otherwise, <c>false</c>.</value>
    public bool IsCompleted { get; }

    /// <summary>
    /// Asynchronously cancels the kernel execution.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cancellation operation.</returns>
    /// <remarks>
    /// This method attempts to gracefully cancel the running kernel.
    /// The actual cancellation mechanism depends on the underlying GPU platform.
    /// </remarks>
    public Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously waits for the kernel execution to complete.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    /// <remarks>
    /// This method will block until the kernel completes, is cancelled, or the
    /// cancellation token is triggered.
    /// </remarks>
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the kernel execution as completed.
    /// </summary>
    /// <remarks>
    /// This method should be called when the kernel execution finishes successfully.
    /// It updates the internal state to reflect completion and may log timing information.
    /// </remarks>
    public void MarkCompleted();
}