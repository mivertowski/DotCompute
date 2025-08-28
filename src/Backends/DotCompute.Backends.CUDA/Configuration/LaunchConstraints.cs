using DotCompute.Backends.CUDA.Optimization.Types;

namespace DotCompute.Backends.CUDA.Optimization.Configuration
{
    /// <summary>
    /// Defines constraints for kernel launch configuration optimization.
    /// </summary>
    public class LaunchConstraints
    {
        /// <summary>
        /// Gets or sets the minimum block size.
        /// </summary>
        public int? MinBlockSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum block size.
        /// </summary>
        public int? MaxBlockSize { get; set; }

        /// <summary>
        /// Gets or sets the problem size (total number of elements to process).
        /// </summary>
        public int? ProblemSize { get; set; }

        /// <summary>
        /// Gets or sets whether block size must be a multiple of warp size.
        /// </summary>
        public bool RequireWarpMultiple { get; set; } = true;

        /// <summary>
        /// Gets or sets whether block size must be a power of two.
        /// </summary>
        public bool RequirePowerOfTwo { get; set; }


        /// <summary>
        /// Gets or sets the optimization hint for specific workload characteristics.
        /// </summary>
        public OptimizationHint OptimizationHint { get; set; } = OptimizationHint.Balanced;

        /// <summary>
        /// Gets a default set of constraints.
        /// </summary>
        public static LaunchConstraints Default => new();
    }
}