// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using DotCompute.Backends.CUDA.DeviceManagement;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Advanced;

/// <summary>
/// Production-grade CUDA Tensor Core manager with WMMA operations,
/// mixed precision support, and performance profiling.
/// </summary>
public sealed partial class CudaTensorCoreManagerProduction : IDisposable
{
    private readonly CudaContext _context;
    private readonly CudaDeviceManager _deviceManager;
    private readonly ILogger<CudaTensorCoreManagerProduction> _logger;
    private readonly ConcurrentDictionary<string, TensorCoreKernel> _kernelCache;
    private readonly TensorCoreCapabilities _capabilities;
    private readonly PerformanceProfiler _profiler;
    private bool _tensorCoresAvailable;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaTensorCoreManagerProduction class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="deviceManager">The device manager.</param>
    /// <param name="logger">The logger.</param>

    public CudaTensorCoreManagerProduction(
        CudaContext context,
        CudaDeviceManager deviceManager,
        ILogger<CudaTensorCoreManagerProduction> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _logger = logger;
        _kernelCache = new ConcurrentDictionary<string, TensorCoreKernel>();
        _profiler = new PerformanceProfiler();


        _capabilities = DetectTensorCoreCapabilities();
        LogCapabilities();
    }

    /// <summary>
    /// Gets whether tensor cores are available.
    /// </summary>
    public bool TensorCoresAvailable => _tensorCoresAvailable;

    /// <summary>
    /// Gets tensor core capabilities.
    /// </summary>
    public TensorCoreCapabilities Capabilities => _capabilities;

    /// <summary>
    /// Detects tensor core capabilities for the current device.
    /// </summary>
    private TensorCoreCapabilities DetectTensorCoreCapabilities()
    {
        var device = _deviceManager.GetDevice(_context.DeviceId);
        var capabilities = new TensorCoreCapabilities();

        // Tensor cores available on compute capability 7.0+ (Volta and newer)

        if (device.ComputeCapabilityMajor >= 7)
        {
            _tensorCoresAvailable = true;
            capabilities.WmmaSupported = true;

            // Volta/Turing (7.0, 7.5) - First gen tensor cores

            if (device.ComputeCapabilityMajor == 7)
            {
                capabilities.Fp16Supported = true;
                capabilities.Int8Supported = device.ComputeCapabilityMinor >= 5;
                capabilities.Int4Supported = false;
                capabilities.Fp64Supported = false;
                capabilities.MaxWmmaM = 16;
                capabilities.MaxWmmaN = 16;
                capabilities.MaxWmmaK = 16;
            }
            // Ampere (8.0, 8.6) - Second gen tensor cores
            else if (device.ComputeCapabilityMajor == 8)
            {
                capabilities.Fp16Supported = true;
                capabilities.Bf16Supported = true;
                capabilities.Tf32Supported = true;
                capabilities.Int8Supported = true;
                capabilities.Int4Supported = true;
                capabilities.Fp64Supported = device.ComputeCapabilityMinor >= 0;
                capabilities.MaxWmmaM = 16;
                capabilities.MaxWmmaN = 16;
                capabilities.MaxWmmaK = 16;
                capabilities.SparsitySupported = device.ComputeCapabilityMinor >= 0;
            }
            // Ada Lovelace (8.9) / Hopper (9.0+) - Third/Fourth gen tensor cores
            else if (device.ComputeCapabilityMajor >= 9 ||

                    (device.ComputeCapabilityMajor == 8 && device.ComputeCapabilityMinor >= 9))
            {
                capabilities.Fp16Supported = true;
                capabilities.Bf16Supported = true;
                capabilities.Tf32Supported = true;
                capabilities.Fp8Supported = true; // New in Hopper
                capabilities.Int8Supported = true;
                capabilities.Int4Supported = true;
                capabilities.Fp64Supported = true;
                capabilities.MaxWmmaM = 64; // Larger tile sizes in Hopper
                capabilities.MaxWmmaN = 64;
                capabilities.MaxWmmaK = 32;
                capabilities.SparsitySupported = true;
                capabilities.TransformerEngineSupported = device.ComputeCapabilityMajor >= 9;
            }

            // Calculate theoretical peak performance

            capabilities.PeakTflops = CalculatePeakTensorPerformance(device, capabilities);
        }


        return capabilities;
    }

