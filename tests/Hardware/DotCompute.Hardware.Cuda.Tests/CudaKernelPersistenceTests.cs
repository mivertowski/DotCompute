// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Memory;
using DotCompute.Tests.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for CUDA kernel persistence and caching functionality
    /// </summary>
    public class CudaKernelPersistenceTests : CudaTestBase, IDisposable
    {
        private readonly CudaAccelerator? _accelerator;
        private readonly CudaKernelCompiler? _compiler;
        private readonly string _cacheDirectory;
        private readonly ILogger<CudaKernelPersistenceTests>? _logger;

        public CudaKernelPersistenceTests(ITestOutputHelper output) : base(output)
        {
            _cacheDirectory = Path.Combine(Path.GetTempPath(), $"cuda_kernel_cache_{Guid.NewGuid()}");
            Directory.CreateDirectory(_cacheDirectory);

            if (IsCudaAvailable())
            {
                var factory = new CudaAcceleratorFactory();
                // Create base CUDA accelerator for tests
                _accelerator = new CudaAccelerator(0, Microsoft.Extensions.Logging.Abstractions.NullLogger<CudaAccelerator>.Instance);


                using var loggerFactory = LoggerFactory.Create(builder =>

                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
                _logger = loggerFactory.CreateLogger<CudaKernelPersistenceTests>();

                // Create compiler with the CUDA context

                var cudaContext = typeof(CudaAccelerator)
                    .GetProperty("CudaContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_accelerator) as DotCompute.Backends.CUDA.CudaContext;


                var compilerLogger = _logger ?? (ILogger)Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                _compiler = new CudaKernelCompiler(cudaContext!, compilerLogger);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _compiler?.Dispose();
                _accelerator?.DisposeAsync().AsTask().Wait();

                // Clean up test cache directory

                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, true);
                }
            }


            base.Dispose(disposing);
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task CompiledKernel_Should_BePersisted_ToDisk()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernel = new KernelDefinition
            {
                Name = "test_persistence",
                Source = @"
                    extern ""C"" __global__ void test_persistence(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = data[idx] * 2.0f;
                        }
                    }",
                EntryPoint = "test_persistence",
                // Language determined by backend
            };

            // Act
            var compiled = await _compiler.CompileAsync(kernel);

            // Assert

            compiled.Should().NotBeNull();

            // Check that cache file was created

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.cubin");
            cacheFiles.Should().NotBeEmpty("Compiled kernel should be cached to disk");

            // Verify the cached file contains valid data

            var cacheFile = cacheFiles[0];
            var fileInfo = new FileInfo(cacheFile);
            fileInfo.Length.Should().BeGreaterThan(0, "Cache file should contain compiled kernel data");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task CachedKernel_Should_BeReused_OnSecondCompilation()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernel = new KernelDefinition
            {
                Name = "test_cache_reuse",
                Source = @"
                    extern ""C"" __global__ void test_cache_reuse(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = data[idx] + 1.0f;
                        }
                    }",
                EntryPoint = "test_cache_reuse",
                // Language determined by backend
            };

            // Act
            var perf = new PerformanceMeasurement("Kernel Compilation", Output);

            // First compilation - should compile from source

            perf.Start();
            var compiled1 = await _compiler.CompileAsync(kernel);
            perf.Stop();
            var firstCompileTime = perf.ElapsedTime;

            // Second compilation - should load from cache

            perf.Start();
            var compiled2 = await _compiler.CompileAsync(kernel);
            perf.Stop();
            var secondCompileTime = perf.ElapsedTime;

            // Assert

            compiled1.Should().NotBeNull();
            compiled2.Should().NotBeNull();

            // Second compilation should be significantly faster (loading from cache)

            Output.WriteLine($"First compile: {firstCompileTime.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Second compile (cached): {secondCompileTime.TotalMilliseconds:F2}ms");


            secondCompileTime.Should().BeLessThan(firstCompileTime.Multiply(0.5),

                "Cached compilation should be at least 2x faster");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task KernelCache_Should_HandleVersioning()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernelV1 = new KernelDefinition
            {
                Name = "versioned_kernel",
                Source = @"
                    extern ""C"" __global__ void versioned_kernel(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = data[idx] * 2.0f;
                        }
                    }",
                EntryPoint = "versioned_kernel",
                // Language determined by backend,
                // Version = "1.0.0"
            };

            var kernelV2 = new KernelDefinition
            {
                Name = "versioned_kernel",
                Source = @"
                    extern ""C"" __global__ void versioned_kernel(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = data[idx] * 3.0f;  // Different operation
                        }
                    }",
                EntryPoint = "versioned_kernel",
                // Language determined by backend,
                // Version = "2.0.0"
            };

            // Act
            var compiledV1 = await _compiler.CompileAsync(kernelV1);
            var compiledV2 = await _compiler.CompileAsync(kernelV2);

            // Test that both versions work correctly

            const int size = 100;
            var testData1 = TestDataGenerator.CreateConstantData(size, 1.0f);
            var testData2 = TestDataGenerator.CreateConstantData(size, 1.0f);


            await using var buffer1 = await _accelerator.Memory.AllocateAsync<float>(size);
            await using var buffer2 = await _accelerator.Memory.AllocateAsync<float>(size);


            await buffer1.CopyFromAsync(testData1);
            await buffer2.CopyFromAsync(testData2);

            // Execute V1 kernel (multiply by 2)

            var args1 = new KernelArguments
            {
                // Buffers = new[] { buffer1 },
                // ScalarArguments = new object[] { size }
            };
            await compiledV1.ExecuteAsync(args1);

            // Execute V2 kernel (multiply by 3)

            var args2 = new KernelArguments
            {
                // Buffers = new[] { buffer2 },
                // ScalarArguments = new object[] { size }
            };
            await compiledV2.ExecuteAsync(args2);


            await _accelerator.SynchronizeAsync();


            var result1 = new float[size];
            var result2 = new float[size];
            await buffer1.CopyToAsync(result1);
            await buffer2.CopyToAsync(result2);

            // Assert

            result1[0].Should().BeApproximately(2.0f, 0.001f, "V1 should multiply by 2");
            result2[0].Should().BeApproximately(3.0f, 0.001f, "V2 should multiply by 3");

            // Verify separate cache files exist

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.cubin");
            cacheFiles.Length.Should().BeGreaterThanOrEqualTo(2, "Different versions should have separate cache files");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task KernelCache_Should_SurviveAcrossSessions()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernel = new KernelDefinition
            {
                Name = "cross_session_kernel",
                Source = @"
                    extern ""C"" __global__ void cross_session_kernel(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = sqrtf(data[idx]);
                        }
                    }",
                EntryPoint = "cross_session_kernel",
                // Language determined by backend
            };

            // Act - First session
            var compiled1 = null as object; // ICompiledKernel
            var ctx1 = _accelerator!.GetType().GetProperty("CudaContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_accelerator) as DotCompute.Backends.CUDA.CudaContext;
            using (var compiler1 = new CudaKernelCompiler(ctx1!, _logger!))
            {
                compiled1 = await compiler1.CompileAsync(kernel);
            }

            // Act - Second session (simulated by new compiler instance)

            var compiled2 = null as object; // ICompiledKernel
            var perf = new PerformanceMeasurement("Cross-session Load", Output);
            perf.Start();
            var ctx2 = _accelerator!.GetType().GetProperty("CudaContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_accelerator) as DotCompute.Backends.CUDA.CudaContext;
            using (var compiler2 = new CudaKernelCompiler(ctx2!, _logger!))
            {
                compiled2 = await compiler2.CompileAsync(kernel);
            }
            perf.Stop();

            // Assert

            compiled1.Should().NotBeNull();
            compiled2.Should().NotBeNull();

            // Loading from cache should be fast

            perf.ElapsedTime.TotalMilliseconds.Should().BeLessThan(100,

                "Loading cached kernel should be very fast");

            // Test functionality

            const int size = 100;
            var testData = TestDataGenerator.CreateLinearSequence(size, 1.0f, 1.0f);
            var expected = testData.Select(x => MathF.Sqrt(x)).ToArray();


            await using var buffer = await _accelerator.Memory.AllocateAsync<float>(size);
            await buffer.CopyFromAsync(testData);


            var args = new KernelArguments
            {
                // Buffers = new[] { buffer },
                // ScalarArguments = new object[] { size }
            };
            // await compiled2.ExecuteAsync(args); // Would need proper type
            await _accelerator.SynchronizeAsync();


            var result = new float[size];
            await buffer.CopyToAsync(result);


            VerifyFloatArraysMatch(expected, result, 0.0001f, context: "Cross-session kernel execution");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task KernelCache_Should_HandleConcurrentAccess()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernel = new KernelDefinition
            {
                Name = "concurrent_kernel",
                Source = @"
                    extern ""C"" __global__ void concurrent_kernel(float* data, float scalar, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = data[idx] * scalar;
                        }
                    }",
                EntryPoint = "concurrent_kernel",
                // Language determined by backend
            };

            // Act - Multiple concurrent compilations
            var tasks = new List<Task<ICompiledKernel>>();
            const int concurrentCount = 10;


            for (int i = 0; i < concurrentCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var compiler = new CudaKernelCompiler(_accelerator.CudaContext, _logger);
                    // Note: Caching properties may need to be configured differently
                    return await compiler.CompileAsync(kernel);
                }));
            }


            var results = await Task.WhenAll(tasks);

            // Assert

            results.Should().HaveCount(concurrentCount);
            results.Should().OnlyContain(r => r != null, "All concurrent compilations should succeed");

            // Only one cache file should exist (all threads should share the same cached kernel)

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*concurrent_kernel*.cubin");
            cacheFiles.Should().HaveCount(1, "Concurrent access should result in single cache file");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task KernelCache_Should_InvalidateOnOptionsChange()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernel = new KernelDefinition
            {
                Name = "options_kernel",
                Source = @"
                    extern ""C"" __global__ void options_kernel(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = __fdividef(1.0f, data[idx]);
                        }
                    }",
                EntryPoint = "options_kernel",
                // Language determined by backend
            };

            var optionsDebug = new DotCompute.Abstractions.CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.None,
                GenerateDebugInfo = true
            };

            var optionsRelease = new DotCompute.Abstractions.CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.O3,
                GenerateDebugInfo = false
            };

            // Act
            var compiledDebug = await _compiler.CompileAsync(kernel, optionsDebug);
            var compiledRelease = await _compiler.CompileAsync(kernel, optionsRelease);

            // Assert

            compiledDebug.Should().NotBeNull();
            compiledRelease.Should().NotBeNull();

            // Should have separate cache entries for different compilation options

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*options_kernel*.cubin");
            cacheFiles.Length.Should().BeGreaterThanOrEqualTo(2,

                "Different compilation options should create separate cache entries");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task LargeKernelLibrary_Should_BenefitFromCaching()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - Create a library of kernels
            var kernelLibrary = new List<KernelDefinition>();
            const int librarySize = 20;


            for (int i = 0; i < librarySize; i++)
            {
                kernelLibrary.Add(new KernelDefinition
                {
                    Name = $"library_kernel_{i}",
                    Source = $@"
                        extern ""C"" __global__ void library_kernel_{i}(float* data, int n) {{
                            int idx = blockIdx.x * blockDim.x + threadIdx.x;
                            if (idx < n) {{
                                data[idx] = data[idx] * {i + 1}.0f;
                            }}
                        }}",
                    EntryPoint = $"library_kernel_{i}",
                    // Language determined by backend
                });
            }

            // Act - First compilation pass
            var firstPassPerf = new PerformanceMeasurement("First Pass Compilation", Output);
            firstPassPerf.Start();


            var firstPassResults = new List<DotCompute.Abstractions.ICompiledKernel>();
            foreach (var kernel in kernelLibrary)
            {
                firstPassResults.Add(await _compiler.CompileAsync(kernel));
            }


            firstPassPerf.Stop();
            firstPassPerf.LogResults();

            // Act - Second compilation pass (should use cache)

            var secondPassPerf = new PerformanceMeasurement("Second Pass (Cached)", Output);
            secondPassPerf.Start();


            var secondPassResults = new List<DotCompute.Abstractions.ICompiledKernel>();
            foreach (var kernel in kernelLibrary)
            {
                secondPassResults.Add(await _compiler.CompileAsync(kernel));
            }


            secondPassPerf.Stop();
            secondPassPerf.LogResults();

            // Assert

            firstPassResults.Should().HaveCount(librarySize);
            secondPassResults.Should().HaveCount(librarySize);

            // Cached compilation should be significantly faster

            var speedup = firstPassPerf.ElapsedTime.TotalMilliseconds / secondPassPerf.ElapsedTime.TotalMilliseconds;
            Output.WriteLine($"Cache speedup: {speedup:F1}x");
            speedup.Should().BeGreaterThan(5.0, "Cache should provide significant speedup for kernel library");

            // Verify all kernels are cached

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.cubin");
            cacheFiles.Length.Should().BeGreaterThanOrEqualTo(librarySize, "All library kernels should be cached");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task KernelCache_Should_HandleCorruptedCacheGracefully()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            var kernel = new KernelDefinition
            {
                Name = "corruption_test",
                Source = @"
                    extern ""C"" __global__ void corruption_test(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = expf(data[idx]);
                        }
                    }",
                EntryPoint = "corruption_test",
                // Language determined by backend
            };

            // First compilation to create cache
            var compiled1 = await _compiler.CompileAsync(kernel);

            // Corrupt the cache file

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*corruption_test*.cubin");
            cacheFiles.Should().NotBeEmpty();


            var cacheFile = cacheFiles[0];
            await File.WriteAllTextAsync(cacheFile, "CORRUPTED DATA");

            // Act - Try to compile again with corrupted cache

            var compiled2 = await _compiler.CompileAsync(kernel);

            // Assert

            compiled2.Should().NotBeNull("Compiler should recover from corrupted cache");

            // Test functionality to ensure it recompiled correctly

            const int size = 10;
            var testData = TestDataGenerator.CreateLinearSequence(size, 0.0f, 0.1f);
            var expected = testData.Select(x => MathF.Exp(x)).ToArray();


            await using var buffer = await _accelerator.Memory.AllocateAsync<float>(size);
            await buffer.CopyFromAsync(testData);


            var args = new KernelArguments
            {
                // Buffers = new[] { buffer },
                // ScalarArguments = new object[] { size }
            };
            // await compiled2.ExecuteAsync(args); // Would need proper type
            await _accelerator.SynchronizeAsync();


            var result = new float[size];
            await buffer.CopyToAsync(result);


            VerifyFloatArraysMatch(expected, result, 0.001f, context: "Kernel execution after cache corruption");
        }
    }
}