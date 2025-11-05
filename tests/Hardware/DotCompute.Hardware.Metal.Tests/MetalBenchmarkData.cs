// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text.Json;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Metal performance benchmark data and thresholds for different hardware configurations.
/// Contains expected performance baselines for various Metal operations.
/// </summary>
public static class MetalBenchmarkData
{
    /// <summary>
    /// Performance thresholds for different Metal operations and hardware types.
    /// </summary>
    public static class PerformanceThresholds
    {
        /// <summary>
        /// Memory bandwidth thresholds in GB/s for different data sizes
        /// </summary>
        public static class MemoryBandwidth
        {
            public static readonly Dictionary<string, Dictionary<int, double>> MinimumBandwidth = new()
            {
                ["AppleSilicon_M1"] = new() { [1] = 200, [4] = 300, [16] = 400, [64] = 450, [256] = 480 },
                ["AppleSilicon_M2"] = new() { [1] = 250, [4] = 400, [16] = 500, [64] = 550, [256] = 600 },
                ["AppleSilicon_M3"] = new() { [1] = 300, [4] = 450, [16] = 550, [64] = 600, [256] = 650 },
                ["Intel_Iris"] = new() { [1] = 50, [4] = 100, [16] = 150, [64] = 180, [256] = 200 },
                ["Intel_RadeonPro"] = new() { [1] = 80, [4] = 150, [16] = 250, [64] = 300, [256] = 350 },
                ["AMD_RadeonPro"] = new() { [1] = 100, [4] = 200, [16] = 350, [64] = 450, [256] = 500 }
            };

            public static double GetMinimumBandwidth(string deviceType, int sizeMB)
            {
                if (MinimumBandwidth.TryGetValue(deviceType, out var thresholds))
                {
                    // Find closest size threshold
                    var closestSize = thresholds.Keys.OrderBy(s => Math.Abs(s - sizeMB)).First();
                    return thresholds[closestSize];
                }

                // Default conservative threshold
                return sizeMB switch
                {
                    <= 4 => 50,
                    <= 16 => 100,
                    <= 64 => 150,
                    _ => 200
                };
            }
        }

        /// <summary>
        /// Compute performance thresholds in GFLOPS
        /// </summary>
        public static class ComputePerformance
        {
            public static readonly Dictionary<string, ComputeThresholds> MinimumGFLOPS = new()
            {
                ["AppleSilicon_M1"] = new()
                {
                    VectorAdd = 800,
                    MatrixMultiply512 = 400,
                    MatrixMultiply1024 = 1200,
                    MatrixMultiply2048 = 2000,
                    FFT1K = 100,
                    FFT4K = 300,
                    Convolution = 500
                },
                ["AppleSilicon_M2"] = new()
                {
                    VectorAdd = 1200,
                    MatrixMultiply512 = 600,
                    MatrixMultiply1024 = 1800,
                    MatrixMultiply2048 = 3000,
                    FFT1K = 150,
                    FFT4K = 450,
                    Convolution = 750
                },
                ["AppleSilicon_M3"] = new()
                {
                    VectorAdd = 1500,
                    MatrixMultiply512 = 800,
                    MatrixMultiply1024 = 2400,
                    MatrixMultiply2048 = 4000,
                    FFT1K = 200,
                    FFT4K = 600,
                    Convolution = 1000
                },
                ["Intel_Iris"] = new()
                {
                    VectorAdd = 200,
                    MatrixMultiply512 = 100,
                    MatrixMultiply1024 = 300,
                    MatrixMultiply2048 = 500,
                    FFT1K = 50,
                    FFT4K = 150,
                    Convolution = 150
                },
                ["Intel_RadeonPro"] = new()
                {
                    VectorAdd = 400,
                    MatrixMultiply512 = 200,
                    MatrixMultiply1024 = 600,
                    MatrixMultiply2048 = 1000,
                    FFT1K = 80,
                    FFT4K = 250,
                    Convolution = 300
                },
                ["AMD_RadeonPro"] = new()
                {
                    VectorAdd = 600,
                    MatrixMultiply512 = 300,
                    MatrixMultiply1024 = 900,
                    MatrixMultiply2048 = 1500,
                    FFT1K = 120,
                    FFT4K = 350,
                    Convolution = 450
                }
            };

