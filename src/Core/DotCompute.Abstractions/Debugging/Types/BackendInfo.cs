// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Information about an available backend.
/// </summary>
public class BackendInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string[] Capabilities { get; init; } = Array.Empty<string>();
    public Dictionary<string, object> Properties { get; init; } = [];
    public string? UnavailabilityReason { get; init; }
    public int Priority { get; init; }

    /// <summary>Gets the backend type (e.g., "CPU", "CUDA", "Metal").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the maximum memory available on this backend in bytes.</summary>
    public long MaxMemory { get; init; }
}
