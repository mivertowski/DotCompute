// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics.Compilation;

/// <summary>
/// Kernel compiler statistics tracking.
/// </summary>
public sealed class KernelCompilerStatistics
{
    private long _totalCompilations;
    private long _successfulCompilations;
    private long _cacheHits;
    private double _totalCompilationTimeMs;

    public void RecordCompilation(double timeMs, bool success)
    {
        _ = Interlocked.Increment(ref _totalCompilations);
        if (success)
        {
            _ = Interlocked.Increment(ref _successfulCompilations);
        }

        lock (this)
        {
            _totalCompilationTimeMs += timeMs;
        }
    }

    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
}