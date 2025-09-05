// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for CUDA kernel compilation pipeline including PTX to CUBIN conversion.
    /// </summary>
    public class CudaKernelCompilationTests : CudaTestBase
    {
        private readonly CudaAccelerator? _accelerator;
        private readonly CudaKernelCompiler _compiler;
        private readonly CudaKernelCache _cache;
        private readonly ILogger _logger;

        public CudaKernelCompilationTests(ITestOutputHelper output) : base(output)
        {
            _logger = new TestLogger(output);
            
            if (IsCudaAvailable().Result)
            {
                var factory = new CudaAcceleratorFactory();
                _accelerator = factory.CreateProductionAccelerator(0) as CudaAccelerator;
                
                if (_accelerator != null)
                {
                    _compiler = new CudaKernelCompiler(_accelerator.Context, _logger);
                    _cache = new CudaKernelCache(_accelerator.Device, _logger);
                }
            }
        }

        [Fact]
        public async Task KernelCompiler_ShouldCompileCudaKernelToPTX()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_compiler == null, "Compiler not available");

            var kernelCode = @"
                extern ""C"" __global__ void simpleKernel(float* data, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        data[idx] = data[idx] * 2.0f;
                    }
                }
            ";

            var definition = new KernelDefinition
            {
                Name = "simpleKernel",
                Code = kernelCode,
                Language = KernelLanguage.CUDA
            };

            var compiledKernel = await _compiler.CompileAsync(definition, new CompilationOptions());

            Assert.NotNull(compiledKernel);
            Assert.Equal("simpleKernel", compiledKernel.Name);
            Assert.NotNull(compiledKernel.Binary);
            Assert.True(compiledKernel.Binary.Length > 0);

            // PTX should contain recognizable patterns
            var ptxText = Encoding.UTF8.GetString(compiledKernel.Binary);
            Assert.Contains(".target", ptxText);
            Assert.Contains("simpleKernel", ptxText);
            Assert.Contains(".visible .entry", ptxText);

            Output.WriteLine($"Compiled kernel '{compiledKernel.Name}':");
            Output.WriteLine($"  Binary size: {compiledKernel.Binary.Length} bytes");
            Output.WriteLine($"  Contains PTX: {ptxText.Contains(".target")}");
        }

        [Fact]
        public async Task KernelCache_ShouldCompilePTXToCUBIN()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_cache == null || _accelerator == null, "Cache or accelerator not available");

            // Skip if compute capability is too old for JIT compilation
            var cc = _accelerator.Device.ComputeCapability;
            Skip.If(cc.Major < 3, $"Compute capability {cc.Major}.{cc.Minor} too old for JIT compilation");

            var kernelCode = @"
                extern ""C"" __global__ void cubinTestKernel(float* data) {
                    int idx = threadIdx.x;
                    data[idx] = idx * 3.14159f;
                }
            ";

            var definition = new KernelDefinition
            {
                Name = "cubinTestKernel",
                Code = kernelCode,
                Language = KernelLanguage.CUDA
            };

            var options = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.O2,
                TargetArchitecture = $"sm_{cc.Major}{cc.Minor}"
            };

            var cachedKernel = await _cache.GetOrCompileKernelAsync(definition, options);

            Assert.NotNull(cachedKernel);
            Assert.NotNull(cachedKernel.PtxCode);
            Assert.NotNull(cachedKernel.CubinCode);
            Assert.True(cachedKernel.CubinCode.Length > 0);

            // CUBIN should be different from PTX (binary format)
            Assert.NotEqual(cachedKernel.PtxCode, cachedKernel.CubinCode);
            
            // CUBIN typically starts with ELF magic number for Linux or different header for Windows
            var hasCubinHeader = cachedKernel.CubinCode.Length > 4 && 
                                (cachedKernel.CubinCode[0] == 0x7F || // ELF
                                 cachedKernel.CubinCode[0] == 0x4E); // NVIDIA format

            Output.WriteLine($"Kernel compilation results:");
            Output.WriteLine($"  PTX size: {cachedKernel.PtxCode.Length} bytes");
            Output.WriteLine($"  CUBIN size: {cachedKernel.CubinCode.Length} bytes");
            Output.WriteLine($"  Has CUBIN header: {hasCubinHeader}");
            Output.WriteLine($"  Compilation time: {cachedKernel.CompilationTime:F2}ms");
        }

        [Fact]
        public async Task KernelCache_ShouldCacheCompiledKernels()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_cache == null, "Cache not available");

            var kernelCode = @"
                extern ""C"" __global__ void cacheTestKernel(float* data) {
                    data[threadIdx.x] = threadIdx.x;
                }
            ";

            var definition = new KernelDefinition
            {
                Name = "cacheTestKernel",
                Code = kernelCode,
                Language = KernelLanguage.CUDA
            };

            var options = new CompilationOptions();

            // First compilation
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var kernel1 = await _cache.GetOrCompileKernelAsync(definition, options);
            sw.Stop();
            var firstCompileTime = sw.ElapsedMilliseconds;

            // Second compilation (should be cached)
            sw.Restart();
            var kernel2 = await _cache.GetOrCompileKernelAsync(definition, options);
            sw.Stop();
            var cachedTime = sw.ElapsedMilliseconds;

            Assert.NotNull(kernel1);
            Assert.NotNull(kernel2);
            
            // Should return the same cached instance
            Assert.Equal(kernel1.KernelHash, kernel2.KernelHash);
            Assert.Equal(kernel1.PtxCode, kernel2.PtxCode);

            // Cached retrieval should be much faster
            Assert.True(cachedTime < firstCompileTime / 2, 
                $"Cache retrieval ({cachedTime}ms) not faster than compilation ({firstCompileTime}ms)");

            var stats = _cache.GetStatistics();
            Assert.True(stats.CacheHits > 0);
            Assert.Equal(1, stats.CacheMisses);
            Assert.True(stats.CacheHitRatio > 0);

            Output.WriteLine($"Cache performance:");
            Output.WriteLine($"  First compile: {firstCompileTime}ms");
            Output.WriteLine($"  Cached retrieval: {cachedTime}ms");
            Output.WriteLine($"  Speed improvement: {firstCompileTime / Math.Max(1, cachedTime):F1}x");
            Output.WriteLine($"  Cache stats: Hits={stats.CacheHits}, Misses={stats.CacheMisses}, Ratio={stats.CacheHitRatio:F2}");
        }

        [Fact]
        public async Task KernelCompiler_ShouldHandleCompilationErrors()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_compiler == null, "Compiler not available");

            var invalidKernelCode = @"
                extern ""C"" __global__ void errorKernel(float* data) {
                    // Syntax error: missing semicolon
                    int idx = threadIdx.x
                    data[idx] = idx;
                }
            ";

            var definition = new KernelDefinition
            {
                Name = "errorKernel",
                Code = invalidKernelCode,
                Language = KernelLanguage.CUDA
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _compiler.CompileAsync(definition, new CompilationOptions());
            });

            Output.WriteLine("Compilation error handling verified");
        }

        [Fact]
        public async Task KernelCompiler_ShouldApplyOptimizationLevels()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_compiler == null, "Compiler not available");

            var kernelCode = @"
                extern ""C"" __global__ void optimizedKernel(float* data, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        float temp = data[idx];
                        temp = temp * 2.0f + 1.0f;
                        temp = temp / 3.0f - 0.5f;
                        data[idx] = temp;
                    }
                }
            ";

            var definition = new KernelDefinition
            {
                Name = "optimizedKernel",
                Code = kernelCode,
                Language = KernelLanguage.CUDA
            };

            // Compile with different optimization levels
            var noOptKernel = await _compiler.CompileAsync(definition, new CompilationOptions 
            { 
                OptimizationLevel = OptimizationLevel.O0 
            });

            var fullOptKernel = await _compiler.CompileAsync(definition, new CompilationOptions 
            { 
                OptimizationLevel = OptimizationLevel.O3,
                EnableFastMath = true
            });

            Assert.NotNull(noOptKernel);
            Assert.NotNull(fullOptKernel);

            // Optimized version may have different size (usually smaller)
            // Note: This is not always guaranteed, depends on the specific optimizations
            Output.WriteLine($"Optimization comparison:");
            Output.WriteLine($"  No optimization (O0): {noOptKernel.Binary.Length} bytes");
            Output.WriteLine($"  Full optimization (O3 + fast-math): {fullOptKernel.Binary.Length} bytes");

            // Both should still contain the kernel entry point
            var noOptPtx = Encoding.UTF8.GetString(noOptKernel.Binary);
            var fullOptPtx = Encoding.UTF8.GetString(fullOptKernel.Binary);
            
            Assert.Contains("optimizedKernel", noOptPtx);
            Assert.Contains("optimizedKernel", fullOptPtx);
        }

        [Fact]
        public async Task KernelCache_ShouldPersistToDisk()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_cache == null, "Cache not available");

            var kernelCode = @"
                extern ""C"" __global__ void persistTestKernel(float* data) {
                    data[threadIdx.x] = threadIdx.x * 2.0f;
                }
            ";

            var definition = new KernelDefinition
            {
                Name = "persistTestKernel",
                Code = kernelCode,
                Language = KernelLanguage.CUDA
            };

            var cachedKernel = await _cache.GetOrCompileKernelAsync(definition, new CompilationOptions());
            Assert.NotNull(cachedKernel);

            // Save cache to disk
            var tempPath = Path.GetTempFileName();
            try
            {
                await _cache.SaveCacheToDiskAsync(tempPath);
                Assert.True(File.Exists(tempPath));
                
                var fileSize = new FileInfo(tempPath).Length;
                Assert.True(fileSize > 0);
                
                Output.WriteLine($"Cache persisted to disk:");
                Output.WriteLine($"  Path: {tempPath}");
                Output.WriteLine($"  Size: {fileSize} bytes");

                // Create new cache and load from disk
                var newCache = new CudaKernelCache(_accelerator!.Device, _logger);
                var loadedCount = await newCache.LoadCacheFromDiskAsync(tempPath);
                
                Assert.True(loadedCount > 0);
                
                // Should find the kernel in loaded cache
                var loadedKernel = await newCache.GetOrCompileKernelAsync(definition, new CompilationOptions());
                Assert.NotNull(loadedKernel);
                Assert.Equal(cachedKernel.KernelHash, loadedKernel.KernelHash);
                
                var stats = newCache.GetStatistics();
                Assert.Equal(1, stats.CacheHits); // Should hit the loaded cache
                Assert.Equal(0, stats.CacheMisses);
                
                Output.WriteLine($"Cache loaded from disk:");
                Output.WriteLine($"  Loaded entries: {loadedCount}");
                Output.WriteLine($"  Cache hits after load: {stats.CacheHits}");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public async Task KernelLauncher_ShouldExecuteCompiledKernels()
        {
            var hasCuda = await IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            Skip.If(_accelerator == null, "Accelerator not available");

            var kernelCode = @"
                extern ""C"" __global__ void launchTestKernel(float* input, float* output, float scalar, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        output[idx] = input[idx] * scalar;
                    }
                }
            ";

            var kernel = await _accelerator.CompileKernelAsync(
                new KernelDefinition
                {
                    Name = "launchTestKernel",
                    Code = kernelCode,
                    Language = KernelLanguage.CUDA
                }
            );

            const int size = 1024;
            const float scalar = 2.5f;
            
            var inputBuffer = _accelerator.AllocateBuffer<float>(size);
            var outputBuffer = _accelerator.AllocateBuffer<float>(size);

            // Initialize input
            var inputData = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
            await inputBuffer.CopyFromHostAsync(inputData.AsMemory());

            // Execute kernel
            await kernel.ExecuteAsync(new KernelArguments
            {
                GridDimensions = new GridDimensions((size + 255) / 256, 1, 1),
                BlockDimensions = new BlockDimensions(256, 1, 1),
                Arguments = new object[] { inputBuffer, outputBuffer, scalar, size },
                DynamicSharedMemorySize = 0
            });

            // Verify results
            var outputData = new float[size];
            await outputBuffer.CopyToHostAsync(outputData.AsMemory());

            for (var i = 0; i < Math.Min(10, size); i++)
            {
                var expected = inputData[i] * scalar;
                Assert.Equal(expected, outputData[i], 3);
            }

            Output.WriteLine($"Kernel execution verified:");
            Output.WriteLine($"  Input[0] = {inputData[0]}, Output[0] = {outputData[0]} (expected {inputData[0] * scalar})");
            Output.WriteLine($"  Input[10] = {inputData[10]}, Output[10] = {outputData[10]} (expected {inputData[10] * scalar})");

            // Cleanup
            inputBuffer.Dispose();
            outputBuffer.Dispose();
            kernel.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cache?.Dispose();
                _compiler?.Dispose();
                _accelerator?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}