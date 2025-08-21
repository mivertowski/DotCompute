// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Types;
using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Compilation;

/// <summary>
/// Represents the result of a compilation fallback operation, including success status,
/// the strategy that ultimately succeeded, and detailed information about all attempts made.
/// </summary>
/// <remarks>
/// The compilation fallback result provides comprehensive information about the
/// recovery process, including:
/// - Whether any fallback strategy ultimately succeeded
/// - Which specific strategy achieved success
/// - The final compilation options that worked
/// - A complete log of all attempts and their outcomes
/// - Performance metrics for the entire fallback process
/// 
/// This information is crucial for optimizing fallback strategy ordering and
/// identifying patterns in compilation failures.
/// </remarks>
public class CompilationFallbackResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the compilation fallback operation succeeded.
    /// </summary>
    /// <value>true if any fallback strategy resulted in successful compilation; otherwise, false.</value>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the error message if the fallback operation failed completely.
    /// </summary>
    /// <value>The final error message, or null if the operation succeeded.</value>
    public string? Error { get; set; }
    
    /// <summary>
    /// Gets or sets the fallback strategy that ultimately resulted in successful compilation.
    /// </summary>
    /// <value>The successful strategy, or null if no strategy succeeded.</value>
    public CompilationFallbackStrategy? FinalStrategy { get; set; }
    
    /// <summary>
    /// Gets or sets the final compilation options that resulted in successful compilation.
    /// </summary>
    /// <value>The successful compilation configuration, or null if no compilation succeeded.</value>
    public CompilationOptions? FinalOptions { get; set; }
    
    /// <summary>
    /// Gets or sets the successfully compiled kernel object.
    /// </summary>
    /// <value>The compiled kernel instance, or null if compilation failed.</value>
    public object? CompiledKernel { get; set; }
    
    /// <summary>
    /// Gets or sets the list of all compilation attempts made during the fallback process.
    /// This includes both successful and failed attempts.
    /// </summary>
    /// <value>A chronological list of all compilation attempts.</value>
    public List<CompilationAttempt> Attempts { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the total duration of the fallback operation in milliseconds.
    /// This includes all compilation attempts and recovery overhead.
    /// </summary>
    /// <value>The total time spent on the fallback operation.</value>
    public double TotalDuration { get; set; }

    /// <summary>
    /// Returns a string representation of the compilation fallback result.
    /// </summary>
    /// <returns>A formatted string indicating success status, strategy used, and performance metrics.</returns>
    public override string ToString()
        => Success
            ? $"SUCCESS with {FinalStrategy} in {TotalDuration}ms ({Attempts.Count} attempts)"
            : $"FAILED after {Attempts.Count} attempts: {Error}";
}