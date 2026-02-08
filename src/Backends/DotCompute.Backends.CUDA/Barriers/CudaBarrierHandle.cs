// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Barriers;

/// <summary>
/// CUDA implementation of barrier synchronization handle with hardware acceleration.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides direct access to CUDA barrier primitives:
/// <list type="bullet">
/// <item><description>Thread-block barriers: __syncthreads() for block-level sync</description></item>
/// <item><description>Grid barriers: grid.sync() via Cooperative Groups (CC 6.0+)</description></item>
/// <item><description>Warp barriers: __syncwarp() for 32-thread SIMD groups (CC 7.0+)</description></item>
/// <item><description>Named barriers: __barrier_sync(id) for multiple barriers per block (CC 7.0+)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Characteristics:</strong>
/// <list type="bullet">
/// <item><description>Thread-block: ~10ns hardware latency</description></item>
/// <item><description>Grid-wide: ~1-10μs depending on grid size</description></item>
/// <item><description>Warp: ~1ns (lockstep SIMD execution)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe for multi-threaded CPU access.
/// GPU threads must follow barrier semantics (all threads in scope must call Sync()).
/// </para>
/// </remarks>
public sealed class CudaBarrierHandle : IBarrierHandle
{
    private readonly CudaContext _context;
    private readonly CudaBarrierProvider? _provider;
    private readonly BarrierScope _scope;
    private readonly int _capacity;
    private readonly string? _name;
    private bool _disposed;
    private int _threadsWaiting;

    // For grid barriers, we need device memory to track state
    private IntPtr _deviceBarrierPtr;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new CUDA barrier handle.
    /// </summary>
    /// <param name="context">The CUDA context owning this barrier.</param>
    /// <param name="provider">The barrier provider for tracking (optional).</param>
    /// <param name="barrierId">Unique barrier identifier.</param>
    /// <param name="scope">The synchronization scope.</param>
    /// <param name="capacity">Maximum threads that will synchronize.</param>
    /// <param name="name">Optional barrier name for debugging.</param>
    internal CudaBarrierHandle(
        CudaContext context,
        CudaBarrierProvider? provider,
        int barrierId,
        BarrierScope scope,
        int capacity,
        string? name = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _provider = provider;
        BarrierId = barrierId;
        _scope = scope;
        _capacity = capacity;
        _name = name;
        _threadsWaiting = 0;

        // For grid barriers, allocate device memory for synchronization state
        if (scope == BarrierScope.Grid)
        {
            var error = CudaRuntime.cudaMalloc(ref _deviceBarrierPtr, (UIntPtr)sizeof(int));
            if (error != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to allocate device memory for grid barrier: {CudaRuntime.GetErrorString(error)}");
            }

            // Initialize to 0
            error = CudaRuntime.cudaMemset(_deviceBarrierPtr, 0, (UIntPtr)sizeof(int));
            if (error != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBarrierPtr);
                throw new InvalidOperationException(
                    $"Failed to initialize grid barrier state: {CudaRuntime.GetErrorString(error)}");
            }
        }
    }

    /// <inheritdoc />
    public int BarrierId { get; }

    /// <inheritdoc />
    public BarrierScope Scope => _scope;

    /// <inheritdoc />
    public int Capacity => _capacity;

    /// <inheritdoc />
    public int ThreadsWaiting
    {
        get
        {
            lock (_lock)
            {
                return _threadsWaiting;
            }
        }
    }

    /// <inheritdoc />
    public bool IsActive => ThreadsWaiting > 0 && ThreadsWaiting < Capacity;

    /// <summary>
    /// Gets the device pointer to barrier state (for grid barriers).
    /// </summary>
    internal IntPtr DeviceBarrierPtr => _deviceBarrierPtr;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>IMPORTANT:</strong> This method is called from GPU kernel code.
    /// The actual synchronization happens via CUDA barrier primitives:
    /// </para>
    /// <para>
    /// <strong>Thread-Block:</strong> Uses __syncthreads() hardware barrier.
    /// All threads in block wait until all threads reach this point.
    /// </para>
    /// <para>
    /// <strong>Grid:</strong> Uses cooperative_groups::this_grid().sync().
    /// Requires kernel launched with cudaLaunchCooperativeKernel.
    /// </para>
    /// <para>
    /// <strong>Warp:</strong> Uses __syncwarp() for 32-thread SIMD groups.
    /// </para>
    /// <para>
    /// <strong>Named:</strong> Uses __barrier_sync(id) for multiple barriers.
    /// </para>
    /// </remarks>
    public void Sync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Increment waiting counter (CPU-side tracking only)
        lock (_lock)
        {
            _threadsWaiting++;

            // When all threads have synced, reset for next cycle
            if (_threadsWaiting >= _capacity)
            {
                _threadsWaiting = 0;
            }
        }

        // Note: Actual GPU synchronization happens in kernel code via:
        // - __syncthreads() for ThreadBlock
        // - grid.sync() for Grid
        // - __syncwarp() for Warp
        // This CPU-side method is for tracking and validation only
    }

    /// <inheritdoc />
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_threadsWaiting > 0)
            {
                throw new InvalidOperationException(
                    "Cannot reset an active barrier. Wait for all threads to complete sync.");
            }

            _threadsWaiting = 0;

            // Reset device memory for grid barriers
            if (_scope == BarrierScope.Grid && _deviceBarrierPtr != IntPtr.Zero)
            {
                var error = CudaRuntime.cudaMemset(_deviceBarrierPtr, 0, (UIntPtr)sizeof(int));
                if (error != CudaError.Success)
                {
                    throw new InvalidOperationException(
                        $"Failed to reset grid barrier state: {CudaRuntime.GetErrorString(error)}");
                }
            }
        }
    }

    /// <summary>
    /// Disposes the barrier handle and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // Free device memory for grid barriers
            if (_deviceBarrierPtr != IntPtr.Zero)
            {
                CudaRuntime.cudaFree(_deviceBarrierPtr);
                _deviceBarrierPtr = IntPtr.Zero;
            }

            // Notify provider to remove from tracking
            _provider?.RemoveBarrier(BarrierId, _name);

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure device memory is freed.
    /// </summary>
    ~CudaBarrierHandle()
    {
        Dispose();
    }

    /// <summary>
    /// Returns a string representation of this barrier handle.
    /// </summary>
    public override string ToString()
    {
        var nameStr = string.IsNullOrEmpty(_name) ? "" : $" '{_name}'";
        return $"CudaBarrier{nameStr}[ID={BarrierId}, Scope={Scope}, " +
               $"Capacity={Capacity}, Waiting={ThreadsWaiting}]";
    }
}
