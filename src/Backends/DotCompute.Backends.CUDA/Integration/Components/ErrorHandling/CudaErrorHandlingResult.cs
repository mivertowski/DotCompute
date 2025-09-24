// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// CUDA error handling result.
/// </summary>
public sealed class CudaErrorHandlingResult
{
    public CudaError OriginalError { get; init; }
    public CudaErrorSeverity Severity { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public bool RecoveryAttempted { get; init; }
    public bool RecoverySuccessful { get; init; }
    public List<string> RecoveryActions { get; init; } = [];
    public string? RecoveryMessage { get; init; }
}