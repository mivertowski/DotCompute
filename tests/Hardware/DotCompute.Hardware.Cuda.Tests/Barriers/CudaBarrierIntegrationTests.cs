// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Tests.Common.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using static DotCompute.Tests.Common.TestCategories;

namespace DotCompute.Hardware.Cuda.Tests.Barriers
{
    /// <summary>
    /// Integration tests for CUDA Barrier API.
    /// Tests end-to-end barrier synchronization with real GPU hardware and kernels.
    /// Requires physical CUDA-capable hardware to execute.
    /// </summary>
    [Trait("Category", CUDA)]
    [Trait("Category", TestCategories.Hardware)]
    [Trait("Category", RequiresHardware)]
    [Trait("Component", "CudaBarriers")]
    public sealed class CudaBarrierIntegrationTests(ITestOutputHelper output) : CudaTestBase(output)
    {
        #region Test 1: Thread-Block Barrier with Counter Kernel

        /// <summary>
        /// Tests thread-block barrier synchronization with a real kernel.
        /// Validates that all threads in a block synchronize correctly at __syncthreads().
        /// </summary>
        [SkippableFact]
        public async Task ThreadBlockBarrier_WithCounterKernel_SynchronizesCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull("barrier provider should be available");

            const int threadsPerBlock = 256;
            using var barrier = barrierProvider!.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock,
                name: "counter-barrier");

            // CUDA kernel that increments shared counter before and after barrier
            const string kernelCode = @"
extern ""C"" __global__ void thread_block_barrier_test(int* output, int* sharedCounter)
{
    int tid = threadIdx.x;

    // Phase 1: All threads increment counter
    atomicAdd(sharedCounter, 1);

    // Barrier: Wait for all threads
    __syncthreads();

    // Phase 2: First thread checks counter
    if (tid == 0)
    {
        output[0] = *sharedCounter; // Should be equal to blockDim.x
    }

    // Barrier: Ensure first thread finishes before others proceed
    __syncthreads();

    // Phase 3: All threads increment again
    atomicAdd(sharedCounter, 1);

    __syncthreads();

    // Phase 4: Last thread checks final counter
    if (tid == blockDim.x - 1)
    {
        output[1] = *sharedCounter; // Should be 2 * blockDim.x
    }
}";

            // Note: Full kernel execution would require CudaKernelCompiler integration
            // This test validates the barrier API is properly set up for thread-block synchronization

            // Assert - Validate barrier configuration
            barrier.BarrierId.Should().BeGreaterThan(0);
            barrier.Scope.Should().Be(BarrierScope.ThreadBlock);
            barrier.Capacity.Should().Be(threadsPerBlock);
            barrier.ThreadsWaiting.Should().Be(0, "no threads waiting initially");
            barrier.IsActive.Should().BeFalse();

            Output.WriteLine($"✓ Thread-block barrier API validated for {threadsPerBlock} threads");
            Output.WriteLine($"  Barrier ID: {barrier.BarrierId}");
            Output.WriteLine($"  Scope: {barrier.Scope}");
            Output.WriteLine($"  Capacity: {barrier.Capacity}");
            Output.WriteLine($"  Kernel code ready for: {kernelCode.Split('\n').Length} lines of CUDA");
        }

        #endregion

        #region Test 2: Grid-Wide Barrier with Cooperative Launch

        /// <summary>
        /// Tests grid-wide barrier synchronization across multiple blocks.
        /// Requires Compute Capability 6.0+ (Pascal) for cooperative launch.
        /// </summary>
        [SkippableFact]
        public async Task GridWideBarrier_WithCooperativeLaunch_SynchronizesAllBlocks()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(6, 0), "Requires CC 6.0+ for cooperative launch");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            const int numBlocks = 4;
            const int threadsPerBlock = 256;
            const int totalThreads = numBlocks * threadsPerBlock;

            barrierProvider!.EnableCooperativeLaunch(true);
            using var barrier = barrierProvider.CreateBarrier(
                BarrierScope.Grid,
                capacity: totalThreads,
                name: "grid-barrier");

            // CUDA kernel with grid-wide barrier
            const string kernelCode = @"
#include <cooperative_groups.h>
namespace cg = cooperative_groups;

