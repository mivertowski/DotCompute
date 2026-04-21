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
    /// <remarks>
    /// Direct tensor-core GEMM from this manager is not wired to a kernel-launch path in v1.0.
    /// Use the standard kernel compilation pipeline (<c>IKernelCompiler</c>) with
    /// [Kernel]-attributed GEMM code, or call cuBLAS/CUTLASS directly via interop.
    /// <para>
    /// <see cref="TensorCoresAvailable"/> and <see cref="Capabilities"/> remain supported
    /// for capability detection.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown in v1.0.</exception>
    public Task<TensorCoreResult> MatrixMultiplyAsync(
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
        ValidateDataTypes(inputType, outputType);

        throw new NotSupportedException(
            "Direct tensor-core GEMM through CudaTensorCoreManagerProduction is not wired to a kernel-launch path in v1.0. " +
            "Use the standard kernel compilation pipeline with a [Kernel] GEMM, or invoke cuBLAS/CUTLASS via interop. " +
            "TensorCoresAvailable/Capabilities remain supported for capability detection.");
    }

    /// <summary>
    /// Performs convolution using tensor cores.
    /// </summary>
    /// <remarks>
    /// Convolution via this manager would lower to <see cref="MatrixMultiplyAsync"/> which is
    /// unsupported in v1.0. Use cuDNN or a [Kernel]-attributed convolution kernel instead.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown in v1.0.</exception>
    public Task<TensorCoreResult> ConvolutionAsync(
        IntPtr input, IntPtr filter, IntPtr output,
        ConvolutionParams parameters,
        DataType dataType,
        IntPtr stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(parameters);

        throw new NotSupportedException(
            "Convolution through CudaTensorCoreManagerProduction is not supported in v1.0. " +
            "Use cuDNN interop or a [Kernel]-attributed convolution through the standard compilation pipeline.");
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
            throw new NotSupportedException(
                $"Tensor Core data type {inputType} is not supported on this CUDA device. Supported types on this device: " +
                $"FP16={_capabilities.Fp16Supported}, BF16={_capabilities.Bf16Supported}, TF32={_capabilities.Tf32Supported}, " +
                $"FP8={_capabilities.Fp8Supported}, INT8={_capabilities.Int8Supported}, INT4={_capabilities.Int4Supported}, FP64={_capabilities.Fp64Supported}. " +
                "Choose a supported type or upgrade the GPU (Hopper CC 9.0 for FP8, Ampere CC 8.0 for BF16/TF32).");
        }
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

