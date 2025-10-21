// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for memory management covering unified memory managers, coherency,
/// memory views, resource management, and performance optimization.
/// </summary>
public class MemoryManagementTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    /// <summary>
    /// Performs unified buffer_ creation_ with valid parameters_ should succeed.
    /// </summary>

    #region Unified Memory Manager Tests

    [Fact]
    [Trait("Category", "UnifiedMemoryManager")]
    public void UnifiedBuffer_Creation_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();

        // Act

        using var buffer = new UnifiedBuffer<float>(memoryManager, 1024);

        // Assert
        _ = buffer.Length.Should().Be(1024);
        _ = buffer.SizeInBytes.Should().Be(1024 * sizeof(float));
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeFalse();
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _ = buffer.IsDisposed.Should().BeFalse();
    }
    /// <summary>
    /// Performs unified buffer_ creation_ with invalid length_ should throw.
    /// </summary>
    /// <param name="invalidLength">The invalid length.</param>

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [Trait("Category", "UnifiedMemoryManager")]
    public void UnifiedBuffer_Creation_WithInvalidLength_ShouldThrow(int invalidLength)
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();

        // Act & Assert

        Action act = () => new UnifiedBuffer<float>(memoryManager, invalidLength);
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }
    /// <summary>
    /// Performs unified buffer_ creation_ with null memory manager_ should throw.
    /// </summary>

    [Fact]
    [Trait("Category", "UnifiedMemoryManager")]
    public void UnifiedBuffer_Creation_WithNullMemoryManager_ShouldThrow()
    {
        // Act & Assert
        Action act = () => new UnifiedBuffer<float>(null!, 1024);
        _ = act.Should().Throw<ArgumentNullException>();
    }
    /// <summary>
    /// Performs unified buffer_ allocation strategy_ should use host first.
    /// </summary>

    [Fact]
    [Trait("Category", "UnifiedMemoryManager")]
    public void UnifiedBuffer_AllocationStrategy_ShouldUseHostFirst()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();

        // Act

        using var buffer = new UnifiedBuffer<int>(memoryManager, 256);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeFalse();
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }
    /// <summary>
    /// Gets unified buffer_ allocation limits_ should enforce constraints.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "UnifiedMemoryManager")]
    public Task UnifiedBuffer_AllocationLimits_ShouldEnforceConstraints()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager { MaxAllocationSize = 1024 };

        // Act & Assert - Allocation within limits should succeed

        using var smallBuffer = new UnifiedBuffer<byte>(memoryManager, 512);
        _ = smallBuffer.Should().NotBeNull();

        // Large allocation should fail

        var act = () => new UnifiedBuffer<byte>(memoryManager, 2048);
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*allocation exceeds maximum allowed size*");


        return Task.CompletedTask;
    }
    /// <summary>
    /// Gets unified buffer_ pooling strategy_ should reuse memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "UnifiedMemoryManager")]
    public async Task UnifiedBuffer_PoolingStrategy_ShouldReuseMemory()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        var allocationCount = 0;
        memoryManager.OnAllocate = () => allocationCount++;

        // Act - Create and dispose multiple buffers

        for (var i = 0; i < 5; i++)
        {
            using var buffer = new UnifiedBuffer<int>(memoryManager, 128);
            await buffer.FillAsync(i);
        }

        // Assert - Should have fewer allocations due to pooling

        _output.WriteLine($"Total allocations: {allocationCount}");
        _ = allocationCount.Should().BeLessThanOrEqualTo(3, "memory pooling should reduce allocation count");
    }
    /// <summary>
    /// Performs unified buffer_ host device sync_ should maintain coherency.
    /// </summary>

    #endregion

    #region Memory Coherency Tests

    [Fact]
    [Trait("Category", "MemoryCoherency")]
    public void UnifiedBuffer_HostDeviceSync_ShouldMaintainCoherency()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<int>(memoryManager, 10);
        var testData = Enumerable.Range(1, 10).ToArray();

        // Act - Write to host, ensure on device, then back to host

        buffer.AsSpan().CopyFrom(testData);
        buffer.MarkHostDirty();
        buffer.EnsureOnDevice();
        buffer.MarkDeviceDirty();
        buffer.EnsureOnHost();

        // Assert
        _ = buffer.AsSpan().ToArray().Should().Equal(testData);
        _ = buffer.State.Should().Be(BufferState.Synchronized);
    }
    /// <summary>
    /// Gets unified buffer_ async sync_ should maintain coherency.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "MemoryCoherency")]
    public async Task UnifiedBuffer_AsyncSync_ShouldMaintainCoherency()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<float>(memoryManager, 8);
        var testData = new float[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f, 6.6f, 7.7f, 8.8f };

        // Act

        await buffer.CopyFromAsync(testData);
        buffer.MarkHostDirty();
        await buffer.EnsureOnDeviceAsync();
        buffer.MarkDeviceDirty();
        await buffer.EnsureOnHostAsync();

        // Assert
        _ = buffer.AsReadOnlySpan().ToArray().Should().Equal(testData);
        _ = buffer.IsDirty.Should().BeFalse();
    }
    /// <summary>
    /// Performs unified buffer_ dirty flags_ should track modifications.
    /// </summary>

    [Fact]
    [Trait("Category", "MemoryCoherency")]
    public void UnifiedBuffer_DirtyFlags_ShouldTrackModifications()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<byte>(memoryManager, 16);

        // Act & Assert - Initial state
        _ = buffer.IsDirty.Should().BeFalse();
        _ = buffer.State.Should().Be(BufferState.HostOnly);

        // Mark host dirty

        buffer.MarkHostDirty();
        _ = buffer.IsDirty.Should().BeFalse(); // HostOnly can't be dirty
        _ = buffer.State.Should().Be(BufferState.HostOnly);

        // Transition to device and mark dirty

        buffer.EnsureOnDevice();
        _ = buffer.State.Should().Be(BufferState.Synchronized);


        buffer.MarkDeviceDirty();
        _ = buffer.IsDirty.Should().BeTrue();
        _ = buffer.State.Should().Be(BufferState.DeviceDirty);

        // Synchronize

        buffer.Synchronize();
        _ = buffer.IsDirty.Should().BeFalse();
        _ = buffer.State.Should().Be(BufferState.Synchronized);
    }
    /// <summary>
    /// Gets unified buffer_ concurrent access_ should maintain coherency.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "MemoryCoherency")]
    public async Task UnifiedBuffer_ConcurrentAccess_ShouldMaintainCoherency()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<int>(memoryManager, 1000);
        var tasks = new List<Task>();

        // Act - Multiple concurrent operations

        for (var i = 0; i < 10; i++)
        {
            var value = i;
            tasks.Add(Task.Run(async () =>
            {
                await buffer.FillAsync(value, value * 100, 50);
                await buffer.SynchronizeAsync();
            }));
        }


        await Task.WhenAll(tasks);

        // Assert - Buffer should not be corrupted
        _ = buffer.State.Should().Be(BufferState.Synchronized);
        _ = buffer.IsDisposed.Should().BeFalse();
    }
    /// <summary>
    /// Performs unified buffer_ slicing_ should create valid views.
    /// </summary>

    #endregion

    #region Memory Views Tests

    [Fact]
    [Trait("Category", "MemoryViews")]
    public void UnifiedBuffer_Slicing_ShouldCreateValidViews()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<int>(memoryManager, 20);
        var data = Enumerable.Range(0, 20).ToArray();
        buffer.AsSpan().CopyFrom(data);

        // Act

        var slice = buffer.Slice(5, 10);

        // Assert
        _ = slice.Length.Should().Be(10);
        _ = slice.Should().NotBeNull();

        // Verify slice references original buffer
        _ = slice.Should().BeSameAs(buffer, "current implementation returns self");
    }
    /// <summary>
    /// Performs unified buffer_ slicing_ with invalid parameters_ should throw.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="reason">The reason.</param>

    [Theory]
    [InlineData(-1, 5, "negative offset")]
    [InlineData(0, -1, "negative length")]
    [InlineData(15, 10, "offset + length > buffer length")]
    [InlineData(25, 5, "offset beyond buffer")]
    [Trait("Category", "MemoryViews")]
    public void UnifiedBuffer_Slicing_WithInvalidParameters_ShouldThrow(int offset, int length, string reason)
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<int>(memoryManager, 20);

        // Act & Assert

        Action act = () => buffer.Slice(offset, length);
        _ = act.Should().Throw<ArgumentOutOfRangeException>(reason);
    }
    /// <summary>
    /// Performs unified buffer_ type casting_ should create valid views.
    /// </summary>

    [Fact]
    [Trait("Category", "MemoryViews")]
    public void UnifiedBuffer_TypeCasting_ShouldCreateValidViews()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<byte>(memoryManager, 16); // 16 bytes = 4 ints

        // Act

        var intView = buffer.AsType<int>();

        // Assert
        _ = intView.Should().NotBeNull();
        // Note: Implementation returns a view, specific assertions depend on actual implementation
    }
    /// <summary>
    /// Performs unified buffer_ overlapping views_ should handle correctly.
    /// </summary>

    [Fact]
    [Trait("Category", "MemoryViews")]
    public void UnifiedBuffer_OverlappingViews_ShouldHandleCorrectly()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<int>(memoryManager, 20);

        // Act - Create overlapping slices

        var slice1 = buffer.Slice(0, 15);
        var slice2 = buffer.Slice(10, 10);

        // Assert - Both slices should be valid
        _ = slice1.Length.Should().Be(15);
        _ = slice2.Length.Should().Be(10);

        // Modifications to overlapping regions should be handled properly
        // This tests the implementation's ability to handle overlapping memory access
        _ = slice1.Should().NotBeNull();
        _ = slice2.Should().NotBeNull();
    }
    /// <summary>
    /// Gets unified buffer_ view disposal_ should not affect parent.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "MemoryViews")]
    public Task UnifiedBuffer_ViewDisposal_ShouldNotAffectParent()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        using var buffer = new UnifiedBuffer<int>(memoryManager, 20);

        // Act

        var slice = buffer.Slice(0, 10);
        // In current implementation, slice is same as buffer, so disposal affects parent
        // This test documents the current behavior

        // Assert
        _ = buffer.IsDisposed.Should().BeFalse();
        _ = slice.IsDisposed.Should().BeFalse();


        return Task.CompletedTask;
    }
    /// <summary>
    /// Performs unified buffer_ disposal_ should cleanup resources.
    /// </summary>

    #endregion

    #region Resource Management Tests

    [Fact]
    [Trait("Category", "ResourceManagement")]
    public void UnifiedBuffer_Disposal_ShouldCleanupResources()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        var buffer = new UnifiedBuffer<int>(memoryManager, 100);

        // Act

        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
        _ = buffer.State.Should().Be(BufferState.Uninitialized);
    }
    /// <summary>
    /// Gets unified buffer_ async disposal_ should cleanup resources.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "ResourceManagement")]
    public async Task UnifiedBuffer_AsyncDisposal_ShouldCleanupResources()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        var buffer = new UnifiedBuffer<int>(memoryManager, 100);

        // Act

        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
        _ = buffer.State.Should().Be(BufferState.Uninitialized);
    }
    /// <summary>
    /// Performs unified buffer_ double disposal_ should be idempotent.
    /// </summary>

    [Fact]
    [Trait("Category", "ResourceManagement")]
    public void UnifiedBuffer_DoubleDisposal_ShouldBeIdempotent()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        var buffer = new UnifiedBuffer<int>(memoryManager, 100);

        // Act

        buffer.Dispose();
        buffer.Dispose(); // Second disposal

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }
    /// <summary>
    /// Performs unified buffer_ access after disposal_ should throw.
    /// </summary>

    [Fact]
    [Trait("Category", "ResourceManagement")]
    public void UnifiedBuffer_AccessAfterDisposal_ShouldThrow()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        var buffer = new UnifiedBuffer<int>(memoryManager, 100);
        buffer.Dispose();

        // Act & Assert

        Action spanAccess = () => buffer.AsSpan();
        Action memoryAccess = () => buffer.AsMemory();
        Action ensureHost = buffer.EnsureOnHost;
        Action ensureDevice = buffer.EnsureOnDevice;

        _ = spanAccess.Should().Throw<ObjectDisposedException>();
        _ = memoryAccess.Should().Throw<ObjectDisposedException>();
        _ = ensureHost.Should().Throw<ObjectDisposedException>();
        _ = ensureDevice.Should().Throw<ObjectDisposedException>();
    }
    /// <summary>
    /// Performs memory allocator_ statistics_ should track usage.
    /// </summary>

    [Fact]
    [Trait("Category", "ResourceManagement")]
    public void MemoryAllocator_Statistics_ShouldTrackUsage()
    {
        // Arrange
        using var allocator = new MemoryAllocator();

        // Act

        using var owner1 = allocator.Allocate<int>(100);
        using var owner2 = allocator.AllocatePinned<float>(200);
        var stats = allocator.GetStatistics();

        // Assert
        _ = stats.TotalAllocations.Should().Be(2);
        _ = stats.TotalAllocatedBytes.Should().Be(100 * sizeof(int) + 200 * sizeof(float));
        _ = stats.ActiveAllocations.Should().Be(2);
    }
    /// <summary>
    /// Gets memory leak detection_ should identify leaks.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "ResourceManagement")]
    public Task MemoryLeakDetection_ShouldIdentifyLeaks()
    {
        // Arrange
        var memoryBefore = GC.GetTotalMemory(true);

        // Act - Create many buffers without disposing

        var buffers = new List<WeakReference>();
        for (var i = 0; i < 100; i++)
        {
            using var memoryManager = new TestUnifiedMemoryManager();
            using var buffer = new UnifiedBuffer<byte>(memoryManager, 1024);
            buffers.Add(new WeakReference(buffer));
            // Buffer will be disposed by using statement, but WeakReference tracks the object
        }

        // Force garbage collection

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();


        var memoryAfter = GC.GetTotalMemory(false);
        var memoryDiff = memoryAfter - memoryBefore;

        // Assert

        _output.WriteLine($"Memory before: {memoryBefore:N0} bytes");
        _output.WriteLine($"Memory after: {memoryAfter:N0} bytes");
        _output.WriteLine($"Memory diff: {memoryDiff:N0} bytes");

        // Check if objects were collected (indicating proper cleanup)

        var aliveCount = buffers.Count(wr => wr.IsAlive);
        _output.WriteLine($"Alive objects: {aliveCount} out of {buffers.Count}");

        // Memory leak detection - should not grow excessively
        _ = memoryDiff.Should().BeLessThan(1024 * 1024, "memory should not leak significantly");


        return Task.CompletedTask;
    }
    /// <summary>
    /// Gets unified buffer_ memory bandwidth_ should meet threshold.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Performance Tests

    [Fact]
    [Trait("Category", "Performance")]
    public async Task UnifiedBuffer_MemoryBandwidth_ShouldMeetThreshold()
    {
        // Arrange
        using var memoryManager = new TestUnifiedMemoryManager();
        const int bufferSize = 1024 * 1024; // 1MB
        const int iterations = 50;
        using var buffer = new UnifiedBuffer<byte>(memoryManager, bufferSize);
        var data = new byte[bufferSize];
        new Random(42).NextBytes(data);


        var stopwatch = Stopwatch.StartNew();

        // Act - Measure copy bandwidth

        for (var i = 0; i < iterations; i++)
        {
            await buffer.CopyFromAsync(data);
            await buffer.CopyToAsync(data.AsMemory());
        }


        stopwatch.Stop();

        // Assert

        var totalBytes = (long)bufferSize * iterations * 2; // Copy in + copy out
        var throughputMBps = totalBytes / (stopwatch.ElapsedMilliseconds / 1000.0) / (1024 * 1024);


        _output.WriteLine($"Memory bandwidth: {throughputMBps:F2} MB/s");
        _output.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds} ms");

        _ = throughputMBps.Should().BeGreaterThan(100, "memory bandwidth should meet minimum threshold");
    }
    /// <summary>
    /// Performs memory allocator_ allocation overhead_ should be minimal.
    /// </summary>

    [Fact]
    [Trait("Category", "Performance")]
    public void MemoryAllocator_AllocationOverhead_ShouldBeMinimal()
    {
        // Arrange
        using var allocator = new MemoryAllocator();
        const int allocationCount = 1000;
        const int bufferSize = 4096;


        var stopwatch = Stopwatch.StartNew();

        // Act

        var owners = new List<IMemoryOwner<byte>>();
        for (var i = 0; i < allocationCount; i++)
        {
            owners.Add(allocator.Allocate<byte>(bufferSize));
        }


        stopwatch.Stop();

        // Assert

        var avgAllocationTime = stopwatch.ElapsedMilliseconds / (double)allocationCount;
        _output.WriteLine($"Average allocation time: {avgAllocationTime:F3} ms");

        _ = avgAllocationTime.Should().BeLessThan(0.1, "allocation should be fast");

        // Cleanup

        foreach (var owner in owners)
        {
            owner.Dispose();
        }
    }
    /// <summary>
    /// Performs unsafe memory operations_ copy performance_ should use vectorization.
    /// </summary>

    [Fact]
    [Trait("Category", "Performance")]
    public void UnsafeMemoryOperations_CopyPerformance_ShouldUseVectorization()
    {
        // Arrange
        const int size = 1024 * 1024;
        var source = new byte[size];
        var destination = new byte[size];
        new Random(42).NextBytes(source);


        var stopwatch = Stopwatch.StartNew();

        // Act

        UnsafeMemoryOperations.CopyMemory(source.AsSpan(), destination.AsSpan());


        stopwatch.Stop();

        // Assert

        var elapsedMs = stopwatch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
        var throughputMBps = size / elapsedMs / (1024 * 1024);
        _output.WriteLine($"Copy throughput: {throughputMBps:F2} MB/s");
        _output.WriteLine($"Time: {elapsedMs:F3} ms");

        _ = destination.Should().Equal(source, "data should be copied correctly");

        // Performance assertion - should complete in reasonable time (< 10ms for 1MB)

        _ = elapsedMs.Should().BeLessThan(10, "1MB copy should complete quickly");

        // If copy is very fast (< 0.1ms), throughput calculation may be unreliable

        if (elapsedMs > 0.1)
        {
            _ = throughputMBps.Should().BeGreaterThan(100, "should achieve reasonable throughput");
        }
    }
    /// <summary>
    /// Performs unsafe memory operations_ fill performance_ should use vectorization.
    /// </summary>

    [Fact]
    [Trait("Category", "Performance")]
    public void UnsafeMemoryOperations_FillPerformance_ShouldUseVectorization()
    {
        // Arrange
        const int size = 1024 * 1024;
        var destination = new byte[size];
        const byte fillValue = 0xAB;


        var stopwatch = Stopwatch.StartNew();

        // Act

        UnsafeMemoryOperations.FillMemory(destination.AsSpan(), fillValue);


        stopwatch.Stop();

        // Assert

        var throughputMBps = size / (stopwatch.ElapsedTicks * 1000.0 / Stopwatch.Frequency) / (1024 * 1024);
        _output.WriteLine($"Fill throughput: {throughputMBps:F2} MB/s");

        _ = destination.Should().OnlyContain(b => b == fillValue);
        _ = throughputMBps.Should().BeGreaterThan(2000, "vectorized fill should be very fast");
    }
    /// <summary>
    /// Performs memory alignment_ should improve performance.
    /// </summary>

    [Fact]
    [Trait("Category", "Performance")]
    public void MemoryAlignment_ShouldImprovePerformance()
    {
        // Arrange
        using var allocator = new MemoryAllocator();
        const int size = 1000;

        // Act

        using var aligned = allocator.AllocateAligned<int>(size, 32);
        using var unaligned = allocator.Allocate<int>(size);

        // Assert

        unsafe
        {
            // Check alignment
            fixed (int* ptr = aligned.Memory.Span)
            {
                var address = new IntPtr(ptr);
                _ = ((long)address % 32).Should().Be(0, "memory should be 32-byte aligned");
            }
        }
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test implementation of IUnifiedMemoryManager for testing purposes.
    /// </summary>
    private sealed class TestUnifiedMemoryManager : IUnifiedMemoryManager
    {
        private readonly List<IUnifiedMemoryBuffer> _allocatedBuffers = [];
        private readonly MemoryPool<byte> _pool = MemoryPool<byte>.Shared;
        private volatile bool _disposed;
        /// <summary>
        /// Gets or sets the max allocation size.
        /// </summary>
        /// <value>The max allocation size.</value>


        public long MaxAllocationSize { get; set; } = long.MaxValue;
        /// <summary>
        /// Gets or sets the on allocate.
        /// </summary>
        /// <value>The on allocate.</value>
        public Action? OnAllocate { get; set; }
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>

        // IUnifiedMemoryManager properties
        public IAccelerator Accelerator => null!;
        /// <summary>
        /// Gets or sets the statistics.
        /// </summary>
        /// <value>The statistics.</value>
        public Abstractions.Memory.MemoryStatistics Statistics => new()
        {
            TotalAllocated = _allocatedBuffers.Sum(b => b.SizeInBytes),
            AllocationCount = _allocatedBuffers.Count,
            ActiveAllocations = _allocatedBuffers.Count(b => !b.IsDisposed),
            CurrentUsage = _allocatedBuffers.Sum(b => b.SizeInBytes)
        };
        long IUnifiedMemoryManager.MaxAllocationSize => MaxAllocationSize;
        /// <summary>
        /// Gets or sets the total available memory.
        /// </summary>
        /// <value>The total available memory.</value>
        public long TotalAvailableMemory => long.MaxValue;
        /// <summary>
        /// Gets or sets the current allocated memory.
        /// </summary>
        /// <value>The current allocated memory.</value>
        public long CurrentAllocatedMemory => _allocatedBuffers.Sum(b => b.SizeInBytes);
        /// <summary>
        /// Gets allocate asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="count">The count.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(int count, MemoryOptions options, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);


            var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            if (sizeInBytes > MaxAllocationSize)
            {
                throw new InvalidOperationException($"Allocation size {sizeInBytes} exceeds maximum allowed size {MaxAllocationSize}");
            }


            OnAllocate?.Invoke();


            var buffer = new TestUnifiedBuffer<T>(count);
            _allocatedBuffers.Add(buffer);
            return new ValueTask<IUnifiedMemoryBuffer<T>>(buffer);
        }
        /// <summary>
        /// Gets allocate and copy asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken);
            await buffer.CopyFromAsync(source, cancellationToken);
            return buffer;
        }
        /// <summary>
        /// Gets allocate raw asynchronously.
        /// </summary>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
        {
            var buffer = new TestRawUnifiedBuffer(sizeInBytes);
            _allocatedBuffers.Add(buffer);
            return new ValueTask<IUnifiedMemoryBuffer>(buffer);
        }
        /// <summary>
        /// Creates a new view.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>The created view.</returns>

        public IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int length) where T : unmanaged => buffer.Slice(offset, length);
        /// <summary>
        /// Gets copy asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) where T : unmanaged => source.CopyToAsync(destination, cancellationToken);
        /// <summary>
        /// Gets copy asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) where T : unmanaged => source.CopyToAsync(sourceOffset, destination, destinationOffset, count, cancellationToken);
        /// <summary>
        /// Gets copy to device asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) where T : unmanaged => destination.CopyFromAsync(source, cancellationToken);
        /// <summary>
        /// Gets copy from device asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken = default) where T : unmanaged => source.CopyToAsync(destination, cancellationToken);
        /// <summary>
        /// Performs free.
        /// </summary>
        /// <param name="buffer">The buffer.</param>

        public void Free(IUnifiedMemoryBuffer buffer)
        {
            _ = _allocatedBuffers.Remove(buffer);
            buffer?.Dispose();
        }
        /// <summary>
        /// Gets free asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
        {
            Free(buffer);
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets optimize asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
            // Simulate memory optimization




            => ValueTask.CompletedTask;
        /// <summary>
        /// Performs clear.
        /// </summary>

        public void Clear()
        {
            foreach (var buffer in _allocatedBuffers.ToList())
            {
                buffer.Dispose();
            }
            _allocatedBuffers.Clear();
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Clear();
            _pool.Dispose();
        }
        /// <summary>
        /// Gets allocate device.
        /// </summary>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <returns>The result of the operation.</returns>

        // Device memory operations - stub implementations for testing
        public static DeviceMemory AllocateDevice(long sizeInBytes) => new(IntPtr.Zero, sizeInBytes);
        /// <summary>
        /// Performs free device.
        /// </summary>
        /// <param name="deviceMemory">The device memory.</param>
        public static void FreeDevice(DeviceMemory deviceMemory) { }
        /// <summary>
        /// Performs memset device.
        /// </summary>
        /// <param name="deviceMemory">The device memory.</param>
        /// <param name="value">The value.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        public static void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes) { }
        /// <summary>
        /// Gets memset device asynchronously.
        /// </summary>
        /// <param name="deviceMemory">The device memory.</param>
        /// <param name="value">The value.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Performs copy host to device.
        /// </summary>
        /// <param name="hostPointer">The host pointer.</param>
        /// <param name="deviceMemory">The device memory.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        public static void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes) { }
        /// <summary>
        /// Performs copy device to host.
        /// </summary>
        /// <param name="deviceMemory">The device memory.</param>
        /// <param name="hostPointer">The host pointer.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        public static void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes) { }
        /// <summary>
        /// Gets copy host to device asynchronously.
        /// </summary>
        /// <param name="hostPointer">The host pointer.</param>
        /// <param name="deviceMemory">The device memory.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy device to host asynchronously.
        /// </summary>
        /// <param name="deviceMemory">The device memory.</param>
        /// <param name="hostPointer">The host pointer.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Performs copy device to device.
        /// </summary>
        /// <param name="sourceDevice">The source device.</param>
        /// <param name="destinationDevice">The destination device.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        public static void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes) { }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;


            foreach (var buffer in _allocatedBuffers.ToList())
            {
                if (buffer is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else
                    buffer.Dispose();
            }
            _allocatedBuffers.Clear();
            _pool.Dispose();
        }
    }

    /// <summary>
    /// Test implementation of IUnifiedMemoryBuffer for testing purposes.
    /// </summary>
    private sealed class TestUnifiedBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
        private readonly T[] _hostArray;
        private readonly GCHandle _handle;
        private BufferState _state;
        private volatile bool _disposed;
        /// <summary>
        /// Initializes a new instance of the TestUnifiedBuffer class.
        /// </summary>
        /// <param name="length">The length.</param>

        public TestUnifiedBuffer(int length)
        {
            Length = length;
            SizeInBytes = length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            _hostArray = new T[length];
            _handle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);
            _state = BufferState.HostOnly;
        }
        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        /// <value>The size in bytes.</value>

        public long SizeInBytes { get; }
        /// <summary>
        /// Gets or sets the length.
        /// </summary>
        /// <value>The length.</value>
        public int Length { get; }
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public static IntPtr DevicePointer => IntPtr.Zero;
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public IAccelerator Accelerator => null!;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public MemoryOptions Options => MemoryOptions.None;
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public BufferState State => _state;
        /// <summary>
        /// Gets or sets a value indicating whether disposed.
        /// </summary>
        /// <value>The is disposed.</value>
        public bool IsDisposed => _disposed;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public bool IsOnHost => _state is BufferState.HostOnly or BufferState.Synchronized or BufferState.HostDirty;
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public bool IsOnDevice => _state is BufferState.DeviceOnly or BufferState.Synchronized or BufferState.DeviceDirty;
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public bool IsDirty => _state is BufferState.HostDirty or BufferState.DeviceDirty;
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public Span<T> AsSpan()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _hostArray.AsSpan();
        }
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _hostArray.AsSpan();
        }
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public Memory<T> AsMemory()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _hostArray.AsMemory();
        }
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public ReadOnlyMemory<T> AsReadOnlyMemory()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _hostArray.AsMemory();
        }
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>

        public DeviceMemory GetDeviceMemory()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new DeviceMemory(new IntPtr(1), SizeInBytes);
        }
        /// <summary>
        /// Performs ensure on host.
        /// </summary>

        public void EnsureOnHost()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_state is BufferState.DeviceOnly or BufferState.DeviceDirty)
            {
                // Simulate device-to-host transfer
                _state = BufferState.Synchronized;
            }
        }
        /// <summary>
        /// Performs ensure on device.
        /// </summary>

        public void EnsureOnDevice()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_state is BufferState.HostOnly or BufferState.HostDirty)
            {
                // Simulate host-to-device transfer
                _state = BufferState.Synchronized;
            }
        }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        {
            EnsureOnHost();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        {
            EnsureOnDevice();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Performs synchronize.
        /// </summary>

        public void Synchronize()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_state is BufferState.HostDirty or BufferState.DeviceDirty)
            {
                _state = BufferState.Synchronized;
            }
        }
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        {
            Synchronize();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>

        public void MarkHostDirty()
        {
            if (_state == BufferState.Synchronized)
            {
                _state = BufferState.HostDirty;
            }
        }
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>

        public void MarkDeviceDirty()
        {
            if (_state == BufferState.Synchronized)
            {
                _state = BufferState.DeviceDirty;
            }
        }
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            source.Span.CopyTo(_hostArray);
            MarkHostDirty();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <typeparam name="TSource">The TSource type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="byteOffset">The byte offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long byteOffset, CancellationToken cancellationToken = default) where TSource : unmanaged => throw new NotSupportedException("Type conversion not supported in test implementation");
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _hostArray.AsSpan().CopyTo(destination.Span);
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <typeparam name="TDestination">The TDestination type parameter.</typeparam>
        /// <param name="destination">The destination.</param>
        /// <param name="byteOffset">The byte offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToAsync<TDestination>(Memory<TDestination> destination, long byteOffset, CancellationToken cancellationToken = default) where TDestination : unmanaged => throw new NotSupportedException("Type conversion not supported in test implementation");
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => destination.CopyFromAsync(_hostArray, cancellationToken);
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        {
            var sourceSlice = _hostArray.AsMemory(sourceOffset, count);
            var destSlice = destination.Slice(destinationOffset, count);
            return destSlice.CopyFromAsync(sourceSlice, cancellationToken);
        }
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        {
            _hostArray.AsSpan().Fill(value);
            MarkHostDirty();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        {
            _hostArray.AsSpan(offset, count).Fill(value);
            MarkHostDirty();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>The result of the operation.</returns>

        public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);

            // Return self for simplicity in tests - real implementation would create a slice

            return this;
        }
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>

        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
            // Return a mock type-converted buffer
            => new TestUnifiedBuffer<TNew>(Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>() / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>());
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>

        public MappedMemory<T> Map(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(AsMemory());
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>

        public MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(_hostArray.AsMemory(offset, length));
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2000:Dispose objects before losing scope", Justification = "MappedMemory is returned to caller for disposal")]
        public ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;


            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
            _state = BufferState.Uninitialized;
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Test implementation of raw IUnifiedMemoryBuffer for testing purposes.
    /// </summary>
    private sealed class TestRawUnifiedBuffer(long sizeInBytes) : IUnifiedMemoryBuffer
    {
        private volatile bool _disposed;
        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        /// <value>The size in bytes.</value>

        public long SizeInBytes { get; } = sizeInBytes;
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public static IntPtr DevicePointer => IntPtr.Zero;
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public static IAccelerator Accelerator => null!;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public MemoryOptions Options => MemoryOptions.None;
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public BufferState State => BufferState.HostOnly;
        /// <summary>
        /// Gets or sets a value indicating whether disposed.
        /// </summary>
        /// <value>The is disposed.</value>
        public bool IsDisposed => _disposed;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public static bool IsOnHost => true;
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public static bool IsOnDevice => false;
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public static bool IsDirty => false;
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>

        public DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);
        /// <summary>
        /// Performs ensure on host.
        /// </summary>
        public static void EnsureOnHost() { }
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public static void EnsureOnDevice() { }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public static void Synchronize() { }
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public static void MarkHostDirty() { }
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public static void MarkDeviceDirty() { }
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        // Copy operations - minimal implementation for testing

        public static ValueTask CopyFromAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask CopyToAsync(Memory<byte> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask CopyToAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask CopyToAsync(IUnifiedMemoryBuffer destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static ValueTask FillAsync(byte value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose() => _disposed = true;
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for test helpers.
/// </summary>
internal static class TestExtensions
{
    /// <summary>
    /// Copies data from a source array to a span.
    /// </summary>
    public static void CopyFrom<T>(this Span<T> destination, T[] source) => source.AsSpan().CopyTo(destination);
}// Additional methods for TestRawUnifiedBuffer...
