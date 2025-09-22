// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute.Enums;
using DotCompute.Linq.Operators.Types;
using System.Collections.Immutable;
namespace DotCompute.Linq.Types
{
    /// <summary>
    /// Represents a compiled kernel package containing all necessary components for execution.
    /// </summary>
    /// <remarks>
    /// This package encapsulates the compiled kernel along with metadata, optimization hints,
    /// and backend-specific information required for efficient execution across different
    /// compute backends.
    /// </remarks>
    public sealed record CompiledKernelPackage
    {
        /// <summary>
        /// Gets the compiled kernel instance ready for execution.
        /// </summary>
        public required ICompiledKernel CompiledKernel { get; init; }
        /// Gets the target backend type for this compiled kernel.
        public required ComputeBackendType Backend { get; init; }
        /// Gets the kernel definition metadata.
        public required KernelDefinition Definition { get; init; }
        /// Gets the optimization hints applied during compilation.
        public ImmutableArray<OptimizationHint> OptimizationHints { get; init; } = ImmutableArray<OptimizationHint>.Empty;
        /// Gets the compilation options used for this kernel.
        public CompilationOptions? CompilationOptions { get; init; }
        /// Gets performance characteristics for this compiled kernel.
        public KernelPerformanceProfile? PerformanceProfile { get; init; }
        /// Gets memory requirements for kernel execution.
        public KernelMemoryRequirements? MemoryRequirements { get; init; }
        /// Gets a value indicating whether this kernel package is valid and ready for execution.
        public bool IsValid => CompiledKernel != null && Definition != null;
        /// Gets the estimated execution cost for this kernel.
        public double EstimatedCost { get; init; }
        /// Gets the compilation timestamp.
        public DateTimeOffset CompiledAt { get; init; } = DateTimeOffset.UtcNow;
    }
    /// Represents performance characteristics of a compiled kernel.
    public sealed record KernelPerformanceProfile
    {
        /// Gets the estimated compute intensity (operations per memory access).
        public double ComputeIntensity { get; init; }
        /// Gets the estimated memory bandwidth utilization.
        public double MemoryBandwidthUtilization { get; init; }
        /// Gets the optimal workgroup size for this kernel.
        public int OptimalWorkgroupSize { get; init; }
        /// Gets the register usage per thread.
        public int RegistersPerThread { get; init; }
        /// Gets the shared memory usage in bytes.
        public int SharedMemoryUsage { get; init; }
    }

    /// Represents memory requirements for kernel execution.
    public sealed record KernelMemoryRequirements
    {
        /// Gets the minimum global memory required in bytes.
        public long MinimumGlobalMemory { get; init; }
        /// Gets the shared memory required per workgroup in bytes.
        public int SharedMemoryPerWorkgroup { get; init; }
        /// Gets the constant memory usage in bytes.
        public int ConstantMemoryUsage { get; init; }
        /// Gets the memory alignment requirements in bytes.
        public int MemoryAlignment { get; init; } = 16;
        /// Gets a value indicating whether peer-to-peer memory access is required.
        public bool RequiresP2PAccess { get; init; }
    }
}
