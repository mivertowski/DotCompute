// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Options for query compilation.
/// </summary>
public class CompilationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable optimizations.
    /// </summary>
    public bool EnableOptimizations { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; set; } = 256;

    /// <summary>
    /// Gets or sets a value indicating whether to generate debug information.
    /// </summary>
    public bool GenerateDebugInfo { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether to use shared memory.
    /// </summary>
    public bool UseSharedMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to inline functions.
    /// </summary>
    public bool InlineFunctions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use vector types.
    /// </summary>
    public bool UseVectorTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets the precision mode.
    /// </summary>
    public string PrecisionMode { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the target accelerator type.
    /// </summary>
    public string TargetAccelerator { get; set; } = "Auto";

    /// <summary>
    /// Gets or sets a value indicating whether to enable caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache size limit in bytes.
    /// </summary>
    public long CacheSizeLimit { get; set; } = 1024 * 1024 * 100; // 100 MB

    /// <summary>
    /// Gets or sets metadata for the compilation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable operator fusion.
    /// </summary>
    public bool EnableOperatorFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable memory coalescing.
    /// </summary>
    public bool EnableMemoryCoalescing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable parallel execution.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable vectorization.
    /// </summary>
    public bool EnableVectorization { get; set; } = true;
}