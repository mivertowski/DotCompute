// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Tests.Common.Helpers;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Performance baselines and expectations for different Metal hardware configurations.
    /// These baselines help validate that Metal backend performance is within expected ranges
    /// for different Apple hardware configurations.
    /// </summary>
    public static class MetalPerformanceBaselines
    {
        /// <summary>
        /// Performance baseline data for different hardware configurations
        /// </summary>
        public static class BaselineData
        {
            /// <summary>
            /// Apple M2 performance baselines (8-core GPU, 16GB unified memory)
            /// </summary>
            public static readonly PerformanceBaseline AppleM2 = new()
            {
                Name = "Apple M2",
                Architecture = "Apple Silicon",
                MaxMemoryBandwidthGBps = 100.0,
                ComputeUnits = 8,
                EstimatedTFlops = 3.6,

                // Memory operations (per 1MB transfer)
                MemoryTransfers = new Dictionary<string, TimeSpan>
                {
                    ["HostToDevice_1MB"] = TimeSpan.FromMicroseconds(50),
                    ["DeviceToHost_1MB"] = TimeSpan.FromMicroseconds(50),
                    ["DeviceToDevice_1MB"] = TimeSpan.FromMicroseconds(10)
                },

                // Compute operations (per 1M elements)
                ComputeOperations = new Dictionary<string, TimeSpan>
                {
                    ["VectorAdd_1M_Float"] = TimeSpan.FromMicroseconds(200),
                    ["VectorMultiply_1M_Float"] = TimeSpan.FromMicroseconds(200),
                    ["MatrixMultiply_1024x1024_Float"] = TimeSpan.FromMilliseconds(10),
                    ["Reduction_1M_Float"] = TimeSpan.FromMicroseconds(500)
                },

                // Bandwidth expectations (GB/s)
                BandwidthExpectations = new Dictionary<string, double>
                {
                    ["MemoryBandwidth"] = 100.0,
                    ["ComputeBandwidth"] = 400.0
                },

                // Scaling factors for test data sizes
                ScalingFactors = new Dictionary<string, int>
                {
                    ["LowMemory"] = 1,
                    ["Standard"] = 2,
                    ["HighMemory"] = 4,
                    ["Stress"] = 8
                }
            };

            /// <summary>
            /// Apple M3 performance baselines (10-core GPU, 24GB unified memory)
            /// </summary>
            public static readonly PerformanceBaseline AppleM3 = new()
            {
                Name = "Apple M3",
                Architecture = "Apple Silicon",
                MaxMemoryBandwidthGBps = 100.0,
                ComputeUnits = 10,
                EstimatedTFlops = 4.28,

                MemoryTransfers = new Dictionary<string, TimeSpan>
                {
                    ["HostToDevice_1MB"] = TimeSpan.FromMicroseconds(40),
                    ["DeviceToHost_1MB"] = TimeSpan.FromMicroseconds(40),
                    ["DeviceToDevice_1MB"] = TimeSpan.FromMicroseconds(8)
                },

                ComputeOperations = new Dictionary<string, TimeSpan>
                {
                    ["VectorAdd_1M_Float"] = TimeSpan.FromMicroseconds(160),
                    ["VectorMultiply_1M_Float"] = TimeSpan.FromMicroseconds(160),
                    ["MatrixMultiply_1024x1024_Float"] = TimeSpan.FromMilliseconds(8),
                    ["Reduction_1M_Float"] = TimeSpan.FromMicroseconds(400)
                },

                BandwidthExpectations = new Dictionary<string, double>
                {
                    ["MemoryBandwidth"] = 100.0,
                    ["ComputeBandwidth"] = 500.0
                },

                ScalingFactors = new Dictionary<string, int>
                {
                    ["LowMemory"] = 1,
                    ["Standard"] = 3,
                    ["HighMemory"] = 6,
                    ["Stress"] = 12
                }
            };

            /// <summary>
            /// Intel Mac with discrete GPU baseline (conservative estimates)
            /// </summary>
            public static readonly PerformanceBaseline IntelMac = new()
            {
                Name = "Intel Mac + Discrete GPU",
                Architecture = "Intel x64",
                MaxMemoryBandwidthGBps = 256.0, // PCIe-based discrete GPU
                ComputeUnits = 24,
                EstimatedTFlops = 5.0,

                MemoryTransfers = new Dictionary<string, TimeSpan>
                {
                    ["HostToDevice_1MB"] = TimeSpan.FromMicroseconds(200),
                    ["DeviceToHost_1MB"] = TimeSpan.FromMicroseconds(300),
                    ["DeviceToDevice_1MB"] = TimeSpan.FromMicroseconds(5)
                },

                ComputeOperations = new Dictionary<string, TimeSpan>
                {
                    ["VectorAdd_1M_Float"] = TimeSpan.FromMicroseconds(150),
                    ["VectorMultiply_1M_Float"] = TimeSpan.FromMicroseconds(150),
                    ["MatrixMultiply_1024x1024_Float"] = TimeSpan.FromMilliseconds(6),
                    ["Reduction_1M_Float"] = TimeSpan.FromMicroseconds(300)
                },

                BandwidthExpectations = new Dictionary<string, double>
                {
                    ["MemoryBandwidth"] = 256.0,
                    ["ComputeBandwidth"] = 600.0
                },

                ScalingFactors = new Dictionary<string, int>
                {
                    ["LowMemory"] = 1,
                    ["Standard"] = 2,
                    ["HighMemory"] = 3,
                    ["Stress"] = 6
                }
            };
        }

        /// <summary>
        /// Auto-detects the appropriate performance baseline for the current hardware
        /// </summary>
        public static PerformanceBaseline DetectHardwareBaseline(ITestOutputHelper output)
        {
            var hardwareInfo = HardwareDetection.Info;

            output.WriteLine("=== Hardware Detection for Performance Baseline ===");
            output.WriteLine($"Platform: {hardwareInfo.Platform}");
            output.WriteLine($"Architecture: {hardwareInfo.Architecture}");
            output.WriteLine($"Total Memory: {hardwareInfo.TotalMemory / (1024.0 * 1024.0 * 1024.0):F1} GB");
            output.WriteLine($"Metal Available: {hardwareInfo.MetalAvailable}");
            output.WriteLine($"Apple Silicon: {hardwareInfo.IsAppleSilicon}");

            if (!hardwareInfo.MetalAvailable)
            {
                throw new InvalidOperationException("Metal is not available on this system");
            }

            if (hardwareInfo.IsAppleSilicon)
            {
                var totalMemoryGB = hardwareInfo.TotalMemory / (1024.0 * 1024.0 * 1024.0);

                if (totalMemoryGB >= 20)
                {
                    output.WriteLine("Detected: Apple Silicon with ≥20GB (likely M3 or newer)");
                    return BaselineData.AppleM3;
                }
                else
                {
                    output.WriteLine("Detected: Apple Silicon with <20GB (likely M1/M2)");
                    return BaselineData.AppleM2;
                }
            }
            else
            {
                output.WriteLine("Detected: Intel Mac with discrete GPU");
                return BaselineData.IntelMac;
            }
        }

        /// <summary>
        /// Calculates appropriate test scale factor based on available hardware
        /// </summary>
        public static int CalculateTestScaleFactor(PerformanceBaseline baseline, string testType = "Standard")
        {
            var hardwareInfo = HardwareDetection.Info;
            var availableMemoryGB = hardwareInfo.AvailableMemory / (1024.0 * 1024.0 * 1024.0);

            // Get base scale from baseline
            var baseScale = baseline.ScalingFactors.TryGetValue(testType, out var scale) ? scale : 1;

            // Adjust based on actual available memory
            if (availableMemoryGB < 4)
            {
                return Math.Max(1, baseScale / 4); // Very conservative for low memory
            }
            else if (availableMemoryGB < 8)
            {
                return Math.Max(1, baseScale / 2); // Conservative for limited memory
            }
            else if (availableMemoryGB > 24)
            {
                return baseScale * 2; // Aggressive for high memory systems
            }

            return baseScale;
        }

        /// <summary>
        /// Validates that a measured performance is within acceptable range of baseline
        /// </summary>
        public static PerformanceValidationResult ValidatePerformance(
            PerformanceBaseline baseline,
            string operationName,
            TimeSpan measuredTime,
            double tolerancePercent = 50.0)
        {
            if (!baseline.ComputeOperations.TryGetValue(operationName, out var expectedTime))
            {
                return new PerformanceValidationResult
                {
                    IsValid = false,
                    Message = $"No baseline found for operation: {operationName}",
                    ExpectedTime = TimeSpan.Zero,
                    MeasuredTime = measuredTime,
                    PerformanceRatio = 0.0
                };
            }

            var ratio = measuredTime.TotalMilliseconds / expectedTime.TotalMilliseconds;
            var percentDiff = Math.Abs(1.0 - ratio) * 100.0;
            var isWithinTolerance = percentDiff <= tolerancePercent;

            return new PerformanceValidationResult
            {
                IsValid = isWithinTolerance,
                Message = isWithinTolerance
                    ? $"Performance within {tolerancePercent}% tolerance"
                    : $"Performance outside tolerance: {percentDiff:F1}% difference",
                ExpectedTime = expectedTime,
                MeasuredTime = measuredTime,
                PerformanceRatio = ratio,
                PercentageDifference = percentDiff
            };
        }

        /// <summary>
        /// Creates a performance test configuration based on current hardware
        /// </summary>
        public static PerformanceTestConfiguration CreateTestConfiguration(ITestOutputHelper output)
        {
            var baseline = DetectHardwareBaseline(output);
            var hardwareInfo = HardwareDetection.Info;

            return new PerformanceTestConfiguration
            {
                Baseline = baseline,
                HardwareInfo = hardwareInfo,
                TestScaling = new Dictionary<string, int>
                {
                    ["Unit"] = CalculateTestScaleFactor(baseline, "LowMemory"),
                    ["Integration"] = CalculateTestScaleFactor(baseline, "Standard"),
                    ["Performance"] = CalculateTestScaleFactor(baseline, "HighMemory"),
                    ["Stress"] = CalculateTestScaleFactor(baseline, "Stress")
                },

                // Test size configurations (element counts)
                TestSizes = new Dictionary<string, int>
                {
                    ["VectorSmall"] = 1024 * CalculateTestScaleFactor(baseline, "LowMemory"),
                    ["VectorMedium"] = 1024 * 1024 * CalculateTestScaleFactor(baseline, "Standard"),
                    ["VectorLarge"] = 16 * 1024 * 1024 * CalculateTestScaleFactor(baseline, "HighMemory"),
                    ["MatrixSmall"] = 256,
                    ["MatrixMedium"] = 1024,
                    ["MatrixLarge"] = 2048 * (int)Math.Sqrt(CalculateTestScaleFactor(baseline, "HighMemory"))
                },

                // Performance expectations
                TolerancePercent = baseline.Architecture == "Apple Silicon" ? 30.0 : 50.0, // Tighter tolerance for Apple Silicon
                MaxTestDuration = TimeSpan.FromMinutes(5),
                WarmupIterations = 3,
                MeasurementIterations = 10
            };
        }
    }

    /// <summary>
    /// Performance baseline definition for a specific hardware configuration
    /// </summary>
    public class PerformanceBaseline
    {
        public required string Name { get; init; }
        public required string Architecture { get; init; }
        public required double MaxMemoryBandwidthGBps { get; init; }
        public required int ComputeUnits { get; init; }
        public required double EstimatedTFlops { get; init; }
        public required Dictionary<string, TimeSpan> MemoryTransfers { get; init; }
        public required Dictionary<string, TimeSpan> ComputeOperations { get; init; }
        public required Dictionary<string, double> BandwidthExpectations { get; init; }
        public required Dictionary<string, int> ScalingFactors { get; init; }
    }

    /// <summary>
    /// Result of performance validation against baseline
    /// </summary>
    public struct PerformanceValidationResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; }
        public TimeSpan ExpectedTime { get; init; }
        public TimeSpan MeasuredTime { get; init; }
        public double PerformanceRatio { get; init; }
        public double PercentageDifference { get; init; }

        public readonly void LogResult(ITestOutputHelper output)
        {
            output.WriteLine($"Performance Validation: {(IsValid ? "PASS" : "FAIL")}");
            output.WriteLine($"  Message: {Message}");
            output.WriteLine($"  Expected: {ExpectedTime.TotalMilliseconds:F2}ms");
            output.WriteLine($"  Measured: {MeasuredTime.TotalMilliseconds:F2}ms");
            output.WriteLine($"  Ratio: {PerformanceRatio:F2}x");
            output.WriteLine($"  Difference: {PercentageDifference:F1}%");
        }
    }

    /// <summary>
    /// Complete test configuration for performance validation
    /// </summary>
    public class PerformanceTestConfiguration
    {
        public required PerformanceBaseline Baseline { get; init; }
        public required HardwareDetection.HardwareInfo HardwareInfo { get; init; }
        public required Dictionary<string, int> TestScaling { get; init; }
        public required Dictionary<string, int> TestSizes { get; init; }
        public required double TolerancePercent { get; init; }
        public required TimeSpan MaxTestDuration { get; init; }
        public required int WarmupIterations { get; init; }
        public required int MeasurementIterations { get; init; }

        public void LogConfiguration(ITestOutputHelper output)
        {
            output.WriteLine("=== Performance Test Configuration ===");
            output.WriteLine($"Baseline: {Baseline.Name}");
            output.WriteLine($"Architecture: {Baseline.Architecture}");
            output.WriteLine($"Tolerance: {TolerancePercent}%");
            output.WriteLine($"Max Duration: {MaxTestDuration.TotalMinutes:F1} minutes");
            output.WriteLine("");

            output.WriteLine("Test Scaling:");
            foreach (var (testType, scale) in TestScaling)
            {
                output.WriteLine($"  {testType}: {scale}x");
            }
            output.WriteLine("");

            output.WriteLine("Test Sizes:");
            foreach (var (sizeType, elements) in TestSizes)
            {
                output.WriteLine($"  {sizeType}: {elements:N0} elements");
            }
        }
    }
}
