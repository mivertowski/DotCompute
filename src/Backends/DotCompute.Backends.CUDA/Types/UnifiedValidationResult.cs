// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Validation result for CUDA operations
    /// </summary>
    public sealed class UnifiedValidationResult
    {
        public bool IsValid { get; init; }
        public string? ErrorMessage { get; init; }
        public List<string> Warnings { get; init; } = [];

        public static UnifiedValidationResult Success() => new() { IsValid = true };
        public static UnifiedValidationResult Success(string message) => new() { IsValid = true, ErrorMessage = message };
        public static UnifiedValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
        public static UnifiedValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
        public static UnifiedValidationResult SuccessWithWarnings(List<string> warnings) => new() { IsValid = true, Warnings = warnings };
        public static UnifiedValidationResult SuccessWithWarnings(string[] warnings) => new() { IsValid = true, Warnings = [.. warnings] };
    }
}