// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Tests.Common;
using DotCompute.Core.Extensions;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Base class for CUDA hardware tests providing common functionality
    /// including hardware detection, performance measurement, and test utilities.
    /// </summary>
    public abstract class CudaTestBase : TestBase
    {
        protected CudaTestBase(ITestOutputHelper output) : base(output) { }

        /// <summary>
        /// Enhanced CUDA availability check with detailed logging
        /// </summary>
        protected new static async Task<bool> IsCudaAvailable()
        {
            try
            {
                // Check for CUDA runtime library first
                var hasCudaRuntime = false;
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    hasCudaRuntime = System.IO.File.Exists("cudart64_12.dll") || 
                                   System.IO.File.Exists("cudart64_11.dll") ||
                                   System.IO.File.Exists("cudart64_10.dll");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    hasCudaRuntime = System.IO.File.Exists("/usr/local/cuda/lib64/libcudart.so") ||
                                   System.IO.File.Exists("/usr/lib/x86_64-linux-gnu/libcudart.so") ||
                                   System.IO.File.Exists("/usr/local/cuda-12.6/targets/x86_64-linux/lib/libcudart.so") ||
                                   System.IO.File.Exists("/usr/local/cuda-12.8/targets/x86_64-linux/lib/libcudart.so.12");
                }
                
                if (!hasCudaRuntime)
                {
                    return false;
                }
                
                // Use the factory's IsAvailable method which can access internal CudaRuntime methods
                var factory = new CudaAcceleratorFactory();
                if (!factory.IsAvailable())
                {
                    return false;
                }
                
                // Try to create an accelerator to verify everything works
                await using var accelerator = factory.CreateDefaultAccelerator();
                
                return accelerator != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if RTX 2000 series Ada GPU is available
        /// </summary>
        protected static async Task<bool> IsRTX2000AdaAvailable()
        {
            if (!await IsCudaAvailable())
            {
                return false;
            }
            
            try
            {
                var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateDefaultAccelerator();
                
                var deviceInfo = accelerator.Info;
                return deviceInfo.IsRTX2000Ada() && 
                       deviceInfo.ComputeCapability!.Major == 8 && 
                       deviceInfo.ComputeCapability.Minor == 9 &&
                       deviceInfo.ArchitectureGeneration().Contains("Ada");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if compute capability meets minimum requirements
        /// </summary>
        protected static async Task<bool> HasMinimumComputeCapability(int majorMin, int minorMin = 0)
        {
            if (!await IsCudaAvailable())
            {
                return false;
            }
            
            try
            {
                var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateDefaultAccelerator();
                
                var cc = accelerator.Info.ComputeCapability;
                return cc.Major > majorMin || (cc.Major == majorMin && cc.Minor >= minorMin);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get detailed device information for logging
        /// </summary>
        protected static async Task<string> GetDeviceInfoString()
        {
            if (!await IsCudaAvailable())
            {
                return "CUDA not available";
            }
            
            try
            {
                var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateDefaultAccelerator();
                
                var info = accelerator.Info;
                return $"{info.Name} (CC {info.ComputeCapability?.Major}.{info.ComputeCapability?.Minor}, " +
                       $"{info.GlobalMemorySize / (1024 * 1024 * 1024):F1} GB, " +
                       $"{info.MaxComputeUnits} SMs, " +
                       $"{info.MaxComputeUnits * 64} cores)";
            }
            catch (Exception ex)
            {
                return $"Error getting device info: {ex.Message}";
            }
        }

        /// <summary>
        /// Performance measurement utility for kernels
        /// </summary>
        protected class PerformanceMeasurement
        {
            private readonly Stopwatch _stopwatch = new();
            private readonly string _operationName;
            private readonly ITestOutputHelper _output;
            
            public PerformanceMeasurement(string operationName, ITestOutputHelper output)
            {
                _operationName = operationName;
                _output = output;
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
        }

        /// <summary>
        /// Utility to generate test data with specific patterns
        /// </summary>
        protected static class TestDataGenerator
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
        }

        /// <summary>
        /// Memory usage tracking utility
        /// </summary>
        protected class MemoryTracker : IDisposable
        {
            private readonly long _initialGCMemory;
            private readonly ITestOutputHelper _output;
            private bool _disposed;
            
            public MemoryTracker(ITestOutputHelper output)
            {
                _output = output;
                _initialGCMemory = GC.GetTotalMemory(true);
                _output.WriteLine($"Memory tracking started - Initial: {_initialGCMemory / (1024 * 1024):F1} MB");
            }
            
            public void LogCurrentUsage(string label = "Current")
            {
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
        /// Verify floating point results with appropriate tolerance
        /// </summary>
        protected static void VerifyFloatArraysMatch(float[] expected, float[] actual, float tolerance = 0.0001f, 
            int maxElementsToCheck = 1000, string? context = null)
        {
            if (expected.Length != actual.Length)
            {
                throw new InvalidOperationException($"Array length mismatch: expected {expected.Length}, actual {actual.Length}");
            }
            
            var elementsToCheck = Math.Min(maxElementsToCheck, expected.Length);
            var errorCount = 0;
            const int maxErrorsToReport = 10;
            
            for (var i = 0; i < elementsToCheck; i++)
            {
                var diff = Math.Abs(expected[i] - actual[i]);
                if (diff > tolerance)
                {
                    if (errorCount < maxErrorsToReport)
                    {
                        var message = $"Mismatch at index {i}: expected {expected[i]}, actual {actual[i]}, diff {diff}";
                        if (context != null)
                        {
                            message = $"{context} - {message}";
                        }
                        throw new InvalidOperationException(message);
                    }
                    errorCount++;
                }
            }
            
            if (errorCount > 0)
            {
                var message = $"Found {errorCount} mismatches out of {elementsToCheck} elements checked (tolerance: {tolerance})";
                if (context != null)
                {
                    message = $"{context} - {message}";
                }
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Log device capabilities and limits
        /// </summary>
        protected async Task LogDeviceCapabilities()
        {
            if (!(await IsCudaAvailable()))
            {
                Output.WriteLine("CUDA not available");
                return;
            }
            
            try
            {
                var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateDefaultAccelerator();
                
                var info = accelerator.Info;
                
                Output.WriteLine("CUDA Device Capabilities:");
                Output.WriteLine($"  Name: {info.Name}");
                Output.WriteLine($"  Architecture: {info.ArchitectureGeneration()}");
                Output.WriteLine($"  Compute Capability: {info.ComputeCapability.Major}.{info.ComputeCapability.Minor}");
                Output.WriteLine($"  Global Memory: {info.GlobalMemoryBytes() / (1024.0 * 1024.0 * 1024.0):F2} GB");
                Output.WriteLine($"  Available Memory: {info.AvailableMemory / (1024.0 * 1024.0 * 1024.0):F2} GB");
                Output.WriteLine($"  Multiprocessors: {info.MultiprocessorCount()}");
                Output.WriteLine($"  CUDA Cores (est.): {info.EstimatedCudaCores()}");
                Output.WriteLine($"  Max Threads/Block: {info.MaxThreadsPerBlock}");
                Output.WriteLine($"  Shared Memory/Block: {info.SharedMemoryPerBlock() / 1024:F0} KB");
                Output.WriteLine($"  L2 Cache Size: {info.L2CacheSize() / 1024:F0} KB");
                Output.WriteLine($"  Memory Bandwidth: {info.MemoryBandwidthGBps():F0} GB/s");
                Output.WriteLine($"  Core Clock: {info.ClockRate() / 1000.0:F0} MHz");
                Output.WriteLine($"  Memory Clock: {info.MemoryClockRate() / 1000.0:F0} MHz");
                Output.WriteLine($"  Warp Size: {info.WarpSize()}");
                Output.WriteLine($"  Unified Memory: {(info.SupportsUnifiedMemory() ? "Yes" : "No")}");
                Output.WriteLine($"  Managed Memory: {(info.SupportsManagedMemory() ? "Yes" : "No")}");
                Output.WriteLine($"  Concurrent Kernels: {(info.SupportsConcurrentKernels() ? "Yes" : "No")}");
                Output.WriteLine($"  ECC Enabled: {(info.IsECCEnabled() ? "Yes" : "No")}");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Error getting device capabilities: {ex.Message}");
            }
        }
    }
}