using System;
using System.Collections.Generic;

namespace DotCompute.Core;

/// <summary>
/// Defines a compute kernel.
/// </summary>
public sealed class KernelDefinition
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the kernel source code or bytecode.
    /// </summary>
    public required KernelSource Source { get; init; }

    /// <summary>
    /// Gets the kernel parameters.
    /// </summary>
    public required IReadOnlyList<KernelParameter> Parameters { get; init; }

    /// <summary>
    /// Gets the work dimensions (1, 2, or 3).
    /// </summary>
    public required int WorkDimensions { get; init; }

    /// <summary>
    /// Gets kernel metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents kernel source code or bytecode.
/// </summary>
public abstract class KernelSource
{
    /// <summary>
    /// Gets the source type.
    /// </summary>
    public abstract KernelSourceType Type { get; }
}

/// <summary>
/// Text-based kernel source.
/// </summary>
public sealed class TextKernelSource : KernelSource
{
    /// <summary>
    /// Gets the source code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the language.
    /// </summary>
    public required string Language { get; init; }

    public override KernelSourceType Type => KernelSourceType.Text;
}

/// <summary>
/// Bytecode-based kernel source.
/// </summary>
public sealed class BytecodeKernelSource : KernelSource
{
    /// <summary>
    /// Gets the bytecode.
    /// </summary>
    public required ReadOnlyMemory<byte> Bytecode { get; init; }

    /// <summary>
    /// Gets the bytecode format.
    /// </summary>
    public required string Format { get; init; }

    public override KernelSourceType Type => KernelSourceType.Bytecode;
}

/// <summary>
/// Kernel source type.
/// </summary>
public enum KernelSourceType
{
    /// <summary>
    /// Text-based source code.
    /// </summary>
    Text,

    /// <summary>
    /// Compiled bytecode.
    /// </summary>
    Bytecode
}

/// <summary>
/// Defines a kernel parameter.
/// </summary>
public sealed class KernelParameter
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public required KernelParameterType Type { get; init; }

    /// <summary>
    /// Gets the element type for buffer parameters.
    /// </summary>
    public Type? ElementType { get; init; }

    /// <summary>
    /// Gets whether this is a constant parameter.
    /// </summary>
    public bool IsConstant { get; init; }

    /// <summary>
    /// Gets the access mode for buffer parameters.
    /// </summary>
    public MemoryAccess Access { get; init; } = MemoryAccess.ReadWrite;
}

/// <summary>
/// Kernel parameter type.
/// </summary>
public enum KernelParameterType
{
    /// <summary>
    /// Memory buffer parameter.
    /// </summary>
    Buffer,

    /// <summary>
    /// Scalar value parameter.
    /// </summary>
    Scalar,

    /// <summary>
    /// Local memory allocation size.
    /// </summary>
    LocalMemory,

    /// <summary>
    /// Sampler parameter (for image processing).
    /// </summary>
    Sampler
}

/// <summary>
/// Memory access mode.
/// </summary>
public enum MemoryAccess
{
    /// <summary>
    /// Read-only access.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Write-only access.
    /// </summary>
    WriteOnly,

    /// <summary>
    /// Read-write access.
    /// </summary>
    ReadWrite
}

/// <summary>
/// Compilation options for kernels.
/// </summary>
public sealed class CompilationOptions
{
    /// <summary>
    /// Gets or sets optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Release;

    /// <summary>
    /// Gets or sets whether to enable fast math.
    /// </summary>
    public bool EnableFastMath { get; set; }

    /// <summary>
    /// Gets or sets additional compiler flags.
    /// </summary>
    public IReadOnlyList<string>? AdditionalFlags { get; set; }

    /// <summary>
    /// Gets or sets preprocessor definitions.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Defines { get; set; }

    /// <summary>
    /// Gets the default compilation options.
    /// </summary>
    public static CompilationOptions Default { get; } = new();
}

/// <summary>
/// Optimization level for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization.
    /// </summary>
    None,

    /// <summary>
    /// Debug build with minimal optimization.
    /// </summary>
    Debug,

    /// <summary>
    /// Release build with standard optimization.
    /// </summary>
    Release,

    /// <summary>
    /// Maximum optimization.
    /// </summary>
    Maximum
}

/// <summary>
/// Represents a compiled kernel ready for execution.
/// </summary>
public interface ICompiledKernel : IAsyncDisposable
{
    /// <summary>
    /// Gets the kernel definition.
    /// </summary>
    public KernelDefinition Definition { get; }

    /// <summary>
    /// Executes the kernel.
    /// </summary>
    public ValueTask ExecuteAsync(
        KernelExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for kernel execution.
/// </summary>
public sealed class KernelExecutionContext
{
    /// <summary>
    /// Gets or sets the global work size.
    /// </summary>
    public required IReadOnlyList<long> GlobalWorkSize { get; init; }

    /// <summary>
    /// Gets or sets the local work size (optional).
    /// </summary>
    public IReadOnlyList<long>? LocalWorkSize { get; init; }

    /// <summary>
    /// Gets or sets the kernel arguments.
    /// </summary>
    public required IReadOnlyList<object> Arguments { get; init; }

    /// <summary>
    /// Gets or sets execution flags.
    /// </summary>
    public KernelExecutionOption Options { get; init; }
}

/// <summary>
/// Kernel execution flags.
/// </summary>
[Flags]
public enum KernelExecutionOption
{
    /// <summary>
    /// No special flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Measure execution time.
    /// </summary>
    MeasureTime = 1,

    /// <summary>
    /// Enable profiling.
    /// </summary>
    EnableProfiling = 2,

    /// <summary>
    /// Force synchronous execution.
    /// </summary>
    Synchronous = 4
}