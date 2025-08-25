// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Context information for GPU recovery operations
/// </summary>
public class GpuRecoveryContext
{
    public string DeviceId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public Exception Error { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}