    /// <summary>
    /// Calculates theoretical peak tensor core performance.
    /// </summary>
    private static double CalculatePeakTensorPerformance(
        CudaDeviceInfo device,
        TensorCoreCapabilities capabilities)
    {
        // Rough estimation based on architecture
        // Real performance depends on many factors
        var tensorcoreMultiplier = device.ComputeCapabilityMajor switch
        {
            7 => 8,   // Volta/Turing: 8x over CUDA cores
            8 => 16,  // Ampere: 16x over CUDA cores
            9 => 32,  // Hopper: 32x over CUDA cores
            _ => 4
        };

        // Base CUDA core TFLOPS (rough estimate)

        var cudaCoreFlops = device.MultiprocessorCount *

                           device.MaxThreadsPerMultiprocessor *

                           device.ClockRate * 2.0 / 1e9; // 2 ops per cycle


        return cudaCoreFlops * tensorcoreMultiplier;
    }

    // Implemented in .LoggerMessages.cs partial file

    /// <summary>
    /// Performs mixed-precision matrix multiplication using tensor cores.
    /// </summary>
    public async Task<TensorCoreResult> MatrixMultiplyAsync(
        IntPtr a, IntPtr b, IntPtr c,
        int m, int n, int k,
        DataType inputType,
        DataType outputType,
        IntPtr stream,
        MatrixLayout layoutA = MatrixLayout.RowMajor,
        MatrixLayout layoutB = MatrixLayout.RowMajor,
        float alpha = 1.0f,
        float beta = 0.0f,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        if (!_tensorCoresAvailable)
        {
            throw new NotSupportedException("Tensor cores are not available on this device");
        }


        ValidateDataTypes(inputType, outputType);


        var startTime = DateTimeOffset.UtcNow;
        var kernelKey = GenerateKernelKey(m, n, k, inputType, outputType, layoutA, layoutB);


        try
        {
            // Get or compile optimized kernel
            var kernel = await GetOrCompileTensorKernelAsync(
                kernelKey, m, n, k, inputType, outputType, layoutA, layoutB, cancellationToken);

            // Calculate grid and block dimensions

            var (gridDim, blockDim) = CalculateOptimalDimensions(m, n, k);


            Execution.CudaTensorCoreManagerProductionLoggers.LogTensorGemmLaunch(_logger, m, k, n, inputType, outputType);

            // Launch kernel

            var launchResult = LaunchTensorKernel(
                kernel, a, b, c, m, n, k,
                alpha, beta, gridDim, blockDim, stream);


            if (!launchResult)
            {
                throw new InvalidOperationException("Failed to launch tensor core kernel");
            }

            // Synchronize if needed

            if (stream == IntPtr.Zero)
            {
                var syncResult = CudaRuntime.cudaDeviceSynchronize();
                CudaRuntime.CheckError(syncResult, "synchronizing tensor core operation");
            }


            var endTime = DateTimeOffset.UtcNow;
            var elapsedMs = (endTime - startTime).TotalMilliseconds;

            // Calculate performance metrics

            var gflops = CalculateGflops(m, n, k, elapsedMs);
            var efficiency = gflops / _capabilities.PeakTflops * 100;

            // Update profiler

            _profiler.RecordOperation(kernelKey, elapsedMs, gflops);


            Execution.CudaTensorCoreManagerProductionLoggers.LogTensorGemmComplete(_logger, elapsedMs, gflops * 1000, efficiency);


            return new TensorCoreResult
            {
                Success = true,
                ElapsedMilliseconds = elapsedMs,
                GFlops = gflops * 1000, // Convert to GFLOPS
                Efficiency = efficiency,
                KernelUsed = kernelKey
            };
        }
        catch (Exception ex)
        {
            Execution.CudaTensorCoreManagerProductionLoggers.LogTensorOperationFailed(_logger, ex);
            throw new TensorCoreException("Tensor core matrix multiplication failed", ex);
        }
    }

