// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Memory;

/// <summary>
/// Unit tests for Metal unified memory optimizer.
/// </summary>
public sealed class MetalUnifiedMemoryOptimizerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalUnifiedMemoryOptimizer> _logger;
    private readonly IntPtr _device;
    private MetalUnifiedMemoryOptimizer? _optimizer;

    public MetalUnifiedMemoryOptimizerTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<MetalUnifiedMemoryOptimizer>();

        // Skip tests if Metal is not supported
        if (!MetalNative.IsMetalSupported())
        {
            Skip.If(true, "Metal is not supported on this system");
        }

        _device = MetalNative.CreateSystemDefaultDevice();
        Skip.If(_device == IntPtr.Zero, "Failed to create Metal device");
    }

    [SkippableFact(DisplayName = "Optimizer initialization detects Apple Silicon correctly")]
    public void OptimizerInitialization_DetectsAppleSilicon()
    {
        // Arrange & Act
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);

        // Assert
        Assert.NotNull(_optimizer);
        _output.WriteLine($"Is Apple Silicon: {_optimizer.IsAppleSilicon}");
        _output.WriteLine($"Has Unified Memory: {_optimizer.IsUnifiedMemory}");
        _output.WriteLine($"Device Location: {_optimizer.DeviceInfo.Location}");

        // Validation: Apple Silicon should have unified memory
        if (_optimizer.IsAppleSilicon)
        {
            Assert.True(_optimizer.IsUnifiedMemory, "Apple Silicon should have unified memory");
        }
    }

    [SkippableFact(DisplayName = "GetOptimalStorageMode returns Shared for unified memory")]
    public void GetOptimalStorageMode_UnifiedMemory_ReturnsShared()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);

        // Act - Test all memory patterns
        var patterns = new[]
        {
            MemoryUsagePattern.FrequentTransfer,
            MemoryUsagePattern.Streaming,
            MemoryUsagePattern.HostVisible,
            MemoryUsagePattern.GpuOnly,
            MemoryUsagePattern.ReadOnly,
            MemoryUsagePattern.Temporary
        };

        foreach (var pattern in patterns)
        {
            var mode = _optimizer.GetOptimalStorageMode(pattern);

            _output.WriteLine($"Pattern: {pattern}, Storage Mode: {mode}, Unified Memory: {_optimizer.IsUnifiedMemory}");

            // Assert - On unified memory (Apple Silicon), should prefer Shared mode
            if (_optimizer.IsUnifiedMemory && _optimizer.IsAppleSilicon)
            {
                Assert.Equal(MetalStorageMode.Shared, mode);
            }
        }
    }

    [SkippableFact(DisplayName = "GetOptimalStorageMode returns appropriate modes for discrete GPU")]
    public void GetOptimalStorageMode_DiscreteGpu_ReturnsAppropriateMode()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);

        // Only test on discrete GPUs
        Skip.If(_optimizer.IsUnifiedMemory, "Test requires discrete GPU");

        // Act & Assert
        var gpuOnlyMode = _optimizer.GetOptimalStorageMode(MemoryUsagePattern.GpuOnly);
        Assert.Equal(MetalStorageMode.Private, gpuOnlyMode);

        var frequentTransferMode = _optimizer.GetOptimalStorageMode(MemoryUsagePattern.FrequentTransfer);
        Assert.Equal(MetalStorageMode.Managed, frequentTransferMode);

        var hostVisibleMode = _optimizer.GetOptimalStorageMode(MemoryUsagePattern.HostVisible);
        Assert.Equal(MetalStorageMode.Shared, hostVisibleMode);
    }

    [SkippableFact(DisplayName = "EstimatePerformanceGain returns correct multipliers")]
    public void EstimatePerformanceGain_ReturnsCorrectMultipliers()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);
        const long bufferSize = 1024 * 1024; // 1MB

        // Act & Assert
        var frequentTransferGain = _optimizer.EstimatePerformanceGain(bufferSize, MemoryUsagePattern.FrequentTransfer);
        var streamingGain = _optimizer.EstimatePerformanceGain(bufferSize, MemoryUsagePattern.Streaming);
        var hostVisibleGain = _optimizer.EstimatePerformanceGain(bufferSize, MemoryUsagePattern.HostVisible);

        _output.WriteLine($"Frequent Transfer Gain: {frequentTransferGain}x");
        _output.WriteLine($"Streaming Gain: {streamingGain}x");
        _output.WriteLine($"Host Visible Gain: {hostVisibleGain}x");

        // On unified memory, should have significant gains
        if (_optimizer.IsUnifiedMemory && _optimizer.IsAppleSilicon)
        {
            Assert.True(frequentTransferGain >= 2.0, "Frequent transfer should have at least 2x gain");
            Assert.True(streamingGain >= 2.0, "Streaming should have at least 2x gain");
            Assert.True(hostVisibleGain >= 1.5, "Host visible should have at least 1.5x gain");
        }
        else
        {
            // Discrete GPU should have no gain
            Assert.Equal(1.0, frequentTransferGain);
            Assert.Equal(1.0, streamingGain);
            Assert.Equal(1.0, hostVisibleGain);
        }
    }

    [SkippableFact(DisplayName = "TrackZeroCopyOperation updates statistics")]
    public void TrackZeroCopyOperation_UpdatesStatistics()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);
        const long operationSize = 4096;

        // Act
        var initialOps = _optimizer.TotalZeroCopyOperations;
        var initialBytes = _optimizer.TotalBytesTransferred;

        _optimizer.TrackZeroCopyOperation(operationSize);
        _optimizer.TrackZeroCopyOperation(operationSize);

        // Assert
        Assert.Equal(initialOps + 2, _optimizer.TotalZeroCopyOperations);
        Assert.Equal(initialBytes + (operationSize * 2), _optimizer.TotalBytesTransferred);
    }

    [SkippableFact(DisplayName = "GetPerformanceStatistics returns complete metrics")]
    public void GetPerformanceStatistics_ReturnsCompleteMetrics()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);
        _optimizer.TrackZeroCopyOperation(1024 * 1024); // 1MB
        _optimizer.TrackZeroCopyOperation(2 * 1024 * 1024); // 2MB

        // Act
        var stats = _optimizer.GetPerformanceStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.ContainsKey("IsAppleSilicon"));
        Assert.True(stats.ContainsKey("HasUnifiedMemory"));
        Assert.True(stats.ContainsKey("TotalZeroCopyOperations"));
        Assert.True(stats.ContainsKey("TotalBytesTransferred"));
        Assert.True(stats.ContainsKey("TotalMegabytesTransferred"));
        Assert.True(stats.ContainsKey("EstimatedTimeSavingsSeconds"));
        Assert.True(stats.ContainsKey("AverageOperationSizeKB"));

        _output.WriteLine("Performance Statistics:");
        foreach (var kvp in stats)
        {
            _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        // Verify calculated values
        Assert.Equal(2L, stats["TotalZeroCopyOperations"]);
        Assert.Equal(3 * 1024 * 1024L, stats["TotalBytesTransferred"]);
        Assert.Equal(3.0, (double)stats["TotalMegabytesTransferred"], precision: 2);
    }

    [SkippableFact(DisplayName = "Device info is correctly populated")]
    public void DeviceInfo_IsCorrectlyPopulated()
    {
        // Arrange & Act
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);
        var deviceInfo = _optimizer.DeviceInfo;

        // Assert
        Assert.NotEqual(IntPtr.Zero, deviceInfo.Name);
        Assert.True(deviceInfo.MaxBufferLength > 0);
        Assert.True(deviceInfo.MaxThreadsPerThreadgroup > 0);

        _output.WriteLine($"Device Max Buffer Length: {deviceInfo.MaxBufferLength / (1024.0 * 1024.0):F2} MB");
        _output.WriteLine($"Device Max Threads Per Threadgroup: {deviceInfo.MaxThreadsPerThreadgroup}");
        _output.WriteLine($"Has Unified Memory: {deviceInfo.HasUnifiedMemory}");
    }

    [SkippableFact(DisplayName = "Multiple operations accumulate correctly")]
    public void MultipleOperations_AccumulateCorrectly()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);
        const int operationCount = 100;
        const long operationSize = 1024;

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            _optimizer.TrackZeroCopyOperation(operationSize);
        }

        // Assert
        Assert.Equal(operationCount, _optimizer.TotalZeroCopyOperations);
        Assert.Equal(operationCount * operationSize, _optimizer.TotalBytesTransferred);

        var stats = _optimizer.GetPerformanceStatistics();
        var avgSizeKB = (double)stats["AverageOperationSizeKB"];
        Assert.Equal(1.0, avgSizeKB, precision: 2);
    }

    [SkippableFact(DisplayName = "Dispose logs final statistics")]
    public void Dispose_LogsFinalStatistics()
    {
        // Arrange
        _optimizer = new MetalUnifiedMemoryOptimizer(_device, _logger);
        _optimizer.TrackZeroCopyOperation(5 * 1024 * 1024); // 5MB

        // Act & Assert (should not throw)
        _optimizer.Dispose();
        _optimizer = null; // Prevent double dispose
    }

    public void Dispose()
    {
        _optimizer?.Dispose();
        if (_device != IntPtr.Zero)
        {
            try
            {
                MetalNative.ReleaseDevice(_device);
            }
            catch
            {
                // Suppress errors during cleanup
            }
        }
    }
}
