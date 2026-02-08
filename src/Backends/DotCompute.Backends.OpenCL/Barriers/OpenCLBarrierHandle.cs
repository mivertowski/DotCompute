// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;

namespace DotCompute.Backends.OpenCL.Barriers;

/// <summary>
/// OpenCL implementation of a barrier handle for work-group synchronization.
/// </summary>
/// <remarks>
/// <para>
/// OpenCL barriers provide work-group level synchronization, equivalent to CUDA
/// thread-block barriers. The primary synchronization mechanism is:
/// </para>
/// <list type="bullet">
/// <item><description><c>barrier(CLK_LOCAL_MEM_FENCE)</c> - local memory fence</description></item>
/// <item><description><c>barrier(CLK_GLOBAL_MEM_FENCE)</c> - global memory fence</description></item>
/// <item><description><c>barrier(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)</c> - both</description></item>
/// </list>
/// <para>
/// <strong>OpenCL Specifics:</strong>
/// <list type="bullet">
/// <item><description>Work-group barriers synchronize all work-items in a work-group</description></item>
/// <item><description>No native support for NDRange-wide (grid) barriers</description></item>
/// <item><description>Sub-group operations available in OpenCL 2.0+ (vendor-dependent)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class OpenCLBarrierHandle : IBarrierHandle
{
    private readonly OpenCLBarrierProvider _provider;
    private volatile int _threadsWaiting;
    private bool _disposed;

    /// <summary>
    /// Initializes a new OpenCL barrier handle.
    /// </summary>
    /// <param name="barrierId">The unique barrier identifier.</param>
    /// <param name="scope">The synchronization scope.</param>
    /// <param name="capacity">The maximum thread capacity.</param>
    /// <param name="fenceType">The OpenCL memory fence type.</param>
    /// <param name="name">Optional barrier name.</param>
    /// <param name="provider">The parent barrier provider.</param>
    internal OpenCLBarrierHandle(
        int barrierId,
        BarrierScope scope,
        int capacity,
        OpenCLMemoryFence fenceType,
        string? name,
        OpenCLBarrierProvider provider)
    {
        BarrierId = barrierId;
        Scope = scope;
        Capacity = capacity;
        FenceType = fenceType;
        Name = name;
        _provider = provider;
    }

    /// <inheritdoc />
    public int BarrierId { get; }

    /// <inheritdoc />
    public BarrierScope Scope { get; }

    /// <inheritdoc />
    public int Capacity { get; }

    /// <inheritdoc />
    public int ThreadsWaiting => _threadsWaiting;

    /// <inheritdoc />
    public bool IsActive => _threadsWaiting > 0 && _threadsWaiting < Capacity;

    /// <summary>
    /// Gets the OpenCL memory fence type for this barrier.
    /// </summary>
    public OpenCLMemoryFence FenceType { get; }

    /// <summary>
    /// Gets the barrier name if specified during creation.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets whether this barrier has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <inheritdoc />
    public void Sync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // In OpenCL, barrier synchronization happens within kernel code.
        // This method provides a host-side interface for tracking and validation.
        // The actual barrier() call is generated in the kernel source.

        // Increment waiting count (for monitoring)
        Interlocked.Increment(ref _threadsWaiting);

        // In real execution, threads would block here until capacity is reached.
        // This is handled by the OpenCL runtime within the kernel.

        // For host-side tracking, we simulate barrier passage
        if (_threadsWaiting >= Capacity)
        {
            Interlocked.Exchange(ref _threadsWaiting, 0);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsActive)
        {
            throw new InvalidOperationException(
                $"Cannot reset barrier {BarrierId}: {_threadsWaiting} threads are waiting. " +
                "Wait for all threads to pass or dispose and recreate the barrier.");
        }

        Interlocked.Exchange(ref _threadsWaiting, 0);
    }

    /// <summary>
    /// Gets the OpenCL barrier call string for kernel code generation.
    /// </summary>
    public string KernelBarrierCall => FenceType switch
    {
        OpenCLMemoryFence.LocalMemory => "barrier(CLK_LOCAL_MEM_FENCE)",
        OpenCLMemoryFence.GlobalMemory => "barrier(CLK_GLOBAL_MEM_FENCE)",
        OpenCLMemoryFence.Both => "barrier(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)",
        _ => "barrier(CLK_LOCAL_MEM_FENCE)"
    };

    /// <summary>
    /// Gets the OpenCL memory fence call string for kernel code generation.
    /// </summary>
    public string KernelMemFenceCall => FenceType switch
    {
        OpenCLMemoryFence.LocalMemory => "mem_fence(CLK_LOCAL_MEM_FENCE)",
        OpenCLMemoryFence.GlobalMemory => "mem_fence(CLK_GLOBAL_MEM_FENCE)",
        OpenCLMemoryFence.Both => "mem_fence(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)",
        _ => "mem_fence(CLK_LOCAL_MEM_FENCE)"
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _provider.RemoveBarrier(BarrierId, Name);
    }
}

/// <summary>
/// OpenCL memory fence types for barrier synchronization.
/// </summary>
public enum OpenCLMemoryFence
{
    /// <summary>
    /// Local memory (work-group shared memory) fence.
    /// Maps to CLK_LOCAL_MEM_FENCE.
    /// </summary>
    LocalMemory = 0,

    /// <summary>
    /// Global memory fence.
    /// Maps to CLK_GLOBAL_MEM_FENCE.
    /// </summary>
    GlobalMemory = 1,

    /// <summary>
    /// Both local and global memory fence.
    /// Maps to CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE.
    /// </summary>
    Both = 2
}
