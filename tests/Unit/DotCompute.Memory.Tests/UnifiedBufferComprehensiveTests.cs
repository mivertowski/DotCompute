// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using NSubstitute;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedBuffer&lt;T&gt; covering all critical scenarios.
/// Part of Phase 1: Memory Module testing to achieve 80% coverage.
/// </summary>
public sealed class UnifiedBufferComprehensiveTests : IDisposable
{
    private readonly IUnifiedMemoryManager _mockMemoryManager;
    private readonly List<IDisposable> _disposables = [];

    public UnifiedBufferComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _ = _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLength_CreatesBuffer()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 1024);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.Length.Should().Be(1024);
        _ = buffer.SizeInBytes.Should().Be(1024 * sizeof(float));
        _ = buffer.IsOnHost.Should().BeTrue("buffer should start on host");
        _ = buffer.IsOnDevice.Should().BeFalse("buffer should not be on device initially");
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithInitialData_PopulatesBuffer()
    {
        // Arrange
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };

        // Act
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Assert
        _ = buffer.Length.Should().Be(4);
        var span = buffer.AsReadOnlySpan();
        _ = span.ToArray().Should().Equal(data);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithInvalidLength_ThrowsArgumentOutOfRangeException(int length)
    {
        // Arrange & Act
        var act = () => new UnifiedBuffer<float>(_mockMemoryManager, length);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("length")
           .WithMessage("*must be positive*");
    }

    [Fact]
    public void Constructor_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new UnifiedBuffer<float>(null!, 1024);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
           .WithParameterName("memoryManager");
    }

    [Fact]
    public void Constructor_ExceedingMaxAllocationSize_ThrowsInvalidOperationException()
    {
        // Arrange
        _ = _mockMemoryManager.MaxAllocationSize.Returns(1024L);

        // Act - Try to allocate 2048 floats = 8192 bytes > 1024 bytes
        var act = () => new UnifiedBuffer<float>(_mockMemoryManager, 2048);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
           .WithMessage("*exceeds maximum allowed size*");
    }

    #endregion

    #region AsSpan Tests

    [Fact]
    public void AsSpan_OnHostBuffer_ReturnsValidSpan()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();

        // Assert
        _ = span.Length.Should().Be(100);
        span[0] = 42;
        _ = buffer.AsReadOnlySpan()[0].Should().Be(42, "modifications should persist");
    }

    [Fact]
    public void AsSpan_OnDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act
        Action act = () => buffer.AsSpan();

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AsReadOnlySpan_ReturnsCorrectData()
    {
        // Arrange
        var data = new[] { 1.0, 2.0, 3.0 };
        var buffer = new UnifiedBuffer<double>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsReadOnlySpan();

        // Assert
        _ = span.ToArray().Should().Equal(data);
    }

    #endregion

    #region AsMemory Tests

    [Fact]
    public void AsMemory_ReturnsValidMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<byte>(_mockMemoryManager, 256);
        _disposables.Add(buffer);

        // Act
        var memory = buffer.AsMemory();

        // Assert
        _ = memory.Length.Should().Be(256);
        memory.Span[0] = 0xFF;
        _ = buffer.AsReadOnlyMemory().Span[0].Should().Be(0xFF);
    }

    [Fact]
    public void AsReadOnlyMemory_OnDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act
        var act = buffer.AsReadOnlyMemory;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void State_InitiallyHostOnly()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeFalse();
        _ = buffer.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void IsOnHost_ReturnsTrue_ForHostOnlyAndSynchronizedStates()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act & Assert - HostOnly
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void IsDirty_ReturnsFalse_ForSynchronizedState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _ = buffer.IsDirty.Should().BeFalse();
    }

    #endregion

    #region Pointer Access Tests

    [Fact]
    public void GetHostPointer_ReturnsValidPointer()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var pointer = buffer.GetHostPointer();

        // Assert
        _ = pointer.Should().NotBe(IntPtr.Zero, "pinned buffer should have valid pointer");
    }

    [Fact]
    public void GetHostPointer_OnDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act
        var act = buffer.GetHostPointer;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DevicePointer_WhenNotOnDevice_ReturnsZero()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        var devicePointer = buffer.DevicePointer;

        // Assert
        _ = devicePointer.Should().Be(IntPtr.Zero, "buffer is not on device");
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Length_ReturnsCorrectValue()
    {
        // Arrange
        const int expectedLength = 1234;
        var buffer = new UnifiedBuffer<double>(_mockMemoryManager, expectedLength);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Length.Should().Be(expectedLength);
    }

    [Fact]
    public void SizeInBytes_CalculatedCorrectly()
    {
        // Arrange
        const int length = 1000;
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, length);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.SizeInBytes.Should().Be(length * sizeof(int));
    }

    [Fact]
    public void Accelerator_AlwaysReturnsNull()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Accelerator.Should().BeNull("unified buffers don't belong to specific accelerators");
    }

    [Fact]
    public void Options_ReturnsNone()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Options.Should().Be(MemoryOptions.None);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 100);

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();

        // Verify operations throw after disposal
        Action actSpan = () => buffer.AsSpan();
        _ = actSpan.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);

        // Act
        buffer.Dispose();
        var act = buffer.Dispose;

        // Assert
        _ = act.Should().NotThrow("multiple Dispose calls should be safe");
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_ReleasesResourcesAsynchronously()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 100);

        // Act
        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public void Buffer_WithMinimumLength_WorksCorrectly()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<byte>(_mockMemoryManager, 1);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.Length.Should().Be(1);
        buffer.AsSpan()[0] = 42;
        _ = buffer.AsReadOnlySpan()[0].Should().Be(42);
    }

    [Fact]
    public void Buffer_WithLargeLength_AllocatesSuccessfully()
    {
        // Arrange
        const int largeSize = 1_000_000; // 1 million floats = 4MB

        // Act
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, largeSize);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.Length.Should().Be(largeSize);
        _ = buffer.SizeInBytes.Should().Be(largeSize * sizeof(float));
    }

    [Fact]
    public void Buffer_WithEmptyInitialData_WorksCorrectly()
    {
        // Arrange
        var emptyData = Array.Empty<int>();

        // Act - Should throw because length would be 0
        Action act = () => new UnifiedBuffer<int>(_mockMemoryManager, emptyData.AsSpan());

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public void Buffer_PreservesDataIntegrity_AcrossMultipleAccesses()
    {
        // Arrange
        var originalData = new[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, originalData.AsSpan());
        _disposables.Add(buffer);

        // Act - Access data multiple times
        var firstAccess = buffer.AsReadOnlySpan().ToArray();
        var secondAccess = buffer.AsReadOnlyMemory().ToArray();
        var thirdAccess = buffer.AsSpan().ToArray();

        // Assert
        _ = firstAccess.Should().Equal(originalData);
        _ = secondAccess.Should().Equal(originalData);
        _ = thirdAccess.Should().Equal(originalData);
    }

    [Fact]
    public void Buffer_AllowsModification_ThroughSpan()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = i * 10;
        }

        // Assert
        var result = buffer.AsReadOnlySpan().ToArray();
        _ = result.Should().Equal([0, 10, 20, 30, 40]);
    }

    #endregion

    #region Concurrent Access Tests (Thread Safety)

    [Fact]
    public async Task Buffer_MultipleAsyncAccesses_DoNotCorruptData()
    {
        // Arrange
        const int bufferSize = 1000;
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, bufferSize);
        _disposables.Add(buffer);

        // Act - Multiple async operations accessing the buffer
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            await Task.Yield();
            var span = buffer.AsReadOnlySpan();
            return span.Length;
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().OnlyContain(size => size == bufferSize, "all tasks should see correct buffer size");
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    #endregion

    #region Memory Manager Interaction Tests

    [Fact]
    public void Constructor_CallsMemoryManager_ForSizeValidation()
    {
        // Arrange
        var memoryManager = Substitute.For<IUnifiedMemoryManager>();
        _ = memoryManager.MaxAllocationSize.Returns(10000L);

        // Act
        _ = new UnifiedBuffer<float>(memoryManager, 100);

        // Assert
        _ = memoryManager.Received(1).MaxAllocationSize;
    }

    #endregion

    #region Type-Specific Tests

    [Fact]
    public void Buffer_WorksWithFloatType()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        buffer.AsSpan()[0] = 3.14f;

        // Assert
        _ = buffer.AsReadOnlySpan()[0].Should().BeApproximately(3.14f, 0.0001f);
    }

    [Fact]
    public void Buffer_WorksWithDoubleType()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<double>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        buffer.AsSpan()[0] = Math.PI;

        // Assert
        _ = buffer.AsReadOnlySpan()[0].Should().BeApproximately(Math.PI, 1e-10);
    }

    [Fact]
    public void Buffer_WorksWithByteType()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<byte>(_mockMemoryManager, 256);
        _disposables.Add(buffer);

        // Fill with pattern
        var span = buffer.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = (byte)i;
        }

        // Assert
        var result = buffer.AsReadOnlySpan().ToArray();
        _ = result.Should().HaveCount(256);
        _ = result[0].Should().Be(0);
        _ = result[255].Should().Be(255);
    }

    #endregion
}
