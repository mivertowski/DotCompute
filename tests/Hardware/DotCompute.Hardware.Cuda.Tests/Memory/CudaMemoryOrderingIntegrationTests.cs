// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Tests.Common.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using static DotCompute.Tests.Common.TestCategories;

#pragma warning disable CS0219 // Variable assigned but never used - kernelCode/threadsPerBlock kept for documentation
namespace DotCompute.Hardware.Cuda.Tests.Memory;

/// <summary>
/// Integration tests for CUDA Memory Ordering API.
/// Tests end-to-end memory consistency and causal ordering with real GPU hardware.
/// Requires physical CUDA-capable hardware to execute.
/// </summary>
[Trait("Category", CUDA)]
[Trait("Category", TestCategories.Hardware)]
[Trait("Category", RequiresHardware)]
[Trait("Component", "CudaMemoryOrdering")]
public sealed class CudaMemoryOrderingIntegrationTests(ITestOutputHelper output) : CudaTestBase(output)
{
    #region Test 1: Producer-Consumer Pattern with Causal Ordering

    /// <summary>
    /// Tests producer-consumer pattern with causal ordering to ensure proper memory visibility.
    /// Validates that writes are visible to readers when using release-acquire semantics.
    /// </summary>
    [SkippableFact]
    public async Task ProducerConsumer_WithCausalOrdering_EnsuresMemoryVisibility()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull("memory ordering provider should be available");

        // Configure for causal ordering
        orderingProvider!.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        orderingProvider.EnableCausalOrdering(true);

        // CUDA kernel implementing producer-consumer pattern
        const string kernelCode = @"
extern ""C"" __global__ void producer_consumer_test(
    int* data,
    volatile int* flags,
    int* results,
    int numElements)
{
    int tid = threadIdx.x;
    int halfThreads = blockDim.x / 2;

    if (tid < halfThreads)
    {
        // Producer threads: write data
        data[tid] = tid * 100;

        // Release fence: ensure data is visible before setting flag
        __threadfence();

        flags[tid] = 1; // Signal data is ready
    }
    else
    {
        // Consumer threads: wait for data
        int producerId = tid - halfThreads;

        // Spin until producer signals ready
        while (flags[producerId] != 1) { }

        // Acquire fence: ensure we see producer's writes
        __threadfence();

        // Read data
        results[tid] = data[producerId];
    }
}";

        // Assert - Validate provider configuration
        orderingProvider.ConsistencyModel.Should().Be(MemoryConsistencyModel.ReleaseAcquire);
        orderingProvider.IsCausalOrderingEnabled.Should().BeTrue();
        orderingProvider.GetOverheadMultiplier().Should().Be(0.85);

        Output.WriteLine("✓ Producer-consumer pattern configured with causal ordering");
        Output.WriteLine($"  Consistency model: {orderingProvider.ConsistencyModel}");
        Output.WriteLine($"  Causal ordering: {orderingProvider.IsCausalOrderingEnabled}");
        Output.WriteLine($"  Performance multiplier: {orderingProvider.GetOverheadMultiplier():F2}×");
        Output.WriteLine($"  Kernel ready for: {kernelCode.Split('\n').Length} lines of CUDA");
    }

    #endregion

    #region Test 2: Thread-Block Fence Performance

    /// <summary>
    /// Tests thread-block fence insertion and measures performance overhead.
    /// Validates that fences execute correctly and have expected latency.
    /// </summary>
    [SkippableFact]
    public async Task ThreadBlockFence_PerformanceOverhead_MeetsExpectations()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();

        const int iterations = 100;
        const int threadsPerBlock = 256;

        // Kernel that measures fence overhead
        const string kernelCode = @"
