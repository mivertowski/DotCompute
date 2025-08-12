using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.Benchmarks;

/// <summary>
/// Comprehensive performance benchmarks for RTX 2000 Ada Generation GPU.
/// Establishes baseline performance and validates against theoretical peak performance.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "Performance")]
[Trait("Category", "Benchmark")]
[Trait("Category", "RequiresGPU")]
public class RTX2000PerformanceBenchmarks : IDisposable
{
    private readonly ITestOutputHelper _output;
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
            
            _output.WriteLine($"Memory bandwidth ({sizeMB} MB): {bandwidth.H2D:F2} GB/s (H2D), {bandwidth.D2H:F2} GB/s (D2H), {bandwidth.D2D:F2} GB/s (D2D)");
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

    private async Task<BandwidthMeasurement> MeasureMemoryBandwidth(int sizeMB)
    {
        const int iterations = 20;
        var size = sizeMB * 1024 * 1024;
        
        var hostData = new byte[size];
        new Random(42).NextBytes(hostData);
        
        IntPtr d_ptr1 = IntPtr.Zero, d_ptr2 = IntPtr.Zero;
        
        try
        {
            CudaMalloc(ref d_ptr1, size);
            CudaMalloc(ref d_ptr2, size);
            
            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                // Warm-up
                for (int i = 0; i < 3; i++)
                {
                    CudaMemcpyHtoD(d_ptr1, hostHandle.AddrOfPinnedObject(), size);
                    CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), d_ptr1, size);
                    CudaMemcpyDtoD(d_ptr2, d_ptr1, size);
                }