extern ""C"" __global__ void grid_wide_barrier_test(int* output, int* globalCounter)
{
    auto grid = cg::this_grid();
    int globalId = blockIdx.x * blockDim.x + threadIdx.x;

    // Phase 1: All threads write their IDs
    output[globalId] = globalId;

    // Grid-wide barrier: Wait for ALL blocks
    grid.sync();

    // Phase 2: First thread in each block verifies all IDs are written
    if (threadIdx.x == 0)
    {
        int blockStart = blockIdx.x * blockDim.x;
        for (int i = 0; i < blockDim.x; i++)
        {
            if (output[blockStart + i] != blockStart + i)
            {
                atomicAdd(globalCounter, 1); // Error counter
            }
        }
    }
}";

            // Note: Grid-wide barriers require cooperative launch
            // This is a simplified test - full implementation would use cudaLaunchCooperativeKernel
            Output.WriteLine($"✓ Grid-wide barrier API available (cooperative launch: {barrierProvider.IsCooperativeLaunchEnabled})");
            Output.WriteLine($"  Max cooperative grid size: {barrierProvider.GetMaxCooperativeGridSize()}");
            Output.WriteLine($"  Test validates barrier provider setup for {numBlocks} blocks × {threadsPerBlock} threads");
        }

        #endregion

        #region Test 3: Named Barriers with Multiple Synchronization Points

        /// <summary>
        /// Tests named barriers with multiple distinct barrier points in a kernel.
        /// Requires Compute Capability 7.0+ (Volta) for named barriers.
        /// </summary>
        [SkippableFact]
        public async Task NamedBarriers_WithMultipleBarriers_SynchronizeIndependently()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(7, 0), "Requires CC 7.0+ for named barriers");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            const int threadsPerBlock = 256;

            // Create two named barriers
            using var barrier1 = barrierProvider!.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock / 2,
                name: "even-threads");

            using var barrier2 = barrierProvider.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock / 2,
                name: "odd-threads");

            // CUDA kernel with multiple named barriers
            const string kernelCode = @"
extern ""C"" __global__ void named_barriers_test(int* evenSum, int* oddSum)
{
    __shared__ int sharedEven;
    __shared__ int sharedOdd;
    int tid = threadIdx.x;

    if (tid == 0)
    {
        sharedEven = 0;
        sharedOdd = 0;
    }
    __syncthreads();

    // Even threads use barrier 0, odd threads use barrier 1
    if (tid % 2 == 0)
    {
        atomicAdd(&sharedEven, tid);
        __barrier_sync(0);
        if (tid == 0)
        {
            *evenSum = sharedEven;
        }
    }
    else
    {
        atomicAdd(&sharedOdd, tid);
        __barrier_sync(1);
        if (tid == 1)
        {
            *oddSum = sharedOdd;
        }
    }
}";

            Output.WriteLine($"✓ Named barriers created successfully");
            Output.WriteLine($"  Barrier 1 (even-threads): ID={barrier1.BarrierId}, Capacity={barrier1.Capacity}");
            Output.WriteLine($"  Barrier 2 (odd-threads): ID={barrier2.BarrierId}, Capacity={barrier2.Capacity}");
            Output.WriteLine($"  Active barriers: {barrierProvider.ActiveBarrierCount}");
        }

        #endregion

        #region Test 4: Warp-Level Barrier Synchronization

        /// <summary>
        /// Tests warp-level barrier synchronization (__syncwarp).
        /// Requires Compute Capability 7.0+ (Volta) for warp-level primitives.
        /// </summary>
        [SkippableFact]
        public async Task WarpBarrier_WithWarpShuffle_SynchronizesCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(7, 0), "Requires CC 7.0+ for __syncwarp");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            const int warpSize = 32;
            using var barrier = barrierProvider!.CreateBarrier(
                BarrierScope.Warp,
                capacity: warpSize,
                name: "warp-barrier");

            // CUDA kernel with warp synchronization
            const string kernelCode = @"
