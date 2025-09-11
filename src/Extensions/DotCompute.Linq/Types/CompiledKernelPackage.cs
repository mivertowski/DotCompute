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

        /// <summary>
        /// Gets the target backend type for this compiled kernel.
        /// </summary>
        public required ComputeBackendType Backend { get; init; }

        /// <summary>
        /// Gets the kernel definition metadata.
        /// </summary>
        public required KernelDefinition Definition { get; init; }

        /// <summary>
        /// Gets the optimization hints applied during compilation.
        /// </summary>
        public ImmutableArray<OptimizationHint> OptimizationHints { get; init; } = ImmutableArray<OptimizationHint>.Empty;

        /// <summary>
        /// Gets the compilation options used for this kernel.
        /// </summary>
        public CompilationOptions? CompilationOptions { get; init; }

        /// <summary>
        /// Gets performance characteristics for this compiled kernel.
        /// </summary>
        public KernelPerformanceProfile? PerformanceProfile { get; init; }

        /// <summary>
        /// Gets memory requirements for kernel execution.
        /// </summary>
        public KernelMemoryRequirements? MemoryRequirements { get; init; }

        /// <summary>
        /// Gets a value indicating whether this kernel package is valid and ready for execution.
        /// </summary>
        public bool IsValid => CompiledKernel != null && Definition != null;

        /// <summary>
        /// Gets the estimated execution cost for this kernel.
        /// </summary>
        public double EstimatedCost { get; init; }

        /// <summary>
        /// Gets the compilation timestamp.
        /// </summary>
        public DateTimeOffset CompiledAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Represents performance characteristics of a compiled kernel.
    /// </summary>
    public sealed record KernelPerformanceProfile
    {
        /// <summary>
        /// Gets the estimated compute intensity (operations per memory access).
        /// </summary>
        public double ComputeIntensity { get; init; }

        /// <summary>
        /// Gets the estimated memory bandwidth utilization.
        /// </summary>
        public double MemoryBandwidthUtilization { get; init; }

        /// <summary>
        /// Gets the optimal workgroup size for this kernel.
        /// </summary>
        public int OptimalWorkgroupSize { get; init; }

        /// <summary>
        /// Gets the register usage per thread.
        /// </summary>
        public int RegistersPerThread { get; init; }

        /// <summary>
        /// Gets the shared memory usage in bytes.
        /// </summary>
        public int SharedMemoryUsage { get; init; }
    }

    /// <summary>
    /// Represents memory requirements for kernel execution.
    /// </summary>
    public sealed record KernelMemoryRequirements
    {
        /// <summary>
        /// Gets the minimum global memory required in bytes.
        /// </summary>
        public long MinimumGlobalMemory { get; init; }

        /// <summary>
        /// Gets the shared memory required per workgroup in bytes.
        /// </summary>
        public int SharedMemoryPerWorkgroup { get; init; }

        /// <summary>
        /// Gets the constant memory usage in bytes.
        /// </summary>
        public int ConstantMemoryUsage { get; init; }

        /// <summary>
        /// Gets the memory alignment requirements in bytes.
        /// </summary>
        public int MemoryAlignment { get; init; } = 16;

        /// <summary>
        /// Gets a value indicating whether peer-to-peer memory access is required.
        /// </summary>
        public bool RequiresP2PAccess { get; init; }
    }
}