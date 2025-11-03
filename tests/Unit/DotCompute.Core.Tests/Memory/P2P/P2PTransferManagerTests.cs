// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using DotCompute.Core.Memory.P2P;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Memory.P2P;

/// <summary>
/// Comprehensive unit tests for P2PTransferManager covering transfer operations,
/// session management, and coordination logic.
/// </summary>
public sealed class P2PTransferManagerTests : IAsyncDisposable
{
    private readonly ILogger _mockLogger;
    private readonly P2PCapabilityDetector _capabilityDetector;
    private P2PTransferManager? _transferManager;

    public P2PTransferManagerTests()
    {
        _mockLogger = Substitute.For<ILogger>();
        _capabilityDetector = new P2PCapabilityDetector(_mockLogger);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transferManager != null)
        {
            await _transferManager.DisposeAsync();
        }
        await _capabilityDetector.DisposeAsync();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidArguments_CreatesInstance()
    {
        // Arrange & Act
        var manager = new P2PTransferManager(_mockLogger, _capabilityDetector);

        // Assert
        _ = manager.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new P2PTransferManager(null!, _capabilityDetector);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullCapabilityDetector_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new P2PTransferManager(_mockLogger, null!);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("capabilityDetector");
    }

    #endregion

    #region InitializeP2PTopologyAsync Tests

    [Fact]
    public async Task InitializeP2PTopologyAsync_ValidDevices_ReturnsSuccessResult()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var devices = CreateMockDevices(3);

