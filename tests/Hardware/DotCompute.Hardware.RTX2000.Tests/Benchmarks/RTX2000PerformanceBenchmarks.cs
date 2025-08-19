using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware.Benchmarks
{

/// <summary>
/// Comprehensive performance benchmarks for RTX 2000 Ada Generation GPU.
/// Establishes baseline performance and validates against theoretical peak performance.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "Performance")]
[Trait("Category", "Benchmark")]
[Trait("Category", "RequiresGPU")]
public sealed class RTX2000PerformanceBenchmarks : IDisposable
{
    private readonly ITestOutputHelper _output;
#pragma warning disable CA1823 // Unused field - Logger for future use
    private static readonly ILogger Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
#pragma warning restore CA1823
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    
    // Logger messages
#pragma warning disable CA1823 // Unused field - Logger messages for future use
    private static readonly Action<ILogger, string, Exception?> LogBenchmarkStart =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3001), "Starting benchmark: {BenchmarkName}");
    
    private static readonly Action<ILogger, double, Exception?> LogBandwidthResult =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(3002), "Bandwidth measurement: {Bandwidth} GB/s");
#pragma warning restore CA1823
    private IntPtr _cudaContext;
    private bool _cudaInitialized;
    private readonly PerformanceBaseline _baseline;

    public RTX2000PerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        _baseline = new PerformanceBaseline();
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
                    _output.WriteLine("CUDA context initialized for benchmarking");
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"CUDA initialization failed: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task BenchmarkMemoryBandwidth_ShouldMeetGDDR6Specifications()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        var results = new MemoryBandwidthResults();

        // Test different transfer sizes
        var transferSizes = new[] { 1, 4, 16, 64, 256, 1024 }; // MB

        foreach (var sizeMB in transferSizes)
        {
            var bandwidth = await MeasureMemoryBandwidth(sizeMB);
            results.AddResult(sizeMB, bandwidth);

            _output.WriteLine($"Memory bandwidth{sizeMB} MB): {bandwidth.H2D:F2} GB/s(H2D), {bandwidth.D2H:F2} GB/s(D2H), {bandwidth.D2D:F2} GB/s(D2D)");
        }

        // Validate against RTX 2000 Ada Gen specifications
        var maxH2DBandwidth = results.H2DBandwidths.Max();
        var maxD2DBandwidth = results.D2DBandwidths.Max();

        maxH2DBandwidth.Should().BeGreaterThan(15.0, "H2D bandwidth should exceed PCIe 4.0 x16 minimum");
        maxD2DBandwidth.Should().BeGreaterThan(200.0, "D2D bandwidth should approach GDDR6 theoretical bandwidth");

        _output.WriteLine($"Peak bandwidth - H2D: {maxH2DBandwidth:F2} GB/s, D2D: {maxD2DBandwidth:F2} GB/s");

        // Store baseline for regression testing
        _baseline.MemoryBandwidth = new BandwidthMeasurement
        {
            H2D = maxH2DBandwidth,
            D2H = results.D2HBandwidths.Max(),
            D2D = maxD2DBandwidth
        };
    }

    private static Task<BandwidthMeasurement> MeasureMemoryBandwidth(int sizeMB) => Task.Run(() => MeasureMemoryBandwidthSync(sizeMB));

    private static BandwidthMeasurement MeasureMemoryBandwidthSync(int sizeMB)
    {
        const int iterations = 20;
        var size = sizeMB * 1024 * 1024;

        var hostData = new byte[size];
        new Random(42).NextBytes(hostData);

        IntPtr d_ptr1 = IntPtr.Zero, d_ptr2 = IntPtr.Zero;

        try
        {
            _ = CudaMalloc(ref d_ptr1, size);
            _ = CudaMalloc(ref d_ptr2, size);

            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                // Warm-up
                for (var i = 0; i < 3; i++)
                {
                    _ = CudaMemcpyHtoD(d_ptr1, hostHandle.AddrOfPinnedObject(), size);
                    _ = CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), d_ptr1, size);
                    _ = CudaMemcpyDtoD(d_ptr2, d_ptr1, size);
                }

                // Measure H2D
                var sw = Stopwatch.StartNew();
                for (var i = 0; i < iterations; i++)
                {
                    _ = CudaMemcpyHtoD(d_ptr1, hostHandle.AddrOfPinnedObject(), size);
                }
                sw.Stop();
                var h2dBandwidth = (size * iterations / (1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

                // Measure D2H
                sw.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    _ = CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), d_ptr1, size);
                }
                sw.Stop();
                var d2hBandwidth = (size * iterations / (1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

                // Measure D2D
                sw.Restart();
                for (var i = 0; i < iterations; i++)
                {
                    _ = CudaMemcpyDtoD(d_ptr2, d_ptr1, size);
                }
                sw.Stop();
                var d2dBandwidth = (size * iterations / (1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

                return new BandwidthMeasurement { H2D = h2dBandwidth, D2H = d2hBandwidth, D2D = d2dBandwidth };
            }
            finally
            {
                hostHandle.Free();
            }
        }
        finally
        {
            if (d_ptr1 != IntPtr.Zero)
                _ = CudaFree(d_ptr1);
            if (d_ptr2 != IntPtr.Zero)
                _ = CudaFree(d_ptr2);
        }
    }

    [SkippableFact]
    public async Task BenchmarkKernelLaunchOverhead_ShouldBeMinimal()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        const int launchCount = 1000;
        const string emptyKernel = @"extern ""C"" __global__ void emptyKernel() { }";

        // Compile empty kernel for overhead measurement
        IntPtr program = IntPtr.Zero, module = IntPtr.Zero, kernel = IntPtr.Zero;

        try
        {
            var result = NvrtcCreateProgram(ref program, emptyKernel, "empty.cu", 0, default!, default!);
            Assert.Equal(0, result); // Empty kernel program creation should succeed;

            var options = new[] { "--gpu-architecture=compute_89" };
            result = NvrtcCompileProgram(program, options.Length, options);
            Assert.Equal(0, result); // Empty kernel compilation should succeed;

            long ptxSize = 0;
            _ = NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            _ = NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            Assert.Equal(0, result); // Module loading should succeed;

            var kernelName = System.Text.Encoding.ASCII.GetBytes("emptyKernel");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            Assert.Equal(0, result); // Kernel function retrieval should succeed;

            // Warm-up launches
            for (var i = 0; i < 100; i++)
            {
                _ = CuLaunchKernel(kernel, 1, 1, 1, 1, 1, 1, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            _ = CudaCtxSynchronize();

            // Measure launch overhead
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < launchCount; i++)
            {
                result = CuLaunchKernel(kernel, 1, 1, 1, 1, 1, 1, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                result.Should().Be(0, $"Kernel launch {i} should succeed");
            }
            _ = CudaCtxSynchronize();
            sw.Stop();

            var averageLaunchTime = sw.Elapsed.TotalMicroseconds / launchCount;
            _output.WriteLine($"Average kernel launch overhead: {averageLaunchTime:F2} μs");

            // RTX 2000 Ada Gen should have low launch overhead
            averageLaunchTime.Should().BeLessThan(50.0, "Kernel launch overhead should be minimal");

            _baseline.KernelLaunchOverhead = averageLaunchTime;
        }
        finally
        {
            if (module != IntPtr.Zero)
                _ = CuModuleUnload(module);
            if (program != IntPtr.Zero)
                _ = NvrtcDestroyProgram(ref program);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task BenchmarkComputeThroughput_ShouldMeetFP32Specifications()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        const string computeKernel = @"
extern ""C"" __global__ void computeIntensive(float* data, int n, int iterations)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if(idx < n) {
        float val = data[idx];
        for(int i = 0; i < iterations; i++) {
            val = val * val + sqrtf(val) - sinf(val) + cosf(val);
        }
        data[idx] = val;
    }
}";

        const int dataSize = 1024 * 1024; // 1M elements
        const int computeIterations = 1000;

        IntPtr program = IntPtr.Zero, module = IntPtr.Zero, kernel = IntPtr.Zero;
        var deviceData = IntPtr.Zero;

        try
        {
            // Compile compute kernel
            var result = NvrtcCreateProgram(ref program, computeKernel, "compute.cu", 0, null!, null!);
            Assert.Equal(0, result);

            var options = new[] { "--gpu-architecture=compute_89", "--use_fast_math" };
            result = NvrtcCompileProgram(program, options.Length, options);
            Assert.Equal(0, result);

            long ptxSize = 0;
            _ = NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            _ = NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            Assert.Equal(0, result);

            var kernelName = System.Text.Encoding.ASCII.GetBytes("computeIntensive");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            Assert.Equal(0, result);

            // Allocate and initialize data
            result = CudaMalloc(ref deviceData, dataSize * sizeof(float));
            Assert.Equal(0, result);

            var hostData = new float[dataSize];
            for (var i = 0; i < dataSize; i++)
            {
                hostData[i] = (float)(i % 100) / 100.0f;
            }

            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                result = CudaMemcpyHtoD(deviceData, hostHandle.AddrOfPinnedObject(), dataSize * sizeof(float));
                Assert.Equal(0, result);
            }
            finally
            {
                hostHandle.Free();
            }

            // Prepare kernel parameters
            var kernelParams = new IntPtr[]
            {
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(sizeof(int)),
                Marshal.AllocHGlobal(sizeof(int))
            };

            try
            {
                Marshal.WriteIntPtr(kernelParams[0], deviceData);
                Marshal.WriteInt32(kernelParams[1], dataSize);
                Marshal.WriteInt32(kernelParams[2], computeIterations);

                var kernelParamsPtr = Marshal.AllocHGlobal(kernelParams.Length * IntPtr.Size);
                try
                {
                    Marshal.Copy(kernelParams, 0, kernelParamsPtr, kernelParams.Length);

                    // Launch compute-intensive kernel
                    const int blockSize = 256;
                    var gridSize = (dataSize + blockSize - 1) / blockSize;

                    var sw = Stopwatch.StartNew();
                    result = CuLaunchKernel(
                        kernel,
                       (uint)gridSize, 1, 1,
                       (uint)blockSize, 1, 1,
                        0, IntPtr.Zero,
                        kernelParamsPtr, IntPtr.Zero);
                    Assert.Equal(0, result);

                    _ = CudaCtxSynchronize();
                    sw.Stop();

                    var totalOperations = (long)dataSize * computeIterations * 6; // 6 operations per iteration
                    var gflops = totalOperations / (sw.Elapsed.TotalSeconds * 1e9);

                    _output.WriteLine($"Compute throughput: {gflops:F2} GFLOPS");
                    _output.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms");

                    // RTX 2000 Ada Gen should achieve substantial GFLOPS
                    gflops.Should().BeGreaterThan(1000, "Should achieve significant compute throughput");

                    _baseline.ComputeThroughputGFLOPS = gflops;
                }
                finally
                {
                    Marshal.FreeHGlobal(kernelParamsPtr);
                }
            }
            finally
            {
                foreach (var param in kernelParams)
                {
                    Marshal.FreeHGlobal(param);
                }
            }
        }
        finally
        {
            if (deviceData != IntPtr.Zero)
                _ = CudaFree(deviceData);
            if (module != IntPtr.Zero)
                _ = CuModuleUnload(module);
            if (program != IntPtr.Zero)
                _ = NvrtcDestroyProgram(ref program);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task BenchmarkMemoryLatency_ShouldBeOptimal()
    {
        Skip.IfNot(_cudaInitialized, "CUDA not available");

        const string latencyKernel = @"
extern ""C"" __global__ void measureLatency(float* data, int* indices, float* results, int n)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if(idx < n) {
        int pointer_chase_idx = indices[idx];
        results[idx] = data[pointer_chase_idx];
    }
}";

        const int arraySize = 1024 * 1024; // 1M elements for cache miss testing

        IntPtr program = IntPtr.Zero, module = IntPtr.Zero, kernel = IntPtr.Zero;
        IntPtr deviceData = IntPtr.Zero, deviceIndices = IntPtr.Zero, deviceResults = IntPtr.Zero;

        try
        {
            // Compile latency measurement kernel
            var result = NvrtcCreateProgram(ref program, latencyKernel, "latency.cu", 0, null!, null!);
            Assert.Equal(0, result);

            var options = new[] { "--gpu-architecture=compute_89" };
            result = NvrtcCompileProgram(program, options.Length, options);
            Assert.Equal(0, result);

            long ptxSize = 0;
            _ = NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            _ = NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            Assert.Equal(0, result);

            var kernelName = System.Text.Encoding.ASCII.GetBytes("measureLatency");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            Assert.Equal(0, result);

            // Allocate device memory
            result = CudaMalloc(ref deviceData, arraySize * sizeof(float));
            Assert.Equal(0, result);
            result = CudaMalloc(ref deviceIndices, arraySize * sizeof(int));
            Assert.Equal(0, result);
            result = CudaMalloc(ref deviceResults, arraySize * sizeof(float));
            Assert.Equal(0, result);

            // Create random access pattern to measure worst-case latency
            var hostData = new float[arraySize];
            var hostIndices = new int[arraySize];
            var random = new Random(42);

            for (var i = 0; i < arraySize; i++)
            {
                hostData[i] = i;
                hostIndices[i] = random.Next(arraySize);
            }

            // Copy data to device
            var dataHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            var indicesHandle = GCHandle.Alloc(hostIndices, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyHtoD(deviceData, dataHandle.AddrOfPinnedObject(), arraySize * sizeof(float));
                Assert.Equal(0, result);
                result = CudaMemcpyHtoD(deviceIndices, indicesHandle.AddrOfPinnedObject(), arraySize * sizeof(int));
                Assert.Equal(0, result);
            }
            finally
            {
                dataHandle.Free();
                indicesHandle.Free();
            }

            // Measure latency
            var kernelParams = new IntPtr[]
            {
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(sizeof(int))
            };

            try
            {
                Marshal.WriteIntPtr(kernelParams[0], deviceData);
                Marshal.WriteIntPtr(kernelParams[1], deviceIndices);
                Marshal.WriteIntPtr(kernelParams[2], deviceResults);
                Marshal.WriteInt32(kernelParams[3], arraySize);

                var kernelParamsPtr = Marshal.AllocHGlobal(kernelParams.Length * IntPtr.Size);
                try
                {
                    Marshal.Copy(kernelParams, 0, kernelParamsPtr, kernelParams.Length);

                    const int blockSize = 256;
                    var gridSize = (arraySize + blockSize - 1) / blockSize;

                    // Warm up
                    _ = CuLaunchKernel(kernel, (uint)gridSize, 1, 1, (uint)blockSize, 1, 1, 0, IntPtr.Zero, kernelParamsPtr, IntPtr.Zero);
                    _ = CudaCtxSynchronize();

                    // Measure
                    var sw = Stopwatch.StartNew();
                    result = CuLaunchKernel(kernel, (uint)gridSize, 1, 1, (uint)blockSize, 1, 1, 0, IntPtr.Zero, kernelParamsPtr, IntPtr.Zero);
                    Assert.Equal(0, result);
                    _ = CudaCtxSynchronize();
                    sw.Stop();

                    var averageLatency = (sw.Elapsed.TotalNanoseconds) / arraySize;
                    _output.WriteLine($"Average memory access latency: {averageLatency:F2} ns");

                    // GDDR6 should provide reasonable latency for random access
                    averageLatency.Should().BeLessThan(1000, "Memory latency should be reasonable for GDDR6");

                    _baseline.MemoryLatencyNs = averageLatency;
                }
                finally
                {
                    Marshal.FreeHGlobal(kernelParamsPtr);
                }
            }
            finally
            {
                foreach (var param in kernelParams)
                {
                    Marshal.FreeHGlobal(param);
                }
            }
        }
        finally
        {
            if (deviceData != IntPtr.Zero)
                _ = CudaFree(deviceData);
            if (deviceIndices != IntPtr.Zero)
                _ = CudaFree(deviceIndices);
            if (deviceResults != IntPtr.Zero)
                _ = CudaFree(deviceResults);
            if (module != IntPtr.Zero)
                _ = CuModuleUnload(module);
            if (program != IntPtr.Zero)
                _ = NvrtcDestroyProgram(ref program);
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task SavePerformanceBaseline_ForRegressionTesting()
    {
        if (!_cudaInitialized)
        {
            _output.WriteLine("CUDA not available - skipping baseline creation");
            return;
        }

        // Run essential benchmarks to establish baseline
        await BenchmarkMemoryBandwidth_ShouldMeetGDDR6Specifications();
        await BenchmarkKernelLaunchOverhead_ShouldBeMinimal();
        await BenchmarkComputeThroughput_ShouldMeetFP32Specifications();
        await BenchmarkMemoryLatency_ShouldBeOptimal();

        var baselineJson = _baseline.ToJson();
        _output.WriteLine("Performance Baseline:");
        _output.WriteLine(baselineJson);

        // Save baseline to file for future regression testing
        var baselinePath = "/tmp/rtx2000_performance_baseline.json";
        await System.IO.File.WriteAllTextAsync(baselinePath, baselineJson);
        _output.WriteLine($"Baseline saved to: {baselinePath}");
    }

    public void Dispose()
    {
        if (_cudaContext != IntPtr.Zero)
        {
            _ = CudaCtxDestroy(_cudaContext);
            _cudaContext = IntPtr.Zero;
        }
        _cudaInitialized = false;
        GC.SuppressFinalize(this);
    }

    #region Supporting Classes

    internal sealed class PerformanceBaseline
    {
        public BandwidthMeasurement MemoryBandwidth { get; set; }
        public double KernelLaunchOverhead { get; set; }
        public double ComputeThroughputGFLOPS { get; set; }
        public double MemoryLatencyNs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string ToJson()
        {
#pragma warning disable IL2026, IL3050 // JsonSerializer AOT warnings - test code only
            return JsonSerializer.Serialize(this, JsonOptions);
#pragma warning restore IL2026, IL3050
        }
    }

    internal sealed class MemoryBandwidthResults
    {
        public List<double> H2DBandwidths { get; } = [];
        public List<double> D2HBandwidths { get; } = [];
        public List<double> D2DBandwidths { get; } = [];

        public void AddResult(int sizeMB, BandwidthMeasurement bandwidth)
        {
            H2DBandwidths.Add(bandwidth.H2D);
            D2HBandwidths.Add(bandwidth.D2H);
            D2DBandwidths.Add(bandwidth.D2D);
        }
    }

    internal struct BandwidthMeasurement : IEquatable<BandwidthMeasurement>
    {
        public double H2D { get; set; }
        public double D2H { get; set; }
        public double D2D { get; set; }
        
        public readonly bool Equals(BandwidthMeasurement other) => H2D.Equals(other.H2D) && D2H.Equals(other.D2H) && D2D.Equals(other.D2D);
            
        public override readonly bool Equals(object? obj) => obj is BandwidthMeasurement other && Equals(other);
        
        public override readonly int GetHashCode() => HashCode.Combine(H2D, D2H, D2D);
        
        public static bool operator ==(BandwidthMeasurement left, BandwidthMeasurement right) => left.Equals(right);
        
        public static bool operator !=(BandwidthMeasurement left, BandwidthMeasurement right) => !left.Equals(right);
    }

    #endregion

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
    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoD_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyDtoD(IntPtr dstDevice, IntPtr srcDevice, long byteCount);

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
    [DllImport("nvcuda", EntryPoint = "cuLaunchKernel", ExactSpelling = true)]
    private static extern int CuLaunchKernel(IntPtr f, uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ, uint sharedMemBytes, IntPtr hStream,
        IntPtr kernelParams, IntPtr extra);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCreateProgram", ExactSpelling = true, CharSet = CharSet.Ansi)]
    private static extern int NvrtcCreateProgram(ref IntPtr prog, 
        [MarshalAs(UnmanagedType.LPStr)] string src, 
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int numHeaders, 
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] headers, 
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] includeNames);

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
}
