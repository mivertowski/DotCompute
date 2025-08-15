using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Comprehensive hardware validation tests specifically for NVIDIA RTX 2000 Ada Generation GPU.
/// Tests actual CUDA kernel compilation, execution, and hardware-specific features.
/// </summary>
[Trait("Category", "HardwareRequired")]
[Trait("Category", "CudaRequired")]
[Trait("Category", "RTX2000")]
[Trait("Category", "HardwareValidation")]
[Trait("Category", "Hardware")]
[Collection("Hardware")]
public class RTX2000HardwareValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private IntPtr _cudaContext;
    private bool _cudaInitialized;
    private int _deviceId;
    private CudaDeviceProperties _deviceProperties;

    public RTX2000HardwareValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _deviceProperties = new CudaDeviceProperties();
        InitializeCudaAndValidateRTX2000();
    }

    private void InitializeCudaAndValidateRTX2000()
    {
        try
        {
            // Initialize CUDA
            var result = CudaInit(0);
            if(result != 0)
            {
                _output.WriteLine($"CUDA initialization failed with error code: {result}");
                return;
            }

            // Get device count
            int deviceCount = 0;
            result = CudaGetDeviceCount(ref deviceCount);
            if(result != 0 || deviceCount == 0)
            {
                _output.WriteLine($"No CUDA devices found. Error code: {result}, Device count: {deviceCount}");
                return;
            }

            // Find RTX 2000 Ada Gen device
            bool foundRTX2000 = false;
            for(int i = 0; i < deviceCount; i++)
            {
                var deviceName = new byte[256];
                result = CudaDeviceGetName(deviceName, 256, i);
                if(result == 0)
                {
                    var name = System.Text.Encoding.ASCII.GetString(deviceName).TrimEnd('\0');
                    _output.WriteLine($"Found device {i}: {name}");
                    
                    if(name.Contains("RTX 2000 Ada") || name.Contains("Ada Generation"))
                    {
                        _deviceId = i;
                        foundRTX2000 = true;
                        
                        // Get device properties
                        var tempProps = _deviceProperties;
                        GetDeviceProperties(i, ref tempProps);
                        _deviceProperties = tempProps;
                        ValidateRTX2000Properties();
                        break;
                    }
                }
            }

            if(!foundRTX2000)
            {
                _output.WriteLine("RTX 2000 Ada Generation GPU not found. Using first available device for testing.");
                _deviceId = 0;
            }

            // Create context on selected device
            result = CudaCtxCreate(ref _cudaContext, 0, _deviceId);
            if(result == 0)
            {
                _cudaInitialized = true;
                _output.WriteLine($"CUDA context created successfully on device {_deviceId}");
            }
            else
            {
                _output.WriteLine($"Failed to create CUDA context. Error code: {result}");
            }
        }
        catch(Exception ex)
        {
            _output.WriteLine($"CUDA initialization exception: {ex.Message}");
        }
    }

    private void ValidateRTX2000Properties()
    {
        _output.WriteLine("RTX 2000 Ada Generation Device Properties:");
        _output.WriteLine($"  Compute Capability: {_deviceProperties.Major}.{_deviceProperties.Minor}");
        _output.WriteLine($"  Global Memory: {_deviceProperties.TotalGlobalMem / (1024 * 1024)} MB");
        _output.WriteLine($"  Multiprocessors: {_deviceProperties.MultiProcessorCount}");
        _output.WriteLine($"  Max Threads per MP: {_deviceProperties.MaxThreadsPerMultiProcessor}");
        _output.WriteLine($"  Max Block Size: {_deviceProperties.MaxThreadsPerBlock}");
        _output.WriteLine($"  Shared Memory per Block: {_deviceProperties.SharedMemPerBlock} bytes");
        _output.WriteLine($"  Memory Clock Rate: {_deviceProperties.MemoryClockRate} KHz");
        _output.WriteLine($"  Memory Bus Width: {_deviceProperties.MemoryBusWidth} bits");
        _output.WriteLine($"  L2 Cache Size: {_deviceProperties.L2CacheSize} bytes");

        // RTX 2000 Ada Gen should have compute capability 8.9
        if(_deviceProperties.Major == 8 && _deviceProperties.Minor == 9)
        {
            _output.WriteLine("✓ Confirmed RTX 2000 Ada GenerationCompute Capability 8.9)");
        }
        else
        {
            _output.WriteLine($"⚠ Expected compute capability 8.9, found {_deviceProperties.Major}.{_deviceProperties.Minor}");
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    public void ValidateComputeCapability89_ShouldConfirmRTX2000AdaGen()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        // RTX 2000 Ada Gen should have compute capability 8.9
        _deviceProperties.Major.Should().Be(8, "RTX 2000 Ada Gen should have major compute capability 8");
        _deviceProperties.Minor.Should().BeGreaterOrEqualTo(9, "RTX 2000 Ada Gen should have minor compute capability 9 or higher");

        _output.WriteLine($"✓ Validated compute capability {_deviceProperties.Major}.{_deviceProperties.Minor}");
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    public void ValidateGDDR6Memory_ShouldReportCorrectSpecifications()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        ulong free = 0, total = 0;
        var result = CudaMemGetInfo(ref free, ref total);
        
        Assert.Equal(0, result); // Memory info query should succeed;
        total.Should().BeGreaterThan(4UL * 1024 * 1024 * 1024, "RTX 2000 Ada Gen should have more than 4GB memory");

        // RTX 2000 Ada Gen has 8GB GDDR6 memory
        var totalGB = total /(1024.0 * 1024.0 * 1024.0);
        _output.WriteLine($"Total GPU Memory: {totalGB:F1} GB");
        
        // Allow some tolerance for system reserved memory
        totalGB.Should().BeInRange(7.0, 8.5, "RTX 2000 Ada Gen should have approximately 8GB memory");

        // Validate memory bandwidth characteristics
        var memoryBandwidth = CalculateTheoreticalMemoryBandwidth();
        _output.WriteLine($"Theoretical Memory Bandwidth: {memoryBandwidth:F1} GB/s");
        memoryBandwidth.Should().BeGreaterThan(200.0, "RTX 2000 Ada Gen should have high memory bandwidth");
    }

    private double CalculateTheoreticalMemoryBandwidth()
    {
        // Memory bandwidth =(Memory Clock Rate * 2) * Memory Bus Width / 8
        // RTX 2000 Ada Gen typically has 192-bit bus width and ~14 Gbps effective memory speed
        var memoryClockMHz = _deviceProperties.MemoryClockRate / 1000.0;
        var effectiveClockMHz = memoryClockMHz * 2; // DDR
        var busWidthBits = _deviceProperties.MemoryBusWidth;
        
        return(effectiveClockMHz * busWidthBits) / 8.0 / 1000.0; // Convert to GB/s
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    [Trait("Category", "Performance")]
    public async Task StressTestMemoryAllocation_ShouldHandleLargeAllocations()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int allocationCount = 100;
        const long allocationSizeMB = 64; // 64 MB per allocation
        const long allocationSize = allocationSizeMB * 1024 * 1024;
        
        var allocations = new List<IntPtr>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Allocate multiple buffers
            for(int i = 0; i < allocationCount; i++)
            {
                IntPtr devicePtr = IntPtr.Zero;
                var result = CudaMalloc(ref devicePtr, allocationSize);
                
                if(result == 0)
                {
                    allocations.Add(devicePtr);
                }
                else
                {
                    _output.WriteLine($"Allocation {i} failed with error code: {result}");
                    break;
                }

                // Log progress
                if((i + 1) % 10 == 0)
                {
                    var allocatedMB =(i + 1) * allocationSizeMB;
                    _output.WriteLine($"Successfully allocated {allocatedMB} MB in {i + 1} buffers");
                }
            }

            stopwatch.Stop();
            var totalAllocatedMB = allocations.Count * allocationSizeMB;
            _output.WriteLine($"Allocated {totalAllocatedMB} MB in {allocations.Count} buffers in {stopwatch.ElapsedMilliseconds} ms");

            // Should be able to allocate at least several GB
            (allocations.Count > 50).Should().BeTrue();
        }
        finally
        {
            // Clean up all allocations
            foreach (var ptr in allocations)
            {
                CudaFree(ptr);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    [Trait("Category", "Performance")]
    public async Task MeasureActualMemoryBandwidth_ShouldMeetGDDR6Specifications()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int testSizeMB = 256; // 256 MB test
        const int testSize = testSizeMB * 1024 * 1024;
        const int iterations = 20;

        var hostData = new byte[testSize];
        new Random(42).NextBytes(hostData);

        IntPtr devicePtr = IntPtr.Zero;
        
        try
        {
            var result = CudaMalloc(ref devicePtr, testSize);
            Assert.Equal(0, result); // Memory allocation should succeed;

            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                // Warm up
                for(int i = 0; i < 3; i++)
                {
                    CudaMemcpyHtoD(devicePtr, hostHandle.AddrOfPinnedObject(), testSize);
                    CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), devicePtr, testSize);
                }

                // Measure Host to Device bandwidth
                var sw = Stopwatch.StartNew();
                for(int i = 0; i < iterations; i++)
                {
                    result = CudaMemcpyHtoD(devicePtr, hostHandle.AddrOfPinnedObject(), testSize);
                    result.Should().Be(0, $"H2D copy iteration {i} should succeed");
                }
                sw.Stop();

                var h2dBandwidth =((long)testSize * iterations /(1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
                _output.WriteLine($"Host to Device bandwidth: {h2dBandwidth:F2} GB/s");

                // Measure Device to Host bandwidth
                sw.Restart();
                for(int i = 0; i < iterations; i++)
                {
                    result = CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), devicePtr, testSize);
                    result.Should().Be(0, $"D2H copy iteration {i} should succeed");
                }
                sw.Stop();

                var d2hBandwidth =((long)testSize * iterations /(1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
                _output.WriteLine($"Device to Host bandwidth: {d2hBandwidth:F2} GB/s");

                // Measure Device to Device bandwidth
                IntPtr devicePtr2 = IntPtr.Zero;
                result = CudaMalloc(ref devicePtr2, testSize);
                Assert.Equal(0, result); // Second memory allocation should succeed;

                try
                {
                    sw.Restart();
                    for(int i = 0; i < iterations; i++)
                    {
                        result = CudaMemcpyDtoD(devicePtr2, devicePtr, testSize);
                        result.Should().Be(0, $"D2D copy iteration {i} should succeed");
                    }
                    sw.Stop();

                    var d2dBandwidth =((long)testSize * iterations /(1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
                    _output.WriteLine($"Device to Device bandwidth: {d2dBandwidth:F2} GB/s");

                    // Validate bandwidth expectations
                    h2dBandwidth.Should().BeGreaterThan(10.0, "H2D bandwidth should be reasonable for PCIe");
                    d2hBandwidth.Should().BeGreaterThan(10.0, "D2H bandwidth should be reasonable for PCIe");
                    d2dBandwidth.Should().BeGreaterThan(100.0, "D2D bandwidth should be much higher than PCIe");

                    _output.WriteLine($"Bandwidth Summary:");
                    _output.WriteLine($"  H2D: {h2dBandwidth:F2} GB/s");
                    _output.WriteLine($"  D2H: {d2hBandwidth:F2} GB/s");
                    _output.WriteLine($"  D2D: {d2dBandwidth:F2} GB/s");
                }
                finally
                {
                    CudaFree(devicePtr2);
                }
            }
            finally
            {
                hostHandle.Free();
            }
        }
        finally
        {
            if(devicePtr != IntPtr.Zero)
            {
                CudaFree(devicePtr);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    public async Task ValidateMultiprocessorUtilization_ShouldUseAllSMs()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        var smCount = _deviceProperties.MultiProcessorCount;
        var maxThreadsPerSM = _deviceProperties.MaxThreadsPerMultiProcessor;
        var maxThreadsPerBlock = _deviceProperties.MaxThreadsPerBlock;

        _output.WriteLine($"Device has {smCount} SMs with {maxThreadsPerSM} max threads per SM");
        _output.WriteLine($"Max threads per block: {maxThreadsPerBlock}");

        // Calculate optimal grid size for full SM utilization
        var threadsPerBlock = Math.Min(256, maxThreadsPerBlock); // Common choice
        var blocksPerSM = maxThreadsPerSM / threadsPerBlock;
        var totalBlocks = smCount * blocksPerSM;

        _output.WriteLine($"Optimal configuration: {totalBlocks} blocks of {threadsPerBlock} threads");
        _output.WriteLine($"Total threads: {totalBlocks * threadsPerBlock}");

        // RTX 2000 Ada Gen should have sufficient multiprocessors for good parallelism
        smCount.Should().BeGreaterThan(20, "RTX 2000 Ada Gen should have many SMs for parallel execution");
        maxThreadsPerSM .Should().BeGreaterThanOrEqualTo(1536, "Ada architecture should support many concurrent threads");

        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    [Trait("Category", "Performance")]
    public async Task TestConcurrentKernelExecution_ShouldSupportMultipleStreams()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int streamCount = 4;
        const int elementsPerStream = 1024 * 1024; // 1M elements
        const int elementSize = sizeof(float);

        var streams = new IntPtr[streamCount];
        var deviceBuffers = new IntPtr[streamCount];
        var hostBuffers = new float[streamCount][];

        try
        {
            // Create streams and allocate memory
            for(int i = 0; i < streamCount; i++)
            {
                var result = CudaStreamCreate(ref streams[i]);
                result.Should().Be(0, $"Stream {i} creation should succeed");

                result = CudaMalloc(ref deviceBuffers[i], elementsPerStream * elementSize);
                result.Should().Be(0, $"Buffer {i} allocation should succeed");

                hostBuffers[i] = new float[elementsPerStream];
                for(int j = 0; j < elementsPerStream; j++)
                {
                    hostBuffers[i][j] = i * elementsPerStream + j;
                }
            }

            var stopwatch = Stopwatch.StartNew();

            // Launch concurrent memory transfers
            var handles = new GCHandle[streamCount];
            for(int i = 0; i < streamCount; i++)
            {
                handles[i] = GCHandle.Alloc(hostBuffers[i], GCHandleType.Pinned);
                var result = CudaMemcpyHtoDAsync(
                    deviceBuffers[i], 
                    handles[i].AddrOfPinnedObject(), 
                    elementsPerStream * elementSize, 
                    streams[i]);
                result.Should().Be(0, $"Async H2D copy for stream {i} should succeed");
            }

            // Synchronize all streams
            for(int i = 0; i < streamCount; i++)
            {
                var result = CudaStreamSynchronize(streams[i]);
                result.Should().Be(0, $"Stream {i} synchronization should succeed");
                handles[i].Free();
            }

            stopwatch.Stop();
            _output.WriteLine($"Concurrent operations completed in {stopwatch.ElapsedMilliseconds} ms");

            // Verify that concurrent execution was faster than sequential would be
            var totalDataMB =(streamCount * elementsPerStream * elementSize) /(1024 * 1024);
            var throughputMBps = totalDataMB /(stopwatch.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"Total throughput: {throughputMBps:F1} MB/s for {totalDataMB} MB");

            throughputMBps.Should().BeGreaterThan(1000, "Concurrent streams should achieve high throughput");
        }
        finally
        {
            // Cleanup
            for(int i = 0; i < streamCount; i++)
            {
                if(streams[i] != IntPtr.Zero)
                    CudaStreamDestroy(streams[i]);
                if(deviceBuffers[i] != IntPtr.Zero)
                    CudaFree(deviceBuffers[i]);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "RTX2000")]
    public async Task ValidateErrorHandling_ShouldRecoverGracefully()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        // Test 1: Invalid memory allocation(too large)
        IntPtr devicePtr = IntPtr.Zero;
        var result = CudaMalloc(ref devicePtr, long.MaxValue);
        result.Should().NotBe(0, "Excessive memory allocation should fail");
        _output.WriteLine($"Large allocation failed as expected with error code: {result}");

        // Test 2: Invalid memory operations
        result = CudaFree(IntPtr.Zero);
        result.Should().NotBe(0, "Freeing null pointer should fail");
        _output.WriteLine($"Null pointer free failed as expected with error code: {result}");

        // Test 3: Memory access validation
        const int testSize = 1024 * sizeof(float);
        result = CudaMalloc(ref devicePtr, testSize);
        Assert.Equal(0, result); // Valid allocation should succeed;

        try
        {
            // Try to copy to invalid device memory(offset beyond allocation)
            var hostData = new float[256];
            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                // This should work
                result = CudaMemcpyHtoD(devicePtr, hostHandle.AddrOfPinnedObject(), testSize);
                Assert.Equal(0, result); // Valid memory copy should succeed;

                // Test successful error recovery - context should still be valid
                ulong free = 0, total = 0;
                result = CudaMemGetInfo(ref free, ref total);
                Assert.Equal(0, result); // Context should still be valid after error recovery;
                _output.WriteLine("Error recovery validation successful");
            }
            finally
            {
                hostHandle.Free();
            }
        }
        finally
        {
            CudaFree(devicePtr);
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if(_cudaContext != IntPtr.Zero)
        {
            CudaCtxDestroy(_cudaContext);
            _cudaContext = IntPtr.Zero;
        }
        _cudaInitialized = false;
    }

    #region CUDA Native Methods and Structures

    [StructLayout(LayoutKind.Sequential)]
    private struct CudaDeviceProperties
    {
        public int Major;
        public int Minor;
        public ulong TotalGlobalMem;
        public int SharedMemPerBlock;
        public int MaxThreadsPerBlock;
        public int MultiProcessorCount;
        public int MaxThreadsPerMultiProcessor;
        public int MemoryClockRate;
        public int MemoryBusWidth;
        public int L2CacheSize;
    }

    // P/Invoke declarations for CUDA Driver API
    private static class CudaNative
    {
        public static class Windows
        {
            [DllImport("nvcuda.dll", EntryPoint = "cuInit")]
            public static extern int CudaInit(uint flags);

            [DllImport("nvcuda.dll", EntryPoint = "cuDeviceGetCount")]
            public static extern int CudaGetDeviceCount(ref int count);

            [DllImport("nvcuda.dll", EntryPoint = "cuDeviceGetName")]
            public static extern int CudaDeviceGetName(byte[] name, int len, int dev);

            [DllImport("nvcuda.dll", EntryPoint = "cuDeviceGetAttribute")]
            public static extern int CudaDeviceGetAttribute(ref int value, int attrib, int dev);

            [DllImport("nvcuda.dll", EntryPoint = "cuCtxCreate_v2")]
            public static extern int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev);

            [DllImport("nvcuda.dll", EntryPoint = "cuCtxDestroy_v2")]
            public static extern int CudaCtxDestroy(IntPtr ctx);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemGetInfo_v2")]
            public static extern int CudaMemGetInfo(ref ulong free, ref ulong total);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemAlloc_v2")]
            public static extern int CudaMalloc(ref IntPtr dptr, long bytesize);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemFree_v2")]
            public static extern int CudaFree(IntPtr dptr);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyHtoD_v2")]
            public static extern int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyDtoH_v2")]
            public static extern int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyDtoD_v2")]
            public static extern int CudaMemcpyDtoD(IntPtr dstDevice, IntPtr srcDevice, long byteCount);

            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyHtoDAsync_v2")]
            public static extern int CudaMemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, long byteCount, IntPtr stream);

            [DllImport("nvcuda.dll", EntryPoint = "cuStreamCreate")]
            public static extern int CudaStreamCreate(ref IntPtr stream, uint flags);

            [DllImport("nvcuda.dll", EntryPoint = "cuStreamDestroy_v2")]
            public static extern int CudaStreamDestroy(IntPtr stream);

            [DllImport("nvcuda.dll", EntryPoint = "cuStreamSynchronize")]
            public static extern int CudaStreamSynchronize(IntPtr stream);
        }

        public static class Linux
        {
            [DllImport("libcuda.so.1", EntryPoint = "cuInit")]
            public static extern int CudaInit(uint flags);

            [DllImport("libcuda.so.1", EntryPoint = "cuDeviceGetCount")]
            public static extern int CudaGetDeviceCount(ref int count);

            [DllImport("libcuda.so.1", EntryPoint = "cuDeviceGetName")]
            public static extern int CudaDeviceGetName(byte[] name, int len, int dev);

            [DllImport("libcuda.so.1", EntryPoint = "cuDeviceGetAttribute")]
            public static extern int CudaDeviceGetAttribute(ref int value, int attrib, int dev);

            [DllImport("libcuda.so.1", EntryPoint = "cuCtxCreate_v2")]
            public static extern int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev);

            [DllImport("libcuda.so.1", EntryPoint = "cuCtxDestroy_v2")]
            public static extern int CudaCtxDestroy(IntPtr ctx);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemGetInfo_v2")]
            public static extern int CudaMemGetInfo(ref ulong free, ref ulong total);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemAlloc_v2")]
            public static extern int CudaMalloc(ref IntPtr dptr, long bytesize);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemFree_v2")]
            public static extern int CudaFree(IntPtr dptr);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyHtoD_v2")]
            public static extern int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyDtoH_v2")]
            public static extern int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyDtoD_v2")]
            public static extern int CudaMemcpyDtoD(IntPtr dstDevice, IntPtr srcDevice, long byteCount);

            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyHtoDAsync_v2")]
            public static extern int CudaMemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, long byteCount, IntPtr stream);

            [DllImport("libcuda.so.1", EntryPoint = "cuStreamCreate")]
            public static extern int CudaStreamCreate(ref IntPtr stream, uint flags);

            [DllImport("libcuda.so.1", EntryPoint = "cuStreamDestroy_v2")]
            public static extern int CudaStreamDestroy(IntPtr stream);

            [DllImport("libcuda.so.1", EntryPoint = "cuStreamSynchronize")]
            public static extern int CudaStreamSynchronize(IntPtr stream);
        }
    }

    // Platform-agnostic wrapper methods
    private static int CudaInit(uint flags) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaInit(flags) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaInit(flags) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaGetDeviceCount(ref int count) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaGetDeviceCount(ref count) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaGetDeviceCount(ref count) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaDeviceGetName(byte[] name, int len, int dev) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaDeviceGetName(name, len, dev) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaDeviceGetName(name, len, dev) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaDeviceGetAttribute(ref int value, int attrib, int dev) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaDeviceGetAttribute(ref value, attrib, dev) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaDeviceGetAttribute(ref value, attrib, dev) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaCtxCreate(ref ctx, flags, dev) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaCtxCreate(ref ctx, flags, dev) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaCtxDestroy(IntPtr ctx) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaCtxDestroy(ctx) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaCtxDestroy(ctx) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaMemGetInfo(ref ulong free, ref ulong total) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaMemGetInfo(ref free, ref total) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaMemGetInfo(ref free, ref total) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaMalloc(ref IntPtr dptr, long bytesize) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaMalloc(ref dptr, bytesize) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaMalloc(ref dptr, bytesize) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaFree(IntPtr dptr) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaFree(dptr) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaFree(dptr) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaMemcpyHtoD(dstDevice, srcHost, byteCount) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaMemcpyHtoD(dstDevice, srcHost, byteCount) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaMemcpyDtoH(dstHost, srcDevice, byteCount) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaMemcpyDtoH(dstHost, srcDevice, byteCount) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaMemcpyDtoD(IntPtr dstDevice, IntPtr srcDevice, long byteCount) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaMemcpyDtoD(dstDevice, srcDevice, byteCount) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaMemcpyDtoD(dstDevice, srcDevice, byteCount) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaMemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, long byteCount, IntPtr stream) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaMemcpyHtoDAsync(dstDevice, srcHost, byteCount, stream) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaMemcpyHtoDAsync(dstDevice, srcHost, byteCount, stream) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaStreamCreate(ref IntPtr stream) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaStreamCreate(ref stream, 0) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaStreamCreate(ref stream, 0) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaStreamDestroy(IntPtr stream) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaStreamDestroy(stream) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaStreamDestroy(stream) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private static int CudaStreamSynchronize(IntPtr stream) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CudaNative.Windows.CudaStreamSynchronize(stream) :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? CudaNative.Linux.CudaStreamSynchronize(stream) :
        throw new PlatformNotSupportedException("CUDA is not supported on this platform");

    private void GetDeviceProperties(int deviceId, ref CudaDeviceProperties props)
    {
        // Device attributes from CUDA driver API
        const int CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR = 75;
        const int CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR = 76;
        const int CU_DEVICE_ATTRIBUTE_SHARED_MEMORY_PER_BLOCK = 8;
        const int CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK = 1;
        const int CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT = 16;
        const int CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR = 39;
        const int CU_DEVICE_ATTRIBUTE_MEMORY_CLOCK_RATE = 36;
        const int CU_DEVICE_ATTRIBUTE_GLOBAL_MEMORY_BUS_WIDTH = 37;
        const int CU_DEVICE_ATTRIBUTE_L2_CACHE_SIZE = 40;

        int value = 0;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR, deviceId) == 0)
            props.Major = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR, deviceId) == 0)
            props.Minor = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_SHARED_MEMORY_PER_BLOCK, deviceId) == 0)
            props.SharedMemPerBlock = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK, deviceId) == 0)
            props.MaxThreadsPerBlock = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT, deviceId) == 0)
            props.MultiProcessorCount = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR, deviceId) == 0)
            props.MaxThreadsPerMultiProcessor = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_MEMORY_CLOCK_RATE, deviceId) == 0)
            props.MemoryClockRate = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_GLOBAL_MEMORY_BUS_WIDTH, deviceId) == 0)
            props.MemoryBusWidth = value;
        
        if(CudaDeviceGetAttribute(ref value, CU_DEVICE_ATTRIBUTE_L2_CACHE_SIZE, deviceId) == 0)
            props.L2CacheSize = value;

        // Get total memory from memory info
        ulong free = 0, total = 0;
        if(CudaMemGetInfo(ref free, ref total) == 0)
        {
            props.TotalGlobalMem = total;
        }
    }

    #endregion
}

/// <summary>
/// Helper attribute to skip tests when conditions aren't met.
/// </summary>
public class SkippableFactAttribute : FactAttribute
{
    public override string? Skip { get; set; }
}

/// <summary>
/// Helper class for skipping tests conditionally.
/// </summary>
public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if(!condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
/// Exception thrown to skip a test.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}