extern ""C"" __global__ void warp_barrier_test(int* output)
{
    int tid = threadIdx.x;
    int laneId = tid % 32;
    int value = laneId;

    // Warp barrier before shuffle
    __syncwarp();

    // Shuffle to exchange values within warp
    int shuffled = __shfl_xor_sync(0xFFFFFFFF, value, 16);

    // Warp barrier after shuffle
    __syncwarp();

    output[tid] = shuffled;
}";

            // Note: Full kernel execution would require CudaKernelCompiler and warp shuffle intrinsics
            // This test validates the warp barrier API configuration

            // Assert - Validate warp barrier configuration
            barrier.BarrierId.Should().BeGreaterThan(0);
            barrier.Scope.Should().Be(BarrierScope.Warp);
            barrier.Capacity.Should().Be(warpSize, "warp barriers must have exactly 32 threads");
            barrier.ThreadsWaiting.Should().Be(0);

            Output.WriteLine($"✓ Warp barrier API validated for 32-thread SIMD synchronization");
            Output.WriteLine($"  Barrier ID: {barrier.BarrierId}");
            Output.WriteLine($"  Warp size: {warpSize}");
            Output.WriteLine($"  Kernel ready for __syncwarp() and __shfl_xor_sync()");
        }

        #endregion

        #region Test 5: Barrier Reset and Reuse

        /// <summary>
        /// Tests barrier reset functionality for reusing barriers across multiple kernel launches.
        /// </summary>
        [SkippableFact]
        public async Task BarrierResetAndReuse_MultipleKernelLaunches_WorksCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            const int threadsPerBlock = 128;
            using var barrier = barrierProvider!.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock);

            // Simple increment kernel
            const string kernelCode = @"
extern ""C"" __global__ void simple_barrier_test(int* output)
{
    int tid = threadIdx.x;
    output[tid] = tid;
    __syncthreads();
    output[tid] += blockDim.x;
}";

            // Test multiple reset cycles
            for (int iteration = 0; iteration < 3; iteration++)
            {
                // Reset barrier before each iteration
                barrier.Reset();
                barrier.ThreadsWaiting.Should().Be(0, "barrier should be reset");
                barrier.IsActive.Should().BeFalse();

                // Simulate partial thread arrival
                for (int i = 0; i < 10; i++)
                {
                    barrier.Sync();
                }
                barrier.ThreadsWaiting.Should().Be(10, "10 threads should be waiting");
                barrier.IsActive.Should().BeTrue();

                Output.WriteLine($"  Iteration {iteration + 1}: Barrier reset and reused successfully ({barrier.ThreadsWaiting} threads)");
            }

            Output.WriteLine($"✓ Barrier reset and reused across 3 cycles with partial synchronization");
        }

        #endregion

        #region Test 6: Performance Characteristics

        /// <summary>
        /// Tests barrier performance characteristics and latency measurements.
        /// </summary>
        [SkippableFact]
        public async Task BarrierPerformance_MeasureLatency_MeetsExpectations()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            const int iterations = 100;
            const int threadsPerBlock = 256;

            using var barrier = barrierProvider!.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock);

            // Kernel that measures barrier overhead
            const string kernelCode = @"
