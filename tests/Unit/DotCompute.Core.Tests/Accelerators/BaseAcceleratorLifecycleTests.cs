// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Tests.TestImplementations;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests.Accelerators;

/// <summary>
/// Tests for BaseAccelerator lifecycle management including initialization, disposal, and state transitions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "BaseAccelerator")]
[Trait("TestType", "Lifecycle")]
public sealed class BaseAcceleratorLifecycleTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly TestAccelerator _accelerator;
    private readonly List<TestAccelerator> _accelerators = [];
    private bool _disposed;

    public BaseAcceleratorLifecycleTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMemory = new Mock<IUnifiedMemoryManager>();

        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Test Accelerator",
            "1.0",
            1024 * 1024 * 1024,
            4,
            3000,
            new Version(1, 0),
            1024 * 1024,
            true
        );

        _accelerator = new TestAccelerator(info, _mockMemory.Object, _mockLogger.Object);
        _accelerators.Add(_accelerator);
    }

    [Fact]
    public void Constructor_InitializesProperties_Correctly()
    {
        // Assert
        _ = _accelerator.Info.Should().NotBeNull();
        _ = _accelerator.Type.Should().Be(AcceleratorType.CPU);
        _ = _accelerator.Memory.Should().Be(_mockMemory.Object);
        _ = _accelerator.IsDisposed.Should().BeFalse();
        _ = _accelerator.Context.Should().NotBeNull();
    }

    [Theory]
    [InlineData(AcceleratorType.GPU, "GPU Device", "2.0", 2048L * 1024 * 1024)]
    [InlineData(AcceleratorType.CUDA, "CUDA Device", "12.0", 8192L * 1024 * 1024)]
    [InlineData(AcceleratorType.OpenCL, "OpenCL Device", "3.0", 4096L * 1024 * 1024)]
    public void Constructor_WithVariousConfigurations_InitializesCorrectly(
        AcceleratorType type, string name, string driverVersion, long memorySize)
    {
        // Arrange
        var info = new AcceleratorInfo(
            type, name, driverVersion, memorySize, 8, 1500,
            new Version(1, 0), 64 * 1024, type == AcceleratorType.CPU);
        var mockMemory = new Mock<IUnifiedMemoryManager>();
        var mockLogger = new Mock<ILogger>();

        // Act
        var accelerator = new TestAccelerator(info, mockMemory.Object, mockLogger.Object);
        _accelerators.Add(accelerator);

        // Assert
        _ = accelerator.Info.Name.Should().Be(name);
        _ = accelerator.Type.Should().Be(type);
        _ = accelerator.Memory.Should().Be(mockMemory.Object);
        _ = accelerator.IsDisposed.Should().BeFalse();
        _ = accelerator.InitializeCoreCalled.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenCalled_DisposesResourcesCorrectly()
    {
        // Act
        _accelerator.Dispose();

        // Assert
        _ = _accelerator.IsDisposed.Should().BeTrue();
        _ = _accelerator.DisposedCount.Should().Be(1);
        _mockMemory.Verify(m => m.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DisposesOnlyOnce()
    {
        // Act
        _accelerator.Dispose();
        _accelerator.Dispose();
        _accelerator.Dispose();

        // Assert
        _ = _accelerator.IsDisposed.Should().BeTrue();
        _ = _accelerator.DisposedCount.Should().Be(1);
        _mockMemory.Verify(m => m.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_DisposesResourcesCorrectly()
    {
        // Act
        await _accelerator.DisposeAsync();

        // Assert
        _ = _accelerator.IsDisposed.Should().BeTrue();
        _ = _accelerator.DisposedCount.Should().Be(1);
        _mockMemory.Verify(m => m.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_DisposesOnlyOnce()
    {
        // Act
        await _accelerator.DisposeAsync();
        await _accelerator.DisposeAsync();
        await _accelerator.DisposeAsync();

        // Assert
        _ = _accelerator.IsDisposed.Should().BeTrue();
        _ = _accelerator.DisposedCount.Should().Be(1);
        _mockMemory.Verify(m => m.Dispose(), Times.Once);
    }

    [Fact]
    public void Context_ReturnsNonNullContext()
    {
        // Assert
        _ = _accelerator.Context.Should().NotBeNull();
        _ = _accelerator.Context.Should().BeOfType<AcceleratorContext>();
    }

    [Fact]
    public void Synchronize_WhenNotDisposed_CompletesSuccessfully()
    {
        // Act
        var act = _accelerator.Synchronize;

        // Assert
        _ = act.Should().NotThrow();
        _ = _accelerator.SynchronizeCalled.Should().BeTrue();
    }

    [Fact]
    public void Synchronize_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _accelerator.Dispose();

        // Act
        var act = _accelerator.Synchronize;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task ConcurrentDisposal_HandledCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();
        var barrier = new Barrier(10);

        // Act - Create 10 tasks that will all try to dispose concurrently
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                _accelerator.Dispose();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should only dispose once despite concurrent calls
        _ = _accelerator.IsDisposed.Should().BeTrue();
        _ = _accelerator.DisposedCount.Should().Be(1);
        _mockMemory.Verify(m => m.Dispose(), Times.Once);
    }

    [Fact]
    public void DisposalState_TrackedCorrectly()
    {
        // Assert initial state
        _ = _accelerator.IsDisposed.Should().BeFalse();
        _ = _accelerator.DisposedCount.Should().Be(0);

        // Act - Dispose
        _accelerator.Dispose();

        // Assert final state
        _ = _accelerator.IsDisposed.Should().BeTrue();
        _ = _accelerator.DisposedCount.Should().Be(1);

        // Act - Try to use after disposal
        var act = _accelerator.TestThrowIfDisposed;
        _ = act.Should().Throw<ObjectDisposedException>()
            .WithMessage($"*{nameof(TestAccelerator)}*");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var accelerator in _accelerators)
        {
            accelerator?.Dispose();
        }

        _disposed = true;
    }
}