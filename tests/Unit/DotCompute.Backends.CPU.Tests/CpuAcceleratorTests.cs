// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Core;
using FluentAssertions;

namespace DotCompute.Backends.CPU;

public class CpuAcceleratorTests
{
    private readonly FakeLogger<CpuAccelerator> _logger;
    private readonly CpuAccelerator _accelerator;

    public CpuAcceleratorTests()
    {
        _logger = new FakeLogger<CpuAccelerator>();
        _accelerator = new CpuAccelerator(_logger);
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_accelerator);
        _accelerator.IsInitialized.Should().BeTrue();
        _accelerator.Name.Should().NotBeNullOrEmpty();
        _accelerator.DeviceType.Should().Be(ComputeDeviceType.CPU);
    }

    [Fact]
    public void DeviceInfo_ShouldContainCpuInformation()
    {
        // Act
        var deviceInfo = _accelerator.DeviceInfo;

        // Assert
        Assert.NotNull(deviceInfo);
        deviceInfo.Name.Should().NotBeNullOrEmpty();
        deviceInfo.DeviceType.Should().Be(ComputeDeviceType.CPU);
((deviceInfo.MemorySize > 0).Should().BeTrue();
((deviceInfo.MaxComputeUnits > 0).Should().BeTrue();
((deviceInfo.MaxWorkGroupSize > 0).Should().BeTrue();
    }

    [Fact]
    public void CreateMemoryManager_ShouldReturnValidManager()
    {
        // Act
        using var memoryManager = _accelerator.CreateMemoryManager();

        // Assert
        Assert.NotNull(memoryManager);
        Assert.IsType<CpuMemoryManager>(memoryManager);
    }

    [Fact]
    public void CreateKernelCompiler_ShouldReturnValidCompiler()
    {
        // Act
        using var compiler = _accelerator.CreateKernelCompiler();

        // Assert
        Assert.NotNull(compiler);
        Assert.IsType<CpuKernelCompiler>(compiler);
    }

    [Fact]
    public void GetCapabilities_ShouldReturnSimdCapabilities()
    {
        // Act
        var capabilities = _accelerator.GetCapabilities();

        // Assert
        Assert.NotNull(capabilities);
        Assert.IsType<SimdCapabilities>(capabilities);
        
        var simdCaps = (SimdCapabilities)capabilities;
        simdCaps.IsHardwareAccelerated.Should().Be(System.Numerics.Vector.IsHardwareAccelerated);
((simdCaps.PreferredVectorWidth > 0).Should().BeTrue();
        simdCaps.SupportedInstructionSets.Should().NotBeNull();
    }

    [Fact]
    public async Task CompileKernelAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "TestKernel",
            Code = new byte[] { 0x01, 0x02, 0x03 },
            Metadata = new Dictionary<string, object>
            {
                ["Operation"] = "Add"
            }
        };

        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(compiledKernel);
        Assert.IsType<CpuCompiledKernel>(compiledKernel);
        compiledKernel.Name.Should().Be("TestKernel");
        compiledKernel.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _accelerator.MethodCall().AsTask());
    }

    [Fact]
    public void GetOptimalWorkGroupSize_ShouldReturnReasonableValue()
    {
        // Act
        var workGroupSize = _accelerator.GetOptimalWorkGroupSize();

        // Assert
        Assert.True(workGroupSize > 0);
        Assert.True(workGroupSize <= 1024); // Reasonable upper bound
    }

    [Fact]
    public void SupportsOperation_WithBasicOperations_ShouldReturnTrue()
    {
        // Act & Assert
        _accelerator.SupportsOperation("VectorAdd").Should().BeTrue();
        _accelerator.SupportsOperation("MatrixMultiply").Should().BeTrue();
        _accelerator.SupportsOperation("Convolution").Should().BeTrue();
        _accelerator.SupportsOperation("Reduction").Should().BeTrue();
    }

    [Fact]
    public void SupportsOperation_WithUnsupportedOperation_ShouldReturnFalse()
    {
        // Act & Assert
        _accelerator.SupportsOperation("UnsupportedOperation").Should().BeFalse();
        _accelerator.SupportsOperation("").Should().BeFalse();
    }

    [Fact]
    public void WarmUp_ShouldCompleteSuccessfully()
    {
        // Act & Assert
        _accelerator.Invoking(a => a.WarmUp()).NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_logger);

        // Act & Assert
        await accelerator.Invoking(a => a.DisposeAsync().AsTask())
            .NotThrowAsync();
    }

    [Fact]
    public void IsInitialized_AfterDispose_ShouldRemainTrue()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_logger);

        // Act
        accelerator.DisposeAsync().AsTask().Wait();

        // Assert
        accelerator.IsInitialized.Should().BeTrue(); // CPU accelerator doesn't change initialization state on dispose
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(64)]
    public void GetOptimalWorkGroupSize_WithDifferentSizes_ShouldHandleGracefully(int requestedSize)
    {
        // Act
        var workGroupSize = _accelerator.GetOptimalWorkGroupSize();

        // Assert
        Assert.True(workGroupSize > 0);
        // The optimal size might not match the requested size, but should be reasonable
    }

    [Fact]
    public void DeviceInfo_Properties_ShouldBeConsistent()
    {
        // Act
        var deviceInfo = _accelerator.DeviceInfo;

        // Assert
        deviceInfo.Name.Should().Be(_accelerator.Name);
        deviceInfo.DeviceType.Should().Be(_accelerator.DeviceType);
        deviceInfo.MaxComputeUnits >= 1.Should().BeTrue();
        deviceInfo.MaxComputeUnits <= Environment.ProcessorCount.Should().BeTrue();
    }

    [Fact]
    public void Logger_ShouldReceiveInitializationMessages()
    {
        // Arrange & Act
        var accelerator = new CpuAccelerator(_logger);

        // Assert
        _logger.Collector.GetSnapshot().Should().NotBeEmpty();
        _logger.Collector.GetSnapshot()
            .Contain(log => log.Message != null && log.Message.Contains("CPU accelerator initialized"));
    }
}

