// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Linq.Compilation.Execution;
using DotCompute.Linq.Operators.Interfaces;
using DotCompute.Linq.Operators.Execution;
namespace DotCompute.Linq.Compilation.Plans;
{
/// <summary>
/// Implementation of a compute stage.
/// </summary>
/// <remarks>
/// This class provides a concrete implementation of <see cref="IComputeStage"/>
/// that represents a single kernel execution within a compute plan.
/// </remarks>
internal class ComputeStage : IComputeStage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComputeStage"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for this stage.</param>
    /// <param name="kernel">The kernel to execute for this stage.</param>
    /// <param name="inputBuffers">The input buffers for this stage.</param>
    /// <param name="outputBuffer">The output buffer for this stage.</param>
    /// <param name="configuration">The execution configuration for this stage.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the parameters is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> or <paramref name="outputBuffer"/> is empty.
    public ComputeStage(
        {
        string id,
        DotCompute.Linq.Operators.Interfaces.IKernel kernel,
        IReadOnlyList<string> inputBuffers,
        string outputBuffer,
        ExecutionConfiguration configuration)
    {
        Id = !string.IsNullOrEmpty(id) ? id : throw new ArgumentException("Stage ID cannot be null or empty.", nameof(id));
        Kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        InputBuffers = inputBuffers ?? throw new ArgumentNullException(nameof(inputBuffers));
        OutputBuffer = !string.IsNullOrEmpty(outputBuffer) ? outputBuffer : throw new ArgumentException("Output buffer cannot be null or empty.", nameof(outputBuffer));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    /// <inheritdoc />
    public string Id { get; }
    public DotCompute.Linq.Operators.Interfaces.IKernel Kernel { get; }
    public IReadOnlyList<string> InputBuffers { get; }
    public string OutputBuffer { get; }
    public ExecutionConfiguration Configuration { get; }
}
