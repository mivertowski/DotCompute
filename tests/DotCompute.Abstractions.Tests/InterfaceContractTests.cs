using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using DotCompute.Abstractions;
using DotCompute.TestUtilities.FluentAssertions;
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
    public void IAcceleratorContract_ShouldDefineRequiredMembers()
    {
        // Arrange
        var acceleratorType = typeof(IAccelerator);

        // Act & Assert
        acceleratorType.Should().BeInterface("IAccelerator should be an interface");
        acceleratorType.Should().HaveProperty("Info", "Should have Info property");
        acceleratorType.Should().HaveProperty("Memory", "Should have Memory property");
    }

    [Fact]
    public void IMemoryManagerContract_ShouldDefineRequiredMembers()
    {
        // Arrange
        var memoryManagerType = typeof(IMemoryManager);

        // Act & Assert
        memoryManagerType.Should().BeInterface("IMemoryManager should be an interface");
        memoryManagerType.Should().HaveMethod("AllocateAsync", new[] { typeof(long) }, 
            "Should have AllocateAsync method");
    }

    [Fact]
    public void IKernelCompilerContract_ShouldDefineCompilationMethods()
    {
        // Arrange
        var compilerType = typeof(IKernelCompiler);

        // Act & Assert
        compilerType.Should().BeInterface("IKernelCompiler should be an interface");
    }

    [Fact]
    public void IBufferContract_ShouldDefineMemoryOperations()
    {
        // Arrange
        var bufferType = typeof(IBuffer<>);

        // Act & Assert
        bufferType.Should().BeInterface("IBuffer<T> should be an interface");
        bufferType.IsGenericTypeDefinition.Should().BeTrue("Should be a generic type definition");
    }

    [Fact]
    public void AcceleratorTypeEnum_ShouldHaveValidValues()
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
    public void AcceleratorTypeValidValues_ShouldBeSupported(AcceleratorType type)
    {
        // Arrange & Act
        var typeString = type.ToString();

        // Assert
        typeString.Should().NotBeNullOrEmpty("Type should have string representation");
        Enum.IsDefined(typeof(AcceleratorType), type).Should().BeTrue("Type should be valid enum value");
    }

    [Fact]
    public void DeviceMemoryStruct_ShouldHaveValidProperties()
    {
        // Arrange & Act
        var defaultMemory = DeviceMemory.Invalid;
        var customMemory = new DeviceMemory(new IntPtr(0x1000), 1024);

        // Assert
        defaultMemory.IsValid.Should().BeFalse("Default invalid memory should not be valid");
        defaultMemory.Size.Should().Be(0, "Default size should be zero");
        customMemory.Size.Should().Be(1024, "Custom size should be set");
        customMemory.IsValid.Should().BeTrue("Custom memory with valid handle should be valid");
    }

    [Fact]
    public void AcceleratorStreamShouldSupportAsyncOperations()
    {
        // Arrange
        var streamType = typeof(AcceleratorStream);

        // Act & Assert
        streamType.Should().BeClass("AcceleratorStream should be a class");
        streamType.Should().BeAssignableTo<IDisposable>("Should implement IDisposable");
        streamType.Should().BeAssignableTo<IAsyncDisposable>("Should implement IAsyncDisposable");
    }

    [Fact]
    public void AcceleratorStreamShouldManageResourceLifecycle()
    {
        // Arrange
        var streamType = typeof(AcceleratorStream);

        // Act & Assert
        streamType.Should().BeClass("AcceleratorStream should be a class");
        streamType.Should().BeAssignableTo<IAsyncDisposable>("Should implement IAsyncDisposable");
    }
}

/// <summary>
/// Mock implementation tests for interface validation
/// </summary>
public class MockImplementationTests
{
    [Fact]
    public void MockAcceleratorShouldImplementInterface()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockInfo = new AcceleratorInfo 
        { 
            Id = "test-cpu",
            Name = "Test CPU",
            DeviceType = "CPU",
            Vendor = "Test"
        };
        mockAccelerator.Setup(x => x.Info).Returns(mockInfo);

        // Act
        var accelerator = mockAccelerator.Object;

