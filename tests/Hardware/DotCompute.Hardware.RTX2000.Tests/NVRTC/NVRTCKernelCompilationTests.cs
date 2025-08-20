using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.RTX2000.Tests.NVRTC;


/// <summary>
/// Tests for NVRTC(NVIDIA Runtime Compilation) on RTX 2000 Ada Generation GPU.
/// Validates real-time kernel compilation and execution capabilities.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "NVRTC")]
[Trait("Category", "RequiresGPU")]
public sealed class NVRTCKernelCompilationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
#pragma warning disable CA1823 // Unused field - Logger for future use
    private static readonly ILogger Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    // Logger messages
    private static readonly Action<ILogger, string, Exception?> LogNvrtcOperation =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5001), "NVRTC operation: {Operation}");
#pragma warning restore CA1823
    private nint _cudaContext;
    private bool _cudaInitialized;
    private bool _nvrtcAvailable;

    public NVRTCKernelCompilationTests(ITestOutputHelper output)
    {
        _output = output;
        InitializeCudaAndNVRTC();
    }

    private void InitializeCudaAndNVRTC()
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

            // Create CUDA context
            result = CudaCtxCreate(ref _cudaContext, 0, 0);
            if (result == 0)
            {
                _cudaInitialized = true;
                _output.WriteLine("CUDA context created successfully");
            }

            // Check NVRTC availability
            try
            {
                int major = 0, minor = 0;
                result = NvrtcVersion(ref major, ref minor);
                if (result == 0)
                {
                    _nvrtcAvailable = true;
                    _output.WriteLine($"NVRTC available - Version {major}.{minor}");
                }
                else
                {
                    _output.WriteLine($"NVRTC version query failed with error: {result}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"NVRTC not available: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Initialization exception: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task CompileSimpleKernel_ShouldSucceed()
    {
        Skip.IfNot(_cudaInitialized && _nvrtcAvailable, "CUDA and NVRTC not available");

        const string kernelSource = @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n)
{
    int i = blockDim.x * blockIdx.x + threadIdx.x;
    if(i < n) {
        c[i] = a[i] + b[i];
    }
}";

        var program = nint.Zero;

        try
        {
            // Create NVRTC program
            var result = NvrtcCreateProgram(ref program, kernelSource, "vectorAdd.cu", 0, default!, default!);
            Assert.Equal(0, result); // NVRTC program creation should succeed;

            _output.WriteLine("NVRTC program created successfully");

            // Set compilation options for RTX 2000 Ada Gen(compute capability 8.9)
            var options = new[]
            {
            "--gpu-architecture=compute_89",
            "--device-c",
            "--std=c++17"
        };

            // Compile the program
            result = NvrtcCompileProgram(program, options.Length, options);

            // Check compilation result
            if (result != 0)
            {
                // Get compilation log
                long logSize = 0;
                _ = NvrtcGetProgramLogSize(program, ref logSize);
                if (logSize > 0)
                {
                    var log = new byte[logSize];
                    _ = NvrtcGetProgramLog(program, log);
                    var logString = Encoding.ASCII.GetString(log).TrimEnd('\0');
                    _output.WriteLine($"Compilation log:\n{logString}");
                }

                Assert.Equal(0, result); // Kernel compilation should succeed;
            }

            _output.WriteLine("Kernel compiled successfully");

            // Get compiled PTX
            long ptxSize = 0;
            result = NvrtcGetPTXSize(program, ref ptxSize);
            Assert.Equal(0, result); // PTX size query should succeed;

            var ptx = new byte[ptxSize];
            result = NvrtcGetPTX(program, ptx);
            Assert.Equal(0, result); // PTX retrieval should succeed;

            var ptxString = Encoding.ASCII.GetString(ptx).TrimEnd('\0');
            _output.WriteLine($"Generated PTX{ptxSize} bytes)");
            _output.WriteLine($"PTX preview: {ptxString[..Math.Min(200, ptxString.Length)]}...");

            // Validate PTX contains expected elements
            Assert.Contains("vectorAdd", ptxString, StringComparison.Ordinal); // "PTX should contain the kernel name";
            Assert.Contains("sm_89", ptxString, StringComparison.Ordinal); // "PTX should be compiled for compute capability 8.9";
        }
        finally
        {
            if (program != nint.Zero)
            {
                _ = NvrtcDestroyProgram(ref program);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task CompileAndExecuteKernel_ShouldProduceCorrectResults()
    {
        Skip.IfNot(_cudaInitialized && _nvrtcAvailable, "CUDA and NVRTC not available");

        const string kernelSource = @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n)
{
    int i = blockDim.x * blockIdx.x + threadIdx.x;
    if(i < n) {
        c[i] = a[i] + b[i];
    }
}";

        const int N = 1024;
        const int size = N * sizeof(float);

        var program = nint.Zero;
        var module = nint.Zero;
        var kernel = nint.Zero;
        nint d_a = nint.Zero, d_b = nint.Zero, d_c = nint.Zero;

        try
        {
            // Compile kernel
            var result = NvrtcCreateProgram(ref program, kernelSource, "vectorAdd.cu", 0, default!, default!);
            Assert.Equal(0, result); // Program creation should succeed;

            var options = new[] { "--gpu-architecture=compute_89" };
            result = NvrtcCompileProgram(program, options.Length, options);
            Assert.Equal(0, result); // Kernel compilation should succeed;

            // Get PTX
            long ptxSize = 0;
            _ = NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            _ = NvrtcGetPTX(program, ptx);

            // Load module from PTX
            result = CuModuleLoadData(ref module, ptx);
            Assert.Equal(0, result); // Module loading should succeed;

            // Get kernel function
            var kernelName = "vectorAdd";
            var kernelNameBytes = Encoding.ASCII.GetBytes(kernelName);
            result = CuModuleGetFunction(ref kernel, module, kernelNameBytes);
            Assert.Equal(0, result); // Kernel function retrieval should succeed;

            // Allocate device memory
            result = CudaMalloc(ref d_a, size);
            Assert.Equal(0, result); // Memory allocation for a should succeed;
            result = CudaMalloc(ref d_b, size);
            Assert.Equal(0, result); // Memory allocation for b should succeed;
            result = CudaMalloc(ref d_c, size);
            Assert.Equal(0, result); // Memory allocation for c should succeed;

            // Prepare host data
            var h_a = new float[N];
            var h_b = new float[N];
            var h_c = new float[N];

            for (var i = 0; i < N; i++)
            {
                h_a[i] = i;
                h_b[i] = i * 2;
            }

            // Copy data to device
            var h_a_handle = GCHandle.Alloc(h_a, GCHandleType.Pinned);
            var h_b_handle = GCHandle.Alloc(h_b, GCHandleType.Pinned);
            try
            {
                result = CudaMemcpyHtoD(d_a, h_a_handle.AddrOfPinnedObject(), size);
                Assert.Equal(0, result); // H2D copy for a should succeed;
                result = CudaMemcpyHtoD(d_b, h_b_handle.AddrOfPinnedObject(), size);
                Assert.Equal(0, result); // H2D copy for b should succeed;
            }
            finally
            {
                h_a_handle.Free();
                h_b_handle.Free();
            }

            // Prepare kernel parameters
            var kernelParams = new nint[]
            {
            Marshal.AllocHGlobal(nint.Size),
            Marshal.AllocHGlobal(nint.Size),
            Marshal.AllocHGlobal(nint.Size),
            Marshal.AllocHGlobal(sizeof(int))
            };

            try
            {
                Marshal.WriteIntPtr(kernelParams[0], d_a);
                Marshal.WriteIntPtr(kernelParams[1], d_b);
                Marshal.WriteIntPtr(kernelParams[2], d_c);
                Marshal.WriteInt32(kernelParams[3], N);

                var kernelParamsPtr = Marshal.AllocHGlobal(kernelParams.Length * nint.Size);
                try
                {
                    Marshal.Copy(kernelParams, 0, kernelParamsPtr, kernelParams.Length);

                    // Launch kernel
                    const int blockSize = 256;
                    var gridSize = (N + blockSize - 1) / blockSize;

                    var sw = Stopwatch.StartNew();
                    result = CuLaunchKernel(
                        kernel,
                       (uint)gridSize, 1, 1,    // grid dimensions
                       blockSize, 1, 1,   // block dimensions
                        0,                       // shared memory
                        nint.Zero,            // stream
                        kernelParamsPtr,        // parameters
                        nint.Zero             // extra parameters
                    );
                    Assert.Equal(0, result); // Kernel launch should succeed;

                    // Synchronize
                    result = CudaCtxSynchronize();
                    Assert.Equal(0, result); // Context synchronization should succeed;
                    sw.Stop();

                    _output.WriteLine($"Kernel executed in {sw.ElapsedTicks * 1000000.0 / Stopwatch.Frequency:F1} microseconds");

                    // Copy result back
                    var h_c_handle = GCHandle.Alloc(h_c, GCHandleType.Pinned);
                    try
                    {
                        result = CudaMemcpyDtoH(h_c_handle.AddrOfPinnedObject(), d_c, size);
                        Assert.Equal(0, result); // D2H copy should succeed;
                    }
                    finally
                    {
                        h_c_handle.Free();
                    }

                    // Verify results
                    for (var i = 0; i < Math.Min(10, N); i++)
                    {
                        var expected = h_a[i] + h_b[i];
                        _ = h_c[i].Should().Be(expected, $"Result at index {i} should be correct");
                    }

                    _output.WriteLine($"Vector addition completed successfully. Sample results:");
                    for (var i = 0; i < Math.Min(5, N); i++)
                    {
                        _output.WriteLine($"  {h_a[i]} + {h_b[i]} = {h_c[i]}");
                    }
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
            // Cleanup
            if (d_a != nint.Zero)
                _ = CudaFree(d_a);
            if (d_b != nint.Zero)
                _ = CudaFree(d_b);
            if (d_c != nint.Zero)
                _ = CudaFree(d_c);
            if (module != nint.Zero)
                _ = CuModuleUnload(module);
            if (program != nint.Zero)
                _ = NvrtcDestroyProgram(ref program);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task CompileComplexKernel_WithOptimizations_ShouldSucceed()
    {
        Skip.IfNot(_cudaInitialized && _nvrtcAvailable, "CUDA and NVRTC not available");

        const string complexKernelSource = @"
#include <cuda_runtime.h>
#include <math.h>

extern ""C"" __global__ void matrixMul(
    float* A, float* B, float* C, 
    int widthA, int heightA, int widthB)
{
    int tx = threadIdx.x;
    int ty = threadIdx.y;
    int bx = blockIdx.x;
    int by = blockIdx.y;
    
    int row = by * blockDim.y + ty;
    int col = bx * blockDim.x + tx;
    
    if(row < heightA && col < widthB) {
        float sum = 0.0f;
        for(int k = 0; k < widthA; ++k) {
            sum += A[row * widthA + k] * B[k * widthB + col];
        }
        C[row * widthB + col] = sum;
    }
}

extern ""C"" __global__ void fastMatrixMul(
    float* A, float* B, float* C, 
    int widthA, int heightA, int widthB)
{
    __shared__ float As[16][16];
    __shared__ float Bs[16][16];
    
    int tx = threadIdx.x;
    int ty = threadIdx.y;
    int bx = blockIdx.x;
    int by = blockIdx.y;
    
    int row = by * 16 + ty;
    int col = bx * 16 + tx;
    
    float sum = 0.0f;
    
    for(int m = 0; m <widthA + 15) / 16; ++m) {
        // Load tiles into shared memory
        if(row < heightA && m * 16 + tx < widthA) {
            As[ty][tx] = A[row * widthA + m * 16 + tx];
        } else {
            As[ty][tx] = 0.0f;
        }
        
        if(col < widthB && m * 16 + ty < widthA) {
            Bs[ty][tx] = B[(m * 16 + ty) * widthB + col];
        } else {
            Bs[ty][tx] = 0.0f;
        }
        
        __syncthreads();
        
        // Compute partial sum
        for(int k = 0; k < 16; ++k) {
            sum += As[ty][k] * Bs[k][tx];
        }
        
        __syncthreads();
    }
    
    if(row < heightA && col < widthB) {
        C[row * widthB + col] = sum;
    }
}";

        var program = nint.Zero;

        try
        {
            var result = NvrtcCreateProgram(ref program, complexKernelSource, "matrixMul.cu", 0, default!, default!);
            Assert.Equal(0, result); // Complex program creation should succeed;

            // Use aggressive optimization options for RTX 2000 Ada Gen
            var options = new[]
            {
            "--gpu-architecture=compute_89",
            "--use_fast_math",
            "--extra-device-vectorization",
            "--maxrregcount=64",
            "--ftz=true",
            "--prec-div=false",
            "--prec-sqrt=false",
            "--fmad=true"
        };

            var sw = Stopwatch.StartNew();
            result = NvrtcCompileProgram(program, options.Length, options);
            sw.Stop();

            if (result != 0)
            {
                long logSize = 0;
                _ = NvrtcGetProgramLogSize(program, ref logSize);
                if (logSize > 0)
                {
                    var log = new byte[logSize];
                    _ = NvrtcGetProgramLog(program, log);
                    var logString = Encoding.ASCII.GetString(log).TrimEnd('\0');
                    _output.WriteLine($"Compilation log:\n{logString}");
                }
            }

            Assert.Equal(0, result); // Complex kernel compilation should succeed;
            _output.WriteLine($"Complex kernel compiled successfully in {sw.ElapsedMilliseconds} ms");

            // Get PTX and validate optimization
            long ptxSize = 0;
            _ = NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            _ = NvrtcGetPTX(program, ptx);

            var ptxString = Encoding.ASCII.GetString(ptx).TrimEnd('\0');
            _output.WriteLine($"Generated optimized PTX{ptxSize} bytes)");

            // Check for optimization indicators
            Assert.Contains("matrixMul", ptxString, StringComparison.Ordinal); // "PTX should contain matrix multiplication kernel";
            Assert.Contains("fastMatrixMul", ptxString, StringComparison.Ordinal); // "PTX should contain optimized matrix multiplication kernel";
            Assert.Contains("shared", ptxString, StringComparison.Ordinal); // "PTX should indicate shared memory usage";
        }
        finally
        {
            if (program != nint.Zero)
            {
                _ = NvrtcDestroyProgram(ref program);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task CompilationBenchmark_ShouldMeetPerformanceExpectations()
    {
        Skip.IfNot(_cudaInitialized && _nvrtcAvailable, "CUDA and NVRTC not available");

        const int compilationRuns = 10;
        var compilationTimes = new List<long>();

        const string kernelSource = @"
extern ""C"" __global__ void benchmark(float* data, int n)
{
    int i = blockDim.x * blockIdx.x + threadIdx.x;
    if(i < n) {
        data[i] = data[i] * data[i] + sqrtf(data[i]);
    }
}";

        for (var run = 0; run < compilationRuns; run++)
        {
            var program = nint.Zero;

            try
            {
                var result = NvrtcCreateProgram(ref program, kernelSource, $"benchmark_{run}.cu", 0, default!, default!);
                Assert.Equal(0, result); // Program creation should succeed;

                var options = new[] { "--gpu-architecture=compute_89" };

                var sw = Stopwatch.StartNew();
                result = NvrtcCompileProgram(program, options.Length, options);
                sw.Stop();

                Assert.Equal(0, result); // Compilation should succeed;
                compilationTimes.Add(sw.ElapsedMilliseconds);
            }
            finally
            {
                if (program != nint.Zero)
                {
                    _ = NvrtcDestroyProgram(ref program);
                }
            }
        }

        var averageTime = compilationTimes.Average();
        var maxTime = compilationTimes.Max();
        var minTime = compilationTimes.Min();

        _output.WriteLine($"Compilation performance over {compilationRuns} runs:");
        _output.WriteLine($"  Average: {averageTime:F1} ms");
        _output.WriteLine($"  Min: {minTime} ms");
        _output.WriteLine($"  Max: {maxTime} ms");

        // NVRTC should compile simple kernels reasonably quickly
        _ = averageTime.Should().BeLessThan(1000, "Average compilation time should be under 1 second");
        _ = maxTime.Should().BeLessThan(2000, "Maximum compilation time should be under 2 seconds");

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_cudaContext != nint.Zero)
        {
            _ = CudaCtxDestroy(_cudaContext);
            _cudaContext = nint.Zero;
        }
        _cudaInitialized = false;
        GC.SuppressFinalize(this);
    }

    #region Native Methods

    // CUDA Driver API
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuInit", ExactSpelling = true)]
    private static extern int CudaInit(uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxCreate_v2", ExactSpelling = true)]
    private static extern int CudaCtxCreate(ref nint ctx, uint flags, int dev);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxDestroy_v2", ExactSpelling = true)]
    private static extern int CudaCtxDestroy(nint ctx);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxSynchronize", ExactSpelling = true)]
    private static extern int CudaCtxSynchronize();

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemAlloc_v2", ExactSpelling = true)]
    private static extern int CudaMalloc(ref nint dptr, long bytesize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemFree_v2", ExactSpelling = true)]
    private static extern int CudaFree(nint dptr);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyHtoD_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyHtoD(nint dstDevice, nint srcHost, long byteCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoH_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyDtoH(nint dstHost, nint srcDevice, long byteCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuModuleLoadData", ExactSpelling = true)]
    private static extern int CuModuleLoadData(ref nint module, byte[] image);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuModuleUnload", ExactSpelling = true)]
    private static extern int CuModuleUnload(nint module);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuModuleGetFunction", ExactSpelling = true)]
    private static extern int CuModuleGetFunction(ref nint hfunc, nint hmod, byte[] name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuLaunchKernel", ExactSpelling = true)]
    private static extern int CuLaunchKernel(
        nint f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        nint hStream,
        nint kernelParams,
        nint extra);

    // NVRTC API
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcVersion", ExactSpelling = true)]
    private static extern int NvrtcVersion(ref int major, ref int minor);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCreateProgram", ExactSpelling = true, CharSet = CharSet.Ansi)]
    private static extern int NvrtcCreateProgram(
        ref nint prog,
        [MarshalAs(UnmanagedType.LPStr)] string src,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int numHeaders,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] headers,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] includeNames);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcDestroyProgram", ExactSpelling = true)]
    private static extern int NvrtcDestroyProgram(ref nint prog);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCompileProgram", ExactSpelling = true)]
    private static extern int NvrtcCompileProgram(nint prog, int numOptions, string[] options);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTXSize", ExactSpelling = true)]
    private static extern int NvrtcGetPTXSize(nint prog, ref long ptxSizeRet);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTX", ExactSpelling = true)]
    private static extern int NvrtcGetPTX(nint prog, [Out] byte[] ptx);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetProgramLogSize", ExactSpelling = true)]
    private static extern int NvrtcGetProgramLogSize(nint prog, ref long logSizeRet);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetProgramLog", ExactSpelling = true)]
    private static extern int NvrtcGetProgramLog(nint prog, [Out] byte[] log);

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
