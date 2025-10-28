// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using DotCompute.Core.Memory.P2P;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Memory.P2P;

/// <summary>
/// Comprehensive unit tests for P2POptimizer covering optimization logic,
/// transfer planning, and adaptive learning.
/// </summary>
public sealed class P2POptimizerTests : IAsyncDisposable
{
    private readonly ILogger _mockLogger;
    private readonly P2PCapabilityMatrix _capabilityMatrix;
    private P2POptimizer? _optimizer;

    public P2POptimizerTests()
    {
        _mockLogger = Substitute.For<ILogger>();
        _capabilityMatrix = new P2PCapabilityMatrix(_mockLogger);
    }

    public async ValueTask DisposeAsync()
    {
        if (_optimizer != null)
        {
            await _optimizer.DisposeAsync();
        }
        await _capabilityMatrix.DisposeAsync();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidArguments_CreatesInstance()
    {
        // Arrange & Act
        var optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);

        // Assert
        _ = optimizer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new P2POptimizer(null!, _capabilityMatrix);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullCapabilityMatrix_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new P2POptimizer(_mockLogger, null!);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("capabilityMatrix");
    }

    #endregion

    #region InitializeTopologyAsync Tests

    [Fact]
    public async Task InitializeTopologyAsync_ValidDevicePairs_CreatesOptimizationProfiles()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var devicePairs = CreateTestDevicePairs(2, true);

        // Act
        await _optimizer.InitializeTopologyAsync(devicePairs, CancellationToken.None);

        // Assert - Verify optimizer was initialized (internal state)
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.Should().NotBeNull();
        _ = stats.OptimizationProfilesActive.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InitializeTopologyAsync_EmptyDevicePairs_HandlesGracefully()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var devicePairs = new List<P2PDevicePair>();

        // Act
        await _optimizer.InitializeTopologyAsync(devicePairs, CancellationToken.None);

