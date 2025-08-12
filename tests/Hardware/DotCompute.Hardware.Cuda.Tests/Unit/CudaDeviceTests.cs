// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.Unit;

/// <summary>
/// Unit tests for CUDA device detection and properties validation
/// </summary>
[Collection("CUDA Hardware Tests")]
public class CudaDeviceTests : IDisposable
{
    private readonly ILogger<CudaDeviceTests> _logger;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];

    public CudaDeviceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaDeviceTests>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Constructor_ShouldInitializeWithValidDevice()
    {
        // Arrange & Act
        Action createAccelerator = () =>
        {
            var accelerator = new CudaAccelerator(0, _logger);
            _accelerators.Add(accelerator);
        };

        // Assert
        if (IsCudaAvailable())
        {
            createAccelerator.Should().NotThrow();
            _accelerators.Should().HaveCount(1);
            _accelerators[0].Info.Should().NotBeNull();
        }
        else
        {
            createAccelerator.Should().Throw<InvalidOperationException>()
                .WithMessage("*CUDA accelerator*");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Info_ShouldContainValidDeviceProperties()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var info = accelerator.Info;

        // Assert
        info.Should().NotBeNull();
        info.Type.Should().Be(AcceleratorType.CUDA);
        info.Name.Should().NotBeNullOrEmpty();
        info.MemorySize.Should().BeGreaterThan(0);
        info.ComputeUnits.Should().BeGreaterThan(0);
        info.MaxClockFrequency.Should().BeGreaterThan(0);
        info.ComputeCapability.Should().NotBeNull();
        info.MaxSharedMemoryPerBlock.Should().BeGreaterThan(0);
        
        // Validate CUDA-specific capabilities
        info.Capabilities.Should().NotBeEmpty();
        info.Capabilities.Should().ContainKeys(
            "ComputeCapabilityMajor",
            "ComputeCapabilityMinor",
            "SharedMemoryPerBlock",
            "MultiprocessorCount",
            "WarpSize"
        );
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CudaAccelerator_Constructor_WithDifferentDeviceIds_ShouldHandleCorrectly(int deviceId)
    {
        // Arrange & Act
        Action createAccelerator = () =>
        {
            var accelerator = new CudaAccelerator(deviceId, _logger);
            _accelerators.Add(accelerator);
        };

        // Assert
        if (IsCudaAvailable() && IsDeviceAvailable(deviceId))
        {
            createAccelerator.Should().NotThrow();
            _accelerators.Last().Info.Should().NotBeNull();
        }
        else
        {
            createAccelerator.Should().ThrowAny<Exception>();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_ComputeCapability_ShouldBeValid()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var computeCapability = accelerator.Info.ComputeCapability;
        var capabilities = accelerator.Info.Capabilities;

        // Assert
        computeCapability.Should().NotBeNull();
        computeCapability.Major.Should().BeInRange(3, 10); // Reasonable range for CUDA compute capabilities
        computeCapability.Minor.Should().BeInRange(0, 9);

        // Validate consistency with capabilities dictionary
        capabilities["ComputeCapabilityMajor"].Should().Be(computeCapability.Major);
        capabilities["ComputeCapabilityMinor"].Should().Be(computeCapability.Minor);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_MemoryProperties_ShouldBeRealistic()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var info = accelerator.Info;

        // Assert
        info.MemorySize.Should().BeInRange(1_000_000_000L, 100_000_000_000L); // 1GB to 100GB reasonable range
        info.MaxSharedMemoryPerBlock.Should().BeInRange(16_384L, 163_840L); // 16KB to 160KB typical range
        
        // Validate memory bandwidth calculation
        var capabilities = info.Capabilities;
        capabilities.Should().ContainKey("MemoryBandwidth");
        var memoryBandwidth = Convert.ToDouble(capabilities["MemoryBandwidth"]);
        memoryBandwidth.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_WarpSize_ShouldBeStandard()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var warpSize = accelerator.Info.Capabilities["WarpSize"];

        // Assert
        warpSize.Should().Be(32); // Standard CUDA warp size
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_MultiprocessorCount_ShouldBeRealistic()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var mpCount = Convert.ToInt32(accelerator.Info.Capabilities["MultiprocessorCount"]);
        var computeUnits = accelerator.Info.ComputeUnits;

        // Assert
        mpCount.Should().BeInRange(1, 256); // Reasonable range for streaming multiprocessors
        mpCount.Should().Be(computeUnits); // Should match compute units
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [InlineData("ComputeCapabilityMajor", 3, 12)]
    [InlineData("ComputeCapabilityMinor", 0, 9)]
    [InlineData("MaxThreadsPerBlock", 256, 2048)]
    [InlineData("AsyncEngineCount", 0, 8)]
    public void CudaAccelerator_Capabilities_ShouldHaveValidRanges(string capabilityName, int minValue, int maxValue)
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var capabilities = accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey(capabilityName);
        var value = Convert.ToInt32(capabilities[capabilityName]);
        value.Should().BeInRange(minValue, maxValue, 
            $"{capabilityName} should be within reasonable bounds");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_BooleanCapabilities_ShouldBeValid()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var capabilities = accelerator.Info.Capabilities;

        // Assert - These should be boolean values
        var booleanCapabilities = new[]
        {
            "UnifiedAddressing",
            "ManagedMemory", 
            "ConcurrentKernels",
            "ECCEnabled"
        };

        foreach (var cap in booleanCapabilities)
        {
            capabilities.Should().ContainKey(cap);
            capabilities[cap].Should().BeOfType<bool>($"{cap} should be a boolean value");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [Trait("Performance", "Fast")]
    public void CudaAccelerator_Constructor_PerformanceTest()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);
        stopwatch.Stop();

        // Assert - Constructor should be relatively fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "CUDA accelerator initialization should complete within 5 seconds");
        
        _output.WriteLine($"CUDA Accelerator initialization took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Reset_ShouldSucceedWithoutErrors()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act & Assert
        Action resetAction = () => accelerator.Reset();
        resetAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_Synchronize_ShouldCompleteSuccessfully()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync();
        await syncAction.Should().NotThrowAsync();
    }

    // Helper Methods
    private static bool IsCudaAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDeviceAvailable(int deviceId)
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceId < deviceCount;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var accelerator in _accelerators)
        {
            try
            {
                accelerator?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing CUDA accelerator");
            }
        }
        _accelerators.Clear();
    }
}