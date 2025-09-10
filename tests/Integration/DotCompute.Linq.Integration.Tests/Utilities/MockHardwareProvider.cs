using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Memory;
using Moq;

namespace DotCompute.Linq.Integration.Tests.Utilities;

/// <summary>
/// Provides mocked hardware components for testing without requiring actual GPU hardware.
/// </summary>
public class MockHardwareProvider
{
    public Mock<IAccelerator> CpuAccelerator { get; }
    public Mock<IAccelerator> GpuAccelerator { get; }
    public Mock<ICudaRuntime> CudaRuntime { get; }
    public Mock<ICompiledKernel> CompiledKernel { get; }
    public Mock<IUnifiedBuffer> UnifiedBuffer { get; }

    public MockHardwareProvider()
    {
        CpuAccelerator = CreateMockCpuAccelerator();
        GpuAccelerator = CreateMockGpuAccelerator();
        CudaRuntime = CreateMockCudaRuntime();
        CompiledKernel = CreateMockCompiledKernel();
        UnifiedBuffer = CreateMockUnifiedBuffer();
    }

    private Mock<IAccelerator> CreateMockCpuAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        
        mock.Setup(x => x.BackendType).Returns(ComputeBackend.CPU);
        mock.Setup(x => x.IsAvailable).Returns(true);
        mock.Setup(x => x.DeviceName).Returns("Mock CPU");
        mock.Setup(x => x.MaxComputeUnits).Returns(Environment.ProcessorCount);
        mock.Setup(x => x.MaxMemory).Returns(8L * 1024 * 1024 * 1024); // 8GB
        
        mock.Setup(x => x.CompileKernel(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>()))
            .ReturnsAsync((KernelDefinition def, CompilationOptions opts) =>
            {
                var mockKernel = CreateMockCompiledKernel();
                mockKernel.Setup(k => k.Name).Returns(def.Name);
                return mockKernel.Object;
            });
            
        mock.Setup(x => x.CreateBuffer<It.IsAnyType>(It.IsAny<int>()))
            .Returns((int size) =>
            {
                var buffer = CreateMockUnifiedBuffer();
                buffer.Setup(b => b.Length).Returns(size);
                return (IUnifiedBuffer<object>)buffer.Object;
            });

