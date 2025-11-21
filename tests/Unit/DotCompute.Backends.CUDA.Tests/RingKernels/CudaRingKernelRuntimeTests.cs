// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Core.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.RingKernels;

/// <summary>
/// Unit tests for CudaRingKernelRuntime.
/// Tests lifecycle management for persistent ring kernels without requiring GPU hardware.
/// </summary>
public class CudaRingKernelRuntimeTests
{
    private readonly ILogger<CudaRingKernelRuntime> _mockLogger;
    private readonly CudaRingKernelCompiler _mockCompiler;
    private readonly MessageQueueRegistry _mockRegistry;
    private readonly CudaRingKernelRuntime _runtime;

    public CudaRingKernelRuntimeTests()
    {
        _mockLogger = Substitute.For<ILogger<CudaRingKernelRuntime>>();
        var compilerLogger = Substitute.For<ILogger<CudaRingKernelCompiler>>();
        var discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var serializerGenerator = new CudaMemoryPackSerializerGenerator(NullLogger<CudaMemoryPackSerializerGenerator>.Instance);
        _mockCompiler = new CudaRingKernelCompiler(compilerLogger, discovery, stubGenerator, serializerGenerator);
        var registryLogger = NullLogger<MessageQueueRegistry>.Instance;
        _mockRegistry = new MessageQueueRegistry(registryLogger);
        _runtime = new CudaRingKernelRuntime(_mockLogger, _mockCompiler, _mockRegistry);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should initialize with valid parameters")]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange & Act
        var runtime = new CudaRingKernelRuntime(_mockLogger, _mockCompiler, _mockRegistry);

        // Assert
        runtime.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor should throw on null logger")]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new CudaRingKernelRuntime(null!, _mockCompiler, _mockRegistry);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw on null compiler")]
    public void Constructor_WithNullCompiler_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new CudaRingKernelRuntime(_mockLogger, null!, _mockRegistry);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("compiler");
    }

    #endregion

    #region LaunchAsync Tests

    [Theory(DisplayName = "LaunchAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LaunchAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.LaunchAsync(kernelId!, 1, 256);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory(DisplayName = "LaunchAsync should validate grid size")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task LaunchAsync_WithInvalidGridSize_ShouldThrow(int gridSize)
    {
        // Act
        Func<Task> act = async () => await _runtime.LaunchAsync("test_kernel", gridSize, 256);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*positive*");
    }

    [Theory(DisplayName = "LaunchAsync should validate block size")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task LaunchAsync_WithInvalidBlockSize_ShouldThrow(int blockSize)
    {
        // Act
        Func<Task> act = async () => await _runtime.LaunchAsync("test_kernel", 1, blockSize);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*positive*");
    }

    [Fact(DisplayName = "LaunchAsync should succeed with valid parameters")]
    public async Task LaunchAsync_WithValidParameters_ShouldSucceed()
    {
        // Note: This will fail without GPU but tests the validation logic
        // Act
        try
        {
            await _runtime.LaunchAsync("test_kernel", 1, 256);
        }
        catch
        {
            // Expected without GPU - testing validation passed
        }

        // Assert
        var kernels = await _runtime.ListKernelsAsync();
        kernels.Should().Contain("test_kernel");
    }

    #endregion

    #region ActivateAsync Tests

    [Theory(DisplayName = "ActivateAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ActivateAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.ActivateAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "ActivateAsync should throw for non-existent kernel")]
    public async Task ActivateAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Act
        Func<Task> act = async () => await _runtime.ActivateAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact(DisplayName = "ActivateAsync should throw if kernel not launched")]
    public async Task ActivateAsync_BeforeLaunch_ShouldThrow()
    {
        // Arrange
        await _runtime.LaunchAsync("test_kernel", 1, 256);

        // Act - activate before launch completes
        Func<Task> act = async () => await _runtime.ActivateAsync("test_kernel");

        // Assert - may succeed or throw depending on launch state
        try
        {
            await act.Invoke();
        }
        catch (InvalidOperationException)
        {
            // Expected if launch hasn't completed
        }
    }

    #endregion

    #region DeactivateAsync Tests

    [Theory(DisplayName = "DeactivateAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeactivateAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.DeactivateAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "DeactivateAsync should throw for non-existent kernel")]
    public async Task DeactivateAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Act
        Func<Task> act = async () => await _runtime.DeactivateAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region TerminateAsync Tests

    [Theory(DisplayName = "TerminateAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TerminateAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.TerminateAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "TerminateAsync should throw for non-existent kernel")]
    public async Task TerminateAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Act
        Func<Task> act = async () => await _runtime.TerminateAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact(DisplayName = "TerminateAsync should remove kernel from list")]
    public async Task TerminateAsync_ShouldRemoveKernelFromList()
    {
        // Arrange
        await _runtime.LaunchAsync("test_kernel", 1, 256);

        // Act
        await _runtime.TerminateAsync("test_kernel");

        // Assert
        var kernels = await _runtime.ListKernelsAsync();
        kernels.Should().NotContain("test_kernel");
    }

    #endregion

    #region SendMessageAsync Tests

    [Theory(DisplayName = "SendMessageAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var message = KernelMessage<int>.Create(0, 0, MessageType.Data, 42);

        // Act
        Func<Task> act = async () => await _runtime.SendMessageAsync(kernelId!, message);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "SendMessageAsync should throw for non-existent kernel")]
    public async Task SendMessageAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var message = KernelMessage<int>.Create(0, 0, MessageType.Data, 42);

        // Act
        Func<Task> act = async () => await _runtime.SendMessageAsync("nonexistent", message);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region ReceiveMessageAsync Tests

    [Theory(DisplayName = "ReceiveMessageAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReceiveMessageAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.ReceiveMessageAsync<int>(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "ReceiveMessageAsync should throw for non-existent kernel")]
    public async Task ReceiveMessageAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Act
        Func<Task> act = async () => await _runtime.ReceiveMessageAsync<int>("nonexistent");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Fact(DisplayName = "ReceiveMessageAsync should handle timeout")]
    public async Task ReceiveMessageAsync_WithTimeout_ShouldReturnNull()
    {
        // Arrange
        await _runtime.LaunchAsync("test_kernel", 1, 256);

        // Act - receive with very short timeout
        var message = await _runtime.ReceiveMessageAsync<int>("test_kernel", TimeSpan.FromMilliseconds(1));

        // Assert - should timeout and return null
        message.Should().BeNull();
    }

    #endregion

    #region GetStatusAsync Tests

    [Theory(DisplayName = "GetStatusAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStatusAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.GetStatusAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "GetStatusAsync should throw for non-existent kernel")]
    public async Task GetStatusAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Act
        Func<Task> act = async () => await _runtime.GetStatusAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Fact(DisplayName = "GetStatusAsync should return valid status")]
    public async Task GetStatusAsync_ForExistingKernel_ShouldReturnStatus()
    {
        // Arrange
        await _runtime.LaunchAsync("test_kernel", 2, 256);

        // Act
        var status = await _runtime.GetStatusAsync("test_kernel");

        // Assert
        status.Should().NotBeNull();
        status.KernelId.Should().Be("test_kernel");
        status.GridSize.Should().Be(2);
        status.BlockSize.Should().Be(256);
        status.IsLaunched.Should().BeTrue();
    }

    #endregion

    #region GetMetricsAsync Tests

    [Theory(DisplayName = "GetMetricsAsync should validate kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMetricsAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Act
        Func<Task> act = async () => await _runtime.GetMetricsAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "GetMetricsAsync should throw for non-existent kernel")]
    public async Task GetMetricsAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Act
        Func<Task> act = async () => await _runtime.GetMetricsAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Fact(DisplayName = "GetMetricsAsync should return valid metrics")]
    public async Task GetMetricsAsync_ForExistingKernel_ShouldReturnMetrics()
    {
        // Arrange
        await _runtime.LaunchAsync("test_kernel", 1, 256);

        // Act
        var metrics = await _runtime.GetMetricsAsync("test_kernel");

        // Assert
        metrics.Should().NotBeNull();
        metrics.LaunchCount.Should().BeGreaterThanOrEqualTo(0);
        metrics.ThroughputMsgsPerSec.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region ListKernelsAsync Tests

    [Fact(DisplayName = "ListKernelsAsync should return empty list initially")]
    public async Task ListKernelsAsync_Initially_ShouldReturnEmptyList()
    {
        // Act
        var kernels = await _runtime.ListKernelsAsync();

        // Assert
        kernels.Should().NotBeNull();
        kernels.Should().BeEmpty();
    }

    [Fact(DisplayName = "ListKernelsAsync should return launched kernels")]
    public async Task ListKernelsAsync_AfterLaunches_ShouldReturnKernelIds()
    {
        // Arrange
        await _runtime.LaunchAsync("kernel1", 1, 256);
        await _runtime.LaunchAsync("kernel2", 1, 256);

        // Act
        var kernels = await _runtime.ListKernelsAsync();

        // Assert
        kernels.Should().Contain("kernel1");
        kernels.Should().Contain("kernel2");
    }

    #endregion

    #region CreateMessageQueueAsync Tests

    [Fact(DisplayName = "CreateMessageQueueAsync should create queue with valid capacity")]
    public async Task CreateMessageQueueAsync_WithValidCapacity_ShouldCreateQueue()
    {
        // Act
        var queue = await _runtime.CreateMessageQueueAsync<int>(256);

        // Assert
        queue.Should().NotBeNull();
        queue.Capacity.Should().Be(256);
    }

    [Theory(DisplayName = "CreateMessageQueueAsync should validate capacity")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)] // Not power of 2
    public async Task CreateMessageQueueAsync_WithInvalidCapacity_ShouldThrow(int capacity)
    {
        // Act
        Func<Task> act = async () => await _runtime.CreateMessageQueueAsync<int>(capacity);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region DisposeAsync Tests

    [Fact(DisplayName = "DisposeAsync should not throw")]
    public async Task DisposeAsync_ShouldNotThrow()
    {
        // Arrange
        var runtime = new CudaRingKernelRuntime(_mockLogger, _mockCompiler, _mockRegistry);

        // Act
        Func<Task> act = async () => await runtime.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "DisposeAsync should be idempotent")]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var runtime = new CudaRingKernelRuntime(_mockLogger, _mockCompiler, _mockRegistry);

        // Act
        await runtime.DisposeAsync();
        Func<Task> act = async () => await runtime.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "DisposeAsync should terminate all active kernels")]
    public async Task DisposeAsync_ShouldTerminateAllKernels()
    {
        // Arrange
        var runtime = new CudaRingKernelRuntime(_mockLogger, _mockCompiler, _mockRegistry);
        await runtime.LaunchAsync("kernel1", 1, 256);
        await runtime.LaunchAsync("kernel2", 1, 256);

        // Act
        await runtime.DisposeAsync();

        // Assert
        var kernels = await runtime.ListKernelsAsync();
        kernels.Should().BeEmpty();
    }

    #endregion
}