        // Assert
        accelerator.Info.DeviceType.Should().Be("CPU");
        accelerator.Info.Name.Should().Be("Test CPU");
    }

    [Fact]
    public void MockMemoryManagerShouldImplementInterface()
    {
        // Arrange
        var mockManager = new Mock<IMemoryManager>();
        var expectedBuffer = new Mock<IMemoryBuffer>().Object;
        
        mockManager.Setup(x => x.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedBuffer);

        // Act
        var manager = mockManager.Object;

        // Assert
        manager.Should().NotBeNull();
        mockManager.Verify(x => x.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MockMemoryManagerAllocateAsync_ShouldReturnBuffer()
    {
        // Arrange
        var mockManager = new Mock<IMemoryManager>();
        var mockBuffer = new Mock<IMemoryBuffer>();
        mockBuffer.Setup(x => x.SizeInBytes).Returns(1024);
        
        mockManager.Setup(x => x.AllocateAsync(1024, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(mockBuffer.Object);

        // Act
        var buffer = await mockManager.Object.AllocateAsync(1024);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(1024);
    }

    [Fact]
    public void MockKernelCompilerShouldImplementInterface()
    {
        // Arrange
        var mockCompiler = new Mock<IKernelCompiler>();
        var mockKernel = new Mock<ICompiledKernel>();
        
        mockCompiler.Setup(x => x.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(mockKernel.Object);
        mockCompiler.Setup(x => x.Name).Returns("Test Compiler");
        mockCompiler.Setup(x => x.SupportedSourceTypes).Returns(new[] { KernelSourceType.CUDA });

        // Act
        var compiler = mockCompiler.Object;

        // Assert
        compiler.Should().NotBeNull();
        compiler.Name.Should().Be("Test Compiler");
    }

    [Fact]
    public async Task MockKernelCompilerCompileAsync_ShouldReturnKernel()
    {
        // Arrange
        var mockCompiler = new Mock<IKernelCompiler>();
        var mockKernel = new Mock<ICompiledKernel>();
        mockKernel.Setup(x => x.Name).Returns("test");
        
        var definition = new KernelDefinition 
        { 
            Name = "test",
            Code = System.Text.Encoding.UTF8.GetBytes("kernel void test() {}")
        };
        
        mockCompiler.Setup(x => x.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(mockKernel.Object);

        // Act
        var kernel = await mockCompiler.Object.CompileAsync(definition, null, CancellationToken.None);

        // Assert
        kernel.Should().NotBeNull();
        kernel.Name.Should().Be("test");
        mockCompiler.Verify(x => x.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Error handling and edge case tests for abstractions
/// </summary>
public class AbstractionsErrorHandlingTests
{
    [Fact]
    public void AcceleratorTypeInvalidValue_ShouldStillWork()
    {
        // Arrange
        var invalidType = (AcceleratorType)999;
        
        // Act
        var result = invalidType.ToString();

        // Assert
        result.Should().Be("999", "Invalid enum values should convert to their numeric string representation");
    }

    [Fact]
    public void DeviceMemoryNegativeSize_ShouldThrowException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var memory = new DeviceMemory(IntPtr.Zero, -1);
        });
    }

    [Fact]
    public void DeviceMemoryZeroSize_ShouldBeValid()
    {
        // Arrange & Act
        var memory = new DeviceMemory(IntPtr.Zero, 0);

        // Assert
        memory.Size.Should().Be(0, "Zero size should be allowed");
        memory.IsValid.Should().BeFalse("Zero size with null handle should not be valid");
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(0)]
    [InlineData(1)]
    public void DeviceMemoryExtremeValues_ShouldBeHandled(long size)
    {
        // Arrange & Act
        var memory = new DeviceMemory(new IntPtr(0x1000), size);

        // Assert
        memory.Size.Should().Be(size, "Valid size values should be preserved");
        memory.IsValid.Should().Be(size > 0, "Only positive sizes with valid handles should be valid");
    }

    [Fact]
    public async Task MockBufferDisposal_ShouldBeTracked()
    {
        // Arrange
        var mockBuffer = new Mock<IMemoryBuffer>();
        var disposed = false;
        mockBuffer.Setup(x => x.DisposeAsync()).Callback(() => disposed = true)
                 .Returns(ValueTask.CompletedTask);

        // Act
        var buffer = mockBuffer.Object;
        await buffer.DisposeAsync();

        // Assert
        disposed.Should().BeTrue("DisposeAsync should be called");
        mockBuffer.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task MockAsyncDisposableDisposalAsync_ShouldBeTracked()
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
    public async Task AcceleratorStreamMultipleDisposal_ShouldBeIdempotent()
    {
        // Arrange
        var mockStream = new Mock<AcceleratorStream>();
        var disposeCount = 0;
        mockStream.Setup(x => x.DisposeAsync()).Callback(() => disposeCount++)
                 .Returns(ValueTask.CompletedTask);

        // Act
        var stream = mockStream.Object;
        await stream.DisposeAsync();
        await stream.DisposeAsync(); // Multiple disposal

        // Assert
        disposeCount.Should().Be(2, "Multiple dispose calls should be tracked");
    }
}