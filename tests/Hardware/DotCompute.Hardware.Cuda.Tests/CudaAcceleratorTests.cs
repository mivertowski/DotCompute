// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Hardware tests for CUDA accelerator functionality.
    /// Tests device initialization, memory operations, and hardware verification.
    /// </summary>
    [Trait("Category", "RequiresCUDA")]
    public class CudaAcceleratorTests : TestBase
    {
        public CudaAcceleratorTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public void Device_Initialization_Should_Succeed_With_RTX_2000()
        {
            // Skip if CUDA hardware is not available
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            
            using var accelerator = factory.CreateAccelerator(0);
            
            accelerator.Should().NotBeNull();
            accelerator.DeviceInfo.Should().NotBeNull();
            
            Output.WriteLine($"Device Name: {accelerator.DeviceInfo.Name}");
            Output.WriteLine($"Compute Capability: {accelerator.DeviceInfo.ComputeCapability.Major}.{accelerator.DeviceInfo.ComputeCapability.Minor}");
            Output.WriteLine($"Global Memory: {accelerator.DeviceInfo.GlobalMemoryBytes / (1024.0 * 1024.0 * 1024.0):F2} GB");
            Output.WriteLine($"Multiprocessors: {accelerator.DeviceInfo.MultiprocessorCount}");
            Output.WriteLine($"CUDA Cores (est.): {accelerator.DeviceInfo.EstimatedCudaCores}");
        }

        [SkippableFact]
        public void Compute_Capability_Should_Be_8_9_For_RTX_2000()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(IsRTX2000Available(), "RTX 2000 series GPU not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            var computeCapability = accelerator.DeviceInfo.ComputeCapability;
            
            // RTX 2000 Ada series should have compute capability 8.9
            computeCapability.Major.Should().Be(8);
            computeCapability.Minor.Should().Be(9);
            accelerator.DeviceInfo.ArchitectureGeneration.Should().Be("Ada Lovelace");
        }

        [SkippableFact]
        public void Memory_Allocation_Should_Work_On_Device()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int bufferSize = 1024 * 1024; // 1 MB
            using var buffer = accelerator.CreateBuffer<float>(bufferSize / sizeof(float));
            
            buffer.Should().NotBeNull();
            buffer.SizeInBytes.Should().Be(bufferSize);
            buffer.ElementCount.Should().Be(bufferSize / sizeof(float));
            
            Output.WriteLine($"Allocated {bufferSize} bytes on device");
            Output.WriteLine($"Buffer element count: {buffer.ElementCount}");
        }

        [SkippableFact]
        public async Task Memory_Transfer_Host_To_Device_Should_Work()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int elementCount = 1024;
            var hostData = new float[elementCount];
            
            // Initialize test data
            for (int i = 0; i < elementCount; i++)
            {
                hostData[i] = i * 2.5f;
            }
            
            using var deviceBuffer = accelerator.CreateBuffer<float>(elementCount);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await deviceBuffer.WriteAsync(hostData.AsSpan(), 0);
            stopwatch.Stop();
            
            var transferRate = (elementCount * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
            
            Output.WriteLine($"Host to Device transfer completed in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"Transfer rate: {transferRate:F2} MB/s");
            
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [SkippableFact]
        public async Task Memory_Transfer_Device_To_Host_Should_Work()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int elementCount = 1024;
            var originalData = new float[elementCount];
            var resultData = new float[elementCount];
            
            // Initialize test data
            for (int i = 0; i < elementCount; i++)
            {
                originalData[i] = (float)Math.Sin(i * 0.1);
            }
            
            using var deviceBuffer = accelerator.CreateBuffer<float>(elementCount);
            
            // Upload data
            await deviceBuffer.WriteAsync(originalData.AsSpan(), 0);
            
            // Download data
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await deviceBuffer.ReadAsync(resultData.AsSpan(), 0);
            stopwatch.Stop();
            
            var transferRate = (elementCount * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
            
            Output.WriteLine($"Device to Host transfer completed in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"Transfer rate: {transferRate:F2} MB/s");
            
            // Verify data integrity
            for (int i = 0; i < elementCount; i++)
            {
                resultData[i].Should().BeApproximately(originalData[i], 0.0001f);
            }
        }

        [SkippableFact]
        public void Stream_Management_Should_Work()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            // Create multiple streams
            var stream1 = accelerator.CreateStream();
            var stream2 = accelerator.CreateStream();
            
            stream1.Should().NotBeNull();
            stream2.Should().NotBeNull();
            stream1.Should().NotBe(stream2);
            
            // Synchronize streams
            stream1.Synchronize();
            stream2.Synchronize();
            
            Output.WriteLine("Created and synchronized multiple CUDA streams");
        }

        [SkippableFact]
        public void Error_Handling_Should_Work_For_Invalid_Operations()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            // Try to allocate too much memory (should fail gracefully)
            var action = () => accelerator.CreateBuffer<float>(long.MaxValue / sizeof(float));
            
            action.Should().Throw<Exception>()
                .Which.Message.Should().Contain("memory");
            
            Output.WriteLine("Error handling test completed - excessive allocation properly rejected");
        }

        [SkippableFact]
        public void Device_Properties_Should_Be_Valid()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            var deviceInfo = accelerator.DeviceInfo;
            
            deviceInfo.DeviceIndex.Should().BeGreaterOrEqualTo(0);
            deviceInfo.Name.Should().NotBeNullOrEmpty();
            deviceInfo.ComputeCapability.Major.Should().BeGreaterThan(0);
            deviceInfo.ComputeCapability.Minor.Should().BeGreaterOrEqualTo(0);
            deviceInfo.GlobalMemoryBytes.Should().BeGreaterThan(0);
            deviceInfo.MultiprocessorCount.Should().BeGreaterThan(0);
            deviceInfo.MaxThreadsPerBlock.Should().BeGreaterThan(0);
            deviceInfo.WarpSize.Should().Be(32); // Standard CUDA warp size
            
            Output.WriteLine($"Device validation complete:");
            Output.WriteLine($"  Name: {deviceInfo.Name}");
            Output.WriteLine($"  Compute: {deviceInfo.ComputeCapability.Major}.{deviceInfo.ComputeCapability.Minor}");
            Output.WriteLine($"  Memory: {deviceInfo.GlobalMemoryBytes / (1024.0 * 1024.0):F0} MB");
            Output.WriteLine($"  SMs: {deviceInfo.MultiprocessorCount}");
            Output.WriteLine($"  Max Threads/Block: {deviceInfo.MaxThreadsPerBlock}");
        }

        [SkippableFact]
        public async Task Unified_Memory_Should_Work_If_Supported()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            if (!accelerator.DeviceInfo.SupportsUnifiedMemory)
            {
                Output.WriteLine("Unified memory not supported on this device - skipping test");
                return;
            }
            
            const int elementCount = 1024;
            var testData = new float[elementCount];
            
            for (int i = 0; i < elementCount; i++)
            {
                testData[i] = i * 0.5f;
            }
            
            using var unifiedBuffer = accelerator.CreateUnifiedBuffer<float>(elementCount);
            
            await unifiedBuffer.WriteAsync(testData.AsSpan(), 0);
            
            var resultData = new float[elementCount];
            await unifiedBuffer.ReadAsync(resultData.AsSpan(), 0);
            
            for (int i = 0; i < elementCount; i++)
            {
                resultData[i].Should().BeApproximately(testData[i], 0.0001f);
            }
            
            Output.WriteLine("Unified memory test completed successfully");
        }

        [SkippableFact]
        public void Performance_Metrics_Should_Be_Available()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            var metrics = accelerator.GetPerformanceMetrics();
            
            metrics.Should().NotBeNull();
            
            Output.WriteLine($"Performance Metrics:");
            Output.WriteLine($"  Kernel Executions: {metrics.KernelExecutions}");
            Output.WriteLine($"  Memory Transfers: {metrics.MemoryTransfers}");
            Output.WriteLine($"  Total Execution Time: {metrics.TotalExecutionTime.TotalMilliseconds:F2} ms");
        }

        /// <summary>
        /// Checks if an RTX 2000 series GPU is available
        /// </summary>
        private static bool IsRTX2000Available()
        {
            if (!IsCudaAvailable()) return false;
            
            try
            {
                var factory = new CudaAcceleratorFactory();
                using var accelerator = factory.CreateAccelerator(0);
                
                var deviceInfo = accelerator.DeviceInfo;
                return deviceInfo.IsRTX2000Ada && 
                       deviceInfo.ComputeCapability.Major == 8 && 
                       deviceInfo.ComputeCapability.Minor == 9;
            }
            catch
            {
                return false;
            }
        }
    }
}