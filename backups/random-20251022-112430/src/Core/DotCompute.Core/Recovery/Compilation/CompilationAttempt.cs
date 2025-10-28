// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Types;
using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Compilation;

/// <summary>
/// Represents a single compilation attempt within a fallback recovery process.
/// Contains detailed information about the strategy used, timing, and outcome.
/// </summary>
/// <remarks>
/// Each compilation attempt records the specific parameters and results of
/// trying to compile a kernel with a particular fallback strategy. This
/// information is used for:
/// - Performance analysis and optimization
/// - Understanding which strategies work for specific types of kernels
/// - Debugging compilation issues
/// - Optimizing the order of fallback strategies
///
/// The timing information helps identify slow compilation paths that may
/// need optimization or should be deprioritized in fallback sequences.
/// </remarks>
public class CompilationAttempt
{
    /// <summary>
    /// Gets or sets the fallback strategy that was applied for this compilation attempt.
    /// </summary>
    /// <value>The specific strategy used for this attempt.</value>
    public CompilationFallbackStrategy Strategy { get; set; }


    /// <summary>
    /// Gets or sets the compilation options that were used for this attempt.
    /// These options may have been modified by the fallback strategy.
    /// </summary>
    /// <value>The compilation configuration for this attempt.</value>
    public CompilationOptions Options { get; set; } = null!;


    /// <summary>
    /// Gets or sets the timestamp when this compilation attempt started.
    /// </summary>
    /// <value>The UTC timestamp marking the beginning of the attempt.</value>
    public DateTimeOffset StartTime { get; set; }


    /// <summary>
    /// Gets or sets the timestamp when this compilation attempt completed.
    /// </summary>
    /// <value>The UTC timestamp marking the end of the attempt.</value>
    public DateTimeOffset EndTime { get; set; }


    /// <summary>
    /// Gets or sets the duration of this compilation attempt.
    /// </summary>
    /// <value>The time span from start to completion of the attempt.</value>
    public TimeSpan Duration { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether this compilation attempt succeeded.
    /// </summary>
    /// <value>true if the attempt resulted in successful compilation; otherwise, false.</value>
    public bool Success { get; set; }


    /// <summary>
    /// Gets or sets the error message if this compilation attempt failed.
    /// </summary>
    /// <value>The error description, or null if the attempt succeeded.</value>
    public string? Error { get; set; }
}