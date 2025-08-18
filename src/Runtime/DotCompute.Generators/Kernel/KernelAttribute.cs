// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel
{
    /// <summary>
    /// Marks a method as a compute kernel that will be compiled for multiple backends.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class KernelAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the backends this kernel supports.
        /// Default is CPU only.
        /// </summary>
        public KernelBackends Backends { get; set; } = KernelBackends.CPU;

        /// <summary>
        /// Gets or sets the preferred vector size for SIMD operations.
        /// Default is 8 (256-bit vectors).
        /// </summary>
        public int VectorSize { get; set; } = 8;

        /// <summary>
        /// Gets or sets whether this kernel should use parallel execution.
        /// Default is true.
        /// </summary>
        public bool IsParallel { get; set; } = true;

        /// <summary>
        /// Gets or sets the grid dimensions for GPU execution.
        /// </summary>
        public int[]? GridDimensions { get; set; }

        /// <summary>
        /// Gets or sets the block dimensions for GPU execution.
        /// </summary>
        public int[]? BlockDimensions { get; set; }

        /// <summary>
        /// Gets or sets optimization hints for the compiler.
        /// </summary>
        public OptimizationHints Optimizations { get; set; } = OptimizationHints.None;

        /// <summary>
        /// Gets or sets memory access pattern hints.
        /// </summary>
        public MemoryAccessPattern MemoryPattern { get; set; } = MemoryAccessPattern.Sequential;
    }

    /// <summary>
    /// Backends supported by a kernel.
    /// </summary>
    [Flags]
    public enum KernelBackends
    {
        /// <summary>
        /// CPU backend with SIMD support.
        /// </summary>
        CPU = 1,

        /// <summary>
        /// NVIDIA CUDA backend.
        /// </summary>
        CUDA = 2,

        /// <summary>
        /// Apple Metal backend.
        /// </summary>
        Metal = 4,

        /// <summary>
        /// OpenCL backend.
        /// </summary>
        OpenCL = 8,

        /// <summary>
        /// All available backends.
        /// </summary>
        All = CPU | CUDA | Metal | OpenCL
    }

    /// <summary>
    /// Optimization hints for kernel compilation.
    /// </summary>
    [Flags]
    public enum OptimizationHints
    {
        /// <summary>
        /// No optimizations.
        /// </summary>
        None = 0,

        /// <summary>
        /// Aggressive inlining.
        /// </summary>
        AggressiveInlining = 1,

        /// <summary>
        /// Loop unrolling.
        /// </summary>
        LoopUnrolling = 2,

        /// <summary>
        /// Vectorization hints.
        /// </summary>
        Vectorize = 4,

        /// <summary>
        /// Prefetching hints.
        /// </summary>
        Prefetch = 8,

        /// <summary>
        /// Fast math operations (may reduce accuracy).
        /// </summary>
        FastMath = 16,

        /// <summary>
        /// All optimizations.
        /// </summary>
        All = AggressiveInlining | LoopUnrolling | Vectorize | Prefetch | FastMath
    }

    /// <summary>
    /// Memory access pattern hints.
    /// </summary>
    public enum MemoryAccessPattern
    {
        /// <summary>
        /// Sequential memory access.
        /// </summary>
        Sequential,

        /// <summary>
        /// Strided memory access.
        /// </summary>
        Strided,

        /// <summary>
        /// Random memory access.
        /// </summary>
        Random,

        /// <summary>
        /// Coalesced memory access (GPU).
        /// </summary>
        Coalesced,

        /// <summary>
        /// Tiled memory access.
        /// </summary>
        Tiled
    }
}
