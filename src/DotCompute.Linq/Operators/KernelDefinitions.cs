// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Linq.Operators;

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
    public KernelParameter[] Parameters { get; set; } = Array.Empty<KernelParameter>();

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
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
    public int[] GlobalWorkSize { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the local work size.
    /// </summary>
    public int[] LocalWorkSize { get; set; } = Array.Empty<int>();
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

    // TODO: Add required capabilities when AcceleratorCapabilities is defined
}