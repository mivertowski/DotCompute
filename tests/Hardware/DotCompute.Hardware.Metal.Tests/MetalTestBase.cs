// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Backends.Metal;
using DotCompute.Backends.Metal.Factory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Abstractions.Memory;
using DotCompute.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Base class for Metal hardware tests providing common functionality
    /// including hardware detection, performance measurement, and test utilities.
    /// </summary>
    public abstract class MetalTestBase : TestBase
    {
        protected new readonly ITestOutputHelper Output;
        protected readonly ILoggerFactory LoggerFactory;
        protected readonly MetalBackendFactory Factory;

        protected MetalTestBase(ITestOutputHelper output) : base(output)
        {
            Output = output;
            
            // Create logger factory that outputs to test console
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddProvider(new XUnitLoggerProvider(output));
            });

            Factory = new MetalBackendFactory(
                LoggerFactory.CreateLogger<MetalBackendFactory>(),
                LoggerFactory);
        }

        /// <summary>
        /// Check if Metal is available on this system
        /// </summary>
        protected static bool IsMetalAvailable()
        {
            try
            {
                // Check if we're on macOS
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return false;
                }

                // Check if Metal is supported
                return MetalBackend.IsAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if we're running on Apple Silicon (M1/M2/M3)
        /// </summary>
        protected static bool IsAppleSilicon()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return false;
                }

                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the macOS version
        /// </summary>
        protected static Version GetMacOSVersion()
        {
            return Environment.OSVersion.Version;
        }

        /// <summary>
        /// Get detailed Metal device information for logging
        /// </summary>
        protected static string GetMetalDeviceInfoString()
        {
            if (!IsMetalAvailable())
            {
                return "Metal not available";
            }

            try
            {
                var device = MetalNative.CreateSystemDefaultDevice();
                if (device == IntPtr.Zero)
                {
                    return "No Metal device found";
                }

                try
                {
                    var deviceInfo = MetalNative.GetDeviceInfo(device);
                    var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown";
                    var memoryGB = deviceInfo.MaxBufferLength / (1024.0 * 1024.0 * 1024.0);
                    
                    return $"{deviceName} " +
                           $"(Max Buffer: {memoryGB:F1} GB, " +
                           $"Unified Memory: {deviceInfo.HasUnifiedMemory}, " +
                           $"Headless: {deviceInfo.IsHeadless})";
                }
                finally
                {
                    MetalNative.ReleaseDevice(device);
                }
            }
            catch (Exception ex)
            {
                return $"Error getting Metal device info: {ex.Message}";
            }
        }

        /// <summary>
        /// Log detailed Metal device capabilities and limits
        /// </summary>
        protected void LogMetalDeviceCapabilities()
        {
            if (!IsMetalAvailable())
            {
                Output.WriteLine("Metal not available");
                return;
            }

            try
            {
                var device = MetalNative.CreateSystemDefaultDevice();
                if (device == IntPtr.Zero)
                {
                    Output.WriteLine("No Metal device found");
                    return;
                }

                try
                {
                    var deviceInfo = MetalNative.GetDeviceInfo(device);
                    var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown";
                    
                    Output.WriteLine("Metal Device Capabilities:");
                    Output.WriteLine($"  Name: {deviceName}");
                    Output.WriteLine($"  Max Buffer Length: {deviceInfo.MaxBufferLength / (1024.0 * 1024.0 * 1024.0):F2} GB");
                    Output.WriteLine($"  Unified Memory: {(deviceInfo.HasUnifiedMemory ? "Yes" : "No")}");
                    Output.WriteLine($"  Headless: {(deviceInfo.IsHeadless ? "Yes" : "No")}");
                    Output.WriteLine($"  Low Power: {(deviceInfo.IsLowPower ? "Yes" : "No")}");
                    Output.WriteLine($"  Supports Non-Uniform Threadgroups: {(deviceInfo.SupportsNonUniformThreadgroups ? "Yes" : "No")}");
                    
                    // Test command queue creation
                    var commandQueue = MetalNative.CreateCommandQueue(device);
                    if (commandQueue != IntPtr.Zero)
                    {
                        Output.WriteLine($"  Command Queue: Available");
                        MetalNative.ReleaseCommandQueue(commandQueue);
                    }
                    else
                    {
                        Output.WriteLine($"  Command Queue: Failed to create");
                    }

                    // Get system memory info (unified memory architecture)
                    if (deviceInfo.HasUnifiedMemory)
                    {
                        var physicalMemory = GC.GetTotalMemory(false);
                        Output.WriteLine($"  System Physical Memory: {physicalMemory / (1024.0 * 1024.0):F0} MB");
                    }
                }
                finally
                {
                    MetalNative.ReleaseDevice(device);
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Error getting Metal device capabilities: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LoggerFactory?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Performance measurement utility for Metal operations
        /// </summary>
        protected class MetalPerformanceMeasurement
        {
            private readonly Stopwatch _stopwatch = new();
            private readonly string _operationName;
            private readonly ITestOutputHelper _output;

            public MetalPerformanceMeasurement(string operationName, ITestOutputHelper output)
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
                    var throughputMBps = dataSize / (_stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
                    _output.WriteLine($"  Throughput: {throughputGBps:F2} GB/s ({throughputMBps:F1} MB/s)");
                }
            }
        }

        /// <summary>
        /// Metal-specific test data generator
        /// </summary>
        protected static class MetalTestDataGenerator
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

            public static int[] CreateIntegerSequence(int count, int start = 0, int step = 1)
            {
                var data = new int[count];
                for (var i = 0; i < count; i++)
                {
                    data[i] = start + i * step;
                }
                return data;
            }

            /// <summary>
            /// Create 2D matrix data for matrix operations
            /// </summary>
            public static float[] CreateMatrix(int rows, int cols, bool identity = false)
            {
                var data = new float[rows * cols];
                if (identity && rows == cols)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        data[i * cols + i] = 1.0f;
                    }
                }
                else
                {
                    var random = new Random(42);
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                    }
                }
                return data;
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
        /// XUnit logger provider
        /// </summary>
        private sealed class XUnitLoggerProvider : ILoggerProvider
        {
            private readonly ITestOutputHelper _output;

            public XUnitLoggerProvider(ITestOutputHelper output)
            {
                _output = output;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new XUnitLogger(_output, categoryName);
            }

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// XUnit logger
        /// </summary>
        private sealed class XUnitLogger : ILogger
        {
            private readonly ITestOutputHelper _output;
            private readonly string _categoryName;

            public XUnitLogger(ITestOutputHelper output, string categoryName)
            {
                _output = output;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                try
                {
                    var message = formatter(state, exception);
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}");
                    
                    if (exception != null)
                    {
                        _output.WriteLine(exception.ToString());
                    }
                }
                catch
                {
                    // Ignore logging errors in tests
                }
            }
        }
    }
}