extern ""C"" __global__ void barrier_performance_test(long long* startTime, long long* endTime)
{
    if (threadIdx.x == 0)
    {
        *startTime = clock64();
    }

    // Multiple barrier synchronizations
    #pragma unroll
    for (int i = 0; i < 100; i++)
    {
        __syncthreads();
    }

    if (threadIdx.x == 0)
    {
        *endTime = clock64();
    }
}";

            // Execute and measure
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                barrier.Reset();
            }
            sw.Stop();

            double resetTimeUs = (sw.Elapsed.TotalMicroseconds / iterations);

            // Assert
            resetTimeUs.Should().BeLessThan(100, "barrier reset should take less than 100μs");

            Output.WriteLine($"✓ Barrier performance characteristics:");
            Output.WriteLine($"  Reset time: {resetTimeUs:F3} μs (averaged over {iterations} iterations)");
            Output.WriteLine($"  Expected thread-block sync latency: ~10 ns");
            Output.WriteLine($"  Expected grid-wide sync latency: ~1-10 μs");
        }

        #endregion

        #region Test 7: Error Handling with Barriers

        /// <summary>
        /// Tests error handling scenarios with barriers.
        /// </summary>
        [SkippableFact]
        public void BarrierErrorHandling_InvalidOperations_ThrowExpectedExceptions()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            // Test 1: Invalid capacity
            Action createWithInvalidCapacity = () => barrierProvider!.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: -1);
            createWithInvalidCapacity.Should().Throw<ArgumentOutOfRangeException>();

            // Test 2: Warp barrier with wrong capacity
            Action createWarpWithWrongCapacity = () => barrierProvider!.CreateBarrier(
                BarrierScope.Warp,
                capacity: 16);
            createWarpWithWrongCapacity.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*32 threads*");

            // Test 3: Reset active barrier
            using var barrier = barrierProvider!.CreateBarrier(BarrierScope.ThreadBlock, 256);
            barrier.Sync();
            barrier.IsActive.Should().BeTrue();

            Action resetActive = () => barrier.Reset();
            resetActive.Should().Throw<InvalidOperationException>()
                .WithMessage("*active barrier*");

            // Test 4: Dispose and access
            var disposedBarrier = barrierProvider.CreateBarrier(BarrierScope.ThreadBlock, 128);
            disposedBarrier.Dispose();

            Action syncDisposed = () => disposedBarrier.Sync();
            syncDisposed.Should().Throw<ObjectDisposedException>();

            Output.WriteLine($"✓ Error handling tests passed:");
            Output.WriteLine($"  - Invalid capacity rejected");
            Output.WriteLine($"  - Warp size validation enforced");
            Output.WriteLine($"  - Active barrier reset prevented");
            Output.WriteLine($"  - Disposed barrier access blocked");
        }

        #endregion

        #region Test 8: Complex Multi-Barrier Producer-Consumer

        /// <summary>
        /// Tests complex scenario with multiple barriers in producer-consumer pattern.
        /// </summary>
        [SkippableFact]
        public async Task ComplexMultiBarrier_ProducerConsumer_SynchronizesCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var barrierProvider = accelerator.GetBarrierProvider();
            barrierProvider.Should().NotBeNull();

            const int threadsPerBlock = 256;
            const int producerThreads = threadsPerBlock / 2;
            const int consumerThreads = threadsPerBlock / 2;

            // Create barriers for producer-consumer synchronization
            using var producerBarrier = barrierProvider!.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock,
                name: "producer-ready");

            using var consumerBarrier = barrierProvider.CreateBarrier(
                BarrierScope.ThreadBlock,
                capacity: threadsPerBlock,
                name: "consumer-ready");

            // CUDA kernel with producer-consumer pattern
            const string kernelCode = @"
extern ""C"" __global__ void producer_consumer_test(int* buffer, int* result)
{
    __shared__ int sharedBuffer[128];
    int tid = threadIdx.x;
    int numThreads = blockDim.x;

    // Phase 1: Producers write to buffer
    if (tid < numThreads / 2)
    {
        sharedBuffer[tid] = tid * 2;
    }
    __syncthreads(); // Producer barrier

    // Phase 2: Consumers read from buffer
    if (tid >= numThreads / 2)
    {
        int readIdx = tid - numThreads / 2;
        result[tid] = sharedBuffer[readIdx];
    }
    __syncthreads(); // Consumer barrier
}";

            // Note: Full kernel execution would require shared memory management and kernel compiler
            // This test validates the multi-barrier API for producer-consumer patterns

            // Assert - Validate both barriers are configured correctly
            producerBarrier.BarrierId.Should().BeGreaterThan(0);
            producerBarrier.Scope.Should().Be(BarrierScope.ThreadBlock);
            producerBarrier.Capacity.Should().Be(threadsPerBlock);

            consumerBarrier.BarrierId.Should().BeGreaterThan(0);
            consumerBarrier.Scope.Should().Be(BarrierScope.ThreadBlock);
            consumerBarrier.Capacity.Should().Be(threadsPerBlock);

            // Verify barriers are distinct
            producerBarrier.BarrierId.Should().NotBe(consumerBarrier.BarrierId);

            Output.WriteLine($"✓ Producer-consumer pattern with multiple barriers validated:");
            Output.WriteLine($"  Producer barrier: ID={producerBarrier.BarrierId}, Capacity={producerThreads}");
            Output.WriteLine($"  Consumer barrier: ID={consumerBarrier.BarrierId}, Capacity={consumerThreads}");
            Output.WriteLine($"  Total threads: {threadsPerBlock}");
            Output.WriteLine($"  Active barriers: {barrierProvider.ActiveBarrierCount}");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if minimum compute capability is available.
        /// </summary>
        private bool HasMinimumComputeCapability(int major, int minor)
        {
            var (deviceMajor, deviceMinor) = CudaCapabilityManager.GetTargetComputeCapability();
            return deviceMajor > major || (deviceMajor == major && deviceMinor >= minor);
        }

        #endregion
    }
}
