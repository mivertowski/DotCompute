// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Recovery
{
    /// <summary>
    /// Configuration options for device reset operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reset options control the behavior and scope of device reset operations.
    /// Different backends may support different features based on hardware capabilities.
    /// </para>
    /// <para>
    /// <b>Orleans Integration</b>: Use reset operations during grain deactivation,
    /// error recovery, or when transferring device ownership between grains.
    /// </para>
    /// </remarks>
    public sealed class ResetOptions
    {
        /// <summary>
        /// Gets or sets the type of reset to perform.
        /// </summary>
        /// <remarks>
        /// Default: <see cref="ResetType.Soft"/> for minimal disruption.
        /// </remarks>
        public ResetType ResetType { get; set; } = ResetType.Soft;

        /// <summary>
        /// Gets or sets whether to wait for all pending operations to complete before reset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, ensures all submitted kernels and memory transfers complete before reset.
        /// When false, may cancel pending operations (behavior varies by backend).
        /// </para>
        /// <para>Default: true (safe default, prevents data loss)</para>
        /// </remarks>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to clear the memory pool during reset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, releases all pooled memory allocations.
        /// When false, retains memory pool for faster subsequent allocations.
        /// </para>
        /// <para>Default: false (preserve pool for performance)</para>
        /// <para><b>Note</b>: <see cref="ResetType.Hard"/> and <see cref="ResetType.Full"/> always clear memory.</para>
        /// </remarks>
        public bool ClearMemoryPool { get; set; }

        /// <summary>
        /// Gets or sets whether to clear the kernel compilation cache.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, removes all cached compiled kernels.
        /// When false, retains compiled kernels for faster reuse.
        /// </para>
        /// <para>Default: false (preserve cache for performance)</para>
        /// <para><b>Note</b>: <see cref="ResetType.Context"/> and above always clear cache.</para>
        /// </remarks>
        public bool ClearKernelCache { get; set; }

        /// <summary>
        /// Gets or sets the maximum time to wait for reset completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Prevents indefinite blocking if device becomes unresponsive.
        /// If timeout expires, reset fails with <see cref="TimeoutException"/>.
        /// </para>
        /// <para>Default: 30 seconds</para>
        /// <para><b>Recommended</b>: 5-10 seconds for Soft/Context, 30-60 seconds for Hard/Full</para>
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to reinitialize the device after reset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, recreates device context and resources after reset.
        /// When false, leaves device in uninitialized state (requires manual reinitialization).
        /// </para>
        /// <para>Default: true (device remains usable)</para>
        /// <para><b>Note</b>: <see cref="ResetType.Full"/> may require reinit regardless of this setting.</para>
        /// </remarks>
        public bool Reinitialize { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to force reset even if device appears healthy.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, performs reset unconditionally.
        /// When false, may skip reset if device health checks pass.
        /// </para>
        /// <para>Default: false (skip unnecessary resets)</para>
        /// </remarks>
        public bool Force { get; set; }

        /// <summary>
        /// Gets the default reset options for routine cleanup.
        /// </summary>
        public static ResetOptions Default { get; } = new()
        {
            ResetType = ResetType.Soft,
            WaitForCompletion = true,
            ClearMemoryPool = false,
            ClearKernelCache = false,
            Timeout = TimeSpan.FromSeconds(30),
            Reinitialize = false,
            Force = false
        };

        /// <summary>
        /// Gets reset options for soft reset (minimal disruption).
        /// </summary>
        public static ResetOptions Soft { get; } = new()
        {
            ResetType = ResetType.Soft,
            WaitForCompletion = true,
            ClearMemoryPool = false,
            ClearKernelCache = false,
            Timeout = TimeSpan.FromSeconds(30),
            Reinitialize = false,
            Force = false
        };

        /// <summary>
        /// Gets reset options for context reset (clear caches).
        /// </summary>
        public static ResetOptions Context { get; } = new()
        {
            ResetType = ResetType.Context,
            WaitForCompletion = true,
            ClearMemoryPool = false,
            ClearKernelCache = true,
            Timeout = TimeSpan.FromSeconds(30),
            Reinitialize = false,
            Force = false
        };

        /// <summary>
        /// Gets reset options for hard reset (clear memory and caches).
        /// </summary>
        public static ResetOptions Hard { get; } = new()
        {
            ResetType = ResetType.Hard,
            WaitForCompletion = true,
            ClearMemoryPool = true,
            ClearKernelCache = true,
            Timeout = TimeSpan.FromSeconds(60),
            Reinitialize = false,
            Force = false
        };

        /// <summary>
        /// Gets reset options for full reset (complete reinitialization).
        /// </summary>
        public static ResetOptions Full { get; } = new()
        {
            ResetType = ResetType.Full,
            WaitForCompletion = true,
            ClearMemoryPool = true,
            ClearKernelCache = true,
            Timeout = TimeSpan.FromSeconds(60),
            Reinitialize = true,
            Force = false
        };

        /// <summary>
        /// Gets reset options for error recovery scenarios.
        /// </summary>
        public static ResetOptions ErrorRecovery { get; } = new()
        {
            ResetType = ResetType.Hard,
            WaitForCompletion = false, // Cancel pending operations
            ClearMemoryPool = true,
            ClearKernelCache = true,
            Timeout = TimeSpan.FromSeconds(60),
            Reinitialize = true,
            Force = true
        };

        /// <summary>
        /// Gets reset options for grain deactivation (Orleans).
        /// </summary>
        public static ResetOptions GrainDeactivation { get; } = new()
        {
            ResetType = ResetType.Context,
            WaitForCompletion = true,
            ClearMemoryPool = false, // Let pool manager decide
            ClearKernelCache = false, // Preserve for other grains
            Timeout = TimeSpan.FromSeconds(10),
            Reinitialize = false, // Device may be reused
            Force = false
        };

        /// <summary>
        /// Gets reset options for complete device cleanup.
        /// </summary>
        public static ResetOptions CompleteCleanup { get; } = new()
        {
            ResetType = ResetType.Full,
            WaitForCompletion = true,
            ClearMemoryPool = true,
            ClearKernelCache = true,
            Timeout = TimeSpan.FromSeconds(60),
            Reinitialize = true,
            Force = true
        };
    }
}
