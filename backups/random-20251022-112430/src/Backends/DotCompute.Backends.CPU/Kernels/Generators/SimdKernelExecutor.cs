// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels.Generators;

/// <summary>
/// Base class for SIMD kernel executors that provides common functionality
/// for all instruction set implementations.
/// </summary>
internal abstract class SimdKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan)
{
    protected readonly KernelDefinition Definition = definition;
    protected readonly KernelExecutionPlan ExecutionPlan = executionPlan;

    /// <summary>
    /// Executes the kernel with the given buffers using the appropriate SIMD implementation.
    /// </summary>
    /// <param name="input1">First input buffer.</param>
    /// <param name="input2">Second input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="elementCount">Number of elements to process.</param>
    /// <param name="vectorWidth">Vector width in bits.</param>
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
    /// <returns>The element type for the operation.</returns>
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