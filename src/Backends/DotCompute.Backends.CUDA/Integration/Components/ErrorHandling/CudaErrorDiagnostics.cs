// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Health;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// CUDA error diagnostics information.
/// </summary>
public sealed class CudaErrorDiagnostics
{
    public long TotalErrors { get; init; }
    public long CriticalErrors { get; init; }
    public long RecoverableErrors { get; init; }
    public double RecoverySuccessRate { get; init; }
    public CudaErrorRecord[] RecentErrors { get; init; } = [];
    public Dictionary<CudaError, long> MostCommonErrors { get; init; } = [];
    public ErrorTrends ErrorTrends { get; init; } = new();
    public CudaDeviceStatus DeviceStatus { get; init; } = new();
    public DateTimeOffset LastDiagnosticsUpdate { get; init; }
}