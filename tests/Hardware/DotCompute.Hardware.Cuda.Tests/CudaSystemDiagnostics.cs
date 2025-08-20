// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware;


/// <summary>
/// Comprehensive system diagnostics for CUDA backend
/// </summary>
[Collection("Hardware")]
public sealed class CudaSystemDiagnostics : IDisposable
{
    private readonly ILogger<CudaSystemDiagnostics> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CudaBackend? _backend;
    private readonly CudaAccelerator? _accelerator;
    private bool _disposed;

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, Exception?> LogCudaRuntimeSystemDiagnostics =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(LogCudaRuntimeSystemDiagnostics)),
            "=== CUDA Runtime System Diagnostics ===");

    private static readonly Action<ILogger, int, int, Exception?> LogCudaRuntimeVersion =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogCudaRuntimeVersion)),
            "CUDA Runtime Version: {Major}.{Minor}");

    private static readonly Action<ILogger, int, int, Exception?> LogCudaDriverVersion =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(3, nameof(LogCudaDriverVersion)),
            "CUDA Driver Version: {Major}.{Minor}");

    private static readonly Action<ILogger, int, Exception?> LogCudaDevicesFound =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4, nameof(LogCudaDevicesFound)),
            "CUDA Devices Found: {DeviceCount}");

    private static readonly Action<ILogger, int, string, Exception?> LogDeviceInfo =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(5, nameof(LogDeviceInfo)),
            "Device {Id}: {Name}");

    private static readonly Action<ILogger, int, int, Exception?> LogComputeCapability =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(6, nameof(LogComputeCapability)),
            "  Compute Capability: {Major}.{Minor}");

    private static readonly Action<ILogger, ulong, double, Exception?> LogGlobalMemory =
        LoggerMessage.Define<ulong, double>(
            LogLevel.Information,
            new EventId(7, nameof(LogGlobalMemory)),
            "  Global Memory: {Memory:N0} bytes ({MemoryGB:F1} GB)");

    private static readonly Action<ILogger, int, Exception?> LogMultiprocessors =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(8, nameof(LogMultiprocessors)),
            "  Multiprocessors: {SMs}");

    private static readonly Action<ILogger, int, Exception?> LogMaxThreadsPerBlock =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(9, nameof(LogMaxThreadsPerBlock)),
            "  Max Threads per Block: {MaxThreads}");

    private static readonly Action<ILogger, ulong, Exception?> LogSharedMemoryPerBlock =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(10, nameof(LogSharedMemoryPerBlock)),
            "  Shared Memory per Block: {SharedMem:N0} bytes");

    private static readonly Action<ILogger, int, Exception?> LogWarpSize =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(11, nameof(LogWarpSize)),
            "  Warp Size: {WarpSize}");

    private static readonly Action<ILogger, int, Exception?> LogClockRate =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(12, nameof(LogClockRate)),
            "  Clock Rate: {ClockRate} kHz");

    private static readonly Action<ILogger, int, Exception?> LogMemoryClock =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(13, nameof(LogMemoryClock)),
            "  Memory Clock: {MemoryClock} kHz");

    private static readonly Action<ILogger, int, Exception?> LogMemoryBusWidth =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(14, nameof(LogMemoryBusWidth)),
            "  Memory Bus Width: {BusWidth} bits");

    private static readonly Action<ILogger, bool, Exception?> LogEccEnabled =
        LoggerMessage.Define<bool>(
            LogLevel.Information,
            new EventId(15, nameof(LogEccEnabled)),
            "  ECC Enabled: {ECC}");

    private static readonly Action<ILogger, bool, Exception?> LogUnifiedAddressing =
        LoggerMessage.Define<bool>(
            LogLevel.Information,
            new EventId(16, nameof(LogUnifiedAddressing)),
            "  Unified Addressing: {UVA}");

    private static readonly Action<ILogger, bool, Exception?> LogConcurrentKernels =
        LoggerMessage.Define<bool>(
            LogLevel.Information,
            new EventId(17, nameof(LogConcurrentKernels)),
            "  Concurrent Kernels: {ConcurrentKernels}");

    private static readonly Action<ILogger, int, int, Exception?> LogNvrtcAvailable =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(18, nameof(LogNvrtcAvailable)),
            "NVRTC Available: Version {Major}.{Minor}");

    private static readonly Action<ILogger, Exception?> LogNvrtcNotAvailable =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(19, nameof(LogNvrtcNotAvailable)),
            "NVRTC Not Available - kernel compilation may not work");

    private static readonly Action<ILogger, Exception?> LogAcceleratorInformation =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(20, nameof(LogAcceleratorInformation)),
            "=== Accelerator Information ===");

    private static readonly Action<ILogger, string, Exception?> LogAcceleratorType =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(21, nameof(LogAcceleratorType)),
            "Accelerator Type: {Type}");

    private static readonly Action<ILogger, string, Exception?> LogDeviceName =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(22, nameof(LogDeviceName)),
            "Device Name: {Name}");

    private static readonly Action<ILogger, string, Exception?> LogDriverVersion =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(23, nameof(LogDriverVersion)),
            "Driver Version: {Version}");

    private static readonly Action<ILogger, long, Exception?> LogTotalMemory =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(24, nameof(LogTotalMemory)),
            "Total Memory: {Memory:N0} bytes");

    private static readonly Action<ILogger, int, Exception?> LogComputeUnits =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(25, nameof(LogComputeUnits)),
            "Compute Units: {Units}");

    private static readonly Action<ILogger, int, Exception?> LogMaxClock =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(26, nameof(LogMaxClock)),
            "Max Clock: {Clock} MHz");

    private static readonly Action<ILogger, string, Exception?> LogComputeCapabilityInfo =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(27, nameof(LogComputeCapabilityInfo)),
            "Compute Capability: {Capability}");

    private static readonly Action<ILogger, bool, Exception?> LogUnifiedMemory =
        LoggerMessage.Define<bool>(
            LogLevel.Information,
            new EventId(28, nameof(LogUnifiedMemory)),
            "Unified Memory: {Unified}");

    private static readonly Action<ILogger, string, string, Exception?> LogCapability =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(29, nameof(LogCapability)),
            "  {Capability}: {Value}");

    private static readonly Action<ILogger, Exception?> LogMemoryManagerDiagnostics =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(30, nameof(LogMemoryManagerDiagnostics)),
            "=== Memory Manager Diagnostics ===");

    private static readonly Action<ILogger, Exception?> LogMemoryManagerTest =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(31, nameof(LogMemoryManagerTest)),
            "Memory Manager Test - Testing various allocation sizes");

    private static readonly Action<ILogger, long, Exception?> LogAllocatingSize =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(32, nameof(LogAllocatingSize)),
            "Allocating {Size:N0} bytes");

    private static readonly Action<ILogger, long, Exception?> LogCopyOperationsVerified =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(33, nameof(LogCopyOperationsVerified)),
            "  Copy operations verified for {Size:N0} bytes");

    private static readonly Action<ILogger, Exception?> LogFillOperationSkipped =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(34, nameof(LogFillOperationSkipped)),
            "  Fill operation test skipped (not available in new API)");

    private static readonly Action<ILogger, Exception?> LogSlicingVerified =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(35, nameof(LogSlicingVerified)),
            "  Slicing verified");

    private static readonly Action<ILogger, Exception?> LogMemoryAllocationTestsCompleted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(36, nameof(LogMemoryAllocationTestsCompleted)),
            "Memory allocation tests completed successfully");

    private static readonly Action<ILogger, Exception?> LogKernelCompilerDiagnostics =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(37, nameof(LogKernelCompilerDiagnostics)),
            "=== Kernel Compiler Diagnostics ===");

    private static readonly Action<ILogger, string, Exception?> LogCompilingWithOptimization =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(38, nameof(LogCompilingWithOptimization)),
            "Compiling with optimization level: {Level}");

    private static readonly Action<ILogger, long, Exception?> LogCompilationSuccessful =
        LoggerMessage.Define<long>(
            LogLevel.Information,
            new EventId(39, nameof(LogCompilationSuccessful)),
            "  Compilation successful in {Time}ms");

    private static readonly Action<ILogger, Exception?> LogExecutionAndVerificationSuccessful =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(40, nameof(LogExecutionAndVerificationSuccessful)),
            "  Execution and verification successful");

    private static readonly Action<ILogger, Exception?> LogLaunchConfigurationDiagnostics =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(41, nameof(LogLaunchConfigurationDiagnostics)),
            "=== Launch Configuration Diagnostics ===");

    private static readonly Action<ILogger, int, Exception?> LogProblemSize =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(42, nameof(LogProblemSize)),
            "Problem Size {Size:N0}:");

    private static readonly Action<ILogger, uint, uint, uint, Exception?> LogGrid =
        LoggerMessage.Define<uint, uint, uint>(
            LogLevel.Information,
            new EventId(43, nameof(LogGrid)),
            "  Grid: ({X}, {Y}, {Z})");

    private static readonly Action<ILogger, uint, uint, uint, Exception?> LogBlock =
        LoggerMessage.Define<uint, uint, uint>(
            LogLevel.Information,
            new EventId(44, nameof(LogBlock)),
            "  Block: ({X}, {Y}, {Z})");

    private static readonly Action<ILogger, ulong, Exception?> LogTotalThreads =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(45, nameof(LogTotalThreads)),
            "  Total Threads: {Threads:N0}");

    private static readonly Action<ILogger, Exception?> LogExecutionSuccessfulAndVerified =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(46, nameof(LogExecutionSuccessfulAndVerified)),
            "  Execution successful and verified");

    private static readonly Action<ILogger, Exception?> LogErrorHandlingDiagnostics =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(47, nameof(LogErrorHandlingDiagnostics)),
            "=== Error Handling Diagnostics ===");

    private static readonly Action<ILogger, string, Exception?> LogCompilationErrorHandled =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(48, nameof(LogCompilationErrorHandled)),
            "Compilation error handled correctly: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogMemoryAllocationErrorHandled =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(49, nameof(LogMemoryAllocationErrorHandled)),
            "Memory allocation error handled correctly: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogExecutionErrorHandled =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(50, nameof(LogExecutionErrorHandled)),
            "Execution error handled correctly: {Message}");

    public CudaSystemDiagnostics(ITestOutputHelper output)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Debug));

        _logger = _loggerFactory.CreateLogger<CudaSystemDiagnostics>();

        if (CudaBackend.IsAvailable())
        {
            _backend = new CudaBackend(_loggerFactory.CreateLogger<CudaBackend>());
            _accelerator = _backend.GetDefaultAccelerator();
        }
    }

    [SkippableFact]
    public void CudaRuntime_SystemDiagnostics()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        LogCudaRuntimeSystemDiagnostics(_logger, null);

        // 1. Driver and Runtime Versions
        var runtimeResult = CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion);
        var driverResult = CudaRuntime.cudaDriverGetVersion(out var driverVersion);

        if (runtimeResult == CudaError.Success)
        {
            var runtimeMajor = runtimeVersion / 1000;
            var runtimeMinor = (runtimeVersion % 1000) / 10;
            LogCudaRuntimeVersion(_logger, runtimeMajor, runtimeMinor, null);
        }

        if (driverResult == CudaError.Success)
        {
            var driverMajor = driverVersion / 1000;
            var driverMinor = (driverVersion % 1000) / 10;
            LogCudaDriverVersion(_logger, driverMajor, driverMinor, null);
        }

        // 2. Device Count and Properties
        var deviceCountResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
        Assert.Equal(CudaError.Success, deviceCountResult);
        LogCudaDevicesFound(_logger, deviceCount, null);

        for (var i = 0; i < deviceCount; i++)
        {
            var props = new CudaDeviceProperties();
            var propResult = CudaRuntime.cudaGetDeviceProperties(ref props, i);

            if (propResult == CudaError.Success)
            {
                LogDeviceInfo(_logger, i, props.Name, null);
                LogComputeCapability(_logger, props.Major, props.Minor, null);
                LogGlobalMemory(_logger, props.TotalGlobalMem, props.TotalGlobalMem / (1024.0 * 1024 * 1024), null);
                LogMultiprocessors(_logger, props.MultiProcessorCount, null);
                LogMaxThreadsPerBlock(_logger, props.MaxThreadsPerBlock, null);
                LogSharedMemoryPerBlock(_logger, props.SharedMemPerBlock, null);
                LogWarpSize(_logger, props.WarpSize, null);
                LogClockRate(_logger, props.ClockRate, null);
                LogMemoryClock(_logger, props.MemoryClockRate, null);
                LogMemoryBusWidth(_logger, props.MemoryBusWidth, null);
                LogEccEnabled(_logger, props.ECCEnabled > 0, null);
                LogUnifiedAddressing(_logger, props.UnifiedAddressing > 0, null);
                LogConcurrentKernels(_logger, props.ConcurrentKernels > 0, null);
            }
        }

        // 3. NVRTC Availability
        if (CudaKernelCompiler.IsNvrtcAvailable())
        {
            var (nvrtcMajor, nvrtcMinor) = CudaKernelCompiler.GetNvrtcVersion();
            LogNvrtcAvailable(_logger, nvrtcMajor, nvrtcMinor, null);
        }
        else
        {
            LogNvrtcNotAvailable(_logger, null);
        }
    }

    [SkippableFact]
    public void AcceleratorInfo_ShouldBeComplete()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        LogAcceleratorInformation(_logger, null);

        var info = _accelerator.Info;

        Assert.NotNull(info);
        Assert.Equal("CUDA", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(info.TotalMemory > 0);
        Assert.True(info.ComputeUnits > 0);
        Assert.NotNull(info.ComputeCapability);
        Assert.NotNull(info.Capabilities);

        LogAcceleratorType(_logger, info.Type, null);
        LogDeviceName(_logger, info.Name, null);
        LogDriverVersion(_logger, info.DriverVersion ?? "Unknown", null);
        LogTotalMemory(_logger, info.TotalMemory, null);
        LogComputeUnits(_logger, info.ComputeUnits, null);
        LogMaxClock(_logger, info.MaxClockFrequency, null);
        LogComputeCapabilityInfo(_logger, info.ComputeCapability?.ToString() ?? "Unknown", null);
        LogUnifiedMemory(_logger, info.IsUnifiedMemory, null);

        // Validate specific capabilities
        var caps = info.Capabilities;
        var expectedCapabilities = new[]
        {
        "ComputeCapabilityMajor", "ComputeCapabilityMinor", "SharedMemoryPerBlock",
        "ConstantMemory", "MultiprocessorCount", "MaxThreadsPerBlock",
        "WarpSize", "ClockRate", "MemoryClockRate"
    };

        foreach (var expectedCap in expectedCapabilities)
        {
            Assert.True(caps.ContainsKey(expectedCap), $"Missing capability: {expectedCap}");
            LogCapability(_logger, expectedCap, caps[expectedCap]?.ToString() ?? "Unknown", null);
        }
    }

    [SkippableFact]
    public async Task MemoryManager_ShouldHandleAllOperations()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        LogMemoryManagerDiagnostics(_logger, null);

        var memory = _accelerator.Memory;
        // Note: GetStatistics() method doesn't exist in new IMemoryManager interface

        LogMemoryManagerTest(_logger, null);

        // Test various allocation sizes
        var testSizes = new[] { 1024, 1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024 };
        var buffers = new List<IMemoryBuffer>();

        try
        {
            foreach (var size in testSizes)
            {
                LogAllocatingSize(_logger, size, null);
                var buffer = await memory.AllocateAsync(size);
                buffers.Add(buffer);

                // Test memory operations
                var testData = new byte[Math.Min(size, 1024)];
                new Random().NextBytes(testData);

                await buffer.CopyFromHostAsync<byte>(testData);
                var readBack = new byte[testData.Length];
                await buffer.CopyToHostAsync<byte>(readBack);

                Assert.Equal(testData, readBack);
                LogCopyOperationsVerified(_logger, size, null);

                // Test fill operation - this would need to be implemented differently
                // Fill operation is not part of the new IMemoryBuffer interface
                LogFillOperationSkipped(_logger, null);

                // Test slicing
                if (size > 2048)
                {
                    var slice = memory.CreateView(buffer, 1024, 1024);
                    Assert.Equal(1024, slice.SizeInBytes);
                    LogSlicingVerified(_logger, null);
                }
            }

            LogMemoryAllocationTestsCompleted(_logger, null);
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    public async Task KernelCompiler_ShouldHandleAllSourceTypes()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        LogKernelCompilerDiagnostics(_logger, null);

        // Test CUDA source compilation
        var cudaSource = @"
extern ""C"" __global__ void testKernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        output[idx] = input[idx] * 2.0f + 1.0f;
    }
}";

        var kernelSource = new TextKernelSource(cudaSource, "testKernel", DotCompute.Abstractions.KernelLanguage.Cuda, "testKernel");
        var definition = new KernelDefinition("testKernel", kernelSource.Code, kernelSource.EntryPoint);

        // Test different optimization levels
        var optimizationLevels = Enum.GetValues<OptimizationLevel>();

        foreach (var optLevel in optimizationLevels)
        {
            var options = new CompilationOptions
            {
                OptimizationLevel = optLevel,
                EnableDebugInfo = optLevel == OptimizationLevel.None
            };

            LogCompilingWithOptimization(_logger, optLevel.ToString(), null);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var compiledKernel = await _accelerator.CompileKernelAsync(definition, options);
            stopwatch.Stop();

            Assert.NotNull(compiledKernel);
            LogCompilationSuccessful(_logger, stopwatch.ElapsedMilliseconds, null);

            // Test execution
            const int N = 1000;
            var input = Enumerable.Range(0, N).Select(i => (float)i).ToArray();
            var output = new float[N];

            var inputBuffer = await _accelerator.Memory.AllocateAsync(N * sizeof(float));
            var outputBuffer = await _accelerator.Memory.AllocateAsync(N * sizeof(float));

            try
            {
                await inputBuffer.CopyFromHostAsync<float>(input);

                var arguments = new KernelArguments(inputBuffer, outputBuffer, N);
                await compiledKernel.ExecuteAsync(arguments);

                await outputBuffer.CopyToHostAsync<float>(output);

                // Verify results
                for (var i = 0; i < N; i++)
                {
                    var expected = input[i] * 2.0f + 1.0f;
                    Assert.True(Math.Abs(output[i] - expected) < 0.001f,
                        $"Incorrect result at {i}: expected {expected}, got {output[i]}");
                }

                LogExecutionAndVerificationSuccessful(_logger, null);
            }
            finally
            {
                await inputBuffer.DisposeAsync();
                await outputBuffer.DisposeAsync();
                await compiledKernel.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    public async Task LaunchConfiguration_ShouldOptimizeForDevice()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        LogLaunchConfigurationDiagnostics(_logger, null);

        var kernelSource = @"
extern ""C"" __global__ void configTest(int* data, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        data[idx] = blockIdx.x * 1000 + threadIdx.x;
    }
}";

        var kernelSourceObj = new TextKernelSource(kernelSource, "configTest", DotCompute.Abstractions.KernelLanguage.Cuda, "configTest");
        var definition = new KernelDefinition("configTest", kernelSourceObj.Code, kernelSourceObj.EntryPoint);
        var compiledKernel = await _accelerator.CompileKernelAsync(definition) as CudaCompiledKernel;
        Assert.NotNull(compiledKernel);

        try
        {
            // Test different problem sizes
            var problemSizes = new[] { 1000, 10000, 100000, 1000000 };

            foreach (var problemSize in problemSizes)
            {
                var config = compiledKernel.GetOptimalLaunchConfig(problemSize);

                LogProblemSize(_logger, problemSize, null);
                LogGrid(_logger, config.GridX, config.GridY, config.GridZ, null);
                LogBlock(_logger, config.BlockX, config.BlockY, config.BlockZ, null);
                LogTotalThreads(_logger, (ulong)config.GridX * config.GridY * config.GridZ * config.BlockX * config.BlockY * config.BlockZ, null);

                // Verify configuration covers the problem
                var totalThreads = config.GridX * config.BlockX;
                _ = (totalThreads >= problemSize).Should().BeTrue(
                    $"Configuration doesn't cover problem size: {totalThreads} < {problemSize}");

                // Test execution with this configuration
                var data = new int[problemSize];
                var buffer = await _accelerator.Memory.AllocateAsync(problemSize * sizeof(int));

                try
                {
                    var arguments = new KernelArguments(buffer, problemSize);
                    await compiledKernel.ExecuteWithConfigAsync(arguments, config);

                    await buffer.CopyToHostAsync<int>(data);

                    // Verify some results(first few elements)
                    for (var i = 0; i < Math.Min(100, problemSize); i++)
                    {
                        var expectedBlock = i / (int)config.BlockX;
                        var expectedThread = i % (int)config.BlockX;
                        var expected = expectedBlock * 1000 + expectedThread;

                        Assert.Equal(expected, data[i]);
                    }

                    LogExecutionSuccessfulAndVerified(_logger, null);
                }
                finally
                {
                    await buffer.DisposeAsync();
                }
            }
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ErrorHandling_ShouldProvideDetailedInformation()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        LogErrorHandlingDiagnostics(_logger, null);

        // Test compilation error handling
        var invalidKernelSource = @"
extern ""C"" __global__ void invalidKernel(float* data)
{
    undeclared_variable = data[threadIdx.x]; // This should cause compilation error
}";

        var kernelSourceObj = new TextKernelSource(invalidKernelSource, "invalidKernel", DotCompute.Abstractions.KernelLanguage.Cuda, "invalidKernel");
        var definition = new KernelDefinition("invalidKernel", kernelSourceObj.Code, kernelSourceObj.EntryPoint);

        var compilationException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _accelerator.CompileKernelAsync(definition));

        Assert.NotNull(compilationException);
        Assert.Contains("Failed to compile", compilationException.Message, StringComparison.Ordinal);
        LogCompilationErrorHandled(_logger, compilationException.Message, null);

        // Test memory allocation error handling(try to allocate very large amount)
        var oversizeAllocation = long.MaxValue / 2; // Very large allocation

        var memoryException = await Assert.ThrowsAsync<OutOfMemoryException>(
            async () => await _accelerator.Memory.AllocateAsync(oversizeAllocation));

        Assert.NotNull(memoryException);
        LogMemoryAllocationErrorHandled(_logger, memoryException.Message, null);

        // Test execution error handling(null arguments)
        var validSource = @"extern ""C"" __global__ void validKernel(float* data, int n) { }";
        var validKernelSource = new TextKernelSource(validSource, "validKernel", DotCompute.Abstractions.KernelLanguage.Cuda, "validKernel");
        var validDefinition = new KernelDefinition("validKernel", validKernelSource.Code, validKernelSource.EntryPoint);
        var validKernel = await _accelerator.CompileKernelAsync(validDefinition);

        try
        {
            var executionException = await Assert.ThrowsAsync<ArgumentException>(
                async () => await validKernel.ExecuteAsync(new KernelArguments()));

            Assert.NotNull(executionException);
            LogExecutionErrorHandled(_logger, executionException.Message, null);
        }
        finally
        {
            await validKernel.DisposeAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _accelerator?.Dispose();
        _backend?.Dispose();
        _loggerFactory?.Dispose();
        _disposed = true;
    }
}
