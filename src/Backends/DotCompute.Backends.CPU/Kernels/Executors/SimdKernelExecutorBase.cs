// <copyright file="SimdKernelExecutorBase.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Types;

namespace DotCompute.Backends.CPU.Kernels.Executors;

/// <summary>
/// Base class for SIMD kernel executors.
/// Provides common functionality for hardware-specific SIMD implementations.
/// </summary>
internal abstract class SimdKernelExecutorBase
{
    /// <summary>
    /// Gets the kernel definition containing metadata and configuration.
    /// </summary>
    protected KernelDefinition Definition { get; }

    /// <summary>
    /// Gets the execution plan specifying vectorization strategy.
    /// </summary>
    protected KernelExecutionPlan ExecutionPlan { get; }

    /// <summary>
    /// Initializes a new instance of the SimdKernelExecutorBase class.
    /// </summary>
    /// <param name="definition">The kernel definition.</param>
    /// <param name="executionPlan">The execution plan for the kernel.</param>
    protected SimdKernelExecutorBase(KernelDefinition definition, KernelExecutionPlan executionPlan)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ExecutionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
    }

    /// <summary>
    /// Executes the kernel with the given input and output buffers.
    /// </summary>
    /// <param name="input1">The first input buffer.</param>
    /// <param name="input2">The second input buffer.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="elementCount">The number of elements to process.</param>
    /// <param name="vectorWidth">The SIMD vector width in bits.</param>
    public abstract void Execute(
        ReadOnlySpan<byte> input1,
        ReadOnlySpan<byte> input2,
        Span<byte> output,
        long elementCount,
        int vectorWidth);

    /// <summary>
    /// Gets the operation type from kernel metadata.
    /// </summary>
    /// <returns>The kernel operation type.</returns>
    protected KernelOperation GetOperationType()
    {
        if (Definition.Metadata?.TryGetValue("Operation", out var op) == true && op is string opStr)
        {
            return Enum.Parse<KernelOperation>(opStr, ignoreCase: true);
        }

        // Default to Add if not specified
        return KernelOperation.Add;
    }

    /// <summary>
    /// Gets the element type from kernel metadata.
    /// </summary>
    /// <returns>The data type of kernel elements.</returns>
    protected Type GetElementType()
    {
        // Extract element type from metadata
        if (Definition.Metadata?.TryGetValue("ElementType", out var typeObj) == true && typeObj is Type type)
        {
            return type;
        }
        return typeof(float); // Default to float
    }
}