// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;

namespace DotCompute.Abstractions.Ports;

/// <summary>
/// Port interface for kernel compilation operations.
/// Part of hexagonal architecture - defines the contract that backend adapters must implement.
/// </summary>
/// <remarks>
/// <para>
/// This port abstracts kernel compilation from backend-specific implementations.
/// Each backend (CUDA, Metal, OpenCL, CPU) provides an adapter that implements this interface.
/// </para>
/// <para>
/// <b>Hexagonal Architecture:</b> This is a "driven port" - the application core
/// defines this interface and backend adapters implement it.
/// </para>
/// </remarks>
public interface IKernelCompilationPort
{
    /// <summary>
    /// Compiles kernel source code to an executable form.
    /// </summary>
    /// <param name="source">The kernel source code.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled kernel ready for execution.</returns>
    ValueTask<ICompiledKernel> CompileAsync(
        KernelSource source,
        KernelCompilationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates kernel source without full compilation.
    /// </summary>
    /// <param name="source">The kernel source to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValueTask<KernelValidationResult> ValidateAsync(
        KernelSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the compilation capabilities of this backend.
    /// </summary>
    KernelCompilationCapabilities Capabilities { get; }
}

/// <summary>
/// Represents kernel source code for compilation.
/// </summary>
/// <param name="Code">The kernel source code.</param>
/// <param name="Language">The source language.</param>
/// <param name="EntryPoint">Optional entry point function name.</param>
public readonly record struct KernelSource(
    string Code,
    KernelLanguage Language,
    string? EntryPoint = null);

/// <summary>
/// Options for kernel compilation.
/// </summary>
public sealed record KernelCompilationOptions
{
    /// <summary>Default compilation options.</summary>
    public static KernelCompilationOptions Default { get; } = new();

    /// <summary>Optimization level.</summary>
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.Default;

    /// <summary>Generate debug information.</summary>
    public bool GenerateDebugInfo { get; init; }

    /// <summary>Target compute capability (backend-specific).</summary>
    public string? TargetCapability { get; init; }

    /// <summary>Additional compiler flags.</summary>
    public IReadOnlyList<string> AdditionalFlags { get; init; } = [];
}

/// <summary>
/// Result of kernel validation.
/// </summary>
public sealed record KernelValidationResult
{
    /// <summary>Whether the kernel is valid.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Validation errors.</summary>
    public IReadOnlyList<KernelDiagnostic> Errors { get; init; } = [];

    /// <summary>Validation warnings.</summary>
    public IReadOnlyList<KernelDiagnostic> Warnings { get; init; } = [];

    /// <summary>Creates a successful validation result.</summary>
    public static KernelValidationResult Success() => new() { IsValid = true };

    /// <summary>Creates a failed validation result.</summary>
    public static KernelValidationResult Failure(params KernelDiagnostic[] errors) =>
        new() { IsValid = false, Errors = errors };
}

/// <summary>
/// A diagnostic message from kernel compilation or validation.
/// </summary>
/// <param name="Code">Diagnostic code (e.g., "DC001").</param>
/// <param name="Message">Human-readable message.</param>
/// <param name="Severity">Diagnostic severity.</param>
/// <param name="Location">Optional source location.</param>
public readonly record struct KernelDiagnostic(
    string Code,
    string Message,
    DiagnosticSeverity Severity,
    SourceLocation? Location = null);

/// <summary>
/// Severity level for diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational message.</summary>
    Info,
    /// <summary>Warning that doesn't prevent compilation.</summary>
    Warning,
    /// <summary>Error that prevents compilation.</summary>
    Error
}

/// <summary>
/// Location in source code.
/// </summary>
/// <param name="Line">Line number (1-based).</param>
/// <param name="Column">Column number (1-based).</param>
/// <param name="FilePath">Optional file path.</param>
public readonly record struct SourceLocation(
    int Line,
    int Column,
    string? FilePath = null);

/// <summary>
/// Kernel source language.
/// </summary>
public enum KernelLanguage
{
    /// <summary>C# kernel (translated by source generator).</summary>
    CSharp,
    /// <summary>CUDA C/C++.</summary>
    Cuda,
    /// <summary>PTX assembly.</summary>
    Ptx,
    /// <summary>Metal Shading Language.</summary>
    Msl,
    /// <summary>OpenCL C.</summary>
    OpenCL,
    /// <summary>SPIR-V binary.</summary>
    SpirV
}

/// <summary>
/// Compilation capabilities of a backend.
/// </summary>
public sealed record KernelCompilationCapabilities
{
    /// <summary>Supported source languages.</summary>
    public required IReadOnlyList<KernelLanguage> SupportedLanguages { get; init; }

    /// <summary>Supports runtime compilation.</summary>
    public bool SupportsRuntimeCompilation { get; init; }

    /// <summary>Supports binary caching.</summary>
    public bool SupportsBinaryCaching { get; init; }

    /// <summary>Maximum kernel parameters.</summary>
    public int MaxKernelParameters { get; init; } = 256;
}

/// <summary>
/// Optimization level for compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>No optimization, fastest compilation.</summary>
    None,
    /// <summary>Basic optimizations.</summary>
    Basic,
    /// <summary>Default optimizations (recommended).</summary>
    Default,
    /// <summary>Aggressive optimizations, slower compilation.</summary>
    Aggressive,
    /// <summary>Maximum optimizations, slowest compilation.</summary>
    Maximum
}
