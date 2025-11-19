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

        // Assert
        Assert.Contains("#include <cuda_runtime.h>", source);
        Assert.Contains("#include <cooperative_groups.h>", source);
        Assert.Contains("#include \"ring_buffer.cuh\"", source);
        Assert.Contains("#include \"message_queue.cuh\"", source);
        Assert.Contains("#include \"memorypack_deserializer.cuh\"", source);
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
    public void GenerateKernelStub_IncludesRingBufferParameters()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("RingBuffer* input_ring", source);
        Assert.Contains("RingBuffer* output_ring", source);
        Assert.Contains("MessageQueue* input_queue", source);
        Assert.Contains("MessageQueue* output_queue", source);
        Assert.Contains("volatile int* terminate_flag", source);
    }

    [Fact]
    public void GenerateKernelStub_MapsUserParameters()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel(
            "ParameterizedKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.ParameterizedKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("int* data", source); // Span<int> → int*
        Assert.Contains("const float* weights", source); // ReadOnlySpan<float> → const float*
        Assert.Contains("int count", source); // int → int
        Assert.Contains("float threshold", source); // float → float
    }

    [Fact]
    public void GenerateKernelStub_IncludesKernelDocumentation()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var method = typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!;

        // Create kernel with custom configuration (must use object initializer for init-only properties)
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

        // Assert
        Assert.Contains("while (*terminate_flag == 0)", source);
        Assert.Contains("message_queue_has_messages(input_queue)", source);
        Assert.Contains("message_queue_dequeue(input_queue, &msg)", source);
    }

    [Fact]
    public void GenerateKernelStub_IncludesLaunchWrapper()
    {
        // Arrange
        var generator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var kernel = CreateTestKernel("TestKernel", typeof(TestKernels).GetMethod(nameof(TestKernels.SimpleKernel))!);

        // Act
        var source = generator.GenerateKernelStub(kernel);

        // Assert
        Assert.Contains("extern \"C\" cudaError_t launch_TestKernel", source);
        Assert.Contains("cudaLaunchCooperativeKernel", source);
        Assert.Contains("dim3 grid_dim", source);
        Assert.Contains("dim3 block_dim", source);
        Assert.Contains("cudaStream_t stream", source);
        Assert.Contains("void** kernel_params", source);
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
    public void EstimateSharedMemorySize_WithScalarParameters_IncludesParameterSize()
    {
        // Arrange
        var kernel = CreateTestKernel(
            "ParameterizedKernel",
            typeof(TestKernels).GetMethod(nameof(TestKernels.ParameterizedKernel))!,
            inputQueueSize: 256);

        // Act
        var size = CudaRingKernelStubGenerator.EstimateSharedMemorySize(kernel);

        // Assert
        // Should include: ring buffer (64) + message queue + 2 scalars (int + float = 8 bytes)
        Assert.True(size >= 64 + 8);
    }

    [Fact]
    public void EstimateSharedMemorySize_NullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            CudaRingKernelStubGenerator.EstimateSharedMemorySize(null!));
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