                // Measure H2D
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    CudaMemcpyHtoD(d_ptr1, hostHandle.AddrOfPinnedObject(), size);
                }
                sw.Stop();
                var h2dBandwidth = (size * iterations / (1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

                // Measure D2H
                sw.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    CudaMemcpyDtoH(hostHandle.AddrOfPinnedObject(), d_ptr1, size);
                }
                sw.Stop();
                var d2hBandwidth = (size * iterations / (1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

                // Measure D2D
                sw.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    CudaMemcpyDtoD(d_ptr2, d_ptr1, size);
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
            if (d_ptr1 != IntPtr.Zero) CudaFree(d_ptr1);
            if (d_ptr2 != IntPtr.Zero) CudaFree(d_ptr2);
        }

        await Task.CompletedTask;
        return new BandwidthMeasurement();
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
            var result = NvrtcCreateProgram(ref program, emptyKernel, "empty.cu", 0, null, null);
            result.Should().Be(0, "Empty kernel program creation should succeed");

            var options = new[] { "--gpu-architecture=compute_89" };
            result = NvrtcCompileProgram(program, options.Length, options);
            result.Should().Be(0, "Empty kernel compilation should succeed");

            long ptxSize = 0;
            NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            result.Should().Be(0, "Module loading should succeed");

            var kernelName = System.Text.Encoding.ASCII.GetBytes("emptyKernel");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            result.Should().Be(0, "Kernel function retrieval should succeed");

            // Warm-up launches
            for (int i = 0; i < 100; i++)
            {
                CuLaunchKernel(kernel, 1, 1, 1, 1, 1, 1, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            CudaCtxSynchronize();

            // Measure launch overhead
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < launchCount; i++)
            {
                result = CuLaunchKernel(kernel, 1, 1, 1, 1, 1, 1, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                result.Should().Be(0, $"Kernel launch {i} should succeed");
            }
            CudaCtxSynchronize();
            sw.Stop();

            var averageLaunchTime = sw.Elapsed.TotalMicroseconds / launchCount;
            _output.WriteLine($"Average kernel launch overhead: {averageLaunchTime:F2} μs");

            // RTX 2000 Ada Gen should have low launch overhead
            averageLaunchTime.Should().BeLessThan(50.0, "Kernel launch overhead should be minimal");

            _baseline.KernelLaunchOverhead = averageLaunchTime;
        }
        finally
        {
            if (module != IntPtr.Zero) CuModuleUnload(module);
            if (program != IntPtr.Zero) NvrtcDestroyProgram(ref program);
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
    if (idx < n) {
        float val = data[idx];
        for (int i = 0; i < iterations; i++) {
            val = val * val + sqrtf(val) - sinf(val) + cosf(val);
        }
        data[idx] = val;
    }
}";

        const int dataSize = 1024 * 1024; // 1M elements
        const int computeIterations = 1000;
        
        IntPtr program = IntPtr.Zero, module = IntPtr.Zero, kernel = IntPtr.Zero;
        IntPtr deviceData = IntPtr.Zero;

        try
        {
            // Compile compute kernel
            var result = NvrtcCreateProgram(ref program, computeKernel, "compute.cu", 0, null, null);
            result.Should().Be(0);

            var options = new[] { "--gpu-architecture=compute_89", "--use_fast_math" };
            result = NvrtcCompileProgram(program, options.Length, options);
            result.Should().Be(0);

            long ptxSize = 0;
            NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            result.Should().Be(0);

            var kernelName = System.Text.Encoding.ASCII.GetBytes("computeIntensive");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            result.Should().Be(0);

            // Allocate and initialize data
            result = CudaMalloc(ref deviceData, dataSize * sizeof(float));
            result.Should().Be(0);

            var hostData = new float[dataSize];
            for (int i = 0; i < dataSize; i++)
            {
                hostData[i] = (float)(i % 100) / 100.0f;
            }

            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                result = CudaMemcpyHtoD(deviceData, hostHandle.AddrOfPinnedObject(), dataSize * sizeof(float));
                result.Should().Be(0);
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
                    int gridSize = (dataSize + blockSize - 1) / blockSize;

                    var sw = Stopwatch.StartNew();
                    result = CuLaunchKernel(
                        kernel,
                        (uint)gridSize, 1, 1,
                        (uint)blockSize, 1, 1,
                        0, IntPtr.Zero,
                        kernelParamsPtr, IntPtr.Zero);
                    result.Should().Be(0);

                    CudaCtxSynchronize();
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
            if (deviceData != IntPtr.Zero) CudaFree(deviceData);
            if (module != IntPtr.Zero) CuModuleUnload(module);
            if (program != IntPtr.Zero) NvrtcDestroyProgram(ref program);
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
    if (idx < n) {
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
            var result = NvrtcCreateProgram(ref program, latencyKernel, "latency.cu", 0, null, null);
            result.Should().Be(0);

            var options = new[] { "--gpu-architecture=compute_89" };
            result = NvrtcCompileProgram(program, options.Length, options);
            result.Should().Be(0);

            long ptxSize = 0;
            NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            NvrtcGetPTX(program, ptx);

            result = CuModuleLoadData(ref module, ptx);
            result.Should().Be(0);

            var kernelName = System.Text.Encoding.ASCII.GetBytes("measureLatency");
            result = CuModuleGetFunction(ref kernel, module, kernelName);
            result.Should().Be(0);

            // Allocate device memory
            result = CudaMalloc(ref deviceData, arraySize * sizeof(float));
            result.Should().Be(0);
            result = CudaMalloc(ref deviceIndices, arraySize * sizeof(int));
            result.Should().Be(0);
            result = CudaMalloc(ref deviceResults, arraySize * sizeof(float));
            result.Should().Be(0);

            // Create random access pattern to measure worst-case latency
            var hostData = new float[arraySize];
            var hostIndices = new int[arraySize];
            var random = new Random(42);
            
            for (int i = 0; i < arraySize; i++)
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
                result.Should().Be(0);
                result = CudaMemcpyHtoD(deviceIndices, indicesHandle.AddrOfPinnedObject(), arraySize * sizeof(int));
                result.Should().Be(0);
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
                    int gridSize = (arraySize + blockSize - 1) / blockSize;

                    // Warm up
                    CuLaunchKernel(kernel, (uint)gridSize, 1, 1, (uint)blockSize, 1, 1, 0, IntPtr.Zero, kernelParamsPtr, IntPtr.Zero);
                    CudaCtxSynchronize();

                    // Measure
                    var sw = Stopwatch.StartNew();
                    result = CuLaunchKernel(kernel, (uint)gridSize, 1, 1, (uint)blockSize, 1, 1, 0, IntPtr.Zero, kernelParamsPtr, IntPtr.Zero);
                    result.Should().Be(0);
                    CudaCtxSynchronize();
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
            if (deviceData != IntPtr.Zero) CudaFree(deviceData);
            if (deviceIndices != IntPtr.Zero) CudaFree(deviceIndices);
            if (deviceResults != IntPtr.Zero) CudaFree(deviceResults);
            if (module != IntPtr.Zero) CuModuleUnload(module);
            if (program != IntPtr.Zero) NvrtcDestroyProgram(ref program);
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
            CudaCtxDestroy(_cudaContext);
            _cudaContext = IntPtr.Zero;
        }
        _cudaInitialized = false;
    }

    #region Supporting Classes

    private class PerformanceBaseline
    {
        public BandwidthMeasurement MemoryBandwidth { get; set; } = new();
        public double KernelLaunchOverhead { get; set; }
        public double ComputeThroughputGFLOPS { get; set; }
        public double MemoryLatencyNs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
    }

    private class MemoryBandwidthResults
    {
        public List<double> H2DBandwidths { get; } = new();
        public List<double> D2HBandwidths { get; } = new();
        public List<double> D2DBandwidths { get; } = new();

        public void AddResult(int sizeMB, BandwidthMeasurement bandwidth)
        {
            H2DBandwidths.Add(bandwidth.H2D);
            D2HBandwidths.Add(bandwidth.D2H);
            D2DBandwidths.Add(bandwidth.D2D);
        }
    }

    private struct BandwidthMeasurement
    {
        public double H2D { get; set; }
        public double D2H { get; set; }
        public double D2D { get; set; }
    }

    #endregion

    #region Native Methods

    [DllImport("nvcuda", EntryPoint = "cuInit")]
    private static extern int CudaInit(uint flags);

    [DllImport("nvcuda", EntryPoint = "cuCtxCreate_v2")]
    private static extern int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev);

    [DllImport("nvcuda", EntryPoint = "cuCtxDestroy_v2")]
    private static extern int CudaCtxDestroy(IntPtr ctx);

    [DllImport("nvcuda", EntryPoint = "cuCtxSynchronize")]
    private static extern int CudaCtxSynchronize();

    [DllImport("nvcuda", EntryPoint = "cuMemAlloc_v2")]
    private static extern int CudaMalloc(ref IntPtr dptr, long bytesize);

    [DllImport("nvcuda", EntryPoint = "cuMemFree_v2")]
    private static extern int CudaFree(IntPtr dptr);

    [DllImport("nvcuda", EntryPoint = "cuMemcpyHtoD_v2")]
    private static extern int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount);

    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoH_v2")]
    private static extern int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount);

    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoD_v2")]
    private static extern int CudaMemcpyDtoD(IntPtr dstDevice, IntPtr srcDevice, long byteCount);

    [DllImport("nvcuda", EntryPoint = "cuModuleLoadData")]
    private static extern int CuModuleLoadData(ref IntPtr module, byte[] image);

    [DllImport("nvcuda", EntryPoint = "cuModuleUnload")]
    private static extern int CuModuleUnload(IntPtr module);

    [DllImport("nvcuda", EntryPoint = "cuModuleGetFunction")]
    private static extern int CuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, byte[] name);

    [DllImport("nvcuda", EntryPoint = "cuLaunchKernel")]
    private static extern int CuLaunchKernel(IntPtr f, uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ, uint sharedMemBytes, IntPtr hStream,
        IntPtr kernelParams, IntPtr extra);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCreateProgram")]
    private static extern int NvrtcCreateProgram(ref IntPtr prog, string src, string name,
        int numHeaders, string[] headers, string[] includeNames);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcDestroyProgram")]
    private static extern int NvrtcDestroyProgram(ref IntPtr prog);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCompileProgram")]
    private static extern int NvrtcCompileProgram(IntPtr prog, int numOptions, string[] options);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTXSize")]
    private static extern int NvrtcGetPTXSize(IntPtr prog, ref long ptxSizeRet);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTX")]
    private static extern int NvrtcGetPTX(IntPtr prog, byte[] ptx);

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
        if (!condition)
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