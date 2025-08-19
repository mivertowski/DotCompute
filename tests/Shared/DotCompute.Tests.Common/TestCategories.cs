namespace DotCompute.Tests.Common
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
        internal static class Traits
        {
            public const string Category = "Category";
            public const string Hardware = "Hardware";
            public const string Platform = "Platform";
            public const string Performance = "Performance";
        }
    }
    
    /// <summary>
    /// Test execution profiles for different scenarios
    /// </summary>
    public static class TestProfiles
    {
        /// <summary>
        /// Gets quick feedback tests - should complete in under 3 minutes
        /// </summary>
        public static string[] QuickTests => new[]
        {
            TestCategories.Unit,
            TestCategories.Mock,
            TestCategories.CI
        };
        
        /// <summary>
        /// Gets standard CI tests - should complete in under 10 minutes
        /// </summary>
        public static string[] StandardCITests => new[]
        {
            TestCategories.Unit,
            TestCategories.Mock,
            TestCategories.Integration,
            TestCategories.CI
        };
        
        /// <summary>
        /// Gets hardware-specific tests - require GPU/special hardware
        /// </summary>
        public static string[] HardwareTests => new[]
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
        /// Gets performance and stress tests
        /// </summary>
        public static string[] PerformanceTests => new[]
        {
            TestCategories.Performance,
            TestCategories.Benchmark,
            TestCategories.Stress,
            TestCategories.LongRunning
        };
        
        /// <summary>
        /// Gets tests to exclude from CI/CD
        /// </summary>
        public static string[] ExcludeFromCI => new[]
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
    }}
