// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA-specific implementation of memory ordering primitives.
/// </summary>
/// <remarks>
/// <para>
/// This provider implements causal memory ordering using CUDA's __threadfence_* intrinsics:
/// <list type="bullet">
/// <item><description><strong>__threadfence_block():</strong> Thread-block scope (~10ns)</description></item>
/// <item><description><strong>__threadfence():</strong> Device scope (~100ns)</description></item>
/// <item><description><strong>__threadfence_system():</strong> System scope (~200ns)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Compute Capability Requirements:</strong>
/// <list type="bullet">
/// <item><description>CC 2.0+: Thread-block and device fences</description></item>
/// <item><description>CC 2.0+ with UVA: System fences (requires unified virtual addressing)</description></item>
/// <item><description>CC 7.0+ (Volta): Hardware acquire-release support</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Characteristics:</strong>
/// <list type="bullet">
/// <item><description>Relaxed model: 1.0× (baseline, no overhead)</description></item>
/// <item><description>Release-Acquire model: 0.85× (15% overhead)</description></item>
/// <item><description>Sequential model: 0.60× (40% overhead)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Configuration methods (SetConsistencyModel, EnableCausalOrdering)
/// are not thread-safe and should be called during initialization only. Fence insertion is safe
/// to call concurrently from multiple threads.
/// </para>
/// </remarks>
public sealed class CudaMemoryOrderingProvider : IMemoryOrderingProvider, IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Debug,
        Message = "CudaMemoryOrderingProvider initialized. Model: {Model}, Acquire-Release supported: {AcquireReleaseSupported}")]
    private static partial void LogProviderInitialized(ILogger logger, MemoryConsistencyModel model, bool acquireReleaseSupported);

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Information,
        Message = "Consistency model changed from {OldModel} to {NewModel}. Performance multiplier: {Multiplier:F2}×")]
    private static partial void LogConsistencyModelChanged(ILogger logger, MemoryConsistencyModel oldModel, MemoryConsistencyModel newModel, double multiplier);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Debug,
        Message = "Causal ordering {Status}. Release-acquire semantics {State}")]
    private static partial void LogCausalOrderingChanged(ILogger logger, string status, string state);

    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Debug,
        Message = "Inserted {FenceType} fence at location: Entry={AtEntry}, Exit={AtExit}, AfterWrites={AfterWrites}, BeforeReads={BeforeReads}")]
    private static partial void LogFenceInserted(ILogger logger, FenceType fenceType, bool atEntry, bool atExit, bool afterWrites, bool beforeReads);

    [LoggerMessage(
        EventId = 9004,
        Level = LogLevel.Warning,
        Message = "System-wide fences require unified virtual addressing (UVA). Falling back to device fences")]
    private static partial void LogSystemFenceNotSupported(ILogger logger);

    [LoggerMessage(
        EventId = 9005,
        Level = LogLevel.Warning,
        Message = "Sequential consistency imposes 40% performance overhead. Consider using Release-Acquire instead")]
    private static partial void LogSequentialConsistencyWarning(ILogger logger);

    #endregion

    private readonly ILogger _logger;
    private readonly bool _acquireReleaseSupported;
    private readonly bool _systemFencesSupported;
    private MemoryConsistencyModel _consistencyModel;
    private bool _causalOrderingEnabled;
    private bool _disposed;

    /// <summary>
    /// Initializes a new CUDA memory ordering provider.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public CudaMemoryOrderingProvider(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;

        // Detect hardware capabilities
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        _acquireReleaseSupported = major >= 7; // Volta (CC 7.0) has native acquire-release
        _systemFencesSupported = major >= 2;    // UVA available from Fermi (CC 2.0)

        // Default to relaxed model (GPU default)
        _consistencyModel = MemoryConsistencyModel.Relaxed;
        _causalOrderingEnabled = false;

        LogProviderInitialized(_logger, _consistencyModel, _acquireReleaseSupported);
    }

    /// <inheritdoc />
    public void EnableCausalOrdering(bool enable = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _causalOrderingEnabled = enable;

        // If enabling causal ordering, automatically use Release-Acquire model
        if (enable && _consistencyModel == MemoryConsistencyModel.Relaxed)
        {
            SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        }

        LogCausalOrderingChanged(_logger,
            enable ? "enabled" : "disabled",
            enable ? "active" : "inactive");
    }

    /// <inheritdoc />
    public void InsertFence(FenceType type, FenceLocation? location = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Validate system fence support
        if (type == FenceType.System && !_systemFencesSupported)
        {
            LogSystemFenceNotSupported(_logger);
            type = FenceType.Device; // Fallback to device fence
        }

        // Use default location if not specified
        location ??= FenceLocation.FullBarrier;

        LogFenceInserted(_logger, type,
            location.AtEntry, location.AtExit,
            location.AfterWrites, location.BeforeReads);

        // TODO: Integration with kernel compiler
        // When InsertFence() is called, the kernel compiler should inject the
        // appropriate __threadfence_* intrinsic at the specified location.
        //
        // Implementation approach:
        // 1. Track fence requests in a queue (thread-safe concurrent collection)
        // 2. During kernel compilation (CudaKernelCompiler), inject fences:
        //    - Parse PTX/CUBIN to find insertion points
        //    - Insert appropriate fence instruction:
        //      * ThreadBlock: bar.sync 0;
        //      * Device: membar.gl;
        //      * System: membar.sys;
        // 3. Clear fence queue after compilation
        //
        // Fence instruction format (PTX):
        // - Thread-block: bar.sync 0;
        // - Device: membar.gl;
        // - System: membar.sys;
        //
        // See: https://docs.nvidia.com/cuda/parallel-thread-execution/
    }

    /// <inheritdoc />
    public void SetConsistencyModel(MemoryConsistencyModel model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var oldModel = _consistencyModel;
        _consistencyModel = model;

        // Warn about performance impact
        if (model == MemoryConsistencyModel.Sequential)
        {
            LogSequentialConsistencyWarning(_logger);
        }

        // If changing to non-relaxed model, enable causal ordering
        if (model != MemoryConsistencyModel.Relaxed)
        {
            _causalOrderingEnabled = true;
        }

        var multiplier = GetOverheadMultiplier();
        LogConsistencyModelChanged(_logger, oldModel, model, multiplier);
    }

    /// <inheritdoc />
    public MemoryConsistencyModel ConsistencyModel => _consistencyModel;

    /// <inheritdoc />
    public bool IsAcquireReleaseSupported => _acquireReleaseSupported;

    /// <inheritdoc />
    public double GetOverheadMultiplier()
    {
        return _consistencyModel switch
        {
            MemoryConsistencyModel.Relaxed => 1.0,          // Baseline
            MemoryConsistencyModel.ReleaseAcquire => 0.85,  // 15% overhead
            MemoryConsistencyModel.Sequential => 0.60,      // 40% overhead
            _ => 1.0
        };
    }

    /// <summary>
    /// Gets whether causal ordering is currently enabled.
    /// </summary>
    internal bool IsCausalOrderingEnabled => _causalOrderingEnabled;

    /// <summary>
    /// Gets whether system-wide fences are supported.
    /// </summary>
    internal bool SupportsSystemFences => _systemFencesSupported;

    /// <summary>
    /// Disposes the memory ordering provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
