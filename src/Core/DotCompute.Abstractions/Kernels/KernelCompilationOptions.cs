using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Configuration options for kernel compilation
/// </summary>
public sealed class KernelCompilationOptions
{
    /// <summary>
    /// Gets or sets the optimization level for compilation
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets whether to generate debug information
    /// </summary>
    public bool GenerateDebugInfo { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable fast math optimizations
    /// </summary>
    public bool EnableFastMath { get; set; } = true;

    /// <summary>
    /// Gets or sets the floating point mode
    /// </summary>
    public FloatingPointMode FloatingPointMode { get; set; } = FloatingPointMode.Default;

    /// <summary>
    /// Gets or sets the target architecture for compilation
    /// </summary>
    public string? TargetArchitecture { get; set; }

    /// <summary>
    /// Gets or sets additional compiler flags
    /// </summary>
    public List<string> CompilerFlags { get; set; } = [];

    /// <summary>
    /// Gets or sets preprocessor definitions
    /// </summary>
    public Dictionary<string, string> Definitions { get; set; } = [];

    /// <summary>
    /// Gets or sets include directories for compilation
    /// </summary>
    public List<string> IncludeDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to enable kernel caching
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache timeout in seconds
    /// </summary>
    public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets whether to use PTX intermediate representation (CUDA only)
    /// </summary>
    public bool UsePtx { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to generate relocatable device code
    /// </summary>
    public bool GenerateRelocatableCode { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum register count per thread
    /// </summary>
    public int? MaxRegisterCount { get; set; }

    /// <summary>
    /// Gets or sets whether to enable line-info generation
    /// </summary>
    public bool GenerateLineInfo { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable verbose compilation output
    /// </summary>
    public bool VerboseOutput { get; set; } = false;

    /// <summary>
    /// Gets or sets the preferred cache configuration
    /// </summary>
    public CacheConfig PreferredCacheConfig { get; set; } = CacheConfig.PreferNone;

    /// <summary>
    /// Gets or sets custom optimization hints
    /// </summary>
    public OptimizationHint OptimizationHints { get; set; } = OptimizationHint.None;

    /// <summary>
    /// Gets or sets whether to enable auto-vectorization
    /// </summary>
    public bool EnableAutoVectorization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable loop unrolling
    /// </summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>
    /// Gets or sets the warning level (0-4)
    /// </summary>
    public int WarningLevel { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether to treat warnings as errors
    /// </summary>
    public bool WarningsAsErrors { get; set; } = false;

    /// <summary>
    /// Creates a new instance of KernelCompilationOptions with default values
    /// </summary>
    public KernelCompilationOptions() { }

    /// <summary>
    /// Creates a copy of the current KernelCompilationOptions
    /// </summary>
    /// <returns>A new KernelCompilationOptions instance with the same values</returns>
    public KernelCompilationOptions Clone()
    {
        return new KernelCompilationOptions
        {
            OptimizationLevel = OptimizationLevel,
            GenerateDebugInfo = GenerateDebugInfo,
            EnableFastMath = EnableFastMath,
            FloatingPointMode = FloatingPointMode,
            TargetArchitecture = TargetArchitecture,
            CompilerFlags = [.. CompilerFlags],
            Definitions = new Dictionary<string, string>(Definitions),
            IncludeDirectories = [.. IncludeDirectories],
            EnableCaching = EnableCaching,
            CacheTimeout = CacheTimeout,
            UsePtx = UsePtx,
            GenerateRelocatableCode = GenerateRelocatableCode,
            MaxRegisterCount = MaxRegisterCount,
            GenerateLineInfo = GenerateLineInfo,
            VerboseOutput = VerboseOutput,
            PreferredCacheConfig = PreferredCacheConfig,
            OptimizationHints = OptimizationHints,
            EnableAutoVectorization = EnableAutoVectorization,
            EnableLoopUnrolling = EnableLoopUnrolling,
            WarningLevel = WarningLevel,
            WarningsAsErrors = WarningsAsErrors
        };
    }

    /// <summary>
    /// Creates compilation options optimized for debug builds
    /// </summary>
    /// <returns>A new KernelCompilationOptions instance configured for debugging</returns>
    public static KernelCompilationOptions Debug()
    {
        return new KernelCompilationOptions
        {
            OptimizationLevel = OptimizationLevel.None,
            GenerateDebugInfo = true,
            GenerateLineInfo = true,
            EnableFastMath = false,
            VerboseOutput = true,
            WarningLevel = 4,
            EnableCaching = false
        };
    }

    /// <summary>
    /// Creates compilation options optimized for release builds
    /// </summary>
    /// <returns>A new KernelCompilationOptions instance configured for release</returns>
    public static KernelCompilationOptions Release()
    {
        return new KernelCompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            GenerateDebugInfo = false,
            EnableFastMath = true,
            EnableAutoVectorization = true,
            EnableLoopUnrolling = true,
            EnableCaching = true,
            WarningsAsErrors = true
        };
    }

    /// <summary>
    /// Creates compilation options balanced between performance and compile time
    /// </summary>
    /// <returns>A new KernelCompilationOptions instance with balanced settings</returns>
    public static KernelCompilationOptions Balanced()
    {
        return new KernelCompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            GenerateDebugInfo = false,
            EnableFastMath = true,
            EnableCaching = true
        };
    }

    /// <summary>
    /// Adds a compiler flag
    /// </summary>
    /// <param name="flag">The compiler flag to add</param>
    public void AddFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag) && !CompilerFlags.Contains(flag))
        {
            CompilerFlags.Add(flag);
        }
    }

    /// <summary>
    /// Adds a preprocessor definition
    /// </summary>
    /// <param name="name">The definition name</param>
    /// <param name="value">The definition value (optional)</param>
    public void AddDefinition(string name, string value = "")
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Definitions[name] = value;
        }
    }

    /// <summary>
    /// Adds an include directory
    /// </summary>
    /// <param name="directory">The include directory path</param>
    public void AddIncludeDirectory(string directory)
    {
        if (!string.IsNullOrWhiteSpace(directory) && !IncludeDirectories.Contains(directory))
        {
            IncludeDirectories.Add(directory);
        }
    }
}