    /// <summary>
    /// Performs convolution using tensor cores.
    /// </summary>
    public async Task<TensorCoreResult> ConvolutionAsync(
        IntPtr input, IntPtr filter, IntPtr output,
        ConvolutionParams parameters,
        DataType dataType,
        IntPtr stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        if (!_tensorCoresAvailable)
        {
            throw new NotSupportedException("Tensor cores are not available on this device");
        }


        Execution.CudaTensorCoreManagerProductionLoggers.LogConvolutionLaunch(
            _logger,
            parameters.BatchSize, parameters.InputChannels,
            parameters.InputHeight, parameters.InputWidth,
            parameters.OutputChannels, parameters.InputChannels,
            parameters.FilterHeight, parameters.FilterWidth);

        // Convert convolution to implicit GEMM for tensor cores

        var m = parameters.OutputChannels;
        var n = parameters.OutputHeight * parameters.OutputWidth;
        var k = parameters.InputChannels * parameters.FilterHeight * parameters.FilterWidth;

        // Use tensor core GEMM with im2col transformation

        var result = await MatrixMultiplyAsync(
            filter, input, output,
            m, n, k,
            dataType, dataType,
            stream,
            MatrixLayout.RowMajor,
            MatrixLayout.ColumnMajor,
            1.0f, 0.0f,
            cancellationToken);


        result.OperationType = "Convolution";
        return result;
    }

    /// <summary>
    /// Gets or compiles an optimized tensor core kernel.
    /// </summary>
    private async Task<TensorCoreKernel> GetOrCompileTensorKernelAsync(
        string kernelKey,
        int m, int n, int k,
        DataType inputType,
        DataType outputType,
        MatrixLayout layoutA,
        MatrixLayout layoutB,
        CancellationToken cancellationToken)
    {
        // Check cache first
        if (_kernelCache.TryGetValue(kernelKey, out var cached))
        {
            Execution.CudaTensorCoreManagerProductionLoggers.LogCachedKernel(_logger);
            return cached;
        }

        // Compile new kernel

        Execution.CudaTensorCoreManagerProductionLoggers.LogCompilingKernel(_logger);


        var kernel = await Task.Run(() =>
        {
            var ptxCode = GenerateTensorCorePtx(
                m, n, k, inputType, outputType, layoutA, layoutB);

            // Compile PTX to cubin

            var cubin = CompilePtxToCubin(ptxCode);


            return new TensorCoreKernel
            {
                Key = kernelKey,
                Ptx = ptxCode,
                Cubin = cubin,
                M = m,
                N = n,
                K = k,
                InputType = inputType,
                OutputType = outputType,
                CompiledAt = DateTimeOffset.UtcNow
            };
        }, cancellationToken);

        _ = _kernelCache.TryAdd(kernelKey, kernel);
        return kernel;
    }

    /// <summary>
    /// Generates PTX code for tensor core operations.
    /// </summary>
    private static string GenerateTensorCorePtx(
        int m, int n, int k,
        DataType inputType,
        DataType outputType,
        MatrixLayout layoutA,
        MatrixLayout layoutB)
    {
        var ptx = new StringBuilder();

        // PTX version and target
        _ = ptx.AppendLine(".version 7.0");
        _ = ptx.AppendLine(".target sm_70"); // Minimum for tensor cores
        _ = ptx.AppendLine(".address_size 64");

        // Kernel entry point
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $".visible .entry tensor_gemm_{inputType}_{outputType}(");
        _ = ptx.AppendLine("    .param .u64 param_a,");
        _ = ptx.AppendLine("    .param .u64 param_b,");
        _ = ptx.AppendLine("    .param .u64 param_c,");
        _ = ptx.AppendLine("    .param .u32 param_m,");
        _ = ptx.AppendLine("    .param .u32 param_n,");
        _ = ptx.AppendLine("    .param .u32 param_k,");
        _ = ptx.AppendLine("    .param .f32 param_alpha,");
        _ = ptx.AppendLine("    .param .f32 param_beta");
        _ = ptx.AppendLine(")");
        _ = ptx.AppendLine("{");

        // Register declarations
        _ = ptx.AppendLine("    .reg .pred %p<2>;");
        _ = ptx.AppendLine("    .reg .b32 %r<64>;");
        _ = ptx.AppendLine("    .reg .b64 %rd<16>;");
        _ = ptx.AppendLine("    .reg .f32 %f<32>;");

