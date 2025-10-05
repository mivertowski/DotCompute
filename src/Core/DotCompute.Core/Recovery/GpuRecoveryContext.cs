// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Context information for GPU recovery operations
/// </summary>
public class GpuRecoveryContext
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the operation.
    /// </summary>
    /// <value>The operation.</value>
    public string Operation { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>The error.</value>
    public Exception Error { get; set; } = null!;
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public Dictionary<string, object> Metadata { get; init; } = [];
}