// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation.Options;
{
/// <summary>
/// Represents compilation options for query expressions.
/// </summary>
/// <remarks>
/// This class provides configuration options that control how LINQ expressions
/// are compiled into GPU kernels, including optimization settings and execution parameters.
/// </remarks>
public class CompilationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable operator fusion optimization.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable operator fusion; otherwise, <c>false</c>.
    /// Default value is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Operator fusion combines multiple operations into a single kernel to reduce
    /// memory bandwidth requirements and improve performance.
    /// </remarks>
    public bool EnableOperatorFusion { get; set; } = true;
    /// Gets or sets a value indicating whether to enable memory coalescing.
    /// <c>true</c> to enable memory coalescing; otherwise, <c>false</c>.
    /// Memory coalescing optimizes memory access patterns to improve
    /// memory bandwidth utilization on the GPU.
    public bool EnableMemoryCoalescing { get; set; } = true;
    /// Gets or sets a value indicating whether to enable parallel execution.
    /// <c>true</c> to enable parallel execution; otherwise, <c>false</c>.
    /// When enabled, operations that can be parallelized will be executed
    /// using multiple threads on the GPU.
    public bool EnableParallelExecution { get; set; } = true;
    /// Gets or sets the maximum number of threads per block.
    /// The maximum number of threads per block. Default value is 256.
    /// This setting controls the GPU kernel launch configuration and affects
    /// occupancy and resource utilization. The optimal value depends on the
    /// specific GPU architecture and kernel characteristics.
    public int MaxThreadsPerBlock { get; set; } = 256;
    /// Gets or sets a value indicating whether to generate debug information.
    /// <c>true</c> to generate debug information; otherwise, <c>false</c>.
    /// Default value is <c>false</c>.
    /// Debug information includes additional metadata and instrumentation
    /// that can help with debugging and profiling, but may impact performance.
    public bool GenerateDebugInfo { get; set; }
}
