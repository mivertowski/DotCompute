// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for the ComputeEngine class.
/// </summary>
public sealed class ComputeEngineTests : IDisposable
{
    private readonly Mock<ILogger<ComputeEngine>> _loggerMock;
    private readonly Mock<IAcceleratorManager> _acceleratorManagerMock;
    private readonly Mock<IMemoryManager> _memoryManagerMock;
    private readonly ComputeEngine _engine;
    private bool _disposed;

    public ComputeEngineTests()
    {
        _loggerMock = new Mock<ILogger<ComputeEngine>>();
        _acceleratorManagerMock = new Mock<IAcceleratorManager>();
        _memoryManagerMock = new Mock<IMemoryManager>();
        _engine = new ComputeEngine(_loggerMock.Object, _acceleratorManagerMock.Object, _memoryManagerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ComputeEngine(null!, _acceleratorManagerMock.Object, _memoryManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullAcceleratorManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ComputeEngine(_loggerMock.Object, null!, _memoryManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ComputeEngine(_loggerMock.Object, _acceleratorManagerMock.Object, null!));
    }

    [Fact]
    public async Task InitializeAsync_ShouldDiscoverAccelerators()
    {
        // Arrange
        var mockAccelerators = new List<IAccelerator>
        {
            Mock.Of<IAccelerator>(),
            Mock.Of<IAccelerator>()
        };
        _acceleratorManagerMock
            .Setup(m => m.DiscoverAcceleratorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAccelerators);

        // Act
        await _engine.InitializeAsync();

        // Assert
        _acceleratorManagerMock.Verify(m => m.DiscoverAcceleratorsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAcceleratorsAsync_ReturnsDiscoveredAccelerators()
    {
        // Arrange
        var mockAccelerators = new List<IAccelerator>
        {
            Mock.Of<IAccelerator>(a => a.Info.Name == "GPU 1"),
            Mock.Of<IAccelerator>(a => a.Info.Name == "GPU 2")
        };
        _acceleratorManagerMock
            .Setup(m => m.GetAcceleratorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAccelerators);

        // Act
        var accelerators = await _engine.GetAcceleratorsAsync();

        // Assert
        Assert.Equal(2, accelerators.Count());
        _acceleratorManagerMock.Verify(m => m.GetAcceleratorsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDefaultAcceleratorAsync_ReturnsDefaultAccelerator()
    {
        // Arrange
        var defaultAccelerator = Mock.Of<IAccelerator>(a => a.Info.Name == "Default GPU");
        _acceleratorManagerMock
            .Setup(m => m.GetDefaultAcceleratorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultAccelerator);

        // Act
        var result = await _engine.GetDefaultAcceleratorAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Default GPU", result.Info.Name);
        _acceleratorManagerMock.Verify(m => m.GetDefaultAcceleratorAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateKernelAsync_WithValidDefinition_CreatesKernel()
    {
        // Arrange
        var accelerator = Mock.Of<IAccelerator>();
        var kernelDefinition = new KernelDefinition
        {
            Name = "TestKernel",
            Code = "test code"
        };
        var compiledKernel = Mock.Of<ICompiledKernel>();
        
        Mock.Get(accelerator)
            .Setup(a => a.CompileKernelAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledKernel);

        // Act
        var result = await _engine.CreateKernelAsync(accelerator, kernelDefinition);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(compiledKernel, result);
    }

    [Fact]
    public async Task CreateKernelAsync_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition { Name = "Test", Code = "code" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _engine.CreateKernelAsync(null!, kernelDefinition));
    }

    [Fact]
    public async Task CreateKernelAsync_WithNullDefinition_ThrowsArgumentNullException()
    {
        // Arrange
        var accelerator = Mock.Of<IAccelerator>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _engine.CreateKernelAsync(accelerator, null!));
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithValidParameters_ExecutesSuccessfully()
    {
        // Arrange
        var kernel = Mock.Of<ICompiledKernel>();
        var arguments = new KernelArguments();
        
        Mock.Get(kernel)
            .Setup(k => k.ExecuteAsync(It.IsAny<KernelArguments>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _engine.ExecuteKernelAsync(kernel, arguments);

        // Assert
        Mock.Get(kernel).Verify(k => k.ExecuteAsync(arguments, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AllocateMemoryAsync_DelegatesToMemoryManager()
    {
        // Arrange
        var accelerator = Mock.Of<IAccelerator>();
        var mockBuffer = Mock.Of<IMemoryBuffer>();
        const long size = 1024;
        
        _memoryManagerMock
            .Setup(m => m.AllocateAsync(accelerator, size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBuffer);

        // Act
        var result = await _engine.AllocateMemoryAsync(accelerator, size);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(mockBuffer, result);
        _memoryManagerMock.Verify(m => m.AllocateAsync(accelerator, size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferMemoryAsync_DelegatesToMemoryManager()
    {
        // Arrange
        var sourceBuffer = Mock.Of<IMemoryBuffer>();
        var destBuffer = Mock.Of<IMemoryBuffer>();
        
        _memoryManagerMock
            .Setup(m => m.TransferAsync(sourceBuffer, destBuffer, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _engine.TransferMemoryAsync(sourceBuffer, destBuffer);

        // Assert
        _memoryManagerMock.Verify(m => m.TransferAsync(sourceBuffer, destBuffer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllResources()
    {
        // Arrange
        _acceleratorManagerMock
            .Setup(m => m.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
        
        _memoryManagerMock
            .Setup(m => m.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Act
        await _engine.DisposeAsync();

        // Assert
        _acceleratorManagerMock.Verify(m => m.DisposeAsync(), Times.Once);
        _memoryManagerMock.Verify(m => m.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesEngine()
    {
        // Act
        _engine.Dispose();

        // Assert - no exceptions thrown
        Assert.True(true);
    }

    [Fact]
    public async Task GetPerformanceMetricsAsync_ReturnsMetrics()
    {
        // Arrange
        var expectedMetrics = new PerformanceMetrics
        {
            KernelExecutions = 100,
            MemoryAllocations = 50,
            TotalMemoryAllocated = 1024 * 1024
        };
        
        _engine.UpdateMetrics(expectedMetrics);

        // Act
        var metrics = await _engine.GetPerformanceMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(expectedMetrics.KernelExecutions, metrics.KernelExecutions);
        Assert.Equal(expectedMetrics.MemoryAllocations, metrics.MemoryAllocations);
        Assert.Equal(expectedMetrics.TotalMemoryAllocated, metrics.TotalMemoryAllocated);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _engine?.Dispose();
            _disposed = true;
        }
    }
}