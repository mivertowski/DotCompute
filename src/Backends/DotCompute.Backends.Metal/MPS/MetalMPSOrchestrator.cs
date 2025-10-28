// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.MPS;

/// <summary>
/// Intelligent orchestrator that automatically selects between MPS and custom kernels
/// based on operation type, data size, and device capabilities.
/// </summary>
public sealed class MetalMPSOrchestrator : IDisposable
{
    private readonly MetalPerformanceShadersBackend _mpsBackend;
    private readonly ILogger<MetalMPSOrchestrator> _logger;
    private readonly MPSCapabilities _capabilities;
    private readonly PerformanceMetrics _metrics;
    private bool _disposed;

    public MetalMPSOrchestrator(
        IntPtr device,
        ILogger<MetalMPSOrchestrator> logger)
    {
        var mpsLogger = Microsoft.Extensions.Logging.LoggerFactory
            .Create(builder => builder.SetMinimumLevel(LogLevel.Information))
            .CreateLogger<MetalPerformanceShadersBackend>();

        _mpsBackend = new MetalPerformanceShadersBackend(device, mpsLogger);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capabilities = _mpsBackend.Capabilities;
        _metrics = new PerformanceMetrics();

        _logger.LogInformation("MPS Orchestrator initialized - Auto-selecting optimal backend for each operation");
    }

