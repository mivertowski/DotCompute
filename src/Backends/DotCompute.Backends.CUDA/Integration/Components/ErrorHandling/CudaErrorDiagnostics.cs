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
    /// <summary>
    /// Gets or sets the total errors.
    /// </summary>
    /// <value>The total errors.</value>
    public long TotalErrors { get; init; }
    /// <summary>
    /// Gets or sets the critical errors.
    /// </summary>
    /// <value>The critical errors.</value>
    public long CriticalErrors { get; init; }
    /// <summary>
    /// Gets or sets the recoverable errors.
    /// </summary>
    /// <value>The recoverable errors.</value>
    public long RecoverableErrors { get; init; }
    /// <summary>
    /// Gets or sets the recovery success rate.
    /// </summary>
    /// <value>The recovery success rate.</value>
    public double RecoverySuccessRate { get; init; }
    /// <summary>
    /// Gets or sets the recent errors.
    /// </summary>
    /// <value>The recent errors.</value>
    public IReadOnlyList<CudaErrorRecord> RecentErrors { get; init; } = [];
    /// <summary>
    /// Gets or sets the most common errors.
    /// </summary>
    /// <value>The most common errors.</value>
    public Dictionary<CudaError, long> MostCommonErrors { get; init; } = [];
    /// <summary>
    /// Gets or sets the error trends.
    /// </summary>
    /// <value>The error trends.</value>
    public ErrorTrends ErrorTrends { get; init; } = new();
    /// <summary>
    /// Gets or sets the device status.
    /// </summary>
    /// <value>The device status.</value>
    public CudaDeviceStatus DeviceStatus { get; init; } = new();
    /// <summary>
    /// Gets or sets the last diagnostics update.
    /// </summary>
    /// <value>The last diagnostics update.</value>
    public DateTimeOffset LastDiagnosticsUpdate { get; init; }
}
