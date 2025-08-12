using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.NVRTC;

/// <summary>
/// Tests for NVRTC (NVIDIA Runtime Compilation) on RTX 2000 Ada Generation GPU.
/// Validates real-time kernel compilation and execution capabilities.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "NVRTC")]
[Trait("Category", "RequiresGPU")]
public class NVRTCKernelCompilationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private IntPtr _cudaContext;
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
    if (i < n) {
        c[i] = a[i] + b[i];
    }
}";

        IntPtr program = IntPtr.Zero;
        
        try
        {
            // Create NVRTC program
            var result = NvrtcCreateProgram(ref program, kernelSource, "vectorAdd.cu", 0, null, null);
            result.Should().Be(0, "NVRTC program creation should succeed");

            _output.WriteLine("NVRTC program created successfully");

            // Set compilation options for RTX 2000 Ada Gen (compute capability 8.9)
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
                NvrtcGetProgramLogSize(program, ref logSize);
                if (logSize > 0)
                {
                    var log = new byte[logSize];
                    NvrtcGetProgramLog(program, log);
                    var logString = Encoding.ASCII.GetString(log).TrimEnd('\0');
                    _output.WriteLine($"Compilation log:\n{logString}");
                }
                
                result.Should().Be(0, "Kernel compilation should succeed");
            }

            _output.WriteLine("Kernel compiled successfully");

            // Get compiled PTX
            long ptxSize = 0;
            result = NvrtcGetPTXSize(program, ref ptxSize);
            result.Should().Be(0, "PTX size query should succeed");

            var ptx = new byte[ptxSize];
            result = NvrtcGetPTX(program, ptx);
            result.Should().Be(0, "PTX retrieval should succeed");

            var ptxString = Encoding.ASCII.GetString(ptx).TrimEnd('\0');
            _output.WriteLine($"Generated PTX ({ptxSize} bytes)");
            _output.WriteLine($"PTX preview: {ptxString.Substring(0, Math.Min(200, ptxString.Length))}...");

            // Validate PTX contains expected elements
            ptxString.Should().Contain("vectorAdd", "PTX should contain the kernel name");
            ptxString.Should().Contain("sm_89", "PTX should be compiled for compute capability 8.9");
        }
        finally
        {
            if (program != IntPtr.Zero)
            {
                NvrtcDestroyProgram(ref program);
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
    if (i < n) {
        c[i] = a[i] + b[i];
    }
}";

        const int N = 1024;
        const int size = N * sizeof(float);

        IntPtr program = IntPtr.Zero;
        IntPtr module = IntPtr.Zero;
        IntPtr kernel = IntPtr.Zero;
        IntPtr d_a = IntPtr.Zero, d_b = IntPtr.Zero, d_c = IntPtr.Zero;

        try
        {
            // Compile kernel
            var result = NvrtcCreateProgram(ref program, kernelSource, "vectorAdd.cu", 0, null, null);
            result.Should().Be(0, "Program creation should succeed");

            var options = new[] { "--gpu-architecture=compute_89" };
            result = NvrtcCompileProgram(program, options.Length, options);
            result.Should().Be(0, "Kernel compilation should succeed");

            // Get PTX
            long ptxSize = 0;
            NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            NvrtcGetPTX(program, ptx);

            // Load module from PTX
            result = CuModuleLoadData(ref module, ptx);
            result.Should().Be(0, "Module loading should succeed");

            // Get kernel function
            var kernelName = "vectorAdd";
            var kernelNameBytes = Encoding.ASCII.GetBytes(kernelName);
            result = CuModuleGetFunction(ref kernel, module, kernelNameBytes);
            result.Should().Be(0, "Kernel function retrieval should succeed");

            // Allocate device memory
            result = CudaMalloc(ref d_a, size);
            result.Should().Be(0, "Memory allocation for a should succeed");
            result = CudaMalloc(ref d_b, size);
            result.Should().Be(0, "Memory allocation for b should succeed");
            result = CudaMalloc(ref d_c, size);
            result.Should().Be(0, "Memory allocation for c should succeed");

            // Prepare host data
            var h_a = new float[N];
            var h_b = new float[N];
            var h_c = new float[N];
            
            for (int i = 0; i < N; i++)
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
                result.Should().Be(0, "H2D copy for a should succeed");
                result = CudaMemcpyHtoD(d_b, h_b_handle.AddrOfPinnedObject(), size);
                result.Should().Be(0, "H2D copy for b should succeed");
            }
            finally
            {
                h_a_handle.Free();
                h_b_handle.Free();
            }

            // Prepare kernel parameters
            var kernelParams = new IntPtr[]
            {
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(IntPtr.Size),
                Marshal.AllocHGlobal(sizeof(int))
            };

            try
            {
                Marshal.WriteIntPtr(kernelParams[0], d_a);
                Marshal.WriteIntPtr(kernelParams[1], d_b);
                Marshal.WriteIntPtr(kernelParams[2], d_c);
                Marshal.WriteInt32(kernelParams[3], N);

                var kernelParamsPtr = Marshal.AllocHGlobal(kernelParams.Length * IntPtr.Size);
                try
                {
                    Marshal.Copy(kernelParams, 0, kernelParamsPtr, kernelParams.Length);

                    // Launch kernel
                    const int blockSize = 256;
                    int gridSize = (N + blockSize - 1) / blockSize;
                    
                    var sw = Stopwatch.StartNew();
                    result = CuLaunchKernel(
                        kernel,
                        (uint)gridSize, 1, 1,    // grid dimensions
                        (uint)blockSize, 1, 1,   // block dimensions
                        0,                       // shared memory
                        IntPtr.Zero,            // stream
                        kernelParamsPtr,        // parameters
                        IntPtr.Zero             // extra parameters
                    );
                    result.Should().Be(0, "Kernel launch should succeed");

                    // Synchronize
                    result = CudaCtxSynchronize();
                    result.Should().Be(0, "Context synchronization should succeed");
                    sw.Stop();

                    _output.WriteLine($"Kernel executed in {sw.ElapsedMicroseconds} microseconds");

                    // Copy result back
                    var h_c_handle = GCHandle.Alloc(h_c, GCHandleType.Pinned);
                    try
                    {
                        result = CudaMemcpyDtoH(h_c_handle.AddrOfPinnedObject(), d_c, size);
                        result.Should().Be(0, "D2H copy should succeed");
                    }
                    finally
                    {
                        h_c_handle.Free();
                    }

                    // Verify results
                    for (int i = 0; i < Math.Min(10, N); i++)
                    {
                        var expected = h_a[i] + h_b[i];
                        h_c[i].Should().Be(expected, $"Result at index {i} should be correct");
                    }

                    _output.WriteLine($"Vector addition completed successfully. Sample results:");
                    for (int i = 0; i < Math.Min(5, N); i++)
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
            if (d_a != IntPtr.Zero) CudaFree(d_a);
            if (d_b != IntPtr.Zero) CudaFree(d_b);
            if (d_c != IntPtr.Zero) CudaFree(d_c);
            if (module != IntPtr.Zero) CuModuleUnload(module);
            if (program != IntPtr.Zero) NvrtcDestroyProgram(ref program);
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
    
    if (row < heightA && col < widthB) {
        float sum = 0.0f;
        for (int k = 0; k < widthA; ++k) {
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
    
    for (int m = 0; m < (widthA + 15) / 16; ++m) {
        // Load tiles into shared memory
        if (row < heightA && m * 16 + tx < widthA) {
            As[ty][tx] = A[row * widthA + m * 16 + tx];
        } else {
            As[ty][tx] = 0.0f;
        }
        
        if (col < widthB && m * 16 + ty < widthA) {
            Bs[ty][tx] = B[(m * 16 + ty) * widthB + col];
        } else {
            Bs[ty][tx] = 0.0f;
        }
        
        __syncthreads();
        
        // Compute partial sum
        for (int k = 0; k < 16; ++k) {
            sum += As[ty][k] * Bs[k][tx];
        }
        
        __syncthreads();
    }
    
    if (row < heightA && col < widthB) {
        C[row * widthB + col] = sum;
    }
}";

        IntPtr program = IntPtr.Zero;
        
        try
        {
            var result = NvrtcCreateProgram(ref program, complexKernelSource, "matrixMul.cu", 0, null, null);
            result.Should().Be(0, "Complex program creation should succeed");

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
                NvrtcGetProgramLogSize(program, ref logSize);
                if (logSize > 0)
                {
                    var log = new byte[logSize];
                    NvrtcGetProgramLog(program, log);
                    var logString = Encoding.ASCII.GetString(log).TrimEnd('\0');
                    _output.WriteLine($"Compilation log:\n{logString}");
                }
            }

            result.Should().Be(0, "Complex kernel compilation should succeed");
            _output.WriteLine($"Complex kernel compiled successfully in {sw.ElapsedMilliseconds} ms");

            // Get PTX and validate optimization
            long ptxSize = 0;
            NvrtcGetPTXSize(program, ref ptxSize);
            var ptx = new byte[ptxSize];
            NvrtcGetPTX(program, ptx);

            var ptxString = Encoding.ASCII.GetString(ptx).TrimEnd('\0');
            _output.WriteLine($"Generated optimized PTX ({ptxSize} bytes)");

            // Check for optimization indicators
            ptxString.Should().Contain("matrixMul", "PTX should contain matrix multiplication kernel");
            ptxString.Should().Contain("fastMatrixMul", "PTX should contain optimized matrix multiplication kernel");
            ptxString.Should().Contain("shared", "PTX should indicate shared memory usage");
        }
        finally
        {
            if (program != IntPtr.Zero)
            {
                NvrtcDestroyProgram(ref program);
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
    if (i < n) {
        data[i] = data[i] * data[i] + sqrtf(data[i]);
    }
}";

        for (int run = 0; run < compilationRuns; run++)
        {
            IntPtr program = IntPtr.Zero;
            
            try
            {
                var result = NvrtcCreateProgram(ref program, kernelSource, $"benchmark_{run}.cu", 0, null, null);
                result.Should().Be(0, "Program creation should succeed");

                var options = new[] { "--gpu-architecture=compute_89" };

                var sw = Stopwatch.StartNew();
                result = NvrtcCompileProgram(program, options.Length, options);
                sw.Stop();

                result.Should().Be(0, "Compilation should succeed");
                compilationTimes.Add(sw.ElapsedMilliseconds);
            }
            finally
            {
                if (program != IntPtr.Zero)
                {
                    NvrtcDestroyProgram(ref program);
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
        averageTime.Should().BeLessThan(1000, "Average compilation time should be under 1 second");
        maxTime.Should().BeLessThan(2000, "Maximum compilation time should be under 2 seconds");

        await Task.CompletedTask;
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

    #region Native Methods

    // CUDA Driver API
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

    [DllImport("nvcuda", EntryPoint = "cuModuleLoadData")]
    private static extern int CuModuleLoadData(ref IntPtr module, byte[] image);

    [DllImport("nvcuda", EntryPoint = "cuModuleUnload")]
    private static extern int CuModuleUnload(IntPtr module);

    [DllImport("nvcuda", EntryPoint = "cuModuleGetFunction")]
    private static extern int CuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, byte[] name);

    [DllImport("nvcuda", EntryPoint = "cuLaunchKernel")]
    private static extern int CuLaunchKernel(
        IntPtr f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        IntPtr hStream,
        IntPtr kernelParams,
        IntPtr extra);

    // NVRTC API
    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcVersion")]
    private static extern int NvrtcVersion(ref int major, ref int minor);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCreateProgram")]
    private static extern int NvrtcCreateProgram(
        ref IntPtr prog,
        [MarshalAs(UnmanagedType.LPStr)] string src,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int numHeaders,
        string[] headers,
        string[] includeNames);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcDestroyProgram")]
    private static extern int NvrtcDestroyProgram(ref IntPtr prog);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcCompileProgram")]
    private static extern int NvrtcCompileProgram(IntPtr prog, int numOptions, string[] options);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTXSize")]
    private static extern int NvrtcGetPTXSize(IntPtr prog, ref long ptxSizeRet);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetPTX")]
    private static extern int NvrtcGetPTX(IntPtr prog, [Out] byte[] ptx);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetProgramLogSize")]
    private static extern int NvrtcGetProgramLogSize(IntPtr prog, ref long logSizeRet);

    [DllImport("nvrtc64_120_0", EntryPoint = "nvrtcGetProgramLog")]
    private static extern int NvrtcGetProgramLog(IntPtr prog, [Out] byte[] log);

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