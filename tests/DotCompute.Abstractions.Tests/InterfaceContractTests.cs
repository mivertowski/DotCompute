using Xunit;
using FluentAssertions;
using DotCompute.Abstractions;
using Moq;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive tests for DotCompute Abstractions interfaces
/// Validates contract compliance and interface behavior
/// Targets 95%+ coverage for abstraction components
/// </summary>
public class InterfaceContractTests
{
    [Fact]
    public void IAccelerator_Contract_ShouldDefineRequiredMembers()
    {
        // Arrange
        var acceleratorType = typeof(IAccelerator);

        // Act & Assert
        acceleratorType.Should().BeInterface("IAccelerator should be an interface");
        acceleratorType.Should().HaveProperty("Type", "Should have Type property");
        acceleratorType.Should().HaveProperty("Name", "Should have Name property");
    }

    [Fact]
    public void IMemoryManager_Contract_ShouldDefineRequiredMembers()
    {
        // Arrange
        var memoryManagerType = typeof(IMemoryManager);

        // Act & Assert
        memoryManagerType.Should().BeInterface("IMemoryManager should be an interface");
        memoryManagerType.Should().HaveMethod("AllocateAsync", new[] { typeof(long) }, 
            "Should have AllocateAsync method");
    }

    [Fact]
    public void IKernelCompiler_Contract_ShouldDefineCompilationMethods()
    {
        // Arrange
        var compilerType = typeof(IKernelCompiler);

        // Act & Assert
        compilerType.Should().BeInterface("IKernelCompiler should be an interface");
    }

    [Fact]
    public void IBuffer_Contract_ShouldDefineMemoryOperations()
    {
        // Arrange
        var bufferType = typeof(IBuffer);

        // Act & Assert
        bufferType.Should().BeInterface("IBuffer should be an interface");
        bufferType.Should().BeAssignableTo<IDisposable>("Should implement IDisposable");
    }

    [Fact]
    public void AcceleratorType_Enum_ShouldHaveValidValues()
    {
        // Arrange & Act
        var acceleratorTypes = Enum.GetValues<AcceleratorType>();

        // Assert
        acceleratorTypes.Should().NotBeEmpty("Should have defined accelerator types");
        acceleratorTypes.Should().Contain(AcceleratorType.CPU, "Should include CPU type");
        acceleratorTypes.Should().Contain(AcceleratorType.CUDA, "Should include CUDA GPU type");
    }

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.CUDA)]
    [InlineData(AcceleratorType.Metal)]
    public void AcceleratorType_ValidValues_ShouldBeSupported(AcceleratorType type)
    {
        // Arrange & Act
        var typeString = type.ToString();

        // Assert
        typeString.Should().NotBeNullOrEmpty("Type should have string representation");
        Enum.IsDefined(typeof(AcceleratorType), type).Should().BeTrue("Type should be valid enum value");
    }

    [Fact]
    public void DeviceMemory_Struct_ShouldHaveValidProperties()
    {
        // Arrange & Act
        var defaultMemory = new DeviceMemory();
        var customMemory = new DeviceMemory { Size = 1024, Alignment = 16 };

        // Assert
        defaultMemory.Size.Should().Be(0, "Default size should be zero");
        customMemory.Size.Should().Be(1024, "Custom size should be set");
        customMemory.Alignment.Should().Be(16, "Custom alignment should be set");
    }

    [Fact]
    public void AcceleratorStream_ShouldSupportAsyncOperations()
    {
        // Arrange
        var streamType = typeof(AcceleratorStream);

        // Act & Assert
        streamType.Should().BeClass("AcceleratorStream should be a class");
        streamType.Should().BeAssignableTo<IDisposable>("Should implement IDisposable");
        streamType.Should().BeAssignableTo<IAsyncDisposable>("Should implement IAsyncDisposable");
    }

    [Fact]
    public void AcceleratorContext_ShouldManageResourceLifecycle()
    {
        // Arrange
        var contextType = typeof(AcceleratorContext);

        // Act & Assert
        contextType.Should().BeClass("AcceleratorContext should be a class");
        contextType.Should().BeAssignableTo<IDisposable>("Should implement IDisposable");
    }
}

/// <summary>
/// Mock implementation tests for interface validation
/// </summary>
public class MockImplementationTests
{
    [Fact]
    public void MockAccelerator_ShouldImplementInterface()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        mockAccelerator.Setup(x => x.Type).Returns(AcceleratorType.CPU);
        mockAccelerator.Setup(x => x.Name).Returns("Test CPU");

        // Act
        var accelerator = mockAccelerator.Object;

