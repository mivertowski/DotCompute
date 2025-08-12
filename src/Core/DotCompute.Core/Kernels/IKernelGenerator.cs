// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Core.Execution;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Interface for generating GPU kernels from expressions or operations.
/// </summary>
public interface IKernelGenerator
{
    /// <summary>
    /// Gets the supported accelerator type for this generator.
    /// </summary>
    AcceleratorType AcceleratorType { get; }

    /// <summary>
    /// Generates kernel source code from an expression.
    /// </summary>
    /// <param name="expression">The expression to compile.</param>
    /// <param name="context">The kernel generation context.</param>
    /// <returns>The generated kernel.</returns>
    GeneratedKernel GenerateKernel(Expression expression, KernelGenerationContext context);

    /// <summary>
    /// Generates kernel source code for a specific operation.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="inputTypes">The input data types.</param>
    /// <param name="outputType">The output data type.</param>
    /// <param name="context">The kernel generation context.</param>
    /// <returns>The generated kernel.</returns>
    GeneratedKernel GenerateOperationKernel(string operation, Type[] inputTypes, Type outputType, KernelGenerationContext context);

    /// <summary>
    /// Validates if an expression can be compiled to a kernel.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>True if the expression can be compiled, false otherwise.</returns>
    bool CanCompile(Expression expression);

    /// <summary>
    /// Gets optimization hints for the kernel compiler.
    /// </summary>
    /// <param name="context">The kernel generation context.</param>
    /// <returns>Optimization hints.</returns>
    KernelOptimizationHints GetOptimizationHints(KernelGenerationContext context);
}

/// <summary>
/// Context for kernel generation.
/// </summary>
public sealed class KernelGenerationContext
{
    /// <summary>
    /// Gets or sets the target device information.
    /// </summary>
    public required AcceleratorInfo DeviceInfo { get; init; }

    /// <summary>
    /// Gets or sets the work group dimensions.
    /// </summary>
    public int[]? WorkGroupDimensions { get; init; }

    /// <summary>
    /// Gets or sets whether to use shared memory.
    /// </summary>
    public bool UseSharedMemory { get; init; }

    /// <summary>
    /// Gets or sets whether to use vector types.
    /// </summary>
    public bool UseVectorTypes { get; init; }

    /// <summary>
    /// Gets or sets the precision mode.
    /// </summary>
    public PrecisionMode Precision { get; init; } = PrecisionMode.Single;

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents a generated kernel.
/// </summary>
public sealed class GeneratedKernel
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the kernel source code.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public required KernelLanguage Language { get; init; }

    /// <summary>
    /// Gets or sets the kernel parameters.
    /// </summary>
    public required KernelParameter[] Parameters { get; init; }

