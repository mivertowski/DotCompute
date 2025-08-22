// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents the context for compilation recovery operations
/// </summary>
public class CompilationRecoveryContext
{
    /// <summary>
    /// Gets or sets the original compilation options
    /// </summary>
    public CompilationOptions OriginalOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the kernel source code
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel name
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compilation error that triggered recovery
    /// </summary>
    public Exception? CompilationError { get; set; }

    /// <summary>
    /// Gets or sets the number of recovery attempts made
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of recovery attempts allowed
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the compilation history for this kernel
    /// </summary>
    public CompilationHistory History { get; set; } = new();

    /// <summary>
    /// Gets or sets additional context data
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Gets or sets the target backend for compilation
    /// </summary>
    public string? TargetBackend { get; set; }

    /// <summary>
    /// Gets a value indicating whether recovery attempts are exhausted
    /// </summary>
    public bool IsRecoveryExhausted => AttemptCount >= MaxAttempts;
}

/// <summary>
/// Represents the compilation history for a kernel
/// </summary>
public class CompilationHistory
{
    /// <summary>
    /// Gets or sets the list of compilation attempts
    /// </summary>
    public List<CompilationAttempt> Attempts { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the first compilation attempt
    /// </summary>
    public DateTimeOffset FirstAttempt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp of the last compilation attempt
    /// </summary>
    public DateTimeOffset LastAttempt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the total number of successful compilations
    /// </summary>
    public int SuccessfulCompilations { get; set; }

    /// <summary>
    /// Gets or sets the total number of failed compilations
    /// </summary>
    public int FailedCompilations { get; set; }
}

/// <summary>
/// Represents a single compilation attempt
/// </summary>
public class CompilationAttempt
{
    /// <summary>
    /// Gets or sets the timestamp of the attempt
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the compilation options used
    /// </summary>
    public CompilationOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the compilation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if compilation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the compilation duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the fallback strategy used
    /// </summary>
    public CompilationFallbackStrategy? Strategy { get; set; }
}

/// <summary>
/// Represents different fallback strategies for compilation recovery
/// </summary>
public enum CompilationFallbackStrategy
{
    /// <summary>
    /// Reduce optimization level
    /// </summary>
    ReduceOptimization,

    /// <summary>
    /// Disable fast math
    /// </summary>
    DisableFastMath,

    /// <summary>
    /// Use interpreted mode
    /// </summary>
    UseInterpretedMode,

    /// <summary>
    /// Change compiler backend
    /// </summary>
    ChangeBackend,

    /// <summary>
    /// Fallback to CPU execution
    /// </summary>
    FallbackToCpu,

    /// <summary>
    /// Custom strategy
    /// </summary>
    Custom
}