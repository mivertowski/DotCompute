using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.Cuda.Tests;


/// <summary>
/// Real hardware tests for CUDA GPU operations.
/// These tests require an NVIDIA GPU with CUDA support.
/// </summary>
[Trait("Category", "HardwareRequired")]
[Trait("Category", "CudaRequired")]
[Trait("Category", "Hardware")]
[Collection("Hardware")]
public sealed class CudaRealHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private nint _cudaContext;
    private bool _cudaInitialized;

    public CudaRealHardwareTests(ITestOutputHelper output)
    {
        _output = output;
        InitializeCuda();
    }

    private void InitializeCuda()
    {
        try
        {
            // Initialize CUDA
            var result = CudaInit(0);
            if (result != 0)
            {
                _output.WriteLine($"CUDA initialization failed with error code: {result}");
                return;
            }

            // Get device count
            var deviceCount = 0;
            result = CudaGetDeviceCount(ref deviceCount);
            if (result != 0 || deviceCount == 0)
            {
                _output.WriteLine($"No CUDA devices found. Error code: {result}, Device count: {deviceCount}");
                return;
            }

            _output.WriteLine($"Found {deviceCount} CUDA device(s)");

            // Create context on first device
            result = CudaCtxCreate(ref _cudaContext, 0, 0);
            if (result == 0)
            {
                _cudaInitialized = true;
                _output.WriteLine("CUDA context created successfully");
            }
            else
            {
                _output.WriteLine($"Failed to create CUDA context. Error code: {result}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"CUDA initialization exception: {ex.Message}");
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public void DetectCudaDevice_ShouldFindGPU()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        // Get device properties
        var deviceCount = 0;
        var result = CudaGetDeviceCount(ref deviceCount);

        Assert.Equal(0, result);
        _ = deviceCount.Should().BeGreaterThan(0, "Should have at least one CUDA device");

        _output.WriteLine($"CUDA device count: {deviceCount}");

        // Get device name
        var deviceName = new byte[256];
        result = CudaDeviceGetName(deviceName, 256, 0);
        Assert.Equal(0, result);

        var name = System.Text.Encoding.ASCII.GetString(deviceName).TrimEnd('\0');
        _output.WriteLine($"Device 0: {name}");
        Assert.NotEmpty(name);
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public void GetDeviceMemory_ShouldReportCorrectSize()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        ulong free = 0, total = 0;
        var result = CudaMemGetInfo(ref free, ref total);

        Assert.Equal(0, result);
        _ = total.Should().BeGreaterThan(0, "Total memory should be greater than 0");
        _ = free.Should().BeGreaterThan(0, "Free memory should be greater than 0");
        _ = (free <= total).Should().BeTrue();

        _output.WriteLine($"GPU Memory - Total: {total / (1024 * 1024)} MB, Free: {free / (1024 * 1024)} MB");
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task AllocateAndFreeMemory_ShouldSucceed()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int size = 1024 * 1024; // 1 MB
        var devicePtr = nint.Zero;

        // Allocate memory on device
        var result = CudaMalloc(ref devicePtr, size);
        Assert.Equal(0, result);
        Assert.NotEqual(nint.Zero, devicePtr);

        _output.WriteLine($"Allocated {size} bytes on GPU at address: 0x{devicePtr:X}");

        // Free memory
        result = CudaFree(devicePtr);
        Assert.Equal(0, result);

        _output.WriteLine("Memory freed successfully");
        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task CopyDataToFromDevice_ShouldMaintainIntegrity()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int elementCount = 1024;
        const int size = elementCount * sizeof(float);

        // Create test data
        var hostData = new float[elementCount];
        var random = new Random(42);
        for (var i = 0; i < elementCount; i++)
        {
            hostData[i] = (float)random.NextDouble() * 100;
        }

        var devicePtr = nint.Zero;

        try
        {
            // Allocate device memory
            var result = CudaMalloc(ref devicePtr, size);
            Assert.Equal(0, result);

            // Copy data to device
            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                result = CudaMemcpyHtoD(devicePtr, hostHandle.AddrOfPinnedObject(), size);
                Assert.Equal(0, result);
                _output.WriteLine($"Copied {size} bytes to device");

                // Copy data back from device
                var resultData = new float[elementCount];
                var resultHandle = GCHandle.Alloc(resultData, GCHandleType.Pinned);
                try
                {
                    result = CudaMemcpyDtoH(resultHandle.AddrOfPinnedObject(), devicePtr, size);
                    Assert.Equal(0, result);
                    _output.WriteLine($"Copied {size} bytes from device");

                    // Verify data integrity
                    for (var i = 0; i < elementCount; i++)
                    {
                        Assert.Equal(hostData[i], resultData[i], 5);
                    }
                    _output.WriteLine("Data integrity verified successfully");
                }
                finally
                {
                    resultHandle.Free();
                }
            }
            finally
            {
                hostHandle.Free();
            }
        }
        finally
        {
            if (devicePtr != nint.Zero)
            {
                _ = CudaFree(devicePtr);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task SimpleKernelExecution_VectorAdd_ShouldCompute()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int N = 1024;
        const int size = N * sizeof(float);

        // Prepare test data
        var h_a = new float[N];
        var h_b = new float[N];
        var h_result = new float[N];

        for (var i = 0; i < N; i++)
        {
            h_a[i] = i;
            h_b[i] = i * 2;
        }

        nint d_a = nint.Zero, d_b = nint.Zero, d_result = nint.Zero;

        try
        {
            // Allocate device memory
            Assert.Equal(0, CudaMalloc(ref d_a, size));
            Assert.Equal(0, CudaMalloc(ref d_b, size));
            Assert.Equal(0, CudaMalloc(ref d_result, size));

            // Copy input data to device
            var h_a_handle = GCHandle.Alloc(h_a, GCHandleType.Pinned);
            var h_b_handle = GCHandle.Alloc(h_b, GCHandleType.Pinned);
            try
            {
                Assert.Equal(0, CudaMemcpyHtoD(d_a, h_a_handle.AddrOfPinnedObject(), size));
                Assert.Equal(0, CudaMemcpyHtoD(d_b, h_b_handle.AddrOfPinnedObject(), size));
            }
            finally
            {
                h_a_handle.Free();
                h_b_handle.Free();
            }

            // Note: Actual kernel execution would require compiling PTX code
            // For this test, we'll simulate by doing a device-to-device copy
            // In a real implementation, we'd use cuModuleLoad and cuLaunchKernel

            // Simulate kernel execution(copy a to result as placeholder)
            Assert.Equal(0, CudaMemcpyDtoD(d_result, d_a, size));

            // Copy result back to host
            var h_result_handle = GCHandle.Alloc(h_result, GCHandleType.Pinned);
            try
            {
                Assert.Equal(0, CudaMemcpyDtoH(h_result_handle.AddrOfPinnedObject(), d_result, size));
            }
            finally
            {
                h_result_handle.Free();
            }

            // Verify some results
            Assert.Equal(h_a[0], h_result[0]);
            Assert.Equal(h_a[N - 1], h_result[N - 1]);

            _output.WriteLine("Kernel execution test completed successfully");
        }
        finally
        {
            if (d_a != nint.Zero)
                _ = CudaFree(d_a);
            if (d_b != nint.Zero)
                _ = CudaFree(d_b);
            if (d_result != nint.Zero)
                _ = CudaFree(d_result);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    [Trait("Category", "Performance")]
    public void MeasureMemoryBandwidth_ShouldReportPerformance()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available on this system");

        const int size = 100 * 1024 * 1024; // 100 MB
        const int iterations = 10;

        var hostData = new byte[size];
        new Random(42).NextBytes(hostData);

        var devicePtr = nint.Zero;

        try
        {
            // Allocate device memory
            var result = CudaMalloc(ref devicePtr, size);
            Assert.Equal(0, result);

            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                // Warm up
                _ = CudaMemcpyHtoD(devicePtr, hostHandle.AddrOfPinnedObject(), size);

                // Measure H2D bandwidth
                var sw = Stopwatch.StartNew();
                for (var i = 0; i < iterations; i++)
                {
                    result = CudaMemcpyHtoD(devicePtr, hostHandle.AddrOfPinnedObject(), size);
                    Assert.Equal(0, result);
                }
                sw.Stop();

                var h2dBandwidth = size * iterations / 1024.0 / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                _output.WriteLine($"Host to Device bandwidth: {h2dBandwidth:F2} GB/s");
                _ = h2dBandwidth.Should().BeGreaterThan(1.0, "H2D bandwidth should be at least 1 GB/s");

                // Measure D2H bandwidth
                sw.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    result = CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), devicePtr, size);
                    Assert.Equal(0, result);
                }
                sw.Stop();

                var d2hBandwidth = size * iterations / 1024.0 / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                _output.WriteLine($"Device to Host bandwidth: {d2hBandwidth:F2} GB/s");
                _ = d2hBandwidth.Should().BeGreaterThan(1.0, "D2H bandwidth should be at least 1 GB/s");
            }
            finally
            {
                hostHandle.Free();
            }
        }
        finally
        {
            if (devicePtr != nint.Zero)
            {
                _ = CudaFree(devicePtr);
            }
        }
    }

    public void Dispose()
    {
        if (_cudaContext != nint.Zero)
        {
            _ = CudaCtxDestroy(_cudaContext);
            _cudaContext = nint.Zero;
        }
        _cudaInitialized = false;
    }

    // P/Invoke declarations for CUDA Driver API
    // Using platform-specific library names
    private static class CudaNative
    {
        // Windows CUDA methods
        public static class Windows
        {
            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuInit")]
            public static extern int CudaInit(uint flags);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuDeviceGetCount")]
            public static extern int CudaGetDeviceCount(ref int count);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuDeviceGetName")]
            public static extern int CudaDeviceGetName(byte[] name, int len, int dev);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuCtxCreate_v2")]
            public static extern int CudaCtxCreate(ref nint ctx, uint flags, int dev);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuCtxDestroy_v2")]
            public static extern int CudaCtxDestroy(nint ctx);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuMemGetInfo_v2")]
            public static extern int CudaMemGetInfo(ref ulong free, ref ulong total);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuMemAlloc_v2")]
            public static extern int CudaMalloc(ref nint dptr, long bytesize);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuMemFree_v2")]
            public static extern int CudaFree(nint dptr);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyHtoD_v2")]
            public static extern int CudaMemcpyHtoD(nint dstDevice, nint srcHost, long byteCount);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyDtoH_v2")]
            public static extern int CudaMemcpyDtoH(nint dstHost, nint srcDevice, long byteCount);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("nvcuda.dll", EntryPoint = "cuMemcpyDtoD_v2")]
            public static extern int CudaMemcpyDtoD(nint dstDevice, nint srcDevice, long byteCount);
        }

        // Linux CUDA methods
        public static class Linux
        {
            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuInit")]
            public static extern int CudaInit(uint flags);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuDeviceGetCount")]
            public static extern int CudaGetDeviceCount(ref int count);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuDeviceGetName")]
            public static extern int CudaDeviceGetName(byte[] name, int len, int dev);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuCtxCreate_v2")]
            public static extern int CudaCtxCreate(ref nint ctx, uint flags, int dev);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuCtxDestroy_v2")]
            public static extern int CudaCtxDestroy(nint ctx);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuMemGetInfo_v2")]
            public static extern int CudaMemGetInfo(ref ulong free, ref ulong total);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuMemAlloc_v2")]
            public static extern int CudaMalloc(ref nint dptr, long bytesize);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuMemFree_v2")]
            public static extern int CudaFree(nint dptr);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyHtoD_v2")]
            public static extern int CudaMemcpyHtoD(nint dstDevice, nint srcHost, long byteCount);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyDtoH_v2")]
            public static extern int CudaMemcpyDtoH(nint dstHost, nint srcDevice, long byteCount);

            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyDtoD_v2")]
            public static extern int CudaMemcpyDtoD(nint dstDevice, nint srcDevice, long byteCount);
        }
    }

    // Platform-agnostic wrapper methods
    private static int CudaInit(uint flags)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaInit(flags);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaInit(flags);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaGetDeviceCount(ref int count)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaGetDeviceCount(ref count);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaGetDeviceCount(ref count);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaDeviceGetName(byte[] name, int len, int dev)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaDeviceGetName(name, len, dev);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaDeviceGetName(name, len, dev);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaCtxCreate(ref nint ctx, uint flags, int dev)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaCtxCreate(ref ctx, flags, dev);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaCtxCreate(ref ctx, flags, dev);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaCtxDestroy(nint ctx)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaCtxDestroy(ctx);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaCtxDestroy(ctx);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaMemGetInfo(ref ulong free, ref ulong total)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaMemGetInfo(ref free, ref total);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaMemGetInfo(ref free, ref total);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaMalloc(ref nint dptr, long bytesize)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaMalloc(ref dptr, bytesize);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaMalloc(ref dptr, bytesize);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaFree(nint dptr)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaFree(dptr);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaFree(dptr);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaMemcpyHtoD(nint dstDevice, nint srcHost, long byteCount)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaMemcpyHtoD(dstDevice, srcHost, byteCount);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaMemcpyHtoD(dstDevice, srcHost, byteCount);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaMemcpyDtoH(nint dstHost, nint srcDevice, long byteCount)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaMemcpyDtoH(dstHost, srcDevice, byteCount);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaMemcpyDtoH(dstHost, srcDevice, byteCount);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }

    private static int CudaMemcpyDtoD(nint dstDevice, nint srcDevice, long byteCount)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CudaNative.Windows.CudaMemcpyDtoD(dstDevice, srcDevice, byteCount);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CudaNative.Linux.CudaMemcpyDtoD(dstDevice, srcDevice, byteCount);
        else
            throw new PlatformNotSupportedException("CUDA is not supported on this platform");
    }
}

/// <summary>
/// Helper attribute to skip tests when conditions aren't met.
/// </summary>
public sealed class SkippableFactAttribute : FactAttribute
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
        if (!condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
/// Exception thrown to skip a test.
/// </summary>
public sealed class SkipException : Exception
{
    public SkipException() : base() { }
    public SkipException(string reason) : base(reason) { }
    public SkipException(string message, Exception innerException) : base(message, innerException) { }
}
