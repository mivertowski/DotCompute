// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Barriers;

/// <summary>
/// Provides GPU-native barrier synchronization primitives for coordinating thread execution.
/// </summary>
/// <remarks>
/// <para>
/// The barrier provider enables creation and management of hardware-accelerated synchronization
/// barriers for GPU kernels. Barriers are essential for algorithms requiring phased computation
/// where threads must coordinate their execution and ensure memory consistency.
/// </para>
/// <para>
/// <strong>Platform Support:</strong>
/// <list type="bullet">
/// <item><description>CUDA (CC 1.0+): Thread-block barriers via __syncthreads()</description></item>
/// <item><description>CUDA (CC 6.0+): Grid-wide barriers via Cooperative Groups</description></item>
/// <item><description>CUDA (CC 7.0+): Named barriers (up to 16 per block)</description></item>
/// <item><description>CUDA (CC 9.0+): Async barriers with transactions</description></item>
/// <item><description>OpenCL 2.0+: Work-group barriers</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Barrier Types:</strong>
/// <list type="table">
/// <item>
/// <term>ThreadBlock</term>
/// <description>Fastest (~10ns), limited to single block, most common</description>
/// </item>
/// <item>
/// <term>Grid</term>
/// <description>Slower (~1-10μs), spans all blocks, requires cooperative launch</description></item>
/// <item>
/// <term>Warp</term>
/// <description>Ultra-fast (~1ns), 32-thread SIMD groups</description>
/// </item>
/// <item>
/// <term>Tile</term>
/// <description>Flexible (~20ns), arbitrary thread subsets</description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// var provider = accelerator.GetBarrierProvider();
/// if (provider != null)
/// {
///     // Create thread-block barrier for 256 threads
///     using var barrier = provider.CreateBarrier(BarrierScope.ThreadBlock, capacity: 256);
///
///     // In kernel: barrier.Sync(); // All threads synchronize here
/// }
/// </code>
/// </para>
/// </remarks>
public interface IBarrierProvider
{
    /// <summary>
    /// Creates a new barrier with the specified scope and capacity.
    /// </summary>
    /// <param name="scope">The synchronization scope determining which threads participate.</param>
    /// <param name="capacity">
    /// Maximum number of threads that will synchronize on this barrier.
    /// Must match the actual number of threads calling sync.
    /// </param>
    /// <param name="name">
    /// Optional barrier name for debugging and named barrier support (CUDA 7.0+).
    /// If specified, enables multiple distinct barriers within same kernel.
    /// </param>
    /// <returns>
    /// A new barrier handle configured for the specified scope and capacity.
    /// The caller is responsible for disposing the handle when done.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Capacity Requirements:</strong>
    /// <list type="bullet">
    /// <item><description>ThreadBlock: capacity ≤ block size (check device limits)</description></item>
    /// <item><description>Grid: capacity = grid size (all threads in kernel)</description></item>
    /// <item><description>Warp: capacity must be 32 (fixed warp size)</description></item>
    /// <item><description>Tile: capacity ≤ block size, any value</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Named Barriers (CUDA 7.0+):</strong>
    /// Up to 16 named barriers can exist per thread block. Names enable multiple
    /// synchronization points in complex kernels without barrier ID conflicts.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong>
    /// <list type="bullet">
    /// <item><description>Barrier creation: ~1μs overhead (once per kernel)</description></item>
    /// <item><description>Barrier sync: 1ns to 10μs depending on scope</description></item>
    /// <item><description>Named barriers: No performance penalty vs anonymous</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>capacity ≤ 0</description></item>
    /// <item><description>capacity exceeds device limits for the specified scope</description></item>
    /// <item><description>Warp scope with capacity != 32</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>Grid scope on device with CC &lt; 6.0</description></item>
    /// <item><description>Named barriers on device with CC &lt; 7.0</description></item>
    /// <item><description>Maximum barrier count exceeded (16 per block)</description></item>
    /// </list>
    /// </exception>
    /// <example>
    /// <code>
    /// // Simple thread-block barrier
    /// using var blockBarrier = provider.CreateBarrier(BarrierScope.ThreadBlock, capacity: 512);
    ///
    /// // Named barriers for multi-phase algorithms
    /// using var phase1 = provider.CreateBarrier(BarrierScope.ThreadBlock, capacity: 512, name: "phase1");
    /// using var phase2 = provider.CreateBarrier(BarrierScope.ThreadBlock, capacity: 512, name: "phase2");
    ///
    /// // Grid-wide barrier (requires cooperative launch)
    /// using var gridBarrier = provider.CreateBarrier(BarrierScope.Grid, capacity: gridSize * blockSize);
    /// </code>
    /// </example>
    public IBarrierHandle CreateBarrier(BarrierScope scope, int capacity, string? name = null);