    /// <summary>
    /// Performs matrix multiplication with automatic backend selection.
    /// </summary>
    public void MatrixMultiply(
        ReadOnlySpan<float> a, int rowsA, int colsA,
        ReadOnlySpan<float> b, int rowsB, int colsB,
        Span<float> c, int rowsC, int colsC,
        float alpha = 1.0f, float beta = 0.0f,
        bool transposeA = false, bool transposeB = false)
    {
        ThrowIfDisposed();

        var dataSize = rowsA * colsA + rowsB * colsB + rowsC * colsC;
        var useMPS = MetalPerformanceShadersBackend.ShouldUseMPS(
            MPSOperationType.MatrixMultiply,
            dataSize,
            _capabilities);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (useMPS)
        {
            _logger.LogTrace("Using MPS for matrix multiply: [{RowsA}x{ColsA}] * [{RowsB}x{ColsB}]",
                rowsA, colsA, rowsB, colsB);

            _mpsBackend.MatrixMultiply(a, rowsA, colsA, b, rowsB, colsB, c, rowsC, colsC,
                alpha, beta, transposeA, transposeB);

            _metrics.RecordMPSOperation(MPSOperationType.MatrixMultiply, stopwatch.Elapsed);
        }
        else
        {
            _logger.LogTrace("Using CPU fallback for matrix multiply (data size too small): [{RowsA}x{ColsA}] * [{RowsB}x{ColsB}]",
                rowsA, colsA, rowsB, colsB);

            // Fallback to CPU implementation
            MatrixMultiplyCPU(a, rowsA, colsA, b, rowsB, colsB, c, rowsC, colsC,
                alpha, beta, transposeA, transposeB);

            _metrics.RecordCPUFallback(MPSOperationType.MatrixMultiply, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Performs convolution with automatic backend selection.
    /// </summary>
    public void Convolution2D(
        ReadOnlySpan<float> input, int inputHeight, int inputWidth, int inputChannels,
        ReadOnlySpan<float> kernel, int kernelHeight, int kernelWidth, int outputChannels,
        Span<float> output, int outputHeight, int outputWidth,
        int strideY = 1, int strideX = 1,
        int paddingY = 0, int paddingX = 0)
    {
        ThrowIfDisposed();

        var dataSize = input.Length + kernel.Length + output.Length;
        var useMPS = MetalPerformanceShadersBackend.ShouldUseMPS(
            MPSOperationType.Convolution,
            dataSize,
            _capabilities);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (useMPS)
        {
            _logger.LogTrace("Using MPS for convolution: [{IH}x{IW}x{IC}] * [{KH}x{KW}]",
                inputHeight, inputWidth, inputChannels, kernelHeight, kernelWidth);

            _mpsBackend.Convolution2D(input, inputHeight, inputWidth, inputChannels,
                kernel, kernelHeight, kernelWidth, outputChannels,
                output, outputHeight, outputWidth,
                strideY, strideX, paddingY, paddingX);

            _metrics.RecordMPSOperation(MPSOperationType.Convolution, stopwatch.Elapsed);
        }
        else
        {
            _logger.LogTrace("Using CPU fallback for convolution (data size too small)");

            // Fallback to CPU implementation
            Convolution2DCPU(input, inputHeight, inputWidth, inputChannels,
                kernel, kernelHeight, kernelWidth, outputChannels,
                output, outputHeight, outputWidth,
                strideY, strideX, paddingY, paddingX);

            _metrics.RecordCPUFallback(MPSOperationType.Convolution, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Applies ReLU activation with automatic backend selection.
    /// </summary>
    public void ReLU(ReadOnlySpan<float> input, Span<float> output)
    {
        ThrowIfDisposed();

        var useMPS = MetalPerformanceShadersBackend.ShouldUseMPS(
            MPSOperationType.Activation,
            input.Length,
            _capabilities);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (useMPS)
        {
            _logger.LogTrace("Using MPS for ReLU: {Count} elements", input.Length);
            _mpsBackend.ReLU(input, output);
            _metrics.RecordMPSOperation(MPSOperationType.Activation, stopwatch.Elapsed);
        }
        else
        {
            _logger.LogTrace("Using CPU fallback for ReLU: {Count} elements", input.Length);
            ReLUCPU(input, output);
            _metrics.RecordCPUFallback(MPSOperationType.Activation, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets performance metrics for MPS vs CPU operations.
    /// </summary>
    public PerformanceMetrics GetMetrics() => _metrics;

    #region CPU Fallback Implementations

    private static void MatrixMultiplyCPU(
        ReadOnlySpan<float> a, int rowsA, int colsA,
        ReadOnlySpan<float> b, int rowsB, int colsB,
        Span<float> c, int rowsC, int colsC,
        float alpha, float beta,
        bool transposeA, bool transposeB)
    {
        // Simple CPU matrix multiply (can be optimized with SIMD)
        for (var i = 0; i < rowsC; i++)
        {
            for (var j = 0; j < colsC; j++)
            {
                float sum = 0;
                for (var k = 0; k < (transposeA ? rowsA : colsA); k++)
                {
                    var aIdx = transposeA ? (k * colsA + i) : (i * colsA + k);
                    var bIdx = transposeB ? (j * colsB + k) : (k * colsB + j);
                    sum += a[aIdx] * b[bIdx];
                }
                var cIdx = i * colsC + j;
                c[cIdx] = alpha * sum + beta * c[cIdx];
            }
        }
    }

    private static void Convolution2DCPU(
        ReadOnlySpan<float> input, int inputHeight, int inputWidth, int inputChannels,
        ReadOnlySpan<float> kernel, int kernelHeight, int kernelWidth, int outputChannels,
        Span<float> output, int outputHeight, int outputWidth,
        int strideY, int strideX,
        int paddingY, int paddingX)
    {
        // Simple CPU convolution (can be optimized with SIMD)
        for (var oc = 0; oc < outputChannels; oc++)
        {
            for (var oh = 0; oh < outputHeight; oh++)
            {
                for (var ow = 0; ow < outputWidth; ow++)
                {
                    float sum = 0;
                    for (var ic = 0; ic < inputChannels; ic++)
                    {
                        for (var kh = 0; kh < kernelHeight; kh++)
                        {
                            for (var kw = 0; kw < kernelWidth; kw++)
                            {
                                var ih = oh * strideY + kh - paddingY;
                                var iw = ow * strideX + kw - paddingX;

                                if (ih >= 0 && ih < inputHeight && iw >= 0 && iw < inputWidth)
                                {
                                    var inputIdx = (ic * inputHeight + ih) * inputWidth + iw;
                                    var kernelIdx = ((oc * inputChannels + ic) * kernelHeight + kh) * kernelWidth + kw;
                                    sum += input[inputIdx] * kernel[kernelIdx];
                                }
                            }
                        }
                    }
                    var outputIdx = (oc * outputHeight + oh) * outputWidth + ow;
                    output[outputIdx] = sum;
                }
            }
        }
    }

    private static void ReLUCPU(ReadOnlySpan<float> input, Span<float> output)
    {
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = Math.Max(0, input[i]);
        }
    }

    #endregion

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalMPSOrchestrator));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogInformation("MPS Orchestrator disposing - MPS ops: {MPSOps}, CPU fallbacks: {CPUOps}",
                _metrics.TotalMPSOperations, _metrics.TotalCPUFallbacks);

            _mpsBackend.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Performance metrics for MPS vs CPU operations.
/// </summary>
public sealed class PerformanceMetrics
{
    private readonly Dictionary<MPSOperationType, int> _mpsOperations = [];
    private readonly Dictionary<MPSOperationType, int> _cpuFallbacks = [];
    private readonly Dictionary<MPSOperationType, TimeSpan> _mpsTotalTime = [];
    private readonly Dictionary<MPSOperationType, TimeSpan> _cpuTotalTime = [];
    private readonly Lock _lock = new();

    public int TotalMPSOperations { get; private set; }
    public int TotalCPUFallbacks { get; private set; }

    public void RecordMPSOperation(MPSOperationType type, TimeSpan duration)
    {
        lock (_lock)
        {
            TotalMPSOperations++;
            _mpsOperations[type] = _mpsOperations.GetValueOrDefault(type) + 1;
            _mpsTotalTime[type] = _mpsTotalTime.GetValueOrDefault(type) + duration;
        }
    }

    public void RecordCPUFallback(MPSOperationType type, TimeSpan duration)
    {
        lock (_lock)
        {
            TotalCPUFallbacks++;
            _cpuFallbacks[type] = _cpuFallbacks.GetValueOrDefault(type) + 1;
            _cpuTotalTime[type] = _cpuTotalTime.GetValueOrDefault(type) + duration;
        }
    }

    public Dictionary<MPSOperationType, (int count, TimeSpan total, TimeSpan avg)> GetMPSStatistics()
    {
        lock (_lock)
        {
            var result = new Dictionary<MPSOperationType, (int, TimeSpan, TimeSpan)>();
            foreach (var kvp in _mpsOperations)
            {
                var total = _mpsTotalTime[kvp.Key];
                var avg = TimeSpan.FromTicks(total.Ticks / kvp.Value);
                result[kvp.Key] = (kvp.Value, total, avg);
            }
            return result;
        }
    }

    public Dictionary<MPSOperationType, (int count, TimeSpan total, TimeSpan avg)> GetCPUStatistics()
    {
        lock (_lock)
        {
            var result = new Dictionary<MPSOperationType, (int, TimeSpan, TimeSpan)>();
            foreach (var kvp in _cpuFallbacks)
            {
                var total = _cpuTotalTime[kvp.Key];
                var avg = TimeSpan.FromTicks(total.Ticks / kvp.Value);
                result[kvp.Key] = (kvp.Value, total, avg);
            }
            return result;
        }
    }
}
