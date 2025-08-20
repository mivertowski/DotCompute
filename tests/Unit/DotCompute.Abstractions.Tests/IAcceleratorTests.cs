// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Moq;
using Xunit;
using FluentAssertions;

using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for the IAccelerator interface.
/// </summary>
public sealed class IAcceleratorTests
{
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<IMemoryManager> _mockMemoryManager;
    private readonly AcceleratorInfo _testAcceleratorInfo;
    private readonly AcceleratorContext _testContext;

    public IAcceleratorTests()
    {
        _mockAccelerator = new Mock<IAccelerator>();
        _mockMemoryManager = new Mock<IMemoryManager>();
        _testAcceleratorInfo = new AcceleratorInfo();
        _testContext = new AcceleratorContext(new IntPtr(0x1000), 0);
    }

    #region Property Tests

    [Fact]
    public void Info_Property_ShouldReturnAcceleratorInfo()
    {
        // Arrange
        _ = _mockAccelerator.SetupGet(a => a.Info).Returns(_testAcceleratorInfo);

        // Act
        var info = _mockAccelerator.Object.Info;

        // Assert
        Assert.NotNull(info);
        Assert.Equal(_testAcceleratorInfo, info);
        _ = info.Name.Should().Be("Test Device");
        _ = info.DeviceType.Should().Be("Test");
    }

    [Fact]
    public void Type_Property_ShouldReturnAcceleratorType()
    {
        // Arrange
        var expectedType = AcceleratorType.CUDA;
        _ = _mockAccelerator.SetupGet(a => a.Type).Returns(expectedType);

        // Act
        var type = _mockAccelerator.Object.Type;

        // Assert
        Assert.Equal(expectedType, type);
    }

    [Fact]
    public void Memory_Property_ShouldReturnMemoryManager()
    {
        // Arrange
        _ = _mockAccelerator.SetupGet(a => a.Memory).Returns(_mockMemoryManager.Object);

        // Act
        var memory = _mockAccelerator.Object.Memory;

        // Assert
        Assert.NotNull(memory);
        Assert.Equal(_mockMemoryManager.Object, memory);
    }

    [Fact]
    public void Context_Property_ShouldReturnAcceleratorContext()
    {
        // Arrange
        _ = _mockAccelerator.SetupGet(a => a.Context).Returns(_testContext);

        // Act
        var context = _mockAccelerator.Object.Context;

        // Assert
        Assert.Equal(_testContext, context);
        _ = context.IsValid.Should().BeTrue();
        _ = context.DeviceId.Should().Be(0);
    }

    #endregion

    #region CompileKernelAsync Tests

    [Fact]
    public async Task CompileKernelAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        var mockCompiledKernel = new Mock<ICompiledKernel>();
        var kernelSource = new TextKernelSource("__global__ void test() { }", "test", KernelLanguage.Cuda);
        var definition = new KernelDefinition { Name = "test", Source = kernelSource.Code };

        _ = mockCompiledKernel.SetupGet(k => k.Name).Returns("test");
        _ = _mockAccelerator.Setup(a => a.CompileKernelAsync(definition, null, CancellationToken.None))
                       .ReturnsAsync(mockCompiledKernel.Object);

