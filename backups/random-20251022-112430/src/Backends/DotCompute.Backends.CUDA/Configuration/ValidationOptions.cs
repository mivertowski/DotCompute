namespace DotCompute.Backends.CUDA.Configuration
{
    /// <summary>
    /// Options for CUDA validation suite execution.
    /// </summary>
    public class ValidationOptions
    {
        /// <summary>
        /// Gets or sets whether to validate system requirements.
        /// </summary>
        public bool ValidateSystemRequirements { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate CUDA installation.
        /// </summary>
        public bool ValidateCudaInstallation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate device capabilities.
        /// </summary>
        public bool ValidateDeviceCapabilities { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate memory management.
        /// </summary>
        public bool ValidateMemoryManagement { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate stream management.
        /// </summary>
        public bool ValidateStreamManagement { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate kernel compilation.
        /// </summary>
        public bool ValidateKernelCompilation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate graph optimization.
        /// </summary>
        public bool ValidateGraphOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate tensor cores.
        /// </summary>
        public bool ValidateTensorCores { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate performance profiling.
        /// </summary>
        public bool ValidatePerformanceProfiling { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate error recovery.
        /// </summary>
        public bool ValidateErrorRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate multi-GPU support.
        /// </summary>
        public bool ValidateMultiGpu { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate production workload.
        /// </summary>
        public bool ValidateProductionWorkload { get; set; } = true;

        /// <summary>
        /// Gets a default set of validation options.
        /// </summary>
        public static ValidationOptions Default => new();

        /// <summary>
        /// Gets quick validation options for basic checks.
        /// </summary>
        public static ValidationOptions Quick => new()
        {
            ValidateSystemRequirements = true,
            ValidateCudaInstallation = true,
            ValidateDeviceCapabilities = true,
            ValidateMemoryManagement = false,
            ValidateStreamManagement = false,
            ValidateKernelCompilation = false,
            ValidateGraphOptimization = false,
            ValidateTensorCores = false,
            ValidatePerformanceProfiling = false,
            ValidateErrorRecovery = false,
            ValidateMultiGpu = false,
            ValidateProductionWorkload = false
        };

        /// <summary>
        /// Gets full validation options for comprehensive testing.
        /// </summary>
        public static ValidationOptions Full => new()
        {
            ValidateSystemRequirements = true,
            ValidateCudaInstallation = true,
            ValidateDeviceCapabilities = true,
            ValidateMemoryManagement = true,
            ValidateStreamManagement = true,
            ValidateKernelCompilation = true,
            ValidateGraphOptimization = true,
            ValidateTensorCores = true,
            ValidatePerformanceProfiling = true,
            ValidateErrorRecovery = true,
            ValidateMultiGpu = true,
            ValidateProductionWorkload = true
        };
    }
}