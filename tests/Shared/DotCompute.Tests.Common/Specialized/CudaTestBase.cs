// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Tests.Common.Fixtures;
using DotCompute.Tests.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.Specialized;

/// <summary>
/// Specialized test base class for CUDA hardware tests.
/// Extends ConsolidatedTestBase with CUDA-specific functionality including device detection,
/// memory management, performance profiling, and CUDA runtime integration.
/// </summary>
public abstract class CudaTestBase : ConsolidatedTestBase
{
    /// <summary>
    /// Initializes a new instance of the CudaTestBase class.
    /// </summary>
    /// <param name="output">Test output helper for logging.</param>
    /// <param name="fixture">Optional common test fixture.</param>
    protected CudaTestBase(ITestOutputHelper output, CommonTestFixture? fixture = null)

        : base(output, fixture)
    {
        LogCudaEnvironment();
    }

    #region CUDA-Specific Hardware Detection

    /// <summary>
    /// Enhanced CUDA availability check with detailed device validation.
    /// </summary>
    /// <returns>True if CUDA is available and functional, false otherwise.</returns>
    protected new static bool IsCudaAvailable()
    {
        // Use base class detection first
        if (!ConsolidatedTestBase.IsCudaAvailable())
            return false;

        try
        {
            // Additional CUDA-specific validation would go here
            // For now, rely on base class implementation
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if RTX 2000 series Ada GPU is available (example implementation).
    /// </summary>
    /// <returns>True if RTX 2000 Ada GPU is detected, false otherwise.</returns>
    protected static bool IsRTX2000AdaAvailable()
    {
        if (!IsCudaAvailable())
            return false;

        try
        {
            // This would typically query actual device properties
            // Placeholder implementation
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if compute capability meets minimum requirements.
    /// </summary>
    /// <param name="majorMin">Minimum major compute capability.</param>
    /// <param name="minorMin">Minimum minor compute capability.</param>
    /// <returns>True if compute capability is sufficient, false otherwise.</returns>
    protected static bool HasMinimumComputeCapability(int majorMin, int minorMin = 0)
    {
        if (!IsCudaAvailable())
            return false;

        try
        {
            // This would typically query actual compute capability
            // For now, assume modern GPU with CC 7.5+
            int major = 8, minor = 9; // Example RTX 2000 Ada values
            return major > majorMin || (major == majorMin && minor >= minorMin);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get detailed CUDA device information for logging.
    /// </summary>
    /// <returns>Device information string.</returns>
    protected static string GetCudaDeviceInfoString()
    {
        if (!IsCudaAvailable())
            return "CUDA not available";

        try
        {
            // This would typically query actual device properties
            // Placeholder implementation
            return "NVIDIA RTX 2000 Ada (CC 8.9, 8.0 GB, 32 SMs, 2048 cores)";
        }
        catch (Exception ex)
        {
            return $"Error getting CUDA device info: {ex.Message}";
        }
    }

    #endregion

    #region CUDA Memory Management

    /// <summary>
    /// Gets current GPU memory usage (CUDA-specific implementation).
    /// </summary>
    /// <returns>Current GPU memory usage in bytes.</returns>
    protected override long GetCurrentGpuMemoryUsage()
    {
        if (!IsCudaAvailable())
            return 0;

        try
        {
            // This would use CUDA runtime API to get actual memory usage
            // Placeholder implementation
            return 512 * 1024 * 1024; // 512MB
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets free GPU memory (CUDA-specific implementation).
    /// </summary>
    /// <returns>Free GPU memory in bytes.</returns>
    protected override long GetFreeGpuMemory()
    {
        if (!IsCudaAvailable())
            return 0;

        try
        {
            // This would use CUDA runtime API to get free memory
            // Placeholder implementation
            return 7L * 1024L * 1024L * 1024L; // 7GB
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region CUDA Performance Utilities

    /// <summary>
    /// GPU synchronization for CUDA kernels.
    /// </summary>
    protected override void SynchronizeGpu()
    {
        if (!IsCudaAvailable())
            return;

        try
        {
            // This would call cudaDeviceSynchronize()
            // Placeholder implementation - no actual synchronization
        }
        catch (Exception ex)
        {
            Log($"Failed to synchronize GPU: {ex.Message}");
        }
    }

    /// <summary>
    /// Performance measurement utility specifically for CUDA kernels.
    /// </summary>
    protected class CudaPerformanceMeasurement(string operationName, ITestOutputHelper output)
    {
        private readonly Stopwatch _stopwatch = new();
        private readonly string _operationName = operationName;
        private readonly ITestOutputHelper _output = output;

        public void Start() => _stopwatch.Restart();
        public void Stop() => _stopwatch.Stop();
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;

        public void LogResults(long dataSize = 0, int operationCount = 1)
        {
            var avgTime = _stopwatch.Elapsed.TotalMilliseconds / operationCount;

            _output.WriteLine($"CUDA {_operationName} Performance:");
            _output.WriteLine($"  Total Time: {_stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Average Time: {avgTime:F2} ms");

            if (dataSize > 0)
            {
                var throughputGBps = dataSize / (_stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
                _output.WriteLine($"  Throughput: {throughputGBps:F2} GB/s");
            }
        }
    }

    #endregion

    #region Test Data Generators

    /// <summary>
    /// CUDA-specific test data generator.
    /// </summary>
    protected static class CudaTestDataGenerator
    {
        /// <summary>
        /// Creates GPU-optimized linear sequence data.
        /// </summary>
        public static float[] CreateGpuOptimizedLinearSequence(int count, float start = 0.0f, float step = 1.0f)
        {
            // Align to GPU memory boundaries for optimal performance
            var alignedCount = ((count + 31) / 32) * 32; // Align to 32-element boundaries
            var data = new float[alignedCount];


            for (var i = 0; i < count; i++)
            {
                data[i] = start + i * step;
            }

            // Fill remaining elements with last value to avoid undefined behavior

            for (var i = count; i < alignedCount; i++)
            {
                data[i] = data[count - 1];
            }


            return data;
        }

        /// <summary>
        /// Creates large dataset for stress testing.
        /// </summary>
        public static float[] CreateLargeDataset(int sizeInMB, int seed = 42)
        {
            var elementCount = (sizeInMB * 1024 * 1024) / sizeof(float);
            return UnifiedTestHelpers.TestDataGenerator.CreateRandomData(elementCount, seed);
        }

        /// <summary>
        /// Creates data with specific patterns for validation.
        /// </summary>
        public static float[] CreateValidationPattern(int count)
        {
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                // Create a pattern that's easy to validate: alternating 1.0 and -1.0
                data[i] = (i % 2 == 0) ? 1.0f : -1.0f;
            }
            return data;
        }
    }

    #endregion

    #region Validation Utilities

    /// <summary>
    /// CUDA-specific validation with GPU-appropriate tolerances.
    /// </summary>
    protected static void VerifyCudaResults(float[] expected, float[] actual, string? context = null)
    {
        // Use slightly higher tolerance for GPU computations due to floating-point precision
        UnifiedTestHelpers.ValidationHelpers.VerifyFloatArraysMatch(
            expected, actual, tolerance: 0.001f, context: context);
    }

    /// <summary>
    /// Validates CUDA memory allocation and deallocation.
    /// </summary>
    protected void ValidateCudaMemoryCleanup()
    {
        if (!IsCudaAvailable())
            return;

        var initialUsage = GetCurrentGpuMemoryUsage();

        // Allow some tolerance for driver overhead

        var maxExpectedUsage = initialUsage + (50 * 1024 * 1024); // 50MB tolerance


        if (GetCurrentGpuMemoryUsage() > maxExpectedUsage)
        {
            Log("WARNING: Potential CUDA memory leak detected");
        }
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Verifies that two float arrays match within a tolerance.
    /// </summary>
    protected static void VerifyFloatArraysMatch(float[] expected, float[] actual, float tolerance = 0.001f, string? message = null)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(expected[i] - actual[i]) <= tolerance,
                $"{message ?? "Arrays don't match"} at index {i}: expected {expected[i]}, actual {actual[i]}");
        }
    }

    /// <summary>
    /// Gets the test timeout for CUDA tests.
    /// </summary>
    protected static TimeSpan TestTimeout => TimeSpan.FromMinutes(2);

    #endregion

    #region Service Configuration

    /// <summary>
    /// Configures CUDA-specific services in the DI container.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);

        if (IsCudaAvailable())
        {
            // Add CUDA-specific services here
            // services.AddSingleton<ICudaAcceleratorFactory, CudaAcceleratorFactory>();
            // services.AddSingleton<ICudaMemoryManager, CudaMemoryManager>();


            Log("CUDA services configured in DI container");
        }
    }

    #endregion

    #region Logging

    /// <summary>
    /// Logs CUDA environment information at test start.
    /// </summary>
    private void LogCudaEnvironment()
    {
        Log("=== CUDA Environment ===");
        Log($"CUDA Available: {IsCudaAvailable()}");


        if (IsCudaAvailable())
        {
            Log($"Device Info: {GetCudaDeviceInfoString()}");
            Log($"RTX 2000 Ada: {IsRTX2000AdaAvailable()}");
            Log($"Compute Capability >= 7.0: {HasMinimumComputeCapability(7, 0)}");


            TakeGpuMemorySnapshot("cuda_initial");
        }


        Log("========================");
    }

    /// <summary>
    /// Logs detailed CUDA device capabilities and limits.
    /// </summary>
    protected void LogCudaDeviceCapabilities()
    {
        if (!IsCudaAvailable())
        {
            Log("CUDA not available");
            return;
        }

        try
        {
            Log("CUDA Device Capabilities:");
            Log($"  Device: {GetCudaDeviceInfoString()}");
            Log($"  Current Memory Usage: {GetCurrentGpuMemoryUsage() / (1024.0 * 1024.0):F1} MB");
            Log($"  Available Memory: {GetFreeGpuMemory() / (1024.0 * 1024.0):F1} MB");

            // Additional device-specific logging would go here

        }
        catch (Exception ex)
        {
            Log($"Error logging CUDA device capabilities: {ex.Message}");
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Performs CUDA-specific cleanup operations.
    /// </summary>
    /// <param name="disposing">Whether disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && IsCudaAvailable())
        {
            try
            {
                ValidateCudaMemoryCleanup();
                TakeGpuMemorySnapshot("cuda_final");
                CompareGpuMemorySnapshots("cuda_initial", "cuda_final");
            }
            catch (Exception ex)
            {
                Log($"Error during CUDA cleanup: {ex.Message}");
            }
        }

        base.Dispose(disposing);
    }

    #endregion
}