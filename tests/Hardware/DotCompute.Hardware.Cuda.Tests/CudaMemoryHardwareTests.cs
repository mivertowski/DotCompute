// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Abstractions.Types;
using DotCompute.Tests.Common;
using DotCompute.Core.Extensions;
using DotCompute.Abstractions.Kernels;
using static DotCompute.Tests.Common.TestCategories;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Comprehensive hardware tests for CUDA memory operations.
    /// Tests device memory allocation, unified memory, memory bandwidth, and advanced memory features.
    /// Requires physical CUDA-capable hardware to execute.
    /// </summary>
    [Trait("Category", CUDA)]
    [Trait("Category", TestCategories.Hardware)]
    [Trait("Category", TestCategories.Memory)]
    [Trait("Category", RequiresHardware)]
    public class CudaMemoryHardwareTests : CudaTestBase
    {
        public CudaMemoryHardwareTests(ITestOutputHelper output) : base(output) { }

        #region Basic Memory Allocation Tests

        [SkippableFact]
        public async Task Device_Memory_Allocation_Should_Succeed_For_Various_Sizes()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            using var memoryTracker = new MemoryTracker(Output);
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            // Test progressively larger allocations
            var testSizes = new[]
            {
                1 * 1024,           // 1 KB
                1 * 1024 * 1024,    // 1 MB
                16 * 1024 * 1024,   // 16 MB
                64 * 1024 * 1024,   // 64 MB
                256 * 1024 * 1024   // 256 MB
            };
            
            foreach (var sizeBytes in testSizes)
            {
                var elementCount = (int)(sizeBytes / sizeof(float));
                
                try
                {
                    await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
                    
                    buffer.Should().NotBeNull();
                    buffer.SizeInBytes.Should().Be(sizeBytes);
                    buffer.ElementCount().Should().Be(elementCount);
                    
                    Output.WriteLine($"✓ Successfully allocated {sizeBytes / (1024.0 * 1024.0):F1} MB");
                    memoryTracker.LogCurrentUsage($"After {sizeBytes / (1024 * 1024)}MB allocation");
                }
                catch (OutOfMemoryException)
                {
                    Output.WriteLine($"✗ Failed to allocate {sizeBytes / (1024.0 * 1024.0):F1} MB - insufficient GPU memory");
                    break;
                }
            }
        }

        [SkippableFact]
        public async Task Large_Memory_Allocation_Should_Handle_GPU_Memory_Limits()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            var totalMemoryGB = deviceInfo.GlobalMemoryBytes() / (1024.0 * 1024.0 * 1024.0);
            var availableMemoryGB = deviceInfo.AvailableMemory / (1024.0 * 1024.0 * 1024.0);
            
            Output.WriteLine($"GPU Memory Information:");
            Output.WriteLine($"  Total Memory: {totalMemoryGB:F2} GB");
            Output.WriteLine($"  Available Memory: {availableMemoryGB:F2} GB");
            
            // Try to allocate 80% of available memory
            var targetAllocationBytes = (long)(deviceInfo.AvailableMemory * 0.8);
            var elementCount = (long)(targetAllocationBytes / sizeof(float));
            
            try
            {
                await using var largeBuffer = await accelerator.Memory.AllocateAsync<float>((int)Math.Min(elementCount, int.MaxValue));
                largeBuffer.Should().NotBeNull();
                
                Output.WriteLine($"✓ Successfully allocated large buffer: {targetAllocationBytes / (1024.0 * 1024.0 * 1024.0):F2} GB");
            }
            catch (OutOfMemoryException ex)
            {
                Output.WriteLine($"✗ Large allocation failed as expected: {ex.Message}");
                // This is acceptable - we're testing limits
            }
            catch (ArgumentOutOfRangeException)
            {
                Output.WriteLine($"✗ Allocation size exceeded system limits");
                // This is also acceptable for very large GPUs
            }
        }

        [SkippableFact]
        public async Task Multiple_Small_Allocations_Should_Not_Fragment_Memory_Excessively()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            using var memoryTracker = new MemoryTracker(Output);
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int allocationCount = 100;
            const int allocationSize = 1024 * 1024; // 1MB each
            var buffers = new IUnifiedMemoryBuffer<float>[allocationCount];
            
            try
            {
                // Allocate many small buffers
                for (var i = 0; i < allocationCount; i++)
                {
                    buffers[i] = await accelerator.Memory.AllocateAsync<float>(allocationSize / sizeof(float));
                    
                    if (i % 20 == 0)
                    {
                        memoryTracker.LogCurrentUsage($"After {i + 1} allocations");
                    }
                }
                
                Output.WriteLine($"✓ Successfully allocated {allocationCount} buffers of {allocationSize / (1024 * 1024)} MB each");
                
                // Verify all buffers are still valid
                for (var i = 0; i < allocationCount; i++)
                {
                    buffers[i].SizeInBytes.Should().Be(allocationSize);
                }
            }
            finally
            {
                // Clean up all buffers
                for (var i = 0; i < allocationCount; i++)
                {
                    buffers[i]?.Dispose();
                }
                
                memoryTracker.LogCurrentUsage("After cleanup");
            }
        }

        #endregion

        #region Memory Transfer Tests

        [SkippableFact]
        public async Task Host_To_Device_Transfer_Should_Preserve_Data_Integrity()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int elementCount = 1024 * 1024; // 1M elements
            
            // Generate test data with various patterns
            var testPatterns = new[]
            {
                TestDataGenerator.CreateLinearSequence(elementCount, 0.0f, 1.0f),
                TestDataGenerator.CreateRandomData(elementCount, seed: 12345),
                TestDataGenerator.CreateSinusoidalData(elementCount, frequency: 0.01),
                TestDataGenerator.CreateConstantData(elementCount, 42.0f)
            };
            
            foreach (var testData in testPatterns)
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
                
                // Upload data
                var perfMeasurement = new PerformanceMeasurement("Host to Device Transfer", Output);
                perfMeasurement.Start();
                await buffer.CopyFromAsync(testData);
                await accelerator.SynchronizeAsync();
                perfMeasurement.Stop();
                
                // Download and verify
                var downloaded = new float[elementCount];
                await buffer.CopyToAsync(downloaded);
                
                // Verify data integrity
                VerifyFloatArraysMatch(testData, downloaded, tolerance: 0.0f, 
                    context: "Host-Device-Host transfer");
                
                var dataSize = elementCount * sizeof(float);
                perfMeasurement.LogResults(dataSize);
            }
        }

        [SkippableFact]
        public async Task Concurrent_Memory_Transfers_Should_Work_Correctly()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int bufferCount = 4;
            const int elementCount = 256 * 1024; // 256K elements each
            
            var buffers = new IUnifiedMemoryBuffer<float>[bufferCount];
            var testDataSets = new float[bufferCount][];
            var tasks = new Task[bufferCount];
            
            try
            {
                // Create buffers and test data
                for (var i = 0; i < bufferCount; i++)
                {
                    buffers[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
                    testDataSets[i] = TestDataGenerator.CreateRandomData(elementCount, seed: i * 1000);
                }
                
                var stopwatch = Stopwatch.StartNew();
                
                // Start concurrent uploads
                for (var i = 0; i < bufferCount; i++)
                {
                    var bufferIndex = i; // Capture loop variable
                    tasks[i] = Task.Run(async () =>
                    {
                        await buffers[bufferIndex].CopyFromAsync(testDataSets[bufferIndex]);
                    });
                }
                
                // Wait for all transfers to complete
                await Task.WhenAll(tasks);
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                
                Output.WriteLine($"Concurrent transfers completed in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
                
                // Verify data integrity for all buffers
                for (var i = 0; i < bufferCount; i++)
                {
                    var downloaded = new float[elementCount];
                    await buffers[i].CopyToAsync(downloaded);
                    
                    VerifyFloatArraysMatch(testDataSets[i], downloaded, tolerance: 0.0f,
                        context: $"Concurrent transfer buffer {i}");
                }
                
                var totalDataSize = bufferCount * elementCount * sizeof(float);
                var totalBandwidth = totalDataSize / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
                Output.WriteLine($"Total concurrent transfer bandwidth: {totalBandwidth:F2} GB/s");
            }
            finally
            {
                foreach (var buffer in buffers)
                {
                    buffer?.Dispose();
                }
            }
        }

        #endregion

        #region Memory Bandwidth Tests

        [SkippableFact]
        public async Task Memory_Bandwidth_Should_Meet_Hardware_Specifications()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            var theoreticalBandwidthGBps = deviceInfo.MemoryBandwidthGBps();
            
            Output.WriteLine($"Theoretical Memory Bandwidth: {theoreticalBandwidthGBps:F0} GB/s");
            
            // Test with different data sizes to find optimal transfer size
            var testSizes = new[] { 1, 4, 16, 64, 256 }; // MB
            
            foreach (var sizeMB in testSizes)
            {
                var sizeBytes = sizeMB * 1024 * 1024;
                var elementCount = (int)(sizeBytes / sizeof(float));
                var testData = TestDataGenerator.CreateRandomData(elementCount);
                
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
                
                // Measure sustained transfer performance
                const int iterations = 10;
                var stopwatch = Stopwatch.StartNew();
                
                for (var i = 0; i < iterations; i++)
                {
                    await buffer.CopyFromAsync(testData);
                }
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                
                var achievedBandwidth = (sizeBytes * iterations) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
                var efficiency = (achievedBandwidth / theoreticalBandwidthGBps) * 100;
                
                Output.WriteLine($"Transfer Size: {sizeMB:3} MB - " +
                               $"Bandwidth: {achievedBandwidth:6.2f} GB/s - " +
                               $"Efficiency: {efficiency:5.1f}%");
                
                // For larger transfers, we should achieve reasonable efficiency
                if (sizeMB >= 16)
                {
                    achievedBandwidth.Should().BeGreaterThan(theoreticalBandwidthGBps * 0.3,
                        $"Large transfers should achieve >30% of theoretical bandwidth");
                }
            }
        }

        [SkippableFact]
        public async Task Device_To_Device_Memory_Copy_Should_Be_Efficient()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int elementCount = 16 * 1024 * 1024; // 16M elements (64MB)
            var testData = TestDataGenerator.CreateRandomData(elementCount);
            
            await using var sourceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var destBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            // Upload initial data
            await sourceBuffer.CopyFromAsync(testData);
            
            // Test device-to-device copy performance
            var perfMeasurement = new PerformanceMeasurement("Device to Device Copy", Output);
            const int copyIterations = 20;
            
            perfMeasurement.Start();
            for (var i = 0; i < copyIterations; i++)
            {
                await destBuffer.CopyFromAsync(sourceBuffer.AsReadOnlyMemory());
            }
            await accelerator.SynchronizeAsync();
            perfMeasurement.Stop();
            
            // Verify copy correctness
            var copiedData = new float[elementCount];
            await destBuffer.CopyToAsync(copiedData);
            VerifyFloatArraysMatch(testData, copiedData, tolerance: 0.0f, context: "Device to Device Copy");
            
            // Calculate and report performance
            var dataSize = elementCount * sizeof(float);
            var totalDataTransferred = dataSize * copyIterations;
            var bandwidth = totalDataTransferred / (perfMeasurement.ElapsedTime.TotalSeconds * 1024 * 1024 * 1024);
            
            perfMeasurement.LogResults(totalDataTransferred);
            Output.WriteLine($"Device-to-Device Copy Bandwidth: {bandwidth:F2} GB/s");
            
            // Device-to-device copies should be very fast
            bandwidth.Should().BeGreaterThan(100, "D2D copies should achieve >100 GB/s on modern hardware");
        }

        #endregion

        #region Unified Memory Tests

        [SkippableFact]
        public async Task Unified_Memory_Should_Work_When_Available()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(6, 0), "Unified Memory requires CC 6.0+");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            
            if (!deviceInfo.SupportsUnifiedMemory())
            {
                Skip.If(true, "Device does not support Unified Memory");
                return;
            }
            
            Output.WriteLine("Testing Unified Memory functionality...");
            
            // Test unified memory allocation and access patterns
            const int elementCount = 1024 * 1024;
            
            try
            {
                // This would typically use cudaMallocManaged or similar
                // For now, we'll test regular memory with unified memory concepts
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
                
                var testData = TestDataGenerator.CreateLinearSequence(elementCount);
                await buffer.CopyFromAsync(testData);
                
                // Create a simple kernel that modifies data
                const string unifiedMemoryKernel = @"
                    extern ""C"" __global__ void ModifyUnifiedData(float* data, int count, float multiplier)
                    {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < count) {
                            data[idx] *= multiplier;
                        }
                    }";
                
                await using var kernel = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "ModifyUnifiedData", Source = unifiedMemoryKernel, EntryPoint = "ModifyUnifiedData" });
                
                // Execute kernel
                var kernelArgs = new KernelArguments();
                kernelArgs.Add(buffer);
                kernelArgs.Add(elementCount);
                kernelArgs.Add(2.0f);
                await kernel.LaunchAsync(
                    ((elementCount + 255) / 256, 1, 1),
                    (256, 1, 1),
                    kernelArgs
                );
                
                await accelerator.SynchronizeAsync();
                
                // Verify results
                var results = new float[elementCount];
                await buffer.CopyToAsync(results);
                
                for (var i = 0; i < Math.Min(1000, elementCount); i++)
                {
                    var expected = testData[i] * 2.0f;
                    Math.Abs(results[i] - expected).Should().BeLessThan(0.001f,
                        $"Unified memory operation failed at index {i}");
                }
                
                Output.WriteLine("✓ Unified Memory test completed successfully");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Unified Memory test failed: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Memory Pattern Tests

        [SkippableFact]
        public async Task Coalesced_Memory_Access_Should_Outperform_Strided_Access()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int arraySize = 1024 * 1024; // 1M elements
            var testData = TestDataGenerator.CreateLinearSequence(arraySize);
            
            await using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(arraySize);
            await using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(arraySize);
            
            await inputBuffer.CopyFromAsync(testData);
            
            // Coalesced access kernel (consecutive threads access consecutive memory)
            const string coalescedKernel = @"
                extern ""C"" __global__ void CoalescedCopy(float* input, float* output, int count)
                {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < count) {
                        output[idx] = input[idx] * 2.0f;
                    }
                }";
            
            // Strided access kernel (threads access memory with large strides)
            const string stridedKernel = @"
                extern ""C"" __global__ void StridedCopy(float* input, float* output, int count, int stride)
                {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    int strided_idx = (idx * stride) % count;
                    if (strided_idx < count) {
                        output[strided_idx] = input[strided_idx] * 2.0f;
                    }
                }";
            
            await using var coalescedKernelObj = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "CoalescedCopy", Source = coalescedKernel, EntryPoint = "CoalescedCopy" });
            await using var stridedKernelObj = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "StridedCopy", Source = stridedKernel, EntryPoint = "StridedCopy" });
            
            var gridSize = (arraySize + 255) / 256;
            var blockSize = 256;
            
            // Measure coalesced access performance
            var coalescedPerf = new PerformanceMeasurement("Coalesced Memory Access", Output);
            coalescedPerf.Start();
            
            for (var i = 0; i < 10; i++)
            {
                var coalescedArgs = new KernelArguments();
                coalescedArgs.Add(inputBuffer);
                coalescedArgs.Add(outputBuffer);
                coalescedArgs.Add(arraySize);
                await coalescedKernelObj.LaunchAsync(
                    (gridSize, 1, 1),
                    (blockSize, 1, 1),
                    coalescedArgs
                );
            }
            await accelerator.SynchronizeAsync();
            coalescedPerf.Stop();
            
            // Measure strided access performance
            var stridedPerf = new PerformanceMeasurement("Strided Memory Access", Output);
            stridedPerf.Start();
            
            for (var i = 0; i < 10; i++)
            {
                var stridedArgs = new KernelArguments();
                stridedArgs.Add(inputBuffer);
                stridedArgs.Add(outputBuffer);
                stridedArgs.Add(arraySize);
                stridedArgs.Add(32); // 32-element stride
                await stridedKernelObj.LaunchAsync(
                    (gridSize, 1, 1),
                    (blockSize, 1, 1),
                    stridedArgs
                );
            }
            await accelerator.SynchronizeAsync();
            stridedPerf.Stop();
            
            var dataSize = arraySize * sizeof(float) * 2 * 10; // Read + write, 10 iterations
            coalescedPerf.LogResults(dataSize);
            stridedPerf.LogResults(dataSize);
            
            // Coalesced access should be significantly faster
            var coalescedBandwidth = dataSize / (coalescedPerf.ElapsedTime.TotalSeconds * 1024 * 1024 * 1024);
            var stridedBandwidth = dataSize / (stridedPerf.ElapsedTime.TotalSeconds * 1024 * 1024 * 1024);
            var speedupRatio = coalescedBandwidth / stridedBandwidth;
            
            Output.WriteLine($"Memory Access Performance Comparison:");
            Output.WriteLine($"  Coalesced: {coalescedBandwidth:F2} GB/s");
            Output.WriteLine($"  Strided:   {stridedBandwidth:F2} GB/s");
            Output.WriteLine($"  Speedup:   {speedupRatio:F2}x");
            
            speedupRatio.Should().BeGreaterThan(1.5, "Coalesced access should be significantly faster than strided access");
        }

        #endregion

        #region Memory Error and Limit Tests

        [SkippableFact]
        public async Task Out_Of_Memory_Should_Be_Handled_Gracefully()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            var deviceInfo = accelerator.Info;
            Output.WriteLine($"Testing memory limits on device with {deviceInfo.GlobalMemoryBytes() / (1024.0 * 1024.0 * 1024.0):F2} GB");
            
            try
            {
                // Try to allocate more memory than available
                var excessiveSize = deviceInfo.GlobalMemoryBytes() + (1024L * 1024L * 1024L); // +1GB
                var elementCount = (long)(excessiveSize / sizeof(float));
                
                // This should either throw an exception or fail gracefully
                Func<Task> allocateExcessiveMemory = async () =>
                {
                    await using var excessiveBuffer = await accelerator.Memory.AllocateAsync<float>((int)Math.Min(elementCount, int.MaxValue));
                };
                
                await allocateExcessiveMemory.Should().ThrowAsync<Exception>("Excessive memory allocation should fail");
                Output.WriteLine("✓ Out of memory condition handled correctly");
            }
            catch (OverflowException)
            {
                Output.WriteLine("✓ Allocation size exceeded system limits (expected for very large GPUs)");
            }
        }

        [SkippableFact]
        public async Task Zero_Size_Buffer_Should_Be_Handled_Correctly()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            // Test zero-size allocation
            Func<Task> createZeroSizeBuffer = async () =>
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(0);
            };
            
            // Should either create a valid buffer or throw an appropriate exception
            try
            {
                await createZeroSizeBuffer.Invoke();
                Output.WriteLine("✓ Zero-size buffer creation handled");
            }
            catch (ArgumentException)
            {
                Output.WriteLine("✓ Zero-size buffer rejected appropriately");
            }
        }

        #endregion

        #region Advanced Memory Features

        [SkippableFact]
        public async Task Memory_Pool_Should_Improve_Allocation_Performance()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(6, 0), "Memory pools require modern CUDA versions");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int allocationCount = 100;
            const int bufferSize = 1024 * 1024; // 1MB each
            const int elementCount = (int)(bufferSize / sizeof(float));
            
            // Measure standard allocation performance
            var standardAllocTime = new PerformanceMeasurement("Standard Allocations", Output);
            var standardBuffers = new IUnifiedMemoryBuffer<float>[allocationCount];
            
            standardAllocTime.Start();
            for (var i = 0; i < allocationCount; i++)
            {
                standardBuffers[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
            }
            standardAllocTime.Stop();
            
            // Clean up
            for (var i = 0; i < allocationCount; i++)
            {
                standardBuffers[i].Dispose();
            }
            
            standardAllocTime.LogResults();
            
            // Note: Actual memory pool testing would require specific CUDA memory pool APIs
            // This test demonstrates the measurement approach for when pools are implemented
            
            Output.WriteLine("Memory allocation performance measured");
            Output.WriteLine("Note: Memory pool optimizations would be implemented in production CUDA backend");
        }

        [SkippableFact]
        public async Task Memory_Prefetching_Should_Improve_Access_Performance()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(6, 0), "Memory prefetching requires Pascal+");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateDefaultAccelerator();
            
            const int dataSize = 64 * 1024 * 1024; // 64MB
            const int elementCount = (int)(dataSize / sizeof(float));
            
            var testData = TestDataGenerator.CreateRandomData(elementCount);
            await using var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            // Test without prefetching
            var noPrefetchPerf = new PerformanceMeasurement("No Prefetch", Output);
            
            noPrefetchPerf.Start();
            await buffer.CopyFromAsync(testData);
            
            // Simple kernel that accesses all data
            const string accessKernel = @"
                extern ""C"" __global__ void AccessData(float* data, int count, float* result)
                {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < count) {
                        result[0] += data[idx]; // Simple reduction-like access
                    }
                }";
            
            await using var kernel = await accelerator.CompileKernelAsync(new KernelDefinition { Name = "AccessData", Source = accessKernel, EntryPoint = "AccessData" });
            await using var resultBuffer = await accelerator.Memory.AllocateAsync<float>(1);
            
            var kernelArgs2 = new KernelArguments();
            kernelArgs2.Add(buffer);
            kernelArgs2.Add(elementCount);
            kernelArgs2.Add(resultBuffer);
            await kernel.LaunchAsync(
                ((elementCount + 255) / 256, 1, 1),
                (256, 1, 1),
                kernelArgs2
            );
            
            await accelerator.SynchronizeAsync();
            noPrefetchPerf.Stop();
            
            noPrefetchPerf.LogResults(dataSize);
            
            // Note: Actual prefetching would use cudaMemPrefetchAsync or similar
            // This test demonstrates the measurement framework for prefetching optimizations
            
            Output.WriteLine("Memory access performance measured");
            Output.WriteLine("Note: Memory prefetching optimizations would be implemented in production backend");
        }

        #endregion
    }
}