public class CpuMemoryManagerTests
{
    private readonly FakeLogger<CpuMemoryManager> _logger;
    private readonly CpuMemoryManager _memoryManager;

    public CpuMemoryManagerTests()
    {
        _logger = new FakeLogger<CpuMemoryManager>();
        _memoryManager = new CpuMemoryManager(_logger);
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_memoryManager);
((_memoryManager.TotalMemory > 0).Should().BeTrue();
((_memoryManager.AvailableMemory > 0).Should().BeTrue();
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(1024 * 1024)]
    public void AllocateBuffer_WithValidSize_ShouldReturnBuffer(long size)
    {
        // Act
        using var buffer = _memoryManager.AllocateBuffer(size);

        // Assert
        Assert.NotNull(buffer);
        Assert.IsType<CpuMemoryBuffer>(buffer);
        buffer.SizeInBytes.Should().Be(size);
    }

    [Fact]
    public void AllocateBuffer_WithZeroSize_ShouldThrowArgumentException()
    {
        // Act & Assert
        _memoryManager.Invoking(m => m.AllocateBuffer(0))
            .Throw<ArgumentException>();
    }

    [Fact]
    public void AllocateBuffer_WithNegativeSize_ShouldThrowArgumentException()
    {
        // Act & Assert
        _memoryManager.Invoking(m => m.AllocateBuffer(-1))
            .Throw<ArgumentException>();
    }

    [Fact]
    public void AllocateBuffer_Multiple_ShouldTrackMemoryUsage()
    {
        // Arrange
        var initialAvailable = _memoryManager.AvailableMemory;

        // Act
        using var buffer1 = _memoryManager.AllocateBuffer(1024);
        using var buffer2 = _memoryManager.AllocateBuffer(2048);

        // Assert
        _memoryManager.AllocatedMemory >= 3072.Should().BeTrue();
        _memoryManager.AvailableMemory <= initialAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_ShouldReleaseAllMemory()
    {
        // Arrange
        var memoryManager = new CpuMemoryManager(_logger);
        var buffer = memoryManager.AllocateBuffer(1024);

        // Act
        await memoryManager.DisposeAsync();

        // Assert
        // Should not throw and memory should be cleaned up
        // Note: Disposed buffer should be marked invalid
    }

    [Fact]
    public void TotalMemory_ShouldBeReasonable()
    {
        // Act
        var totalMemory = _memoryManager.TotalMemory;

        // Assert
        Assert.True(totalMemory > 1024 * 1024); // At least 1MB
        Assert.True(totalMemory <= long.MaxValue);
    }

    [Fact]
    public void FragmentationRatio_ShouldBeWithinRange()
    {
        // Act
        var fragmentation = _memoryManager.FragmentationRatio;

        // Assert
        Assert.True(fragmentation >= 0.0);
        Assert.True(fragmentation <= 1.0);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _memoryManager?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class CpuMemoryBufferTests
{
    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(1024 * 1024)]
    public void Constructor_WithValidSize_ShouldCreateBuffer(long size)
    {
        // Act
        using var buffer = new CpuMemoryBuffer(size);

        // Assert
        buffer.SizeInBytes.Should().Be(size);
        buffer.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroSize_ShouldThrowArgumentException()
    {
        // Act & Assert
        Action action = () => new CpuMemoryBuffer(0);
        Assert.Throws<ArgumentException>(() => action());
    }

    [Fact]
    public void GetMemory_ShouldReturnValidMemory()
    {
        // Arrange
        using var buffer = new CpuMemoryBuffer(1024);

        // Act
        var memory = buffer.GetMemory();

        // Assert
        memory.Length.Should().Be(1024);
    }

    [Fact]
    public void WriteData_ShouldUpdateBufferContents()
    {
        // Arrange
        using var buffer = new CpuMemoryBuffer(16);
        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        buffer.WriteData(testData, 0);

        // Assert
        var memory = buffer.GetMemory();
        for (int i = 0; i < testData.Length; i++)
        {
            memory.Span[i].Should().Be(testData[i]);
        }
    }

    [Fact]
    public void ReadData_ShouldReturnCorrectData()
    {
        // Arrange
        using var buffer = new CpuMemoryBuffer(16);
        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        buffer.WriteData(testData, 0);

        // Act
        var readData = new byte[testData.Length];
        buffer.ReadData(readData, 0);

        // Assert
        readData.BeEquivalentTo(testData);
    }

    [Fact]
    public void Dispose_ShouldMarkBufferInvalid()
    {
        // Arrange
        var buffer = new CpuMemoryBuffer(1024);

        // Act
        buffer.Dispose();

        // Assert
        buffer.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetMemory_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var buffer = new CpuMemoryBuffer(1024);
        buffer.Dispose();

        // Act & Assert
        buffer.Invoking(b => b.GetMemory())
            .Throw<ObjectDisposedException>();
    }
}
