// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation.Plans;
/// <summary>
/// Represents an executable compute plan generated from a LINQ expression.
/// </summary>
/// <remarks>
/// A compute plan is the compiled representation of a LINQ query that can be
/// executed on a GPU. It consists of one or more stages, each representing
/// a kernel execution with specific input/output buffers and configuration.
/// </remarks>
public interface IComputePlan
{
    /// <summary>
    /// Gets the unique identifier for this compute plan.
    /// </summary>
    /// <value>
    /// A unique GUID that identifies this specific compute plan instance.
    /// </value>
    public Guid Id { get; }
    /// Gets the stages in the compute plan.
    /// An ordered list of compute stages that will be executed sequentially
    /// or in parallel as determined by their dependencies.
    public IReadOnlyList<IComputeStage> Stages { get; }
    /// Gets the input parameters required by the plan.
    /// A dictionary mapping parameter names to their expected types.
    /// <remarks>
    /// These parameters must be provided when executing the compute plan
    /// and typically correspond to constants and external data from the original expression.
    /// </remarks>
    public IReadOnlyDictionary<string, Type> InputParameters { get; }
    /// Gets the output type of the compute plan.
    /// The type of data that will be produced by executing this compute plan.
    public Type OutputType { get; }
    /// Gets the estimated memory requirements in bytes.
    /// An estimate of the total memory (device and host) required to execute this plan.
    /// This estimate includes input buffers, output buffers, intermediate storage,
    /// and any temporary memory needed during execution.
    public long EstimatedMemoryUsage { get; }
    /// Gets metadata about the compute plan.
    /// A dictionary containing additional information about the plan,
    /// such as optimization settings, performance hints, and debug information.
    public IReadOnlyDictionary<string, object> Metadata { get; }
}