            public static ComputeThresholds GetThresholds(string deviceType)
            {
                return MinimumGFLOPS.TryGetValue(deviceType, out var thresholds)
                    ? thresholds
                    : new ComputeThresholds();
            }
        }

        /// <summary>
        /// Memory allocation performance thresholds
        /// </summary>
        public static class MemoryAllocation
        {
            public static readonly Dictionary<string, AllocationThresholds> MaximumTimes = new()
            {
                ["AppleSilicon_M1"] = new() { SmallBuffer = 0.1, MediumBuffer = 0.3, LargeBuffer = 1.0 },
                ["AppleSilicon_M2"] = new() { SmallBuffer = 0.08, MediumBuffer = 0.25, LargeBuffer = 0.8 },
                ["AppleSilicon_M3"] = new() { SmallBuffer = 0.06, MediumBuffer = 0.2, LargeBuffer = 0.6 },
                ["Intel_Iris"] = new() { SmallBuffer = 0.5, MediumBuffer = 1.5, LargeBuffer = 5.0 },
                ["Intel_RadeonPro"] = new() { SmallBuffer = 0.3, MediumBuffer = 1.0, LargeBuffer = 3.0 },
                ["AMD_RadeonPro"] = new() { SmallBuffer = 0.2, MediumBuffer = 0.8, LargeBuffer = 2.5 }
            };

            public static AllocationThresholds GetThresholds(string deviceType)
            {
                return MaximumTimes.TryGetValue(deviceType, out var thresholds)
                    ? thresholds
                    : new AllocationThresholds() { SmallBuffer = 1.0, MediumBuffer = 3.0, LargeBuffer = 10.0 };
            }
        }

        /// <summary>
        /// Kernel compilation time thresholds in milliseconds
        /// </summary>
        public static class KernelCompilation
        {
            public static readonly Dictionary<string, CompilationThresholds> MaximumTimes = new()
            {
                ["AppleSilicon_M1"] = new() { Simple = 50, Complex = 200, MathHeavy = 300 },
                ["AppleSilicon_M2"] = new() { Simple = 40, Complex = 150, MathHeavy = 250 },
                ["AppleSilicon_M3"] = new() { Simple = 30, Complex = 120, MathHeavy = 200 },
                ["Intel_Iris"] = new() { Simple = 100, Complex = 400, MathHeavy = 600 },
                ["Intel_RadeonPro"] = new() { Simple = 80, Complex = 300, MathHeavy = 500 },
                ["AMD_RadeonPro"] = new() { Simple = 70, Complex = 250, MathHeavy = 400 }
            };

            public static CompilationThresholds GetThresholds(string deviceType)
            {
                return MaximumTimes.TryGetValue(deviceType, out var thresholds)
                    ? thresholds
                    : new CompilationThresholds() { Simple = 200, Complex = 800, MathHeavy = 1200 };
            }
        }
    }

    /// <summary>
    /// Historical performance data for trend analysis
    /// </summary>
    public static class HistoricalData
    {
        private static readonly Dictionary<string, List<PerformanceSnapshot>> _snapshots = new();

        public static void RecordSnapshot(string testName, PerformanceSnapshot snapshot)
        {
            if (!_snapshots.ContainsKey(testName))
            {
                _snapshots[testName] = new List<PerformanceSnapshot>();
            }

            _snapshots[testName].Add(snapshot);

            // Keep only last 100 snapshots per test
            if (_snapshots[testName].Count > 100)
            {
                _snapshots[testName].RemoveAt(0);
            }
        }

