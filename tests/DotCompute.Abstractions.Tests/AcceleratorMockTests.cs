using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Abstractions.Tests;

public class AcceleratorMockTests
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
        var kernelDef = new KernelDefinition("test", kernelSource, new CompilationOptions());

        accelerator.Info.Returns(info);
        accelerator.Memory.Returns(memoryManager);
        accelerator.CompileKernelAsync(kernelDef, Arg.Any<CompilationOptions?>(), default)
            .Returns(ValueTask.FromResult(compiledKernel));

        // Act
        var actualInfo = accelerator.Info;
        var memory = accelerator.Memory;
        var compiled = await accelerator.CompileKernelAsync(kernelDef);

        // Assert
        actualInfo.Should().BeSameAs(info);
        memory.Should().BeSameAs(memoryManager);
        compiled.Should().BeSameAs(compiledKernel);
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

public class BufferMockTests
{
    [Fact]
    public void IBuffer_Interface_ShouldWorkCorrectly()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        var accelerator = Substitute.For<IAccelerator>();
        buffer.Accelerator.Returns(accelerator);
        buffer.SizeInBytes.Returns(4096);

        // Act
        var acc = buffer.Accelerator;
        var size = buffer.SizeInBytes;

        // Assert
        acc.Should().BeSameAs(accelerator);
        size.Should().Be(4096);
    }

    [Fact]
    public async Task IBuffer_CopyToAsync_WithBuffer_ShouldBeCallable()
    {
        // Arrange
        var source = Substitute.For<IBuffer<byte>>();
        var destination = Substitute.For<IBuffer<byte>>();
        source.CopyToAsync(destination, default).Returns(ValueTask.CompletedTask);

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
        source.CopyToAsync(0, destination, 0, 1024, default).Returns(ValueTask.CompletedTask);

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
        buffer.FillAsync(42, default).Returns(ValueTask.CompletedTask);

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
        buffer.Slice(100, 200).Returns(sliced);

        // Act
        var result = buffer.Slice(100, 200);

        // Assert
        result.Should().BeSameAs(sliced);
    }

    [Fact]
    public void IBuffer_AsType_ShouldReturnNewBuffer()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<byte>>();
        var newBuffer = Substitute.For<IBuffer<int>>();
        buffer.AsType<int>().Returns(newBuffer);

        // Act
        var result = buffer.AsType<int>();

        // Assert
        result.Should().BeSameAs(newBuffer);
    }

    [Fact]
    public void IBuffer_Map_ShouldReturnMappedMemory()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<double>>();
        // MappedMemory constructor is internal, so we can't create it directly
        // Just verify the method is callable
        buffer.Map(MapMode.ReadWrite).Returns(default(MappedMemory<double>));

        // Act
        _ = buffer.Map(MapMode.ReadWrite);

        // Assert
        // Can't test MappedMemory properties since constructor is internal
        // Just verify the method was called
        buffer.Received(1).Map(MapMode.ReadWrite);
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

public class CompiledKernelMockTests
{
    [Fact]
    public void ICompiledKernel_Interface_ShouldWorkCorrectly()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        kernel.Name.Returns("TestKernel");

        // Act
        var name = kernel.Name;

        // Assert
        name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task ICompiledKernel_ExecuteAsync_ShouldBeCallable()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        var args = KernelArguments.Create(2);
        kernel.ExecuteAsync(args, default).Returns(ValueTask.CompletedTask);

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

public class AcceleratorProviderMockTests
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
        accelerator1.Info.Returns(info1);
        accelerator2.Info.Returns(info2);

        provider.Name.Returns("TestProvider");
        provider.SupportedTypes.Returns(new[] { AcceleratorType.CUDA, AcceleratorType.CPU });
        provider.DiscoverAsync(default).Returns(ValueTask.FromResult<IEnumerable<IAccelerator>>(
            new[] { accelerator1, accelerator2 }));
        provider.CreateAsync(info1, default).Returns(ValueTask.FromResult(accelerator1));

        // Act
        var name = provider.Name;
        var types = provider.SupportedTypes;
        var discovered = await provider.DiscoverAsync();
        var created = await provider.CreateAsync(info1);

        // Assert
        name.Should().Be("TestProvider");
        types.Should().BeEquivalentTo(new[] { AcceleratorType.CUDA, AcceleratorType.CPU });
        discovered.Should().HaveCount(2);
        discovered.First().Should().BeSameAs(accelerator1);
        created.Should().BeSameAs(accelerator1);
    }
}

public class MemoryManagerMockTests
{
    [Fact]
    public async Task IMemoryManager_AllocateAsync_ShouldReturnBuffer()
    {
        // Arrange
        var memoryManager = Substitute.For<IMemoryManager>();
        var buffer = Substitute.For<IMemoryBuffer>();
        buffer.SizeInBytes.Returns(1024);
        memoryManager.AllocateAsync(1024, MemoryOptions.None, default)
            .Returns(ValueTask.FromResult(buffer));

        // Act
        var result = await memoryManager.AllocateAsync(1024);

        // Assert
        result.Should().BeSameAs(buffer);
        result.SizeInBytes.Should().Be(1024);
    }

    [Fact]
    public async Task IMemoryManager_AllocateAndCopyAsync_ShouldReturnBuffer()
    {
        // Arrange
        var memoryManager = Substitute.For<IMemoryManager>();
        var buffer = Substitute.For<IMemoryBuffer>();
        var data = new int[] { 1, 2, 3, 4, 5 };
        memoryManager.AllocateAndCopyAsync<int>(new ReadOnlyMemory<int>(data), MemoryOptions.None, default)
            .Returns(ValueTask.FromResult(buffer));

        // Act
        var result = await memoryManager.AllocateAndCopyAsync<int>(new ReadOnlyMemory<int>(data));

        // Assert
        result.Should().BeSameAs(buffer);
    }

    [Fact]
    public void IMemoryManager_CreateView_ShouldReturnView()
    {
        // Arrange
        var memoryManager = Substitute.For<IMemoryManager>();
        var buffer = Substitute.For<IMemoryBuffer>();
        var view = Substitute.For<IMemoryBuffer>();
        memoryManager.CreateView(buffer, 100, 200).Returns(view);

        // Act
        var result = memoryManager.CreateView(buffer, 100, 200);

        // Assert
        result.Should().BeSameAs(view);
    }
}

public class MemoryBufferMockTests
{
    private static readonly int[] array = new int[] { 1, 2, 3 };

    [Fact]
    public async Task IMemoryBuffer_CopyFromHostAsync_ShouldBeCallable()
    {
        // Arrange
        var buffer = Substitute.For<IMemoryBuffer>();
        var data = new ReadOnlyMemory<int>(array);
        buffer.CopyFromHostAsync(data, 0, default).Returns(ValueTask.CompletedTask);

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
        buffer.CopyToHostAsync(data, 0, default).Returns(ValueTask.CompletedTask);

        // Act
        await buffer.CopyToHostAsync(data);

        // Assert
        await buffer.Received(1).CopyToHostAsync(data, 0, Arg.Any<CancellationToken>());
    }
}