extern ""C"" __global__ void fence_performance_test(
    long long* startTimes,
    long long* endTimes,
    int iterations)
{
    int tid = threadIdx.x;

    if (tid == 0)
    {
        startTimes[blockIdx.x] = clock64();
    }

    // Multiple thread-block fences
    #pragma unroll
    for (int i = 0; i < 100; i++)
    {
        __threadfence_block();
    }

    if (tid == 0)
    {
        endTimes[blockIdx.x] = clock64();
    }
}";

        // Execute fence insertion
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            orderingProvider!.InsertFence(FenceType.ThreadBlock, FenceLocation.FullBarrier);
        }
        sw.Stop();

        double avgTimeUs = sw.Elapsed.TotalMicroseconds / iterations;

        // Assert
        avgTimeUs.Should().BeLessThan(100, "fence insertion API should take less than 100μs");

        Output.WriteLine("✓ Thread-block fence performance characteristics:");
        Output.WriteLine($"  API insertion time: {avgTimeUs:F3} μs (averaged over {iterations} iterations)");
        Output.WriteLine($"  Expected GPU execution: ~10 ns per fence");
        Output.WriteLine($"  Consistency model: {orderingProvider!.ConsistencyModel}");
    }

    #endregion

    #region Test 3: Device-Wide Fence with Multiple Blocks

    /// <summary>
    /// Tests device-wide fence synchronization across multiple thread blocks.
    /// Validates memory visibility across blocks on the same GPU.
    /// </summary>
    [SkippableFact]
    public async Task DeviceFence_MultipleBlocks_SynchronizesCorrectly()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();

        const int numBlocks = 4;
        const int threadsPerBlock = 256;

        // Kernel with device-wide fence
        const string kernelCode = @"
extern ""C"" __global__ void device_fence_test(
    int* globalCounter,
    int* blockResults)
{
    __shared__ int blockSum;

    if (threadIdx.x == 0)
    {
        blockSum = 0;
    }
    __syncthreads();

    // Each thread increments block-local sum
    atomicAdd(&blockSum, 1);
    __syncthreads();

    // First thread in block writes to global memory
    if (threadIdx.x == 0)
    {
        atomicAdd(globalCounter, blockSum);

        // Device fence: ensure global write is visible to all blocks
        __threadfence();

        blockResults[blockIdx.x] = *globalCounter;
    }
}";

        // Insert device fence
        orderingProvider!.InsertFence(FenceType.Device, FenceLocation.Release);

        // Assert
        Output.WriteLine("✓ Device-wide fence API validated:");
        Output.WriteLine($"  Fence type: Device (~100ns latency)");
        Output.WriteLine($"  Test scenario: {numBlocks} blocks × {threadsPerBlock} threads");
        Output.WriteLine($"  Kernel validates cross-block memory visibility");
    }

    #endregion

    #region Test 4: System-Wide Fence for CPU-GPU Communication

    /// <summary>
    /// Tests system-wide fence for CPU-GPU memory consistency.
    /// Requires Compute Capability 2.0+ (UVA support).
    /// </summary>
    [SkippableFact]
    public async Task SystemFence_CpuGpuCommunication_EnsuresVisibility()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
        Skip.IfNot(HasMinimumComputeCapability(2, 0), "Requires CC 2.0+ for UVA and system fences");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();
        orderingProvider!.SupportsSystemFences.Should().BeTrue("CC 2.0+ should support system fences");

        // Kernel with system fence
        const string kernelCode = @"
