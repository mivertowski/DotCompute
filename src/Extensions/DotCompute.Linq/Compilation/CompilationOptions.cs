// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation;
/// <summary>
/// Options for query compilation.
/// </summary>
public class CompilationOptions
{
    /// <summary>
    /// Gets the default compilation options.
    /// </summary>
    public static CompilationOptions Default { get; } = new();
    /// Gets or sets a value indicating whether to enable optimizations.
    public bool EnableOptimizations { get; set; } = true;
    /// Gets or sets the maximum threads per block.
    public int MaxThreadsPerBlock { get; set; } = 256;
    /// Gets or sets a value indicating whether to generate debug information.
    public bool GenerateDebugInfo { get; set; }
    /// Gets or sets a value indicating whether to use shared memory.
    public bool UseSharedMemory { get; set; } = true;
    /// Gets or sets a value indicating whether to inline functions.
    public bool InlineFunctions { get; set; } = true;
    /// Gets or sets a value indicating whether to use vector types.
    public bool UseVectorTypes { get; set; } = true;
    /// Gets or sets the precision mode.
    public string PrecisionMode { get; set; } = "Default";
    /// Gets or sets the target accelerator type.
    public string TargetAccelerator { get; set; } = "Auto";
    /// Gets or sets a value indicating whether to enable caching.
    public bool EnableCaching { get; set; } = true;
    /// Gets or sets the cache size limit in bytes.
    public long CacheSizeLimit { get; set; } = 1024 * 1024 * 100; // 100 MB
    /// Gets or sets metadata for the compilation.
    public Dictionary<string, object> Metadata { get; set; } = [];
    /// Gets or sets a value indicating whether to enable operator fusion.
    public bool EnableOperatorFusion { get; set; } = true;
    /// Gets or sets a value indicating whether to enable memory coalescing.
    public bool EnableMemoryCoalescing { get; set; } = true;
    /// Gets or sets a value indicating whether to enable parallel execution.
    public bool EnableParallelExecution { get; set; } = true;
    /// Gets or sets a value indicating whether to enable vectorization.
    public bool EnableVectorization { get; set; } = true;
}
