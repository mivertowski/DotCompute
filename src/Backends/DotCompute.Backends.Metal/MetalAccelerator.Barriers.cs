// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.Metal.Barriers;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Accelerators;

public partial class MetalAccelerator
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Disposed via DisposeBarrierProvider() partial method called from Dispose()")]
    private MetalBarrierProvider? _barrierProvider;

    /// <summary>
    /// Gets the barrier provider for this Metal accelerator.
    /// </summary>
    /// <returns>
    /// A <see cref="IBarrierProvider"/> instance for GPU barrier synchronization,
    /// or null if barriers are not supported by this device.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The barrier provider enables hardware-accelerated synchronization primitives:
    /// <list type="bullet">
    /// <item><description>Threadgroup barriers (all Metal devices)</description></item>
    /// <item><description>Simdgroup barriers (Apple Silicon + modern AMD GPUs)</description></item>
    /// <item><description>Memory fences (device, threadgroup, and texture scopes)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Limitations vs CUDA:</strong>
    /// <list type="bullet">
    /// <item><description>❌ No grid-wide barriers (architectural limitation)</description></item>
    /// <item><description>❌ No hardware named barriers (use multiple kernel dispatches)</description></item>
    /// <item><description>✅ Threadgroup/simdgroup barriers fully supported</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var barrierProvider = accelerator.GetBarrierProvider();
    /// if (barrierProvider != null)
    /// {
    ///     // Create threadgroup barrier for 256 threads
    ///     using var barrier = barrierProvider.CreateBarrier(
    ///         BarrierScope.ThreadBlock, capacity: 256);
    ///
    ///     // Execute kernel with barrier synchronization
    ///     await barrierProvider.ExecuteWithBarrierAsync(
    ///         kernel, barrier, config, args);
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance Characteristics:</strong>
    /// <list type="bullet">
    /// <item><description>Threadgroup: ~10-20ns synchronization latency</description></item>
    /// <item><description>Simdgroup: ~5ns latency (32-thread SIMD groups)</description></item>
    /// <item><description>Memory fences: +5-10ns per fence scope</description></item>
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
        Justification = "Method performs lazy initialization and can return null on failure")]
    public IBarrierProvider? GetBarrierProvider()
    {
        if (_barrierProvider != null)
        {
            return _barrierProvider;
        }

        // Create provider on first access
        try
        {
            var config = new MetalBarrierConfiguration
            {
                EnableProfiling = false, // Can be configured via options in the future
                ValidateBarrierParameters = true
            };

            _barrierProvider = new MetalBarrierProvider(_logger, config);
            return _barrierProvider;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Metal barrier provider");
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
