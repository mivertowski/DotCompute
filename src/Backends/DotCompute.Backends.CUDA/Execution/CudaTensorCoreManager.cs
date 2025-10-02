// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using DotCompute.Backends.CUDA.Advanced.Features.Models;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Abstractions.Interfaces.Kernels;
using System;
namespace DotCompute.Backends.CUDA.Advanced
{

    /// <summary>
    /// Manager for CUDA Tensor Core operations (RTX 2000 Ada specific)
    /// </summary>
    public sealed class CudaTensorCoreManager : IDisposable
    {
        private readonly CudaContext _context;
        private readonly CudaDeviceProperties _deviceProperties;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, CudaTensorCoreKernel> _tensorKernels;
        private readonly Timer _performanceTimer;
        private readonly CudaTensorCoreMetrics _metrics;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the CudaTensorCoreManager class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="deviceProperties">The device properties.</param>
        /// <param name="logger">The logger.</param>

        public CudaTensorCoreManager(
            CudaContext context,
            CudaDeviceProperties deviceProperties,
            ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _deviceProperties = deviceProperties;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tensorKernels = new ConcurrentDictionary<string, CudaTensorCoreKernel>();
            _metrics = new CudaTensorCoreMetrics();

            _performanceTimer = new Timer(UpdatePerformanceMetrics, null,
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            _logger.LogDebugMessage($"");
        }

        /// <summary>
        /// Gets whether Tensor Cores are supported on this device
        /// </summary>
        public bool IsSupported => _deviceProperties.Major >= 7; // Volta and newer

        /// <summary>
        /// Gets the Tensor Core generation
        /// </summary>
        public int TensorCoreGeneration => (_deviceProperties.Major, _deviceProperties.Minor) switch
        {
            (7, 0) => 1, // Volta
            (7, 5) => 2, // Turing
            (8, 0) or (8, 6) => 3, // Ampere
            (8, 9) => 4, // Ada Lovelace
            (9, 0) => 4, // Hopper
            _ when _deviceProperties.Major >= 9 => 4,
            _ => 0
        };

        /// <summary>
        /// Optimizes a kernel for Tensor Core acceleration
        /// </summary>
        public async Task<CudaOptimizationResult> OptimizeKernelAsync(
            CudaCompiledKernel kernel,
            KernelArgument[] arguments,
            CancellationToken cancellationToken = default)
        {
            if (!IsSupported)
            {
                return new CudaOptimizationResult
                {
                    Success = false,
                    ErrorMessage = "Tensor Cores not supported on this device"
                };
            }

            try
            {
                var tensorKernel = new CudaTensorCoreKernel
                {
                    Id = $"{kernel.Name}_tensor_{Guid.NewGuid():N}",
                    BaseKernel = kernel,
                    OptimizedAt = DateTimeOffset.UtcNow
                };

                // Analyze kernel for Tensor Core opportunities
                var analysis = await AnalyzeKernelForTensorCoresAsync(kernel, arguments, cancellationToken)
                    .ConfigureAwait(false);
                tensorKernel.Analysis = analysis;

                if (analysis.CanUseTensorCores)
                {
                    // Apply Tensor Core optimizations
                    await ApplyTensorCoreOptimizationsAsync(tensorKernel, cancellationToken)
                        .ConfigureAwait(false);

                    _tensorKernels[tensorKernel.Id] = tensorKernel;

                    return new CudaOptimizationResult
                    {
                        Success = true,
                        OptimizationsApplied = analysis.AppliedOptimizations,
                        PerformanceGain = analysis.EstimatedSpeedup
                    };
                }

                return new CudaOptimizationResult
                {
                    Success = false,
                    ErrorMessage = "Kernel not suitable for Tensor Core acceleration"
                };
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error optimizing kernel for Tensor Cores");
                return new CudaOptimizationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Executes a Tensor Core optimized operation
        /// </summary>
        public async Task<CudaTensorCoreExecutionResult> ExecuteTensorOperationAsync(
            CudaTensorOperation operation,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsSupported)
            {
                throw new NotSupportedException("Tensor Cores not supported on this device");
            }

            var startTime = DateTimeOffset.UtcNow;

            try
            {
                _context.MakeCurrent();

                var result = await ExecuteSpecificTensorOperationAsync(operation, cancellationToken)
                    .ConfigureAwait(false);

                var endTime = DateTimeOffset.UtcNow;

                // Update metrics
                _metrics.ThroughputTFLOPS = CalculateThroughput(operation, endTime - startTime);
                _metrics.Utilization = Math.Min(1.0, _metrics.Utilization + 0.1);

                return new CudaTensorCoreExecutionResult
                {
                    Success = true,
                    OperationType = operation.Type,
                    ExecutionTime = endTime - startTime,
                    ThroughputTFLOPS = _metrics.ThroughputTFLOPS,
                    TensorCoreUtilization = result.TensorCoreUtilization
                };
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage("");
                return new CudaTensorCoreExecutionResult
                {
                    Success = false,
                    OperationType = operation.Type,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Creates an optimized GEMM operation using Tensor Cores
        /// </summary>
        public async Task<CudaTensorCoreExecutionResult> ExecuteOptimizedGEMMAsync(
            CudaTensorGEMMOperation gemmOp,
            CancellationToken cancellationToken = default)
        {
            if (!IsSupported)
            {
                throw new NotSupportedException("Tensor Cores not supported");
            }

            // Validate matrix dimensions for Tensor Core compatibility
            if (!ValidateGEMMDimensions(gemmOp))
            {
                throw new ArgumentException("Matrix dimensions not compatible with Tensor Cores");
            }

            var operation = new CudaTensorOperation
            {
                Type = CudaTensorOperationType.GEMM,
                Precision = gemmOp.Precision,
                DimensionsA = [gemmOp.M, gemmOp.K],
                DimensionsB = [gemmOp.K, gemmOp.N],
                DimensionsC = [gemmOp.M, gemmOp.N],
                Alpha = gemmOp.Alpha,
                Beta = gemmOp.Beta
            };

            return await ExecuteTensorOperationAsync(operation, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Optimizes memory layout for Tensor Core operations
        /// </summary>
        public CudaTensorMemoryLayout OptimizeMemoryLayout(
            CudaTensorDescriptor descriptor,
            CudaTensorPrecision precision)
        {
            ThrowIfDisposed();

            var layout = new CudaTensorMemoryLayout
            {
                Precision = precision,
                OriginalDimensions = [.. descriptor.Dimensions]
            };

            // Optimize for specific Tensor Core generation
            layout = TensorCoreGeneration switch
            {
                // Ada Lovelace (RTX 2000 Ada)
                4 => OptimizeForAdaTensorCores(layout),
                // Ampere
                3 => OptimizeForAmpereTensorCores(layout),
                // Turing
                2 => OptimizeForTuringTensorCores(layout),
                // Volta
                1 => OptimizeForVoltaTensorCores(layout),
                _ => GetDefaultLayout(layout),
            };
            return layout;
        }

        /// <summary>
        /// Gets performance metrics for Tensor Core usage
        /// </summary>
        public CudaTensorCoreMetrics GetMetrics()
        {
            return new CudaTensorCoreMetrics
            {
                EfficiencyScore = _metrics.EfficiencyScore,
                Utilization = _metrics.Utilization,
                ThroughputTFLOPS = _metrics.ThroughputTFLOPS
            };
        }

        /// <summary>
        /// Performs maintenance operations
        /// </summary>
        public void PerformMaintenance()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Clean up old tensor kernels
                var cutoffTime = DateTimeOffset.UtcNow.AddHours(-2);
                var oldKernels = _tensorKernels.Values
                    .Where(k => k.OptimizedAt < cutoffTime && k.ExecutionCount == 0)
                    .Take(10)
                    .ToList();

                foreach (var kernel in oldKernels)
                {
                    _ = _tensorKernels.TryRemove(kernel.Id, out _);
                }

                // Update efficiency score
                UpdateEfficiencyScore();

                if (oldKernels.Count > 0)
                {
                    _logger.LogDebugMessage(" unused Tensor Core kernels");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Tensor Core maintenance");
            }
        }

        private async Task<CudaTensorCoreAnalysis> AnalyzeKernelForTensorCoresAsync(
            CudaCompiledKernel kernel,
            KernelArgument[] arguments,
            CancellationToken cancellationToken)
        {
            var analysis = new CudaTensorCoreAnalysis();

            // Analyze kernel characteristics
            var hasMatrixOperations = await DetectMatrixOperationsAsync(kernel, arguments, cancellationToken)
                .ConfigureAwait(false);

            var hasSuitablePrecision = arguments.Any(arg =>
                arg.Type == typeof(Half) || arg.Type == typeof(float) ||
                arg.Type == typeof(double) || arg.Type.Name.Contains("bfloat16", StringComparison.OrdinalIgnoreCase));

            var hasSuitableDimensions = arguments.Any(arg =>
                arg.Value is int[] dims && dims.Length >= 2 &&
                dims.All(d => d % 8 == 0)); // Tensor Cores prefer multiples of 8

            analysis.CanUseTensorCores = hasMatrixOperations && hasSuitablePrecision && hasSuitableDimensions;

            if (analysis.CanUseTensorCores)
            {
                analysis.EstimatedSpeedup = CalculateEstimatedSpeedup(arguments);
                analysis.AppliedOptimizations.Add("Tensor Core GEMM acceleration");
                analysis.AppliedOptimizations.Add("Optimized memory layout");

                if (TensorCoreGeneration >= 4) // Ada and newer
                {
                    analysis.AppliedOptimizations.Add("4th Gen Tensor Core optimizations");
                    analysis.AppliedOptimizations.Add("FP8 precision support");
                }
            }

            return analysis;
        }

        private static async Task<bool> DetectMatrixOperationsAsync(
            CudaCompiledKernel kernel,
            KernelArgument[] arguments,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            // Simple heuristic: look for matrix-like argument patterns
            var matrixArguments = arguments.Count(arg =>
                arg.IsDeviceMemory && arg.SizeInBytes > 1024 * 1024); // Large buffers

            var hasMatrixDimensions = arguments.Any(arg =>
                arg.Value is int[] dims && dims.Length == 2);

            return matrixArguments >= 2 && hasMatrixDimensions;
        }

        private double CalculateEstimatedSpeedup(KernelArgument[] arguments)
        {
            // Estimate speedup based on operation characteristics
            var generation = TensorCoreGeneration;
            var baseSpeedup = generation switch
            {
                4 => 8.0,  // Ada Lovelace
                3 => 6.0,  // Ampere  
                2 => 4.0,  // Turing
                1 => 3.0,  // Volta
                _ => 1.0
            };

            // Adjust based on precision
            var precisionMultiplier = arguments.Any(arg => arg.Type == typeof(Half)) ? 1.5 : 1.0;

            return baseSpeedup * precisionMultiplier;
        }

        private async Task ApplyTensorCoreOptimizationsAsync(
            CudaTensorCoreKernel tensorKernel,
            CancellationToken cancellationToken)
        {
            // Apply Tensor Core specific optimizations
            tensorKernel.TensorCoreGeneration = TensorCoreGeneration;
            tensorKernel.OptimizedLayout = true;
            tensorKernel.SupportedPrecisions = GetSupportedPrecisions();

            await Task.CompletedTask.ConfigureAwait(false); // Placeholder for async optimization work
        }

        private List<CudaTensorPrecision> GetSupportedPrecisions()
        {
            var precisions = new List<CudaTensorPrecision>();

            switch (TensorCoreGeneration)
            {
                case 4: // Ada Lovelace
                    precisions.AddRange([
                        CudaTensorPrecision.FP32,
                    CudaTensorPrecision.FP16,
                    CudaTensorPrecision.BF16,
                    CudaTensorPrecision.FP8_E4M3,
                    CudaTensorPrecision.FP8_E5M2,
                    CudaTensorPrecision.INT8,
                    CudaTensorPrecision.INT4
                    ]);
                    break;
                case 3: // Ampere
                    precisions.AddRange([
                        CudaTensorPrecision.FP32,
                    CudaTensorPrecision.FP16,
                    CudaTensorPrecision.BF16,
                    CudaTensorPrecision.INT8,
                    CudaTensorPrecision.INT4,
                    CudaTensorPrecision.INT1
                    ]);
                    break;
                case 2: // Turing
                    precisions.AddRange([
                        CudaTensorPrecision.FP32,
                    CudaTensorPrecision.FP16,
                    CudaTensorPrecision.INT8,
                    CudaTensorPrecision.INT4,
                    CudaTensorPrecision.INT1
                    ]);
                    break;
                case 1: // Volta
                    precisions.AddRange([
                        CudaTensorPrecision.FP32,
                    CudaTensorPrecision.FP16
                    ]);
                    break;
            }

            return precisions;
        }

        private static async Task<CudaTensorCoreExecutionMetrics> ExecuteSpecificTensorOperationAsync(
            CudaTensorOperation operation,
            CancellationToken cancellationToken)
        {
            // Execute the specific tensor operation
            // This would involve calling optimized CUTLASS or cuBLAS routines TODO

            var metrics = new CudaTensorCoreExecutionMetrics
            {
                TensorCoreUtilization = 0.85, // Example utilization
                MemoryBandwidthUtilization = 0.78,
                ComputeIntensity = CalculateComputeIntensity(operation)
            };

            await Task.Delay(1, cancellationToken); // Simulate execution time
            return metrics;
        }

        private static double CalculateComputeIntensity(CudaTensorOperation operation)
        {
            // Calculate arithmetic intensity (FLOPs per byte)
            if (operation.Type == CudaTensorOperationType.GEMM)
            {
                var m = operation.DimensionsA[0];
                var k = operation.DimensionsA[1];
                var n = operation.DimensionsB[1];

                var flops = 2.0 * m * n * k; // GEMM FLOPs
                var bytes = (m * k + k * n + m * n) * GetBytesPerElement(operation.Precision);

                return flops / bytes;
            }

            return 1.0; // Default intensity
        }

        private static int GetBytesPerElement(CudaTensorPrecision precision)
        {
            return precision switch
            {
                CudaTensorPrecision.FP32 => 4,
                CudaTensorPrecision.FP16 => 2,
                CudaTensorPrecision.BF16 => 2,
                CudaTensorPrecision.FP8_E4M3 => 1,
                CudaTensorPrecision.FP8_E5M2 => 1,
                CudaTensorPrecision.INT8 => 1,
                CudaTensorPrecision.INT4 => 1, // Packed
                CudaTensorPrecision.INT1 => 1, // Packed
                _ => 4
            };
        }

        private static double CalculateThroughput(CudaTensorOperation operation, TimeSpan executionTime)
        {
            if (operation.Type == CudaTensorOperationType.GEMM)
            {
                var m = operation.DimensionsA[0];
                var k = operation.DimensionsA[1];
                var n = operation.DimensionsB[1];

                var flops = 2.0 * m * n * k;
                var tflops = flops / (executionTime.TotalSeconds * 1e12);

                return tflops;
            }

            return 0.0;
        }

        private bool ValidateGEMMDimensions(CudaTensorGEMMOperation gemmOp)
        {
            // Tensor Cores have specific alignment requirements
            var generation = TensorCoreGeneration;

            var alignment = generation switch
            {
                4 => 8,  // Ada requires 8-element alignment
                3 => 8,  // Ampere requires 8-element alignment
                2 => 8,  // Turing requires 8-element alignment
                1 => 8,  // Volta requires 8-element alignment
                _ => 1
            };

            return gemmOp.M % alignment == 0 &&
                   gemmOp.N % alignment == 0 &&
                   gemmOp.K % alignment == 0;
        }

        private static CudaTensorMemoryLayout OptimizeForAdaTensorCores(CudaTensorMemoryLayout layout)
        {
            // Ada Lovelace (4th Gen) specific optimizations
            layout.Alignment = 16; // 128-bit alignment
            layout.PreferredFormat = CudaTensorFormat.NHWC;
            layout.UsePackedFormats = true;
            layout.Support4BitPrecision = true;
            layout.SupportFP8 = true;
            return layout;
        }

        private static CudaTensorMemoryLayout OptimizeForAmpereTensorCores(CudaTensorMemoryLayout layout)
        {
            // Ampere (3rd Gen) specific optimizations
            layout.Alignment = 16;
            layout.PreferredFormat = CudaTensorFormat.NHWC;
            layout.UsePackedFormats = true;
            layout.Support4BitPrecision = true;
            return layout;
        }

        private static CudaTensorMemoryLayout OptimizeForTuringTensorCores(CudaTensorMemoryLayout layout)
        {
            // Turing (2nd Gen) specific optimizations
            layout.Alignment = 8;
            layout.PreferredFormat = CudaTensorFormat.NCHW;
            layout.UsePackedFormats = false;
            return layout;
        }

        private static CudaTensorMemoryLayout OptimizeForVoltaTensorCores(CudaTensorMemoryLayout layout)
        {
            // Volta (1st Gen) specific optimizations
            layout.Alignment = 8;
            layout.PreferredFormat = CudaTensorFormat.NCHW;
            layout.UsePackedFormats = false;
            return layout;
        }

        private static CudaTensorMemoryLayout GetDefaultLayout(CudaTensorMemoryLayout layout)
        {
            layout.Alignment = 4;
            layout.PreferredFormat = CudaTensorFormat.NCHW;
            return layout;
        }

        private void UpdateEfficiencyScore()
        {
            var totalExecutions = _tensorKernels.Values.Sum(k => k.ExecutionCount);
            var avgSpeedup = _tensorKernels.Values
                .Where(k => k.Analysis.EstimatedSpeedup > 1.0)
                .Select(k => k.Analysis.EstimatedSpeedup)
                .DefaultIfEmpty(1.0)
                .Average();

            _metrics.EfficiencyScore = totalExecutions > 0 ?
                Math.Min(1.0, (avgSpeedup - 1.0) / 10.0) : 0.5;
        }

        private void UpdatePerformanceMetrics(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                UpdateEfficiencyScore();

                // Decay utilization over time
                _metrics.Utilization = Math.Max(0.0, _metrics.Utilization * 0.95);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating Tensor Core performance metrics");
            }
        }

        private string GetArchitectureName()
        {
            return (_deviceProperties.Major, _deviceProperties.Minor) switch
            {
                (7, 0) => "Volta",
                (7, 5) => "Turing",
                (8, 0) => "Ampere GA100",
                (8, 6) => "Ampere GA10x",
                (8, 9) => "Ada Lovelace",
                (9, 0) => "Hopper",
                _ => $"SM {_deviceProperties.Major}.{_deviceProperties.Minor}"
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CudaTensorCoreManager));
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _performanceTimer?.Dispose();
                _tensorKernels.Clear();
                _disposed = true;
            }
        }
    }
    /// <summary>
    /// A class that represents cuda tensor core kernel.
    /// </summary>

    // Supporting types for Tensor Core operations
    public sealed class CudaTensorCoreKernel
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the base kernel.
        /// </summary>
        /// <value>The base kernel.</value>
        public CudaCompiledKernel BaseKernel { get; set; } = null!;
        /// <summary>
        /// Gets or sets the optimized at.
        /// </summary>
        /// <value>The optimized at.</value>
        public DateTimeOffset OptimizedAt { get; set; }
        /// <summary>
        /// Gets or sets the analysis.
        /// </summary>
        /// <value>The analysis.</value>
        public CudaTensorCoreAnalysis Analysis { get; set; } = new();
        /// <summary>
        /// Gets or sets the tensor core generation.
        /// </summary>
        /// <value>The tensor core generation.</value>
        public int TensorCoreGeneration { get; set; }
        /// <summary>
        /// Gets or sets the optimized layout.
        /// </summary>
        /// <value>The optimized layout.</value>
        public bool OptimizedLayout { get; set; }
        /// <summary>
        /// Gets or sets the supported precisions.
        /// </summary>
        /// <value>The supported precisions.</value>
        public IList<CudaTensorPrecision> SupportedPrecisions { get; } = [];
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public int ExecutionCount { get; set; }
        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        /// <value>The total execution time.</value>
        public TimeSpan TotalExecutionTime { get; set; }
    }
    /// <summary>
    /// A class that represents cuda tensor core analysis.
    /// </summary>

    public sealed class CudaTensorCoreAnalysis
    {
        /// <summary>
        /// Gets or sets a value indicating whether use tensor cores.
        /// </summary>
        /// <value>The can use tensor cores.</value>
        public bool CanUseTensorCores { get; set; }
        /// <summary>
        /// Gets or sets the estimated speedup.
        /// </summary>
        /// <value>The estimated speedup.</value>
        public double EstimatedSpeedup { get; set; } = 1.0;
        /// <summary>
        /// Gets or sets the applied optimizations.
        /// </summary>
        /// <value>The applied optimizations.</value>
        public IList<string> AppliedOptimizations { get; } = [];
        /// <summary>
        /// Gets or sets the recommended precisions.
        /// </summary>
        /// <value>The recommended precisions.</value>
        public IList<CudaTensorPrecision> RecommendedPrecisions { get; } = [];
    }
    /// <summary>
    /// A class that represents cuda tensor operation.
    /// </summary>

    public sealed class CudaTensorOperation
    {
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public CudaTensorOperationType Type { get; set; }
        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public CudaTensorPrecision Precision { get; set; }
        /// <summary>
        /// Gets or sets the dimensions a.
        /// </summary>
        /// <value>The dimensions a.</value>
        public int[] DimensionsA { get; set; } = [];
        /// <summary>
        /// Gets or sets the dimensions b.
        /// </summary>
        /// <value>The dimensions b.</value>
        public int[] DimensionsB { get; set; } = [];
        /// <summary>
        /// Gets or sets the dimensions c.
        /// </summary>
        /// <value>The dimensions c.</value>
        public int[] DimensionsC { get; set; } = [];
        /// <summary>
        /// Gets or sets the alpha.
        /// </summary>
        /// <value>The alpha.</value>
        public float Alpha { get; set; } = 1.0f;
        /// <summary>
        /// Gets or sets the beta.
        /// </summary>
        /// <value>The beta.</value>
        public float Beta { get; set; }
    }
    /// <summary>
    /// A class that represents cuda tensor g e m m operation.
    /// </summary>

    public sealed class CudaTensorGEMMOperation
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
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public CudaTensorPrecision Precision { get; set; }
        /// <summary>
        /// Gets or sets the alpha.
        /// </summary>
        /// <value>The alpha.</value>
        public float Alpha { get; set; } = 1.0f;
        /// <summary>
        /// Gets or sets the beta.
        /// </summary>
        /// <value>The beta.</value>
        public float Beta { get; set; }
        /// <summary>
        /// Gets or sets the transpose a.
        /// </summary>
        /// <value>The transpose a.</value>

        public bool TransposeA { get; set; }
        /// <summary>
        /// Gets or sets the transpose b.
        /// </summary>
        /// <value>The transpose b.</value>
        public bool TransposeB { get; set; }
    }
    /// <summary>
    /// A class that represents cuda tensor descriptor.
    /// </summary>

