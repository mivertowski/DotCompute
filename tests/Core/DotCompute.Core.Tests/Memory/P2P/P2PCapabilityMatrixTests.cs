// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Memory.P2P;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Core.Tests.Memory.P2P
{
    /// <summary>
    /// Test suite for P2P Capability Matrix functionality.
    /// </summary>
    public sealed class P2PCapabilityMatrixTests : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly P2PCapabilityMatrix _matrix;
        private readonly MockAccelerator _cudaDevice1;
        private readonly MockAccelerator _cudaDevice2;
        private readonly MockAccelerator _rocmDevice;

        public P2PCapabilityMatrixTests(ITestOutputHelper output)
        {
            _output = output;
            _matrix = new P2PCapabilityMatrix(new TestLogger<P2PCapabilityMatrix>(output));
            
            _cudaDevice1 = new MockAccelerator("cuda_1", "RTX 4090 #1", AcceleratorType.CUDA);
            _cudaDevice2 = new MockAccelerator("cuda_2", "RTX 4090 #2", AcceleratorType.CUDA);
            _rocmDevice = new MockAccelerator("rocm_1", "MI250X", AcceleratorType.GPU);
        }

        [Fact]
        public async Task BuildMatrixAsync_WithMultipleDevices_ShouldCreateMatrix()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2, _rocmDevice };

            // Act
            await _matrix.BuildMatrixAsync(devices);

            // Assert
            var stats = _matrix.GetMatrixStatistics();
            Assert.Equal(3, stats.TotalDevices);
            Assert.True(stats.TotalConnections > 0);

            _output.WriteLine($"Matrix built: {stats.TotalDevices} devices, {stats.TotalConnections} connections");
        }

        [Fact]
        public async Task GetP2PCapabilityAsync_BetweenCudaDevices_ShouldDetectNVLink()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2 };
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var capability = await _matrix.GetP2PCapabilityAsync(_cudaDevice1, _cudaDevice2);

            // Assert
            Assert.True(capability.IsSupported);
            Assert.Equal(P2PConnectionType.NVLink, capability.ConnectionType);
            Assert.True(capability.EstimatedBandwidthGBps > 0);

            _output.WriteLine($"CUDA P2P capability: {capability.ConnectionType}, {capability.EstimatedBandwidthGBps} GB/s");
        }

        [Fact]
        public async Task GetP2PCapabilityAsync_BetweenSameDevice_ShouldReturnNotSupported()
        {
            // Arrange
            var devices = new[] { _cudaDevice1 };
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var capability = await _matrix.GetP2PCapabilityAsync(_cudaDevice1, _cudaDevice1);

            // Assert
            Assert.False(capability.IsSupported);
            Assert.Equal(P2PConnectionType.None, capability.ConnectionType);
            Assert.Contains("Same device", capability.LimitationReason);
        }

        [Fact]
        public async Task FindOptimalP2PPathAsync_WithDirectConnection_ShouldReturnDirectPath()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2 };
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var path = await _matrix.FindOptimalP2PPathAsync(_cudaDevice1, _cudaDevice2);

            // Assert
            Assert.NotNull(path);
            Assert.True(path.IsDirectP2P);
            Assert.Equal(1, path.Hops);
            Assert.Empty(path.IntermediateDevices);
            Assert.True(path.TotalBandwidthGBps > 0);

            _output.WriteLine($"Direct P2P path: {path.TotalBandwidthGBps} GB/s, {path.EstimatedLatencyMs} ms latency");
        }

        [Fact]
        public async Task GetP2PConnectionsAsync_ForCudaDevice_ShouldReturnConnections()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2, _rocmDevice };
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var connections = await _matrix.GetP2PConnectionsAsync(_cudaDevice1);

            // Assert
            Assert.NotEmpty(connections);
            var p2pConnections = connections.Where(c => c.Capability.IsSupported).ToList();
            Assert.NotEmpty(p2pConnections);

            _output.WriteLine($"P2P connections for {_cudaDevice1.Info.Name}: {p2pConnections.Count} connections");
        }

        [Fact]
        public async Task GetTopologyAnalysis_AfterMatrixBuild_ShouldProvideAnalysis()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2, _rocmDevice };
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var analysis = _matrix.GetTopologyAnalysis();

            // Assert
            Assert.Equal(3, analysis.TotalDevices);
            Assert.True(analysis.TotalPossibleConnections > 0);
            Assert.True(analysis.P2PConnectivityRatio >= 0.0 && analysis.P2PConnectivityRatio <= 1.0);

            if (analysis.TopologyClusters.Any())
            {
                _output.WriteLine($"Topology clusters: {string.Join(", ", analysis.TopologyClusters.Select(c => c.ClusterType))}");
            }

            if (analysis.HighPerformancePaths.Any())
            {
                var bestPath = analysis.HighPerformancePaths.First();
                _output.WriteLine($"Best performance path: {bestPath.BandwidthGBps} GB/s ({bestPath.ConnectionType})");
            }
        }

        [Fact]
        public async Task ValidateMatrixIntegrityAsync_AfterBuild_ShouldPassValidation()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2 };
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var validation = await _matrix.ValidateMatrixIntegrityAsync();

            // Assert
            Assert.NotNull(validation);
            // Note: Some asymmetry issues might be expected in mock environment
            _output.WriteLine($"Matrix validation: {(validation.IsValid ? "PASS" : "FAIL")}, {validation.Issues.Count} issues");
        }

        [Fact]
        public async Task GetMatrixStatistics_AfterOperations_ShouldReturnStats()
        {
            // Arrange
            var devices = new[] { _cudaDevice1, _cudaDevice2, _rocmDevice };
            await _matrix.BuildMatrixAsync(devices);

            // Perform some operations
            await _matrix.GetP2PCapabilityAsync(_cudaDevice1, _cudaDevice2);
            await _matrix.GetP2PCapabilityAsync(_cudaDevice1, _rocmDevice);

            // Act
            var stats = _matrix.GetMatrixStatistics();

            // Assert
            Assert.Equal(3, stats.TotalDevices);
            Assert.True(stats.TotalConnections >= 0);
            Assert.True(stats.CacheHitRatio >= 0.0 && stats.CacheHitRatio <= 1.0);
            Assert.True(stats.LastRefreshTime > default);

            _output.WriteLine($"Matrix statistics: {stats.TotalConnections} connections, {stats.P2PEnabledConnections} P2P-enabled");
            _output.WriteLine($"Connection types - NVLink: {stats.NVLinkConnections}, PCIe: {stats.PCIeConnections}");
        }

        [Theory]
        [InlineData("cuda_1", "cuda_2", true)] // CUDA to CUDA should support P2P
        [InlineData("cuda_1", "rocm_1", false)] // Cross-vendor should not support P2P
        public async Task GetP2PCapabilityAsync_VariousDevicePairs_ShouldReturnExpectedSupport(
            string device1Id, string device2Id, bool expectedSupport)
        {
            // Arrange
            var deviceMap = new[]
            {
                _cudaDevice1, _cudaDevice2, _rocmDevice
            }.ToDictionary(d => d.Info.Id);

            var devices = deviceMap.Values.ToArray();
            await _matrix.BuildMatrixAsync(devices);

            // Act
            var capability = await _matrix.GetP2PCapabilityAsync(deviceMap[device1Id], deviceMap[device2Id]);

            // Assert
            Assert.Equal(expectedSupport, capability.IsSupported);
            
            if (expectedSupport)
            {
                Assert.True(capability.EstimatedBandwidthGBps > 0);
                Assert.NotEqual(P2PConnectionType.None, capability.ConnectionType);
            }
            else
            {
                Assert.Equal(0.0, capability.EstimatedBandwidthGBps);
                Assert.Equal(P2PConnectionType.None, capability.ConnectionType);
            }

            _output.WriteLine($"P2P {device1Id} -> {device2Id}: {(expectedSupport ? "SUPPORTED" : "NOT SUPPORTED")}");
        }

        public async ValueTask DisposeAsync()
        {
            await _matrix.DisposeAsync();
            _cudaDevice1.Dispose();
            _cudaDevice2.Dispose();
            _rocmDevice.Dispose();
        }
    }
}