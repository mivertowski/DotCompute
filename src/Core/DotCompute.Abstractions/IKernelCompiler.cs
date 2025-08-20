// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;

namespace DotCompute.Abstractions
{

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
    public class ValidationResult : IEquatable<ValidationResult>
    {
        private readonly List<string> _warnings = new();

        /// <summary>
        /// Gets whether the validation passed.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// Gets the error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Gets any warnings from validation.
        /// </summary>
        public string[] Warnings => [.. _warnings];

        private ValidationResult(bool isValid, string? errorMessage, string[]? warnings)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            if (warnings != null)
            {
                _warnings.AddRange(warnings);
            }
        }

        /// <summary>
        /// Adds a warning to this validation result.
        /// </summary>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning))
            {
                _warnings.Add(warning);
            }
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

        public override bool Equals(object? obj) => obj is ValidationResult other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(IsValid);
            hash.Add(ErrorMessage);
            foreach (var warning in Warnings)
            {
                hash.Add(warning);
            }
            return hash.ToHashCode();
        }

        public static bool operator ==(ValidationResult left, ValidationResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValidationResult left, ValidationResult right)
        {
            return !(left == right);
        }

        public bool Equals(ValidationResult? other)
        {
            if (other is null)
            {
                return false;
            }

            return IsValid == other.IsValid &&
               ErrorMessage == other.ErrorMessage &&
               Warnings.SequenceEqual(other.Warnings);
        }
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
    public readonly struct CompilationMetadata(
        TimeSpan compilationTime,
        long codeSize,
        int registersPerThread,
        long sharedMemoryPerBlock,
        string[]? optimizationNotes = null) : IEquatable<CompilationMetadata>
    {
        /// <summary>
        /// Gets the time taken to compile.
        /// </summary>
        public TimeSpan CompilationTime { get; } = compilationTime;

        /// <summary>
        /// Gets the size of the compiled code in bytes.
        /// </summary>
        public long CodeSize { get; } = codeSize;

        /// <summary>
        /// Gets the register usage per thread.
        /// </summary>
        public int RegistersPerThread { get; } = registersPerThread;

        /// <summary>
        /// Gets the shared memory usage per block.
        /// </summary>
        public long SharedMemoryPerBlock { get; } = sharedMemoryPerBlock;

        /// <summary>
        /// Gets any optimization notes from the compiler.
        /// </summary>
        public string[]? OptimizationNotes { get; } = optimizationNotes;

        public override bool Equals(object? obj) => obj is CompilationMetadata other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(CompilationTime);
            hash.Add(CodeSize);
            hash.Add(RegistersPerThread);
            hash.Add(SharedMemoryPerBlock);
            if (OptimizationNotes != null)
            {
                foreach (var note in OptimizationNotes)
                {
                    hash.Add(note);
                }
            }
            return hash.ToHashCode();
        }

        public static bool operator ==(CompilationMetadata left, CompilationMetadata right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CompilationMetadata left, CompilationMetadata right)
        {
            return !(left == right);
        }

        public bool Equals(CompilationMetadata other)
        {
            return CompilationTime.Equals(other.CompilationTime) &&
                   CodeSize == other.CodeSize &&
                   RegistersPerThread == other.RegistersPerThread &&
                   SharedMemoryPerBlock == other.SharedMemoryPerBlock &&
                   ((OptimizationNotes == null && other.OptimizationNotes == null) ||
                    (OptimizationNotes != null && other.OptimizationNotes != null &&
                     OptimizationNotes.SequenceEqual(other.OptimizationNotes)));
        }
    }
}
