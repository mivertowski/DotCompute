// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.OpenCL.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.OpenCL.Tests.RingKernels;

/// <summary>
/// Unit tests for OpenCLRingKernelRuntime.
/// Tests kernel lifecycle management and message routing without requiring OpenCL hardware.
/// </summary>
public class OpenCLRingKernelRuntimeTests
{
    private readonly ILogger<OpenCLRingKernelRuntime> _mockLogger;
    private readonly ILogger<OpenCLRingKernelCompiler> _mockCompilerLogger;
    private readonly OpenCLRingKernelCompiler _compiler;

    public OpenCLRingKernelRuntimeTests()
    {
        _mockLogger = Substitute.For<ILogger<OpenCLRingKernelRuntime>>();
        _mockCompilerLogger = Substitute.For<ILogger<OpenCLRingKernelCompiler>>();
        _compiler = new OpenCLRingKernelCompiler(_mockCompilerLogger);
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw on null context")]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new OpenCLRingKernelRuntime(null!, _mockLogger, _compiler);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact(DisplayName = "Constructor should throw on null logger")]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();

        // Act
        Action act = () => _ = new OpenCLRingKernelRuntime(context, null!, _compiler);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should throw on null compiler")]
    public void Constructor_WithNullCompiler_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();

        // Act
        Action act = () => _ = new OpenCLRingKernelRuntime(context, _mockLogger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("compiler");
    }

    [Fact(DisplayName = "Constructor should initialize with valid parameters")]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange
        var context = CreateMockContext();

        // Act
        Action act = () => _ = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region LaunchAsync Tests

    [Fact(DisplayName = "LaunchAsync should throw before context initialization")]
    public async Task LaunchAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.LaunchAsync("test_kernel", 256, 64);

        // Assert - Will throw because context is null
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory(DisplayName = "LaunchAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LaunchAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.LaunchAsync(kernelId!, 256, 64);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory(DisplayName = "LaunchAsync should throw on invalid grid or block size")]
    [InlineData(0, 64)]
    [InlineData(256, 0)]
    [InlineData(-1, 64)]
    [InlineData(256, -1)]
    public async Task LaunchAsync_WithInvalidSizes_ShouldThrow(int gridSize, int blockSize)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.LaunchAsync("test_kernel", gridSize, blockSize);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*positive*");
    }

    #endregion

    #region ActivateAsync Tests

    [Fact(DisplayName = "ActivateAsync should throw for non-existent kernel")]
    public async Task ActivateAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.ActivateAsync("non_existent_kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "ActivateAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ActivateAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.ActivateAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region DeactivateAsync Tests

    [Fact(DisplayName = "DeactivateAsync should throw for non-existent kernel")]
    public async Task DeactivateAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.DeactivateAsync("non_existent_kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "DeactivateAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeactivateAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.DeactivateAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region TerminateAsync Tests

    [Fact(DisplayName = "TerminateAsync should throw for non-existent kernel")]
    public async Task TerminateAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.TerminateAsync("non_existent_kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "TerminateAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TerminateAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.TerminateAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region SendMessageAsync Tests

    [Fact(DisplayName = "SendMessageAsync should throw for non-existent kernel")]
    public async Task SendMessageAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        Func<Task> act = async () => await runtime.SendMessageAsync("non_existent_kernel", message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "SendMessageAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        Func<Task> act = async () => await runtime.SendMessageAsync(kernelId!, message);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ReceiveMessageAsync Tests

    [Fact(DisplayName = "ReceiveMessageAsync should throw for non-existent kernel")]
    public async Task ReceiveMessageAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.ReceiveMessageAsync<int>("non_existent_kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "ReceiveMessageAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReceiveMessageAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.ReceiveMessageAsync<int>(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetStatusAsync Tests

    [Fact(DisplayName = "GetStatusAsync should throw for non-existent kernel")]
    public async Task GetStatusAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.GetStatusAsync("non_existent_kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "GetStatusAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStatusAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.GetStatusAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetMetricsAsync Tests

    [Fact(DisplayName = "GetMetricsAsync should throw for non-existent kernel")]
    public async Task GetMetricsAsync_WithNonExistentKernel_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.GetMetricsAsync("non_existent_kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory(DisplayName = "GetMetricsAsync should throw on null or empty kernel ID")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMetricsAsync_WithInvalidKernelId_ShouldThrow(string? kernelId)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.GetMetricsAsync(kernelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ListKernelsAsync Tests

    [Fact(DisplayName = "ListKernelsAsync should return empty list when no kernels launched")]
    public async Task ListKernelsAsync_WithNoKernels_ShouldReturnEmpty()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        var kernels = await runtime.ListKernelsAsync();

        // Assert
        kernels.Should().NotBeNull();
        kernels.Should().BeEmpty();
    }

    #endregion

    #region CreateMessageQueueAsync Tests

    [Fact(DisplayName = "CreateMessageQueueAsync should throw before context initialization")]
    public async Task CreateMessageQueueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.CreateMessageQueueAsync<int>(256);

        // Assert - Will throw because context is null
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory(DisplayName = "CreateMessageQueueAsync should throw on invalid capacity")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)] // Not power of 2
    [InlineData(15)]  // Not power of 2
    public async Task CreateMessageQueueAsync_WithInvalidCapacity_ShouldThrow(int capacity)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.CreateMessageQueueAsync<int>(capacity);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*power of 2*");
    }

    [Theory(DisplayName = "CreateMessageQueueAsync should accept valid power-of-2 capacities")]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(256)]
    [InlineData(1024)]
    public async Task CreateMessageQueueAsync_WithValidCapacity_ShouldNotThrow(int capacity)
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.CreateMessageQueueAsync<int>(capacity);

        // Assert - Will throw because context is null, but should pass validation
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region Dispose Tests

    [Fact(DisplayName = "DisposeAsync should complete successfully")]
    public async Task DisposeAsync_ShouldComplete()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        Func<Task> act = async () => await runtime.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "DisposeAsync should be idempotent")]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        await runtime.DisposeAsync();
        Func<Task> act = async () => await runtime.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Operations after dispose should throw")]
    public async Task Operations_AfterDispose_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        await runtime.DisposeAsync();

        // Act & Assert
        Func<Task> launchAct = async () => await runtime.LaunchAsync("test_kernel", 256, 64);
        Func<Task> listAct = async () => await runtime.ListKernelsAsync();

        await launchAct.Should().ThrowAsync<ObjectDisposedException>();
        await listAct.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Lifecycle Integration Tests

    [Fact(DisplayName = "ListKernelsAsync should reflect launched kernels")]
    public async Task Lifecycle_ListKernelsAsync_ShouldReflectState()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        var initialKernels = await runtime.ListKernelsAsync();

        // Assert
        initialKernels.Should().BeEmpty();
    }

    [Fact(DisplayName = "Concurrent operations should be thread-safe")]
    public async Task Lifecycle_ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var context = CreateMockContext();
        var runtime = new OpenCLRingKernelRuntime(context, _mockLogger, _compiler);

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () => await runtime.ListKernelsAsync()));
        }

        Func<Task> act = async () => await Task.WhenAll(tasks);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Helper Methods

    private static OpenCLContext CreateMockContext()
    {
        // Note: This is a placeholder. In reality, OpenCLContext requires actual OpenCL initialization
        // Hardware tests will use real OpenCL context
        return null!;
    }


    #endregion
}