        return mock;
    }

    private Mock<IAccelerator> CreateMockGpuAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        
        mock.Setup(x => x.BackendType).Returns(ComputeBackend.GPU);
        mock.Setup(x => x.IsAvailable).Returns(true);
        mock.Setup(x => x.DeviceName).Returns("Mock RTX 2000 Ada");
        mock.Setup(x => x.MaxComputeUnits).Returns(2560);
        mock.Setup(x => x.MaxMemory).Returns(16L * 1024 * 1024 * 1024); // 16GB
        mock.Setup(x => x.ComputeCapability).Returns(new Version(8, 9));
        
        mock.Setup(x => x.CompileKernel(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>()))
            .Returns(async (KernelDefinition def, CompilationOptions opts) =>
            {
                // Simulate compilation time
                await Task.Delay(100);
                
                var mockKernel = CreateMockCompiledKernel();
                mockKernel.Setup(k => k.Name).Returns(def.Name);
                mockKernel.Setup(k => k.TargetBackend).Returns(ComputeBackend.GPU);
                return mockKernel.Object;
            });
            
        mock.Setup(x => x.CreateBuffer<It.IsAnyType>(It.IsAny<int>()))
            .Returns((int size) =>
            {
                var buffer = CreateMockUnifiedBuffer();
                buffer.Setup(b => b.Length).Returns(size);
                buffer.Setup(b => b.DeviceType).Returns(DeviceType.GPU);
                return (IUnifiedBuffer<object>)buffer.Object;
            });

        return mock;
    }

    private Mock<ICudaRuntime> CreateMockCudaRuntime()
    {
        var mock = new Mock<ICudaRuntime>();
        
        mock.Setup(x => x.IsAvailable).Returns(true);
        mock.Setup(x => x.GetDeviceCount()).Returns(2);
        mock.Setup(x => x.GetDriverVersion()).Returns(new Version(12, 0));
        mock.Setup(x => x.GetRuntimeVersion()).Returns(new Version(12, 0));
        
        mock.Setup(x => x.GetDeviceProperties(It.IsAny<int>()))
            .Returns((int deviceId) => new CudaDeviceProperties
            {
                Name = $"Mock GPU {deviceId}",
                ComputeCapabilityMajor = 8,
                ComputeCapabilityMinor = 9,
                MaxThreadsPerBlock = 1024,
                MaxThreadsPerMultiProcessor = 2048,
                MultiProcessorCount = 40,
                SharedMemoryPerBlock = 49152,
                TotalConstantMemory = 65536,
                TotalGlobalMemory = 16L * 1024 * 1024 * 1024,
                WarpSize = 32,
                MaxBlockDimX = 1024,
                MaxBlockDimY = 1024,
                MaxBlockDimZ = 64,
                MaxGridDimX = int.MaxValue,
                MaxGridDimY = 65535,
                MaxGridDimZ = 65535
            });
            
        mock.Setup(x => x.SetDevice(It.IsAny<int>())).Returns(true);
        mock.Setup(x => x.GetCurrentDevice()).Returns(0);
        
        return mock;
    }

    private Mock<ICompiledKernel> CreateMockCompiledKernel()
    {
        var mock = new Mock<ICompiledKernel>();
        
        mock.Setup(x => x.IsValid).Returns(true);
        mock.Setup(x => x.TargetBackend).Returns(ComputeBackend.CPU);
        mock.Setup(x => x.CompilationTime).Returns(TimeSpan.FromMilliseconds(50));
        
        mock.Setup(x => x.ExecuteAsync(It.IsAny<object[]>()))
            .Returns(async (object[] parameters) =>
            {
                // Simulate execution time
                await Task.Delay(10);
                
                return new KernelExecutionResult
                {
                    Success = true,
                    ExecutionTime = TimeSpan.FromMilliseconds(10),
                    Result = GenerateResultData(parameters)
                };
            });
            
        mock.Setup(x => x.GetResourceUsage())
            .Returns(new KernelResourceUsage
            {
                MemoryUsage = 1024 * 1024, // 1MB
                ComputeUnitsUsed = 4,
                ExecutionTime = TimeSpan.FromMilliseconds(10)
            });

        return mock;
    }

    private Mock<IUnifiedBuffer> CreateMockUnifiedBuffer()
    {
        var mock = new Mock<IUnifiedBuffer>();
        
        mock.Setup(x => x.Length).Returns(1000);
        mock.Setup(x => x.ElementSize).Returns(4); // sizeof(float)
        mock.Setup(x => x.DeviceType).Returns(DeviceType.CPU);
        mock.Setup(x => x.IsDisposed).Returns(false);
        
        mock.Setup(x => x.CopyFromAsync(It.IsAny<Array>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        mock.Setup(x => x.CopyToAsync(It.IsAny<Array>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        mock.Setup(x => x.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mock;
    }

    private object GenerateResultData(object[] parameters)
    {
        // Generate mock result data based on parameter types
        if (parameters.Length == 0)
            return Array.Empty<float>();
            
        var firstParam = parameters[0];
        return firstParam switch
        {
            float[] floatArray => GenerateMockFloatResult(floatArray.Length),
            int[] intArray => GenerateMockIntResult(intArray.Length),
            double[] doubleArray => GenerateMockDoubleResult(doubleArray.Length),
            _ => Array.Empty<float>()
        };
    }

    private float[] GenerateMockFloatResult(int length)
    {
        var result = new float[length];
        var random = new Random(42); // Deterministic for testing
        
        for (int i = 0; i < length; i++)
        {
            result[i] = random.NextSingle() * 100;
        }
        
        return result;
    }

    private int[] GenerateMockIntResult(int length)
    {
        var result = new int[length];
        var random = new Random(42);
        
        for (int i = 0; i < length; i++)
        {
            result[i] = random.Next(0, 1000);
        }
        
        return result;
    }

    private double[] GenerateMockDoubleResult(int length)
    {
        var result = new double[length];
        var random = new Random(42);
        
        for (int i = 0; i < length; i++)
        {
            result[i] = random.NextDouble() * 1000;
        }
        
        return result;
    }

    /// <summary>
    /// Creates a mock performance profile for testing optimization scenarios.
    /// </summary>
    public PerformanceProfile CreateMockPerformanceProfile(
        TimeSpan? executionTime = null,
        long? memoryUsage = null,
        double? throughput = null)
    {
        return new PerformanceProfile
        {
            ExecutionTime = executionTime ?? TimeSpan.FromMilliseconds(100),
            MemoryUsage = memoryUsage ?? 1024 * 1024,
            ThroughputPerSecond = throughput ?? 1000,
            CpuUtilization = 0.5,
            GpuUtilization = 0.8,
            MemoryBandwidthUtilization = 0.6,
            CacheHitRate = 0.85,
            PowerConsumption = 150, // Watts
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates mock system information for testing.
    /// </summary>
    public SystemInfo CreateMockSystemInfo()
    {
        return new SystemInfo
        {
            CpuCores = Environment.ProcessorCount,
            CpuFrequency = 3_200_000_000, // 3.2 GHz
            AvailableMemory = 16L * 1024 * 1024 * 1024, // 16GB
            TotalMemory = 32L * 1024 * 1024 * 1024, // 32GB
            GpuAvailable = true,
            GpuMemory = 16L * 1024 * 1024 * 1024, // 16GB
            NumaNodes = 1,
            CacheLineSize = 64,
            PageSize = 4096,
            SupportedInstructionSets = new[] { "SSE4.2", "AVX2", "AVX512" },
            OperatingSystem = Environment.OSVersion.VersionString,
            Architecture = "x64"
        };
    }

    /// <summary>
    /// Creates mock CUDA device properties for testing.
    /// </summary>
    public CudaDeviceProperties CreateMockCudaDeviceProperties(int deviceId = 0)
    {
        return new CudaDeviceProperties
        {
            DeviceId = deviceId,
            Name = $"Mock RTX 2000 Ada (Device {deviceId})",
            ComputeCapabilityMajor = 8,
            ComputeCapabilityMinor = 9,
            MaxThreadsPerBlock = 1024,
            MaxThreadsPerMultiProcessor = 2048,
            MultiProcessorCount = 40,
            SharedMemoryPerBlock = 49152,
            TotalConstantMemory = 65536,
            TotalGlobalMemory = 16L * 1024 * 1024 * 1024,
            WarpSize = 32,
            MaxBlockDimX = 1024,
            MaxBlockDimY = 1024,
            MaxBlockDimZ = 64,
            MaxGridDimX = int.MaxValue,
            MaxGridDimY = 65535,
            MaxGridDimZ = 65535,
            ClockRate = 2520 * 1000, // 2.52 GHz
            MemoryClockRate = 9001 * 1000, // 18 Gbps effective
            MemoryBusWidth = 256,
            L2CacheSize = 32 * 1024 * 1024, // 32MB
            MaxTexture1DWidth = 131072,
            MaxTexture2DWidth = 131072,
            MaxTexture2DHeight = 65536,
            MaxTexture3DWidth = 16384,
            MaxTexture3DHeight = 16384,
            MaxTexture3DDepth = 16384,
            ConcurrentKernels = true,
            ECCEnabled = false,
            PCIBusId = deviceId,
            PCIDeviceId = 0,
            PCIDomainId = 0,
            IntegratedGpu = false,
            CanMapHostMemory = true,
            ComputeMode = 0,
            MaxSurfaceTexture1DWidth = 32768
        };
    }

    /// <summary>
    /// Disposes all mock objects and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        // Nothing to dispose for mocks, but included for consistency
    }
}