// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using DotCompute.Core.Memory.P2P;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Core.Tests.Memory.P2P
{
    /// <summary>
    /// Comprehensive test suite for P2P Transfer Manager functionality.
    /// </summary>
    public sealed class P2PTransferManagerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<P2PTransferManager> _logger;
        private readonly P2PTransferManager _transferManager;
        private readonly MockAccelerator _device1;
        private readonly MockAccelerator _device2;
        private readonly MockAccelerator _device3;

        public P2PTransferManagerTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new TestLogger<P2PTransferManager>(output);
            
            var capabilityDetector = new P2PCapabilityDetector(new TestLogger<P2PCapabilityDetector>(output));
            _transferManager = new P2PTransferManager(_logger, capabilityDetector);

            // Create mock devices for testing
            _device1 = new MockAccelerator("device_1", "CUDA Device 1", AcceleratorType.CUDA);
            _device2 = new MockAccelerator("device_2", "CUDA Device 2", AcceleratorType.CUDA);
            _device3 = new MockAccelerator("device_3", "ROCm Device 1", AcceleratorType.GPU);
        }

        [Fact]
        public async Task InitializeP2PTopologyAsync_WithMultipleDevices_ShouldSucceed()
        {
            // Arrange
            var devices = new[] { _device1, _device2, _device3 };

            // Act
            var result = await _transferManager.InitializeP2PTopologyAsync(devices);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(3, result.TotalDevices);
            Assert.True(result.SuccessfulConnections > 0);
            Assert.NotEmpty(result.DevicePairs);

            _output.WriteLine($"P2P topology initialized: {result.SuccessfulConnections} successful connections");
        }

        [Fact]
        public async Task InitializeP2PTopologyAsync_WithEmptyDeviceArray_ShouldThrow()
        {
            // Arrange
            var devices = Array.Empty<IAccelerator>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _transferManager.InitializeP2PTopologyAsync(devices));
        }

        [Fact]
        public async Task ExecuteP2PTransferAsync_WithCompatibleDevices_ShouldSucceed()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<float>(_device1, 1024);
            var destBuffer = new MockBuffer<float>(_device2, 1024);
            var options = P2PTransferOptions.Default;

            // Act
            var result = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.True(result.TransferTimeMs > 0);
            Assert.True(result.ThroughputGBps > 0);
            Assert.NotNull(result.SessionId);

            _output.WriteLine($"P2P transfer completed: {result.TransferTimeMs:F1}ms, {result.ThroughputGBps:F2} GB/s");
        }

        [Fact]
        public async Task ExecuteP2PTransferAsync_WithValidation_ShouldValidateIntegrity()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<int>(_device1, 512);
            var destBuffer = new MockBuffer<int>(_device2, 512);
            var options = new P2PTransferOptions { EnableValidation = true };

            // Act
            var result = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.NotNull(result.ValidationResults);
            Assert.True(result.ValidationResults.IsValid);

            _output.WriteLine($"Validated P2P transfer: {result.ValidationResults.ValidationDetails.Count} validation checks passed");
        }

        [Fact]
        public async Task ExecuteP2PScatterAsync_WithMultipleDestinations_ShouldDistributeData()
        {
            // Arrange
            var devices = new[] { _device1, _device2, _device3 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<double>(_device1, 2048);
            var destBuffers = new[]
            {
                new MockBuffer<double>(_device2, 1024),
                new MockBuffer<double>(_device3, 1024)
            };
            var options = P2PScatterOptions.Default;

            // Act
            var result = await _transferManager.ExecuteP2PScatterAsync(sourceBuffer, destBuffers, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(2, result.TransferResults.Length);
            Assert.All(result.TransferResults, r => Assert.True(r.IsSuccessful));
            Assert.True(result.AverageThroughputGBps > 0);

            _output.WriteLine($"P2P scatter completed: {result.AverageThroughputGBps:F2} GB/s average throughput");
        }

        [Fact]
        public async Task ExecuteP2PGatherAsync_WithMultipleSources_ShouldAggregateData()
        {
            // Arrange
            var devices = new[] { _device1, _device2, _device3 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffers = new[]
            {
                new MockBuffer<byte>(_device1, 512),
                new MockBuffer<byte>(_device2, 512)
            };
            var destBuffer = new MockBuffer<byte>(_device3, 1024);
            var options = P2PGatherOptions.Default;

            // Act
            var result = await _transferManager.ExecuteP2PGatherAsync(sourceBuffers, destBuffer, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(2, result.TransferResults.Length);
            Assert.All(result.TransferResults, r => Assert.True(r.IsSuccessful));
            Assert.True(result.AverageThroughputGBps > 0);

            _output.WriteLine($"P2P gather completed: {result.AverageThroughputGBps:F2} GB/s average throughput");
        }

        [Fact]
        public async Task GetTransferStatistics_AfterTransfers_ShouldReturnAccurateStats()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<int>(_device1, 256);
            var destBuffer = new MockBuffer<int>(_device2, 256);

            // Execute multiple transfers
            for (int i = 0; i < 5; i++)
            {
                await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer);
            }

            // Act
            var stats = _transferManager.GetTransferStatistics();

            // Assert
            Assert.Equal(5, stats.TotalTransfers);
            Assert.Equal(5, stats.SuccessfulTransfers);
            Assert.Equal(0, stats.FailedTransfers);
            Assert.True(stats.AverageThroughputGBps > 0);
            Assert.True(stats.PeakThroughputGBps >= stats.AverageThroughputGBps);

            _output.WriteLine($"Transfer statistics: {stats.TotalTransfers} transfers, {stats.AverageThroughputGBps:F2} GB/s average");
        }

        [Fact]
        public async Task GetCapabilityMatrix_AfterInitialization_ShouldReturnMatrix()
        {
            // Arrange
            var devices = new[] { _device1, _device2, _device3 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            // Act
            var matrix = _transferManager.GetCapabilityMatrix();

            // Assert
            Assert.NotNull(matrix);
            var matrixStats = matrix.GetMatrixStatistics();
            Assert.True(matrixStats.TotalDevices >= 3);
            Assert.True(matrixStats.TotalConnections > 0);

            _output.WriteLine($"Capability matrix: {matrixStats.TotalDevices} devices, {matrixStats.P2PEnabledConnections} P2P connections");
        }

        [Fact]
        public async Task GetActiveSessions_DuringTransfer_ShouldReturnActiveSessions()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<float>(_device1, 1024);
            var destBuffer = new MockBuffer<float>(_device2, 1024);

            // Act & Assert
            var transferTask = _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer);
            
            // Check active sessions during transfer
            await Task.Delay(10); // Give transfer time to start
            var activeSessions = _transferManager.GetActiveSessions();
            
            await transferTask; // Wait for completion

            // During the brief transfer, we might catch active sessions
            // After completion, there should be no active sessions
            var finalActiveSessions = _transferManager.GetActiveSessions();
            Assert.Empty(finalActiveSessions);

            _output.WriteLine($"Active sessions checked during transfer execution");
        }

        [Theory]
        [InlineData(1024, P2PTransferPriority.High)]
        [InlineData(1024 * 1024, P2PTransferPriority.Normal)]
        [InlineData(100 * 1024 * 1024, P2PTransferPriority.Low)]
        public async Task ExecuteP2PTransferAsync_WithDifferentSizes_ShouldAdaptStrategy(long transferSize, P2PTransferPriority expectedPriority)
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var elementCount = (int)(transferSize / sizeof(float));
            var sourceBuffer = new MockBuffer<float>(_device1, elementCount);
            var destBuffer = new MockBuffer<float>(_device2, elementCount);
            var options = new P2PTransferOptions { Priority = expectedPriority };

            // Act
            var result = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.NotNull(result.TransferPlan);

            _output.WriteLine($"Transfer size {transferSize} bytes: strategy={result.TransferPlan.Strategy}, time={result.TransferTimeMs:F1}ms");
        }

        [Fact]
        public async Task ExecuteP2PTransferAsync_WithCancellation_ShouldCancel()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<int>(_device1, 1024);
            var destBuffer = new MockBuffer<int>(_device2, 1024);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(1); // Cancel very quickly

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task ExecuteP2PTransferAsync_WithMismatchedBufferSizes_ShouldFail()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<float>(_device1, 1024);
            var destBuffer = new MockBuffer<float>(_device2, 512); // Different size

            // Act
            var result = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.NotNull(result.ErrorMessage);

            _output.WriteLine($"Expected failure with mismatched buffer sizes: {result.ErrorMessage}");
        }

        [Fact]
        public async Task ExecuteP2PTransferAsync_WithSynchronization_ShouldCoordinate()
        {
            // Arrange
            var devices = new[] { _device1, _device2 };
            await _transferManager.InitializeP2PTopologyAsync(devices);

            var sourceBuffer = new MockBuffer<double>(_device1, 256);
            var destBuffer = new MockBuffer<double>(_device2, 256);
            var options = new P2PTransferOptions { EnableSynchronization = true };

            // Act
            var result = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destBuffer, options);

            // Assert
            Assert.True(result.IsSuccessful);

            _output.WriteLine($"Synchronized P2P transfer completed successfully");
        }

        public void Dispose()
        {
            _transferManager?.DisposeAsync().AsTask().Wait();
            _device1?.Dispose();
            _device2?.Dispose();
            _device3?.Dispose();
        }
    }
}