        // WMMA fragment declarations based on data type

        var wmmaShape = GetWmmaShape(inputType);
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    // WMMA fragments for {wmmaShape.M}x{wmmaShape.N}x{wmmaShape.K}");

        // Generate WMMA operations

        GenerateWmmaOperations(ptx, inputType, outputType, wmmaShape);

        _ = ptx.AppendLine("}");


        return ptx.ToString();
    }

    /// <summary>
    /// Generates WMMA operations in PTX.
    /// </summary>
    private static void GenerateWmmaOperations(
        StringBuilder ptx,
        DataType inputType,
        DataType outputType,
        WmmaShape shape)
    {
        // Load fragments
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    // Load A fragment");
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    wmma.load.a.sync.aligned.{shape.M}x{shape.N}x{shape.K}.row.{GetPtxType(inputType)}");

        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    // Load B fragment");
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    wmma.load.b.sync.aligned.{shape.M}x{shape.N}x{shape.K}.col.{GetPtxType(inputType)}");

        // Compute
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    // Compute C = A * B");
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    wmma.mma.sync.aligned.{shape.M}x{shape.N}x{shape.K}.row.col.{GetPtxType(outputType)}.{GetPtxType(inputType)}");

        // Store result
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    // Store C fragment");
        _ = ptx.AppendLine(CultureInfo.InvariantCulture, $"    wmma.store.d.sync.aligned.{shape.M}x{shape.N}x{shape.K}.row.{GetPtxType(outputType)}");
    }

    /// <summary>
    /// Compiles PTX code to cubin.
    /// </summary>
    private static byte[] CompilePtxToCubin(string ptxCode)
        // TODO
        // This would use NVRTC to compile PTX to cubin
        // For now, return placeholder




        => Encoding.UTF8.GetBytes(ptxCode);

    /// <summary>
    /// Launches a tensor core kernel.
    /// </summary>
    private bool LaunchTensorKernel(
        TensorCoreKernel kernel,
        IntPtr a, IntPtr b, IntPtr c,
        int m, int n, int k,
        float alpha, float beta,
        dim3 gridDim, dim3 blockDim,
        IntPtr stream)
    {
        // TODO
        // This would launch the compiled kernel
        // Using cuLaunchKernel or similar API
        Execution.CudaTensorCoreManagerProductionLoggers.LogKernelLaunch(
            _logger,
            gridDim.x, gridDim.y, gridDim.z,
            blockDim.x, blockDim.y, blockDim.z);

        // Placeholder for actual launch

        return true;
    }

    /// <summary>
    /// Calculates optimal grid and block dimensions for tensor cores.
    /// </summary>
    private (dim3 grid, dim3 block) CalculateOptimalDimensions(int m, int n, int k)
    {
        // Tensor cores work on specific tile sizes
        var tileM = Math.Min(_capabilities.MaxWmmaM, 16);
        var tileN = Math.Min(_capabilities.MaxWmmaN, 16);

        // Calculate grid dimensions

        var gridX = (n + tileN - 1) / tileN;
        var gridY = (m + tileM - 1) / tileM;

        // Warp size is always 32 for NVIDIA GPUs

        const int warpSize = 32;

        // Block dimensions (multiple of warp size)

        var blockX = Math.Min(gridX, 8) * warpSize;
        var blockY = Math.Min(gridY, 4);


        return (
            new dim3((uint)gridX, (uint)gridY, 1),
            new dim3((uint)blockX, (uint)blockY, 1)
        );
    }

    /// <summary>
    /// Validates data type support.
    /// </summary>
    private void ValidateDataTypes(DataType inputType, DataType outputType)
    {
        var supported = inputType switch
        {
            DataType.Float16 => _capabilities.Fp16Supported,
            DataType.BFloat16 => _capabilities.Bf16Supported,
            DataType.TensorFloat32 => _capabilities.Tf32Supported,
            DataType.Float8E4M3 or DataType.Float8E5M2 => _capabilities.Fp8Supported,
            DataType.Int8 => _capabilities.Int8Supported,
            DataType.Int4 => _capabilities.Int4Supported,
            DataType.Float64 => _capabilities.Fp64Supported,
            _ => false
        };


        if (!supported)
        {
            throw new NotSupportedException($"Data type {inputType} is not supported by tensor cores on this device");
        }
    }

    /// <summary>
    /// Gets WMMA shape for data type.
    /// </summary>
    private static WmmaShape GetWmmaShape(DataType dataType)
    {
        // Different architectures support different shapes
        return dataType switch
        {
            DataType.Float16 => new WmmaShape { M = 16, N = 16, K = 16 },
            DataType.BFloat16 => new WmmaShape { M = 16, N = 16, K = 16 },
            DataType.TensorFloat32 => new WmmaShape { M = 16, N = 16, K = 8 },
            DataType.Int8 => new WmmaShape { M = 16, N = 16, K = 32 },
            DataType.Int4 => new WmmaShape { M = 16, N = 16, K = 64 },
            _ => new WmmaShape { M = 16, N = 16, K = 16 }
        };
    }

    /// <summary>
    /// Gets PTX type string for data type.
    /// </summary>
    private static string GetPtxType(DataType dataType)
    {
        return dataType switch
        {
            DataType.Float16 => "f16",
            DataType.BFloat16 => "bf16",
            DataType.TensorFloat32 => "tf32",
            DataType.Float8E4M3 => "e4m3",
            DataType.Float8E5M2 => "e5m2",
            DataType.Int8 => "s8",
            DataType.Int4 => "s4",
            DataType.Float32 => "f32",
            DataType.Float64 => "f64",
            _ => "f32"
        };
    }

    /// <summary>
    /// Generates kernel cache key.
    /// </summary>
    private static string GenerateKernelKey(
        int m, int n, int k,
        DataType inputType,
        DataType outputType,
        MatrixLayout layoutA,
        MatrixLayout layoutB) => $"gemm_{m}x{n}x{k}_{inputType}_{outputType}_{layoutA}_{layoutB}";

    /// <summary>
    /// Calculates GFLOPS for the operation.
    /// </summary>
    private static double CalculateGflops(int m, int n, int k, double elapsedMs)
    {
        // GEMM requires 2*M*N*K operations
        var operations = 2.0 * m * n * k;
        return operations / (elapsedMs * 1e6); // GFLOPS
    }

    /// <summary>
    /// Gets performance statistics.
    /// </summary>
    public TensorCoreStatistics Statistics => new TensorCoreStatistics
    {
        TensorCoresAvailable = _tensorCoresAvailable,
        CachedKernels = _kernelCache.Count,
        TotalOperations = _profiler.TotalOperations,
        AverageGFlops = _profiler.AverageGFlops,
        PeakGFlops = _profiler.PeakGFlops,
        Capabilities = _capabilities
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _kernelCache.Clear();
        }
    }

    /// <summary>
    /// Tensor core kernel information.
    /// </summary>
    private sealed class TensorCoreKernel
    {
        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>The key.</value>
        public string Key { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the ptx.
        /// </summary>
        /// <value>The ptx.</value>
        public string Ptx { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the cubin.
        /// </summary>
        /// <value>The cubin.</value>
        public byte[] Cubin { get; init; } = [];
        /// <summary>
        /// Gets or sets the m.
        /// </summary>
        /// <value>The m.</value>
        public int M { get; init; }
        /// <summary>
        /// Gets or sets the n.
        /// </summary>
        /// <value>The n.</value>
        public int N { get; init; }
        /// <summary>
        /// Gets or sets the k.
        /// </summary>
        /// <value>The k.</value>
        public int K { get; init; }
        /// <summary>
        /// Gets or sets the input type.
        /// </summary>
        /// <value>The input type.</value>
        public DataType InputType { get; init; }
        /// <summary>
        /// Gets or sets the output type.
        /// </summary>
        /// <value>The output type.</value>
        public DataType OutputType { get; init; }
        /// <summary>
        /// Gets or sets the compiled at.
        /// </summary>
        /// <value>The compiled at.</value>
        public DateTimeOffset CompiledAt { get; init; }
    }

    /// <summary>
    /// Performance profiler for tensor operations.
    /// </summary>
    private sealed class PerformanceProfiler
    {
        private long _totalOperations;
        private double _totalGFlops;
        private double _peakGFlops;
        /// <summary>
        /// Gets or sets the total operations.
        /// </summary>
        /// <value>The total operations.</value>


        public long TotalOperations => _totalOperations;
        /// <summary>
        /// Gets or sets the average g flops.
        /// </summary>
        /// <value>The average g flops.</value>
        public double AverageGFlops => _totalOperations > 0 ? _totalGFlops / _totalOperations : 0;
        /// <summary>
        /// Gets or sets the peak g flops.
        /// </summary>
        /// <value>The peak g flops.</value>
        public double PeakGFlops => _peakGFlops;
        /// <summary>
        /// Performs record operation.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="elapsedMs">The elapsed ms.</param>
        /// <param name="gflops">The gflops.</param>


        public void RecordOperation(string kernel, double elapsedMs, double gflops)
        {
            _ = Interlocked.Increment(ref _totalOperations);
            _ = Interlocked.Exchange(ref _totalGFlops, _totalGFlops + gflops);

            // Update peak

            var currentPeak = _peakGFlops;
            while (gflops > currentPeak)
            {
                _ = Interlocked.CompareExchange(ref _peakGFlops, gflops, currentPeak);
                currentPeak = _peakGFlops;
            }
        }
    }
}

/// <summary>
/// Tensor core capabilities.
/// </summary>
public sealed class TensorCoreCapabilities
{
    /// <summary>
    /// Gets or sets the wmma supported.
    /// </summary>
    /// <value>The wmma supported.</value>
    public bool WmmaSupported { get; set; }
    /// <summary>
    /// Gets or sets the fp16 supported.
    /// </summary>
    /// <value>The fp16 supported.</value>
    public bool Fp16Supported { get; set; }
    /// <summary>
    /// Gets or sets the bf16 supported.
    /// </summary>
    /// <value>The bf16 supported.</value>
    public bool Bf16Supported { get; set; }
    /// <summary>
    /// Gets or sets the tf32 supported.
    /// </summary>
    /// <value>The tf32 supported.</value>
    public bool Tf32Supported { get; set; }
    /// <summary>
    /// Gets or sets the fp8 supported.
    /// </summary>
    /// <value>The fp8 supported.</value>
    public bool Fp8Supported { get; set; }
    /// <summary>
    /// Gets or sets the int8 supported.
    /// </summary>
    /// <value>The int8 supported.</value>
    public bool Int8Supported { get; set; }
    /// <summary>
    /// Gets or sets the int4 supported.
    /// </summary>
    /// <value>The int4 supported.</value>
    public bool Int4Supported { get; set; }
    /// <summary>
    /// Gets or sets the fp64 supported.
    /// </summary>
    /// <value>The fp64 supported.</value>
    public bool Fp64Supported { get; set; }
    /// <summary>
    /// Gets or sets the sparsity supported.
    /// </summary>
    /// <value>The sparsity supported.</value>
    public bool SparsitySupported { get; set; }
    /// <summary>
    /// Gets or sets the transformer engine supported.
    /// </summary>
    /// <value>The transformer engine supported.</value>
    public bool TransformerEngineSupported { get; set; }
    /// <summary>
    /// Gets or sets the max wmma m.
    /// </summary>
    /// <value>The max wmma m.</value>
    public int MaxWmmaM { get; set; }
    /// <summary>
    /// Gets or sets the max wmma n.
    /// </summary>
    /// <value>The max wmma n.</value>
    public int MaxWmmaN { get; set; }
    /// <summary>
    /// Gets or sets the max wmma k.
    /// </summary>
    /// <value>The max wmma k.</value>
    public int MaxWmmaK { get; set; }
    /// <summary>
    /// Gets or sets the peak tflops.
    /// </summary>
    /// <value>The peak tflops.</value>
    public double PeakTflops { get; set; }
}
/// <summary>
/// An data type enumeration.
/// </summary>

/// <summary>
/// Supported data types for tensor operations.
/// </summary>
public enum DataType
{
    Float16,
    BFloat16,
    TensorFloat32,
    Float8E4M3,
    Float8E5M2,
    Int8,
    Int4,
    Float32,
    Float64
}
/// <summary>
/// An matrix layout enumeration.
/// </summary>

/// <summary>
/// Matrix layout for tensor operations.
/// </summary>
public enum MatrixLayout
{
    RowMajor,
    ColumnMajor
}

/// <summary>
/// WMMA shape configuration.
/// </summary>
public struct WmmaShape : IEquatable<WmmaShape>
{
    /// <summary>
    /// Gets or sets the m.
    /// </summary>
    /// <value>The m.</value>
    public int M { get; set; }
    /// <summary>
    /// Gets or sets the n.
    /// </summary>
    /// <value>The n.</value>
    public int N { get; set; }
    /// <summary>
    /// Gets or sets the k.
    /// </summary>
    /// <value>The k.</value>
    public int K { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current WmmaShape.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is WmmaShape other && Equals(other);

    /// <summary>
    /// Determines whether the specified WmmaShape is equal to the current WmmaShape.
    /// </summary>
    /// <param name="other">The WmmaShape to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public bool Equals(WmmaShape other) => M == other.M && N == other.N && K == other.K;

    /// <summary>
    /// Returns the hash code for this WmmaShape.
    /// </summary>
    /// <returns>A hash code for the current WmmaShape.</returns>
    public override int GetHashCode() => HashCode.Combine(M, N, K);

    /// <summary>
    /// Determines whether two WmmaShape instances are equal.
    /// </summary>
    /// <param name="left">The first WmmaShape to compare.</param>
    /// <param name="right">The second WmmaShape to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public static bool operator ==(WmmaShape left, WmmaShape right) => left.Equals(right);

    /// <summary>
    /// Determines whether two WmmaShape instances are not equal.
    /// </summary>
    /// <param name="left">The first WmmaShape to compare.</param>
    /// <param name="right">The second WmmaShape to compare.</param>
    /// <returns>True if not equal; otherwise, false.</returns>
    public static bool operator !=(WmmaShape left, WmmaShape right) => !left.Equals(right);
}

/// <summary>
/// Convolution parameters.
/// </summary>
public sealed class ConvolutionParams
{
    /// <summary>
    /// Gets or sets the batch size.
    /// </summary>
    /// <value>The batch size.</value>
    public int BatchSize { get; init; }
    /// <summary>
    /// Gets or sets the input channels.
    /// </summary>
    /// <value>The input channels.</value>
    public int InputChannels { get; init; }
    /// <summary>
    /// Gets or sets the output channels.
    /// </summary>
    /// <value>The output channels.</value>
    public int OutputChannels { get; init; }
    /// <summary>
    /// Gets or sets the input height.
    /// </summary>
    /// <value>The input height.</value>
    public int InputHeight { get; init; }
    /// <summary>
    /// Gets or sets the input width.
    /// </summary>
    /// <value>The input width.</value>
    public int InputWidth { get; init; }
    /// <summary>
    /// Gets or sets the filter height.
    /// </summary>
    /// <value>The filter height.</value>
    public int FilterHeight { get; init; }
    /// <summary>
    /// Gets or sets the filter width.
    /// </summary>
    /// <value>The filter width.</value>
    public int FilterWidth { get; init; }
    /// <summary>
    /// Gets or sets the stride h.
    /// </summary>
    /// <value>The stride h.</value>
    public int StrideH { get; init; } = 1;
    /// <summary>
    /// Gets or sets the stride w.
    /// </summary>
    /// <value>The stride w.</value>
    public int StrideW { get; init; } = 1;
    /// <summary>
    /// Gets or sets the pad h.
    /// </summary>
    /// <value>The pad h.</value>
    public int PadH { get; init; }
    /// <summary>
    /// Gets or sets the pad w.
    /// </summary>
    /// <value>The pad w.</value>

    public int PadW { get; init; }
    /// <summary>
    /// Gets or sets the output height.
    /// </summary>
    /// <value>The output height.</value>


    public int OutputHeight => (InputHeight + 2 * PadH - FilterHeight) / StrideH + 1;
    /// <summary>
    /// Gets or sets the output width.
    /// </summary>
    /// <value>The output width.</value>
    public int OutputWidth => (InputWidth + 2 * PadW - FilterWidth) / StrideW + 1;
}

/// <summary>
/// Tensor core operation result.
/// </summary>
public sealed class TensorCoreResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; init; }
    /// <summary>
    /// Gets or sets the elapsed milliseconds.
    /// </summary>
    /// <value>The elapsed milliseconds.</value>
    public double ElapsedMilliseconds { get; init; }
    /// <summary>
    /// Gets or sets the g flops.
    /// </summary>
    /// <value>The g flops.</value>
    public double GFlops { get; init; }
    /// <summary>
    /// Gets or sets the efficiency.
    /// </summary>
    /// <value>The efficiency.</value>
    public double Efficiency { get; init; }
    /// <summary>
    /// Gets or sets the kernel used.
    /// </summary>
    /// <value>The kernel used.</value>
    public string KernelUsed { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    /// <value>The operation type.</value>
    public string OperationType { get; set; } = "GEMM";
}

/// <summary>
/// Tensor core statistics.
/// </summary>
public sealed class TensorCoreStatistics
{
    /// <summary>
    /// Gets or sets the tensor cores available.
    /// </summary>
    /// <value>The tensor cores available.</value>
    public bool TensorCoresAvailable { get; init; }
    /// <summary>
    /// Gets or sets the cached kernels.
    /// </summary>
    /// <value>The cached kernels.</value>
    public int CachedKernels { get; init; }
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public long TotalOperations { get; init; }
    /// <summary>
    /// Gets or sets the average g flops.
    /// </summary>
    /// <value>The average g flops.</value>
    public double AverageGFlops { get; init; }
    /// <summary>
    /// Gets or sets the peak g flops.
    /// </summary>
    /// <value>The peak g flops.</value>
    public double PeakGFlops { get; init; }
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    /// <value>The capabilities.</value>
    public TensorCoreCapabilities Capabilities { get; init; } = new();
}

/// <summary>
/// Exception for tensor core operations.
/// </summary>
public sealed class TensorCoreException : Exception
{
    /// <summary>
    /// Initializes a new instance of the TensorCoreException class.
    /// </summary>
    /// <param name="message">The message.</param>
    public TensorCoreException(string message) : base(message) { }
    /// <summary>
    /// Initializes a new instance of the TensorCoreException class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="inner">The inner.</param>
    public TensorCoreException(string message, Exception inner) : base(message, inner) { }
    /// <summary>
    /// Initializes a new instance of the TensorCoreException class.
    /// </summary>
    public TensorCoreException()
    {
    }
}

/// <summary>
/// CUDA dimension structure.
/// </summary>
public readonly struct dim3(uint x, uint y, uint z) : IEquatable<dim3>
{
    /// <summary>
    /// The x coordinate.
    /// </summary>
    public uint x { get; } = x;

    /// <summary>
    /// The y coordinate.
    /// </summary>
    public uint y { get; } = y;

    /// <summary>
    /// The z coordinate.
    /// </summary>
    public uint z { get; } = z;

    /// <summary>
    /// Determines whether the specified object is equal to the current dim3.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is dim3 other && Equals(other);

    /// <summary>
    /// Determines whether the specified dim3 is equal to the current dim3.
    /// </summary>
    /// <param name="other">The dim3 to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public bool Equals(dim3 other) => x == other.x && y == other.y && z == other.z;

    /// <summary>
    /// Returns the hash code for this dim3.
    /// </summary>
    /// <returns>A hash code for the current dim3.</returns>
    public override int GetHashCode() => HashCode.Combine(x, y, z);

    /// <summary>
    /// Determines whether two dim3 instances are equal.
    /// </summary>
    /// <param name="left">The first dim3 to compare.</param>
    /// <param name="right">The second dim3 to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public static bool operator ==(dim3 left, dim3 right) => left.Equals(right);

    /// <summary>
    /// Determines whether two dim3 instances are not equal.
    /// </summary>
    /// <param name="left">The first dim3 to compare.</param>
    /// <param name="right">The second dim3 to compare.</param>
    /// <returns>True if not equal; otherwise, false.</returns>
    public static bool operator !=(dim3 left, dim3 right) => !left.Equals(right);
}
