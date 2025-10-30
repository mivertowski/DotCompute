// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Utilities.ErrorHandling.Enums;

/// <summary>
/// Error classification enumeration for systematic error categorization.
/// Provides standardized error types for consistent handling across all backends.
/// </summary>
public enum ErrorClassification
{
    /// <summary>
    /// Unknown or unclassified error.
    /// </summary>
    Unknown,

    /// <summary>
    /// Memory exhaustion or out-of-memory conditions.
    /// </summary>
    MemoryExhaustion,

    /// <summary>
    /// Required hardware device not found or unavailable.
    /// </summary>
    DeviceNotFound,

    /// <summary>
    /// Hardware device is currently busy or in use.
    /// </summary>
    DeviceBusy,

    /// <summary>
    /// Hardware failure or malfunction detected.
    /// </summary>
    HardwareFailure,

    /// <summary>
    /// Invalid configuration or setup detected.
    /// </summary>
    InvalidConfiguration,

    /// <summary>
    /// Invalid input parameters or data provided.
    /// </summary>
    InvalidInput,

    /// <summary>
    /// Access denied or insufficient permissions.
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// Operation timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Data corruption or integrity violation detected.
    /// </summary>
    DataCorruption,

    /// <summary>
    /// Network connectivity or communication error.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Security violation or unauthorized access attempt.
    /// </summary>
    SecurityViolation,

    /// <summary>
    /// Critical system failure.
    /// </summary>
    SystemFailure,

    /// <summary>
    /// Compute engine or kernel execution error.
    /// </summary>
    ComputeError,

    /// <summary>
    /// Requested feature is not supported.
    /// </summary>
    FeatureNotSupported,

    /// <summary>
    /// Resource contention or concurrency issue.
    /// </summary>
    ResourceContention,

    /// <summary>
    /// Temporary failure that may resolve on retry.
    /// </summary>
    TemporaryFailure
}
