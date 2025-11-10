// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA.Barriers;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

public partial class CudaAccelerator
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Disposed via DisposeBarrierProvider() partial method called from DisposeCoreAsync()")]
    private CudaBarrierProvider? _barrierProvider;

    /// <summary>
    /// Gets the barrier provider for this CUDA accelerator.
    /// </summary>
    /// <returns>
    /// A <see cref="IBarrierProvider"/> instance for GPU barrier synchronization,
    /// or null if barriers are not supported by this device.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The barrier provider enables hardware-accelerated synchronization primitives:
    /// <list type="bullet">
    /// <item><description>Thread-block barriers (all devices, CC 1.0+)</description></item>
    /// <item><description>Grid-wide barriers (Pascal+, CC 6.0+)</description></item>
    /// <item><description>Named barriers (CC 7.0+ or device-specific support)</description></item>
    /// <item><description>Warp barriers (Volta+, CC 7.0+)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var barrierProvider = accelerator.GetBarrierProvider();
    /// if (barrierProvider != null)
    /// {
    ///     // Create thread-block barrier for 256 threads
    ///     using var barrier = barrierProvider.CreateBarrier(
    ///         BarrierScope.ThreadBlock, capacity: 256);
    ///
    ///     // In kernel: barrier.Sync(); // All threads wait here
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance Characteristics:</strong>
    /// <list type="bullet">
    /// <item><description>Thread-block: ~10ns synchronization latency</description></item>
    /// <item><description>Grid-wide: ~1-10μs latency (requires cooperative launch)</description></item>
    /// <item><description>Warp: ~1ns latency (32-thread SIMD groups)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is thread-safe. The returned provider
    /// can be used concurrently from multiple threads.
    /// </para>
    /// </remarks>
    /// <seealso cref="IBarrierProvider"/>
    /// <seealso cref="BarrierScope"/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "Method performs lazy initialization and can return null on failure, following the same pattern as GetTimingProvider()")]
    public IBarrierProvider? GetBarrierProvider()
    {
        if (_barrierProvider != null)
        {
            return _barrierProvider;
        }

        // Create provider on first access
        try
        {
            _barrierProvider = new CudaBarrierProvider(_context, _device, Logger);
            return _barrierProvider;
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to create CUDA barrier provider");
            return null;
        }
    }

    /// <summary>
    /// Disposes the barrier provider if it was created.
    /// </summary>
    partial void DisposeBarrierProvider()
    {
        _barrierProvider?.Dispose();
        _barrierProvider = null;
    }
}
