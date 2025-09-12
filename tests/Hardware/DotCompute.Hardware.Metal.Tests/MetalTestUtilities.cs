// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using FluentAssertions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Utility classes and methods for Metal testing infrastructure.
/// Provides common test data generation, performance measurement, and validation helpers.
/// </summary>
public static class MetalTestUtilities
{
    /// <summary>
    /// Generate test data with specific patterns for Metal tests
    /// </summary>
    public static class TestDataGenerator
    {
        public static float[] CreateLinearSequence(int count, float start = 0.0f, float step = 1.0f)
        {
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                data[i] = start + i * step;
            }
            return data;
        }

        public static float[] CreateSinusoidalData(int count, double frequency = 0.01, float amplitude = 1.0f)
        {
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                data[i] = amplitude * (float)Math.Sin(i * frequency);
            }
            return data;
        }

        public static float[] CreateRandomData(int count, int seed = 42, float min = -1.0f, float max = 1.0f)
        {
            var random = new Random(seed);
            var data = new float[count];
            var range = max - min;

            for (var i = 0; i < count; i++)
            {
                data[i] = min + (float)random.NextDouble() * range;
            }
            return data;
        }

        public static float[] CreateConstantData(int count, float value)
        {
            var data = new float[count];
            Array.Fill(data, value);
            return data;
        }

        public static float[] CreateGaussianNoise(int count, float mean = 0.0f, float stdDev = 1.0f, int seed = 42)
        {
            var random = new Random(seed);
            var data = new float[count];

            for (var i = 0; i < count; i += 2)
            {
                // Box-Muller transform
                var u1 = random.NextDouble();
                var u2 = random.NextDouble();
                var z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                var z1 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

                data[i] = mean + stdDev * (float)z0;
                if (i + 1 < count)
                {
                    data[i + 1] = mean + stdDev * (float)z1;
                }
            }

            return data;
        }

        public static int[] CreateIntegerSequence(int count, int start = 0, int step = 1)
        {
            var data = new int[count];
            for (var i = 0; i < count; i++)
            {
                data[i] = start + i * step;
            }
            return data;
        }

        public static byte[] CreateRandomBytes(int count, int seed = 42)
        {
            var random = new Random(seed);
            var data = new byte[count];
            random.NextBytes(data);
            return data;
        }
    }

    /// <summary>
    /// Create common Metal compute kernel definitions for testing
    /// </summary>
    public static class KernelTemplates
    {
        public static KernelDefinition SimpleAdd => new()
        {
            Name = "simple_add",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void simple_add(
                    device float* a [[buffer(0)]],
                    device float* b [[buffer(1)]],
                    device float* c [[buffer(2)]],
                    constant uint& n [[buffer(3)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= n) return;
                    c[id] = a[id] + b[id];
                }",
            EntryPoint = "simple_add"
        };

        public static KernelDefinition VectorScale => new()
        {
            Name = "vector_scale",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void vector_scale(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant float& scale [[buffer(2)]],
                    constant uint& n [[buffer(3)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= n) return;
                    output[id] = input[id] * scale;
                }",
            EntryPoint = "vector_scale"
        };

        public static KernelDefinition Reduction => new()
        {
            Name = "reduction_sum",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void reduction_sum(
                    device float* input [[buffer(0)]],
                    device atomic<float>* output [[buffer(1)]],
                    constant uint& n [[buffer(2)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= n) return;
                    atomic_fetch_add_explicit(output, input[id], memory_order_relaxed);
                }",
            EntryPoint = "reduction_sum"
        };

        public static KernelDefinition MemoryBenchmark => new()
        {
            Name = "memory_benchmark",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void memory_benchmark(
                    device float4* input [[buffer(0)]],
                    device float4* output [[buffer(1)]],
                    constant uint& iterations [[buffer(2)]],
                    constant uint& n [[buffer(3)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= n) return;
                    
                    float4 data = input[id];
                    for (uint i = 0; i < iterations; i++) {
                        data = data * 1.01f + float4(0.01f);
                    }
                    output[id] = data;
                }",
            EntryPoint = "memory_benchmark"
        };
    }

    /// <summary>
    /// Verify floating point results with appropriate tolerance
    /// </summary>
    public static void VerifyFloatArraysMatch(float[] expected, float[] actual, 
        float tolerance = 0.0001f, int maxElementsToCheck = 1000, string? context = null)
    {
        expected.Length.Should().Be(actual.Length, $"Array length mismatch{(context != null ? $" in {context}" : "")}");

        var elementsToCheck = Math.Min(maxElementsToCheck, expected.Length);
        var errorCount = 0;
        const int maxErrorsToReport = 10;
        var errorMessages = new List<string>();

        for (var i = 0; i < elementsToCheck; i++)
        {
            var diff = Math.Abs(expected[i] - actual[i]);
            if (diff > tolerance)
            {
                if (errorCount < maxErrorsToReport)
                {
                    errorMessages.Add($"Index {i}: expected {expected[i]}, actual {actual[i]}, diff {diff}");
                }
                errorCount++;
            }
        }

        if (errorCount > 0)
        {
            var message = $"Found {errorCount} mismatches out of {elementsToCheck} elements (tolerance: {tolerance})";
            if (context != null)
            {
                message = $"{context} - {message}";
            }

            if (errorMessages.Count > 0)
            {
                message += "\nFirst errors:\n" + string.Join("\n", errorMessages);
            }

            throw new AssertionFailedException(message);
        }
    }

    /// <summary>
    /// Verify integer arrays match exactly
    /// </summary>
    public static void VerifyIntArraysMatch(int[] expected, int[] actual, 
        int maxElementsToCheck = 1000, string? context = null)
    {
        expected.Length.Should().Be(actual.Length, $"Array length mismatch{(context != null ? $" in {context}" : "")}");

        var elementsToCheck = Math.Min(maxElementsToCheck, expected.Length);
        var errorCount = 0;
        const int maxErrorsToReport = 10;
        var errorMessages = new List<string>();

        for (var i = 0; i < elementsToCheck; i++)
        {
            if (expected[i] != actual[i])
            {
                if (errorCount < maxErrorsToReport)
                {
                    errorMessages.Add($"Index {i}: expected {expected[i]}, actual {actual[i]}");
                }
                errorCount++;
            }
        }

        if (errorCount > 0)
        {
            var message = $"Found {errorCount} mismatches out of {elementsToCheck} elements";
            if (context != null)
            {
                message = $"{context} - {message}";
            }

            if (errorMessages.Count > 0)
            {
                message += "\nFirst errors:\n" + string.Join("\n", errorMessages);
            }

            throw new AssertionFailedException(message);
        }
    }

    /// <summary>
    /// Calculate optimal launch parameters for Metal kernels
    /// </summary>
    public static (int threadgroups, int threadsPerThreadgroup) CalculateLaunchParams(
        int totalWork, int preferredThreadgroupSize = 256, int maxThreadgroupSize = 1024)
    {
        var threadsPerThreadgroup = Math.Min(preferredThreadgroupSize, maxThreadgroupSize);
        var threadgroups = (totalWork + threadsPerThreadgroup - 1) / threadsPerThreadgroup;
        return (threadgroups, threadsPerThreadgroup);
    }

    /// <summary>
    /// Create kernel arguments from parameters
    /// </summary>
    public static KernelArguments CreateKernelArguments(params object[] parameters)
    {
        var args = new KernelArguments();
        foreach (var param in parameters)
        {
            args.Add(param);
        }
        return args;
    }

    /// <summary>
    /// Measure kernel execution time
    /// </summary>
    public static async Task<TimeSpan> MeasureKernelExecutionAsync(
        ICompiledKernel kernel,
        (int, int, int) threadgroups,
        (int, int, int) threadsPerThreadgroup,
        KernelArguments args,
        IAccelerator accelerator,
        int iterations = 1)
    {
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            await kernel.LaunchAsync(threadgroups, threadsPerThreadgroup, args);
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        return iterations > 1 
            ? TimeSpan.FromMilliseconds(stopwatch.Elapsed.TotalMilliseconds / iterations)
            : stopwatch.Elapsed;
    }

    /// <summary>
    /// Generate performance statistics from timing data
    /// </summary>
    public static PerformanceStatistics CalculatePerformanceStatistics(IEnumerable<TimeSpan> timings)
    {
        var times = timings.Select(t => t.TotalMilliseconds).ToArray();
        
        if (times.Length == 0)
        {
            return new PerformanceStatistics();
        }

        Array.Sort(times);

        var mean = times.Average();
        var variance = times.Select(t => Math.Pow(t - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        return new PerformanceStatistics
        {
            Mean = TimeSpan.FromMilliseconds(mean),
            Min = TimeSpan.FromMilliseconds(times[0]),
            Max = TimeSpan.FromMilliseconds(times[^1]),
            Median = TimeSpan.FromMilliseconds(times[times.Length / 2]),
            StandardDeviation = TimeSpan.FromMilliseconds(stdDev),
            Count = times.Length
        };
    }
}

/// <summary>
/// Performance measurement utility for Metal tests
/// </summary>
public sealed class PerformanceMeasurement : IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private readonly string _operationName;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public PerformanceMeasurement(string operationName, ITestOutputHelper output)
    {
        _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public void Start() => _stopwatch.Restart();

    public void Stop() => _stopwatch.Stop();

    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    public void LogResults(long dataSize = 0, int operationCount = 1)
    {
        var avgTime = _stopwatch.Elapsed.TotalMilliseconds / operationCount;

        _output.WriteLine($"{_operationName} Performance:");
        _output.WriteLine($"  Total Time: {_stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Average Time: {avgTime:F2} ms");

        if (dataSize > 0)
        {
            var throughputGBps = dataSize / (_stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
            _output.WriteLine($"  Throughput: {throughputGBps:F2} GB/s");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            _disposed = true;
        }
    }
}

/// <summary>
/// Memory usage tracking utility for tests
/// </summary>
public sealed class PerformanceMemoryTracker : IDisposable
{
    private readonly long _initialGCMemory;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public PerformanceMemoryTracker(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _initialGCMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"Memory tracking started - Initial: {_initialGCMemory / (1024 * 1024):F1} MB");
    }

    public void LogCurrentUsage(string label = "Current")
    {
        if (_disposed) return;

        var currentMemory = GC.GetTotalMemory(false);
        var deltaMemory = currentMemory - _initialGCMemory;

        _output.WriteLine($"{label} Memory: {currentMemory / (1024 * 1024):F1} MB " +
                        $"(Δ{deltaMemory / (1024 * 1024):+F1;-F1;+0.0} MB)");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            var finalMemory = GC.GetTotalMemory(true);
            var totalDelta = finalMemory - _initialGCMemory;

            _output.WriteLine($"Memory tracking ended - Final: {finalMemory / (1024 * 1024):F1} MB " +
                            $"(Total Δ{totalDelta / (1024 * 1024):+F1;-F1;+0.0} MB)");
            _disposed = true;
        }
    }
}

/// <summary>
/// Performance statistics data structure
/// </summary>
public struct PerformanceStatistics
{
    public TimeSpan Mean { get; init; }
    public TimeSpan Min { get; init; }
    public TimeSpan Max { get; init; }
    public TimeSpan Median { get; init; }
    public TimeSpan StandardDeviation { get; init; }
    public int Count { get; init; }

    public override readonly string ToString()
    {
        return $"Count: {Count}, Mean: {Mean.TotalMilliseconds:F2}ms, " +
               $"Min: {Min.TotalMilliseconds:F2}ms, Max: {Max.TotalMilliseconds:F2}ms, " +
               $"Median: {Median.TotalMilliseconds:F2}ms, StdDev: {StandardDeviation.TotalMilliseconds:F2}ms";
    }
}

/// <summary>
/// Metal system information helper
/// </summary>
public static class MetalSystemInfo
{
    public static bool IsAppleSilicon()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && 
               RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }

    public static bool IsIntelMac()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && 
               RuntimeInformation.ProcessArchitecture == Architecture.X64;
    }

    public static Version GetMacOSVersion()
    {
        return Environment.OSVersion.Version;
    }

    public static string GetSystemDescription()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Not macOS";
        }

        var architecture = IsAppleSilicon() ? "Apple Silicon" : "Intel";
        var version = GetMacOSVersion();
        return $"macOS {version} ({architecture})";
    }

    public static Dictionary<string, object> GetSystemCapabilities()
    {
        return new Dictionary<string, object>
        {
            ["Platform"] = RuntimeInformation.OSDescription,
            ["Architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["IsAppleSilicon"] = IsAppleSilicon(),
            ["MacOSVersion"] = GetMacOSVersion().ToString(),
            ["ProcessorCount"] = Environment.ProcessorCount,
            ["WorkingSet"] = Environment.WorkingSet,
            ["Is64BitProcess"] = Environment.Is64BitProcess
        };
    }
}

// Extension methods for common test operations
public static class TestExtensions
{
    public static float[] CreateRandomData(this Random random, int count, float min = -1.0f, float max = 1.0f)
    {
        var data = new float[count];
        var range = max - min;

        for (var i = 0; i < count; i++)
        {
            data[i] = min + (float)random.NextDouble() * range;
        }
        return data;
    }

    public static void Shuffle<T>(this Random random, T[] array)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}