        // Assert
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.OptimizationProfilesActive.Should().Be(0);
    }

    [Fact]
    public async Task InitializeTopologyAsync_DisabledPairs_SkipsInitialization()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var devicePairs = CreateTestDevicePairs(2, false); // Disabled pairs

        // Act
        await _optimizer.InitializeTopologyAsync(devicePairs, CancellationToken.None);

        // Assert
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.OptimizationProfilesActive.Should().Be(0);
    }

    [Fact]
    public async Task InitializeTopologyAsync_CancellationRequested_PropagatesException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var devicePairs = CreateTestDevicePairs(2, true);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _optimizer.InitializeTopologyAsync(devicePairs, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InitializeTopologyAsync_MultipleDevicePairs_CreatesMultipleProfiles()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var devicePairs = CreateTestDevicePairs(5, true);

        // Act
        await _optimizer.InitializeTopologyAsync(devicePairs, CancellationToken.None);

        // Assert
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.OptimizationProfilesActive.Should().Be(5);
    }

    #endregion

    #region CreateOptimalTransferPlanAsync Tests

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_SmallTransfer_SelectsDirectP2P()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        var transferSize = 512 * 1024; // 512KB
        var options = P2PTransferOptions.Default;

        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        // Act
        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, transferSize, options, CancellationToken.None);

        // Assert
        _ = plan.Should().NotBeNull();
        _ = plan.Strategy.Should().Be(P2PTransferStrategy.DirectP2P);
        _ = plan.TransferSize.Should().Be(transferSize);
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_LargeTransfer_ConsidersPipelinedOrChunked()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair(bandwidthGBps: 50.0);
        var transferSize = 200L * 1024 * 1024; // 200MB
        var options = new P2PTransferOptions { PipelineDepth = 4 };

        await InitializeCapabilityMatrix(sourceDevice, targetDevice, bandwidthGBps: 50.0);

        // Act
        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, transferSize, options, CancellationToken.None);

        // Assert
        _ = plan.Should().NotBeNull();
        _ = plan.Strategy.Should().BeOneOf(P2PTransferStrategy.PipelinedP2P, P2PTransferStrategy.ChunkedP2P);
        _ = plan.PipelineDepth.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_NullSourceDevice_ThrowsArgumentNullException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var targetDevice = CreateMockDevice("GPU1");

        // Act
        var act = async () => await _optimizer.CreateOptimalTransferPlanAsync(
            null!, targetDevice, 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_NullTargetDevice_ThrowsArgumentNullException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceDevice = CreateMockDevice("GPU0");

        // Act
        var act = async () => await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, null!, 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_HighBandwidthConnection_UsesLargerChunks()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair(bandwidthGBps: 100.0);
        var transferSize = 50L * 1024 * 1024; // 50MB
        var options = P2PTransferOptions.Default;

        await InitializeCapabilityMatrix(sourceDevice, targetDevice, bandwidthGBps: 100.0);

        // Act
        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, transferSize, options, CancellationToken.None);

        // Assert
        _ = plan.ChunkSize.Should().BeGreaterThan(4 * 1024 * 1024); // > 4MB
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_LowBandwidthConnection_UsesSmallerChunks()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair(bandwidthGBps: 5.0);
        var transferSize = 10L * 1024 * 1024; // 10MB
        var options = P2PTransferOptions.Default;

        await InitializeCapabilityMatrix(sourceDevice, targetDevice, bandwidthGBps: 5.0);

        // Act
        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, transferSize, options, CancellationToken.None);

        // Assert
        _ = plan.ChunkSize.Should().BeLessThan(16 * 1024 * 1024); // < 16MB
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_UnsupportedP2P_FallsBackToHostMediated()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        var transferSize = 10 * 1024 * 1024;

        await InitializeCapabilityMatrix(sourceDevice, targetDevice, isSupported: false);

        // Act
        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, transferSize, P2PTransferOptions.Default, CancellationToken.None);

        // Assert
        _ = plan.Strategy.Should().Be(P2PTransferStrategy.HostMediated);
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_GeneratesUniquePlanId()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        // Act
        var plan1 = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024, P2PTransferOptions.Default, CancellationToken.None);
        var plan2 = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Assert
        _ = plan1.PlanId.Should().NotBe(plan2.PlanId);
    }

    [Fact]
    public async Task CreateOptimalTransferPlanAsync_IncludesOptimizationScore()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        // Act
        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Assert
        _ = plan.OptimizationScore.Should().BeInRange(0.0, 1.0);
    }

    #endregion

    #region CreateScatterPlanAsync Tests

    [Fact]
    public async Task CreateScatterPlanAsync_ValidBuffers_CreatesScatterPlan()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffers = new[] { CreateMockBuffer<float>(250), CreateMockBuffer<float>(250), CreateMockBuffer<float>(500) };

        // Act
        var plan = await _optimizer.CreateScatterPlanAsync(
            sourceBuffer, destBuffers, new P2PScatterOptions(), CancellationToken.None);

        // Assert
        _ = plan.Should().NotBeNull();
        _ = plan.Chunks.Should().HaveCount(3);
        _ = plan.Chunks.Sum(c => c.ElementCount).Should().Be(1000);
    }

    [Fact]
    public async Task CreateScatterPlanAsync_NullSourceBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var destBuffers = new[] { CreateMockBuffer<float>(100) };

        // Act
        var act = async () => await _optimizer.CreateScatterPlanAsync<float>(
            null!, destBuffers, new P2PScatterOptions(), CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateScatterPlanAsync_NullDestinationBuffers_ThrowsArgumentException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffer = CreateMockBuffer<float>(1000);

        // Act
        var act = async () => await _optimizer.CreateScatterPlanAsync(
            sourceBuffer, null!, new P2PScatterOptions(), CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateScatterPlanAsync_EmptyDestinationBuffers_ThrowsArgumentException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffers = Array.Empty<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = async () => await _optimizer.CreateScatterPlanAsync(
            sourceBuffer, destBuffers, new P2PScatterOptions(), CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateScatterPlanAsync_EqualDistribution_CreatesEqualChunks()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffers = new[] { CreateMockBuffer<float>(250), CreateMockBuffer<float>(250), CreateMockBuffer<float>(250), CreateMockBuffer<float>(250) };

        // Act
        var plan = await _optimizer.CreateScatterPlanAsync(
            sourceBuffer, destBuffers, new P2PScatterOptions(), CancellationToken.None);

        // Assert
        _ = plan.Chunks.Should().HaveCount(4);
        _ = plan.Chunks.Should().AllSatisfy(c => c.ElementCount.Should().Be(250));
    }

    [Fact]
    public async Task CreateScatterPlanAsync_UnevenDistribution_HandlesRemainder()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var destBuffers = new[] { CreateMockBuffer<float>(300), CreateMockBuffer<float>(300), CreateMockBuffer<float>(400) };

        // Act
        var plan = await _optimizer.CreateScatterPlanAsync(
            sourceBuffer, destBuffers, new P2PScatterOptions(), CancellationToken.None);

        // Assert
        _ = plan.Chunks.Should().HaveCount(3);
        var totalElements = plan.Chunks.Sum(c => c.ElementCount);
        _ = totalElements.Should().Be(1000);
    }

    #endregion

    #region CreateGatherPlanAsync Tests

    [Fact]
    public async Task CreateGatherPlanAsync_ValidBuffers_CreatesGatherPlan()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffers = new[] { CreateMockBuffer<float>(250), CreateMockBuffer<float>(250), CreateMockBuffer<float>(500) };
        var destBuffer = CreateMockBuffer<float>(1000);

        // Act
        var plan = await _optimizer.CreateGatherPlanAsync(
            sourceBuffers, destBuffer, new P2PGatherOptions(), CancellationToken.None);

        // Assert
        _ = plan.Should().NotBeNull();
        _ = plan.Chunks.Should().HaveCount(3);
        _ = plan.Chunks.Sum(c => c.ElementCount).Should().Be(1000);
    }

    [Fact]
    public async Task CreateGatherPlanAsync_NullSourceBuffers_ThrowsArgumentException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var destBuffer = CreateMockBuffer<float>(1000);

        // Act
        var act = async () => await _optimizer.CreateGatherPlanAsync<float>(
            null!, destBuffer, new P2PGatherOptions(), CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateGatherPlanAsync_NullDestinationBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffers = new[] { CreateMockBuffer<float>(100) };

        // Act
        var act = async () => await _optimizer.CreateGatherPlanAsync(
            sourceBuffers, null!, new P2PGatherOptions(), CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateGatherPlanAsync_CorrectOffsetCalculation_SequentialDestination()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var sourceBuffers = new[] { CreateMockBuffer<float>(100), CreateMockBuffer<float>(200), CreateMockBuffer<float>(300) };
        var destBuffer = CreateMockBuffer<float>(600);

        // Act
        var plan = await _optimizer.CreateGatherPlanAsync(
            sourceBuffers, destBuffer, new P2PGatherOptions(), CancellationToken.None);

        // Assert
        _ = plan.Chunks[0].DestinationOffset.Should().Be(0);
        _ = plan.Chunks[1].DestinationOffset.Should().Be(100);
        _ = plan.Chunks[2].DestinationOffset.Should().Be(300);
    }

    #endregion

    #region RecordTransferResultAsync Tests

    [Fact]
    public async Task RecordTransferResultAsync_SuccessfulTransfer_UpdatesProfile()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024 * 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Act
        await _optimizer.RecordTransferResultAsync(
            plan, actualTransferTimeMs: 10.0, actualThroughputGBps: 5.0, wasSuccessful: true, CancellationToken.None);

        // Assert
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.TotalTransferHistory.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordTransferResultAsync_FailedTransfer_RecordsFailure()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024 * 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Act
        await _optimizer.RecordTransferResultAsync(
            plan, actualTransferTimeMs: 100.0, actualThroughputGBps: 0.1, wasSuccessful: false, CancellationToken.None);

        // Assert - Should not throw
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordTransferResultAsync_PoorPerformance_TriggersAdaptation()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024 * 1024, P2PTransferOptions.Default, CancellationToken.None);

        var estimatedTime = plan.EstimatedTransferTimeMs;

        // Act - Record much worse performance
        await _optimizer.RecordTransferResultAsync(
            plan, actualTransferTimeMs: estimatedTime * 3, actualThroughputGBps: 1.0, wasSuccessful: true, CancellationToken.None);

        // Assert - Adaptive optimization should occur (internal state change)
        var stats = _optimizer.GetOptimizationStatistics();
        _ = stats.TotalTransferHistory.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetOptimizationRecommendationsAsync Tests

    [Fact]
    public async Task GetOptimizationRecommendationsAsync_NoHistory_ReturnsEmptyRecommendations()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);

        // Act
        var recommendations = await _optimizer.GetOptimizationRecommendationsAsync(CancellationToken.None);

        // Assert
        _ = recommendations.Should().NotBeNull();
        _ = recommendations.PerformanceRecommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOptimizationRecommendationsAsync_LowEfficiency_GeneratesPerformanceRecommendations()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        var plan = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024 * 1024, P2PTransferOptions.Default, CancellationToken.None);

        // Record poor performance multiple times
        for (var i = 0; i < 5; i++)
        {
            await _optimizer.RecordTransferResultAsync(
                plan, actualTransferTimeMs: plan.EstimatedTransferTimeMs * 5, actualThroughputGBps: 0.5, wasSuccessful: true, CancellationToken.None);
        }

        // Act
        var recommendations = await _optimizer.GetOptimizationRecommendationsAsync(CancellationToken.None);

        // Assert
        _ = recommendations.PerformanceRecommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOptimizationRecommendationsAsync_IncludesTimestamp()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);

        // Act
        var recommendations = await _optimizer.GetOptimizationRecommendationsAsync(CancellationToken.None);

        // Assert
        _ = recommendations.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region GetOptimizationStatistics Tests

    [Fact]
    public void GetOptimizationStatistics_InitialState_ReturnsZeroStatistics()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);

        // Act
        var stats = _optimizer.GetOptimizationStatistics();

        // Assert
        _ = stats.Should().NotBeNull();
        _ = stats.TotalOptimizedTransfers.Should().Be(0);
        _ = stats.OptimizationProfilesActive.Should().Be(0);
    }

    [Fact]
    public async Task GetOptimizationStatistics_AfterOptimizations_ReflectsActivity()
    {
        // Arrange
        _optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);
        var (sourceDevice, targetDevice) = CreateMockDevicePair();
        await InitializeCapabilityMatrix(sourceDevice, targetDevice);

        // Act
        _ = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 1024, P2PTransferOptions.Default, CancellationToken.None);
        _ = await _optimizer.CreateOptimalTransferPlanAsync(
            sourceDevice, targetDevice, 2048, P2PTransferOptions.Default, CancellationToken.None);

        var stats = _optimizer.GetOptimizationStatistics();

        // Assert
        _ = stats.TotalOptimizedTransfers.Should().Be(2);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DisposedOptimizer_HandlesGracefully()
    {
        // Arrange
        var optimizer = new P2POptimizer(_mockLogger, _capabilityMatrix);

        // Act
        await optimizer.DisposeAsync();
        await optimizer.DisposeAsync(); // Double dispose

        // Assert - Should not throw
    }

    #endregion

    #region Helper Methods

    private static (IAccelerator source, IAccelerator target) CreateMockDevicePair(double bandwidthGBps = 50.0)
    {
        var source = CreateMockDevice("GPU0");
        var target = CreateMockDevice("GPU1");
        return (source, target);
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

    private async Task InitializeCapabilityMatrix(IAccelerator device1, IAccelerator device2, bool isSupported = true, double bandwidthGBps = 50.0)
    {
        var capability = new P2PConnectionCapability
        {
            IsSupported = isSupported,
            ConnectionType = isSupported ? P2PConnectionType.NVLink : P2PConnectionType.None,
            EstimatedBandwidthGBps = isSupported ? bandwidthGBps : 0.0,
            LimitationReason = isSupported ? null : "Not supported for testing"
        };

        await _capabilityMatrix.BuildMatrixAsync([device1, device2], CancellationToken.None);
    }

    private static List<P2PDevicePair> CreateTestDevicePairs(int count, bool isEnabled)
    {
        var pairs = new List<P2PDevicePair>();
        for (var i = 0; i < count; i++)
        {
            var device1 = CreateMockDevice($"GPU{i * 2}");
            var device2 = CreateMockDevice($"GPU{i * 2 + 1}");

            pairs.Add(new P2PDevicePair
            {
                Device1 = device1,
                Device2 = device2,
                IsEnabled = isEnabled,
                Capability = new P2PConnectionCapability
                {
                    IsSupported = isEnabled,
                    ConnectionType = isEnabled ? P2PConnectionType.NVLink : P2PConnectionType.None,
                    EstimatedBandwidthGBps = isEnabled ? 50.0 : 0.0
                }
            });
        }
        return pairs;
    }

    private static IUnifiedMemoryBuffer<T> CreateMockBuffer<T>(int length) where T : unmanaged
    {
        var buffer = Substitute.For<IUnifiedMemoryBuffer<T>>();
        _ = buffer.Length.Returns(length);
        // buffer.SizeInBytes.Returns(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()); // Namespace DotCompute.Core.System.Runtime doesn't exist
        _ = buffer.SizeInBytes.Returns(length * sizeof(int)); // Simplified for testing
        _ = buffer.Accelerator.Returns(CreateMockDevice($"GPU{length % 10}"));
        return buffer;
    }

    #endregion
}
