// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Tests.Common.Fixtures;
using DotCompute.Tests.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.Specialized;

/// <summary>
/// Specialized test base class for Metal hardware tests on macOS.
/// Extends ConsolidatedTestBase with Metal-specific functionality including device detection,
/// unified memory management, performance profiling, and Metal framework integration.
/// </summary>
public abstract class MetalTestBase : ConsolidatedTestBase
{
    /// <summary>
    /// Initializes a new instance of the MetalTestBase class.
    /// </summary>
    /// <param name="output">Test output helper for logging.</param>
    /// <param name="fixture">Optional common test fixture.</param>
    protected MetalTestBase(ITestOutputHelper output, CommonTestFixture? fixture = null)

        : base(output, fixture)
    {
        LogMetalEnvironment();
    }

    #region Metal-Specific Hardware Detection

    /// <summary>
    /// Enhanced Metal availability check with device validation.
    /// </summary>
    /// <returns>True if Metal is available and functional, false otherwise.</returns>
    protected new static bool IsMetalAvailable() => ConsolidatedTestBase.IsMetalAvailable();

    /// <summary>
    /// Check if running on Apple Silicon (M1/M2/M3/M4).
    /// </summary>
    /// <returns>True if running on Apple Silicon, false otherwise.</returns>
    protected new static bool IsAppleSilicon() => ConsolidatedTestBase.IsAppleSilicon();

    /// <summary>
    /// Get the macOS version.
    /// </summary>
    /// <returns>macOS version.</returns>
    protected static Version GetMacOSVersion() => Environment.OSVersion.Version;

    /// <summary>
    /// Check if macOS version meets minimum requirements for Metal features.
    /// </summary>
    /// <param name="minimumVersion">Minimum required macOS version.</param>
    /// <returns>True if version requirement is met, false otherwise.</returns>
    protected static bool HasMinimumMacOSVersion(Version minimumVersion) => GetMacOSVersion() >= minimumVersion;

    /// <summary>
    /// Get detailed Metal device information for logging.
    /// </summary>
    /// <returns>Device information string.</returns>
    protected static string GetMetalDeviceInfoString()
    {
        if (!IsMetalAvailable())
            return "Metal not available";

        try
        {
            // This would typically query actual Metal device properties
            // Placeholder implementation for cross-platform compatibility
            var deviceName = IsAppleSilicon() ? "Apple M-Series GPU" : "Intel Integrated Graphics";
            var memoryInfo = IsAppleSilicon() ? "Unified Memory" : "Shared Memory";


            return $"{deviceName} ({memoryInfo}, macOS {GetMacOSVersion()})";
        }
        catch (Exception ex)
        {
            return $"Error getting Metal device info: {ex.Message}";
        }
    }

    #endregion

    #region Metal Memory Management

