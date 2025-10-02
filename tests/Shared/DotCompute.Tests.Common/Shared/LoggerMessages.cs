// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Tests.Common.Constants;

/// <summary>
/// Shared constants and utilities for test logging patterns across DotCompute test projects.
/// Provides consistent event ID ranges and standardized logging patterns.
/// </summary>
internal static class SharedTestConstants
{
    /// <summary>
    /// Event ID range reserved for test-specific logging: 50000-50999
    /// </summary>
    public static class EventIds
    {
        /// <summary>
        /// The test started.
        /// </summary>
        public const int TestStarted = 50000;
        /// <summary>
        /// The test completed.
        /// </summary>
        public const int TestCompleted = 50001;
        /// <summary>
        /// The test failed.
        /// </summary>
        public const int TestFailed = 50002;
        /// <summary>
        /// The hardware detected.
        /// </summary>
        public const int HardwareDetected = 50003;
        /// <summary>
        /// The performance measured.
        /// </summary>
        public const int PerformanceMeasured = 50004;
        /// <summary>
        /// The memory usage.
        /// </summary>
        public const int MemoryUsage = 50005;
        /// <summary>
        /// The test data generated.
        /// </summary>
        public const int TestDataGenerated = 50006;
        /// <summary>
        /// The test assertion.
        /// </summary>
        public const int TestAssertion = 50007;
        /// <summary>
        /// The test skipped.
        /// </summary>
        public const int TestSkipped = 50008;
        /// <summary>
        /// The test cleanup.
        /// </summary>
        public const int TestCleanup = 50009;
        /// <summary>
        /// The generic message.
        /// </summary>
        public const int GenericMessage = 50010;
    }

    /// <summary>
    /// Standard test categories for consistent categorization.
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// The memory allocation.
        /// </summary>
        public const string MemoryAllocation = "MemoryAllocation";
        /// <summary>
        /// The buffer types.
        /// </summary>
        public const string BufferTypes = "BufferTypes";
        /// <summary>
        /// The performance.
        /// </summary>
        public const string Performance = "Performance";
        /// <summary>
        /// The hardware.
        /// </summary>
        public const string Hardware = "Hardware";
        /// <summary>
        /// The unit.
        /// </summary>
        public const string Unit = "Unit";
        /// <summary>
        /// The integration.
        /// </summary>
        public const string Integration = "Integration";
        /// <summary>
        /// The gpu.
        /// </summary>
        public const string Gpu = "Gpu";
        /// <summary>
        /// The cuda.
        /// </summary>
        public const string Cuda = "Cuda";
        /// <summary>
        /// The open c l.
        /// </summary>
        public const string OpenCL = "OpenCL";
    }

    /// <summary>
    /// Standard test timeouts in milliseconds.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>
        /// The short.
        /// </summary>
        public const int Short = 5000;      // 5 seconds
        /// <summary>
        /// The medium.
        /// </summary>
        public const int Medium = 30000;    // 30 seconds
        /// <summary>
        /// The long.
        /// </summary>
        public const int Long = 120000;     // 2 minutes
        /// <summary>
        /// The very long.
        /// </summary>
        public const int VeryLong = 300000; // 5 minutes
    }
}