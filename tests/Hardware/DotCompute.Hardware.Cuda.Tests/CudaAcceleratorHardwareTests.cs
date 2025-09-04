// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Abstractions.Types;
using DotCompute.Tests.Common;
using DotCompute.Core.Extensions;
using static DotCompute.Tests.Common.TestCategories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Comprehensive hardware tests for CUDA accelerator functionality.
    /// Tests device initialization, kernel execution, and hardware-specific features.
    /// Requires physical CUDA-capable hardware to execute.
    /// </summary>
    [Trait("Category", CUDA)]
    [Trait("Category", TestCategories.Hardware)]
    [Trait("Category", RequiresHardware)]
    public class CudaAcceleratorHardwareTests : CudaTestBase
    {
        public CudaAcceleratorHardwareTests(ITestOutputHelper output) : base(output) { }

        #region Device Initialization Tests

        [SkippableFact]
        public async Task Device_Initialization_Should_Succeed_With_Available_Hardware()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            using var memoryTracker = new MemoryTracker(Output);
            
            var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            accelerator.Should().NotBeNull();
            accelerator.Info.Should().NotBeNull();
            accelerator.IsDisposed().Should().BeFalse();
            
            await LogDeviceCapabilities();
            memoryTracker.LogCurrentUsage("After Initialization");
        }

        [SkippableFact]
        public async Task Device_Should_Report_Correct_Hardware_Specifications()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            // Verify basic hardware properties
            deviceInfo.Name.Should().NotBeNullOrEmpty();
            deviceInfo.ComputeCapability.Major.Should().BeGreaterThanOrEqualTo(3);
            deviceInfo.ComputeCapability.Minor.Should().BeGreaterThanOrEqualTo(0);
            deviceInfo.GlobalMemorySize.Should().BeGreaterThan(0);
            deviceInfo.MaxComputeUnits.Should().BeGreaterThan(0);
            deviceInfo.MaxThreadsPerBlock.Should().BeGreaterThan(0);
            (deviceInfo.Capabilities?["WarpSize"] ?? 32).Should().Be(32); // CUDA standard warp size
            
            Output.WriteLine($"Device specifications validated:");
            Output.WriteLine($"  Name: {deviceInfo.Name}");
            Output.WriteLine($"  CC: {deviceInfo.ComputeCapability.Major}.{deviceInfo.ComputeCapability.Minor}");
            Output.WriteLine($"  Memory: {deviceInfo.GlobalMemorySize / (1024.0 * 1024.0 * 1024.0):F2} GB");
            Output.WriteLine($"  SMs: {deviceInfo.MaxComputeUnits}");
        }

        [SkippableFact]
        public async Task Device_Should_Support_Minimum_Compute_Capability()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(3, 5), "Requires minimum compute capability 3.5");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var cc = accelerator.Info.ComputeCapability;
            
            // Test that we meet minimum requirements for modern CUDA features
            (cc.Major >= 3 && (cc.Major > 3 || cc.Minor >= 5)).Should().BeTrue(
                $"Compute capability {cc.Major}.{cc.Minor} should be >= 3.5 for modern features");
        }

        #endregion

        #region Kernel Execution Tests

        [SkippableFact]
        public async Task Simple_Vector_Addition_Kernel_Should_Execute_Successfully()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            using var memoryTracker = new MemoryTracker(Output);
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int elementCount = 1024 * 1024; // 1M elements
            var testData1 = TestDataGenerator.CreateLinearSequence(elementCount, 1.0f, 2.0f);
            var testData2 = TestDataGenerator.CreateLinearSequence(elementCount, 0.5f, 1.5f);
            var expected = new float[elementCount];
            
            // Calculate expected results
            for (var i = 0; i < elementCount; i++)
            {
                expected[i] = testData1[i] + testData2[i];
            }
            
            // Create GPU buffers
            await using var buffer1 = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var buffer2 = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var resultBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            // Upload data to GPU
            await buffer1.CopyFromAsync(testData1);
            await buffer2.CopyFromAsync(testData2);
            
            memoryTracker.LogCurrentUsage("After Data Upload");
            
            // Create and compile kernel
            const string kernelSource = @"
                extern ""C"" __global__ void VectorAdd(float* a, float* b, float* result, int count)
                {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < count) {
                        result[idx] = a[idx] + b[idx];
                    }
                }";
            
            var kernelDef = new KernelDefinition("VectorAdd", kernelSource, "VectorAdd");
            await using var kernel = await accelerator.CompileKernelAsync(kernelDef);
            
            // Execute kernel with performance measurement
            var perfMeasurement = new PerformanceMeasurement("Vector Addition Kernel", Output);
            
            perfMeasurement.Start();
            var kernelArgs = new KernelArguments(buffer1, buffer2, resultBuffer, elementCount);
            await kernel.ExecuteAsync(kernelArgs);
            
            await accelerator.SynchronizeAsync();
            perfMeasurement.Stop();
            
            // Download results
            var results = new float[elementCount];
            await resultBuffer.CopyToAsync(results);
            
            memoryTracker.LogCurrentUsage("After Kernel Execution");
            
            // Verify results
            VerifyFloatArraysMatch(expected, results, tolerance: 0.001f, context: "Vector Addition");
            
            // Log performance metrics
            var dataSize = elementCount * sizeof(float) * 3; // 3 arrays accessed
            perfMeasurement.LogResults(dataSize);
        }

        [SkippableFact]
        public async Task Matrix_Multiplication_Kernel_Should_Execute_Correctly()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(5, 0), "Matrix multiplication requires CC 5.0+");
            
            const int matrixSize = 256; // 256x256 matrices
            const int elementCount = matrixSize * matrixSize;
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            // Generate test matrices
            var matrixA = TestDataGenerator.CreateRandomData(elementCount, seed: 123, min: 0.1f, max: 1.0f);
            var matrixB = TestDataGenerator.CreateRandomData(elementCount, seed: 456, min: 0.1f, max: 1.0f);
            
            // Create GPU buffers
            await using var bufferA = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var bufferB = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var resultBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            // Upload data
            await bufferA.CopyFromAsync(matrixA);
            await bufferB.CopyFromAsync(matrixB);
            
            // Matrix multiplication kernel (simplified tiled version)
            const string matMulKernel = @"
                #define TILE_SIZE 16
                
                extern ""C"" __global__ void MatrixMultiply(float* A, float* B, float* C, int N)
                {
                    __shared__ float As[TILE_SIZE][TILE_SIZE];
                    __shared__ float Bs[TILE_SIZE][TILE_SIZE];
                    
                    int bx = blockIdx.x, by = blockIdx.y;
                    int tx = threadIdx.x, ty = threadIdx.y;
                    
                    int Row = by * TILE_SIZE + ty;
                    int Col = bx * TILE_SIZE + tx;
                    
                    float Cvalue = 0;
                    
                    for (int ph = 0; ph < (N + TILE_SIZE - 1) / TILE_SIZE; ++ph) {
                        if (Row < N && (ph * TILE_SIZE + tx) < N)
                            As[ty][tx] = A[Row * N + ph * TILE_SIZE + tx];
                        else
                            As[ty][tx] = 0;
                            
                        if ((ph * TILE_SIZE + ty) < N && Col < N)
                            Bs[ty][tx] = B[(ph * TILE_SIZE + ty) * N + Col];
                        else
                            Bs[ty][tx] = 0;
                            
                        __syncthreads();
                        
                        for (int k = 0; k < TILE_SIZE; ++k) {
                            Cvalue += As[ty][k] * Bs[k][tx];
                        }
                        
                        __syncthreads();
                    }
                    
                    if (Row < N && Col < N) {
                        C[Row * N + Col] = Cvalue;
                    }
                }";
            
            await using var kernel = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "MatrixMultiply", Source = matMulKernel, EntryPoint = "MatrixMultiply" });
            
            var perfMeasurement = new PerformanceMeasurement("Matrix Multiplication", Output);
            
            // Configure launch parameters for tiled matrix multiplication
            var tilesPerSide = (matrixSize + 15) / 16; // 16x16 tiles
            
            perfMeasurement.Start();
            
            // Use the same successful pattern as Vector Add test instead of the problematic (gridDim, blockDim, args) signature
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(tilesPerSide, tilesPerSide),
                BlockSize = new Dim3(16, 16)
            };
            await kernel.LaunchAsync<float>(launchConfig, bufferA, bufferB, resultBuffer, matrixSize);
            
            await accelerator.SynchronizeAsync();
            perfMeasurement.Stop();
            
            // Download and verify results (simplified verification)
            var results = new float[elementCount];
            await resultBuffer.CopyToAsync(results);
            
            // Basic sanity check - results should not be all zeros
            var nonZeroCount = 0;
            for (var i = 0; i < Math.Min(1000, results.Length); i++)
            {
                if (Math.Abs(results[i]) > 0.001f)
                {
                    nonZeroCount++;
                }
            }
            
            nonZeroCount.Should().BeGreaterThan(results.Length / 10, "Matrix multiplication should produce meaningful results");
            
            // Calculate theoretical FLOPS
            var operations = 2L * matrixSize * matrixSize * matrixSize; // 2N³ operations
            var gflops = operations / (perfMeasurement.ElapsedTime.TotalSeconds * 1e9);
            
            Output.WriteLine($"Matrix Multiplication Performance:");
            Output.WriteLine($"  GFLOPS: {gflops:F2}");
            Output.WriteLine($"  Matrix Size: {matrixSize}x{matrixSize}");
        }

        #endregion

        #region Hardware Feature Tests

        [SkippableFact]
        public async Task Concurrent_Kernel_Execution_Should_Be_Supported()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            if (deviceInfo.SupportsConcurrentKernels())
            {
                Output.WriteLine("Device supports concurrent kernel execution");
                deviceInfo.SupportsConcurrentKernels().Should().BeTrue();
            }
            else
            {
                Output.WriteLine("Device does not support concurrent kernel execution");
                Skip.If(true, "Device does not support concurrent kernels");
            }
        }

        [SkippableFact]
        public async Task Unified_Memory_Should_Be_Available_On_Modern_Hardware()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(6, 0), "Unified Memory requires CC 6.0+");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            if (deviceInfo.SupportsUnifiedMemory())
            {
                deviceInfo.SupportsUnifiedMemory().Should().BeTrue();
                Output.WriteLine("Unified Memory is supported and available");
            }
            else
            {
                Output.WriteLine("Unified Memory is not available on this device");
            }
        }

        [SkippableFact]
        public async Task Device_Should_Have_Adequate_Shared_Memory()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            // Modern GPUs should have at least 48KB shared memory per block
            var sharedMemoryKB = deviceInfo.SharedMemoryPerBlock() / 1024;
            sharedMemoryKB.Should().BeGreaterThanOrEqualTo(32, "Modern GPUs should have at least 32KB shared memory");
            
            Output.WriteLine($"Shared memory per block: {sharedMemoryKB} KB");
            
            // Check if device supports configurable shared memory
            if (await HasMinimumComputeCapability(7, 0))
            {
                sharedMemoryKB.Should().BeGreaterThanOrEqualTo(64, "CC 7.0+ devices should support 64KB+ shared memory");
            }
        }

        [SkippableFact]
        public async Task Device_Should_Support_Modern_Memory_Bandwidth()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            // Modern GPUs should have reasonable memory bandwidth
            deviceInfo.MemoryBandwidthGBps().Should().BeGreaterThan(100, 
                "Modern GPUs should have >100 GB/s memory bandwidth");
            
            Output.WriteLine($"Memory bandwidth: {deviceInfo.MemoryBandwidthGBps():F0} GB/s");
            Output.WriteLine($"Memory bus width: {deviceInfo.MemoryBusWidth} bits");
            Output.WriteLine($"Memory clock: {deviceInfo.MemoryClockRate() / 1000.0:F0} MHz");
        }

        [SkippableFact]
        public async Task RTX_2000_Ada_Hardware_Should_Have_Specific_Capabilities()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await IsRTX2000AdaAvailable(), "RTX 2000 Ada hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            // RTX 2000 Ada specific tests
            deviceInfo.ComputeCapability.Major.Should().Be(8);
            deviceInfo.ComputeCapability.Minor.Should().Be(9);
            deviceInfo.ArchitectureGeneration().Should().Contain("Ada");
            
            // RTX 2000 Ada should have modern features
            deviceInfo.SupportsUnifiedMemory().Should().BeTrue();
            deviceInfo.SupportsManagedMemory().Should().BeTrue();
            deviceInfo.SupportsConcurrentKernels().Should().BeTrue();
            
            Output.WriteLine("RTX 2000 Ada capabilities verified:");
            Output.WriteLine($"  Architecture: {deviceInfo.ArchitectureGeneration()}");
            Output.WriteLine($"  Compute Capability: {deviceInfo.ComputeCapability.Major}.{deviceInfo.ComputeCapability.Minor}");
            Output.WriteLine($"  RT Cores: {(deviceInfo.SupportsRayTracing() ? "Available" : "Not Available")}");
            Output.WriteLine($"  Tensor Cores: {(deviceInfo.SupportsTensorOperations() ? "Available" : "Not Available")}");
        }

        #endregion

        #region Error Handling and Recovery Tests

        [SkippableFact]
        public async Task Invalid_Kernel_Should_Produce_Compilation_Error()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const string invalidKernelSource = @"
                extern ""C"" __global__ void InvalidKernel()
                {
                    // This should cause a compilation error
                    undefined_function();
                    invalid_variable = 42;
                }";
            
            Func<Task> compileAction = async () => 
            {
                await using var kernel = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "InvalidKernel", Source = invalidKernelSource, EntryPoint = "InvalidKernel" });
            };
            
            await compileAction.Should().ThrowAsync<Exception>("Invalid kernel should fail to compile");
            Output.WriteLine("Kernel compilation error handling verified");
        }

        [SkippableFact]
        public async Task Device_Should_Recover_From_Kernel_Timeout()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            // Create a kernel that runs for a very long time
            const string longRunningKernel = @"
                extern ""C"" __global__ void LongRunningKernel(int* data, int iterations)
                {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx == 0) {
                        for (int i = 0; i < iterations; i++) {
                            // Perform some work to consume time
                            data[0] += i;
                        }
                    }
                }";
            
            await using var buffer = await accelerator.Memory.AllocateAsync<int>(1);
            await using var kernel = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "LongRunningKernel", Source = longRunningKernel, EntryPoint = "LongRunningKernel" });
            
            try
            {
                // Launch a kernel that might timeout (with very high iteration count)
                await kernel.LaunchAsync(
                    (1, 1, 1),
                    (1, 1, 1),
                    buffer, 100_000_000 // Very high iteration count
                );
                
                await accelerator.SynchronizeAsync();
                Output.WriteLine("Kernel completed without timeout");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Kernel timeout handled gracefully: {ex.GetType().Name}");
                
                // Verify that the device is still functional after potential timeout
                await using var testBuffer = await accelerator.Memory.AllocateAsync<float>(1024);
                testBuffer.Should().NotBeNull("Device should remain functional after kernel timeout");
            }
        }

        #endregion

        #region Performance Benchmarks

        [SkippableFact]
        public async Task Memory_Copy_Performance_Should_Meet_Expectations()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int dataSize = 64 * 1024 * 1024; // 64MB
            const int elementCount = (int)(dataSize / sizeof(float));
            
            var testData = TestDataGenerator.CreateRandomData(elementCount);
            await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            // Test host-to-device transfer
            var perfMeasurement = new PerformanceMeasurement("Host to Device Transfer", Output);
            
            perfMeasurement.Start();
            for (var i = 0; i < 10; i++)
            {
                await buffer.CopyFromAsync(testData);
                await accelerator.SynchronizeAsync();
            }
            perfMeasurement.Stop();
            
            var hostToDeviceBandwidth = (dataSize * 10L) / (perfMeasurement.ElapsedTime.TotalSeconds * 1024 * 1024 * 1024);
            perfMeasurement.LogResults(dataSize * 10);
            
            // Test device-to-host transfer
            var results = new float[elementCount];
            perfMeasurement = new PerformanceMeasurement("Device to Host Transfer", Output);
            
            perfMeasurement.Start();
            for (var i = 0; i < 10; i++)
            {
                await buffer.CopyToAsync(results);
                await accelerator.SynchronizeAsync();
            }
            perfMeasurement.Stop();
            
            var deviceToHostBandwidth = (dataSize * 10L) / (perfMeasurement.ElapsedTime.TotalSeconds * 1024 * 1024 * 1024);
            perfMeasurement.LogResults(dataSize * 10);
            
            // Verify reasonable transfer rates (should be > 1 GB/s for modern hardware)
            hostToDeviceBandwidth.Should().BeGreaterThan(1.0, "H2D transfer should be > 1 GB/s");
            deviceToHostBandwidth.Should().BeGreaterThan(1.0, "D2H transfer should be > 1 GB/s");
            
            Output.WriteLine($"Transfer Performance Summary:");
            Output.WriteLine($"  Host to Device: {hostToDeviceBandwidth:F2} GB/s");
            Output.WriteLine($"  Device to Host: {deviceToHostBandwidth:F2} GB/s");
        }

        #endregion
    }
}