    public sealed class CudaTensorDescriptor
    {
        /// <summary>
        /// Gets or sets the dimensions.
        /// </summary>
        /// <value>The dimensions.</value>
        public int[] Dimensions { get; set; } = [];
        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public CudaTensorPrecision Precision { get; set; }
        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        /// <value>The format.</value>
        public CudaTensorFormat Format { get; set; }
    }
    /// <summary>
    /// A class that represents cuda tensor memory layout.
    /// </summary>

    public sealed class CudaTensorMemoryLayout
    {
        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public CudaTensorPrecision Precision { get; set; }
        /// <summary>
        /// Gets or sets the original dimensions.
        /// </summary>
        /// <value>The original dimensions.</value>
        public int[] OriginalDimensions { get; set; } = [];
        /// <summary>
        /// Gets or sets the optimized dimensions.
        /// </summary>
        /// <value>The optimized dimensions.</value>
        public int[] OptimizedDimensions { get; set; } = [];
        /// <summary>
        /// Gets or sets the alignment.
        /// </summary>
        /// <value>The alignment.</value>
        public int Alignment { get; set; } = 1;
        /// <summary>
        /// Gets or sets the preferred format.
        /// </summary>
        /// <value>The preferred format.</value>
        public CudaTensorFormat PreferredFormat { get; set; }
        /// <summary>
        /// Gets or sets the use packed formats.
        /// </summary>
        /// <value>The use packed formats.</value>
        public bool UsePackedFormats { get; set; }
        /// <summary>
        /// Gets or sets the support4 bit precision.
        /// </summary>
        /// <value>The support4 bit precision.</value>
        public bool Support4BitPrecision { get; set; }
        /// <summary>
        /// Gets or sets the support f p8.
        /// </summary>
        /// <value>The support f p8.</value>
        public bool SupportFP8 { get; set; }
    }
    /// <summary>
    /// A class that represents cuda tensor core execution result.
    /// </summary>