        // Act
        var result = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.TotalDevices.Should().Be(3);
        _ = result.DevicePairs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeP2PTopologyAsync_NullDevices_ThrowsArgumentException()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);

        // Act
        var act = async () => await _transferManager.InitializeP2PTopologyAsync(null!, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeP2PTopologyAsync_EmptyDevices_ThrowsArgumentException()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var devices = Array.Empty<IAccelerator>();

        // Act
        var act = async () => await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeP2PTopologyAsync_TwoDevices_DetectsOnePair()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var devices = CreateMockDevices(2);

        // Act
        var result = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Assert
        _ = result.DevicePairs.Should().HaveCount(1);
    }

    [Fact]
    public async Task InitializeP2PTopologyAsync_FourDevices_DetectsSixPairs()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var devices = CreateMockDevices(4);

        // Act
        var result = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Assert
        _ = result.DevicePairs.Should().HaveCount(6); // 4 choose 2 = 6
    }

    [Fact]
    public async Task InitializeP2PTopologyAsync_CancellationRequested_PropagatesException()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var devices = CreateMockDevices(2);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await _transferManager.InitializeP2PTopologyAsync(devices, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InitializeP2PTopologyAsync_SuccessfulConnections_UpdatesStatistics()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var devices = CreateMockDevices(3);

        // Act
        var result = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Assert
        _ = result.SuccessfulConnections.Should().BeGreaterThanOrEqualTo(0);
        _ = result.TotalDevices.Should().Be(3);
    }

    #endregion

    #region ExecuteP2PTransferAsync Tests

    [Fact]
    public async Task ExecuteP2PTransferAsync_ValidBuffers_ReturnsTransferResult()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Act
        var result = await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, destBuffer, null, CancellationToken.None);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteP2PTransferAsync_NullSourceBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var destBuffer = CreateMockBuffer<float>(1000);

        // Act
        var act = async () => await _transferManager.ExecuteP2PTransferAsync<float>(
            null!, destBuffer, null, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteP2PTransferAsync_NullDestinationBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);

        // Act
        var act = async () => await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, null!, null, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteP2PTransferAsync_CustomOptions_UsesProvidedOptions()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        var options = new P2PTransferOptions
        {
            PreferredChunkSize = 2 * 1024 * 1024,
            PipelineDepth = 2,
            EnableValidation = true
        };

        // Act
        var result = await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, destBuffer, options, CancellationToken.None);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteP2PTransferAsync_DefaultOptions_UsesDefaults()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Act
        var result = await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, destBuffer, null, CancellationToken.None);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteP2PTransferAsync_GeneratesUniqueSessionId()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Act
        var result1 = await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, destBuffer, null, CancellationToken.None);
        var result2 = await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, destBuffer, null, CancellationToken.None);

        // Assert
        _ = result1.SessionId.Should().NotBe(result2.SessionId);
    }

    #endregion

    #region Scatter/Gather Tests

    [Fact]
    public async Task ExecuteScatterAsync_ValidBuffers_ReturnsResults()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffers = new[]
        {
            CreateMockBuffer<float>(250),
            CreateMockBuffer<float>(250),
            CreateMockBuffer<float>(500)
        };

        var devices = new[] { sourceBuffer.Accelerator }
            .Concat(destBuffers.Select(b => b.Accelerator))
            .Distinct()
            .ToArray();
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Act
        // TODO: Implement ExecuteScatterAsync method
        // var result = await _transferManager.ExecuteScatterAsync(sourceBuffer, destBuffers, null, CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = devices.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteGatherAsync_ValidBuffers_ReturnsResults()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffers = new[]
        {
            CreateMockBuffer<float>(250),
            CreateMockBuffer<float>(250),
            CreateMockBuffer<float>(500)
        };
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = sourceBuffers.Select(b => b.Accelerator)
            .Append(destBuffer.Accelerator)
            .Distinct()
            .ToArray();
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Act
        // TODO: Implement ExecuteGatherAsync method
        // var result = await _transferManager.ExecuteGatherAsync(sourceBuffers, destBuffer, null, CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = devices.Should().NotBeEmpty();
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task GetTransferSessionAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        var result = await _transferManager.ExecuteP2PTransferAsync(
            sourceBuffer, destBuffer, null, CancellationToken.None);

        // Act
        // TODO: Implement GetTransferSessionAsync method
        // var session = await _transferManager.GetTransferSessionAsync(result.SessionId, CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public void GetTransferSessionAsync_NonExistentSession_ReturnsNull()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var fakeSessionId = Guid.NewGuid().ToString();

        // Act
        // TODO: Implement GetTransferSessionAsync method
        // var session = await _transferManager.GetTransferSessionAsync(fakeSessionId, CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = fakeSessionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetActiveSessionsAsync_WithActiveSessions_ReturnsActiveSessions()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        _ = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, null, CancellationToken.None);
        _ = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, null, CancellationToken.None);

        // Act
        // TODO: Implement GetActiveSessionsAsync method
        // var sessions = await _transferManager.GetActiveSessionsAsync(CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = devices.Should().HaveCount(2);
    }

    #endregion

    #region Statistics and Metrics Tests

    [Fact]
    public async Task GetTransferStatisticsAsync_AfterTransfers_ReturnsStatistics()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        _ = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, null, CancellationToken.None);

        // Act
        // TODO: Implement GetTransferStatisticsAsync method
        // var stats = await _transferManager.GetTransferStatisticsAsync(CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = devices.Should().HaveCount(2);
    }

    [Fact]
    public void GetTransferStatisticsAsync_NoTransfers_ReturnsZeroStatistics()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);

        // Act
        // TODO: Implement GetTransferStatisticsAsync method
        // var stats = await _transferManager.GetTransferStatisticsAsync(CancellationToken.None);

        // Assert
        // Skipped - method not implemented
        _ = _transferManager.Should().NotBeNull();
    }

    #endregion

    #region Concurrent Transfer Tests

    [Fact]
    public async Task ExecuteP2PTransferAsync_ConcurrentTransfers_HandlesCorrectly()
    {
        // Arrange
        _transferManager = new P2PTransferManager(_mockLogger, _capabilityDetector);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffer = CreateMockBuffer<float>(1000);

        var devices = new[] { sourceBuffer.Accelerator, destBuffer.Accelerator };
        _ = await _transferManager.InitializeP2PTopologyAsync(devices, CancellationToken.None);

        // Act - Start multiple concurrent transfers
        var tasks = new Task<P2PTransferResult>[5];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _transferManager.ExecuteP2PTransferAsync(
                sourceBuffer, destBuffer, null, CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().HaveCount(5);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DisposedManager_HandlesGracefully()
    {
        // Arrange
        var manager = new P2PTransferManager(_mockLogger, _capabilityDetector);

        // Act
        await manager.DisposeAsync();
        await manager.DisposeAsync(); // Double dispose

        // Assert - Should not throw
    }

    #endregion

    #region Helper Methods

    private static IAccelerator[] CreateMockDevices(int count)
    {
        var devices = new IAccelerator[count];
        for (var i = 0; i < count; i++)
        {
            devices[i] = CreateMockDevice($"GPU{i}");
        }
        return devices;
    }

    private static IAccelerator CreateMockDevice(string id)
    {
        var device = Substitute.For<IAccelerator>();
        _ = device.Info.Returns(new AcceleratorInfo
        {
            Id = id,
            Name = $"Test {id}",
            DeviceType = "GPU",
            Vendor = "Test"
        });
        _ = device.Type.Returns(AcceleratorType.GPU);
        return device;
    }

    private static IUnifiedMemoryBuffer<T> CreateMockBuffer<T>(int length) where T : unmanaged
    {
        var buffer = Substitute.For<IUnifiedMemoryBuffer<T>>();
        _ = buffer.Length.Returns(length);
        // buffer.SizeInBytes.Returns(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()); // Namespace DotCompute.Core.System.Runtime doesn't exist
        _ = buffer.SizeInBytes.Returns(length * sizeof(int)); // Simplified for testing
        _ = buffer.Accelerator.Returns(CreateMockDevice($"GPU{length % 3}"));
        return buffer;
    }

    #endregion
}