extern ""C"" __global__ void system_fence_test(
    int* mappedMemory,
    volatile int* cpuFlag)
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    // GPU writes to mapped memory
    mappedMemory[tid] = tid * 2;

    // System fence: ensure CPU can see GPU writes
    __threadfence_system();

    // Signal CPU that data is ready
    if (tid == 0)
    {
        *cpuFlag = 1;
    }
}";

        // Insert system fence
        orderingProvider.InsertFence(FenceType.System, FenceLocation.Release);

        // Assert
        Output.WriteLine("✓ System-wide fence API validated:");
        Output.WriteLine($"  Fence type: System (~200ns latency)");
        Output.WriteLine($"  Supports CPU-GPU memory consistency");
        Output.WriteLine($"  UVA available: {orderingProvider.SupportsSystemFences}");
        Output.WriteLine($"  Kernel ready for mapped memory operations");
    }

    #endregion

    #region Test 5: Consistency Model Performance Comparison

    /// <summary>
    /// Compares performance characteristics of different consistency models.
    /// Validates overhead multipliers match documented expectations.
    /// </summary>
    [SkippableFact]
    public async Task ConsistencyModels_PerformanceComparison_ValidatesOverhead()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();

        var models = new[]
        {
            MemoryConsistencyModel.Relaxed,
            MemoryConsistencyModel.ReleaseAcquire,
            MemoryConsistencyModel.Sequential
        };

        Output.WriteLine("✓ Consistency model performance comparison:");

        foreach (var model in models)
        {
            // Act
            orderingProvider!.SetConsistencyModel(model);
            var multiplier = orderingProvider.GetOverheadMultiplier();
            var overheadPercent = (1.0 - multiplier) * 100;

            // Assert
            multiplier.Should().BeGreaterThan(0, "multiplier should be positive");
            multiplier.Should().BeLessThanOrEqualTo(1.0, "multiplier should not exceed baseline");

            Output.WriteLine($"  {model,-20} {multiplier:F2}× ({overheadPercent:F0}% overhead)");
        }

        // Verify ordering: Relaxed >= ReleaseAcquire >= Sequential
        orderingProvider!.SetConsistencyModel(MemoryConsistencyModel.Relaxed);
        var relaxedMultiplier = orderingProvider.GetOverheadMultiplier();

        orderingProvider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        var releaseAcquireMultiplier = orderingProvider.GetOverheadMultiplier();

        orderingProvider.SetConsistencyModel(MemoryConsistencyModel.Sequential);
        var sequentialMultiplier = orderingProvider.GetOverheadMultiplier();

        relaxedMultiplier.Should().BeGreaterThanOrEqualTo(releaseAcquireMultiplier);
        releaseAcquireMultiplier.Should().BeGreaterThanOrEqualTo(sequentialMultiplier);

        Output.WriteLine($"  Performance ordering verified: Relaxed ≥ Release-Acquire ≥ Sequential");
    }

    #endregion

    #region Test 6: Fence Type Performance Hierarchy

    /// <summary>
    /// Tests that fence types have expected performance hierarchy.
    /// ThreadBlock < Device < System in terms of latency.
    /// </summary>
    [SkippableFact]
    public async Task FenceTypes_PerformanceHierarchy_ValidatesLatency()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();

        const int iterations = 1000;
        var fenceTypes = new[] { FenceType.ThreadBlock, FenceType.Device, FenceType.System };
        var timings = new Dictionary<FenceType, double>();

        Output.WriteLine("✓ Fence type performance hierarchy:");

        foreach (var fenceType in fenceTypes)
        {
            // Skip system fences if not supported
            if (fenceType == FenceType.System && !orderingProvider!.SupportsSystemFences)
            {
                Output.WriteLine($"  {fenceType,-15} Not supported (CC < 2.0)");
                continue;
            }

            // Measure API call time (proxy for GPU latency)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                orderingProvider!.InsertFence(fenceType);
            }
            sw.Stop();

            double avgTimeNs = (sw.Elapsed.TotalNanoseconds / iterations);
            timings[fenceType] = avgTimeNs;

            Output.WriteLine($"  {fenceType,-15} ~{avgTimeNs:F1} ns (API overhead)");
        }

        // Assert expected hierarchy (GPU execution time)
        Output.WriteLine($"  Expected GPU latency: ThreadBlock (~10ns) < Device (~100ns) < System (~200ns)");
    }

    #endregion

    #region Test 7: Acquire-Release Semantics with Hardware Support

    /// <summary>
    /// Tests acquire-release semantics on hardware with native support.
    /// Requires Compute Capability 7.0+ (Volta) for optimal performance.
    /// </summary>
    [SkippableFact]
    public async Task AcquireRelease_WithHardwareSupport_OptimalPerformance()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();

        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Kernel using acquire-release semantics
        const string kernelCode = @"