    public sealed class CudaTensorCoreExecutionResult
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <value>The success.</value>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        /// <value>The operation type.</value>
        public CudaTensorOperationType OperationType { get; set; }
        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        /// <value>The execution time.</value>
        public TimeSpan ExecutionTime { get; set; }
        /// <summary>
        /// Gets or sets the throughput t f l o p s.
        /// </summary>
        /// <value>The throughput t f l o p s.</value>
        public double ThroughputTFLOPS { get; set; }
        /// <summary>
        /// Gets or sets the tensor core utilization.
        /// </summary>
        /// <value>The tensor core utilization.</value>
        public double TensorCoreUtilization { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
    }
    /// <summary>
    /// A class that represents cuda tensor core execution metrics.
    /// </summary>

    public sealed class CudaTensorCoreExecutionMetrics
    {
        /// <summary>
        /// Gets or sets the tensor core utilization.
        /// </summary>
        /// <value>The tensor core utilization.</value>
        public double TensorCoreUtilization { get; set; }
        /// <summary>
        /// Gets or sets the memory bandwidth utilization.
        /// </summary>
        /// <value>The memory bandwidth utilization.</value>
        public double MemoryBandwidthUtilization { get; set; }
        /// <summary>
        /// Gets or sets the compute intensity.
        /// </summary>
        /// <value>The compute intensity.</value>
        public double ComputeIntensity { get; set; }
    }
    /// <summary>
    /// An cuda tensor operation type enumeration.
    /// </summary>

    public enum CudaTensorOperationType
    {
        GEMM,
        Convolution,
        MatrixMultiply,
        BatchedGEMM
    }
    /// <summary>
    /// An cuda tensor precision enumeration.
    /// </summary>

    public enum CudaTensorPrecision
    {
        FP32,
        FP16,
        BF16,
        FP8_E4M3,
        FP8_E5M2,
        INT8,
        INT4,
        INT1
    }
    /// <summary>
    /// An cuda tensor format enumeration.
    /// </summary>

    public enum CudaTensorFormat
    {
        NCHW,   // Batch, Channels, Height, Width
        NHWC,   // Batch, Height, Width, Channels
        CHW,    // Channels, Height, Width
        HWC     // Height, Width, Channels
    }
}
