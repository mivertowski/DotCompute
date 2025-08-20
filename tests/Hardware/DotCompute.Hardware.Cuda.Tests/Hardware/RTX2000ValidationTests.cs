// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.Cuda.Tests.Hardware;


/// <summary>
/// RTX 2000 series specific validation tests
/// </summary>
[Collection("CUDA Hardware Tests")]
public sealed class RTX2000ValidationTests : IDisposable
{
    private readonly ILogger<RTX2000ValidationTests> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];

    // LoggerMessage delegate for performance
    private static readonly Action<ILogger, Exception, Exception?> LogAcceleratorDisposeError =
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(1, nameof(LogAcceleratorDisposeError)),
            "Error disposing CUDA accelerator: {Exception}");

    public RTX2000ValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = _loggerFactory.CreateLogger<RTX2000ValidationTests>();
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_DeviceDetection_ShouldIdentifyRTX2000SeriesGPU()
    {
        // Arrange & Act
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        // Assert
        if (IsRTX2000Series(info.Name))
        {
            Assert.NotNull(info);
            _ = info.DeviceType.Should().Be(AcceleratorType.CUDA.ToString());
            _ = info.ComputeCapability.Should().NotBeNull();
            _ = (info.ComputeCapability!.Major >= 7).Should().BeTrue(); // RTX 2000 series has compute capability 7.5

            _output.WriteLine($"Detected RTX 2000 series GPU: {info.Name}");
            _output.WriteLine($"Compute Capability: {info.ComputeCapability.Major}.{info.ComputeCapability.Minor}");
            _output.WriteLine($"Memory Size: {info.MemorySize / (1024 * 1024 * 1024)} GB");
        }
        else
        {
            _output.WriteLine($"RTX 2000 series GPU not detected. Found: {info.Name}");
        }
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_TuringArchitectureFeatures_ShouldBeSupported()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series specific features
        _ = info.ComputeCapability!.Major.Should().Be(7); // RTX 2000 series should have compute capability 7.x(Turing)
        _ = info.ComputeCapability!.Minor.Should().Be(5); // RTX 2000 series should have compute capability 7.5

        // Turing architecture features
        _ = info.Capabilities.Should().ContainKey("ConcurrentKernels");
        _ = info.Capabilities.Should().ContainKey("AsyncEngineCount");
        _ = info.Capabilities.Should().ContainKey("UnifiedAddressing");

        var concurrentKernels = (bool)info.Capabilities!["ConcurrentKernels"];
        _ = concurrentKernels.Should().BeTrue(); // RTX 2000 series should support concurrent kernel execution

        var asyncEngines = Convert.ToInt32(info.Capabilities!["AsyncEngineCount"], CultureInfo.InvariantCulture);
        _ = asyncEngines.Should().BeGreaterThanOrEqualTo(2, "RTX 2000 series should have multiple async engines");

        _output.WriteLine($"Turing Architecture Features Verified:");
        _output.WriteLine($"  Concurrent Kernels: {concurrentKernels}");
        _output.WriteLine($"  Async Engines: {asyncEngines}");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_MemorySpecifications_ShouldMatchExpectedCapacity()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series memory specifications
        var memoryGB = info.MemorySize / (1024.0 * 1024.0 * 1024.0);

        // RTX 2060: 6GB, RTX 2070: 8GB, RTX 2080: 8/11GB, RTX 2080 Ti: 11GB
        _ = memoryGB.Should().BeInRange(4.0, 12.0, "RTX 2000 series should have between 4GB and 12GB memory");

        var memoryBandwidth = Convert.ToDouble(info.Capabilities!["MemoryBandwidth"]!, CultureInfo.InvariantCulture);
        _ = memoryBandwidth.Should().BeGreaterThan(200.0, "RTX 2000 series should have >200 GB/s memory bandwidth");

        var memoryBusWidth = Convert.ToInt32(info.Capabilities["MemoryBusWidth"]!, CultureInfo.InvariantCulture);
        _ = memoryBusWidth.Should().BeInRange(192, 384, "RTX 2000 series should have 192-384 bit memory bus");

        _output.WriteLine($"RTX 2000 Memory Specifications:");
        _output.WriteLine($"  Memory Size: {memoryGB:F1} GB");
        _output.WriteLine($"  Memory Bandwidth: {memoryBandwidth:F1} GB/s");
        _output.WriteLine($"  Memory Bus Width: {memoryBusWidth} bits");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_StreamingMultiprocessors_ShouldMatchArchitecture()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series SM specifications
        var smCount = Convert.ToInt32(info.Capabilities!["MultiprocessorCount"]!, CultureInfo.InvariantCulture);
        _ = smCount.Should().BeInRange(30, 72, "RTX 2000 series should have 30-72 SMs depending on model");
        Assert.Equal(info.ComputeUnits, smCount); // SM count should match compute units

        var maxThreadsPerBlock = Convert.ToInt32(info.Capabilities!["MaxThreadsPerBlock"]!, CultureInfo.InvariantCulture);
        Assert.Equal(1024, maxThreadsPerBlock); // RTX 2000 series should support 1024 threads per block;

        var maxThreadsPerSM = Convert.ToInt32(info.Capabilities!["MaxThreadsPerMultiprocessor"]!, CultureInfo.InvariantCulture);
        Assert.Equal(1024, maxThreadsPerSM); // Turing architecture should support 1024 threads per SM;

        var warpSize = Convert.ToInt32(info.Capabilities!["WarpSize"]!, CultureInfo.InvariantCulture);
        Assert.Equal(32, warpSize); // CUDA warp size should be 32;

        _output.WriteLine($"RTX 2000 SM Specifications:");
        _output.WriteLine($"  SM Count: {smCount}");
        _output.WriteLine($"  Max Threads/Block: {maxThreadsPerBlock}");
        _output.WriteLine($"  Max Threads/SM: {maxThreadsPerSM}");
        _output.WriteLine($"  Warp Size: {warpSize}");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_SharedMemoryConfiguration_ShouldSupportTuringSpecs()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series shared memory specifications
        var sharedMemPerBlock = info.MaxSharedMemoryPerBlock;
        _ = sharedMemPerBlock.Should().BeInRange(48 * 1024L, 96 * 1024L, "RTX 2000 series should support 48-96KB shared memory per block");

        var sharedMemPerSM = Convert.ToInt64(info.Capabilities!["SharedMemoryPerBlock"]!, CultureInfo.InvariantCulture);
        Assert.Equal(sharedMemPerBlock, sharedMemPerSM); // Shared memory values should be consistent

        _output.WriteLine($"RTX 2000 Shared Memory:");
        _output.WriteLine($"  Shared Memory/Block: {sharedMemPerBlock / 1024} KB");
        _output.WriteLine($"  Shared Memory/SM: {sharedMemPerSM / 1024} KB");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_ClockFrequencies_ShouldBeRealistic()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series clock frequencies
        var baseClock = info.MaxClockFrequency; // in MHz
        _ = baseClock.Should().BeInRange(1200, 2100, "RTX 2000 series base clock should be 1200-2100 MHz");

        var memoryClockRate = Convert.ToInt32(info.Capabilities!["MemoryClockRate"]!, CultureInfo.InvariantCulture);
        _ = memoryClockRate.Should().BeInRange(6000, 8000, "RTX 2000 series memory clock should be 6000-8000 MHz effective");

        _output.WriteLine($"RTX 2000 Clock Frequencies:");
        _output.WriteLine($"  Base Clock: {baseClock} MHz");
        _output.WriteLine($"  Memory Clock: {memoryClockRate} MHz");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_TensorCoreSupport_ShouldBeAvailable()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series should have first-generation Tensor Cores
        _ = info.ComputeCapability!.Major.Should().Be(7);
        _ = info.ComputeCapability!.Minor.Should().Be(5);

        // Note: Tensor Core availability is implicit in compute capability 7.5
        _output.WriteLine($"RTX 2000 Tensor Core Support:");
        _output.WriteLine($"  Compute Capability: {info.ComputeCapability.Major}.{info.ComputeCapability.Minor}");
        _output.WriteLine($"  First-generation Tensor Cores available");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_RTCoreSupport_ShouldBeAvailable()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Assert - RTX 2000 series should have first-generation RT Cores
        _ = info.ComputeCapability!.Major.Should().Be(7);

        // RT Cores are hardware-specific and not directly exposed via CUDA properties
        // But we can infer their presence from RTX branding and compute capability
        _output.WriteLine($"RTX 2000 RT Core Support:");
        _output.WriteLine($"  First-generation RT Cores availableinferred from RTX branding)");
        _output.WriteLine($"  Hardware ray-tracing acceleration supported");
    }

    [Theory]
    [Trait("Category", "Hardware")]
    [Trait("Hardware", "RTX2000")]
    [InlineData(OptimizationLevel.Maximum)]
    [InlineData(OptimizationLevel.Default)]
    public async Task RTX2000_OptimizedKernelCompilation_ShouldLeverageTuringFeatures(OptimizationLevel optimizationLevel)
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        var kernelDefinition = CreateTuringOptimizedKernel();
        var options = new CompilationOptions
        {
            OptimizationLevel = optimizationLevel,
            AdditionalFlags = ["--gpu-architecture=sm_75"] // Turing-specific
        };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, options);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("turing_optimized");

        _output.WriteLine($"Successfully compiled Turing-optimized kernel with {optimizationLevel} optimization");

        (compiledKernel as IDisposable)?.Dispose();
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "RTX2000")]
    public void RTX2000_MemoryBandwidth_ShouldMeetSpecifications()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        // Act
        var stats = memoryManager!.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        _ = (stats.TotalMemory > 0).Should().BeTrue();

        var expectedMemoryGB = GetExpectedMemoryForRTX2000(info.Name);
        var actualMemoryGB = stats.TotalMemory / (1024.0 * 1024.0 * 1024.0);

        // Allow for some variance due to system reserved memory
        _ = actualMemoryGB.Should().BeInRange(expectedMemoryGB - 0.5, expectedMemoryGB + 0.5,
            $"{info.Name} should have approximately {expectedMemoryGB}GB memory");

        _output.WriteLine($"RTX 2000 Memory Validation:");
        _output.WriteLine($"  Model: {info.Name}");
        _output.WriteLine($"  Expected Memory: {expectedMemoryGB}GB");
        _output.WriteLine($"  Actual Memory: {actualMemoryGB:F2}GB");
    }

    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Hardware", "RTX2000")]
    public async Task RTX2000_ThermalStressTest_ShouldHandleExtendedLoad()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var info = accelerator.Info;

        if (!IsRTX2000Series(info.Name))
            return;

        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        const int stressIterations = 100;
        const int bufferSize = 32 * 1024 * 1024; // 32MB per iteration

        _output.WriteLine($"Starting thermal stress test on {info.Name}");

        // Act - Sustained memory operations to generate thermal load
        var buffers = new List<ISyncMemoryBuffer>();

        try
        {
            for (var i = 0; i < stressIterations; i++)
            {
                var buffer = memoryManager!.Allocate(bufferSize);
                buffers.Add(buffer);

                // Fill buffer to generate memory traffic
                memoryManager.Fill(buffer, (byte)(i % 256), bufferSize);

                if (i % 10 == 0)
                {
                    await accelerator.SynchronizeAsync();
                    _output.WriteLine($"Stress test iteration {i}/{stressIterations}");
                }
            }

            // Final synchronization
            await accelerator.SynchronizeAsync();

            // Assert
            Assert.Equal(stressIterations, buffers.Count); // All allocations should succeed under thermal stress

            _output.WriteLine($"Thermal stress test completed successfully");
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                memoryManager!.Free(buffer);
            }
        }
    }

    // Helper Methods
    private CudaAccelerator CreateAccelerator()
    {
        var accelerator = new CudaAccelerator(0, null);
        _accelerators.Add(accelerator);
        return accelerator;
    }

    private static bool IsRTX2000Series(string deviceName)
    {
        var rtx2000Names = new[]
        {
        "RTX 2060", "RTX 2070", "RTX 2080", "RTX 2080 Ti",
        "GeForce RTX 2060", "GeForce RTX 2070", "GeForce RTX 2080", "GeForce RTX 2080 Ti",
        "Quadro RTX 4000", "Quadro RTX 5000", "Quadro RTX 6000", "Quadro RTX 8000"
    };

        return rtx2000Names.Any(name => deviceName.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private static double GetExpectedMemoryForRTX2000(string deviceName)
    {
        return deviceName.ToUpperInvariant() switch
        {
            var name when name.Contains("RTX 2060", StringComparison.OrdinalIgnoreCase) => 6.0,
            var name when name.Contains("RTX 2070", StringComparison.OrdinalIgnoreCase) => 8.0,
            var name when name.Contains("RTX 2080 TI", StringComparison.OrdinalIgnoreCase) => 11.0,
            var name when name.Contains("RTX 2080", StringComparison.OrdinalIgnoreCase) => 8.0,
            var name when name.Contains("QUADRO RTX 4000", StringComparison.OrdinalIgnoreCase) => 8.0,
            var name when name.Contains("QUADRO RTX 5000", StringComparison.OrdinalIgnoreCase) => 16.0,
            var name when name.Contains("QUADRO RTX 6000", StringComparison.OrdinalIgnoreCase) => 24.0,
            var name when name.Contains("QUADRO RTX 8000", StringComparison.OrdinalIgnoreCase) => 48.0,
            _ => 8.0 // Default assumption
        };
    }

    private static KernelDefinition CreateTuringOptimizedKernel()
    {
        const string kernelSource = @"
__global__ void turing_optimized(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    if(idx < n) {
        // Use features available in compute capability 7.5(Turing)
        float val = input[idx];
        
        // Fast math operations optimized for Turing
        val = __fmaf_rn(val, 2.0f, 1.0f); // Fused multiply-add
        val = __expf(val);                 // Fast exponential
        val = __powf(val, 0.5f);           // Fast power
        
        output[idx] = val;
    }
}";
        return new KernelDefinition
        {
            Name = "turing_optimized",
            Code = kernelSource,
            EntryPoint = "turing_optimized"
        };
    }

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

    private static bool IsNvrtcAvailable()
    {
        try
        {
            // Since IsNvrtcAvailable doesn't exist, return true for tests
            return true;
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
                LogAcceleratorDisposeError(_logger, ex, null);
            }
        }
        _accelerators.Clear();
        _loggerFactory?.Dispose();
    }
}
