using DotCompute.Abstractions;
using NSubstitute;
using Xunit;
using FluentAssertions;

using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute mock setup requires storing ValueTask

namespace DotCompute.Abstractions.Tests;


public sealed class AcceleratorMockTests
{
    [Fact]
    public async Task IAccelerator_Interface_ShouldWorkCorrectly()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(AcceleratorType.CUDA, "Test GPU", "1.0", 8589934592L);
        var memoryManager = Substitute.For<IMemoryManager>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        var kernelSource = new TextKernelSource("test code", "test", KernelLanguage.CSharpIL);
        var kernelDef = new KernelDefinition("test", kernelSource.Code, kernelSource.EntryPoint);

        _ = accelerator.Info.Returns(info);
        _ = accelerator.Memory.Returns(memoryManager);
        _ = accelerator.CompileKernelAsync(kernelDef, Arg.Any<CompilationOptions?>(), default)
            .Returns(ValueTask.FromResult(compiledKernel));

        // Act
        var actualInfo = accelerator.Info;
        var memory = accelerator.Memory;
        var compiled = await accelerator.CompileKernelAsync(kernelDef);

        // Assert
        _ = actualInfo.Should().BeSameAs(info);
        _ = memory.Should().BeSameAs(memoryManager);
        _ = compiled.Should().BeSameAs(compiledKernel);
    }

    [Fact]
    public async Task IAccelerator_SynchronizeAsync_ShouldBeCallable()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();

        // Act
        await accelerator.SynchronizeAsync();

        // Assert
        await accelerator.Received(1).SynchronizeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IAccelerator_DisposeAsync_ShouldBeCallable()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();

        // Act
        await accelerator.DisposeAsync();

        // Assert
        await accelerator.Received(1).DisposeAsync();
    }
}

public sealed class BufferMockTests
{
    [Fact]
    public void IBuffer_Interface_ShouldWorkCorrectly()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        var accelerator = Substitute.For<IAccelerator>();
        _ = buffer.Accelerator.Returns(accelerator);
        _ = buffer.SizeInBytes.Returns(4096);

        // Act
        var acc = buffer.Accelerator;
        var size = buffer.SizeInBytes;

        // Assert
        _ = acc.Should().BeSameAs(accelerator);
        Assert.Equal(4096, size);
    }

    [Fact]
    public async Task IBuffer_CopyToAsync_WithBuffer_ShouldBeCallable()
    {
        // Arrange
        var source = Substitute.For<IBuffer<byte>>();
        var destination = Substitute.For<IBuffer<byte>>();
        _ = source.CopyToAsync(destination, default).Returns(ValueTask.CompletedTask);

        // Act
        await source.CopyToAsync(destination);

        // Assert
        await source.Received(1).CopyToAsync(destination, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IBuffer_CopyToAsync_WithRange_ShouldBeCallable()
    {
        // Arrange
        var source = Substitute.For<IBuffer<byte>>();
        var destination = Substitute.For<IBuffer<byte>>();
        _ = source.CopyToAsync(0, destination, 0, 1024, default).Returns(ValueTask.CompletedTask);

        // Act
        await source.CopyToAsync(0, destination, 0, 1024);

        // Assert
        await source.Received(1).CopyToAsync(0, destination, 0, 1024, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IBuffer_FillAsync_ShouldBeCallable()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        _ = buffer.FillAsync(42, default).Returns(ValueTask.CompletedTask);

        // Act
        await buffer.FillAsync(42);

        // Assert
        await buffer.Received(1).FillAsync(42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IBuffer_Slice_ShouldReturnBuffer()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<float>>();
        var sliced = Substitute.For<IBuffer<float>>();
        _ = buffer.Slice(100, 200).Returns(sliced);

        // Act
        var result = buffer.Slice(100, 200);

        // Assert
        _ = result.Should().BeSameAs(sliced);
    }

    [Fact]
    public void IBuffer_AsType_ShouldReturnNewBuffer()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<byte>>();
        var newBuffer = Substitute.For<IBuffer<int>>();
        _ = buffer.AsType<int>().Returns(newBuffer);

        // Act
        var result = buffer.AsType<int>();

        // Assert
        _ = result.Should().BeSameAs(newBuffer);
    }

    [Fact]
    public void IBuffer_Map_ShouldReturnMappedMemory()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<double>>();
        // MappedMemory constructor is internal, so we can't create it directly
        // Just verify the method is callable
        _ = buffer.Map(MapMode.ReadWrite).Returns(default(MappedMemory<double>));

        // Act
        _ = buffer.Map(MapMode.ReadWrite);

        // Assert
        // Can't test MappedMemory properties since constructor is internal
        // Just verify the method was called
        _ = buffer.Received(1).Map(MapMode.ReadWrite);
    }

    [Fact]
    public async Task IBuffer_DisposeAsync_ShouldBeCallable()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();

        // Act
        await buffer.DisposeAsync();

        // Assert
        await buffer.Received(1).DisposeAsync();
    }
}

public sealed class CompiledKernelMockTests
{
    [Fact]
    public void ICompiledKernel_Interface_ShouldWorkCorrectly()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        _ = kernel.Name.Returns("TestKernel");

        // Act
        var name = kernel.Name;