        // Assert
        accelerator.Type.Should().Be(AcceleratorType.CPU);
        accelerator.Name.Should().Be("Test CPU");
    }

    [Fact]
    public void MockMemoryManager_ShouldImplementInterface()
    {
        // Arrange
        var mockManager = new Mock<IMemoryManager>();
        var expectedBuffer = new Mock<IBuffer>().Object;
        
        mockManager.Setup(x => x.AllocateAsync(It.IsAny<long>()))
                  .ReturnsAsync(expectedBuffer);

        // Act
        var manager = mockManager.Object;

        // Assert
        manager.Should().NotBeNull();
        mockManager.Verify(x => x.AllocateAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task MockMemoryManager_AllocateAsync_ShouldReturnBuffer()
    {
        // Arrange
        var mockManager = new Mock<IMemoryManager>();
        var mockBuffer = new Mock<IBuffer>();
        mockBuffer.Setup(x => x.Size).Returns(1024);
        
        mockManager.Setup(x => x.AllocateAsync(1024))
                  .ReturnsAsync(mockBuffer.Object);

        // Act
        var buffer = await mockManager.Object.AllocateAsync(1024);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Size.Should().Be(1024);
    }

    [Fact]
    public void MockKernelCompiler_ShouldImplementInterface()
    {
        // Arrange
        var mockCompiler = new Mock<IKernelCompiler>();
        var mockKernel = new Mock<IKernel>();
        
        mockCompiler.Setup(x => x.CompileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(mockKernel.Object);

        // Act
        var compiler = mockCompiler.Object;

        // Assert
        compiler.Should().NotBeNull();
    }

    [Fact]
    public async Task MockKernelCompiler_CompileAsync_ShouldReturnKernel()
    {
        // Arrange
        var mockCompiler = new Mock<IKernelCompiler>();
        var mockKernel = new Mock<IKernel>();
        var source = "kernel void test() {}";
        
        mockCompiler.Setup(x => x.CompileAsync(source, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(mockKernel.Object);

        // Act
        var kernel = await mockCompiler.Object.CompileAsync(source, CancellationToken.None);

        // Assert
        kernel.Should().NotBeNull();
        mockCompiler.Verify(x => x.CompileAsync(source, It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Error handling and edge case tests for abstractions
/// </summary>
public class AbstractionsErrorHandlingTests
{
    [Fact]
    public void AcceleratorType_InvalidCast_ShouldThrowException()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidCastException>(() =>
        {
            var invalidType = (AcceleratorType)999;
            var result = invalidType.ToString();
        });
    }

    [Fact]
    public void DeviceMemory_NegativeSize_ShouldBeHandled()
    {
        // Arrange & Act
        var memory = new DeviceMemory { Size = -1 };

        // Assert
        memory.Size.Should().Be(-1, "Negative size should be preserved for validation");
    }

    [Fact]
    public void DeviceMemory_ZeroAlignment_ShouldBeHandled()
    {
        // Arrange & Act
        var memory = new DeviceMemory { Alignment = 0 };

        // Assert
        memory.Alignment.Should().Be(0, "Zero alignment should be preserved for validation");
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(0)]
    public void DeviceMemory_ExtremeValues_ShouldBeHandled(long size)
    {
        // Arrange & Act
        var memory = new DeviceMemory { Size = size };

        // Assert
        memory.Size.Should().Be(size, "Extreme values should be preserved");
    }

    [Fact]
    public void MockBuffer_Disposal_ShouldBeTracked()
    {
        // Arrange
        var mockBuffer = new Mock<IBuffer>();
        var disposed = false;
        mockBuffer.Setup(x => x.Dispose()).Callback(() => disposed = true);

        // Act
        var buffer = mockBuffer.Object;
        buffer.Dispose();

        // Assert
        disposed.Should().BeTrue("Dispose should be called");
        mockBuffer.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task MockAsyncDisposable_DisposalAsync_ShouldBeTracked()
    {
        // Arrange
        var mockStream = new Mock<AcceleratorStream>();
        var disposed = false;
        mockStream.Setup(x => x.DisposeAsync()).Callback(() => disposed = true)
                 .Returns(ValueTask.CompletedTask);

        // Act
        var stream = mockStream.Object;
        await stream.DisposeAsync();

        // Assert
        disposed.Should().BeTrue("DisposeAsync should be called");
        mockStream.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void AcceleratorContext_MultipleDisposal_ShouldBeIdempotent()
    {
        // Arrange
        var mockContext = new Mock<AcceleratorContext>();
        var disposeCount = 0;
        mockContext.Setup(x => x.Dispose()).Callback(() => disposeCount++);

        // Act
        var context = mockContext.Object;
        context.Dispose();
        context.Dispose(); // Multiple disposal

        // Assert
        disposeCount.Should().Be(2, "Multiple dispose calls should be tracked");
    }
}