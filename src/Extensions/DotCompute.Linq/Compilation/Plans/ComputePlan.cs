// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation.Plans;
{
/// <summary>
/// Implementation of a compute plan.
/// </summary>
/// <remarks>
/// This class provides a concrete implementation of <see cref="IComputePlan"/>
/// that represents an executable compute plan generated from a LINQ expression.
/// </remarks>
internal class ComputePlan : IComputePlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComputePlan"/> class.
    /// </summary>
    /// <param name="stages">The execution stages in the compute plan.</param>
    /// <param name="inputParameters">The input parameters required by the plan.</param>
    /// <param name="outputType">The output type of the compute plan.</param>
    /// <param name="estimatedMemoryUsage">The estimated memory requirements in bytes.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stages"/>, <paramref name="inputParameters"/>, 
    /// or <paramref name="outputType"/> is null.
    /// </exception>
    public ComputePlan(
        {
        IReadOnlyList<IComputeStage> stages,
        IReadOnlyDictionary<string, Type> inputParameters,
        Type outputType,
        long estimatedMemoryUsage)
    {
        Id = Guid.NewGuid();
        Stages = stages ?? throw new ArgumentNullException(nameof(stages));
        InputParameters = inputParameters ?? throw new ArgumentNullException(nameof(inputParameters));
        OutputType = outputType ?? throw new ArgumentNullException(nameof(outputType));
        EstimatedMemoryUsage = estimatedMemoryUsage;
        Metadata = new Dictionary<string, object>
        {
            ["CreatedAt"] = DateTime.UtcNow,
            ["Version"] = "1.0"
        };
    }
    /// <inheritdoc />
    public Guid Id { get; }
    public IReadOnlyList<IComputeStage> Stages { get; }
    public IReadOnlyDictionary<string, Type> InputParameters { get; }
    public Type OutputType { get; }
    public long EstimatedMemoryUsage { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
}
