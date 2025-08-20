// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Tests.Utilities;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Hardware.Utilities;


/// <summary>
/// Utility classes and methods for CUDA testing
/// </summary>
public static class CudaTestUtilities
{
    /// <summary>
    /// Check if CUDA hardware is available for testing
    /// </summary>
    public static bool IsCudaAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if NVRTC is available for kernel compilation
    /// </summary>
    public static bool IsNvrtcAvailable() => CudaKernelCompiler.IsNvrtcAvailable();

    /// <summary>
    /// Get the number of available CUDA devices
    /// </summary>
    public static int GetDeviceCount()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success ? deviceCount : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Check if a specific device ID is valid
    /// </summary>
    public static bool IsValidDeviceId(int deviceId)
    {
        var deviceCount = GetDeviceCount();
        return deviceId >= 0 && deviceId < deviceCount;
    }

    /// <summary>
    /// Get device properties for a specific device
    /// </summary>
    public static CudaDeviceProperties? GetDeviceProperties(int deviceId)
    {
        if (!IsValidDeviceId(deviceId))
            return null;

        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            return result == CudaError.Success ? props : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if the device supports a minimum compute capability
    /// </summary>
    public static bool SupportsComputeCapability(int deviceId, int minMajor, int minMinor = 0)
    {
        var props = GetDeviceProperties(deviceId);
        if (props == null)
            return false;

        return props.Value.Major > minMajor ||
              (props.Value.Major == minMajor && props.Value.Minor >= minMinor);
    }

    /// <summary>
    /// Check if this is an RTX 2000 series GPU
    /// </summary>
    public static bool IsRTX2000Series(string deviceName)
    {
        var rtx2000Names = new[]
        {
        "RTX 2060", "RTX 2070", "RTX 2080", "RTX 2080 Ti",
        "GeForce RTX 2060", "GeForce RTX 2070", "GeForce RTX 2080", "GeForce RTX 2080 Ti",
        "Quadro RTX 4000", "Quadro RTX 5000", "Quadro RTX 6000", "Quadro RTX 8000"
    };

        return rtx2000Names.Any(name => deviceName.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if this is a Tensor Core capable GPU
    /// </summary>
    public static bool SupportsTensorCores(int deviceId)
        // Tensor Cores are available on compute capability 7.0+(Volta, Turing, Ampere, etc.)
        => SupportsComputeCapability(deviceId, 7, 0);

    /// <summary>
    /// Get memory usage information for a device
    /// </summary>
    public static (ulong free, ulong total) GetMemoryInfo()
    {
        try
        {
            _ = CudaRuntime.cudaMemGetInfo(out var free, out var total);
            return (free, total);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Create a test accelerator with proper logging
    /// </summary>
    public static CudaAccelerator CreateTestAccelerator(int deviceId = 0, ILogger<CudaAccelerator>? logger = null)
    {
        if (!IsValidDeviceId(deviceId))
        {
            throw new ArgumentException($"Invalid device ID: {deviceId}");
        }

        logger ??= CreateTestLogger<CudaAccelerator>();
        return new CudaAccelerator(deviceId, logger);
    }

    /// <summary>
    /// Create a test logger for the specified type
    /// </summary>
    public static ILogger<T> CreateTestLogger<T>()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        return loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Skip test if CUDA is not available
    /// </summary>
    public static void RequireCuda()
    {
        if (!IsCudaAvailable())
        {
            throw new SkipException("CUDA is not available");
        }
    }

    /// <summary>
    /// Skip test if NVRTC is not available
    /// </summary>
    public static void RequireNvrtc()
    {
        RequireCuda();

        if (!IsNvrtcAvailable())
        {
            throw new SkipException("NVRTC is not available");
        }
    }

    /// <summary>
    /// Skip test if specific compute capability is not available
    /// </summary>
    public static void RequireComputeCapability(int minMajor, int minMinor = 0, int deviceId = 0)
    {
        RequireCuda();

        if (!SupportsComputeCapability(deviceId, minMajor, minMinor))
        {
            throw new SkipException($"Compute capability {minMajor}.{minMinor}+ required");
        }
    }

    /// <summary>
    /// Skip test if RTX 2000 series is not available
    /// </summary>
    public static void RequireRTX2000Series(int deviceId = 0)
    {
        RequireCuda();

        var props = GetDeviceProperties(deviceId);
        if (props == null || !IsRTX2000Series(props.Value.Name))
        {
            throw new SkipException("RTX 2000 series GPU required");
        }
    }

    /// <summary>
    /// Get platform-specific information
    /// </summary>
    public static PlatformInfo GetPlatformInfo()
    {
        return new PlatformInfo
        {
            OperatingSystem = Environment.OSVersion.Platform.ToString(),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            FrameworkVersion = Environment.Version.ToString(),
            Is64Bit = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount
        };
    }

    /// <summary>
    /// Validate test environment setup
    /// </summary>
    public static TestEnvironmentValidation ValidateTestEnvironment()
    {
        var validation = new TestEnvironmentValidation();

        // Check CUDA availability
        validation.CudaAvailable = IsCudaAvailable();
        if (validation.CudaAvailable)
        {
            validation.DeviceCount = GetDeviceCount();

            // Get primary device info
            var props = GetDeviceProperties(0);
            if (props != null)
            {
                validation.PrimaryDeviceName = props.Value.Name;
                validation.ComputeCapability = $"{props.Value.Major}.{props.Value.Minor}";
                validation.DeviceMemoryGB = (double)props.Value.TotalGlobalMem / (1024 * 1024 * 1024);
            }
        }

        // Check NVRTC availability
        validation.NvrtcAvailable = IsNvrtcAvailable();
        if (validation.NvrtcAvailable)
        {
            var (major, minor) = CudaKernelCompiler.GetNvrtcVersion();
            validation.NvrtcVersion = $"{major}.{minor}";
        }

        // Platform information
        validation.PlatformInfo = GetPlatformInfo();

        return validation;
    }
}

/// <summary>
/// CUDA-specific test data generators
/// </summary>
public static class CudaTestData
{
    /// <summary>
    /// Generate test kernel definitions for various scenarios
    /// </summary>
    internal static class TestKernels
    {
        public static KernelDefinition SimpleVectorAdd() => new()
        {
            Name = "vector_add",
            Code = @"
__global__ void vector_add(float* a, float* b, float* c, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}",
            EntryPoint = "vector_add"
        };

        public static KernelDefinition SimpleScalarMultiply() => new()
        {
            Name = "scalar_multiply",
            Code = @"
__global__ void scalar_multiply(float* input, float* output, float scalar, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        output[idx] = input[idx] * scalar;
    }
}",
            EntryPoint = "scalar_multiply"
        };

        public static KernelDefinition SharedMemoryReduction() => new()
        {
            Name = "reduce_sum",
            Code = @"
__global__ void reduce_sum(float* input, float* output, int n)
{
    __shared__ float shared_data[256];
    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    shared_data[tid] =(idx < n) ? input[idx] : 0.0f;
    __syncthreads();
    
    for(int stride = blockDim.x / 2; stride > 0; stride >>= 1) {
        if(tid < stride) {
            shared_data[tid] += shared_data[tid + stride];
        }
        __syncthreads();
    }
    
    if(tid == 0) {
        output[blockIdx.x] = shared_data[0];
    }
}",
            EntryPoint = "reduce_sum"
        };

        public static KernelDefinition MatrixMultiply() => new()
        {
            Name = "matrix_multiply",
            Code = @"
__global__ void matrix_multiply(float* A, float* B, float* C, int n)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if(row < n && col < n) {
        float sum = 0.0f;
        for(int k = 0; k < n; k++) {
            sum += A[row * n + k] * B[k * n + col];
        }
        C[row * n + col] = sum;
    }
}",
            EntryPoint = "matrix_multiply"
        };

        public static KernelDefinition InvalidSyntax() => new()
        {
            Name = "invalid_kernel",
            Code = "invalid cuda syntax {{{ this will not compile",
            EntryPoint = "invalid_kernel"
        };

        public static KernelDefinition WithMathFunctions() => new()
        {
            Name = "math_kernel",
            Code = @"
__global__ void math_kernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        float val = input[idx];
        output[idx] = sinf(val) + cosf(val) + sqrtf(val);
    }
}",
            EntryPoint = "math_kernel"
        };

        public static KernelDefinition TensorCoreExample() => new()
        {
            Name = "tensor_core_gemm",
            Code = @"
#include <mma.h>
using namespace nvcuda;
using FluentAssertions;

__global__ void tensor_core_gemm(half* a, half* b, float* c, int n)
{
    wmma::fragment<wmma::matrix_a, 16, 16, 16, half, wmma::row_major> a_frag;
    wmma::fragment<wmma::matrix_b, 16, 16, 16, half, wmma::col_major> b_frag;
    wmma::fragment<wmma::accumulator, 16, 16, 16, float> c_frag;
    
    int warp_row =(blockIdx.y * blockDim.y + threadIdx.y) / 32 * 16;
    int warp_col =(blockIdx.x * blockDim.x + threadIdx.x) / 32 * 16;
    
    if(warp_row < n && warp_col < n) {
        wmma::fill_fragment(c_frag, 0.0f);
        
        for(int k = 0; k < n; k += 16) {
            wmma::load_matrix_sync(a_frag, a + warp_row * n + k, n);
            wmma::load_matrix_sync(b_frag, b + k * n + warp_col, n);
            wmma::mma_sync(c_frag, a_frag, b_frag, c_frag);
        }
        
        wmma::store_matrix_sync(c + warp_row * n + warp_col, c_frag, n, wmma::mem_row_major);
    }
}",
            EntryPoint = "tensor_core_gemm"
        };
    }

    /// <summary>
    /// Generate test data for various numeric types
    /// </summary>
    internal static class Arrays
    {
        public static float[] SmallFloat(int size = 1024) => TestDataGenerator.GenerateFloatArray(size);
        public static double[] SmallDouble(int size = 1024) => TestDataGenerator.GenerateDoubleArray(size);
        public static int[] SmallInt(int size = 1024) => TestDataGenerator.GenerateIntArray(size);

        public static float[] MediumFloat(int size = 64 * 1024) => TestDataGenerator.GenerateFloatArray(size);
        public static double[] MediumDouble(int size = 64 * 1024) => TestDataGenerator.GenerateDoubleArray(size);

        public static float[] LargeFloat(int size = 1024 * 1024) => TestDataGenerator.GenerateFloatArray(size);

        public static float[][] Matrix(int rows, int cols)
        {
            var matrix = new float[rows][];
            for (var i = 0; i < rows; i++)
            {
                matrix[i] = TestDataGenerator.GenerateFloatArray(cols);
            }
            return matrix;
        }

        public static byte[] RandomBytes(int size)
        {
            var data = new byte[size];
            TestDataGenerator.FillRandomBytes(data);
            return data;
        }
    }
}

/// <summary>
/// Platform information for test diagnostics
/// </summary>
public class PlatformInfo
{
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string FrameworkVersion { get; set; } = string.Empty;
    public bool Is64Bit { get; set; }
    public int ProcessorCount { get; set; }
}

/// <summary>
/// Test environment validation results
/// </summary>
public class TestEnvironmentValidation
{
    public bool CudaAvailable { get; set; }
    public int DeviceCount { get; set; }
    public string PrimaryDeviceName { get; set; } = string.Empty;
    public string ComputeCapability { get; set; } = string.Empty;
    public double DeviceMemoryGB { get; set; }

    public bool NvrtcAvailable { get; set; }
    public string NvrtcVersion { get; set; } = string.Empty;

    public PlatformInfo PlatformInfo { get; set; } = new();

    public bool IsFullyFunctional => CudaAvailable && NvrtcAvailable && DeviceCount > 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CUDA Test Environment Validation:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  CUDA Available: {CudaAvailable}");

        if (CudaAvailable)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Device Count: {DeviceCount}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Primary Device: {PrimaryDeviceName}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Compute Capability: {ComputeCapability}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Device Memory: {DeviceMemoryGB:F1} GB");
        }

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  NVRTC Available: {NvrtcAvailable}");
        if (NvrtcAvailable)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  NVRTC Version: {NvrtcVersion}");
        }

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Platform: {PlatformInfo.OperatingSystem} {PlatformInfo.Architecture}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  .NET: {PlatformInfo.FrameworkVersion}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Fully Functional: {IsFullyFunctional}");

        return sb.ToString();
    }
}

/// <summary>
/// Custom exception for skipping tests
/// </summary>
public sealed class SkipException : Exception
{
    public SkipException() : base() { }
    public SkipException(string message) : base(message) { }
    public SkipException(string message, Exception innerException) : base(message, innerException) { }
}
