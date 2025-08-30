using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Configuration;

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Configuration for dynamic parallelism kernel launches.
    /// </summary>
    public class DynamicParallelismConfig
    {
        /// <summary>
        /// Gets or sets the launch configuration for the parent kernel.
        /// </summary>
        public LaunchConfiguration ParentConfig { get; set; } = null!;

        /// <summary>
        /// Gets or sets the launch configuration for child kernels.
        /// </summary>
        public LaunchConfiguration ChildConfig { get; set; } = null!;

        /// <summary>
        /// Gets or sets the maximum nesting depth for kernel launches.
        /// </summary>
        public int MaxNestingDepth { get; set; }

        /// <summary>
        /// Gets or sets the total warps available across all SMs.
        /// </summary>
        public int TotalWarpsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the number of warps needed by the parent kernel.
        /// </summary>
        public int ParentWarpsNeeded { get; set; }

        /// <summary>
        /// Gets or sets the number of warps needed per child kernel.
        /// </summary>
        public int ChildWarpsPerParent { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of child kernels that can be launched per parent.
        /// </summary>
        public int MaxChildrenPerParent { get; set; }
    }
}