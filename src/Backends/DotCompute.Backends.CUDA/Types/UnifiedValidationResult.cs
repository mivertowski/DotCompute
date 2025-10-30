// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Validation result for CUDA operations
    /// </summary>
    public sealed class UnifiedValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether valid.
        /// </summary>
        /// <value>The is valid.</value>
        public bool IsValid { get; init; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; init; }
        /// <summary>
        /// Gets or sets the warnings.
        /// </summary>
        /// <value>The warnings.</value>
        public IReadOnlyList<string> Warnings { get; init; } = [];
        /// <summary>
        /// Gets success.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public static UnifiedValidationResult Success() => new() { IsValid = true };
        /// <summary>
        /// Gets success.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>
        public static UnifiedValidationResult Success(string message) => new() { IsValid = true, ErrorMessage = message };
        /// <summary>
        /// Gets error.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>
        public static UnifiedValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
        /// <summary>
        /// Gets failure.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>
        public static UnifiedValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
        /// <summary>
        /// Gets success with warnings.
        /// </summary>
        /// <param name="warnings">The warnings.</param>
        /// <returns>The result of the operation.</returns>
        public static UnifiedValidationResult SuccessWithWarnings(IReadOnlyList<string> warnings) => new() { IsValid = true, Warnings = warnings };
        /// <summary>
        /// Gets success with warnings.
        /// </summary>
        /// <param name="warnings">The warnings.</param>
        /// <returns>The result of the operation.</returns>
        public static UnifiedValidationResult SuccessWithWarnings(string[] warnings) => new() { IsValid = true, Warnings = [.. warnings] };
    }
}
