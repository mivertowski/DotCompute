// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Accelerators;

/// <summary>
/// Partial class for Metal memory ordering provider integration.
/// </summary>
public partial class MetalAccelerator
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Disposed via DisposeMemoryOrderingProvider() partial method called from Dispose()")]
    private MetalMemoryOrderingProvider? _memoryOrderingProvider;

    /// <summary>
    /// Gets the Metal memory ordering provider for controlling memory consistency.
    /// </summary>
    /// <returns>Memory ordering provider instance, or null if not available.</returns>
    /// <remarks>
    /// <para>
    /// Metal's memory ordering provider implements causal memory ordering using barrier primitives:
    /// <list type="bullet">
    /// <item><description>threadgroup_barrier() for threadgroup scope (~10-20ns)</description></item>
    /// <item><description>simdgroup_barrier() for simdgroup scope (~5ns)</description></item>
    /// <item><description>Memory fence flags: mem_device, mem_threadgroup, mem_texture</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong>
    /// <list type="bullet">
    /// <item><description>Relaxed model: 1.0× (no overhead)</description></item>
    /// <item><description>Release-Acquire model: 0.85× (15% overhead)</description></item>
    /// <item><description>Sequential model: 0.60× (40% overhead)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Apple Silicon Advantage:</strong> Unified memory architecture provides strong coherence
    /// guarantees, making Release-Acquire semantics more efficient than on discrete GPUs.
    /// </para>
    /// </remarks>
    public IMemoryOrderingProvider? GetMemoryOrderingProvider()
    {
        if (_memoryOrderingProvider != null)
        {
            return _memoryOrderingProvider;
        }

        try
        {
            var config = new MetalMemoryOrderingConfiguration
            {
                EnableProfiling = false,
                ValidateFenceInsertions = true,
                OptimizeFencePlacement = true,
                MaxOptimizationPasses = 3
            };

            _memoryOrderingProvider = new MetalMemoryOrderingProvider(_logger, config);

            _logger.LogInformation(
                "Metal memory ordering provider initialized. " +
                "Default model: Relaxed, Acquire-Release supported: {AcquireReleaseSupported}",
                _memoryOrderingProvider.IsAcquireReleaseSupported);

            return _memoryOrderingProvider;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to create Metal memory ordering provider. " +
                "Advanced memory ordering features will not be available.");
            return null;
        }
    }

    /// <summary>
    /// Disposes the memory ordering provider.
    /// Called during accelerator disposal.
    /// </summary>
    partial void DisposeMemoryOrderingProvider()
    {
        if (_memoryOrderingProvider != null)
        {
            _memoryOrderingProvider.Dispose();
            _memoryOrderingProvider = null;

            _logger.LogDebug("Metal memory ordering provider disposed");
        }
    }
}