    /// <summary>
    /// Gets or sets the required work group size.
    /// </summary>
    public int[]? RequiredWorkGroupSize { get; init; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets optimization metadata.
    /// </summary>
    public Dictionary<string, object>? OptimizationMetadata { get; init; }

    // Legacy compatibility properties for tests
    
    /// <summary>
    /// Gets the kernel ID (alias for Name as Guid).
    /// </summary>
    public Guid Id => Name != null ? GenerateIdFromName(Name) : Guid.Empty;
    
    /// <summary>
    /// Gets the source code (alias for Source).
    /// </summary>
    public string SourceCode => Source;
    
    /// <summary>
    /// Gets the entry point name (defaults to Name).
    /// </summary>
    public string EntryPoint => Name;
    
    /// <summary>
    /// Gets the kernel metadata (alias for OptimizationMetadata).
    /// </summary>
    public KernelMetadata? Metadata => OptimizationMetadata != null ? CreateMetadataFromDict(OptimizationMetadata) : null;
    
    private static Guid GenerateIdFromName(string name)
    {
        // Generate a deterministic GUID from the name
        var bytes = System.Text.Encoding.UTF8.GetBytes(name);
        var hash = System.Security.Cryptography.SHA1.Create().ComputeHash(bytes);
        var guid = new byte[16];
        Array.Copy(hash, guid, 16);
        return new Guid(guid);
    }
    
    private static KernelMetadata CreateMetadataFromDict(Dictionary<string, object> dict)
    {
        return new KernelMetadata
        {
            DeviceId = dict.GetValueOrDefault("DeviceId", "unknown")?.ToString() ?? "unknown",
            DeviceType = dict.GetValueOrDefault("DeviceType", "unknown")?.ToString() ?? "unknown",
            KernelName = dict.GetValueOrDefault("KernelName", "kernel")?.ToString() ?? "kernel",
            CachedAt = DateTimeOffset.UtcNow,
            AccessCount = 0,
            LastAccessed = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Represents a kernel parameter.
/// </summary>
public sealed class KernelParameter
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the parameter type.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// Gets or sets whether this is an input parameter.
    /// </summary>
    public bool IsInput { get; init; }

    /// <summary>
    /// Gets or sets whether this is an output parameter.
    /// </summary>
    public bool IsOutput { get; init; }

    /// <summary>
    /// Gets or sets the memory space.
    /// </summary>
    public MemorySpace MemorySpace { get; init; } = MemorySpace.Global;

    /// <summary>
    /// Gets or sets whether this parameter is read-only.
    /// </summary>
    public bool IsReadOnly { get; init; }
}

/// <summary>
/// Kernel optimization hints.
/// </summary>
public sealed class KernelOptimizationHints
{
    /// <summary>
    /// Gets or sets whether to unroll loops.
    /// </summary>
    public bool UnrollLoops { get; init; }

    /// <summary>
    /// Gets or sets the vector width to use.
    /// </summary>
    public int VectorWidth { get; init; } = 1;

    /// <summary>
    /// Gets or sets whether to use fused multiply-add.
    /// </summary>
    public bool UseFMA { get; init; }

    /// <summary>
    /// Gets or sets whether to prefetch data.
    /// </summary>
    public bool PrefetchData { get; init; }

    /// <summary>
    /// Gets or sets the cache configuration.
    /// </summary>
    public CacheConfiguration CacheConfig { get; init; } = CacheConfiguration.Default;

    /// <summary>
    /// Gets or sets additional hints.
    /// </summary>
    public Dictionary<string, object>? AdditionalHints { get; init; }
}

/// <summary>
/// Kernel language.
/// </summary>
public enum KernelLanguage
{
    /// <summary>
    /// C# kernel language
    /// </summary>
    CSharp,

    /// <summary>
    /// OpenCL C
    /// </summary>
    OpenCL,

    /// <summary>
    /// CUDA C++
    /// </summary>
    CUDA,

    /// <summary>
    /// Metal Shading Language
    /// </summary>
    Metal,

    /// <summary>
    /// DirectCompute HLSL
    /// </summary>
    DirectCompute,

    /// <summary>
    /// Vulkan GLSL
    /// </summary>
    Vulkan,

    /// <summary>
    /// WebGPU WGSL
    /// </summary>
    WebGPU
}

/// <summary>
/// Memory space for kernel parameters.
/// </summary>
public enum MemorySpace
{
    /// <summary>
    /// Global memory
    /// </summary>
    Global,

    /// <summary>
    /// Shared/Local memory
    /// </summary>
    Shared,

    /// <summary>
    /// Constant memory
    /// </summary>
    Constant,

    /// <summary>
    /// Private/Register memory
    /// </summary>
    Private
}

/// <summary>
/// Precision mode for computations.
/// </summary>
public enum PrecisionMode
{
    /// <summary>
    /// Half precision (16-bit)
    /// </summary>
    Half,

    /// <summary>
    /// Single precision (32-bit)
    /// </summary>
    Single,

    /// <summary>
    /// Double precision (64-bit)
    /// </summary>
    Double,

    /// <summary>
    /// Mixed precision
    /// </summary>
    Mixed
}

/// <summary>
/// Cache configuration.
/// </summary>
public enum CacheConfiguration
{
    /// <summary>
    /// Default configuration
    /// </summary>
    Default,

    /// <summary>
    /// Prefer L1 cache
    /// </summary>
    PreferL1,

    /// <summary>
    /// Prefer shared memory
    /// </summary>
    PreferShared,

    /// <summary>
    /// Equal preference
    /// </summary>
    Equal
}