        // Act
        var result = await _mockAccelerator.Object.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        _ = result.Name.Should().Be("test");
        _mockAccelerator.Verify(a => a.CompileKernelAsync(definition, null, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CompileKernelAsync_WithOptions_ShouldPassOptions()
    {
        // Arrange
        var mockCompiledKernel = new Mock<ICompiledKernel>();
        var kernelSource = new TextKernelSource("__global__ void test() { }", "test", KernelLanguage.Cuda);
        var definition = new KernelDefinition { Name = "test", Source = kernelSource.Code };
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = true
        };

        _ = mockCompiledKernel.SetupGet(k => k.Name).Returns("test");
        _ = _mockAccelerator.Setup(a => a.CompileKernelAsync(definition, options, CancellationToken.None))
                       .ReturnsAsync(mockCompiledKernel.Object);

        // Act
        var result = await _mockAccelerator.Object.CompileKernelAsync(definition, options);

        // Assert
        Assert.NotNull(result);
        _mockAccelerator.Verify(a => a.CompileKernelAsync(definition, options, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CompileKernelAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var kernelSource = new TextKernelSource("__global__ void test() { }", "test", KernelLanguage.Cuda);
        var definition = new KernelDefinition { Name = "test", Source = kernelSource.Code };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = _mockAccelerator.Setup(a => a.CompileKernelAsync(definition, null, cts.Token))
                       .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => _mockAccelerator.Object.CompileKernelAsync(definition, cancellationToken: cts.Token).AsTask());
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        _ = _mockAccelerator.Setup(a => a.CompileKernelAsync(null!, null, CancellationToken.None))
                       .ThrowsAsync(new ArgumentNullException("definition"));

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _mockAccelerator.Object.CompileKernelAsync(null!).AsTask());
    }

    [Fact]
    public async Task CompileKernelAsync_WhenCompilationFails_ShouldThrowAcceleratorException()
    {
        // Arrange
        var kernelSource = new TextKernelSource("invalid code", "test", KernelLanguage.Cuda);
        var definition = new KernelDefinition { Name = "test", Source = kernelSource.Code };

        _ = _mockAccelerator.Setup(a => a.CompileKernelAsync(definition, null, CancellationToken.None))
                       .ThrowsAsync(new AcceleratorException("Compilation failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AcceleratorException>(
            () => _mockAccelerator.Object.CompileKernelAsync(definition).AsTask());
        _ = exception.Message.Should().Be("Compilation failed");
    }

    #endregion

    #region SynchronizeAsync Tests

    [Fact]
    public async Task SynchronizeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        _ = _mockAccelerator.Setup(a => a.SynchronizeAsync(CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockAccelerator.Object.SynchronizeAsync();

        // Assert
        _mockAccelerator.Verify(a => a.SynchronizeAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SynchronizeAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = _mockAccelerator.Setup(a => a.SynchronizeAsync(cts.Token))
                       .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockAccelerator.Object.SynchronizeAsync(cts.Token).AsTask());
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDeviceError_ShouldThrowAcceleratorException()
    {
        // Arrange
        _ = _mockAccelerator.Setup(a => a.SynchronizeAsync(CancellationToken.None))
                       .ThrowsAsync(new AcceleratorException("Device synchronization failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AcceleratorException>(
           () => _mockAccelerator.Object.SynchronizeAsync().AsTask());
        _ = exception.Message.Should().Be("Device synchronization failed");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        _ = _mockAccelerator.Setup(a => a.DisposeAsync())
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockAccelerator.Object.DisposeAsync();

        // Assert
        _mockAccelerator.Verify(a => a.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCallsShouldBeIdempotent()
    {
        // Arrange
        _ = _mockAccelerator.Setup(a => a.DisposeAsync())
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockAccelerator.Object.DisposeAsync();
        await _mockAccelerator.Object.DisposeAsync();
        await _mockAccelerator.Object.DisposeAsync();

        // Assert
        _mockAccelerator.Verify(a => a.DisposeAsync(), Times.Exactly(3));
    }

    #endregion

    #region Interface Integration Tests

    [Fact]
    public void IAccelerator_ShouldInheritFromIAsyncDisposable()
    {
        // Arrange & Act
        var type = typeof(IAccelerator);

        // Assert
        _ = Assert.IsAssignableFrom<IAsyncDisposable>(type);
    }

    [Fact]
    public async Task CompleteWorkflow_ShouldExecuteAllOperationsInOrder()
    {
        // Arrange
        var mockCompiledKernel = new Mock<ICompiledKernel>();
        var kernelSource = new TextKernelSource("__global__ void test() { }", "test", KernelLanguage.Cuda);
        var definition = new KernelDefinition { Name = "test", Source = kernelSource.Code };

        _ = _mockAccelerator.SetupGet(a => a.Info).Returns(_testAcceleratorInfo);
        _ = _mockAccelerator.SetupGet(a => a.Type).Returns(AcceleratorType.CUDA);
        _ = _mockAccelerator.SetupGet(a => a.Memory).Returns(_mockMemoryManager.Object);
        _ = _mockAccelerator.SetupGet(a => a.Context).Returns(_testContext);
        _ = _mockAccelerator.Setup(a => a.CompileKernelAsync(definition, null, CancellationToken.None))
                       .ReturnsAsync(mockCompiledKernel.Object);
        _ = _mockAccelerator.Setup(a => a.SynchronizeAsync(CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);
        _ = _mockAccelerator.Setup(a => a.DisposeAsync())
                       .Returns(ValueTask.CompletedTask);

        // Act
        var accelerator = _mockAccelerator.Object;
        var info = accelerator.Info;
        var type = accelerator.Type;
        var memory = accelerator.Memory;
        var context = accelerator.Context;
        var kernel = await accelerator.CompileKernelAsync(definition);
        await accelerator.SynchronizeAsync();
        await accelerator.DisposeAsync();

        // Assert
        Assert.NotNull(info);
        Assert.Equal(AcceleratorType.CUDA, type);
        Assert.NotNull(memory);
        _ = context.IsValid.Should().BeTrue();
        Assert.NotNull(kernel);

        // Verify all methods were called
        _mockAccelerator.Verify(a => a.CompileKernelAsync(definition, null, CancellationToken.None), Times.Once);
        _mockAccelerator.Verify(a => a.SynchronizeAsync(CancellationToken.None), Times.Once);
        _mockAccelerator.Verify(a => a.DisposeAsync(), Times.Once);
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.CUDA)]
    [InlineData(AcceleratorType.ROCm)]
    [InlineData(AcceleratorType.Metal)]
    [InlineData(AcceleratorType.OpenCL)]
    public void Type_Property_ShouldSupportAllAcceleratorTypes(AcceleratorType acceleratorType)
    {
        // Arrange
        _ = _mockAccelerator.SetupGet(a => a.Type).Returns(acceleratorType);

        // Act
        var type = _mockAccelerator.Object.Type;

        // Assert
        Assert.Equal(acceleratorType, type);
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange & Act
        var type = typeof(IAccelerator);
        var infoProperty = type.GetProperty(nameof(IAccelerator.Info));
        var typeProperty = type.GetProperty(nameof(IAccelerator.Type));
        var memoryProperty = type.GetProperty(nameof(IAccelerator.Memory));
        var contextProperty = type.GetProperty(nameof(IAccelerator.Context));

        // Assert
        _ = infoProperty!.CanRead.Should().BeTrue();
        _ = infoProperty.CanWrite.Should().BeFalse();
        _ = typeProperty!.CanRead.Should().BeTrue();
        _ = typeProperty.CanWrite.Should().BeFalse();
        _ = memoryProperty!.CanRead.Should().BeTrue();
        _ = memoryProperty.CanWrite.Should().BeFalse();
        _ = contextProperty!.CanRead.Should().BeTrue();
        _ = contextProperty.CanWrite.Should().BeFalse();
    }

    #endregion
}
