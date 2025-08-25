// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Monitors kernel execution for hangs and timeouts
/// </summary>
public interface IKernelExecutionMonitor : IDisposable
{
    public string KernelId { get; }
    public string DeviceId { get; }
    public TimeSpan ExecutionTime { get; }
    public bool IsHanging { get; }
    public bool IsCompleted { get; }

    public Task CancelAsync(CancellationToken cancellationToken = default);
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
    public void MarkCompleted();
}