// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Integration tests for VectorAdd ring kernel code generation.
/// Validates that the compiler generates correct CUDA C code for VectorAdd message processing.
/// </summary>
public class VectorAddIntegrationTests
{
    private readonly CudaRingKernelCompiler _compiler;

    public VectorAddIntegrationTests()
    {
        var logger = NullLogger<CudaRingKernelCompiler>.Instance;
        _compiler = new CudaRingKernelCompiler(logger);
    }

    [Fact(DisplayName = "Compiler: Generate valid CUDA C for VectorAdd ring kernel")]
    public void Compiler_GenerateVectorAddKernel_ShouldProduceValidCode()
    {
        // This test doesn't require GPU - tests code generation only

        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAddKernel",
            Source = "// VectorAdd kernel code",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "vectoradd_kernel",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 1024,
            Domain = RingKernelDomain.General,
            MaxInputMessageSize = 256,
            MaxOutputMessageSize = 256,
            MessagingStrategy = MessagePassingStrategy.SharedMemory
        };

        // Act
        var cudaSource = _compiler.CompileToCudaC(kernelDef, "// VectorAdd kernel code", config);

        // Assert: Generated code contains essential components
        cudaSource.Should().Contain("#include <cuda_runtime.h>");
        cudaSource.Should().Contain("#include <cooperative_groups.h>");
        cudaSource.Should().Contain("#include \"VectorAddSerialization.cu\"");
        cudaSource.Should().Contain("#define MAX_MESSAGE_SIZE");
        cudaSource.Should().Contain("struct MessageQueue");
        cudaSource.Should().Contain("struct KernelControl");
        cudaSource.Should().Contain("__global__ void");
        cudaSource.Should().Contain("while (true)"); // Persistent loop
        cudaSource.Should().Contain("process_vector_add_message");
        cudaSource.Should().Contain("unsigned char input_buffer[MAX_MESSAGE_SIZE]");
        cudaSource.Should().Contain("unsigned char output_buffer[MAX_MESSAGE_SIZE]");
        cudaSource.Should().Contain("extern \"C\"");
    }

    [Fact(DisplayName = "Compiler: VectorAdd code uses configurable MAX_MESSAGE_SIZE constant")]
    public void Compiler_VectorAddCode_ShouldUseConfigurableBufferSize()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAddKernel",
            Source = "// Test kernel",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "test_kernel",
            Mode = RingKernelMode.Persistent,
            MaxInputMessageSize = 512, // Custom size
            MaxOutputMessageSize = 512
        };

        // Act
        var cudaSource = _compiler.CompileToCudaC(kernelDef, "// Test kernel", config);

        // Assert: MAX_MESSAGE_SIZE should be set from config.MaxInputMessageSize
        cudaSource.Should().Contain("#define MAX_MESSAGE_SIZE 512");

        // Should use MAX_MESSAGE_SIZE instead of hardcoded values
        cudaSource.Should().Contain("unsigned char input_buffer[MAX_MESSAGE_SIZE]");
        cudaSource.Should().Contain("unsigned char output_buffer[MAX_MESSAGE_SIZE]");
        cudaSource.Should().Contain("process_vector_add_message");

        // Should NOT contain hardcoded buffer sizes
        cudaSource.Should().NotContain("char msg_buffer[256]");
        cudaSource.Should().NotContain("new char[");
        cudaSource.Should().NotContain("delete[]");
    }

    [Fact(DisplayName = "Compiler: VectorAdd serialization logic is correct")]
    public void Compiler_VectorAddSerialization_ShouldHaveCorrectLogic()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAddKernel",
            Source = "// Test kernel",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "test_kernel",
            Mode = RingKernelMode.Persistent,
            MaxInputMessageSize = 256,
            MaxOutputMessageSize = 256
        };

        // Act
        var cudaSource = _compiler.CompileToCudaC(kernelDef, "// Test kernel", config);

        // Assert: VectorAdd processing logic
        cudaSource.Should().Contain("process_vector_add_message(");
        cudaSource.Should().Contain("input_buffer, MAX_MESSAGE_SIZE");
        cudaSource.Should().Contain("output_buffer, MAX_MESSAGE_SIZE");
        cudaSource.Should().Contain("if (success)");
        cudaSource.Should().Contain("output_queue->try_enqueue(output_buffer)");
        cudaSource.Should().Contain("control->msg_count.fetch_add(1, cuda::memory_order_relaxed)");
    }
}
