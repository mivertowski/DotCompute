namespace DotCompute.Tests
{
    /// <summary>
    /// Centralized test category constants for consistent test filtering.
    /// </summary>
    public static class TestCategories
    {
        // Core categories
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Mock = "Mock";
        public const string CI = "CI"; // Tests safe for CI/CD
        
        // Hardware categories
        public const string Hardware = "Hardware";
        public const string HardwareRequired = "HardwareRequired";
        public const string CudaRequired = "CudaRequired";
        public const string OpenCLRequired = "OpenCLRequired";
        public const string DirectComputeRequired = "DirectComputeRequired";
        public const string RTX2000 = "RTX2000";
        public const string GPU = "GPU";
        
        // Performance categories
        public const string Performance = "Performance";
        public const string Benchmark = "Benchmark";
        public const string Stress = "Stress";
        public const string LongRunning = "LongRunning";
        
        // Platform categories
        public const string Windows = "Windows";
        public const string Linux = "Linux";
        public const string MacOS = "MacOS";
        
        // Special categories
        public const string EdgeCase = "EdgeCase";
        public const string Flaky = "Flaky";
        public const string Manual = "Manual"; // Tests that should only run manually
        public const string Nightly = "Nightly"; // Tests for nightly builds
        
        /// <summary>
        /// Helper to create xUnit trait attributes
        /// </summary>
        public static class Traits
        {
            public static string Category = "Category";
            public static string Hardware = "Hardware";
            public static string Platform = "Platform";
            public static string Performance = "Performance";
        }
    }
    
    /// <summary>
    /// Test execution profiles for different scenarios
    /// </summary>
    public static class TestProfiles
    {
        /// <summary>
        /// Quick feedback tests - should complete in under 3 minutes
        /// </summary>
        public static readonly string[] QuickTests = 
        {
            TestCategories.Unit,
            TestCategories.Mock,
            TestCategories.CI
        };
        
        /// <summary>
        /// Standard CI tests - should complete in under 10 minutes
        /// </summary>
        public static readonly string[] StandardCITests = 
        {
            TestCategories.Unit,
            TestCategories.Mock,
            TestCategories.Integration,
            TestCategories.CI
        };
        
        /// <summary>
        /// Hardware-specific tests - require GPU/special hardware
        /// </summary>
        public static readonly string[] HardwareTests = 
        {
            TestCategories.Hardware,
            TestCategories.HardwareRequired,
            TestCategories.CudaRequired,
            TestCategories.OpenCLRequired,
            TestCategories.DirectComputeRequired,
            TestCategories.RTX2000,
            TestCategories.GPU
        };
        
        /// <summary>
        /// Performance and stress tests
        /// </summary>
        public static readonly string[] PerformanceTests = 
        {
            TestCategories.Performance,
            TestCategories.Benchmark,
            TestCategories.Stress,
            TestCategories.LongRunning
        };
        
        /// <summary>
        /// Tests to exclude from CI/CD
        /// </summary>
        public static readonly string[] ExcludeFromCI = 
        {
            TestCategories.Hardware,
            TestCategories.HardwareRequired,
            TestCategories.CudaRequired,
            TestCategories.OpenCLRequired,
            TestCategories.DirectComputeRequired,
            TestCategories.RTX2000,
            TestCategories.GPU,
            TestCategories.Stress,
            TestCategories.LongRunning,
            TestCategories.Manual,
            TestCategories.Flaky
        };
    }
}