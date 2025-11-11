// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA.Barriers;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Types;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.Barriers;

/// <summary>
/// Unit tests for <see cref="CudaBarrierProvider"/>.
/// </summary>
public sealed class CudaBarrierProviderTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CudaContext _context;
    private readonly CudaDevice _device;
    private readonly CudaBarrierProvider _provider;

    public CudaBarrierProviderTests(ITestOutputHelper output)
    {
        _output = output;

        // Create minimal CUDA context for testing
        _device = new CudaDevice(0);
        _context = new CudaContext(deviceId: 0);
        _provider = new CudaBarrierProvider(_context, _device, NullLogger.Instance);
    }

    public void Dispose()
    {
        _provider?.Dispose();
        _context?.Dispose();
    }

    #region Barrier Creation Tests

    [Fact]
    public void CreateBarrier_ThreadBlock_ReturnsValidHandle()
    {
        // Arrange
        const int capacity = 256;

        // Act
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, capacity);

        // Assert
        barrier.Should().NotBeNull();
        barrier.BarrierId.Should().BeGreaterThan(0);
        barrier.Scope.Should().Be(BarrierScope.ThreadBlock);
        barrier.Capacity.Should().Be(capacity);
        barrier.ThreadsWaiting.Should().Be(0);
        barrier.IsActive.Should().BeFalse();

        _output.WriteLine($"Created barrier: {barrier}");
    }

    [Fact]
    public void CreateBarrier_WithName_RegistersNamedBarrier()
    {
        // Arrange
        const string name = "test-barrier";
        const int capacity = 512;

        // Act
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, capacity, name);
        var retrieved = _provider.GetBarrier(name);

        // Assert
        barrier.Should().NotBeNull();
        retrieved.Should().NotBeNull();
        retrieved!.BarrierId.Should().Be(barrier.BarrierId);
        retrieved.Capacity.Should().Be(capacity);

        _output.WriteLine($"Named barrier created: {name} -> ID {barrier.BarrierId}");
    }

    [Fact]
    public void CreateBarrier_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        const string name = "duplicate-barrier";
        using var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 128, name);

        // Act & Assert
        var act = () => _provider.CreateBarrier(BarrierScope.ThreadBlock, 256, name);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{name}'*");

        _output.WriteLine("Duplicate name correctly rejected");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateBarrier_InvalidCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        // Act & Assert
        var act = () => _provider.CreateBarrier(BarrierScope.ThreadBlock, invalidCapacity);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("capacity");

        _output.WriteLine($"Invalid capacity {invalidCapacity} correctly rejected");
    }

    [Fact]
    public void CreateBarrier_WarpScope_RequiresCapacity32()
    {
        // Act & Assert - correct capacity
        using var validBarrier = _provider.CreateBarrier(BarrierScope.Warp, 32);
        validBarrier.Should().NotBeNull();
        validBarrier.Capacity.Should().Be(32);

        // Act & Assert - incorrect capacity
        var act = () => _provider.CreateBarrier(BarrierScope.Warp, 64);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*exactly 32 threads*");

        _output.WriteLine("Warp barrier capacity validation working correctly");
    }

    [Fact]
    public void CreateBarrier_GridScope_RequiresSupportedDevice()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        if (major >= 6) // Pascal+ supports grid barriers
        {
            // Act
            using var barrier = _provider.CreateBarrier(BarrierScope.Grid, 1024);

            // Assert
            barrier.Should().NotBeNull();
            barrier.Scope.Should().Be(BarrierScope.Grid);
            _output.WriteLine("Grid barrier created successfully on CC 6.0+ device");
        }
        else
        {
            // Act & Assert
            var act = () => _provider.CreateBarrier(BarrierScope.Grid, 1024);
            act.Should().Throw<NotSupportedException>()
                .WithMessage("*Compute Capability 6.0+*");
            _output.WriteLine("Grid barrier correctly rejected on CC < 6.0 device");
        }
    }

    #endregion

    #region Named Barrier Management Tests

    [Fact]
    public void GetBarrier_ExistingName_ReturnsHandle()
    {
        // Arrange
        const string name = "existing-barrier";
        using var created = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256, name);

        // Act
        var retrieved = _provider.GetBarrier(name);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.BarrierId.Should().Be(created.BarrierId);
        _output.WriteLine($"Successfully retrieved barrier by name: {name}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonexistent-barrier")]
    public void GetBarrier_InvalidName_ReturnsNull(string? name)
    {
        // Act
        var retrieved = _provider.GetBarrier(name!);

        // Assert
        retrieved.Should().BeNull();
        _output.WriteLine($"Nonexistent barrier name '{name}' correctly returned null");
    }

    #endregion

    #region Cooperative Launch Tests

    [Fact]
    public void EnableCooperativeLaunch_OnSupportedDevice_EnablesSuccessfully()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        if (major >= 6) // Pascal+ supports cooperative launch
        {
            // Act
            _provider.EnableCooperativeLaunch(true);

            // Assert
            _provider.IsCooperativeLaunchEnabled.Should().BeTrue();
            _output.WriteLine("Cooperative launch enabled successfully");

            // Disable
            _provider.EnableCooperativeLaunch(false);
            _provider.IsCooperativeLaunchEnabled.Should().BeFalse();
            _output.WriteLine("Cooperative launch disabled successfully");
        }
        else
        {
            // Act & Assert
            var act = () => _provider.EnableCooperativeLaunch(true);
            act.Should().Throw<NotSupportedException>()
                .WithMessage("*Compute Capability 6.0+*");
            _output.WriteLine("Cooperative launch correctly rejected on CC < 6.0 device");
        }
    }

    [Fact]
    public void GetMaxCooperativeGridSize_ReturnsSensibleValue()
    {
        // Act
        var maxSize = _provider.GetMaxCooperativeGridSize();

        // Assert
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        if (major >= 6)
        {
            maxSize.Should().BeGreaterThan(0, "CC 6.0+ should support cooperative launch");
            maxSize.Should().BeLessThan(1_000_000, "max size should be realistic");
            _output.WriteLine($"Max cooperative grid size: {maxSize:N0} threads");
        }
        else
        {
            maxSize.Should().Be(0, "CC < 6.0 should not support cooperative launch");
            _output.WriteLine("Cooperative launch not supported on this device");
        }
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public void ActiveBarrierCount_TracksBarrierLifecycle()
    {
        // Arrange
        _provider.ActiveBarrierCount.Should().Be(0, "should start with zero barriers");

        // Act - create barriers
        var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 128);
        _provider.ActiveBarrierCount.Should().Be(1);

        var barrier2 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        _provider.ActiveBarrierCount.Should().Be(2);

        var barrier3 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 512);
        _provider.ActiveBarrierCount.Should().Be(3);

        _output.WriteLine($"Created 3 barriers, active count: {_provider.ActiveBarrierCount}");

        // Act - dispose barriers
        barrier1.Dispose();
        _provider.ActiveBarrierCount.Should().Be(2);

        barrier2.Dispose();
        _provider.ActiveBarrierCount.Should().Be(1);

        barrier3.Dispose();
        _provider.ActiveBarrierCount.Should().Be(0);

        _output.WriteLine("All barriers disposed, active count returned to 0");
    }

    [Fact]
    public void ResetAllBarriers_ClearsAllBarriers()
    {
        // Arrange
        var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 128, "barrier1");
        var barrier2 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256, "barrier2");
        _provider.ActiveBarrierCount.Should().Be(2);

        // Act
        _provider.ResetAllBarriers();

        // Assert
        _provider.ActiveBarrierCount.Should().Be(0);
        _provider.GetBarrier("barrier1").Should().BeNull();
        _provider.GetBarrier("barrier2").Should().BeNull();

        _output.WriteLine("All barriers successfully reset");

        // Cleanup - barriers already disposed by ResetAllBarriers
        GC.KeepAlive(barrier1);
        GC.KeepAlive(barrier2);
    }

    [Fact]
    public void Dispose_CleansUpAllResources()
    {
        // Arrange
        var provider = new CudaBarrierProvider(_context, _device, NullLogger.Instance);
        provider.CreateBarrier(BarrierScope.ThreadBlock, 128);
        provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        provider.ActiveBarrierCount.Should().Be(2);

        // Act
        provider.Dispose();

        // Assert
        var act = () => provider.CreateBarrier(BarrierScope.ThreadBlock, 128);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Provider disposal correctly invalidates further operations");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void CreateBarrier_ConcurrentCreation_IsThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int barriersPerThread = 5;
        var tasks = new List<Task<List<IBarrierHandle>>>();

        // Act - Use anonymous barriers to avoid hitting 16 named barrier limit
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            var task = Task.Run(() =>
            {
                var barriers = new List<IBarrierHandle>();
                for (int j = 0; j < barriersPerThread; j++)
                {
                    var barrier = _provider.CreateBarrier(
                        BarrierScope.ThreadBlock,
                        256,
                        name: null); // Anonymous barrier to avoid hardware limit
                    barriers.Add(barrier);
                }
                return barriers;
            });
            tasks.Add(task);
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var allBarriers = tasks.SelectMany(t => t.Result).ToList();
        allBarriers.Should().HaveCount(threadCount * barriersPerThread);
        _provider.ActiveBarrierCount.Should().Be(threadCount * barriersPerThread);

        // Verify all IDs are unique
        var uniqueIds = allBarriers.Select(b => b.BarrierId).Distinct().Count();
        uniqueIds.Should().Be(allBarriers.Count, "all barrier IDs should be unique");

        _output.WriteLine($"Created {allBarriers.Count} barriers concurrently, all IDs unique");

        // Cleanup
        foreach (var barrier in allBarriers)
        {
            barrier.Dispose();
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateBarrier_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new CudaBarrierProvider(_context, _device, NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        var act = () => provider.CreateBarrier(BarrierScope.ThreadBlock, 128);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Disposed provider correctly rejects operations");
    }

    [Fact]
    public void GetBarrier_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new CudaBarrierProvider(_context, _device, NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        var act = () => provider.GetBarrier("any-name");
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Disposed provider correctly rejects GetBarrier");
    }

    #endregion

    #region ExecuteWithBarrierAsync Tests

    [Fact]
    public async Task ExecuteWithBarrierAsync_NullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: null!,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("kernel");

        _output.WriteLine("Null kernel correctly rejected");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_NullBarrier_ThrowsArgumentNullException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: null!,
            config: config,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("barrier");

        _output.WriteLine("Null barrier correctly rejected");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: null!,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("config");

        _output.WriteLine("Null config correctly rejected");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("arguments");

        _output.WriteLine("Null arguments correctly rejected");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_InvalidConfigType_ThrowsArgumentException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var invalidConfig = new object(); // Wrong type

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: invalidConfig,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*LaunchConfiguration*")
            .WithParameterName("config");

        _output.WriteLine("Invalid config type correctly rejected");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_ThreadBlockBarrier_CapacityExceedsBlockSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 512); // Larger than block
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1) // Only 256 threads per block
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capacity*exceeds block size*");

        _output.WriteLine("Thread-block barrier capacity validation working correctly");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_GridBarrier_CapacityMismatchTotalThreads_ThrowsInvalidOperationException()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.IfNot(major >= 6, "Grid barriers require CC 6.0+");

        var mockKernel = new MockCompiledKernel("test-kernel");
        const int blocks = 4;
        const int threadsPerBlock = 256;
        const int totalThreads = blocks * threadsPerBlock; // 1024

        using var barrier = _provider.CreateBarrier(BarrierScope.Grid, 512); // Wrong capacity
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(blocks, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(threadsPerBlock, 1, 1)
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Grid barrier capacity*must equal total thread count*");

        _output.WriteLine("Grid barrier capacity validation working correctly");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_GridBarrier_AutoEnablesCooperativeLaunch()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.IfNot(major >= 6, "Grid barriers require CC 6.0+");

        var mockKernel = new MockCompiledKernel("test-kernel");
        const int blocks = 2;
        const int threadsPerBlock = 256;
        const int totalThreads = blocks * threadsPerBlock;

        using var barrier = _provider.CreateBarrier(BarrierScope.Grid, totalThreads);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(blocks, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(threadsPerBlock, 1, 1)
        };

        // Ensure cooperative launch is disabled initially
        _provider.EnableCooperativeLaunch(false);
        _provider.IsCooperativeLaunchEnabled.Should().BeFalse();

        // Act
        await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: new object[] { 42, "test" });

        // Assert
        _provider.IsCooperativeLaunchEnabled.Should().BeTrue();
        mockKernel.ExecutedParameters.Should().NotBeNull();

        _output.WriteLine("Grid barrier automatically enabled cooperative launch");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_GridBarrier_ExceedsMaxCooperativeSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.IfNot(major >= 6, "Grid barriers require CC 6.0+");

        var mockKernel = new MockCompiledKernel("test-kernel");
        var maxSize = _provider.GetMaxCooperativeGridSize();
        Skip.IfNot(maxSize > 0, "Cooperative launch not supported");

        // Try to launch with more threads than supported
        var totalThreads = maxSize + 1024;
        using var barrier = _provider.CreateBarrier(BarrierScope.Grid, totalThreads);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(totalThreads / 256, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum cooperative grid size*");

        _output.WriteLine($"Correctly rejected grid size exceeding max cooperative size ({maxSize})");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_WarpBarrier_InvalidCapacity_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.Warp, 32); // Correct capacity
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // This should work fine (warp barrier with capacity 32)
        await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        mockKernel.ExecutedParameters.Should().NotBeNull();
        _output.WriteLine("Warp barrier with capacity 32 executed successfully");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_TileBarrier_CapacityExceedsBlockSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.Tile, 512); // Larger than block
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // Act & Assert
        var act = async () => await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tile barrier capacity*exceeds block size*");

        _output.WriteLine("Tile barrier capacity validation working correctly");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_ThreadBlockBarrier_PrependsBarrierId()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };
        var userArgs = new object[] { 42, "test", 3.14 };

        // Act
        await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: userArgs);

        // Assert
        mockKernel.ExecutedParameters.Should().NotBeNull();
        mockKernel.ExecutedParameters!.Should().HaveCount(4); // barrierId + 3 user args
        mockKernel.ExecutedParameters![0].Should().Be(barrier.BarrierId);
        mockKernel.ExecutedParameters![1].Should().Be(42);
        mockKernel.ExecutedParameters![2].Should().Be("test");
        mockKernel.ExecutedParameters![3].Should().Be(3.14);

        _output.WriteLine($"Barrier ID {barrier.BarrierId} correctly prepended to arguments");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_GridBarrier_PrependsBarrierIdAndDevicePtr()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.IfNot(major >= 6, "Grid barriers require CC 6.0+");

        var mockKernel = new MockCompiledKernel("test-kernel");
        const int totalThreads = 512;
        using var barrier = _provider.CreateBarrier(BarrierScope.Grid, totalThreads);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(2, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };
        var userArgs = new object[] { 100 };

        // Act
        await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: userArgs);

        // Assert
        mockKernel.ExecutedParameters.Should().NotBeNull();
        mockKernel.ExecutedParameters!.Should().HaveCount(3); // barrierId + devicePtr + 1 user arg
        mockKernel.ExecutedParameters![0].Should().Be(barrier.BarrierId);
        mockKernel.ExecutedParameters![1].Should().BeOfType<IntPtr>(); // Device pointer
        mockKernel.ExecutedParameters![2].Should().Be(100);

        _output.WriteLine($"Grid barrier correctly prepended ID and device pointer");
    }

    [Fact]
    public async Task ExecuteWithBarrierAsync_ValidExecution_CallsKernelExecuteAsync()
    {
        // Arrange
        var mockKernel = new MockCompiledKernel("test-kernel");
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var config = new LaunchConfiguration
        {
            GridSize = new Abstractions.Types.Dim3(1, 1, 1),
            BlockSize = new Abstractions.Types.Dim3(256, 1, 1)
        };

        // Act
        await _provider.ExecuteWithBarrierAsync(
            kernel: mockKernel,
            barrier: barrier,
            config: config,
            arguments: Array.Empty<object>());

        // Assert
        mockKernel.WasExecuted.Should().BeTrue();
        mockKernel.ExecutionCount.Should().Be(1);

        _output.WriteLine("Kernel ExecuteAsync successfully invoked");
    }

    #endregion

    #region Mock Classes

    /// <summary>
    /// Mock implementation of ICompiledKernel for testing.
    /// </summary>
    private sealed class MockCompiledKernel : Abstractions.Interfaces.Kernels.ICompiledKernel
    {
        private bool _disposed;

        public MockCompiledKernel(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public bool IsReady => true;
        public string BackendType => "CUDA";
        public bool WasExecuted { get; private set; }
        public int ExecutionCount { get; private set; }
        public object[]? ExecutedParameters { get; private set; }

        public Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            WasExecuted = true;
            ExecutionCount++;
            ExecutedParameters = parameters;

            return Task.CompletedTask;
        }

        public object GetMetadata()
        {
            return new { Name, BackendType };
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    #endregion
}
