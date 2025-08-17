using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class CudaBenchmarks : IDisposable
{
    private IntPtr _cudaContext;
    private IntPtr _deviceMemory;
    private byte[] _hostData = null!;
    private bool _cudaAvailable;

    [Params(1024 * 1024, 16 * 1024 * 1024, 128 * 1024 * 1024)] // 1MB, 16MB, 128MB
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _hostData = new byte[DataSize];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        Random.Shared.NextBytes(_hostData);
#pragma warning restore CA5394

        try
        {
            // Initialize CUDA
            var result = CudaInit(0);
            if (result != 0)
            {
                _cudaAvailable = false;
                return;
            }

            // Create context
            result = CudaCtxCreate(ref _cudaContext, 0, 0);
            if (result != 0)
            {
                _cudaAvailable = false;
                return;
            }

            // Allocate device memory
            result = CudaMalloc(ref _deviceMemory, DataSize);
            _cudaAvailable = result == 0;
        }
        catch
        {
            _cudaAvailable = false;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_cudaAvailable)
        {
            if (_deviceMemory != IntPtr.Zero)
            {
                _ = CudaFree(_deviceMemory);
            }

            if (_cudaContext != IntPtr.Zero)
            {
                _ = CudaCtxDestroy(_cudaContext);
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("CUDA")]
    public int HostToDevice_CUDA()
    {
        if (!_cudaAvailable)
        {
            return -1;
        }

        var handle = GCHandle.Alloc(_hostData, GCHandleType.Pinned);
        try
        {
            return CudaMemcpyHtoD(_deviceMemory, handle.AddrOfPinnedObject(), DataSize);
        }
        finally
        {
            handle.Free();
        }
    }

    [Benchmark]
    [BenchmarkCategory("CUDA")]
    public int DeviceToHost_CUDA()
    {
        if (!_cudaAvailable)
        {
            return -1;
        }

        var result = new byte[DataSize];
        var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
        try
        {
            return CudaMemcpyDtoH(handle.AddrOfPinnedObject(), _deviceMemory, DataSize);
        }
        finally
        {
            handle.Free();
        }
    }

    [Benchmark]
    [BenchmarkCategory("CUDA")]
    public int DeviceToDevice_CUDA()
    {
        if (!_cudaAvailable)
        {
            return -1;
        }

        var tempDevice = IntPtr.Zero;
        try
        {
            var result = CudaMalloc(ref tempDevice, DataSize);
            if (result != 0)
            {
                return result;
            }

            result = CudaMemcpyDtoD(tempDevice, _deviceMemory, DataSize);
            _ = CudaFree(tempDevice);
            return result;
        }
        catch
        {
            if (tempDevice != IntPtr.Zero)
            {
                _ = CudaFree(tempDevice);
            }

            return -1;
        }
    }

    [Benchmark]
    [BenchmarkCategory("CUDA")]
    public (ulong free, ulong total) GetMemoryInfo_CUDA()
    {
        if (!_cudaAvailable)
        {
            return (0, 0);
        }

        ulong free = 0, total = 0;
        _ = CudaMemGetInfo(ref free, ref total);
        return (free, total);
    }

    // P/Invoke declarations
    [DllImport("libcuda.so.1", EntryPoint = "cuInit", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaInit(uint flags);

    [DllImport("libcuda.so.1", EntryPoint = "cuCtxCreate_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev);

    [DllImport("libcuda.so.1", EntryPoint = "cuCtxDestroy_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaCtxDestroy(IntPtr ctx);

    [DllImport("libcuda.so.1", EntryPoint = "cuMemAlloc_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaMalloc(ref IntPtr dptr, long bytesize);

    [DllImport("libcuda.so.1", EntryPoint = "cuMemFree_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaFree(IntPtr dptr);

    [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyHtoD_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount);

    [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyDtoH_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount);

    [DllImport("libcuda.so.1", EntryPoint = "cuMemcpyDtoD_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaMemcpyDtoD(IntPtr dstDevice, IntPtr srcDevice, long byteCount);

    [DllImport("libcuda.so.1", EntryPoint = "cuMemGetInfo_v2", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories | DllImportSearchPath.System32)]
    private static extern int CudaMemGetInfo(ref ulong free, ref ulong total);

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}