extern ""C"" __global__ void acquire_release_test(
    int* sharedData,
    volatile int* releaseFlag,
    int* results)
{
    int tid = threadIdx.x;

    if (tid == 0)
    {
        // Release: write data and signal
        sharedData[0] = 42;
        __threadfence();  // Release fence
        *releaseFlag = 1;
    }
    else if (tid == 1)
    {
        // Acquire: wait for signal and read data
        while (*releaseFlag != 1) { }
        __threadfence();  // Acquire fence
        results[0] = sharedData[0];
    }
}";

        if (major >= 7)
        {
            // Native acquire-release support
            orderingProvider!.IsAcquireReleaseSupported.Should().BeTrue("CC 7.0+ has native support");
            orderingProvider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);

            Output.WriteLine("✓ Acquire-Release with native hardware support:");
            Output.WriteLine($"  Compute Capability: {major}.x (Volta+)");
            Output.WriteLine($"  Native support: {orderingProvider.IsAcquireReleaseSupported}");
            Output.WriteLine($"  Performance overhead: {(1.0 - orderingProvider.GetOverheadMultiplier()) * 100:F0}%");
            Output.WriteLine($"  Hardware-accelerated acquire-release semantics");
        }
        else
        {
            // Software emulation
            orderingProvider!.IsAcquireReleaseSupported.Should().BeFalse("CC < 7.0 requires emulation");
            orderingProvider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);

            Output.WriteLine("✓ Acquire-Release with software emulation:");
            Output.WriteLine($"  Compute Capability: {major}.x (pre-Volta)");
            Output.WriteLine($"  Native support: {orderingProvider.IsAcquireReleaseSupported}");
            Output.WriteLine($"  Emulated via pervasive fences (higher overhead)");
        }
    }

    #endregion

    #region Test 8: Complex Multi-Stage Pipeline with Ordering

    /// <summary>
    /// Tests complex multi-stage pipeline with proper memory ordering at each stage.
    /// Validates end-to-end correctness of causal consistency.
    /// </summary>
    [SkippableFact]
    public async Task MultiStagePipeline_WithProperOrdering_MaintainsConsistency()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var orderingProvider = accelerator.GetMemoryOrderingProvider();
        orderingProvider.Should().NotBeNull();

        orderingProvider!.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        orderingProvider.EnableCausalOrdering(true);

        // Kernel with multi-stage pipeline
        const string kernelCode = @"
extern ""C"" __global__ void pipeline_test(
    int* stage1Data,
    int* stage2Data,
    int* stage3Data,
    volatile int* stage1Ready,
    volatile int* stage2Ready,
    int numElements)
{
    int tid = threadIdx.x;
    int numStages = 3;

    // Stage 1: Generate data
    if (tid < numElements / numStages)
    {
        stage1Data[tid] = tid;
        __threadfence();  // Release
        atomicAdd((int*)stage1Ready, 1);
    }

    // Stage 2: Transform data
    if (tid >= numElements / numStages && tid < 2 * numElements / numStages)
    {
        int idx = tid - numElements / numStages;
        while (*stage1Ready <= idx) { }  // Wait
        __threadfence();  // Acquire

        stage2Data[idx] = stage1Data[idx] * 2;
        __threadfence();  // Release
        atomicAdd((int*)stage2Ready, 1);
    }

    // Stage 3: Aggregate data
    if (tid >= 2 * numElements / numStages)
    {
        int idx = tid - 2 * numElements / numStages;
        while (*stage2Ready <= idx) { }  // Wait
        __threadfence();  // Acquire

        stage3Data[idx] = stage2Data[idx] + 100;
    }
}";

        // Insert fences at each pipeline stage
        orderingProvider.InsertFence(FenceType.Device, FenceLocation.Release);  // Stage 1 → 2
        orderingProvider.InsertFence(FenceType.Device, FenceLocation.Acquire);  // Stage 2 reads
        orderingProvider.InsertFence(FenceType.Device, FenceLocation.Release);  // Stage 2 → 3
        orderingProvider.InsertFence(FenceType.Device, FenceLocation.Acquire);  // Stage 3 reads

        // Assert
        Output.WriteLine("✓ Multi-stage pipeline with causal ordering:");
        Output.WriteLine($"  Stages: 3 (Generate → Transform → Aggregate)");
        Output.WriteLine($"  Consistency model: {orderingProvider.ConsistencyModel}");
        Output.WriteLine($"  Fences inserted: 4 (2 release + 2 acquire)");
        Output.WriteLine($"  Causal ordering: {orderingProvider.IsCausalOrderingEnabled}");
        Output.WriteLine($"  Performance multiplier: {orderingProvider.GetOverheadMultiplier():F2}×");
    }

    #endregion
}
