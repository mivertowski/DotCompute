// This file provides category constants for test categorization
// It's included in all test projects via test-categories.props

namespace DotCompute.Tests.Common
{
    /// <summary>
    /// Test category constants for organizing and filtering tests.
    /// </summary>
    public static class TestCategories
    {
        // Basic test types
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Performance = "Performance";
        public const string Mock = "Mock";

        // Hardware categories
        public const string Hardware = "Hardware";
        public const string GPU = "GPU";
        public const string CPU = "CPU";

        // Specific hardware backends
        public const string CUDA = "CUDA";
        public const string OpenCL = "OpenCL";
        public const string DirectCompute = "DirectCompute";
        public const string Metal = "Metal";

        // Hardware-specific models
        public const string RTX2000 = "RTX2000";
        public const string RTX3000 = "RTX3000";
        public const string RTX4000 = "RTX4000";

        // Test characteristics
        public const string LongRunning = "LongRunning";
        public const string SlowIntegration = "SlowIntegration";
        public const string MemoryIntensive = "MemoryIntensive";
        public const string NetworkDependent = "NetworkDependent";

        // Component-specific categories
        public const string Core = "Core";
        public const string Memory = "Memory";
        public const string Algorithms = "Algorithms";
        public const string Backends = "Backends";
        public const string Extensions = "Extensions";

        // Platform-specific categories
        public const string Windows = "Windows";
        public const string Linux = "Linux";
        public const string MacOS = "MacOS";

        // CI/CD categories
        public const string RequiresHardware = "RequiresHardware";
        public const string ContinuousIntegration = "CI";
        public const string LocalOnly = "LocalOnly";

        // Regression and stability
        public const string Regression = "Regression";
        public const string Stability = "Stability";
        public const string Stress = "Stress";

        // Security and compliance
        public const string Security = "Security";
        public const string Compliance = "Compliance";

        /// <summary>
        /// Gets all hardware-dependent categories.
        /// </summary>
        public static readonly string[] HardwareDependent = 
        {
            Hardware, GPU, CUDA, OpenCL, DirectCompute, Metal, 
            RTX2000, RTX3000, RTX4000, RequiresHardware
        };

        /// <summary>
        /// Gets all categories that can run in CI without hardware.
        /// </summary>
        public static readonly string[] CICompatible = 
        {
            Unit, Integration, Mock, CPU, Core, Memory, 
            Algorithms, ContinuousIntegration
        };

        /// <summary>
        /// Gets all performance-related categories.
        /// </summary>
        public static readonly string[] PerformanceRelated = 
        {
            Performance, LongRunning, MemoryIntensive, Stress
        };

        /// <summary>
        /// Determines if a category requires hardware to run.
        /// </summary>
        /// <param name="category">The test category</param>
        /// <returns>True if the category requires physical hardware</returns>
        public static bool RequiresPhysicalHardware(string category)
        {
            return Array.Exists(HardwareDependent, c => 
                string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines if a category is suitable for CI environments.
        /// </summary>
        /// <param name="category">The test category</param>
        /// <returns>True if the category can run in CI</returns>
        public static bool IsCICompatible(string category)
        {
            return Array.Exists(CICompatible, c => 
                string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
        }
    }
}