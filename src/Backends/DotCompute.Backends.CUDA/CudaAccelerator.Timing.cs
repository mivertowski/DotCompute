// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA.Timing;
using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Timing provider integration for CudaAccelerator.
/// </summary>
public sealed partial class CudaAccelerator
{
    // CA2213: DisposeTiming() is called from DisposeCoreAsync(), which disposes this field
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed via DisposeTiming() partial method")]
    private CudaTimingProvider? _timingProvider;

    /// <inheritdoc/>
    public override ITimingProvider? GetTimingProvider()
    {
        if (_timingProvider == null)
        {
            // Create timing provider on demand
            // Use the default stream for timing operations
            var stream = new CudaStream(_context.Stream);
            _timingProvider = new CudaTimingProvider(_device, stream, Logger);
        }

        return _timingProvider;
    }

    /// <summary>
    /// Disposes the timing provider resources.
    /// </summary>
    partial void DisposeTiming()
    {
        _timingProvider?.Dispose();
        _timingProvider = null;
    }
}
