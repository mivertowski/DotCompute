using DotCompute.Abstractions;
using System;
using System.Collections.Generic;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Result of compiling a LINQ expression to a compute kernel.
/// </summary>
/// <remarks>
/// ⚠️ STUB IMPLEMENTATION - Phase 2 (Test Infrastructure)
/// Full implementation planned for Phase 3-5.
/// </remarks>
public class CompilationResult
{
    /// <summary>
    /// Gets a value indicating whether the compilation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the compiled kernel delegate (if successful).
    /// </summary>
    public Delegate? CompiledKernel { get; init; }

    /// <summary>
    /// Gets the compilation error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the compilation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the compilation time in milliseconds.
    /// </summary>
    public double CompilationTimeMs { get; init; }

    /// <summary>
    /// Gets the backend that was selected for execution.
    /// </summary>
    public ComputeBackend SelectedBackend { get; init; }

    /// <summary>
    /// Gets the generated kernel code (for debugging).
    /// </summary>
    public string? GeneratedCode { get; init; }

    /// <summary>
    /// Creates a successful compilation result.
    /// </summary>
    public static CompilationResult CreateSuccess(
        Delegate compiledKernel,
        ComputeBackend backend,
        double compilationTimeMs,
        IReadOnlyList<string>? warnings = null)
    {
        return new CompilationResult
        {
            Success = true,
            CompiledKernel = compiledKernel,
            SelectedBackend = backend,
            CompilationTimeMs = compilationTimeMs,
            Warnings = warnings ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a failed compilation result.
    /// </summary>
    public static CompilationResult CreateFailure(string errorMessage)
    {
        return new CompilationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
