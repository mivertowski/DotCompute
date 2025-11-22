// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Compilation;

/// <summary>
/// Tests for <see cref="CudaRingKernelStubGenerator"/>.
/// Tests the unified Ring Kernel stub generation with inline handlers and K2K messaging support.
/// </summary>
public class CudaRingKernelStubGeneratorTests
{
    #region Single Kernel Generation Tests

    [Fact]
    public void GenerateKernelStub_WithValidKernel_GeneratesCompleteSource()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.NotNull(source);
        Assert.NotEmpty(source);
        Assert.Contains("TestKernel_kernel", source);
        Assert.Contains("__global__", source);
        Assert.Contains("extern \"C\"", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesRequiredHeaders()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert - new unified API headers
        Assert.Contains("#include <cuda_runtime.h>", source);
        Assert.Contains("#include <cooperative_groups.h>", source);
        Assert.Contains("#include <cuda/atomic>", source);
        // MessageQueue struct is now inline
        Assert.Contains("struct MessageQueue", source);
        Assert.Contains("struct RingKernelControlBlock", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesMetadataComments()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("Auto-Generated CUDA Ring Kernel Stub", source);
        Assert.Contains("Kernel ID: TestKernel", source);
        Assert.Contains("DO NOT EDIT", source);
        Assert.Contains("Generated:", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesControlBlockParameter()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert - new unified API uses control block instead of individual ring buffers
        Assert.Contains("RingKernelControlBlock* control_block", source);
        Assert.Contains("control_block->should_terminate", source);
        Assert.Contains("control_block->is_active", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesMessageQueueExtraction()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert - new API extracts queues from control block
        Assert.Contains("MessageQueue* input_queue = reinterpret_cast<MessageQueue*>", source);
        Assert.Contains("MessageQueue* output_queue = reinterpret_cast<MessageQueue*>", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesKernelDocumentation()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var method = typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!;

        // Create kernel with custom configuration
        var attr = new RingKernelAttribute { KernelId = "TestKernel" };
        var kernel = new DiscoveredRingKernel
        {
            KernelId = "TestKernel",
            Method = method,
            Attribute = attr,
            Parameters = new List<KernelParameterMetadata>(),
            ContainingType = method.DeclaringType!,
            Namespace = method.DeclaringType!.Namespace ?? string.Empty,
            Capacity = 2048,
            InputQueueSize = 256,
            OutputQueueSize = 256,
            Mode = RingKernelMode.EventDriven,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue,
            Domain = RingKernelDomain.General,
            Backends = KernelBackends.CUDA
        };

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("@brief Ring Kernel: TestKernel", source);
        Assert.Contains("Capacity: 2048", source);
        Assert.Contains("Mode: EventDriven", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesCooperativeGroups()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("cooperative_groups::grid_group grid", source);
        Assert.Contains("cooperative_groups::this_grid()", source);
        Assert.Contains("grid.sync()", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesMessageProcessingLoop()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert - new unified API message loop
        Assert.Contains("while (control_block->should_terminate == 0)", source);
        Assert.Contains("control_block->is_active == 1", source);
        Assert.Contains("input_queue->try_dequeue", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesLaunchWrapper_WhenRequested()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act - request host launcher generation
        var source = generator.GenerateKernelStub(kernel, includeHostLauncher: true);

        // Assert
        Assert.Contains("extern \"C\" cudaError_t launch_TestKernel", source);
        Assert.Contains("cudaLaunchCooperativeKernel", source);
        Assert.Contains("dim3 grid_dim", source);
        Assert.Contains("dim3 block_dim", source);
        Assert.Contains("cudaStream_t stream", source);
        Assert.Contains("void** kernel_params", source);
    }

    [Fact]
    public void GenerateKernelStub_ExcludesLaunchWrapper_ByDefault()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act - default behavior (no host launcher for JIT mode)
        var source = generator.GenerateKernelStub(kernel);

        // Assert - no launch wrapper
        Assert.DoesNotContain("extern \"C\" cudaError_t launch_TestKernel", source);
    }

    [Fact]
    public void GenerateKernelStub_NullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GenerateKernelStub(null!));
    }

    #endregion

    #region Batch Generation Tests

    [Fact]
    public void GenerateBatchKernelStubs_WithMultipleKernels_GeneratesCombinedSource()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernels = new[]
        {
            CreateTestKernel("Kernel1", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!),
            CreateTestKernel("Kernel2", typeof(TestKernels).GetMethod(nameof(TestKernels.ParameterizedKernel))!),
            CreateTestKernel("Kernel3", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!)
        };

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels, "TestBatch");

        // Assert
        Assert.NotNull(source);
        Assert.NotEmpty(source);
        Assert.Contains("Unit Name: TestBatch", source);
        Assert.Contains("Kernel Count: 3", source);
        Assert.Contains("Kernel1_kernel", source);
        Assert.Contains("Kernel2_kernel", source);
        Assert.Contains("Kernel3_kernel", source);
    }

    [Fact]
    public void GenerateBatchKernelStubs_IncludesCommonHeadersOnce()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernels = new[]
        {
            CreateTestKernel("Kernel1", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!),
            CreateTestKernel("Kernel2", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!)
        };

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels);

        // Assert
        var includeCount = source.Split("#include <cuda_runtime.h>").Length - 1;
        Assert.Equal(1, includeCount); // Should only appear once
    }

