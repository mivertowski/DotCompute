// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Linq.Compilation.Execution;
using DotCompute.Linq.Operators.Execution;
using DotCompute.Linq.Operators.Interfaces;
namespace DotCompute.Linq.Compilation.Plans;
/// <summary>
/// Represents a stage in a compute plan.
/// </summary>
/// <remarks>
/// A compute stage represents a single kernel execution within a larger compute plan.
/// Each stage has specific input buffers, produces output in a designated buffer,
/// and has execution configuration parameters.
/// </remarks>
public interface IComputeStage
{
    /// <summary>
    /// Gets the stage identifier.
    /// </summary>
    /// <value>
    /// A unique string identifier for this stage within the compute plan.
    /// </value>
    public string Id { get; }
    /// Gets the executable kernel for this stage.
    /// The executable kernel that will be executed during this stage.
    /// <remarks>
    /// This kernel must implement ExecuteAsync functionality, typically through
    /// implementations like DynamicCompiledKernel that wrap ICompiledKernel.
    /// </remarks>
    public DotCompute.Linq.Operators.Interfaces.IKernel Kernel { get; }
    /// Gets the input buffers for this stage.
    /// A list of buffer identifiers that contain the input data for this stage.
    /// These buffer identifiers correspond to either the original input parameters
    /// or the output buffers of previous stages in the compute plan.
    public IReadOnlyList<string> InputBuffers { get; }
    /// Gets the output buffer for this stage.
    /// The identifier of the buffer where this stage will write its results.
    public string OutputBuffer { get; }
    /// Gets the execution configuration for this stage.
    /// Configuration parameters that control how the kernel is launched and executed.
    public ExecutionConfiguration Configuration { get; }
}
