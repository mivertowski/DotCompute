// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Defines the interface for compiling kernels from source code or expressions.
/// </summary>
public interface IKernelCompiler
{
    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the supported kernel source types.
    /// </summary>
    public KernelSourceType[] SupportedSourceTypes { get; }

    /// <summary>
    /// Compiles a kernel from the given definition.
    /// </summary>
    /// <param name="definition">The kernel definition to compile.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled kernel.</returns>
    public ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a kernel definition can be compiled.
    /// </summary>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>A validation result indicating whether compilation is possible.</returns>
    public ValidationResult Validate(KernelDefinition definition);
}

// Note: CompilationOptions and ICompiledKernel are defined in IAccelerator.cs

/// <summary>
/// Base class for compiler-specific options.
/// </summary>
public abstract class CompilerSpecificOptions
{
    /// <summary>
    /// Gets the compiler name this options object is for.
    /// </summary>
    public abstract string CompilerName { get; }
}

/// <summary>
/// Represents a validation result.
/// </summary>
public readonly struct ValidationResult : IEquatable<ValidationResult>
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets any warnings from validation.
    /// </summary>
    public string[]? Warnings { get; }

    private ValidationResult(bool isValid, string? errorMessage, string[]? warnings)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        Warnings = warnings;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, null, null);

    /// <summary>
    /// Creates a successful validation result with warnings.
    /// </summary>
    public static ValidationResult SuccessWithWarnings(params string[] warnings)
        => new(true, null, warnings);

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(string errorMessage)
        => new(false, errorMessage, null);

    /// <summary>
    /// Creates a failed validation result with warnings.
    /// </summary>
    public static ValidationResult FailureWithWarnings(string errorMessage, params string[] warnings)
        => new(false, errorMessage, warnings);

    public override bool Equals(object? obj) => throw new NotImplementedException();

    public override int GetHashCode() => throw new NotImplementedException();

    public static bool operator ==(ValidationResult left, ValidationResult right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ValidationResult left, ValidationResult right)
    {
        return !(left == right);
    }

    public bool Equals(ValidationResult other) => throw new NotImplementedException();
}

/// <summary>
/// Defines the source type of a kernel.
/// </summary>
public enum KernelSourceType
{
    /// <summary>
    /// C# expression tree.
    /// </summary>
    ExpressionTree,

    /// <summary>
    /// CUDA C/C++ source code.
    /// </summary>
    CUDA,

    /// <summary>
    /// OpenCL C source code.
    /// </summary>
    OpenCL,

    /// <summary>
    /// HLSL shader code.
    /// </summary>
    HLSL,

    /// <summary>
    /// SPIR-V bytecode.
    /// </summary>
    SPIRV,

    /// <summary>
    /// Metal shader language.
    /// </summary>
    Metal,

    /// <summary>
    /// ROCm HIP source code.
    /// </summary>
    HIP,

    /// <summary>
    /// SYCL/DPC++ source code.
    /// </summary>
    SYCL,

    /// <summary>
    /// Pre-compiled binary.
    /// </summary>
    Binary
}

/// <summary>
/// Metadata about a kernel compilation.
/// </summary>
public readonly struct CompilationMetadata : IEquatable<CompilationMetadata>
{
    /// <summary>
    /// Gets the time taken to compile.
    /// </summary>
    public TimeSpan CompilationTime { get; }

    /// <summary>
    /// Gets the size of the compiled code in bytes.
    /// </summary>
    public long CodeSize { get; }

    /// <summary>
    /// Gets the register usage per thread.
    /// </summary>
    public int RegistersPerThread { get; }

    /// <summary>
    /// Gets the shared memory usage per block.
    /// </summary>
    public long SharedMemoryPerBlock { get; }

    /// <summary>
    /// Gets any optimization notes from the compiler.
    /// </summary>
    public string[]? OptimizationNotes { get; }

    public CompilationMetadata(
        TimeSpan compilationTime,
        long codeSize,
        int registersPerThread,
        long sharedMemoryPerBlock,
        string[]? optimizationNotes = null)
    {
        CompilationTime = compilationTime;
        CodeSize = codeSize;
        RegistersPerThread = registersPerThread;
        SharedMemoryPerBlock = sharedMemoryPerBlock;
        OptimizationNotes = optimizationNotes;
    }

    public override bool Equals(object? obj) => throw new NotImplementedException();

    public override int GetHashCode() => throw new NotImplementedException();

    public static bool operator ==(CompilationMetadata left, CompilationMetadata right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CompilationMetadata left, CompilationMetadata right)
    {
        return !(left == right);
    }

    public bool Equals(CompilationMetadata other) => throw new NotImplementedException();
}
