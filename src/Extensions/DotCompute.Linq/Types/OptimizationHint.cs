// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
namespace DotCompute.Linq.Types
{
    /// <summary>
    /// Represents optimization hints for kernel compilation and execution.
    /// </summary>
    /// <remarks>
    /// Optimization hints guide the compiler and runtime to make informed decisions
    /// about code generation, memory layout, and execution strategies to achieve
    /// optimal performance for specific workload patterns.
    /// </remarks>
    public sealed record OptimizationHint
    {
        /// <summary>
        /// Gets the type of optimization hint.
        /// </summary>
        public required OptimizationHintType Type { get; init; }
        /// <summary>
        /// Gets the priority level of this hint.
        /// </summary>
        public OptimizationPriority Priority { get; init; } = OptimizationPriority.Medium;

        /// <summary>
        /// Gets the hint value or parameter.
        /// </summary>
        public object? Value { get; init; }

        /// <summary>
        /// Gets additional metadata associated with this hint.
        /// </summary>
        public ImmutableDictionary<string, object> Metadata { get; init; } =
            ImmutableDictionary<string, object>.Empty;

        /// <summary>
        /// Gets the scope where this hint applies.
        /// </summary>
        public OptimizationScope Scope { get; init; } = OptimizationScope.Kernel;

        /// <summary>
        /// Gets a description of what this hint optimizes.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the estimated performance benefit of applying this hint.
        /// </summary>
        public double EstimatedBenefit { get; init; } = 1.0;

        /// <summary>
        /// Creates a vectorization optimization hint.
        /// </summary>
        /// <param name="vectorWidth">The preferred vector width.</param>
        /// <returns>A vectorization optimization hint.</returns>
        public static OptimizationHint Vectorize(int vectorWidth) => new()
        {
            Type = OptimizationHintType.Vectorization,
            Value = vectorWidth,
            Priority = OptimizationPriority.High,
            Description = $"Vectorize with width {vectorWidth}"
        };
        /// <summary>
        /// Creates a memory coalescing optimization hint.
        /// </summary>
        /// <param name="accessPattern">The memory access pattern.</param>
        /// <returns>A memory coalescing optimization hint.</returns>
        public static OptimizationHint CoalesceMemory(string accessPattern) => new()
        {
            Type = OptimizationHintType.MemoryCoalescing,
            Value = accessPattern,
            Description = $"Coalesce memory accesses for {accessPattern} pattern"
        };
        /// <summary>
        /// Creates a workgroup size optimization hint.
        /// </summary>
        /// <param name="size">The optimal workgroup size.</param>
        /// <returns>A workgroup size optimization hint.</returns>
        public static OptimizationHint WorkgroupSize(int size) => new()
        {
            Type = OptimizationHintType.WorkgroupSize,
            Value = size,
            Priority = OptimizationPriority.Medium,
            Description = $"Use workgroup size of {size}"
        };
        /// <summary>
        /// Creates a loop unrolling optimization hint.
        /// </summary>
        /// <param name="factor">The unroll factor.</param>
        /// <returns>A loop unrolling optimization hint.</returns>
        public static OptimizationHint UnrollLoops(int factor) => new()
        {
            Type = OptimizationHintType.LoopUnrolling,
            Value = factor,
            Description = $"Unroll loops with factor {factor}"
        };
    }

    /// <summary>
    /// Defines the types of optimization hints available.
    /// </summary>
    public enum OptimizationHintType
    {
        /// <summary>Vectorization optimization for SIMD instructions.</summary>
        Vectorization,
        /// <summary>Memory access pattern optimization.</summary>
        MemoryCoalescing,
        /// <summary>Loop unrolling optimization.</summary>
        LoopUnrolling,
        /// <summary>Workgroup or thread block size optimization.</summary>
        WorkgroupSize,
        /// <summary>Cache optimization hints.</summary>
        CacheOptimization,
        /// <summary>Instruction scheduling optimization.</summary>
        InstructionScheduling,
        /// <summary>Register allocation optimization.</summary>
        RegisterAllocation,
        /// <summary>Shared memory optimization.</summary>
        SharedMemoryOptimization,
        /// <summary>Branch prediction optimization.</summary>
        BranchPrediction,
        /// <summary>Data prefetching optimization.</summary>
        DataPrefetch,
        /// <summary>Custom backend-specific optimization.</summary>
        Custom,
        /// <summary>Parallelization optimization hint.</summary>
        Parallelization,
        /// <summary>Memory layout optimization.</summary>
        MemoryLayout,
        /// <summary>GPU execution optimization.</summary>
        GpuExecution,
        /// <summary>General performance optimization hint.</summary>
        Performance
    }

    /// <summary>
    /// Defines the priority levels for optimization hints.
    /// </summary>
    public enum OptimizationPriority
    {
        /// <summary>Low priority hint, may be ignored under resource constraints.</summary>
        Low,
        /// <summary>Medium priority hint, applied when possible.</summary>
        Medium,
        /// <summary>High priority hint, should be applied if feasible.</summary>
        High,
        /// <summary>Critical hint, must be applied for correctness.</summary>
        Critical
    }
    /// <summary>
    /// Defines the scope where optimization hints apply.
    /// </summary>
    public enum OptimizationScope
    {
        /// <summary>Applies to the entire kernel.</summary>
        Kernel,
        /// <summary>Applies to a specific function or method.</summary>
        Function,
        /// <summary>Applies to a loop or iteration construct.</summary>
        Loop,
        /// <summary>Applies to a basic block of instructions.</summary>
        Block,
        /// <summary>Applies to memory operations.</summary>
        Memory,
        /// <summary>Applies to the entire compilation unit.</summary>
        Global
    }
}