        // Assert
        Assert.Equal("TestKernel", name);
    }

    [Fact]
    public async Task ICompiledKernel_ExecuteAsync_ShouldBeCallable()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        var args = KernelArguments.Create(2);
        _ = kernel.ExecuteAsync(args, default).Returns(ValueTask.CompletedTask);

        // Act
        await kernel.ExecuteAsync(args);

        // Assert
        await kernel.Received(1).ExecuteAsync(args, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ICompiledKernel_DisposeAsync_ShouldBeCallable()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();

        // Act
        await kernel.DisposeAsync();

        // Assert
        await kernel.Received(1).DisposeAsync();
    }
}

public sealed class AcceleratorProviderMockTests
{
    [Fact]
    public async Task IAcceleratorProvider_Interface_ShouldWorkCorrectly()
    {
        // Arrange
        var provider = Substitute.For<IAcceleratorProvider>();
        var info1 = new AcceleratorInfo(AcceleratorType.CUDA, "GPU 0", "1.0", 8589934592L);
        var info2 = new AcceleratorInfo(AcceleratorType.CUDA, "GPU 1", "1.0", 8589934592L);
        var accelerator1 = Substitute.For<IAccelerator>();
        var accelerator2 = Substitute.For<IAccelerator>();
        _ = accelerator1.Info.Returns(info1);
        _ = accelerator2.Info.Returns(info2);

        _ = provider.Name.Returns("TestProvider");
        _ = provider.SupportedTypes.Returns([AcceleratorType.CUDA, AcceleratorType.CPU]);
        _ = provider.DiscoverAsync(default).Returns(ValueTask.FromResult<IEnumerable<IAccelerator>>(
            new[] { accelerator1, accelerator2 }));
        _ = provider.CreateAsync(info1, default).Returns(ValueTask.FromResult(accelerator1));

        // Act
        var name = provider.Name;
        var types = provider.SupportedTypes;
        var discovered = await provider.DiscoverAsync();
        var created = await provider.CreateAsync(info1);

        // Assert
        Assert.Equal("TestProvider", name);
        _ = types.Should().BeEquivalentTo(new[] { AcceleratorType.CUDA, AcceleratorType.CPU });
        Assert.Equal(2, discovered.Count());
        _ = discovered.First().Should().BeSameAs(accelerator1);
        _ = created.Should().BeSameAs(accelerator1);
    }
}

public sealed class MemoryManagerMockTests
{
    [Fact]
    public async Task IMemoryManager_AllocateAsync_ShouldReturnBuffer()
    {
        // Arrange
        var memoryManager = Substitute.For<IMemoryManager>();
        var buffer = Substitute.For<IMemoryBuffer>();
        _ = buffer.SizeInBytes.Returns(1024);
        _ = memoryManager.AllocateAsync(1024, MemoryOptions.None, default)
            .Returns(ValueTask.FromResult(buffer));

        // Act
        var result = await memoryManager.AllocateAsync(1024);

        // Assert
        _ = result.Should().BeSameAs(buffer);
        _ = result.SizeInBytes.Should().Be(1024);
    }

    [Fact]
    public async Task IMemoryManager_AllocateAndCopyAsync_ShouldReturnBuffer()
    {
        // Arrange
        var memoryManager = Substitute.For<IMemoryManager>();
        var buffer = Substitute.For<IMemoryBuffer>();
        var data = new int[] { 1, 2, 3, 4, 5 };
        _ = memoryManager.AllocateAndCopyAsync(new ReadOnlyMemory<int>(data), MemoryOptions.None, default)
            .Returns(ValueTask.FromResult(buffer));

        // Act
        var result = await memoryManager.AllocateAndCopyAsync(new ReadOnlyMemory<int>(data));

        // Assert
        _ = result.Should().BeSameAs(buffer);
    }

    [Fact]
    public void IMemoryManager_CreateView_ShouldReturnView()
    {
        // Arrange
        var memoryManager = Substitute.For<IMemoryManager>();
        var buffer = Substitute.For<IMemoryBuffer>();
        var view = Substitute.For<IMemoryBuffer>();
        _ = memoryManager.CreateView(buffer, 100, 200).Returns(view);

        // Act
        var result = memoryManager.CreateView(buffer, 100, 200);

        // Assert
        _ = result.Should().BeSameAs(view);
    }
}

public sealed class MemoryBufferMockTests
{
    private static readonly int[] array = [1, 2, 3];

    [Fact]
    public async Task IMemoryBuffer_CopyFromHostAsync_ShouldBeCallable()
    {
        // Arrange
        var buffer = Substitute.For<IMemoryBuffer>();
        var data = new ReadOnlyMemory<int>(array);
        _ = buffer.CopyFromHostAsync(data, 0, default).Returns(ValueTask.CompletedTask);

        // Act
        await buffer.CopyFromHostAsync(data);

        // Assert
        await buffer.Received(1).CopyFromHostAsync(data, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IMemoryBuffer_CopyToHostAsync_ShouldBeCallable()
    {
        // Arrange
        var buffer = Substitute.For<IMemoryBuffer>();
        var data = new Memory<int>(new int[3]);
        _ = buffer.CopyToHostAsync(data, 0, default).Returns(ValueTask.CompletedTask);

        // Act
        await buffer.CopyToHostAsync(data);

        // Assert
        await buffer.Received(1).CopyToHostAsync(data, 0, Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012

