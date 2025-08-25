using System.Collections.Generic;

namespace DotCompute.Backends.CUDA.Compilation.Configuration
{
    /// <summary>
    /// Options for CUDA kernel compilation.
    /// </summary>
    public class CompilationOptions
    {
        /// <summary>
        /// Gets or sets the target architecture (e.g., "sm_70", "sm_80").
        /// </summary>
        public string Architecture { get; set; } = "sm_60";

        /// <summary>
        /// Gets or sets whether to enable debug information.
        /// </summary>
        public bool EnableDebugInfo { get; set; } = false;

        /// <summary>
        /// Gets or sets the optimization level (0-3).
        /// </summary>
        public int OptimizationLevel { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to use fast math operations.
        /// </summary>
        public bool UseFastMath { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to generate position-independent code.
        /// </summary>
        public bool GeneratePositionIndependentCode { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum register count per thread.
        /// </summary>
        public int? MaxRegistersPerThread { get; set; }

        /// <summary>
        /// Gets or sets additional compiler flags.
        /// </summary>
        public List<string> AdditionalFlags { get; set; } = new();

        /// <summary>
        /// Gets or sets include directories for compilation.
        /// </summary>
        public List<string> IncludeDirectories { get; set; } = new();

        /// <summary>
        /// Gets or sets preprocessor definitions.
        /// </summary>
        public Dictionary<string, string> Defines { get; set; } = new();

        /// <summary>
        /// Gets a default set of compilation options.
        /// </summary>
        public static CompilationOptions Default => new();

        /// <summary>
        /// Gets options optimized for performance.
        /// </summary>
        public static CompilationOptions Performance => new()
        {
            OptimizationLevel = 3,
            UseFastMath = true,
            EnableDebugInfo = false
        };

        /// <summary>
        /// Gets options for debugging.
        /// </summary>
        public static CompilationOptions Debug => new()
        {
            OptimizationLevel = 0,
            UseFastMath = false,
            EnableDebugInfo = true
        };
    }
}