    /// <summary>
    /// Gets current GPU memory usage (Metal unified memory implementation).
    /// </summary>
    /// <returns>Current GPU memory usage in bytes.</returns>
    protected override long GetCurrentGpuMemoryUsage()
    {
        if (!IsMetalAvailable())
            return 0;

        try
        {
            // Metal uses unified memory - return system memory usage as proxy
            return GC.GetTotalMemory(false);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets free GPU memory (Metal unified memory implementation).
    /// </summary>
    /// <returns>Free GPU memory in bytes.</returns>
    protected override long GetFreeGpuMemory()
    {
        if (!IsMetalAvailable())
            return 0;

        try
        {
            // Metal uses unified memory - estimate based on system memory
            var totalSystem = GC.GetTotalMemory(true);
            var availableSystem = GC.GetTotalMemory(false);
            return Math.Max(0, availableSystem - totalSystem);
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region Metal Performance Utilities

    /// <summary>
    /// GPU synchronization for Metal command buffers.
    /// </summary>
    protected override void SynchronizeGpu()
    {
        if (!IsMetalAvailable())
            return;

        try
        {
            // This would typically call Metal command buffer commit and wait
            // Placeholder implementation
        }
        catch (Exception ex)
        {
            Log($"Failed to synchronize Metal GPU: {ex.Message}");
        }
    }

    /// <summary>
    /// Performance measurement utility specifically for Metal operations.
    /// </summary>
    protected class MetalPerformanceMeasurement(string operationName, ITestOutputHelper output)
    {
        private readonly Stopwatch _stopwatch = new();
        private readonly string _operationName = operationName;
        private readonly ITestOutputHelper _output = output;
        /// <summary>
        /// Performs start.
        /// </summary>

        public void Start() => _stopwatch.Restart();
        /// <summary>
        /// Performs stop.
        /// </summary>
        public void Stop() => _stopwatch.Stop();
        /// <summary>
        /// Gets or sets the elapsed time.
        /// </summary>
        /// <value>The elapsed time.</value>
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;
        /// <summary>
        /// Performs log results.
        /// </summary>
        /// <param name="dataSize">The data size.</param>
        /// <param name="operationCount">The operation count.</param>

        public void LogResults(long dataSize = 0, int operationCount = 1)
        {
            var avgTime = _stopwatch.Elapsed.TotalMilliseconds / operationCount;

            _output.WriteLine($"Metal {_operationName} Performance:");
            _output.WriteLine($"  Total Time: {_stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Average Time: {avgTime:F2} ms");

            if (dataSize > 0)
            {
                var throughputGBps = dataSize / (_stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
                var throughputMBps = dataSize / (_stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
                _output.WriteLine($"  Throughput: {throughputGBps:F2} GB/s ({throughputMBps:F1} MB/s)");
            }
        }
    }

    #endregion

    #region Test Data Generators

    /// <summary>
    /// Metal-specific test data generator optimized for unified memory.
    /// </summary>
    protected static class MetalTestDataGenerator
    {
        /// <summary>
        /// Creates data aligned for optimal Metal performance.
        /// </summary>
        public static float[] CreateMetalOptimizedData(int count, int seed = 42)
        {
            // Align to SIMD boundaries for Apple Silicon optimization
            var alignedCount = ((count + 7) / 8) * 8; // Align to 8-element SIMD boundaries
            var data = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(alignedCount, seed);


            return data;
        }

        /// <summary>
        /// Creates 2D texture-like data for Metal compute shaders.
        /// </summary>
        public static float[] CreateTextureData(int width, int height, bool pattern = false, int seed = 42)
        {
            var data = new float[width * height * 4]; // RGBA format


            if (pattern)
            {
                // Create a checkerboard pattern for validation
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var index = (y * width + x) * 4;
                        var isBlack = (x / 8 + y / 8) % 2 == 0;


                        data[index] = isBlack ? 0.0f : 1.0f;     // R
                        data[index + 1] = isBlack ? 0.0f : 1.0f; // G
                        data[index + 2] = isBlack ? 0.0f : 1.0f; // B
                        data[index + 3] = 1.0f;                  // A
                    }
                }
            }
            else
            {
                var random = new Random(seed);
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = (float)random.NextDouble();
                }
            }


            return data;
        }

        /// <summary>
        /// Creates large unified memory dataset for Apple Silicon testing.
        /// </summary>
        public static float[] CreateUnifiedMemoryDataset(int sizeInMB, int seed = 42)
        {
            var elementCount = (sizeInMB * 1024 * 1024) / sizeof(float);
            return CreateMetalOptimizedData(elementCount, seed);
        }

        /// <summary>
        /// Creates matrix data optimized for Metal Performance Shaders.
        /// </summary>
        public static float[] CreateMPSMatrix(int rows, int cols, bool identity = false, int seed = 42) => UnifiedTestHelpers.TestDataGenerator.CreateMatrix(rows, cols, identity, seed);
    }

    #endregion

    #region Validation Utilities

    /// <summary>
    /// Metal-specific validation with appropriate tolerances for unified memory.
    /// </summary>
    protected static void VerifyMetalResults(float[] expected, float[] actual, string? context = null)
    {
        // Use moderate tolerance for Metal computations
        UnifiedTestHelpers.ValidationHelpers.VerifyFloatArraysMatch(
            expected, actual, tolerance: 0.0001f, context: context);
    }

    /// <summary>
    /// Validates Metal unified memory usage patterns.
    /// </summary>
    protected void ValidateMetalMemoryUsage()
    {
        if (!IsMetalAvailable())
            return;

        try
        {
            var currentUsage = GetCurrentGpuMemoryUsage();
            var currentUsageMB = currentUsage / (1024.0 * 1024.0);


            Log($"Metal unified memory usage: {currentUsageMB:F1} MB");

            // Metal uses unified memory, so we primarily track system memory

            LogMemoryUsage();
        }
        catch (Exception ex)
        {
            Log($"Error validating Metal memory usage: {ex.Message}");
        }
    }

    #endregion

    #region Service Configuration

    /// <summary>
    /// Configures Metal-specific services in the DI container.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);

        if (IsMetalAvailable())
        {
            // Add Metal-specific services here
            // services.AddSingleton<IMetalBackendFactory, MetalBackendFactory>();
            // services.AddSingleton<IMetalMemoryManager, MetalMemoryManager>();


            Log("Metal services configured in DI container");
        }
    }

    #endregion

    #region Logging

    /// <summary>
    /// Logs Metal environment information at test start.
    /// </summary>
    private void LogMetalEnvironment()
    {
        Log("=== Metal Environment ===");
        Log($"Metal Available: {IsMetalAvailable()}");
        Log($"Apple Silicon: {IsAppleSilicon()}");
        Log($"macOS Version: {GetMacOSVersion()}");


        if (IsMetalAvailable())
        {
            Log($"Device Info: {GetMetalDeviceInfoString()}");
            Log($"Minimum macOS 10.11+: {HasMinimumMacOSVersion(new Version(10, 11))}");
            Log($"Unified Memory Architecture: {IsAppleSilicon()}");


            TakeGpuMemorySnapshot("metal_initial");
        }


        Log("=========================");
    }

    /// <summary>
    /// Logs detailed Metal device capabilities and limits.
    /// </summary>
    protected void LogMetalDeviceCapabilities()
    {
        if (!IsMetalAvailable())
        {
            Log("Metal not available");
            return;
        }

        try
        {
            Log("Metal Device Capabilities:");
            Log($"  Platform: {PlatformInfo}");
            Log($"  Device: {GetMetalDeviceInfoString()}");
            Log($"  Architecture: {(IsAppleSilicon() ? "Apple Silicon" : "Intel")}");
            Log($"  macOS Version: {GetMacOSVersion()}");


            if (IsAppleSilicon())
            {
                Log($"  Unified Memory: Yes");
                Log($"  System Memory: {GC.GetTotalMemory(false) / (1024 * 1024):F0} MB");
            }
            else
            {
                Log($"  Shared Memory: Yes (Intel integrated)");
            }

            // Additional Metal-specific capability logging would go here

        }
        catch (Exception ex)
        {
            Log($"Error logging Metal device capabilities: {ex.Message}");
        }
    }

    #endregion

    #region Platform-Specific Utilities

    /// <summary>
    /// Checks if specific Metal features are available.
    /// </summary>
    /// <param name="featureName">Name of the Metal feature to check.</param>
    /// <returns>True if feature is supported, false otherwise.</returns>
    protected static bool SupportsMetalFeature(string featureName)
    {
        if (!IsMetalAvailable())
            return false;

        return featureName.ToUpperInvariant() switch
        {
            "COMPUTE_SHADERS" => true,
            "TESSELLATION" => IsAppleSilicon() || HasMinimumMacOSVersion(new Version(10, 12)),
            "RAY_TRACING" => IsAppleSilicon() && HasMinimumMacOSVersion(new Version(13, 0)),
            "MACHINE_LEARNING" => IsAppleSilicon(),
            "UNIFIED_MEMORY" => IsAppleSilicon(),
            _ => false
        };
    }

    /// <summary>
    /// Gets optimal threadgroup size for Metal compute shaders.
    /// </summary>
    /// <param name="problemSize">Total problem size.</param>
    /// <returns>Optimal threadgroup dimensions.</returns>
    protected static (int width, int height) GetOptimalThreadgroupSize(int problemSize)
    {
        if (IsAppleSilicon())
        {
            // Apple Silicon typically performs well with 32x1 or 16x2 threadgroups
            return problemSize > 1024 ? (32, 1) : (16, 1);
        }
        else
        {
            // Intel integrated graphics prefer smaller threadgroups
            return (8, 1);
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Performs Metal-specific cleanup operations.
    /// </summary>
    /// <param name="disposing">Whether disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && IsMetalAvailable())
        {
            try
            {
                ValidateMetalMemoryUsage();
                TakeGpuMemorySnapshot("metal_final");
                CompareGpuMemorySnapshots("metal_initial", "metal_final");
            }
            catch (Exception ex)
            {
                Log($"Error during Metal cleanup: {ex.Message}");
            }
        }

        base.Dispose(disposing);
    }

    #endregion
}
