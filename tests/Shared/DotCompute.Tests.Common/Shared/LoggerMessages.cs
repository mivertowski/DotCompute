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
        public const int TestStarted = 50000;
        public const int TestCompleted = 50001;
        public const int TestFailed = 50002;
        public const int HardwareDetected = 50003;
        public const int PerformanceMeasured = 50004;
        public const int MemoryUsage = 50005;
        public const int TestDataGenerated = 50006;
        public const int TestAssertion = 50007;
        public const int TestSkipped = 50008;
        public const int TestCleanup = 50009;
        public const int GenericMessage = 50010;
    }

    /// <summary>
    /// Standard test categories for consistent categorization.
    /// </summary>
    public static class Categories
    {
        public const string MemoryAllocation = "MemoryAllocation";
        public const string BufferTypes = "BufferTypes";
        public const string Performance = "Performance";
        public const string Hardware = "Hardware";
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Gpu = "Gpu";
        public const string Cuda = "Cuda";
        public const string OpenCL = "OpenCL";
    }

    /// <summary>
    /// Standard test timeouts in milliseconds.
    /// </summary>
    public static class Timeouts
    {
        public const int Short = 5000;      // 5 seconds
        public const int Medium = 30000;    // 30 seconds
        public const int Long = 120000;     // 2 minutes
        public const int VeryLong = 300000; // 5 minutes
    }
}