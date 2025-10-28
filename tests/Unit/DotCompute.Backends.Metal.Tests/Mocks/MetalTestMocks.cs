// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Factory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.Tests.Mocks
{
    /// <summary>
    /// Mock implementations for Metal backend testing when hardware is not available.
    /// Provides realistic behavior simulation for unit testing scenarios.
    /// </summary>
    public static class MetalTestMocks
    {
        /// <summary>
        /// Creates a mock Metal accelerator for unit testing
        /// </summary>
        public static IAccelerator CreateMockMetalAccelerator(
            string deviceName = "Mock Metal GPU",
            long totalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            int computeUnits = 24,
            bool isAppleSilicon = true)
        {
            var mockAccelerator = Substitute.For<IAccelerator>();
            var mockInfo = CreateMockDeviceInfo(deviceName, totalMemory, computeUnits, isAppleSilicon);
            var mockMemory = CreateMockMemoryManager(totalMemory);
            mockAccelerator.Info.Returns(mockInfo);
            mockAccelerator.Memory.Returns(mockMemory);
            mockAccelerator.Type.Returns(AcceleratorType.Metal);

            // Configure async operations
            mockAccelerator.SynchronizeAsync().Returns(ValueTask.CompletedTask);

            // Configure disposal
            mockAccelerator.When(x => x.DisposeAsync()).Do(_ => { /* Mock disposal */ });

            return mockAccelerator;
        }

        /// <summary>
        /// Creates a mock device info with realistic Metal GPU characteristics
        /// </summary>
        public static AcceleratorInfo CreateMockDeviceInfo(
            string deviceName = "Mock Metal GPU",
            long totalMemory = 8L * 1024 * 1024 * 1024,
            int computeUnits = 24,
            bool isAppleSilicon = true)
        {
            var mockInfo = new AcceleratorInfo()
            {
                Id = "mock_metal_gpu",
                Name = deviceName,
                DeviceType = AcceleratorType.Metal.ToString(),
                Vendor = "Apple Inc.",
                DriverVersion = "3.1.0",
                TotalMemory = totalMemory,
                AvailableMemory = (long)(totalMemory * 0.85),
                MaxSharedMemoryPerBlock = 32 * 1024, // 32KB
                MaxMemoryAllocationSize = totalMemory,
                Capabilities = new Dictionary<string, object>
                {
                    ["UnifiedMemory"] = isAppleSilicon,
                    ["MaxThreadsPerThreadgroup"] = 1024,
                    ["MaxBuffersPerKernel"] = 31,
                    ["MaxTexturesPerKernel"] = 128,
                    ["SupportsNonUniformThreadgroups"] = true,
                    ["SupportsReadWriteTextures"] = true,
                    ["SupportsArgumentBuffers"] = true,
                    ["Family"] = isAppleSilicon ? "Apple" : "Mac",
                    ["MetalFeatureSet"] = "Metal_GPUFamily2_v1"
                }
            };

            return mockInfo;
        }

        /// <summary>
        /// Creates a mock memory manager with realistic Metal memory behavior
        /// </summary>
        public static IUnifiedMemoryManager CreateMockMemoryManager(long totalMemory = 8L * 1024 * 1024 * 1024)
        {
            var mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
            var currentUsage = 0L;

            // Configure memory allocation
            mockMemoryManager.AllocateAsync<float>(Arg.Any<int>(), Arg.Any<MemoryOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var elementCount = callInfo.Arg<int>();
                    var sizeInBytes = elementCount * sizeof(float);
                    return ValueTask.FromResult(CreateMockBuffer<float>(elementCount, sizeInBytes));
                });

            mockMemoryManager.AllocateAsync<int>(Arg.Any<int>(), Arg.Any<MemoryOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var elementCount = callInfo.Arg<int>();
                    var sizeInBytes = elementCount * sizeof(int);
                    return ValueTask.FromResult(CreateMockBuffer<int>(elementCount, sizeInBytes));
                });

            mockMemoryManager.AllocateAsync<byte>(Arg.Any<int>(), Arg.Any<MemoryOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var elementCount = callInfo.Arg<int>();
                    var sizeInBytes = elementCount * sizeof(byte);
                    return ValueTask.FromResult(CreateMockBuffer<byte>(elementCount, sizeInBytes));
                });

            // Configure memory statistics
            var mockStats = new MemoryStatistics
            {
                TotalMemoryBytes = totalMemory,
                UsedMemoryBytes = currentUsage,
                AvailableMemoryBytes = (long)(totalMemory * 0.85),
                TotalCapacity = totalMemory,
                CurrentUsage = currentUsage
            };
            
            mockMemoryManager.Statistics.Returns(mockStats);
            mockMemoryManager.TotalAvailableMemory.Returns((long)(totalMemory * 0.85));
            mockMemoryManager.CurrentAllocatedMemory.Returns(callInfo => currentUsage);

            return mockMemoryManager;
        }

        /// <summary>
        /// Creates a mock unified memory buffer
        /// </summary>
        public static IUnifiedMemoryBuffer<T> CreateMockBuffer<T>(int elementCount, long sizeInBytes) 
            where T : unmanaged
        {
            var mockBuffer = Substitute.For<IUnifiedMemoryBuffer<T>>();
            var hostData = new T[elementCount];

            mockBuffer.Length.Returns(elementCount);
            mockBuffer.SizeInBytes.Returns(sizeInBytes);
            // IsValid removed from interface - buffer is considered valid if not null

            // Configure data transfer operations
            mockBuffer.CopyFromAsync(Arg.Any<ReadOnlyMemory<T>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var sourceData = callInfo.Arg<ReadOnlyMemory<T>>();
                    sourceData.CopyTo(hostData.AsMemory());
                    return ValueTask.CompletedTask;
                });

            mockBuffer.CopyToAsync(Arg.Any<Memory<T>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var targetData = callInfo.Arg<Memory<T>>();
                    hostData.AsMemory().CopyTo(targetData);
                    return ValueTask.CompletedTask;
                });

            // Configure disposal
            mockBuffer.When(x => x.DisposeAsync()).Do(_ => { /* Mock disposal */ });

            return mockBuffer;
        }

        /// <summary>
        /// Creates a mock kernel manager
        /// </summary>
        public static ICompiledKernel CreateMockKernelManager()
        {
            // For now, return a mock compiled kernel directly since we don't have a separate kernel manager interface
            var mockKernel = Substitute.For<ICompiledKernel>();
            mockKernel.Name.Returns("MockKernel");
            // Note: IsReady property doesn't exist on ICompiledKernel interface
            return mockKernel;
        }

        /// <summary>
        /// Creates a mock compiled kernel
        /// </summary>
        public static ICompiledKernel CreateMockCompiledKernel(KernelDefinition definition)
        {
            var mockKernel = Substitute.For<ICompiledKernel>();

            mockKernel.Name.Returns(definition.Name);
            // Note: IsReady property doesn't exist on ICompiledKernel interface

            // Configure kernel execution with proper KernelArguments type
            mockKernel.ExecuteAsync(
                Arg.Any<KernelArguments>(),
                Arg.Any<CancellationToken>())
                .Returns(ValueTask.CompletedTask);

            // Configure disposal
            mockKernel.When(x => x.Dispose()).Do(_ => { /* Mock disposal */ });

            return mockKernel;
        }

        /// <summary>
        /// Creates a mock Metal backend factory
        /// </summary>
        public static MetalBackendFactory CreateMockMetalFactory(ILogger<MetalBackendFactory>? logger = null)
        {
            var mockLogger = logger ?? Substitute.For<ILogger<MetalBackendFactory>>();
            var mockLoggerFactory = Substitute.For<ILoggerFactory>();
            
            mockLoggerFactory.CreateLogger<MetalBackendFactory>().Returns(mockLogger);
            
            // Return a real factory but with mocked dependencies
            return new MetalBackendFactory(mockLogger, mockLoggerFactory);
        }

        /// <summary>
        /// Creates realistic test data generators for various Metal testing scenarios
        /// </summary>
        public static class TestDataGenerators
        {
            /// <summary>
            /// Generates test data for vector operations
            /// </summary>
            public static (float[] inputA, float[] inputB, float[] expectedOutput) CreateVectorAdditionData(
                int elementCount, 
                Random? random = null)
            {
                random ??= new Random(42);
                
                var inputA = new float[elementCount];
                var inputB = new float[elementCount];
                var expectedOutput = new float[elementCount];

                for (var i = 0; i < elementCount; i++)
                {
                    inputA[i] = (float)(random.NextDouble() * 100.0 - 50.0);
                    inputB[i] = (float)(random.NextDouble() * 100.0 - 50.0);
                    expectedOutput[i] = inputA[i] + inputB[i];
                }

                return (inputA, inputB, expectedOutput);
            }

            /// <summary>
            /// Generates test data for matrix operations
            /// </summary>
            public static (float[] matrixA, float[] matrixB, float[] expectedResult) CreateMatrixMultiplyData(
                int rows, int cols, int innerDim, Random? random = null)
            {
                random ??= new Random(42);
                
                var matrixA = new float[rows * innerDim];
                var matrixB = new float[innerDim * cols];
                var expectedResult = new float[rows * cols];

                // Initialize matrices with random values
                for (var i = 0; i < matrixA.Length; i++)
                {
                    matrixA[i] = (float)(random.NextDouble() * 10.0 - 5.0);
                }

                for (var i = 0; i < matrixB.Length; i++)
                {
                    matrixB[i] = (float)(random.NextDouble() * 10.0 - 5.0);
                }

                // Calculate expected result (CPU reference)
                for (var row = 0; row < rows; row++)
                {
                    for (var col = 0; col < cols; col++)
                    {
                        var sum = 0.0f;
                        for (var k = 0; k < innerDim; k++)
                        {
                            sum += matrixA[row * innerDim + k] * matrixB[k * cols + col];
                        }
                        expectedResult[row * cols + col] = sum;
                    }
                }

                return (matrixA, matrixB, expectedResult);
            }

            /// <summary>
            /// Creates performance test data with configurable scaling
            /// </summary>
            public static float[] CreatePerformanceTestData(int baseSize, int scaleFactor, Random? random = null)
            {
                random ??= new Random(42);
                var totalSize = baseSize * scaleFactor;
                var data = new float[totalSize];

                for (var i = 0; i < totalSize; i++)
                {
                    data[i] = (float)(Math.Sin(i * 0.01) + random.NextDouble() * 0.1);
                }

                return data;
            }
        }

        /// <summary>
        /// Mock system information provider for testing
        /// </summary>
        public static class MockSystemInfo
        {
            public static bool SimulateAppleSilicon { get; set; } = true;
            public static long SimulatedGpuMemory { get; set; } = 8L * 1024 * 1024 * 1024;
            public static string SimulatedMacOSVersion { get; set; } = "14.0";

            public static bool IsMetalAvailable()
            {
                // For unit tests, always return true unless explicitly disabled
                return Environment.GetEnvironmentVariable("DOTCOMPUTE_DISABLE_METAL_MOCK") != "true";
            }

            public static bool IsAppleSilicon()
            {
                return SimulateAppleSilicon || 
                       (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && 
                        RuntimeInformation.ProcessArchitecture == Architecture.Arm64);
            }

            public static Dictionary<string, object> GetMockSystemCapabilities()
            {
                return new Dictionary<string, object>
                {
                    ["Platform"] = "macOS",
                    ["Architecture"] = SimulateAppleSilicon ? "arm64" : "x86_64",
                    ["IsAppleSilicon"] = IsAppleSilicon(),
                    ["MacOSVersion"] = SimulatedMacOSVersion,
                    ["MetalAvailable"] = IsMetalAvailable(),
                    ["EstimatedGpuMemory"] = SimulatedGpuMemory,
                    ["MaxThreadgroupSize"] = 1024,
                    ["PreferredThreadgroupSize"] = 256
                };
            }
        }
    }

    /// <summary>
    /// Extension methods for mock setup helpers
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Configures a mock accelerator to simulate specific hardware characteristics
        /// </summary>
        public static IAccelerator ConfigureFor(this IAccelerator mockAccelerator, string scenario)
        {
            return scenario.ToLowerInvariant() switch
            {
                "apple-silicon-m2" => ConfigureForAppleSiliconM2(mockAccelerator),
                "apple-silicon-m3" => ConfigureForAppleSiliconM3(mockAccelerator),
                "intel-mac" => ConfigureForIntelMac(mockAccelerator),
                "low-memory" => ConfigureForLowMemory(mockAccelerator),
                "high-performance" => ConfigureForHighPerformance(mockAccelerator),
                _ => mockAccelerator
            };
        }

        private static IAccelerator ConfigureForAppleSiliconM2(IAccelerator mock)
        {
            var info = mock.Info;
            info.Name.Returns("Apple M2");
            info.TotalMemory.Returns(16L * 1024 * 1024 * 1024); // 16GB unified
            info.ComputeUnits.Returns(10);
            info.Capabilities.Returns(new Dictionary<string, object>
            {
                ["UnifiedMemory"] = true,
                ["Family"] = "Apple",
                ["Generation"] = "M2"
            });
            return mock;
        }

        private static IAccelerator ConfigureForAppleSiliconM3(IAccelerator mock)
        {
            var info = mock.Info;
            info.Name.Returns("Apple M3");
            info.TotalMemory.Returns(24L * 1024 * 1024 * 1024); // 24GB unified
            info.ComputeUnits.Returns(16);
            info.Capabilities.Returns(new Dictionary<string, object>
            {
                ["UnifiedMemory"] = true,
                ["Family"] = "Apple",
                ["Generation"] = "M3"
            });
            return mock;
        }

        private static IAccelerator ConfigureForIntelMac(IAccelerator mock)
        {
            var info = mock.Info;
            info.Name.Returns("AMD Radeon Pro 5500M");
            info.TotalMemory.Returns(4L * 1024 * 1024 * 1024); // 4GB discrete
            info.ComputeUnits.Returns(24);
            info.Capabilities.Returns(new Dictionary<string, object>
            {
                ["UnifiedMemory"] = false,
                ["Family"] = "Mac",
                ["Generation"] = "Intel"
            });
            return mock;
        }

        private static IAccelerator ConfigureForLowMemory(IAccelerator mock)
        {
            var info = mock.Info;
            info.TotalMemory.Returns(2L * 1024 * 1024 * 1024); // 2GB
            info.AvailableMemory.Returns(1536L * 1024 * 1024); // 1.5GB available
            return mock;
        }

        private static IAccelerator ConfigureForHighPerformance(IAccelerator mock)
        {
            var info = mock.Info;
            info.Name.Returns("High-Performance Metal GPU");
            info.TotalMemory.Returns(32L * 1024 * 1024 * 1024); // 32GB
            info.ComputeUnits.Returns(64);
            return mock;
        }
    }
}