// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators
{

/// <summary>
/// Kernel parameter information with direction.
/// </summary>
public class KernelParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelParameter"/> class.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="direction">The parameter direction.</param>
    public KernelParameter(string name, Type type, ParameterDirection direction)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Direction = direction;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the parameter direction.
    /// </summary>
    public ParameterDirection Direction { get; }
}

/// <summary>
/// Parameter direction for kernel parameters.
/// </summary>
public enum ParameterDirection
{
    /// <summary>
    /// Input parameter.
    /// </summary>
    In,

    /// <summary>
    /// Output parameter.
    /// </summary>
    Out,

    /// <summary>
    /// Input/output parameter.
    /// </summary>
    InOut
}

/// <summary>
/// Represents a kernel generated from expressions with optimization metadata.
/// </summary>
public class GeneratedKernel
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the generated kernel source code.
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public Core.Kernels.KernelLanguage Language { get; set; }
    
    /// <summary>
    /// Gets or sets the kernel parameters.
    /// </summary>
    public GeneratedKernelParameter[] Parameters { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the required work group size.
    /// </summary>
    public int[]? RequiredWorkGroupSize { get; set; }
    
    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }
    
    /// <summary>
    /// Gets or sets optimization metadata from expression analysis.
    /// </summary>
    public Dictionary<string, object>? OptimizationMetadata { get; set; }
    
    /// <summary>
    /// Gets or sets the original expression that generated this kernel.
    /// </summary>
    public Expression? SourceExpression { get; set; }
}

/// <summary>
/// Represents a parameter in a generated kernel.
/// </summary>
public class GeneratedKernelParameter
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the parameter type.
    /// </summary>
    public Type Type { get; set; } = typeof(object);
    
    /// <summary>
    /// Gets or sets a value indicating whether this parameter is an input.
    /// </summary>
    public bool IsInput { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether this parameter is an output.
    /// </summary>
    public bool IsOutput { get; set; }
    
    /// <summary>
    /// Gets or sets the size in bytes if this is a buffer parameter.
    /// </summary>
    public long? SizeInBytes { get; set; }
    
    /// <summary>
    /// Gets or sets additional parameter metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Context for kernel generation with device-specific optimizations.
/// </summary>
public class KernelGenerationContext
{
    /// <summary>
    /// Gets or sets the target device information.
    /// </summary>
    public AcceleratorInfo? DeviceInfo { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to use shared memory optimizations.
    /// </summary>
    public bool UseSharedMemory { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to use vector types.
    /// </summary>
    public bool UseVectorTypes { get; set; }
    
    /// <summary>
    /// Gets or sets the floating-point precision mode.
    /// </summary>
    public PrecisionMode Precision { get; set; } = PrecisionMode.Single;
    
    /// <summary>
    /// Gets or sets the work group dimensions.
    /// </summary>
    public int[] WorkGroupDimensions { get; set; } = [256, 1, 1];
    
    /// <summary>
    /// Gets or sets additional generation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Floating-point precision modes for kernel generation.
/// </summary>
public enum PrecisionMode
{
    /// <summary>
    /// Half precision (16-bit).
    /// </summary>
    Half,
    
    /// <summary>
    /// Single precision (32-bit).
    /// </summary>
    Single,
    
    /// <summary>
    /// Double precision (64-bit).
    /// </summary>
    Double
}

/// <summary>
/// Kernel definition with metadata.
/// </summary>
public class KernelDefinition
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel parameters.
    /// </summary>
    public IList<KernelParameter> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Kernel implementation language.
/// </summary>
public enum KernelLanguage
{
    /// <summary>
    /// C# kernel.
    /// </summary>
    CSharp,

    /// <summary>
    /// CUDA kernel.
    /// </summary>
    CUDA,

    /// <summary>
    /// OpenCL kernel.
    /// </summary>
    OpenCL,

    /// <summary>
    /// Metal kernel.
    /// </summary>
    Metal,

    /// <summary>
    /// SPIR-V kernel.
    /// </summary>
    SPIRV
}

/// <summary>
/// Work items configuration for kernel execution.
/// </summary>
public class WorkItems
{
    /// <summary>
    /// Gets or sets the global work size.
    /// </summary>
    public int[] GlobalWorkSize { get; set; } = [];

    /// <summary>
    /// Gets or sets the local work size.
    /// </summary>
    public int[] LocalWorkSize { get; set; } = [];
}

/// <summary>
/// Runtime kernel that can be executed.
/// </summary>
public interface IKernel : IDisposable
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel for execution.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the compilation operation.</returns>
    Task CompileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the kernel with the given parameters.
    /// </summary>
    /// <param name="workItems">The work items configuration.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the execution.</returns>
    Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the kernel parameters.
    /// </summary>
    /// <returns>The kernel parameter information.</returns>
    IReadOnlyList<KernelParameter> GetParameterInfo();
}


/// <summary>
/// Properties of a compiled kernel.
/// </summary>
public class KernelProperties
{
    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the register count.
    /// </summary>
    public int RegisterCount { get; set; }

    /// <summary>
    /// Gets or sets the required accelerator capabilities.
    /// </summary>
    public HashSet<string> RequiredCapabilities { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the preferred work group size.
    /// </summary>
    public int? PreferredWorkGroupSize { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether this kernel supports atomic operations.
    /// </summary>
    public bool SupportsAtomicOperations { get; set; }
    
    /// <summary>
    /// Gets or sets the minimum compute capability required.
    /// </summary>
    public string? MinimumComputeCapability { get; set; }
}}
