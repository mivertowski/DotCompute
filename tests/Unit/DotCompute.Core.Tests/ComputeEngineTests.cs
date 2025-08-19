// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Tests for the IComputeEngine interface implementations.
/// Note: Since ComputeEngine is an interface, this tests a mock implementation.
/// </summary>
public sealed class ComputeEngineTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IComputeEngine> _computeEngineMock;
    private bool _disposed;

    public ComputeEngineTests()
    {
        _loggerMock = new Mock<ILogger>();
        _computeEngineMock = new Mock<IComputeEngine>();

        // Setup default behavior for the compute engine
        _computeEngineMock.Setup(e => e.AvailableBackends)
            .Returns([ComputeBackendType.CPU, ComputeBackendType.CUDA]);
        _computeEngineMock.Setup(e => e.DefaultBackend)
            .Returns(ComputeBackendType.CPU);
    }

    [Fact]
    public void ComputeEngine_AvailableBackends_ReturnsConfiguredBackends()
    {
        // Act
        var backends = _computeEngineMock.Object.AvailableBackends;

        // Assert
        Assert.NotNull(backends);
        Assert.Contains(ComputeBackendType.CPU, backends);
        Assert.Contains(ComputeBackendType.CUDA, backends);
    }

    [Fact]
    public void ComputeEngine_DefaultBackend_ReturnsExpectedDefault()
    {
        // Act
        var defaultBackend = _computeEngineMock.Object.DefaultBackend;

        // Assert
        Assert.Equal(ComputeBackendType.CPU, defaultBackend);
    }

    [Fact]
    public async Task CompileKernelAsync_WithValidSource_ReturnsCompiledKernel()
    {
        // Arrange
        var kernelSource = "__global__ void testKernel() { }";
        var compiledKernel = Mock.Of<ICompiledKernel>();

        _computeEngineMock
            .Setup(e => e.CompileKernelAsync(kernelSource, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledKernel);

        // Act
        var result = await _computeEngineMock.Object.CompileKernelAsync(kernelSource);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(compiledKernel, result);
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullSource_ThrowsArgumentException()
    {
        // Arrange
        _computeEngineMock
            .Setup(e => e.CompileKernelAsync(null!, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentNullException());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _computeEngineMock.Object.CompileKernelAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidKernel_ExecutesSuccessfully()
    {
        // Arrange
        var kernel = Mock.Of<ICompiledKernel>();
        var arguments = new object[] { 1, 2, 3 };
        var backendType = ComputeBackendType.CPU;

        _computeEngineMock
            .Setup(e => e.ExecuteAsync(kernel, arguments, backendType, null, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act & Assert(should not throw)
        await _computeEngineMock.Object.ExecuteAsync(kernel, arguments, backendType);

        _computeEngineMock.Verify(e => e.ExecuteAsync(kernel, arguments, backendType, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullKernel_ThrowsArgumentException()
    {
        // Arrange
        var arguments = new object[] { 1, 2, 3 };
        var backendType = ComputeBackendType.CPU;

        _computeEngineMock
            .Setup(e => e.ExecuteAsync(null!, arguments, backendType, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentNullException());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _computeEngineMock.Object.ExecuteAsync(null!, arguments, backendType));
    }

    [Fact]
    public void ExecutionOptions_DefaultValues_AreCorrect()
    {
        // Act
        var options = ExecutionOptions.Default;

        // Assert
        Assert.NotNull(options);
        Assert.Equal(ExecutionPriority.Normal, options.Priority);
        Assert.False(options.EnableProfiling);
        Assert.Null(options.Timeout);
    }

    [Fact]
    public void ExecutionOptions_CustomValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new ExecutionOptions
        {
            Priority = ExecutionPriority.High,
            EnableProfiling = true,
            Timeout = TimeSpan.FromSeconds(30),
            GlobalWorkSize = [1024, 1024],
            LocalWorkSize = [16, 16]
        };

        // Assert
        Assert.Equal(ExecutionPriority.High, options.Priority);
        Assert.True(options.EnableProfiling);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Equal([1024, 1024], options.GlobalWorkSize);
        Assert.Equal([16, 16], options.LocalWorkSize);
    }

    [Fact]
    public void ComputeBackendType_EnumValues_AreCorrect()
    {
        // Arrange & Act
        var backendTypes = Enum.GetValues<ComputeBackendType>();

        // Assert
        Assert.Contains(ComputeBackendType.CPU, backendTypes);
        Assert.Contains(ComputeBackendType.CUDA, backendTypes);
        Assert.Contains(ComputeBackendType.OpenCL, backendTypes);
        Assert.Contains(ComputeBackendType.Metal, backendTypes);
        Assert.Contains(ComputeBackendType.Vulkan, backendTypes);
        Assert.Contains(ComputeBackendType.DirectCompute, backendTypes);
    }

    [Fact]
    public void ExecutionPriority_EnumValues_AreCorrect()
    {
        // Arrange & Act
        var priorities = Enum.GetValues<ExecutionPriority>();

        // Assert
        Assert.Contains(ExecutionPriority.Low, priorities);
        Assert.Contains(ExecutionPriority.Normal, priorities);
        Assert.Contains(ExecutionPriority.High, priorities);
        Assert.Contains(ExecutionPriority.Critical, priorities);
    }

    [Fact]
    public async Task DisposeAsync_WithComputeEngine_DisposesCorrectly()
    {
        // Arrange
        _computeEngineMock
            .Setup(e => e.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Act & Assert(should not throw)
        await _computeEngineMock.Object.DisposeAsync();

        _computeEngineMock.Verify(e => e.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task CompileKernelAsync_WithCompilationOptions_PassesOptions()
    {
        // Arrange
        var kernelSource = "test kernel";
        var entryPoint = "main";
        var options = new CompilationOptions();
        var compiledKernel = Mock.Of<ICompiledKernel>();

        _computeEngineMock
            .Setup(e => e.CompileKernelAsync(kernelSource, entryPoint, options, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledKernel);

        // Act
        var result = await _computeEngineMock.Object.CompileKernelAsync(kernelSource, entryPoint, options);

        // Assert
        Assert.NotNull(result);
        _computeEngineMock.Verify(e => e.CompileKernelAsync(kernelSource, entryPoint, options, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithExecutionOptions_PassesOptions()
    {
        // Arrange
        var kernel = Mock.Of<ICompiledKernel>();
        var arguments = new object[] { 1, 2 };
        var backendType = ComputeBackendType.CUDA;
        var options = new ExecutionOptions { Priority = ExecutionPriority.High };

        _computeEngineMock
            .Setup(e => e.ExecuteAsync(kernel, arguments, backendType, options, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _computeEngineMock.Object.ExecuteAsync(kernel, arguments, backendType, options);

        // Assert
        _computeEngineMock.Verify(e => e.ExecuteAsync(kernel, arguments, backendType, options, It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // No resources to dispose in this test class
            _disposed = true;
            GC.SuppressFinalize(this);
            GC.SuppressFinalize(this);
        }
    }
}
}
