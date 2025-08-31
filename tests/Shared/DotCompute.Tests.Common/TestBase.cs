using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common;

/// <summary>
/// Base test class providing common functionality for all DotCompute tests.
/// Includes hardware detection, performance measurement, memory tracking, and test data generation.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    private readonly Stopwatch _testStopwatch;
    private readonly long _initialMemory;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the TestBase class.
    /// </summary>
    /// <param name="output">Test output helper for logging test information.</param>
    protected TestBase(ITestOutputHelper output)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        _testStopwatch = Stopwatch.StartNew();
        _initialMemory = GC.GetTotalMemory(false);
        
        LogTestStart();
    }

    #region Hardware Detection

    /// <summary>
    /// Checks if CUDA is available on the current system.
    /// </summary>
    /// <returns>True if CUDA is available, false otherwise.</returns>
    public static bool IsCudaAvailable()
    {
        try
        {
            // Check for CUDA runtime library
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return System.IO.File.Exists("cudart64_12.dll") || 
                       System.IO.File.Exists("cudart64_11.dll");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return System.IO.File.Exists("/usr/local/cuda/lib64/libcudart.so") ||
                       System.IO.File.Exists("/usr/lib/x86_64-linux-gnu/libcudart.so");
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if OpenCL is available on the current system.
    /// </summary>
    /// <returns>True if OpenCL is available, false otherwise.</returns>
    public static bool IsOpenClAvailable()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return System.IO.File.Exists("OpenCL.dll");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return System.IO.File.Exists("/usr/lib/x86_64-linux-gnu/libOpenCL.so.1") ||
                       System.IO.File.Exists("/usr/local/lib/libOpenCL.so");
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the CPU supports SIMD instructions (SSE, AVX).
    /// </summary>
    /// <returns>True if SIMD is supported, false otherwise.</returns>
    public static bool IsSIMDSupported()
    {
        try
        {
            // Basic check for x64 architecture which typically supports at least SSE2
            return RuntimeInformation.OSArchitecture == Architecture.X64 ||
                   RuntimeInformation.OSArchitecture == Architecture.X86;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current platform information.
    /// </summary>
    /// <returns>Platform description string.</returns>
    public static string PlatformInfo => $"{RuntimeInformation.OSDescription} - {RuntimeInformation.OSArchitecture}";

    #endregion

    #region Performance Measurement

    /// <summary>
    /// Measures the execution time of a synchronous action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <param name="iterations">Number of iterations to run (default: 1).</param>
    /// <returns>Elapsed time in milliseconds.</returns>
    protected double MeasureExecutionTime(Action action, int iterations = 1)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        // Warm up
        action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        
        stopwatch.Stop();
        
        var avgTime = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Output.WriteLine($"Execution time: {avgTime:F2}ms (avg over {iterations} iterations)");
        
        return avgTime;
    }

    /// <summary>
    /// Measures the execution time of an asynchronous function.
    /// </summary>
    /// <param name="func">The async function to measure.</param>
    /// <param name="iterations">Number of iterations to run (default: 1).</param>
    /// <returns>Elapsed time in milliseconds.</returns>
    protected async Task<double> MeasureExecutionTimeAsync(Func<Task> func, int iterations = 1)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        // Warm up
        await func();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            await func();
        }
        
        stopwatch.Stop();
        
        var avgTime = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Output.WriteLine($"Async execution time: {avgTime:F2}ms (avg over {iterations} iterations)");
        
        return avgTime;
    }

    /// <summary>
    /// Creates a performance benchmark context for detailed measurement.
    /// </summary>
    /// <param name="name">Name of the benchmark.</param>
    /// <returns>Performance measurement context.</returns>
    protected PerformanceContext CreatePerformanceContext(string name)
    {
        return new PerformanceContext(name, Output);
    }

    #endregion

    #region Test Data Generation

    /// <summary>
    /// Generates a random array of float values.
    /// </summary>
    /// <param name="size">Size of the array.</param>
    /// <param name="seed">Random seed for reproducible results.</param>
    /// <returns>Array of random float values.</returns>
    protected static float[] GenerateRandomFloats(int size, int seed = 42)
    {
        var random = new Random(seed);
        var data = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range: -1.0 to 1.0
        }
        
        return data;
    }

    /// <summary>
    /// Generates a random array of integer values.
    /// </summary>
    /// <param name="size">Size of the array.</param>
    /// <param name="minValue">Minimum value (inclusive).</param>
    /// <param name="maxValue">Maximum value (exclusive).</param>
    /// <param name="seed">Random seed for reproducible results.</param>
    /// <returns>Array of random integer values.</returns>
    protected static int[] GenerateRandomInts(int size, int minValue = 0, int maxValue = 1000, int seed = 42)
    {
        var random = new Random(seed);
        var data = new int[size];
        
        for (int i = 0; i < size; i++)
        {
            data[i] = random.Next(minValue, maxValue);
        }
        
        return data;
    }

    /// <summary>
    /// Generates a sequential array of values.
    /// </summary>
    /// <param name="size">Size of the array.</param>
    /// <param name="start">Starting value.</param>
    /// <param name="increment">Increment between values.</param>
    /// <returns>Array of sequential values.</returns>
    protected static float[] GenerateSequentialFloats(int size, float start = 0.0f, float increment = 1.0f)
    {
        var data = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            data[i] = start + i * increment;
        }
        
        return data;
    }

    #endregion

    #region Memory Tracking

    /// <summary>
    /// Gets the current memory usage delta since test start.
    /// </summary>
    /// <returns>Memory usage change in bytes.</returns>
    protected long GetMemoryUsageDelta()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var currentMemory = GC.GetTotalMemory(false);
        return currentMemory - _initialMemory;
    }

    /// <summary>
    /// Logs current memory usage information.
    /// </summary>
    protected void LogMemoryUsage()
    {
        var delta = GetMemoryUsageDelta();
        var current = GC.GetTotalMemory(false);
        
        Output.WriteLine($"Memory - Current: {current:N0} bytes, Delta: {delta:N0} bytes");
        
        if (Math.Abs(delta) > 1024 * 1024) // Warn for > 1MB delta
        {
            Output.WriteLine($"WARNING: Significant memory delta detected: {delta / 1024.0 / 1024.0:F2} MB");
        }
    }

    #endregion

    #region Test Lifecycle

    /// <summary>
    /// Logs test start information.
    /// </summary>
    private void LogTestStart()
    {
        Output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        Output.WriteLine($"Platform: {PlatformInfo}");
        Output.WriteLine($"CUDA Available: {IsCudaAvailable()}");
        Output.WriteLine($"OpenCL Available: {IsOpenClAvailable()}");
        Output.WriteLine($"SIMD Supported: {IsSIMDSupported()}");
        Output.WriteLine($"Initial Memory: {_initialMemory:N0} bytes");
    }

    /// <summary>
    /// Logs test completion information.
    /// </summary>
    protected void LogTestComplete()
    {
        _testStopwatch.Stop();
        
        Output.WriteLine($"Test completed in: {_testStopwatch.Elapsed.TotalMilliseconds:F2}ms");
        LogMemoryUsage();
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes resources and logs test completion.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            LogTestComplete();
            _testStopwatch?.Stop();
            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// Performance measurement context for detailed benchmarking.
/// </summary>
public sealed class PerformanceContext : IDisposable
{
    private readonly string _name;
    private readonly ITestOutputHelper _output;
    private readonly Stopwatch _stopwatch;
    private readonly long _initialMemory;
    private bool _disposed;

    internal PerformanceContext(string name, ITestOutputHelper output)
    {
        _name = name;
        _output = output;
        _stopwatch = Stopwatch.StartNew();
        _initialMemory = GC.GetTotalMemory(false);
        
        _output.WriteLine($"Performance measurement started: {_name}");
    }

    /// <summary>
    /// Adds a checkpoint measurement.
    /// </summary>
    /// <param name="checkpoint">Checkpoint name.</param>
    public void Checkpoint(string checkpoint)
    {
        var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
        var currentMemory = GC.GetTotalMemory(false);
        var memoryDelta = currentMemory - _initialMemory;
        
        _output.WriteLine($"Checkpoint '{checkpoint}': {elapsed:F2}ms, Memory Delta: {memoryDelta:N0} bytes");
    }

    /// <summary>
    /// Disposes the performance context and logs final results.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            var totalTime = _stopwatch.Elapsed.TotalMilliseconds;
            var finalMemory = GC.GetTotalMemory(false);
            var totalMemoryDelta = finalMemory - _initialMemory;
            
            _output.WriteLine($"Performance measurement completed: {_name}");
            _output.WriteLine($"Total time: {totalTime:F2}ms, Total memory delta: {totalMemoryDelta:N0} bytes");
            
            _disposed = true;
        }
    }
}