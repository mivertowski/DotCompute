// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.RingKernels;

/// <summary>
/// Unit tests for CudaRingKernelCompiler.
/// Tests CUDA C code generation for persistent ring kernels.
/// </summary>
public class CudaRingKernelCompilerTests
{
    private readonly ILogger<CudaRingKernelCompiler> _mockLogger;
    private readonly CudaRingKernelCompiler _compiler;

    public CudaRingKernelCompilerTests()
    {
        _mockLogger = Substitute.For<ILogger<CudaRingKernelCompiler>>();
        _compiler = new CudaRingKernelCompiler(_mockLogger);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should initialize with valid logger")]
    public void Constructor_WithValidLogger_ShouldInitialize()
    {
        // Arrange & Act
        var compiler = new CudaRingKernelCompiler(_mockLogger);

        // Assert
        compiler.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should throw on null logger")]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new CudaRingKernelCompiler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region CompileToCudaC Tests

    [Fact(DisplayName = "CompileToCudaC should generate valid CUDA headers")]
    public void CompileToCudaC_ShouldGenerateValidHeaders()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel");

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("#include <cuda_runtime.h>");
        result.Should().Contain("#include <cooperative_groups.h>");
        result.Should().Contain("#include <cuda/atomic>");
        result.Should().Contain("namespace cg = cooperative_groups;");
    }

    [Fact(DisplayName = "CompileToCudaC should generate message queue structure")]
    public void CompileToCudaC_ShouldGenerateMessageQueue()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel");

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("struct MessageQueue");
        result.Should().Contain("T* buffer");
        result.Should().Contain("int capacity");
        result.Should().Contain("cuda::atomic<int>* head");
        result.Should().Contain("cuda::atomic<int>* tail");
        result.Should().Contain("bool try_enqueue");
        result.Should().Contain("bool try_dequeue");
    }

    [Fact(DisplayName = "CompileToCudaC should generate control structure")]
    public void CompileToCudaC_ShouldGenerateControlStructure()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel");

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("struct KernelControl");
        result.Should().Contain("cuda::atomic<int> active");
        result.Should().Contain("cuda::atomic<int> terminate");
        result.Should().Contain("cuda::atomic<long> msg_count");
    }

    [Fact(DisplayName = "CompileToCudaC should generate persistent kernel loop for Persistent mode")]
    public void CompileToCudaC_WithPersistentMode_ShouldGeneratePersistentLoop()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel", RingKernelMode.Persistent);

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("while (true)");
        result.Should().Contain("control->terminate.load");
        result.Should().Contain("control->active.load");
        result.Should().Contain("__nanosleep(1000)");
        result.Should().Contain("input_queue->try_dequeue");
    }

    [Fact(DisplayName = "CompileToCudaC should generate event-driven loop for EventDriven mode")]
    public void CompileToCudaC_WithEventDrivenMode_ShouldGenerateEventDrivenLoop()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel", RingKernelMode.EventDriven);

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("// Event-driven mode");
        result.Should().Contain("while (input_queue->try_dequeue");
        result.Should().Contain("int processed = 0");
        result.Should().NotContain("while (true)");
    }

    [Fact(DisplayName = "CompileToCudaC should add grid sync for GraphAnalytics domain")]
    public void CompileToCudaC_WithGraphAnalyticsDomain_ShouldAddGridSync()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel", domain: RingKernelDomain.GraphAnalytics);

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("// Graph analytics: Synchronize after message processing");
        result.Should().Contain("grid.sync()");
    }

    [Fact(DisplayName = "CompileToCudaC should generate helper functions")]
    public void CompileToCudaC_ShouldGenerateHelperFunctions()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel");

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("extern \"C\"");
        result.Should().Contain("cudaError_t init_message_queue");
        result.Should().Contain("cudaMalloc(&q, sizeof(MessageQueue<char>))");
        result.Should().Contain("cudaMalloc(&q->buffer");
        result.Should().Contain("cudaMalloc(&q->head");
        result.Should().Contain("cudaMalloc(&q->tail");
    }

    [Fact(DisplayName = "CompileToCudaC should sanitize kernel ID with special characters")]
    public void CompileToCudaC_WithSpecialCharsInKernelId_ShouldSanitize()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test-kernel.v2");

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("test_kernel_v2_kernel");
        result.Should().NotContain("test-kernel.v2");
    }

    [Fact(DisplayName = "CompileToCudaC should use launch bounds for persistent kernels")]
    public void CompileToCudaC_WithPersistentMode_ShouldUseLaunchBounds()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel", RingKernelMode.Persistent);

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("__launch_bounds__(256, 2)");
    }

    [Fact(DisplayName = "CompileToCudaC should throw on null kernel definition")]
    public void CompileToCudaC_WithNullKernelDefinition_ShouldThrow()
    {
        // Arrange
        var config = CreateTestConfig("test_kernel");

        // Act
        Action act = () => _compiler.CompileToCudaC(null!, "void kernel() {}", config);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("kernelDefinition");
    }

    [Fact(DisplayName = "CompileToCudaC should throw on null config")]
    public void CompileToCudaC_WithNullConfig_ShouldThrow()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");

        // Act
        Action act = () => _compiler.CompileToCudaC(kernelDef, "void kernel() {}", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Theory(DisplayName = "CompileToCudaC should throw on null or empty source code")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CompileToCudaC_WithInvalidSourceCode_ShouldThrow(string? sourceCode)
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel");

        // Act
        Action act = () => _compiler.CompileToCudaC(kernelDef, sourceCode!, config);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "CompileToCudaC should generate atomic memory ordering for enqueue")]
    public void CompileToCudaC_ShouldGenerateAtomicMemoryOrdering()
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel");

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain("cuda::memory_order_acquire");
        result.Should().Contain("cuda::memory_order_release");
        result.Should().Contain("cuda::memory_order_relaxed");
        result.Should().Contain("compare_exchange_strong");
    }

    [Theory(DisplayName = "CompileToCudaC should respect capacity for burst processing limit")]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void CompileToCudaC_WithDifferentCapacities_ShouldSetBurstLimit(int capacity)
    {
        // Arrange
        var kernelDef = CreateTestKernelDefinition("TestKernel");
        var config = CreateTestConfig("test_kernel", RingKernelMode.EventDriven, capacity);

        // Act
        var result = _compiler.CompileToCudaC(kernelDef, "void kernel() {}", config);

        // Assert
        result.Should().Contain($"if (processed >= {capacity})");
    }

    #endregion

    #region Dispose Tests

    [Fact(DisplayName = "Dispose should not throw")]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var compiler = new CudaRingKernelCompiler(_mockLogger);

        // Act
        Action act = () => compiler.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Dispose should be idempotent")]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var compiler = new CudaRingKernelCompiler(_mockLogger);

        // Act
        compiler.Dispose();
        Action act = () => compiler.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Helper Methods

    private static KernelDefinition CreateTestKernelDefinition(string name)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = "void kernel() { }",
            EntryPoint = "kernel"
        };
    }

    private static RingKernelConfig CreateTestConfig(
        string kernelId,
        RingKernelMode mode = RingKernelMode.Persistent,
        int capacity = 1024,
        RingKernelDomain domain = RingKernelDomain.General)
    {
        return new RingKernelConfig
        {
            KernelId = kernelId,
            Mode = mode,
            Capacity = capacity,
            Domain = domain,
            InputQueueSize = 256,
            OutputQueueSize = 256,
            MessagingStrategy = MessagePassingStrategy.SharedMemory
        };
    }

    #endregion
}