        public static IReadOnlyList<PerformanceSnapshot> GetSnapshots(string testName)
        {
            return _snapshots.TryGetValue(testName, out var snapshots)
                ? snapshots.AsReadOnly()
                : new List<PerformanceSnapshot>().AsReadOnly();
        }

        public static PerformanceTrend AnalyzeTrend(string testName, int lookbackCount = 10)
        {
            var snapshots = GetSnapshots(testName);
            if (snapshots.Count < 2)
            {
                return new PerformanceTrend { Trend = TrendDirection.Stable, Confidence = 0.0 };
            }

            var recentSnapshots = snapshots.TakeLast(Math.Min(lookbackCount, snapshots.Count)).ToList();
            var values = recentSnapshots.Select(s => s.Value).ToArray();

            // Simple linear regression to detect trend
            var n = values.Length;
            var sumX = n * (n - 1) / 2; // 0 + 1 + 2 + ... + (n-1)
            var sumY = values.Sum();
            var sumXY = values.Select((v, i) => i * v).Sum();
            var sumX2 = n * (n - 1) * (2 * n - 1) / 6; // 0² + 1² + 2² + ... + (n-1)²

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var correlation = Math.Abs(slope * Math.Sqrt(sumX2 / n) / Math.Sqrt(values.Select(v => v * v).Average() - Math.Pow(values.Average(), 2)));

            var trend = Math.Abs(slope) < 0.001 ? TrendDirection.Stable :
                       slope > 0 ? TrendDirection.Improving : TrendDirection.Degrading;

            return new PerformanceTrend
            {
                Trend = trend,
                Confidence = Math.Min(correlation, 1.0),
                Slope = slope,
                RecentAverage = recentSnapshots.TakeLast(3).Average(s => s.Value),
                HistoricalAverage = snapshots.Average(s => s.Value)
            };
        }
    }

    /// <summary>
    /// Device detection and classification
    /// </summary>
    public static class DeviceClassification
    {
        public static string ClassifyDevice(string deviceName, bool isAppleSilicon)
        {
            if (isAppleSilicon)
            {
                return deviceName.ToLowerInvariant() switch
                {
                    var name when name.Contains("m3") => "AppleSilicon_M3",
                    var name when name.Contains("m2") => "AppleSilicon_M2",
                    var name when name.Contains("m1") => "AppleSilicon_M1",
                    _ => "AppleSilicon_M1" // Default to M1 for unknown Apple Silicon
                };
            }
            else
            {
                return deviceName.ToLowerInvariant() switch
                {
                    var name when name.Contains("radeon") && name.Contains("pro") => "AMD_RadeonPro",
                    var name when name.Contains("radeon") => "AMD_Radeon",
                    var name when name.Contains("iris") => "Intel_Iris",
                    var name when name.Contains("intel") => "Intel_Iris",
                    _ => "Intel_Iris" // Default to Intel for unknown discrete GPUs
                };
            }
        }

        public static DeviceCapabilities EstimateCapabilities(string deviceType)
        {
            return deviceType switch
            {
                "AppleSilicon_M3" => new()
                {
                    ComputeUnits = 20,
                    MaxMemoryBandwidth = 650,
                    MaxComputePerformance = 4000,
                    UnifiedMemory = true,
                    OptimalThreadgroupSize = 256
                },
                "AppleSilicon_M2" => new()
                {
                    ComputeUnits = 16,
                    MaxMemoryBandwidth = 600,
                    MaxComputePerformance = 3000,
                    UnifiedMemory = true,
                    OptimalThreadgroupSize = 256
                },
                "AppleSilicon_M1" => new()
                {
                    ComputeUnits = 8,
                    MaxMemoryBandwidth = 480,
                    MaxComputePerformance = 2000,
                    UnifiedMemory = true,
                    OptimalThreadgroupSize = 256
                },
                "Intel_RadeonPro" => new()
                {
                    ComputeUnits = 32,
                    MaxMemoryBandwidth = 350,
                    MaxComputePerformance = 1000,
                    UnifiedMemory = false,
                    OptimalThreadgroupSize = 256
                },
                "Intel_Iris" => new()
                {
                    ComputeUnits = 8,
                    MaxMemoryBandwidth = 200,
                    MaxComputePerformance = 500,
                    UnifiedMemory = false,
                    OptimalThreadgroupSize = 256
                },
                "AMD_RadeonPro" => new()
                {
                    ComputeUnits = 40,
                    MaxMemoryBandwidth = 500,
                    MaxComputePerformance = 1500,
                    UnifiedMemory = false,
                    OptimalThreadgroupSize = 256
                },
                _ => new() // Default capabilities
                {
                    ComputeUnits = 4,
                    MaxMemoryBandwidth = 100,
                    MaxComputePerformance = 200,
                    UnifiedMemory = false,
                    OptimalThreadgroupSize = 256
                }
            };
        }
    }

