// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Gpu;

/// <summary>
/// Provides context information for GPU recovery operations.
/// Contains all relevant information about the failure and recovery environment.
/// </summary>
/// <remarks>
/// This class encapsulates the state and metadata needed to perform
/// informed recovery decisions and to track recovery operations.
/// </remarks>
public class GpuRecoveryContext
{
    /// <summary>
    /// Gets or sets the unique identifier of the GPU device where the failure occurred.
    /// </summary>
    /// <value>A string identifier for the device, typically the device index or name.</value>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name or description of the operation that failed.
    /// </summary>
    /// <value>A descriptive string identifying the failed operation (e.g., "kernel_execution", "memory_allocation").</value>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception that caused the failure requiring recovery.
    /// </summary>
    /// <value>The original exception that triggered the recovery process.</value>
    public Exception Error { get; set; } = null!;

    /// <summary>
    /// Gets or sets the timestamp when the failure occurred.
    /// </summary>
    /// <value>The UTC timestamp of when the error was detected.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets additional metadata related to the failure and recovery context.
    /// </summary>
    /// <value>A dictionary containing key-value pairs of relevant metadata such as memory usage, kernel parameters, etc.</value>
    /// <remarks>
    /// Common metadata keys include:
    /// - "memory_usage": Current memory usage at time of failure
    /// - "kernel_name": Name of the kernel that failed
    /// - "execution_time": Time spent executing before failure
    /// - "retry_count": Number of previous retry attempts
    /// </remarks>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
