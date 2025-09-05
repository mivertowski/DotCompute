// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Persistent;
using DotCompute.Backends.CUDA.Persistent.Types;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    public class PersistentKernelTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<PersistentKernelTests> _logger;
        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly CudaMemoryManager _memoryManager;
        private readonly CudaKernelCompiler _compiler;
        private readonly CudaKernelLauncher _launcher;
        private readonly CudaPersistentKernelManager _persistentManager;

        public PersistentKernelTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new TestLogger<PersistentKernelTests>(output);
            
            _context = new CudaContext(0);
            _device = new CudaDevice(0, _logger);
            _memoryManager = new CudaMemoryManagerConsolidated(_context, _device, _logger);
            _compiler = new CudaKernelCompiler(_context, _logger);
            _launcher = new CudaKernelLauncher(_context, _logger);
            _persistentManager = new CudaPersistentKernelManager(
                _context, _device, _memoryManager, _launcher, _logger);
        }

        [Fact]
        public async Task RingBuffer_Allocation_Should_Create_Correct_Structure()
        {
            // Arrange
            var allocator = new CudaRingBufferAllocator(_context, _logger);
            const int depth = 3;
            const long elementsPerSlice = 1024;
            
            // Act
            var ringBuffer = await allocator.AllocateRingBufferAsync<float>(depth, elementsPerSlice);

            // Assert
            _ = ringBuffer.Should().NotBeNull();
            _ = ringBuffer.Depth.Should().Be(depth);
            _ = ringBuffer.ElementsPerSlice.Should().Be(elementsPerSlice);
            _ = ringBuffer.CurrentTimeStep.Should().Be(0);
            
            // Verify we can get slice pointers
            var slice0 = ringBuffer.GetSlicePointer(0);
            var slice1 = ringBuffer.GetSlicePointer(1);
            var slice2 = ringBuffer.GetSlicePointer(2);

            _ = slice0.Should().NotBe(IntPtr.Zero);
            _ = slice1.Should().NotBe(IntPtr.Zero);
            _ = slice2.Should().NotBe(IntPtr.Zero);
            _ = slice1.Should().NotBe(slice0);
            _ = slice2.Should().NotBe(slice1);
            
            // Cleanup
            ringBuffer.Dispose();
            allocator.Dispose();
        }

        [Fact]
        public async Task RingBuffer_Advance_Should_Update_TimeStep()
        {
            // Arrange
            var allocator = new CudaRingBufferAllocator(_context, _logger);
            var ringBuffer = await allocator.AllocateRingBufferAsync<float>(3, 256);

            // Act & Assert
            _ = ringBuffer.CurrentTimeStep.Should().Be(0);
            
            ringBuffer.Advance();
            _ = ringBuffer.CurrentTimeStep.Should().Be(1);
            
            ringBuffer.Advance();
            _ = ringBuffer.CurrentTimeStep.Should().Be(2);
            
            ringBuffer.Advance();
            _ = ringBuffer.CurrentTimeStep.Should().Be(0); // Should wrap around
            
            // Cleanup
            ringBuffer.Dispose();
            allocator.Dispose();
        }

        [Fact]
        public async Task WaveRingBuffer_Should_Provide_Correct_Pointers()
        {
            // Arrange
            var allocator = new CudaRingBufferAllocator(_context, _logger);
            const int gridWidth = 128;
            const int gridHeight = 64;
            
            // Act
            var waveBuffer = await allocator.AllocateWaveBufferAsync<float>(
                gridWidth, gridHeight, gridDepth: 1, temporalDepth: 3);

            // Assert
            _ = waveBuffer.Should().NotBeNull();
            _ = waveBuffer.GridDimensions.Should().Be((gridWidth, gridHeight, 1));
            
            var current = waveBuffer.Current;
            var previous = waveBuffer.Previous;
            var twoStepsAgo = waveBuffer.TwoStepsAgo;

            _ = current.Should().NotBe(IntPtr.Zero);
            _ = previous.Should().NotBe(IntPtr.Zero);
            _ = twoStepsAgo.Should().NotBe(IntPtr.Zero);
            
            // After swap, pointers should rotate
            var oldCurrent = current;
            waveBuffer.SwapBuffers();
            _ = waveBuffer.Current.Should().NotBe(oldCurrent);
            
            // Cleanup
            waveBuffer.Dispose();
            allocator.Dispose();
        }

        [Fact]
        public async Task PersistentKernelConfig_Validation_Should_Catch_Invalid_Values()
        {
            // Arrange
            var config = new PersistentKernelConfig();
            
            // Test invalid ring buffer depth
            config.RingBufferDepth = 1;
            var act1 = () => config.Validate();
            _ = act1.Should().Throw<ArgumentException>()
                .WithMessage("*Ring buffer depth must be at least 2*");
            
            // Test invalid max iterations
            config.RingBufferDepth = 3;
            config.MaxIterations = 0;
            var act2 = () => config.Validate();
            _ = act2.Should().Throw<ArgumentException>()
                .WithMessage("*Max iterations must be positive*");
            
            // Test invalid block size
            config.MaxIterations = 100;
            config.BlockSize = 2048;
            var act3 = () => config.Validate();
            _ = act3.Should().Throw<ArgumentException>()
                .WithMessage("*Block size must be between 1 and 1024*");
            
            // Test invalid shared memory
            config.BlockSize = 256;
            config.SharedMemoryBytes = 50 * 1024;
            var act4 = () => config.Validate();
            _ = act4.Should().Throw<ArgumentException>()
                .WithMessage("*Shared memory exceeds typical GPU limits*");
            
            // Valid configuration should not throw
            config.SharedMemoryBytes = 16 * 1024;
            config.Validate(); // Should not throw
        }

        [Fact]
        public async Task Simple_Wave_Kernel_Should_Launch_And_Stop()
        {
            // Skip if no CUDA device available
            if (!CudaContext.IsAvailable)
            {
                _output.WriteLine("Skipping test - no CUDA device available");
                return;
            }

            // Arrange
            var kernelCode = @"
                extern ""C"" __global__ void simple_persistent_kernel(
                    float* data,
                    int* control,
                    int n,
                    int max_iter)
                {
                    int tid = blockIdx.x * blockDim.x + threadIdx.x;
                    
                    // Simple persistent loop
                    while (control[0] == 1 && control[1] < max_iter) {
                        if (tid < n) {
                            data[tid] += 1.0f;
                        }
                        
                        __syncthreads();
                        
                        if (tid == 0) {
                            atomicAdd(&control[1], 1);
                        }
                        
                        __syncthreads();
                    }
                }";

            var kernel = await _compiler.CompileKernelAsync(
                kernelCode, "simple_persistent_kernel");

            var config = new PersistentKernelConfig
            {
                GridResident = true,
                MaxIterations = 10,
                BlockSize = 64,
                RingBufferDepth = 2
            };

            // Simplified test - just verify kernel compilation and basic launch
            // Real persistent kernel testing would require the full wave kernel infrastructure TODO
            
            var dataBuffer = await _memoryManager.AllocateAsync<float>(256);
            var controlBuffer = await _memoryManager.AllocateAsync<int>(4);
            
            // Initialize control buffer
            await controlBuffer.CopyFromHostAsync(new[] { 1, 0, 0, 0 });
            
            var launchConfig = new KernelLaunchConfig(4, 64, 0);
            var stream = new CudaStream(_context);
            
            // Launch kernel (non-persistent for this test)
            await _launcher.LaunchAsync(
                kernel,
                launchConfig,
                stream,
                dataBuffer.DevicePointer,
                controlBuffer.DevicePointer,
                256,
                10);
            
            await stream.SynchronizeAsync();
            
            // Read back control buffer to verify iterations
            var control = new int[4];
            await controlBuffer.CopyToHostAsync(control);

            _ = control[1].Should().BeGreaterThan(0, "Kernel should have completed some iterations");
            
            // Cleanup
            dataBuffer.Dispose();
            controlBuffer.Dispose();
            stream.Dispose();
        }

        [Fact]
        public async Task Ring_Buffer_Copy_Operations_Should_Work()
        {
            // Arrange
            var allocator = new CudaRingBufferAllocator(_context, _logger);
            const int elements = 256;
            var ringBuffer = await allocator.AllocateRingBufferAsync<float>(3, elements);
            
            // Create test data
            var testData = new float[elements];
            for (var i = 0; i < elements; i++)
            {
                testData[i] = i * 0.5f;
            }
            
            // Act - Copy to slice
            await ringBuffer.CopyToSliceAsync(0, testData);
            
            // Copy back from slice
            var readBack = new float[elements];
            await ringBuffer.CopyFromSliceAsync(0, readBack);
            
            // Assert
            for (var i = 0; i < elements; i++)
            {
                _ = readBack[i].Should().BeApproximately(testData[i], 0.0001f);
            }
            
            // Cleanup
            ringBuffer.Dispose();
            allocator.Dispose();
        }

        [Theory]
        [InlineData(WaveEquationType.Acoustic1D, 256, 1, 1)]
        [InlineData(WaveEquationType.Acoustic2D, 64, 64, 1)]
        [InlineData(WaveEquationType.Acoustic3D, 32, 32, 32)]
        public async Task Wave_Buffer_Dimensions_Should_Match_Grid(
            WaveEquationType waveType,
            int width,
            int height,
            int depth)
        {
            // Arrange
            var allocator = new CudaRingBufferAllocator(_context, _logger);
            
            // Act
            var waveBuffer = await allocator.AllocateWaveBufferAsync<float>(
                width, height, depth, temporalDepth: 3);

            // Assert
            _ = waveBuffer.GridDimensions.Width.Should().Be(width);
            _ = waveBuffer.GridDimensions.Height.Should().Be(height);
            _ = waveBuffer.GridDimensions.Depth.Should().Be(depth);
            _ = waveBuffer.ElementsPerSlice.Should().Be((long)width * height * depth);
            
            // Cleanup
            waveBuffer.Dispose();
            allocator.Dispose();
        }

        public void Dispose()
        {
            _persistentManager?.Dispose();
            _launcher?.Dispose();
            _compiler?.Dispose();
            _memoryManager?.Dispose();
            _device?.Dispose();
            _context?.Dispose();
        }

        private class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;

            public TestLogger(ITestOutputHelper output)
            {
                _output = output;
            }

            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, 
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                _output.WriteLine($"[{logLevel}] {message}");
                if (exception != null)
                {
                    _output.WriteLine($"Exception: {exception}");
                }
            }
        }
    }
}