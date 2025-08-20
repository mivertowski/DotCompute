// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Memory.P2P;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Core.Tests.Memory.P2P
{
    /// <summary>
    /// Test suite for P2P Validator functionality.
    /// </summary>
    public sealed class P2PValidatorTests : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly P2PValidator _validator;
        private readonly MockAccelerator _sourceDevice;
        private readonly MockAccelerator _targetDevice;

        public P2PValidatorTests(ITestOutputHelper output)
        {
            _output = output;
            _validator = new P2PValidator(new TestLogger<P2PValidator>(output));
            _sourceDevice = new MockAccelerator("source", "Source Device", AcceleratorType.CUDA);
            _targetDevice = new MockAccelerator("target", "Target Device", AcceleratorType.CUDA);
        }

        [Fact]
        public async Task ValidateTransferReadinessAsync_WithCompatibleBuffers_ShouldPass()
        {
            // Arrange
            var sourceBuffer = new MockBuffer<float>(_sourceDevice, 1024);
            var destBuffer = new MockBuffer<float>(_targetDevice, 1024);
            var transferPlan = CreateMockTransferPlan(sourceBuffer, destBuffer, P2PTransferStrategy.DirectP2P);

            // Act
            var result = await _validator.ValidateTransferReadinessAsync(sourceBuffer, destBuffer, transferPlan);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.ValidationDetails);
            Assert.All(result.ValidationDetails, detail => Assert.True(detail.IsValid));

            _output.WriteLine($"Transfer readiness validation passed with {result.ValidationDetails.Count} checks");
        }

        [Fact]
        public async Task ValidateTransferReadinessAsync_WithIncompatibleBuffers_ShouldFail()
        {
            // Arrange
            var sourceBuffer = new MockBuffer<float>(_sourceDevice, 1024);
            var destBuffer = new MockBuffer<float>(_targetDevice, 512); // Different size
            var transferPlan = CreateMockTransferPlan(sourceBuffer, destBuffer, P2PTransferStrategy.DirectP2P);

            // Act
            var result = await _validator.ValidateTransferReadinessAsync(sourceBuffer, destBuffer, transferPlan);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("size mismatch", result.ErrorMessage, System.StringComparison.OrdinalIgnoreCase);

            _output.WriteLine($"Transfer readiness validation failed as expected: {result.ErrorMessage}");
        }

        [Fact]
        public async Task ValidateTransferIntegrityAsync_WithSmallBuffer_ShouldPerformFullValidation()
        {
            // Arrange
            var bufferSize = 100; // Small buffer for full validation
            var sourceBuffer = new MockBuffer<int>(_sourceDevice, bufferSize);
            var destBuffer = new MockBuffer<int>(_targetDevice, bufferSize);
            var transferPlan = CreateMockTransferPlan(sourceBuffer, destBuffer, P2PTransferStrategy.DirectP2P);

            // Act
            var result = await _validator.ValidateTransferIntegrityAsync(sourceBuffer, destBuffer, transferPlan);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.ValidationDetails);
            
            var integrityCheck = Assert.Single(result.ValidationDetails, d => d.ValidationType.Contains("Integrity"));
            Assert.True(integrityCheck.IsValid);

            _output.WriteLine($"Small buffer integrity validation: {integrityCheck.ValidationType}");
        }

        [Fact]
        public async Task ValidateTransferIntegrityAsync_WithLargeBuffer_ShouldPerformSampledValidation()
        {
            // Arrange
            var bufferSize = 100 * 1024 * 1024; // 100MB buffer for sampled validation
            var elementCount = bufferSize / sizeof(double);
            var sourceBuffer = new MockBuffer<double>(_sourceDevice, elementCount);
            var destBuffer = new MockBuffer<double>(_targetDevice, elementCount);
            var transferPlan = CreateMockTransferPlan(sourceBuffer, destBuffer, P2PTransferStrategy.ChunkedP2P);

            // Act
            var result = await _validator.ValidateTransferIntegrityAsync(sourceBuffer, destBuffer, transferPlan);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.ValidationDetails);
            
            // Should have both sampled validation and checksum validation for large buffers
            var hasChecksum = result.ValidationDetails.Any(d => d.ValidationType.Contains("Checksum"));
            Assert.True(hasChecksum);

            _output.WriteLine($"Large buffer validation completed with {result.ValidationDetails.Count} validation checks");
        }

        [Fact]
        public async Task ExecuteP2PBenchmarkAsync_WithCompatibleDevices_ShouldReturnResults()
        {
            // Arrange
            var options = new P2PBenchmarkOptions
            {
                MinTransferSizeMB = 1,
                MaxTransferSizeMB = 16,
                UseCachedResults = false
            };

            // Act
            var result = await _validator.ExecuteP2PBenchmarkAsync(_sourceDevice, _targetDevice, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.TransferSizes);
            Assert.True(result.PeakThroughputGBps > 0);
            Assert.True(result.AverageThroughputGBps > 0);
            Assert.True(result.MinimumLatencyMs > 0);

            _output.WriteLine($"P2P benchmark completed: Peak {result.PeakThroughputGBps:F2} GB/s, Avg {result.AverageThroughputGBps:F2} GB/s");
            _output.WriteLine($"Transfer sizes tested: {result.TransferSizes.Count}");
        }

        [Fact]
        public async Task ExecuteP2PBenchmarkAsync_WithCachedResults_ShouldUseCacheOnSecondCall()
        {
            // Arrange
            var options = new P2PBenchmarkOptions
            {
                MinTransferSizeMB = 1,
                MaxTransferSizeMB = 4,
                UseCachedResults = true
            };

            // Act
            var firstResult = await _validator.ExecuteP2PBenchmarkAsync(_sourceDevice, _targetDevice, options);
            var secondResult = await _validator.ExecuteP2PBenchmarkAsync(_sourceDevice, _targetDevice, options);

            // Assert
            Assert.True(firstResult.IsSuccessful);
            Assert.True(secondResult.IsSuccessful);
            
            // Results should be identical when cached
            Assert.Equal(firstResult.PeakThroughputGBps, secondResult.PeakThroughputGBps);
            Assert.Equal(firstResult.TransferSizes.Count, secondResult.TransferSizes.Count);

            _output.WriteLine("Cached benchmark results returned successfully");
        }

        [Fact]
        public async Task ExecuteMultiGpuBenchmarkAsync_WithMultipleDevices_ShouldReturnComprehensiveResults()
        {
            // Arrange
            var devices = new[] { _sourceDevice, _targetDevice, new MockAccelerator("device3", "Device 3", AcceleratorType.CUDA) };
            var options = new P2PMultiGpuBenchmarkOptions
            {
                PairwiseTransferSizeMB = 8,
                EnablePairwiseBenchmarks = true,
                EnableScatterBenchmarks = true,
                EnableGatherBenchmarks = true,
                UseCachedResults = false
            };

            // Act
            var result = await _validator.ExecuteMultiGpuBenchmarkAsync(devices, options);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(3, result.DeviceCount);
            Assert.NotEmpty(result.PairwiseBenchmarks);
            Assert.NotEmpty(result.ScatterBenchmarks);
            Assert.NotEmpty(result.GatherBenchmarks);
            Assert.True(result.PeakPairwiseThroughputGBps > 0);

            _output.WriteLine($"Multi-GPU benchmark completed: {result.DeviceCount} devices");
            _output.WriteLine($"Pairwise: {result.PairwiseBenchmarks.Count}, Scatter: {result.ScatterBenchmarks.Count}, Gather: {result.GatherBenchmarks.Count}");
        }

        [Fact]
        public async Task GetValidationStatistics_AfterMultipleOperations_ShouldReturnAccurateStats()
        {
            // Arrange & Act
            var sourceBuffer = new MockBuffer<byte>(_sourceDevice, 1024);
            var destBuffer = new MockBuffer<byte>(_targetDevice, 1024);
            var transferPlan = CreateMockTransferPlan(sourceBuffer, destBuffer, P2PTransferStrategy.DirectP2P);

            // Perform multiple validations
            for (int i = 0; i < 5; i++)
            {
                await _validator.ValidateTransferReadinessAsync(sourceBuffer, destBuffer, transferPlan);
                await _validator.ValidateTransferIntegrityAsync(sourceBuffer, destBuffer, transferPlan);
            }

            // Execute benchmark
            await _validator.ExecuteP2PBenchmarkAsync(_sourceDevice, _targetDevice);

            var stats = _validator.GetValidationStatistics();

            // Assert
            Assert.Equal(10, stats.TotalValidations); // 5 readiness + 5 integrity
            Assert.Equal(10, stats.SuccessfulValidations);
            Assert.Equal(0, stats.FailedValidations);
            Assert.True(stats.IntegrityChecks > 0);
            Assert.Equal(1.0, stats.ValidationSuccessRate);
            Assert.True(stats.BenchmarksExecuted > 0);

            _output.WriteLine($"Validation statistics: {stats.TotalValidations} validations, {stats.BenchmarksExecuted} benchmarks");
        }

        [Theory]
        [InlineData(P2PTransferStrategy.DirectP2P)]
        [InlineData(P2PTransferStrategy.ChunkedP2P)]
        [InlineData(P2PTransferStrategy.PipelinedP2P)]
        [InlineData(P2PTransferStrategy.HostMediated)]
        public async Task ValidateTransferReadinessAsync_WithDifferentStrategies_ShouldValidateCorrectly(P2PTransferStrategy strategy)
        {
            // Arrange
            var sourceBuffer = new MockBuffer<long>(_sourceDevice, 512);
            var destBuffer = new MockBuffer<long>(_targetDevice, 512);
            var transferPlan = CreateMockTransferPlan(sourceBuffer, destBuffer, strategy);

            // Act
            var result = await _validator.ValidateTransferReadinessAsync(sourceBuffer, destBuffer, transferPlan);

            // Assert
            Assert.True(result.IsValid);
            
            var strategyValidation = result.ValidationDetails.FirstOrDefault(d => d.ValidationType == "TransferStrategy");
            Assert.NotNull(strategyValidation);
            Assert.True(strategyValidation.IsValid);

            _output.WriteLine($"Strategy {strategy} validation passed");
        }

        private static P2PTransferPlan CreateMockTransferPlan<T>(
            IBuffer<T> sourceBuffer,
            IBuffer<T> destBuffer,
            P2PTransferStrategy strategy) where T : unmanaged
        {
            return new P2PTransferPlan
            {
                PlanId = System.Guid.NewGuid().ToString(),
                SourceDevice = sourceBuffer.Accelerator,
                TargetDevice = destBuffer.Accelerator,
                TransferSize = sourceBuffer.SizeInBytes,
                Capability = new P2PConnectionCapability
                {
                    IsSupported = strategy != P2PTransferStrategy.HostMediated,
                    ConnectionType = strategy == P2PTransferStrategy.HostMediated ? P2PConnectionType.None : P2PConnectionType.PCIe,
                    EstimatedBandwidthGBps = 32.0,
                    LimitationReason = null
                },
                Strategy = strategy,
                ChunkSize = 4 * 1024 * 1024, // 4MB
                PipelineDepth = 2,
                EstimatedTransferTimeMs = 10.0,
                OptimizationScore = 0.8,
                CreatedAt = System.DateTimeOffset.UtcNow
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _validator.DisposeAsync();
            _sourceDevice.Dispose();
            _targetDevice.Dispose();
        }
    }
}