    /// <summary>
    /// Test configuration and scaling factors
    /// </summary>
    public static class TestConfiguration
    {
        public static TestScaleFactors GetScaleFactors(string deviceType, long availableMemory)
        {
            var baseFactors = deviceType switch
            {
                "AppleSilicon_M3" => new TestScaleFactors { Memory = 1.0, Compute = 1.0, Time = 1.0 },
                "AppleSilicon_M2" => new TestScaleFactors { Memory = 0.8, Compute = 0.8, Time = 1.2 },
                "AppleSilicon_M1" => new TestScaleFactors { Memory = 0.6, Compute = 0.6, Time = 1.5 },
                "Intel_RadeonPro" => new TestScaleFactors { Memory = 0.4, Compute = 0.3, Time = 2.0 },
                "Intel_Iris" => new TestScaleFactors { Memory = 0.2, Compute = 0.2, Time = 3.0 },
                "AMD_RadeonPro" => new TestScaleFactors { Memory = 0.5, Compute = 0.4, Time = 1.8 },
                _ => new TestScaleFactors { Memory = 0.1, Compute = 0.1, Time = 5.0 }
            };

            // Adjust for available memory
            var memoryGB = availableMemory / (1024.0 * 1024 * 1024);
            var memoryFactor = memoryGB switch
            {
                >= 32 => 1.0,
                >= 16 => 0.8,
                >= 8 => 0.6,
                >= 4 => 0.4,
                _ => 0.2
            };

            return new TestScaleFactors
            {
                Memory = baseFactors.Memory * memoryFactor,
                Compute = baseFactors.Compute,
                Time = baseFactors.Time
            };
        }

        public static int ScaleDataSize(int baseSize, TestScaleFactors factors)
        {
            return (int)(baseSize * factors.Memory);
        }

        public static TimeSpan ScaleTimeout(TimeSpan baseTimeout, TestScaleFactors factors)
        {
            return TimeSpan.FromMilliseconds(baseTimeout.TotalMilliseconds * factors.Time);
        }

        public static double ScalePerformanceExpectation(double baseExpectation, TestScaleFactors factors)
        {
            return baseExpectation * factors.Compute;
        }
    }
}

/// <summary>
/// Data structures for benchmark thresholds and results
/// </summary>
public record ComputeThresholds
{
    public double VectorAdd { get; init; } = 100;
    public double MatrixMultiply512 { get; init; } = 50;
    public double MatrixMultiply1024 { get; init; } = 200;
    public double MatrixMultiply2048 { get; init; } = 500;
    public double FFT1K { get; init; } = 25;
    public double FFT4K { get; init; } = 100;
    public double Convolution { get; init; } = 150;
}

public record AllocationThresholds
{
    public double SmallBuffer { get; init; } = 1.0;   // < 1MB
    public double MediumBuffer { get; init; } = 3.0;  // 1-100MB
    public double LargeBuffer { get; init; } = 10.0;  // > 100MB

