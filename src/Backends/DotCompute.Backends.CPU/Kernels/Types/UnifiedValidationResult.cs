// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Types;

/// <summary>
/// Represents a validation result for kernel compilation.
/// </summary>
internal readonly struct UnifiedValidationResult(bool isValid, string? errorMessage)
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; } = isValid;

    /// <summary>
    /// Gets the error message if validation failed, otherwise null.
    /// </summary>
    public string? ErrorMessage { get; } = errorMessage;
}