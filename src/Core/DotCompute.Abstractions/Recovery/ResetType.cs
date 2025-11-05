// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Recovery
{
    /// <summary>
    /// Defines the type of device reset to perform.
    /// </summary>
    /// <remarks>
    /// Different reset types provide varying levels of device state cleanup:
    /// <list type="bullet">
    /// <item><see cref="Soft"/>: Quick cleanup without full reinitialization</item>
    /// <item><see cref="Context"/>: Reset device context and kernel state</item>
    /// <item><see cref="Hard"/>: Full device reset with memory clearing</item>
    /// <item><see cref="Full"/>: Complete reset including driver state</item>
    /// </list>
    /// </remarks>
    public enum ResetType
    {
        /// <summary>
        /// Soft reset: Clear pending operations and flush command queues.
        /// Fastest option with minimal disruption to device state.
        /// </summary>
        /// <remarks>
        /// <para><b>CUDA</b>: Synchronize device, clear kernel queues</para>
        /// <para><b>Metal</b>: Wait for command buffers, clear pools</para>
        /// <para><b>OpenCL</b>: Flush command queues, wait for completion</para>
        /// <para><b>CPU</b>: Clear thread pool work items</para>
        /// <para><b>Typical Duration</b>: 1-10ms</para>
        /// </remarks>
        Soft = 0,

        /// <summary>
        /// Context reset: Reset device context, clear kernel compilation cache,
        /// and release temporary resources.
        /// </summary>
        /// <remarks>
        /// <para><b>CUDA</b>: Reset current context, clear NVRTC cache</para>
        /// <para><b>Metal</b>: Recreate command queue, clear shader cache</para>
        /// <para><b>OpenCL</b>: Release and recreate context</para>
        /// <para><b>CPU</b>: Clear SIMD state, reset thread affinity</para>
        /// <para><b>Typical Duration</b>: 10-50ms</para>
        /// </remarks>
        Context = 1,

        /// <summary>
        /// Hard reset: Full device reset including memory deallocation.
        /// Clears all device state except driver-level configuration.
        /// </summary>
        /// <remarks>
        /// <para><b>CUDA</b>: cudaDeviceReset() - clears all allocations</para>
        /// <para><b>Metal</b>: Release all buffers, recreate device resources</para>
        /// <para><b>OpenCL</b>: Release all memory objects, recreate context</para>
        /// <para><b>CPU</b>: Clear memory pools, reset performance counters</para>
        /// <para><b>Typical Duration</b>: 50-200ms</para>
        /// <para><b>Warning</b>: All UnifiedBuffer instances become invalid</para>
        /// </remarks>
        Hard = 2,

        /// <summary>
        /// Full reset: Complete device and driver state reset.
        /// Most thorough option, used for recovery from critical errors.
        /// </summary>
        /// <remarks>
        /// <para><b>CUDA</b>: cudaDeviceReset() + driver reinit</para>
        /// <para><b>Metal</b>: Release device, recreate from system default</para>
        /// <para><b>OpenCL</b>: Reinitialize platform and device</para>
        /// <para><b>CPU</b>: Full backend reinitialization</para>
        /// <para><b>Typical Duration</b>: 200-1000ms</para>
        /// <para><b>Warning</b>: All accelerator references become invalid</para>
        /// </remarks>
        Full = 3
    }
}
