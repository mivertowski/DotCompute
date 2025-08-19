using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware.StressTests;


/// <summary>
/// Comprehensive stress tests for RTX 2000 Ada Generation GPU.
/// Tests system stability, thermal behavior, and error recovery under extreme conditions.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "StressTest")]
[Trait("Category", "RequiresGPU")]
[Collection("Hardware Stress Tests")] // Serialize stress tests
public sealed class HardwareStressTests : IDisposable
{
    private readonly ITestOutputHelper _output;
#pragma warning disable CA1823 // Unused field - Logger for future use
    private static readonly ILogger Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    // Logger messages
    private static readonly Action<ILogger, string, Exception?> LogStressTest =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(7001), "Stress test: {TestName}");
#pragma warning restore CA1823
    private IntPtr _cudaContext;
    private bool _cudaInitialized;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public HardwareStressTests(ITestOutputHelper output)
    {
        _output = output;
        _cancellationTokenSource = new CancellationTokenSource();
        InitializeCuda();
    }

    private void InitializeCuda()
    {
        try
        {
            var result = CudaInit(0);
            if (result == 0)
            {
                result = CudaCtxCreate(ref _cudaContext, 0, 0);
                if (result == 0)
                {
                    _cudaInitialized = true;
                    _output.WriteLine("CUDA context initialized for stress testing");
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"CUDA initialization failed: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task MemoryAllocationStress_ShouldHandleExtremeAllocations()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        const int maxAllocations = 1000;
        const int baseAllocationSizeMB = 32;
        var allocations = new List<IntPtr>();
        var allocationSizes = new List<long>();
        var random = new Random(42);

        _output.WriteLine($"Starting memory allocation stress test with up to {maxAllocations} allocations");

        try
        {
            // Phase 1: Progressive allocation until memory exhaustion
            var successfulAllocations = 0;
            var sw = Stopwatch.StartNew();

            for (var i = 0; i < maxAllocations; i++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                // Vary allocation sizes to stress the memory allocator
                var sizeMB = baseAllocationSizeMB + random.Next(0, baseAllocationSizeMB * 2);
                var sizeBytes = sizeMB * 1024 * 1024;

                var devicePtr = IntPtr.Zero;
                var result = CudaMalloc(ref devicePtr, sizeBytes);

                if (result == 0)
                {
                    allocations.Add(devicePtr);
                    allocationSizes.Add(sizeBytes);
                    successfulAllocations++;

                    // Log progress every 50 allocations
                    if ((successfulAllocations % 50) == 0)
                    {
                        var totalAllocatedMB = allocationSizes.Sum() / (1024 * 1024);
                        _output.WriteLine($"Allocated {successfulAllocations} buffers, total: {totalAllocatedMB} MB");
                    }
                }
                else
                {
                    _output.WriteLine($"Allocation failed at iteration {i} with error code: {result}");
                    break;
                }
            }

            sw.Stop();
            var totalAllocatedGB = allocationSizes.Sum() / (1024.0 * 1024.0 * 1024.0);
            var allocationRate = successfulAllocations / sw.Elapsed.TotalSeconds;

            _output.WriteLine($"Allocation phase completed:");
            _output.WriteLine($"  Successful allocations: {successfulAllocations}");
            _output.WriteLine($"  Total allocated: {totalAllocatedGB:F2} GB");
            _output.WriteLine($"  Allocation rate: {allocationRate:F2} allocations/sec");
            _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds} ms");

            _ = successfulAllocations.Should().BeGreaterThan(50, "Should be able to perform substantial allocations");
            _ = totalAllocatedGB.Should().BeGreaterThan(4.0, "Should allocate several GB before exhaustion");

            // Phase 2: Random deallocation and reallocation
            _output.WriteLine("Starting random deallocation/reallocation phase...");

            var deallocationsCount = 0;
            var reallocationsCount = 0;
            sw.Restart();

            for (var cycle = 0; cycle < 100; cycle++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                // Randomly deallocate some buffers
                var deallocationCount = Math.Min(10, allocations.Count / 4);
                for (var i = 0; i < deallocationCount && allocations.Count > 0; i++)
                {
                    var index = random.Next(allocations.Count);
                    var result = CudaFree(allocations[index]);

                    if (result == 0)
                    {
                        allocations.RemoveAt(index);
                        allocationSizes.RemoveAt(index);
                        deallocationsCount++;
                    }
                }

                // Try to reallocate
                for (var i = 0; i < deallocationCount; i++)
                {
                    var sizeMB = baseAllocationSizeMB + random.Next(0, baseAllocationSizeMB);
                    var sizeBytes = sizeMB * 1024 * 1024;

                    var devicePtr = IntPtr.Zero;
                    var result = CudaMalloc(ref devicePtr, sizeBytes);

                    if (result == 0)
                    {
                        allocations.Add(devicePtr);
                        allocationSizes.Add(sizeBytes);
                        reallocationsCount++;
                    }
                }
            }

            sw.Stop();
            _output.WriteLine($"Random allocation/deallocation phase completed:");
            _output.WriteLine($"  Deallocations: {deallocationsCount}");
            _output.WriteLine($"  Reallocations: {reallocationsCount}");
            _output.WriteLine($"  Final allocation count: {allocations.Count}");
            _output.WriteLine($"  Phase duration: {sw.ElapsedMilliseconds} ms");

            _ = deallocationsCount.Should().BeGreaterThan(0, "Should successfully deallocate memory");
            _ = reallocationsCount.Should().BeGreaterThan(0, "Should successfully reallocate memory after fragmentation");
        }
        finally
        {
            // Cleanup all remaining allocations
            _output.WriteLine("Cleaning up remaining allocations...");
            foreach (var ptr in allocations)
            {
                _ = CudaFree(ptr);
            }
            _output.WriteLine($"Cleaned up {allocations.Count} allocations");
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task ThermalStressTest_ShouldMaintainStability()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        const int stressTestDurationMs = 30000; // 30 seconds
        const int dataSize = 1024 * 1024; // 1M elements
        const int concurrentKernels = 4;

        _output.WriteLine($"Starting thermal stress test for {stressTestDurationMs / 1000} seconds");

        var stressKernel = @"
extern ""C"" __global__ void thermalStress(float* data, int n, int iterations)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if(idx < n) {
        float val = data[idx];
        
        // High-intensity compute to generate heat
        for(int i = 0; i < iterations; i++) {
            val = val * val + sqrtf(fabsf(val)) - sinf(val) + cosf(val * 2.0f);
            val = powf(val, 1.01f) + tanhf(val * 0.5f) - expf(val * 0.01f);
            val = fmaf(val, 0.99f, 0.01f);
            
            // Prevent overflow
            if(fabsf(val) > 1e6f) val *= 0.5f;
        }
        
        data[idx] = val;
    }
}";

        IntPtr program = IntPtr.Zero, module = IntPtr.Zero, kernel = IntPtr.Zero;
        var deviceBuffers = new IntPtr[concurrentKernels];
        var hostBuffers = new float[concurrentKernels][];

        try
        {
            // Compile stress kernel
            var result = NvrtcCreateProgram(ref program, stressKernel, "stress.cu", 0, null!, null!);
            Assert.Equal(0, result); // Stress kernel compilation should succeed;

            var options = new[] { "--gpu-architecture=compute_89", "--use_fast_math" };
            result = NvrtcCompileProgram(program, options.Length, options);
            Assert.Equal(0, result); // Kernel compilation should succeed;

            long ptxSize = 0;
            _ = NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            _ = NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            Assert.Equal(0, result); // Module loading should succeed;

            var kernelName = System.Text.Encoding.ASCII.GetBytes("thermalStress");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            Assert.Equal(0, result); // Kernel function retrieval should succeed;

            // Prepare concurrent workloads
            for (var i = 0; i < concurrentKernels; i++)
            {
                result = CudaMalloc(ref deviceBuffers[i], dataSize * sizeof(float));
                _ = result.Should().Be(0, $"Memory allocation for buffer {i} should succeed");

                hostBuffers[i] = new float[dataSize];
                var random = new Random(42 + i);
                for (var j = 0; j < dataSize; j++)
                {
                    hostBuffers[i][j] = (float)(random.NextDouble() * 2.0 - 1.0);
                }

                var hostHandle = GCHandle.Alloc(hostBuffers[i], GCHandleType.Pinned);
                try
                {
                    result = CudaMemcpyHtoD(deviceBuffers[i], hostHandle.AddrOfPinnedObject(), dataSize * sizeof(float));
                    _ = result.Should().Be(0, $"Data copy to device buffer {i} should succeed");
                }
                finally
                {
                    hostHandle.Free();
                }
            }

            // Run thermal stress test
            var stressTestSw = Stopwatch.StartNew();
            var iterationCount = 0;
            var kernelFailures = 0;

            _output.WriteLine("Starting intensive thermal stress workload...");

            while (stressTestSw.ElapsedMilliseconds < stressTestDurationMs && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Launch concurrent kernels to maximize heat generation
                var kernelParams = new IntPtr[concurrentKernels][];
                var kernelParamsPtrs = new IntPtr[concurrentKernels];

                try
                {
                    for (var i = 0; i < concurrentKernels; i++)
                    {
                        kernelParams[i] =
                        [
                            Marshal.AllocHGlobal(IntPtr.Size),
                        Marshal.AllocHGlobal(sizeof(int)),
                        Marshal.AllocHGlobal(sizeof(int))
                        ];

                        Marshal.WriteIntPtr(kernelParams[i][0], deviceBuffers[i]);
                        Marshal.WriteInt32(kernelParams[i][1], dataSize);
                        Marshal.WriteInt32(kernelParams[i][2], 1000); // High iteration count for heat

                        kernelParamsPtrs[i] = Marshal.AllocHGlobal(kernelParams[i].Length * IntPtr.Size);
                        Marshal.Copy(kernelParams[i], 0, kernelParamsPtrs[i], kernelParams[i].Length);

                        // Launch kernel
                        const int blockSize = 256;
                        var gridSize = (dataSize + blockSize - 1) / blockSize;

                        result = CuLaunchKernel(
                            kernel,
                           (uint)gridSize, 1, 1,
                           (uint)blockSize, 1, 1,
                            0, IntPtr.Zero,
                            kernelParamsPtrs[i], IntPtr.Zero);

                        if (result != 0)
                        {
                            kernelFailures++;
                            _output.WriteLine($"Kernel launch failed with error code: {result}");
                        }
                    }

                    // Synchronize all kernels
                    result = CudaCtxSynchronize();
                    if (result != 0)
                    {
                        kernelFailures++;
                        _output.WriteLine($"Context synchronization failed with error code: {result}");
                    }

                    iterationCount++;

                    // Log progress every 10 iterations
                    if (iterationCount % 10 == 0)
                    {
                        var elapsedSeconds = stressTestSw.ElapsedMilliseconds / 1000.0;
                        var progress = (stressTestSw.ElapsedMilliseconds * 100.0) / stressTestDurationMs;
                        _output.WriteLine($"Stress test progress: {progress:F1}% ({elapsedSeconds:F1}s), iterations: {iterationCount}, failures: {kernelFailures}");
                    }
                }
                finally
                {
                    // Cleanup kernel parameters
                    for (var i = 0; i < concurrentKernels; i++)
                    {
                        if (kernelParams[i] != null)
                        {
                            foreach (var param in kernelParams[i])
                            {
                                Marshal.FreeHGlobal(param);
                            }
                        }
                        if (kernelParamsPtrs[i] != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(kernelParamsPtrs[i]);
                        }
                    }
                }

                // Brief pause to prevent overwhelming the system
                await Task.Delay(10, _cancellationTokenSource.Token);
            }

            stressTestSw.Stop();

            _output.WriteLine($"Thermal stress test completed:");
            _output.WriteLine($"  Duration: {stressTestSw.ElapsedMilliseconds / 1000.0:F1} seconds");
            _output.WriteLine($"  Total iterations: {iterationCount}");
            _output.WriteLine($"  Kernel failures: {kernelFailures}");
            _output.WriteLine($"  Failure rate: {(kernelFailures * 100.0) / (iterationCount * concurrentKernels):F2}%");

            // Validate thermal stability
            var failureRate = (kernelFailures * 100.0) / (iterationCount * concurrentKernels);
            _ = failureRate.Should().BeLessThan(5.0, "Failure rate should be low even under thermal stress");
            _ = iterationCount.Should().BeGreaterThan(10, "Should complete multiple stress iterations");

            _output.WriteLine("✓ System maintained stability under thermal stress");
        }
        finally
        {
            // Cleanup
            for (var i = 0; i < concurrentKernels; i++)
            {
                if (deviceBuffers[i] != IntPtr.Zero)
                    _ = CudaFree(deviceBuffers[i]);
            }
            if (module != IntPtr.Zero)
                _ = CuModuleUnload(module);
            if (program != IntPtr.Zero)
                _ = NvrtcDestroyProgram(ref program);
        }
    }

    [SkippableFact]
    public async Task ConcurrentStreamStressTest_ShouldMaintainPerformance()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        const int streamCount = 8;
        const int operationsPerStream = 100;
        const int dataSize = 256 * 1024; // 256K elements per operation

        _output.WriteLine($"Starting concurrent stream stress test with {streamCount} streams");

        var streams = new IntPtr[streamCount];
        var deviceBuffers = new List<IntPtr>[streamCount];
        var completedOperations = new int[streamCount];

        try
        {
            // Create CUDA streams
            for (var i = 0; i < streamCount; i++)
            {
                var result = CudaStreamCreate(ref streams[i], 0);
                Assert.Equal(0, result); // Stream creation should succeed
                deviceBuffers[i] = [];
            }

            _output.WriteLine($"Created {streamCount} CUDA streams");

            // Launch concurrent operations
            var tasks = new Task[streamCount];
            var stopwatch = Stopwatch.StartNew();

            for (var streamIdx = 0; streamIdx < streamCount; streamIdx++)
            {
                var capturedStreamIdx = streamIdx; // Capture for closure

                tasks[streamIdx] = Task.Run(async () =>
                {
                    var random = new Random(42 + capturedStreamIdx);

                    for (var op = 0; op < operationsPerStream; op++)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                            break;

                        try
                        {
                            // Allocate memory
                            var devicePtr = IntPtr.Zero;
                            var result = CudaMalloc(ref devicePtr, dataSize * sizeof(float));
                            if (result == 0)
                            {
                                deviceBuffers[capturedStreamIdx].Add(devicePtr);

                                // Perform memory operations
                                var hostData = new float[dataSize];
                                for (var i = 0; i < dataSize; i++)
                                {
                                    hostData[i] = (float)random.NextDouble();
                                }

                                var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
                                try
                                {
                                    // Asynchronous copy to device
                                    result = CudaMemcpyHtoDAsync(
                                        devicePtr,
                                        hostHandle.AddrOfPinnedObject(),
                                        dataSize * sizeof(float),
                                        streams[capturedStreamIdx]);

                                    if (result == 0)
                                    {
                                        // Asynchronous copy back
                                        result = CudaMemcpyDtoHAsync(
                                            hostHandle.AddrOfPinnedObject(),
                                            devicePtr,
                                            dataSize * sizeof(float),
                                            streams[capturedStreamIdx]);
                                    }

                                    if (result == 0)
                                    {
                                        completedOperations[capturedStreamIdx]++;
                                    }
                                }
                                finally
                                {
                                    hostHandle.Free();
                                }
                            }

                            // Log progress
                            if (op % 20 == 0)
                            {
                                _output.WriteLine($"Stream {capturedStreamIdx}: {op}/{operationsPerStream} operations completed");
                            }
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"Stream {capturedStreamIdx} operation failed: {ex.Message}");
                        }

                        // Brief delay to prevent overwhelming
                        await Task.Delay(5, _cancellationTokenSource.Token);
                    }
                }, _cancellationTokenSource.Token);
            }

            // Wait for all streams to complete
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Synchronize all streams
            for (var i = 0; i < streamCount; i++)
            {
                var result = CudaStreamSynchronize(streams[i]);
                Assert.Equal(0, result); // Stream synchronization should succeed
            }

            var totalOperations = completedOperations.Sum();
            var operationsPerSecond = totalOperations / stopwatch.Elapsed.TotalSeconds;
            var averageOperationsPerStream = completedOperations.Average();

            _output.WriteLine($"Concurrent stream stress test results:");
            _output.WriteLine($"  Total duration: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Total operations: {totalOperations}");
            _output.WriteLine($"  Operations/second: {operationsPerSecond:F2}");
            _output.WriteLine($"  Average per stream: {averageOperationsPerStream:F1}");
            _output.WriteLine($"  Stream utilization:");

            for (var i = 0; i < streamCount; i++)
            {
                var utilization = (completedOperations[i] * 100.0) / operationsPerStream;
                _output.WriteLine($"    Stream {i}: {completedOperations[i]}/{operationsPerStream}{utilization:F1}%)");
            }

            // Validate performance
            _ = totalOperations.Should().BeGreaterThan((int)(streamCount * operationsPerStream * 0.8),
                "Should complete most operations even under stress");
            _ = averageOperationsPerStream.Should().BeGreaterThan((int)(operationsPerStream * 0.8),
                "Each stream should maintain reasonable performance");

            _output.WriteLine("✓ Concurrent streams maintained performance under stress");
        }
        finally
        {
            // Cleanup
            for (var i = 0; i < streamCount; i++)
            {
                // Free all buffers for this stream
                foreach (var buffer in deviceBuffers[i])
                {
                    _ = CudaFree(buffer);
                }

                // Destroy stream
                if (streams[i] != IntPtr.Zero)
                    _ = CudaStreamDestroy(streams[i]);
            }
            _output.WriteLine("Cleanup completed for concurrent stream stress test");
        }
    }

    [SkippableFact]
    public async Task ErrorRecoveryStressTest_ShouldRecoverGracefully()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        _output.WriteLine("Starting error recovery stress test");

        var recoveryCount = 0;
        var totalErrors = 0;

        // Test 1: Invalid memory operations
        _output.WriteLine("Testing invalid memory operations recovery...");
        for (var i = 0; i < 50; i++)
        {
            try
            {
                // Try to free invalid pointer
                var result = CudaFree(new IntPtr(0xDEADBEEF));
                if (result != 0)
                    totalErrors++;

                // Try to copy to/from invalid pointers
                var dummyPtr = Marshal.AllocHGlobal(1024);
                try
                {
                    result = CudaMemcpyHtoD(new IntPtr(0xBADCAFE), dummyPtr, 1024);
                    if (result != 0)
                        totalErrors++;
                }
                finally
                {
                    Marshal.FreeHGlobal(dummyPtr);
                }

                // Verify context is still valid after errors
                ulong free = 0, total = 0;
                result = CudaMemGetInfo(ref free, ref total);
                if (result == 0)
                    recoveryCount++;

                if (i % 10 == 0)
                {
                    _output.WriteLine($"Error recovery test: {i}/50 completed, {recoveryCount} recoveries");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error recovery test iteration {i} failed: {ex.Message}");
            }

            await Task.Delay(10, _cancellationTokenSource.Token);
        }

        // Test 2: Invalid kernel launches
        _output.WriteLine("Testing invalid kernel launch recovery...");
        var invalidKernelErrors = 0;
        var kernelRecoveries = 0;

        for (var i = 0; i < 20; i++)
        {
            try
            {
                // Try to launch with invalid kernel handle
                var result = CuLaunchKernel(
                    new IntPtr(0xDEADBEEF),
                    1, 1, 1, 1, 1, 1, 0,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (result != 0)
                    invalidKernelErrors++;

                // Verify context is still functional
                result = CudaCtxSynchronize();
                if (result is 0 or 1) // Success or no error to report
                {
                    kernelRecoveries++;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Invalid kernel test iteration {i} failed: {ex.Message}");
            }

            await Task.Delay(10, _cancellationTokenSource.Token);
        }

        // Test 3: Memory exhaustion recovery
        _output.WriteLine("Testing memory exhaustion recovery...");
        var exhaustionRecoveries = 0;
        var largeAllocations = new List<IntPtr>();

        try
        {
            // Allocate until exhaustion
            for (var i = 0; i < 100; i++)
            {
                var devicePtr = IntPtr.Zero;
                var result = CudaMalloc(ref devicePtr, 512 * 1024 * 1024); // 512 MB chunks

                if (result == 0)
                {
                    largeAllocations.Add(devicePtr);
                }
                else
                {
                    // Memory exhausted, try to recover
                    break;
                }
            }

            _output.WriteLine($"Allocated {largeAllocations.Count} large buffers before exhaustion");

            // Test recovery by freeing half and reallocating
            var halfCount = largeAllocations.Count / 2;
            for (var i = 0; i < halfCount; i++)
            {
                var result = CudaFree(largeAllocations[i]);
                if (result == 0)
                    exhaustionRecoveries++;
            }
            largeAllocations.RemoveRange(0, halfCount);

            // Try to allocate again
            var recoveryPtr = IntPtr.Zero;
            var recoveryResult = CudaMalloc(ref recoveryPtr, 256 * 1024 * 1024);
            if (recoveryResult == 0)
            {
                exhaustionRecoveries++;
                largeAllocations.Add(recoveryPtr);
            }
        }
        finally
        {
            // Cleanup remaining allocations
            foreach (var ptr in largeAllocations)
            {
                _ = CudaFree(ptr);
            }
        }

        // Validate error recovery
        _output.WriteLine("Error recovery stress test results:");
        _output.WriteLine($"  Total errors generated: {totalErrors}");
        _output.WriteLine($"  Context recoveries: {recoveryCount}/50");
        _output.WriteLine($"  Invalid kernel errors: {invalidKernelErrors}");
        _output.WriteLine($"  Kernel recoveries: {kernelRecoveries}/20");
        _output.WriteLine($"  Memory exhaustion recoveries: {exhaustionRecoveries}");

        _ = recoveryCount.Should().BeGreaterThan(40, "Should recover from most invalid memory operations");
        _ = kernelRecoveries.Should().BeGreaterThan(15, "Should recover from most invalid kernel launches");
        _ = exhaustionRecoveries.Should().BeGreaterThan(0, "Should recover from memory exhaustion");

        _output.WriteLine("✓ System demonstrated robust error recovery capabilities");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        if (_cudaContext != IntPtr.Zero)
        {
            _ = CudaCtxDestroy(_cudaContext);
            _cudaContext = IntPtr.Zero;
        }
        _cudaInitialized = false;

        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Native Methods

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuInit", ExactSpelling = true)]
    private static extern int CudaInit(uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxCreate_v2", ExactSpelling = true)]
    private static extern int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxDestroy_v2", ExactSpelling = true)]
    private static extern int CudaCtxDestroy(IntPtr ctx);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxSynchronize", ExactSpelling = true)]
    private static extern int CudaCtxSynchronize();

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemGetInfo_v2", ExactSpelling = true)]
    private static extern int CudaMemGetInfo(ref ulong free, ref ulong total);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemAlloc_v2", ExactSpelling = true)]
    private static extern int CudaMalloc(ref IntPtr dptr, long bytesize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemFree_v2", ExactSpelling = true)]
    private static extern int CudaFree(IntPtr dptr);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyHtoD_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoH_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyHtoDAsync_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, long byteCount, IntPtr stream);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoHAsync_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyDtoHAsync(IntPtr dstHost, IntPtr srcDevice, long byteCount, IntPtr stream);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuStreamCreate", ExactSpelling = true)]
    private static extern int CudaStreamCreate(ref IntPtr stream, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuStreamDestroy_v2", ExactSpelling = true)]
    private static extern int CudaStreamDestroy(IntPtr stream);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuStreamSynchronize", ExactSpelling = true)]
    private static extern int CudaStreamSynchronize(IntPtr stream);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuLaunchKernel", ExactSpelling = true)]
    private static extern int CuLaunchKernel(IntPtr f, uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ, uint sharedMemBytes, IntPtr hStream,
        IntPtr kernelParams, IntPtr extra);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuModuleLoadData", ExactSpelling = true)]
    private static extern int CuModuleLoadData(ref IntPtr module, byte[] image);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuModuleUnload", ExactSpelling = true)]
    private static extern int CuModuleUnload(IntPtr module);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuModuleGetFunction", ExactSpelling = true)]
    private static extern int CuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, byte[] name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCreateProgram", ExactSpelling = true)]
    private static extern int NvrtcCreateProgram(ref IntPtr prog, string src, string name,
        int numHeaders, string[] headers, string[] includeNames);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcDestroyProgram", ExactSpelling = true)]
    private static extern int NvrtcDestroyProgram(ref IntPtr prog);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCompileProgram", ExactSpelling = true)]
    private static extern int NvrtcCompileProgram(IntPtr prog, int numOptions, string[] options);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTXSize", ExactSpelling = true)]
    private static extern int NvrtcGetPTXSize(IntPtr prog, ref long ptxSizeRet);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTX", ExactSpelling = true)]
    private static extern int NvrtcGetPTX(IntPtr prog, byte[] ptx);

    #endregion
}

/// <summary>
/// Collection definition to serialize hardware stress tests.
/// </summary>
[CollectionDefinition("Hardware Stress Tests")]
public sealed class HardwareStressTestGroup
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

/// <summary>
/// Helper attribute to skip tests when conditions aren't met.
/// </summary>
internal sealed class SkippableFactAttribute : FactAttribute
{
    public override string? Skip { get; set; }
}

/// <summary>
/// Helper class for skipping tests conditionally.
/// </summary>
internal static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
/// Exception thrown to skip a test.
/// </summary>
internal sealed class SkipException : Exception
{
    public SkipException() : base() { }
    public SkipException(string reason) : base(reason) { }
    public SkipException(string message, Exception innerException) : base(message, innerException) { }
}
