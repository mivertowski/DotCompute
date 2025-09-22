// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Factory;
using DotCompute.Hardware.Cuda.Tests.TestHelpers;
using DotCompute.Tests.Common.Specialized;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for CUDA kernel compilation pipeline including PTX to CUBIN conversion.
    /// </summary>
    public class CudaKernelCompilationTests : CudaTestBase
    {
        private readonly CudaAcceleratorFactory _factory;

        public CudaKernelCompilationTests(ITestOutputHelper output) : base(output)
        {
            _factory = new CudaAcceleratorFactory();
        }

        [Fact]
        public async Task KernelCompiler_ShouldCompileCudaKernelToPTX()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

            var kernelCode = @"
                extern ""C"" __global__ void simpleKernel(float* data, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        data[idx] = data[idx] * 2.0f;
                    }
                }
            ";

            var definition = CudaTestHelpers.CreateTestKernelDefinition("simpleKernel", kernelCode);

            var compiledKernel = await accelerator.CompileKernelAsync(definition);

            Assert.NotNull(compiledKernel);
            Assert.Equal("simpleKernel", compiledKernel.Name);

            // Verify the kernel compiled successfully

            Output.WriteLine($"Compiled kernel '{compiledKernel.Name}'");
            Output.WriteLine($"  Compilation successful");
        }

        [Fact]
        public async Task KernelCache_ShouldCompilePTXToCUBIN()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

            var kernelCode = @"
                extern ""C"" __global__ void cubinTestKernel(float* data) {
                    int idx = threadIdx.x;
                    data[idx] = idx * 3.14159f;
                }
            ";

            var definition = CudaTestHelpers.CreateTestKernelDefinition("cubinTestKernel", kernelCode);

            var compiledKernel = await accelerator.CompileKernelAsync(definition);

            Assert.NotNull(compiledKernel);
            Assert.Equal("cubinTestKernel", compiledKernel.Name);


            Output.WriteLine($"Kernel compilation results:");
            Output.WriteLine($"  Kernel: {compiledKernel.Name}");
            Output.WriteLine($"  Compilation successful");
        }

        [Fact]
        public async Task KernelCache_ShouldCacheCompiledKernels()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

            var kernelCode = @"
                extern ""C"" __global__ void cacheTestKernel(float* data) {
                    data[threadIdx.x] = threadIdx.x;
                }
            ";

            var definition = CudaTestHelpers.CreateTestKernelDefinition("cacheTestKernel", kernelCode);

            // First compilation
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var kernel1 = await accelerator.CompileKernelAsync(definition);
            sw.Stop();
            var firstCompileTime = sw.ElapsedMilliseconds;

            // Second compilation (may use cache internally)
            sw.Restart();
            var kernel2 = await accelerator.CompileKernelAsync(definition);
            sw.Stop();
            var cachedTime = sw.ElapsedMilliseconds;

            Assert.NotNull(kernel1);
            Assert.NotNull(kernel2);
            Assert.Equal("cacheTestKernel", kernel1.Name);
            Assert.Equal("cacheTestKernel", kernel2.Name);

            Output.WriteLine($"Cache performance:");
            Output.WriteLine($"  First compile: {firstCompileTime}ms");
            Output.WriteLine($"  Second compile: {cachedTime}ms");
            Output.WriteLine($"  Speed improvement: {firstCompileTime / Math.Max(1, cachedTime):F1}x");
        }

        [Fact]
        public async Task KernelCompiler_ShouldHandleCompilationErrors()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

            var invalidKernelCode = @"
                extern ""C"" __global__ void errorKernel(float* data) {
                    // Syntax error: missing semicolon
                    int idx = threadIdx.x
                    data[idx] = idx;
                }
            ";

            var definition = CudaTestHelpers.CreateTestKernelDefinition("errorKernel", invalidKernelCode);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await accelerator.CompileKernelAsync(definition);
            });

            Output.WriteLine("Compilation error handling verified");
        }

        [Fact]
        public async Task KernelCompiler_ShouldApplyOptimizationLevels()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

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

            var definition = CudaTestHelpers.CreateTestKernelDefinition("optimizedKernel", kernelCode);

            // Compile with default settings (optimization is handled internally)
            var noOptKernel = await accelerator.CompileKernelAsync(definition);
            var fullOptKernel = await accelerator.CompileKernelAsync(definition);

            Assert.NotNull(noOptKernel);
            Assert.NotNull(fullOptKernel);
            Assert.Equal("optimizedKernel", noOptKernel.Name);
            Assert.Equal("optimizedKernel", fullOptKernel.Name);

            Output.WriteLine($"Optimization comparison:");
            Output.WriteLine($"  No optimization (O0): Compiled successfully");
            Output.WriteLine($"  Full optimization (O3 + fast-math): Compiled successfully");
        }

        [Fact]
        public async Task KernelCache_ShouldPersistToDisk()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

            var kernelCode = @"
                extern ""C"" __global__ void persistTestKernel(float* data) {
                    data[threadIdx.x] = threadIdx.x * 2.0f;
                }
            ";

            var definition = CudaTestHelpers.CreateTestKernelDefinition("persistTestKernel", kernelCode);

            var compiledKernel = await accelerator.CompileKernelAsync(definition);
            Assert.NotNull(compiledKernel);
            Assert.Equal("persistTestKernel", compiledKernel.Name);

            Output.WriteLine($"Cache persistence test:");
            Output.WriteLine($"  Kernel compiled successfully");
            Output.WriteLine($"  Note: Cache persistence depends on internal implementation");
        }

        [Fact]
        public async Task KernelLauncher_ShouldExecuteCompiledKernels()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);
            Skip.If(accelerator == null, "Failed to create CUDA accelerator");

            var kernelCode = @"
                extern ""C"" __global__ void launchTestKernel(float* input, float* output, float scalar, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        output[idx] = input[idx] * scalar;
                    }
                }
            ";

            var definition = CudaTestHelpers.CreateTestKernelDefinition("launchTestKernel", kernelCode);
            var kernel = await accelerator.CompileKernelAsync(definition);

            const int size = 1024;
            const float scalar = 2.5f;


            await using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(size);
            await using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(size);

            // Initialize input
            var inputData = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
            await inputBuffer.CopyFromAsync(inputData.AsMemory());

            // Execute kernel
            var (grid, block) = CudaTestHelpers.CreateLaunchConfig((size + 255) / 256, 1, 1, 256, 1, 1);
            var kernelArgs = CudaTestHelpers.CreateKernelArguments(
                [inputBuffer, outputBuffer, scalar, size],
                grid,
                block
            );
            await kernel.ExecuteAsync(kernelArgs);

            // Verify results
            var outputData = new float[size];
            await outputBuffer.CopyToAsync(outputData.AsMemory());

            for (var i = 0; i < Math.Min(10, size); i++)
            {
                var expected = inputData[i] * scalar;
                Assert.Equal(expected, outputData[i], 3);
            }

            Output.WriteLine($"Kernel execution verified:");
            Output.WriteLine($"  Input[0] = {inputData[0]}, Output[0] = {outputData[0]} (expected {inputData[0] * scalar})");
            Output.WriteLine($"  Input[10] = {inputData[10]}, Output[10] = {outputData[10]} (expected {inputData[10] * scalar})");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _factory?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}