    public double GetThreshold(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 1024 * 1024 => SmallBuffer,
            < 100 * 1024 * 1024 => MediumBuffer,
            _ => LargeBuffer
        };
    }
}

public record CompilationThresholds
{
    public double Simple { get; init; } = 100;    // Simple kernels
    public double Complex { get; init; } = 500;   // Complex kernels with loops
    public double MathHeavy { get; init; } = 1000; // Math-intensive kernels
}

public record DeviceCapabilities
{
    public int ComputeUnits { get; init; }
    public double MaxMemoryBandwidth { get; init; }
    public double MaxComputePerformance { get; init; }
    public bool UnifiedMemory { get; init; }
    public int OptimalThreadgroupSize { get; init; }
}

public record TestScaleFactors
{
    public double Memory { get; init; } = 1.0;
    public double Compute { get; init; } = 1.0;
    public double Time { get; init; } = 1.0;
}

public record PerformanceSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string TestName { get; init; } = "";
    public string DeviceType { get; init; } = "";
    public double Value { get; init; }
    public string Units { get; init; } = "";
    public string BuildVersion { get; init; } = "";
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record PerformanceTrend
{
    public TrendDirection Trend { get; init; }
    public double Confidence { get; init; }
    public double Slope { get; init; }
    public double RecentAverage { get; init; }
    public double HistoricalAverage { get; init; }
}

public enum TrendDirection
{
    Improving,
    Stable,
    Degrading
}

/// <summary>
/// Utilities for working with benchmark data
/// </summary>
public static class BenchmarkUtilities
{
    /// <summary>
    /// Detect current device type for benchmark classification
    /// </summary>
    public static string DetectCurrentDeviceType()
    {
        var isAppleSilicon = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        // This would typically query the actual Metal device name
        // For now, we'll use system detection
        if (isAppleSilicon)
        {
            // Could use more sophisticated detection for M1/M2/M3
            return "AppleSilicon_M1"; // Default assumption
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Intel_Iris"; // Default for Intel Macs
        }

        return "Unknown";
    }

    /// <summary>
    /// Calculate performance score relative to baseline
    /// </summary>
    public static double CalculatePerformanceScore(double actual, double expected)
    {
        if (expected <= 0)
            return 0.0;
        return (actual / expected) * 100.0;
    }

    /// <summary>
    /// Determine if performance meets threshold
    /// </summary>
    public static bool MeetsPerformanceThreshold(double actual, double threshold, double tolerancePercent = 20.0)
    {
        var minimumAcceptable = threshold * (1.0 - tolerancePercent / 100.0);
        return actual >= minimumAcceptable;
    }

    /// <summary>
    /// Generate performance report
    /// </summary>
    public static string GeneratePerformanceReport(string testName, Dictionary<string, double> metrics, string deviceType)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Performance Report: {testName}");
        report.AppendLine($"Device Type: {deviceType}");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();

        foreach (var metric in metrics)
        {
            var score = CalculatePerformanceScore(metric.Value, 100.0); // Assuming 100 as baseline
            var status = score >= 80 ? "✓" : score >= 60 ? "⚠" : "✗";

            report.AppendLine($"{status} {metric.Key}: {metric.Value:F2} (Score: {score:F1}%)");
        }

        return report.ToString();
    }

    /// <summary>
    /// Export benchmark data to JSON
    /// </summary>
    public static string ExportBenchmarkData(IEnumerable<PerformanceSnapshot> snapshots)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(snapshots, options);
    }

    /// <summary>
    /// Import benchmark data from JSON
    /// </summary>
    public static IEnumerable<PerformanceSnapshot> ImportBenchmarkData(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        try
        {
            return JsonSerializer.Deserialize<PerformanceSnapshot[]>(json, options) ?? Array.Empty<PerformanceSnapshot>();
        }
        catch
        {
            return Array.Empty<PerformanceSnapshot>();
        }
    }
}
