using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common;

/// <summary>
/// Exception thrown to skip a test when conditions are not met.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}

/// <summary>
/// GPU-specific test base class that extends TestBase with GPU hardware detection,
/// memory tracking, and specialized testing utilities for accelerated computing scenarios.
/// </summary>
public abstract class GpuTestBase : TestBase
{
    private readonly Dictionary<string, GpuMemorySnapshot> _memorySnapshots;

    /// <summary>
    /// Initializes a new instance of the GpuTestBase class.
    /// </summary>
    /// <param name="output">Test output helper for logging GPU-specific test information.</param>
    protected GpuTestBase(ITestOutputHelper output) : base(output)
    {
        _memorySnapshots = new Dictionary<string, GpuMemorySnapshot>();
        LogGpuEnvironment();
    }

    #region GPU Hardware Detection

    /// <summary>
    /// Checks if NVIDIA GPU hardware is available with detailed capability detection.
    /// </summary>
    /// <returns>GPU capability information.</returns>
    public static GpuCapabilities GetNvidiaCapabilities()
    {
        var capabilities = new GpuCapabilities
        {
            HasCuda = IsCudaAvailable(),
            HasNvidiaGpu = false,
            ComputeCapability = "Unknown",
            TotalMemoryMB = 0,
            SupportsUnifiedMemory = false,
            SupportsDynamicParallelism = false
        };

        try
        {
            // Try to detect NVIDIA GPU through various methods
            if (capabilities.HasCuda)
            {
                // This would typically use CUDA runtime APIs
                // For now, we'll use basic detection
                capabilities.HasNvidiaGpu = DetectNvidiaGpu();
                
                if (capabilities.HasNvidiaGpu)
                {
                    capabilities.ComputeCapability = GetComputeCapability();
                    capabilities.TotalMemoryMB = GetGpuMemoryMB();
                    capabilities.SupportsUnifiedMemory = CheckUnifiedMemorySupport();
                    capabilities.SupportsDynamicParallelism = CheckDynamicParallelismSupport();
                }
            }
        }
        catch (Exception ex)
        {
            // Log exception but don't fail the capability detection
            Debug.WriteLine($"GPU capability detection failed: {ex.Message}");
        }

        return capabilities;
    }

