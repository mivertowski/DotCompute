// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.Mock;

/// <summary>
/// Mock device tests for CI/CD environments without actual CUDA hardware
/// </summary>
[Collection("CUDA Mock Tests")]
public class CudaMockDeviceTests
{
    private readonly ILogger<CudaMockDeviceTests> _logger;
    private readonly ITestOutputHelper _output;

    public CudaMockDeviceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaMockDeviceTests>();
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_DeviceProperties_ShouldSimulateValidGPU()
    {
        // Arrange
        var mockProperties = CreateMockRTX2070Properties();

        // Act & Assert
        mockProperties.Major.Should().Be(7);
        mockProperties.Minor.Should().Be(5);
        mockProperties.Name.Should().Be("Mock RTX 2070");
        mockProperties.TotalGlobalMem.Should().Be(8UL * 1024 * 1024 * 1024); // 8GB
        mockProperties.MultiProcessorCount.Should().Be(36);
        mockProperties.MaxThreadsPerBlock.Should().Be(1024);
        mockProperties.WarpSize.Should().Be(32);
        mockProperties.SharedMemPerBlock.Should().Be(49152UL); // 48KB
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_AcceleratorInfo_ShouldProvideRealisticCapabilities()
    {
        // Arrange
        var mockProperties = CreateMockRTX2070Properties();
        var mockInfo = CreateAcceleratorInfoFromMockProperties(mockProperties);

        // Act & Assert
        mockInfo.Type.Should().Be(AcceleratorType.CUDA);
        mockInfo.Name.Should().Be("Mock RTX 2070");
        mockInfo.MemorySize.Should().Be(8L * 1024 * 1024 * 1024); // 8GB
        mockInfo.ComputeUnits.Should().Be(36);
        mockInfo.ComputeCapability.Should().Be(new Version(7, 5));
        
        mockInfo.Capabilities.Should().ContainKey("ComputeCapabilityMajor");
        mockInfo.Capabilities.Should().ContainKey("ComputeCapabilityMinor");
        mockInfo.Capabilities.Should().ContainKey("MultiprocessorCount");
        mockInfo.Capabilities.Should().ContainKey("WarpSize");
        mockInfo.Capabilities.Should().ContainKey("UnifiedAddressing");
        mockInfo.Capabilities.Should().ContainKey("ConcurrentKernels");
    }

    [Theory]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    [InlineData("Mock GTX 1660", 6, 1, 22, 6144)] // Maxwell
    [InlineData("Mock RTX 2060", 7, 5, 30, 6144)] // Turing
    [InlineData("Mock RTX 3070", 8, 6, 46, 8192)] // Ampere
    [InlineData("Mock RTX 4070", 8, 9, 46, 12288)] // Ada Lovelace
    public void MockDevice_DifferentArchitectures_ShouldHaveCorrectSpecs(
        string deviceName, int computeMajor, int computeMinor, int smCount, long memoryMB)
    {
        // Arrange
        var mockProperties = new CudaDeviceProperties
        {
            Name = deviceName,
            Major = computeMajor,
            Minor = computeMinor,
            MultiProcessorCount = smCount,
            TotalGlobalMem = (ulong)(memoryMB * 1024 * 1024),
            MaxThreadsPerBlock = 1024,
            WarpSize = 32,
            SharedMemPerBlock = computeMajor >= 8 ? 65536UL : 49152UL, // 64KB for Ampere+, 48KB for earlier
            MaxThreadsPerMultiProcessor = 1024,
            ClockRate = 1500000, // 1.5 GHz
            MemoryClockRate = 7000000, // 7 GHz effective
            MemoryBusWidth = 256,
            L2CacheSize = 4 * 1024 * 1024, // 4MB
            AsyncEngineCount = 2,
            UnifiedAddressing = 1,
            ManagedMemory = 1,
            ConcurrentKernels = 1,
            ECCEnabled = 0
        };

        // Act
        var mockInfo = CreateAcceleratorInfoFromMockProperties(mockProperties);

        // Assert
        mockInfo.Name.Should().Be(deviceName);
        mockInfo.ComputeCapability.Major.Should().Be(computeMajor);
        mockInfo.ComputeCapability.Minor.Should().Be(computeMinor);
        mockInfo.ComputeUnits.Should().Be(smCount);
        mockInfo.MemorySize.Should().Be(memoryMB * 1024L * 1024L);
        
        _output.WriteLine($"Mock GPU: {deviceName}");
        _output.WriteLine($"  Compute Capability: {computeMajor}.{computeMinor}");
        _output.WriteLine($"  SM Count: {smCount}");
        _output.WriteLine($"  Memory: {memoryMB}MB");
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_MemoryStatistics_ShouldSimulateRealisticUsage()
    {
        // Arrange
        var totalMemory = 8L * 1024 * 1024 * 1024; // 8GB
        var usedMemory = 1024L * 1024 * 1024; // 1GB used
        var freeMemory = totalMemory - usedMemory;
        
        var mockStats = new MemoryStatistics
        {
            TotalMemory = totalMemory,
            UsedMemory = usedMemory,
            FreeMemory = freeMemory,
            AllocatedMemory = usedMemory / 2, // Half of used memory is allocated by our system
            AllocationCount = 10,
            PeakMemory = usedMemory + 512L * 1024 * 1024 // Peak was 1.5GB
        };

        // Act & Assert
        mockStats.TotalMemory.Should().Be(8L * 1024 * 1024 * 1024);
        mockStats.FreeMemory.Should().Be(totalMemory - usedMemory);
        mockStats.UsedMemory.Should().BeGreaterThan(0);
        mockStats.AllocatedMemory.Should().BeLessOrEqualTo(mockStats.UsedMemory);
        mockStats.AllocationCount.Should().BeGreaterThan(0);
        mockStats.PeakMemory.Should().BeGreaterOrEqualTo(mockStats.UsedMemory);
        
        var utilizationPercent = (mockStats.UsedMemory * 100.0) / mockStats.TotalMemory;
        _output.WriteLine($"Mock Memory Usage: {utilizationPercent:F1}% ({mockStats.UsedMemory / (1024*1024*1024)}GB / {mockStats.TotalMemory / (1024*1024*1024)}GB)");
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_ErrorSimulation_ShouldHandleDeviceErrors()
    {
        // Arrange
        var mockErrors = new[]
        {
            CudaError.OutOfMemory,
            CudaError.InvalidDevice,
            CudaError.InvalidValue,
            CudaError.LaunchFailure,
            CudaError.InvalidDevicePointer
        };

        // Act & Assert
        foreach (var error in mockErrors)
        {
            var errorString = GetMockErrorString(error);
            errorString.Should().NotBeNullOrEmpty($"Error {error} should have a descriptive string");
            errorString.Should().Contain(error.ToString().ToLowerInvariant());
            
            _output.WriteLine($"Mock Error: {error} -> {errorString}");
        }
    }

    [Theory]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(16 * 1024 * 1024)]
    public void MockDevice_MemoryAllocation_ShouldSimulateRealisticBehavior(long sizeInBytes)
    {
        // Arrange
        var mockMemoryManager = CreateMockMemoryManager();
        var mockBuffer = CreateMockMemoryBuffer(sizeInBytes);

        // Act
        var allocationResult = SimulateAllocation(mockMemoryManager, sizeInBytes);

        // Assert
        allocationResult.Should().NotBeNull();
        allocationResult.Success.Should().BeTrue();
        allocationResult.AllocatedSize.Should().Be(sizeInBytes);
        allocationResult.AllocationTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
        
        _output.WriteLine($"Mock allocation of {sizeInBytes / 1024}KB took {allocationResult.AllocationTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_KernelCompilation_ShouldSimulateNVRTCBehavior()
    {
        // Arrange
        var mockKernelSource = @"
__global__ void mock_kernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        output[idx] = input[idx] * 2.0f;
    }
}";
        var mockCompilationResult = CreateMockCompilationResult(mockKernelSource, true);

        // Act & Assert
        mockCompilationResult.Success.Should().BeTrue();
        mockCompilationResult.CompiledCode.Should().NotBeEmpty();
        mockCompilationResult.CompilationTime.Should().BeLessThan(TimeSpan.FromSeconds(10));
        mockCompilationResult.CompilerLog.Should().NotBeNull();
        
        // Simulate PTX output
        var ptxString = System.Text.Encoding.UTF8.GetString(mockCompilationResult.CompiledCode);
        ptxString.Should().Contain(".version", "Mock PTX should contain version directive");
        ptxString.Should().Contain(".entry", "Mock PTX should contain entry directive");
        
        _output.WriteLine($"Mock compilation completed in {mockCompilationResult.CompilationTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"PTX size: {mockCompilationResult.CompiledCode.Length} bytes");
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_KernelCompilation_InvalidCode_ShouldSimulateErrors()
    {
        // Arrange
        var invalidKernelSource = "invalid cuda syntax {{{ this will fail";
        var mockCompilationResult = CreateMockCompilationResult(invalidKernelSource, false);

        // Act & Assert
        mockCompilationResult.Success.Should().BeFalse();
        mockCompilationResult.CompiledCode.Should().BeEmpty();
        mockCompilationResult.ErrorMessage.Should().NotBeNullOrEmpty();
        mockCompilationResult.CompilerLog.Should().Contain("error", "Compiler log should contain error information");
        
        _output.WriteLine($"Mock compilation failed as expected: {mockCompilationResult.ErrorMessage}");
    }

    [Theory]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    [InlineData(OptimizationLevel.None, 5000, 2000)] // Slower with no optimization
    [InlineData(OptimizationLevel.Default, 3000, 1500)] // Medium performance
    [InlineData(OptimizationLevel.Maximum, 2000, 1000)] // Fastest with max optimization
    public void MockDevice_OptimizationLevels_ShouldAffectPerformance(
        OptimizationLevel level, int expectedCompileTimeMs, int expectedExecutionTimeUs)
    {
        // Arrange
        var mockKernelSource = CreateComplexKernelSource();
        var mockCompilationResult = CreateMockCompilationResult(mockKernelSource, true, level);

        // Act & Assert
        mockCompilationResult.Success.Should().BeTrue();
        
        // Compilation time should vary with optimization level
        var actualCompileTime = mockCompilationResult.CompilationTime.TotalMilliseconds;
        actualCompileTime.Should().BeLessOrEqualTo(expectedCompileTimeMs + 1000, 
            $"Compilation with {level} should complete within expected time");

        // Simulated execution performance should improve with higher optimization
        var mockExecutionTime = SimulateKernelExecution(mockCompilationResult, level);
        mockExecutionTime.TotalMicroseconds.Should().BeLessOrEqualTo(expectedExecutionTimeUs + 500,
            $"Execution with {level} optimization should meet performance targets");
        
        _output.WriteLine($"Optimization {level}: Compile={actualCompileTime:F0}ms, Execute={mockExecutionTime.TotalMicroseconds:F0}μs");
    }

    [Fact]
    [Trait("Category", "Mock")]
    [Trait("Hardware", "CUDA")]
    public void MockDevice_ConcurrentOperations_ShouldSimulateParallelBehavior()
    {
        // Arrange
        const int concurrentOperations = 10;
        var mockOperations = Enumerable.Range(0, concurrentOperations)
            .Select(i => CreateMockAsyncOperation($"operation_{i}"))
            .ToArray();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = mockOperations.Select(op => op.Execute()).ToArray();
        Task.WaitAll(results);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(concurrentOperations);
        results.Should().AllSatisfy(result => result.Result.Should().BeTrue());
        
        // Concurrent execution should be faster than sequential
        var estimatedSequentialTime = concurrentOperations * 100; // 100ms per operation
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(estimatedSequentialTime * 0.8, 
            "Concurrent operations should be faster than sequential execution");
        
        _output.WriteLine($"Completed {concurrentOperations} concurrent operations in {stopwatch.ElapsedMilliseconds}ms");
    }

    // Helper Methods for Mock Creation
    private static CudaDeviceProperties CreateMockRTX2070Properties()
    {
        return new CudaDeviceProperties
        {
            Name = "Mock RTX 2070",
            Major = 7,
            Minor = 5,
            TotalGlobalMem = 8UL * 1024 * 1024 * 1024, // 8GB
            MultiProcessorCount = 36,
            MaxThreadsPerBlock = 1024,
            MaxThreadsPerMultiProcessor = 1024,
            WarpSize = 32,
            SharedMemPerBlock = 49152UL, // 48KB
            ConstMemSize = 65536UL, // 64KB
            L2CacheSize = 4 * 1024 * 1024, // 4MB
            ClockRate = 1620000, // 1.62 GHz
            MemoryClockRate = 7000000, // 7 GHz effective  
            MemoryBusWidth = 256,
            AsyncEngineCount = 2,
            UnifiedAddressing = 1,
            ManagedMemory = 1,
            ConcurrentKernels = 1,
            ECCEnabled = 0
        };
    }

    private static AcceleratorInfo CreateAcceleratorInfoFromMockProperties(CudaDeviceProperties props)
    {
        var capabilities = new Dictionary<string, object>
        {
            ["ComputeCapabilityMajor"] = props.Major,
            ["ComputeCapabilityMinor"] = props.Minor,
            ["SharedMemoryPerBlock"] = props.SharedMemPerBlock,
            ["ConstantMemory"] = props.ConstMemSize,
            ["L2CacheSize"] = props.L2CacheSize,
            ["MultiprocessorCount"] = props.MultiProcessorCount,
            ["MaxThreadsPerBlock"] = props.MaxThreadsPerBlock,
            ["MaxThreadsPerMultiprocessor"] = props.MaxThreadsPerMultiProcessor,
            ["WarpSize"] = props.WarpSize,
            ["AsyncEngineCount"] = props.AsyncEngineCount,
            ["UnifiedAddressing"] = props.UnifiedAddressing > 0,
            ["ManagedMemory"] = props.ManagedMemory > 0,
            ["ConcurrentKernels"] = props.ConcurrentKernels > 0,
            ["ECCEnabled"] = props.ECCEnabled > 0,
            ["ClockRate"] = props.ClockRate,
            ["MemoryClockRate"] = props.MemoryClockRate,
            ["MemoryBusWidth"] = props.MemoryBusWidth,
            ["MemoryBandwidth"] = 2.0 * props.MemoryClockRate * (props.MemoryBusWidth / 8) / 1.0e6
        };

        return new AcceleratorInfo(
            type: AcceleratorType.CUDA,
            name: props.Name,
            driverVersion: $"{props.Major}.{props.Minor}",
            memorySize: (long)props.TotalGlobalMem,
            computeUnits: props.MultiProcessorCount,
            maxClockFrequency: props.ClockRate / 1000,
            computeCapability: new Version(props.Major, props.Minor),
            maxSharedMemoryPerBlock: (long)props.SharedMemPerBlock,
            isUnifiedMemory: false
        )
        {
            Capabilities = capabilities
        };
    }

    private static string GetMockErrorString(CudaError error)
    {
        return error switch
        {
            CudaError.Success => "no error",
            CudaError.OutOfMemory => "out of memory",
            CudaError.InvalidDevice => "invalid device ordinal",
            CudaError.InvalidValue => "invalid argument",
            CudaError.LaunchFailure => "launch failure",
            CudaError.InvalidDevicePointer => "invalid device pointer",
            _ => $"mock error: {error.ToString().ToLowerInvariant()}"
        };
    }

    private static MockMemoryManager CreateMockMemoryManager()
    {
        return new MockMemoryManager
        {
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            AvailableMemory = 7L * 1024 * 1024 * 1024 // 7GB available
        };
    }

    private static MockMemoryBuffer CreateMockMemoryBuffer(long size)
    {
        return new MockMemoryBuffer
        {
            SizeInBytes = size,
            IsDisposed = false,
            AllocationTime = DateTime.UtcNow
        };
    }

    private static MockAllocationResult SimulateAllocation(MockMemoryManager manager, long size)
    {
        var latencyMs = size switch
        {
            <= 1024 => 0.1, // Very small allocations are fast
            <= 1024 * 1024 => 1.0, // Medium allocations
            _ => size / (1024.0 * 1024.0 * 1024.0) * 50 // Large allocations scale with size
        };

        return new MockAllocationResult
        {
            Success = size <= manager.AvailableMemory,
            AllocatedSize = size,
            AllocationTime = TimeSpan.FromMilliseconds(latencyMs),
            ErrorMessage = size > manager.AvailableMemory ? "Insufficient memory" : null
        };
    }

    private static MockCompilationResult CreateMockCompilationResult(
        string sourceCode, bool success, OptimizationLevel optimization = OptimizationLevel.Default)
    {
        if (!success)
        {
            return new MockCompilationResult
            {
                Success = false,
                CompiledCode = Array.Empty<byte>(),
                CompilationTime = TimeSpan.FromMilliseconds(500),
                ErrorMessage = "Compilation failed: syntax error",
                CompilerLog = "error: expected ';' before '{' token"
            };
        }

        // Simulate compilation time based on optimization level
        var baseCompileTimeMs = optimization switch
        {
            OptimizationLevel.None => 1000,
            OptimizationLevel.Default => 2000,
            OptimizationLevel.Maximum => 4000,
            _ => 2000
        };

        // Add complexity factor based on source code size
        var complexityMs = sourceCode.Length / 10;
        var totalCompileTimeMs = baseCompileTimeMs + complexityMs;

        // Generate mock PTX
        var mockPtx = GenerateMockPTX(sourceCode, optimization);

        return new MockCompilationResult
        {
            Success = true,
            CompiledCode = System.Text.Encoding.UTF8.GetBytes(mockPtx),
            CompilationTime = TimeSpan.FromMilliseconds(totalCompileTimeMs),
            CompilerLog = $"Compilation successful with {optimization} optimization",
            ErrorMessage = null
        };
    }

    private static string CreateComplexKernelSource()
    {
        return @"
__global__ void complex_kernel(float* input, float* output, float* temp, int n)
{
    __shared__ float shared_data[256];
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int tid = threadIdx.x;
    
    if (idx < n) {
        shared_data[tid] = input[idx];
    }
    
    __syncthreads();
    
    if (idx < n) {
        float sum = 0.0f;
        for (int i = 0; i < blockDim.x; i++) {
            sum += shared_data[i] * sinf(shared_data[i]) * cosf(shared_data[i]);
        }
        temp[idx] = sum;
        output[idx] = expf(temp[idx]) / (1.0f + expf(temp[idx])); // sigmoid
    }
}";
    }

    private static TimeSpan SimulateKernelExecution(MockCompilationResult compilation, OptimizationLevel optimization)
    {
        var baseExecutionUs = optimization switch
        {
            OptimizationLevel.None => 2000,
            OptimizationLevel.Default => 1500,
            OptimizationLevel.Maximum => 1000,
            _ => 1500
        };

        return TimeSpan.FromMicroseconds(baseExecutionUs);
    }

    private static MockAsyncOperation CreateMockAsyncOperation(string name)
    {
        return new MockAsyncOperation
        {
            Name = name,
            ExecutionTimeMs = 100 + new Random().Next(0, 100) // 100-200ms
        };
    }

    private static string GenerateMockPTX(string sourceCode, OptimizationLevel optimization)
    {
        var ptxBuilder = new StringBuilder();
        
        ptxBuilder.AppendLine(".version 7.5");
        ptxBuilder.AppendLine(".target sm_75");
        ptxBuilder.AppendLine(".address_size 64");
        ptxBuilder.AppendLine();
        
        ptxBuilder.AppendLine($"// Generated from source code ({sourceCode.Length} chars)");
        ptxBuilder.AppendLine($"// Optimization level: {optimization}");
        ptxBuilder.AppendLine();
        
        ptxBuilder.AppendLine(".visible .entry mock_kernel(");
        ptxBuilder.AppendLine("    .param .u64 mock_kernel_param_0,");
        ptxBuilder.AppendLine("    .param .u64 mock_kernel_param_1,");
        ptxBuilder.AppendLine("    .param .u32 mock_kernel_param_2");
        ptxBuilder.AppendLine(")");
        ptxBuilder.AppendLine("{");
        ptxBuilder.AppendLine("    .reg .pred %p<2>;");
        ptxBuilder.AppendLine("    .reg .f32 %f<4>;");
        ptxBuilder.AppendLine("    .reg .b32 %r<8>;");
        ptxBuilder.AppendLine("    .reg .b64 %rd<8>;");
        ptxBuilder.AppendLine();
        ptxBuilder.AppendLine("    // Mock PTX instructions");
        ptxBuilder.AppendLine("    mov.u32 %r1, %ctaid.x;");
        ptxBuilder.AppendLine("    mov.u32 %r2, %ntid.x;");
        ptxBuilder.AppendLine("    mad.lo.s32 %r3, %r1, %r2, %tid.x;");
        ptxBuilder.AppendLine("    ld.param.u32 %r4, [mock_kernel_param_2];");
        ptxBuilder.AppendLine("    setp.ge.s32 %p1, %r3, %r4;");
        ptxBuilder.AppendLine("    @%p1 bra LBB0_2;");
        ptxBuilder.AppendLine();
        ptxBuilder.AppendLine("    // Mock computation");
        ptxBuilder.AppendLine("    ld.param.u64 %rd1, [mock_kernel_param_0];");
        ptxBuilder.AppendLine("    mul.wide.u32 %rd2, %r3, 4;");
        ptxBuilder.AppendLine("    add.s64 %rd3, %rd1, %rd2;");
        ptxBuilder.AppendLine("    ld.global.f32 %f1, [%rd3];");
        ptxBuilder.AppendLine("    mul.f32 %f2, %f1, 0f40000000;");
        ptxBuilder.AppendLine("    ld.param.u64 %rd4, [mock_kernel_param_1];");
        ptxBuilder.AppendLine("    add.s64 %rd5, %rd4, %rd2;");
        ptxBuilder.AppendLine("    st.global.f32 [%rd5], %f2;");
        ptxBuilder.AppendLine();
        ptxBuilder.AppendLine("LBB0_2:");
        ptxBuilder.AppendLine("    ret;");
        ptxBuilder.AppendLine("}");
        
        return ptxBuilder.ToString();
    }

    // Mock Data Structures
    private class MockMemoryManager
    {
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
    }

    private class MockMemoryBuffer
    {
        public long SizeInBytes { get; set; }
        public bool IsDisposed { get; set; }
        public DateTime AllocationTime { get; set; }
    }

    private class MockAllocationResult
    {
        public bool Success { get; set; }
        public long AllocatedSize { get; set; }
        public TimeSpan AllocationTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private class MockCompilationResult
    {
        public bool Success { get; set; }
        public byte[] CompiledCode { get; set; } = Array.Empty<byte>();
        public TimeSpan CompilationTime { get; set; }
        public string CompilerLog { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    private class MockAsyncOperation
    {
        public string Name { get; set; } = string.Empty;
        public int ExecutionTimeMs { get; set; }

        public Task<bool> Execute()
        {
            return Task.Run(async () =>
            {
                await Task.Delay(ExecutionTimeMs);
                return true;
            });
        }
    }
}