    /// <summary>
    /// Gets an existing barrier by name, or null if not found.
    /// </summary>
    /// <param name="name">The barrier name specified during creation.</param>
    /// <returns>
    /// The barrier handle if found, otherwise null.
    /// Handle lifetime is managed by the creator; this method provides read-only access.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method enables barrier sharing across different parts of kernel code.
    /// Useful for complex algorithms where multiple functions need to synchronize
    /// on the same barrier.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Multiple threads can safely call this method
    /// concurrently. The returned handle is thread-safe for sync operations.
    /// </para>
    /// </remarks>
    public IBarrierHandle? GetBarrier(string name);

    /// <summary>
    /// Enables cooperative kernel launch mode for grid-wide barriers.
    /// </summary>
    /// <param name="enable">True to enable cooperative launch, false to disable.</param>
    /// <remarks>
    /// <para>
    /// Cooperative launch is required for <see cref="BarrierScope.Grid"/> barriers.
    /// When enabled, kernels are launched using <c>cudaLaunchCooperativeKernel</c>,
    /// guaranteeing all threads execute concurrently.
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong>
    /// <list type="bullet">
    /// <item><description>Compute Capability 6.0+ (Pascal or newer)</description></item>
    /// <item><description>Grid size ≤ maximum concurrent kernel limit (check device props)</description></item>
    /// <item><description>Single GPU only (no multi-GPU cooperative launch yet)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong> Cooperative launch may slightly increase kernel
    /// launch overhead (~10-50μs) but enables powerful grid-wide synchronization patterns.
    /// </para>
    /// <para>
    /// <strong>Device Limits:</strong> Query <c>cooperativeKernel</c> attribute to determine
    /// if device supports cooperative launch, and <c>maxThreadsPerMultiProcessor</c> to
    /// calculate maximum concurrent grid size.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown when device does not support cooperative launch (CC &lt; 6.0).
    /// </exception>
    public void EnableCooperativeLaunch(bool enable = true);

    /// <summary>
    /// Gets whether cooperative kernel launch is currently enabled.
    /// </summary>
    /// <value>
    /// True if cooperative launch is enabled, false otherwise.
    /// </value>
    /// <remarks>
    /// This property reflects the current configuration set via <see cref="EnableCooperativeLaunch"/>.
    /// When true, all kernel launches will use cooperative mode, enabling grid-wide barriers.
    /// </remarks>
    public bool IsCooperativeLaunchEnabled { get; }

    /// <summary>
    /// Gets the maximum number of threads that can participate in a grid-wide barrier.
    /// </summary>
    /// <returns>
    /// Maximum grid size for cooperative launch, or 0 if not supported.
    /// Value depends on device compute capability and available SM resources.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This limit is computed from device properties:
    /// <code>
    /// maxGridSize = multiProcessorCount * maxThreadsPerMultiProcessor
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Typical Values:</strong>
    /// <list type="bullet">
    /// <item><description>Pascal (CC 6.0): ~50,000 threads</description></item>
    /// <item><description>Volta (CC 7.0): ~80,000 threads</description></item>
    /// <item><description>Ampere (CC 8.0): ~100,000+ threads</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int GetMaxCooperativeGridSize();

    /// <summary>
    /// Gets the number of currently active (non-disposed) barriers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this property to monitor barrier resource usage. Most algorithms require
    /// 1-4 barriers, but complex kernels may use up to the hardware limit (typically 16).
    /// </para>
    /// <para>
    /// <strong>Resource Limits:</strong>
    /// <list type="bullet">
    /// <item><description>Thread-block barriers: Up to 16 per block (named barriers)</description></item>
    /// <item><description>Grid barriers: Limited by device memory, typically hundreds</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int ActiveBarrierCount { get; }

    /// <summary>
    /// Destroys all barriers and resets the provider to initial state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ WARNING:</strong> This method forcefully destroys all barriers, including
    /// those that may have waiting threads. Only call this during cleanup or error recovery.
    /// </para>
    /// <para>
    /// After reset, all existing <see cref="IBarrierHandle"/> references become invalid
    /// and will throw <see cref="ObjectDisposedException"/> if used.
    /// </para>
    /// </remarks>
    public void ResetAllBarriers();
}
