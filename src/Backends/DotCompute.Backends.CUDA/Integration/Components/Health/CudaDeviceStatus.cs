// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Health;

/// <summary>
/// Device status information.
/// </summary>
public sealed class CudaDeviceStatus
{
    public int DeviceId { get; init; }
    public bool IsAccessible { get; init; }
    public long TotalMemory { get; init; }
    public long FreeMemory { get; init; }
    public double MemoryUtilization { get; init; }
    public DateTimeOffset LastQueried { get; init; }
    public string? ErrorMessage { get; init; }
}