    [Fact]
    public void GenerateBatchKernelStubs_WithSeparatorComments()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernels = new[]
        {
            CreateTestKernel("Kernel1", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!),
            CreateTestKernel("Kernel2", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!)
        };

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels);

        // Assert
        Assert.Contains("Ring Kernel: Kernel1", source);
        Assert.Contains("Ring Kernel: Kernel2", source);
        Assert.Contains("============================================================================", source);
    }

    [Fact]
    public void GenerateBatchKernelStubs_WithEmptyCollection_ReturnsEmptyString()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernels = Array.Empty<DiscoveredRingKernel>();

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels);

        // Assert
        Assert.NotNull(source);
        Assert.Empty(source);
    }

    [Fact]
    public void GenerateBatchKernelStubs_NullKernels_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            generator.GenerateBatchKernelStubs(null!));
    }

    [Fact]
    public void GenerateBatchKernelStubs_NullCompilationUnitName_ThrowsArgumentException()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernels = new[]
        {
            CreateTestKernel("Kernel1", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!)
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            generator.GenerateBatchKernelStubs(kernels, null!));
    }

    #endregion

    #region K2K Messaging Infrastructure Tests

    [Fact]
    public void GenerateBatchKernelStubs_WithK2KKernel_IncludesK2KInfrastructure()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateK2KTestKernel("K2KKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);
        var kernels = new[] { kernel };

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels, "K2KBatch");

        // Assert - K2K infrastructure
        Assert.Contains("K2K (Kernel-to-Kernel) Messaging Infrastructure", source);
        Assert.Contains("K2KMessageRegistry", source);
        Assert.Contains("k2k_send", source);
        Assert.Contains("k2k_try_receive", source);
        Assert.Contains("k2k_pending_count", source);
    }

    [Fact]
    public void GenerateBatchKernelStubs_WithTemporalKernel_IncludesHLCInfrastructure()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "TemporalKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            enableTimestamps: true);
        var kernels = new[] { kernel };

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels, "TemporalBatch");

        // Assert - HLC infrastructure
        Assert.Contains("Hybrid Logical Clock (HLC) Infrastructure", source);
        Assert.Contains("HlcTimestamp", source);
        Assert.Contains("hlc_now", source);
        Assert.Contains("hlc_tick", source);
        Assert.Contains("hlc_update", source);
    }

    [Fact]
    public void GenerateBatchKernelStubs_WithWarpPrimitivesKernel_IncludesWarpHelpers()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateWarpPrimitivesTestKernel("WarpKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);
        var kernels = new[] { kernel };

        // Act
        var source = generator.GenerateBatchKernelStubs(kernels, "WarpBatch");

        // Assert - warp reduction helpers
        Assert.Contains("Warp Reduction Helpers", source);
        Assert.Contains("warp_reduce_sum", source);
        Assert.Contains("warp_reduce_max", source);
        Assert.Contains("warp_reduce_min", source);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateKernelForCuda_WithValidKernel_ReturnsTrue()
    {
        // Arrange
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var isValid = CudaRingKernelStubGenerator.ValidateKernelForCuda(kernel);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateKernelForCuda_WithInvalidCapacity_ReturnsFalse()
    {
        // Arrange
        var kernel = CreateTestKernel(
            "TestKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            capacity: 0); // Invalid

        // Act
        var isValid = CudaRingKernelStubGenerator.ValidateKernelForCuda(kernel);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateKernelForCuda_WithInvalidQueueSize_ReturnsFalse()
    {
        // Arrange
        var kernel = CreateTestKernel(
            "TestKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            inputQueueSize: -1); // Invalid

        // Act
        var isValid = CudaRingKernelStubGenerator.ValidateKernelForCuda(kernel);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateKernelForCuda_NullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            CudaRingKernelStubGenerator.ValidateKernelForCuda(null!));
    }

    #endregion

    #region Shared Memory Estimation Tests

    [Fact]
    public void EstimateSharedMemorySize_WithBasicKernel_ReturnsPositiveSize()
    {
        // Arrange
        var kernel = CreateTestKernel(
            "TestKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            inputQueueSize: 256);

        // Act
        var size = CudaRingKernelStubGenerator.EstimateSharedMemorySize(kernel);

        // Assert
        Assert.True(size > 0);
        Assert.True(size >= 64); // At least ring buffer overhead
    }

    [Fact]
    public void EstimateSharedMemorySize_NullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            CudaRingKernelStubGenerator.EstimateSharedMemorySize(null!));
    }

    #endregion

    #region Orleans.GpuBridge.Core Code Generation Tests

    [Fact]
    public void GenerateKernelStub_WithEnableTimestamps_GeneratesClockCalls()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "TimestampKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            enableTimestamps: true);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("long long kernel_start_time = clock64();", source);
        Assert.Contains("long long kernel_end_time = clock64();", source);
        Assert.Contains("control_block->total_execution_cycles", source);
    }

    [Fact]
    public void GenerateKernelStub_WithBatchProcessing_GeneratesFixedBatchLoop()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "BatchKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            processingMode: Abstractions.RingKernels.RingProcessingMode.Batch);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("const int BATCH_SIZE = 16;", source);
        Assert.Contains("for (int batch_idx = 0; batch_idx < BATCH_SIZE; batch_idx++)", source);
        Assert.Contains("// Batch mode: process up to BATCH_SIZE messages", source);
    }

    [Fact]
    public void GenerateKernelStub_WithAdaptiveProcessing_GeneratesDynamicBatchSizing()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "AdaptiveKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            processingMode: Abstractions.RingKernels.RingProcessingMode.Adaptive);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("const int ADAPTIVE_THRESHOLD = 10;", source);
        Assert.Contains("const int MAX_BATCH_SIZE = 16;", source);
        Assert.Contains("int queue_depth = input_queue != nullptr ? input_queue->size() : 0;", source);
        Assert.Contains("int batch_size = (queue_depth > ADAPTIVE_THRESHOLD) ? MAX_BATCH_SIZE : 1;", source);
        Assert.Contains("// Adaptive mode: adjust batch size based on queue depth", source);
    }

    [Fact]
    public void GenerateKernelStub_WithContinuousProcessing_GeneratesSingleMessage()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "ContinuousKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            processingMode: Abstractions.RingKernels.RingProcessingMode.Continuous);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("// Continuous mode: process single message per iteration for min latency", source);
        Assert.DoesNotContain("for (int batch_idx", source); // No batch loop
    }

    [Fact]
    public void GenerateKernelStub_WithMaxMessagesPerIteration_GeneratesIterationLimit()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "FairnessKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            maxMessagesPerIteration: 32);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("int messages_this_iteration = 0;", source);
        Assert.Contains("messages_this_iteration < 32", source);
        Assert.Contains("messages_this_iteration++;", source);
    }

    [Fact]
    public void GenerateKernelStub_WithRelaxedMemory_NoFences_WhenCausalDisabled()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "RelaxedKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            memoryConsistency: Abstractions.Memory.MemoryConsistencyModel.Relaxed,
            enableCausalOrdering: false);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert - no memory fence for relaxed with causal disabled
        Assert.DoesNotContain("__threadfence()", source);
        Assert.DoesNotContain("__threadfence_system()", source);
    }

    [Fact]
    public void GenerateKernelStub_WithReleaseAcquire_GeneratesThreadFence()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "ReleaseAcquireKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            memoryConsistency: Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("__threadfence();", source);
        Assert.Contains("// Release-acquire semantics", source);
    }

    [Fact]
    public void GenerateKernelStub_WithSequential_GeneratesSystemFence()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "SequentialKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            memoryConsistency: Abstractions.Memory.MemoryConsistencyModel.Sequential);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("__threadfence_system();", source);
        Assert.Contains("// Sequential consistency: full memory barrier", source);
    }

    [Fact]
    public void GenerateKernelStub_WithWarpBarrier_GeneratesSyncWarp()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "WarpBarrierKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            useBarriers: true,
            barrierScope: Abstractions.Barriers.BarrierScope.Warp);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("__syncwarp();", source);
        Assert.Contains("// Warp-level barrier (32 threads)", source);
    }

    [Fact]
    public void GenerateKernelStub_WithThreadBlockBarrier_GeneratesSyncThreads()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "ThreadBlockBarrierKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            useBarriers: true,
            barrierScope: Abstractions.Barriers.BarrierScope.ThreadBlock);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("__syncthreads();", source);
        Assert.Contains("// Thread-block barrier (all threads in block)", source);
    }

    [Fact]
    public void GenerateKernelStub_WithGridBarrier_GeneratesGridSync()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "GridBarrierKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            useBarriers: true,
            barrierScope: Abstractions.Barriers.BarrierScope.Grid);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("grid.sync();", source);
        Assert.Contains("// Grid-wide barrier (cooperative launch required)", source);
    }

    [Fact]
    public void GenerateKernelStub_WithComprehensiveConfiguration_GeneratesAllFeatures()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateOrleansTestKernel(
            "ComprehensiveKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!,
            enableTimestamps: true,
            processingMode: Abstractions.RingKernels.RingProcessingMode.Adaptive,
            maxMessagesPerIteration: 64,
            useBarriers: true,
            barrierScope: Abstractions.Barriers.BarrierScope.ThreadBlock,
            memoryConsistency: Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire,
            enableCausalOrdering: true);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        // Timestamps
        Assert.Contains("long long kernel_start_time = clock64();", source);

        // Adaptive processing
        Assert.Contains("const int ADAPTIVE_THRESHOLD = 10;", source);
        Assert.Contains("int queue_depth = input_queue != nullptr ? input_queue->size() : 0;", source);

        // Fairness control
        Assert.Contains("int messages_this_iteration = 0;", source);
        Assert.Contains("messages_this_iteration < 64", source);

        // Memory fence
        Assert.Contains("__threadfence();", source);

        // Barrier
        Assert.Contains("__syncthreads();", source);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test kernel metadata instance.
    /// </summary>
    private static DiscoveredRingKernel CreateTestKernel(
        string kernelId,
        MethodInfo method,
        int capacity = 1024,
        int inputQueueSize = 256,
        int outputQueueSize = 256)
    {
        var attr = new RingKernelAttribute { KernelId = kernelId };

        var parameters = method.GetParameters()
            .Select(p =>
            {
                var isBuffer = p.ParameterType.IsGenericType &&
                              (p.ParameterType.GetGenericTypeDefinition().Name.StartsWith("Span", StringComparison.Ordinal) ||
                               p.ParameterType.GetGenericTypeDefinition().Name.StartsWith("ReadOnlySpan", StringComparison.Ordinal));

                var elementType = isBuffer && p.ParameterType.IsGenericType
                    ? p.ParameterType.GetGenericArguments()[0]
                    : p.ParameterType;

                return new KernelParameterMetadata
                {
                    Name = p.Name ?? "param",
                    ParameterType = p.ParameterType,
                    ElementType = elementType,
                    IsBuffer = isBuffer,
                    IsReadOnly = p.ParameterType.IsGenericType &&
                                p.ParameterType.GetGenericTypeDefinition().Name == "ReadOnlySpan`1"
                };
            })
            .ToList();

        return new DiscoveredRingKernel
        {
            KernelId = kernelId,
            Method = method,
            Attribute = attr,
            Parameters = parameters,
            ContainingType = method.DeclaringType!,
            Namespace = method.DeclaringType!.Namespace ?? string.Empty,
            Capacity = capacity,
            InputQueueSize = inputQueueSize,
            OutputQueueSize = outputQueueSize,
            Mode = RingKernelMode.Persistent,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue,
            Domain = RingKernelDomain.General,
            Backends = KernelBackends.CUDA
        };
    }

    /// <summary>
    /// Creates a test kernel with K2K messaging enabled.
    /// </summary>
    private static DiscoveredRingKernel CreateK2KTestKernel(
        string kernelId,
        MethodInfo method)
    {
        var attr = new RingKernelAttribute { KernelId = kernelId };
        return new DiscoveredRingKernel
        {
            KernelId = kernelId,
            Method = method,
            Attribute = attr,
            Parameters = new List<KernelParameterMetadata>(),
            ContainingType = method.DeclaringType!,
            Namespace = method.DeclaringType!.Namespace ?? string.Empty,
            Capacity = 1024,
            InputQueueSize = 256,
            OutputQueueSize = 256,
            Mode = RingKernelMode.Persistent,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue,
            Domain = RingKernelDomain.General,
            Backends = KernelBackends.CUDA,
            UsesK2KMessaging = true,
            SubscribesToKernels = new[] { "OtherKernel1", "OtherKernel2" },
            PublishesToKernels = new[] { "OtherKernel3" }
        };
    }

    /// <summary>
    /// Creates a test kernel with warp primitives enabled.
    /// </summary>
    private static DiscoveredRingKernel CreateWarpPrimitivesTestKernel(
        string kernelId,
        MethodInfo method)
    {
        var attr = new RingKernelAttribute { KernelId = kernelId };
        return new DiscoveredRingKernel
        {
            KernelId = kernelId,
            Method = method,
            Attribute = attr,
            Parameters = new List<KernelParameterMetadata>(),
            ContainingType = method.DeclaringType!,
            Namespace = method.DeclaringType!.Namespace ?? string.Empty,
            Capacity = 1024,
            InputQueueSize = 256,
            OutputQueueSize = 256,
            Mode = RingKernelMode.Persistent,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue,
            Domain = RingKernelDomain.General,
            Backends = KernelBackends.CUDA,
            UsesWarpPrimitives = true
        };
    }

    /// <summary>
    /// Creates a test kernel with Orleans.GpuBridge.Core properties for testing code generation.
    /// </summary>
    private static DiscoveredRingKernel CreateOrleansTestKernel(
        string kernelId,
        MethodInfo method,
        int capacity = 1024,
        int inputQueueSize = 256,
        int outputQueueSize = 256,
        bool enableTimestamps = false,
        Abstractions.RingKernels.RingProcessingMode processingMode = Abstractions.RingKernels.RingProcessingMode.Continuous,
        int maxMessagesPerIteration = 0,
        bool useBarriers = false,
        Abstractions.Barriers.BarrierScope barrierScope = Abstractions.Barriers.BarrierScope.ThreadBlock,
        Abstractions.Memory.MemoryConsistencyModel memoryConsistency = Abstractions.Memory.MemoryConsistencyModel.Relaxed,
        bool enableCausalOrdering = false)
    {
        var attr = new RingKernelAttribute { KernelId = kernelId };

        var parameters = method.GetParameters()
            .Select(p =>
            {
                var isBuffer = p.ParameterType.IsGenericType &&
                              (p.ParameterType.GetGenericTypeDefinition().Name.StartsWith("Span", StringComparison.Ordinal) ||
                               p.ParameterType.GetGenericTypeDefinition().Name.StartsWith("ReadOnlySpan", StringComparison.Ordinal));

                var elementType = isBuffer && p.ParameterType.IsGenericType
                    ? p.ParameterType.GetGenericArguments()[0]
                    : p.ParameterType;

                return new KernelParameterMetadata
                {
                    Name = p.Name ?? "param",
                    ParameterType = p.ParameterType,
                    ElementType = elementType,
                    IsBuffer = isBuffer,
                    IsReadOnly = p.ParameterType.IsGenericType &&
                                p.ParameterType.GetGenericTypeDefinition().Name == "ReadOnlySpan`1"
                };
            })
            .ToList();

        return new DiscoveredRingKernel
        {
            KernelId = kernelId,
            Method = method,
            Attribute = attr,
            Parameters = parameters,
            ContainingType = method.DeclaringType!,
            Namespace = method.DeclaringType!.Namespace ?? string.Empty,
            Capacity = capacity,
            InputQueueSize = inputQueueSize,
            OutputQueueSize = outputQueueSize,
            Mode = RingKernelMode.Persistent,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue,
            Domain = RingKernelDomain.General,
            Backends = KernelBackends.CUDA,
            // Orleans.GpuBridge.Core properties
            EnableTimestamps = enableTimestamps,
            ProcessingMode = processingMode,
            MaxMessagesPerIteration = maxMessagesPerIteration,
            UseBarriers = useBarriers,
            BarrierScope = barrierScope,
            MemoryConsistency = memoryConsistency,
            EnableCausalOrdering = enableCausalOrdering
        };
    }

    #endregion

    #region Test Kernel Definitions

    /// <summary>
    /// Test kernels for stub generation.
    /// </summary>
    private static class TestKernels
    {
        [RingKernel(KernelId = "SimpleKernel")]
        public static void SimpleKernel(Span<int> data)
        {
        }

        [RingKernel(KernelId = "ParameterizedKernel")]
        public static void ParameterizedKernel(
            Span<int> data,
            ReadOnlySpan<float> weights,
            int count,
            float threshold)
        {
        }
    }

    #endregion
}