    /// <summary>
    /// Checks if AMD GPU hardware is available with OpenCL support.
    /// </summary>
    /// <returns>True if AMD GPU is available, false otherwise.</returns>
    public static bool IsAmdGpuAvailable()
    {
        try
        {
            return IsOpenClAvailable() && DetectAmdGpu();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if Intel GPU hardware is available.
    /// </summary>
    /// <returns>True if Intel GPU is available, false otherwise.</returns>
    public static bool IsIntelGpuAvailable()
    {
        try
        {
            return DetectIntelGpu();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets comprehensive GPU system information.
    /// </summary>
    /// <returns>GPU system information.</returns>
    public static GpuSystemInfo GetGpuSystemInfo()
    {
        return new GpuSystemInfo
        {
            NvidiaCapabilities = GetNvidiaCapabilities(),
            HasAmdGpu = IsAmdGpuAvailable(),
            HasIntelGpu = IsIntelGpuAvailable(),
            OpenClAvailable = IsOpenClAvailable(),
            Platform = PlatformInfo
        };
    }

    #endregion

    #region Test Skip Conditions

    /// <summary>
    /// Skips the test if CUDA is not available.
    /// </summary>
    protected void SkipIfNoCuda()
    {
        if (!IsCudaAvailable()) 
        {
            throw new SkipException("CUDA is not available on this system");
        }
    }

    /// <summary>
    /// Skips the test if no GPU hardware is available.
    /// </summary>
    protected void SkipIfNoGpu()
    {
        var hasAnyGpu = GetNvidiaCapabilities().HasNvidiaGpu || 
                        IsAmdGpuAvailable() || 
                        IsIntelGpuAvailable();
        
        if (!hasAnyGpu) 
        {
            throw new SkipException("No GPU hardware detected on this system");
        }
    }

    /// <summary>
    /// Skips the test if OpenCL is not available.
    /// </summary>
    protected void SkipIfNoOpenCL()
    {
        if (!IsOpenClAvailable()) 
        {
            throw new SkipException("OpenCL is not available on this system");
        }
    }

    /// <summary>
    /// Skips the test if the GPU doesn't meet minimum compute capability requirements.
    /// </summary>
    /// <param name="minimumComputeCapability">Minimum required compute capability (e.g., "3.5").</param>
    protected void SkipIfInsufficientComputeCapability(string minimumComputeCapability)
    {
        var capabilities = GetNvidiaCapabilities();
        
        if (!capabilities.HasNvidiaGpu)
        {
            throw new SkipException("No NVIDIA GPU detected");
        }

        // Simple version comparison (would need more sophisticated logic for production)
        var hasRequiredCapability = string.Compare(capabilities.ComputeCapability, 
                                                 minimumComputeCapability, 
                                                 StringComparison.Ordinal) >= 0;
        
        if (!hasRequiredCapability) 
        {
            throw new SkipException($"GPU compute capability {capabilities.ComputeCapability} is below required {minimumComputeCapability}");
        }
    }

    /// <summary>
    /// Skips the test if insufficient GPU memory is available.
    /// </summary>
    /// <param name="requiredMemoryMB">Required memory in megabytes.</param>
    protected void SkipIfInsufficientGpuMemory(int requiredMemoryMB)
    {
        var capabilities = GetNvidiaCapabilities();
        
        if (capabilities.TotalMemoryMB < requiredMemoryMB) 
        {
            throw new SkipException($"Insufficient GPU memory: {capabilities.TotalMemoryMB}MB available, {requiredMemoryMB}MB required");
        }
    }

    #endregion

    #region GPU Memory Tracking

    /// <summary>
    /// Takes a snapshot of current GPU memory usage.
    /// </summary>
    /// <param name="name">Name for the memory snapshot.</param>
    protected void TakeGpuMemorySnapshot(string name)
    {
        try
        {
            var snapshot = new GpuMemorySnapshot
            {
                Name = name,
                Timestamp = DateTime.Now,
                SystemMemory = GC.GetTotalMemory(false),
                GpuMemoryUsed = GetCurrentGpuMemoryUsage(),
                GpuMemoryFree = GetFreeGpuMemory()
            };

            _memorySnapshots[name] = snapshot;
            
            Output.WriteLine($"GPU Memory Snapshot '{name}': " +
                           $"GPU Used: {snapshot.GpuMemoryUsed:N0} bytes, " +
                           $"GPU Free: {snapshot.GpuMemoryFree:N0} bytes, " +
                           $"System: {snapshot.SystemMemory:N0} bytes");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Failed to take GPU memory snapshot '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Compares two GPU memory snapshots and logs the differences.
    /// </summary>
    /// <param name="beforeSnapshot">Name of the before snapshot.</param>
    /// <param name="afterSnapshot">Name of the after snapshot.</param>
    protected void CompareGpuMemorySnapshots(string beforeSnapshot, string afterSnapshot)
    {
        if (!_memorySnapshots.TryGetValue(beforeSnapshot, out var before) ||
            !_memorySnapshots.TryGetValue(afterSnapshot, out var after))
        {
            Output.WriteLine("Cannot compare snapshots: one or both snapshots not found");
            return;
        }

        var gpuMemoryDelta = after.GpuMemoryUsed - before.GpuMemoryUsed;
        var systemMemoryDelta = after.SystemMemory - before.SystemMemory;
        var timeDelta = (after.Timestamp - before.Timestamp).TotalMilliseconds;

        Output.WriteLine($"Memory Comparison ({beforeSnapshot} -> {afterSnapshot}):");
        Output.WriteLine($"  GPU Memory Delta: {gpuMemoryDelta:N0} bytes ({gpuMemoryDelta / 1024.0 / 1024.0:F2} MB)");
        Output.WriteLine($"  System Memory Delta: {systemMemoryDelta:N0} bytes ({systemMemoryDelta / 1024.0 / 1024.0:F2} MB)");
        Output.WriteLine($"  Time Delta: {timeDelta:F2}ms");

        // Warn about potential memory leaks
        if (Math.Abs(gpuMemoryDelta) > 10 * 1024 * 1024) // > 10MB
        {
            Output.WriteLine($"WARNING: Large GPU memory delta detected: {gpuMemoryDelta / 1024.0 / 1024.0:F2} MB");
        }
    }

    /// <summary>
    /// Validates that GPU memory usage is within acceptable limits.
    /// </summary>
    /// <param name="maxAllowedMemoryMB">Maximum allowed memory usage in MB.</param>
    protected void ValidateGpuMemoryUsage(int maxAllowedMemoryMB)
    {
        try
        {
            var currentUsage = GetCurrentGpuMemoryUsage();
            var currentUsageMB = currentUsage / 1024.0 / 1024.0;

            Output.WriteLine($"Current GPU memory usage: {currentUsageMB:F2} MB");

            if (currentUsageMB > maxAllowedMemoryMB)
            {
                throw new InvalidOperationException(
                    $"GPU memory usage ({currentUsageMB:F2} MB) exceeds limit ({maxAllowedMemoryMB} MB)");
            }
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            Output.WriteLine($"GPU memory validation failed: {ex.Message}");
        }
    }

    #endregion

    #region GPU Performance Utilities

    /// <summary>
    /// Measures GPU kernel execution time with proper synchronization.
    /// </summary>
    /// <param name="kernelExecution">Action representing kernel execution.</param>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <returns>Average execution time in milliseconds.</returns>
    protected double MeasureGpuKernelTime(Action kernelExecution, int iterations = 1)
    {
        ArgumentNullException.ThrowIfNull(kernelExecution);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        // Warm-up run
        kernelExecution();
        SynchronizeGpu();

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            kernelExecution();
        }

        SynchronizeGpu(); // Ensure all GPU work is complete
        stopwatch.Stop();

        var avgTime = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Output.WriteLine($"GPU kernel execution time: {avgTime:F2}ms (avg over {iterations} iterations)");

        return avgTime;
    }

    /// <summary>
    /// Calculates theoretical bandwidth utilization for memory operations.
    /// </summary>
    /// <param name="bytesTransferred">Total bytes transferred.</param>
    /// <param name="elapsedMs">Elapsed time in milliseconds.</param>
    /// <returns>Bandwidth in GB/s.</returns>
    protected double CalculateBandwidth(long bytesTransferred, double elapsedMs)
    {
        if (elapsedMs <= 0) return 0;

        var bandwidthGBps = (bytesTransferred / (1024.0 * 1024.0 * 1024.0)) / (elapsedMs / 1000.0);
        
        Output.WriteLine($"Bandwidth: {bandwidthGBps:F2} GB/s " +
                        $"({bytesTransferred:N0} bytes in {elapsedMs:F2}ms)");

        return bandwidthGBps;
    }

    #endregion

    #region Private Helper Methods

    private void LogGpuEnvironment()
    {
        var gpuInfo = GetGpuSystemInfo();
        
        Output.WriteLine("=== GPU Environment ===");
        Output.WriteLine($"NVIDIA GPU: {gpuInfo.NvidiaCapabilities.HasNvidiaGpu}");
        
        if (gpuInfo.NvidiaCapabilities.HasNvidiaGpu)
        {
            Output.WriteLine($"  Compute Capability: {gpuInfo.NvidiaCapabilities.ComputeCapability}");
            Output.WriteLine($"  Total Memory: {gpuInfo.NvidiaCapabilities.TotalMemoryMB} MB");
            Output.WriteLine($"  Unified Memory: {gpuInfo.NvidiaCapabilities.SupportsUnifiedMemory}");
            Output.WriteLine($"  Dynamic Parallelism: {gpuInfo.NvidiaCapabilities.SupportsDynamicParallelism}");
        }
        
        Output.WriteLine($"AMD GPU: {gpuInfo.HasAmdGpu}");
        Output.WriteLine($"Intel GPU: {gpuInfo.HasIntelGpu}");
        Output.WriteLine($"OpenCL: {gpuInfo.OpenClAvailable}");
        Output.WriteLine("========================");
    }

    private static bool DetectNvidiaGpu()
    {
        // Placeholder for actual NVIDIA GPU detection logic
        // In production, this would use CUDA runtime APIs
        return IsCudaAvailable();
    }

    private static bool DetectAmdGpu()
    {
        // Placeholder for AMD GPU detection logic
        // In production, this would check for AMD drivers/runtime
        return false;
    }

    private static bool DetectIntelGpu()
    {
        // Placeholder for Intel GPU detection logic
        // In production, this would check for Intel GPU drivers
        return false;
    }

    private static string GetComputeCapability()
    {
        // Placeholder - would query actual compute capability
        return "7.5"; // Example compute capability
    }

    private static int GetGpuMemoryMB()
    {
        // Placeholder - would query actual GPU memory
        return 8192; // Example 8GB
    }

    private static bool CheckUnifiedMemorySupport()
    {
        // Placeholder - would check for unified memory support
        return true;
    }

    private static bool CheckDynamicParallelismSupport()
    {
        // Placeholder - would check for dynamic parallelism support
        return true;
    }

    private static long GetCurrentGpuMemoryUsage()
    {
        // Placeholder - would query actual GPU memory usage
        return 1024 * 1024 * 256; // Example 256MB
    }

    private static long GetFreeGpuMemory()
    {
        // Placeholder - would query actual free GPU memory
        return 1024L * 1024L * 1024L * 7L; // Example 7GB free
    }

    private static void SynchronizeGpu()
    {
        // Placeholder - would perform actual GPU synchronization
        // In CUDA: cudaDeviceSynchronize()
        // In OpenCL: clFinish()
    }

    #endregion
}

/// <summary>
/// Represents GPU capability information.
/// </summary>
public class GpuCapabilities
{
    public bool HasCuda { get; set; }
    public bool HasNvidiaGpu { get; set; }
    public string ComputeCapability { get; set; } = string.Empty;
    public int TotalMemoryMB { get; set; }
    public bool SupportsUnifiedMemory { get; set; }
    public bool SupportsDynamicParallelism { get; set; }
}

/// <summary>
/// Represents comprehensive GPU system information.
/// </summary>
public class GpuSystemInfo
{
    public GpuCapabilities NvidiaCapabilities { get; set; } = new();
    public bool HasAmdGpu { get; set; }
    public bool HasIntelGpu { get; set; }
    public bool OpenClAvailable { get; set; }
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// Represents a GPU memory usage snapshot.
/// </summary>
public class GpuMemorySnapshot
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long SystemMemory { get; set; }
    public long GpuMemoryUsed { get; set; }
    public long GpuMemoryFree { get; set; }
}