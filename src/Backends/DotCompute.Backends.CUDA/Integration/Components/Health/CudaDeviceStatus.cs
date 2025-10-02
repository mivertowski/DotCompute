// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Health;

/// <summary>
/// Device status information.
/// </summary>
public sealed class CudaDeviceStatus
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public int DeviceId { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether accessible.
    /// </summary>
    /// <value>The is accessible.</value>
    public bool IsAccessible { get; init; }
    /// <summary>
    /// Gets or sets the total memory.
    /// </summary>
    /// <value>The total memory.</value>
    public long TotalMemory { get; init; }
    /// <summary>
    /// Gets or sets the free memory.
    /// </summary>
    /// <value>The free memory.</value>
    public long FreeMemory { get; init; }
    /// <summary>
    /// Gets or sets the memory utilization.
    /// </summary>
    /// <value>The memory utilization.</value>
    public double MemoryUtilization { get; init; }
    /// <summary>
    /// Gets or sets the last queried.
    /// </summary>
    /// <value>The last queried.</value>
    public DateTimeOffset LastQueried { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; init; }
}