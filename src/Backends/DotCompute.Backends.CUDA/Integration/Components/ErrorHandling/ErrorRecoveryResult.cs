// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// Error recovery result.
/// </summary>
public sealed class ErrorRecoveryResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> ActionsPerformed { get; init; } = [];
}