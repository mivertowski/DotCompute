// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Linq.Optimization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Selects the optimal compute backend (CPU SIMD, CUDA, Metal) based on workload characteristics.
/// Implements intelligent backend selection considering data size, compute intensity, transfer overhead,
/// and hardware availability.
/// </summary>
public sealed class BackendSelector
{
    private readonly ILogger<BackendSelector> _logger;
    private readonly AvailableBackends _availableBackends;
    private static readonly object _detectionLock = new();
    private static AvailableBackends? _cachedBackends;

    // Performance thresholds (tuned based on benchmarks)
    private const int MinGpuDataSize = 10_000; // Elements
    private const int OptimalGpuDataSize = 1_000_000; // Elements
    private const double PcieBandwidthGBps = 12.0; // PCIe Gen3 x16
    private const double KernelLaunchOverheadMs = 0.050; // 50 microseconds

    /// <summary>
    /// Initializes a new instance of the BackendSelector.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public BackendSelector(ILogger<BackendSelector>? logger = null)
    {
        _logger = logger ?? NullLogger<BackendSelector>.Instance;
        _availableBackends = DetectAvailableBackends();

        _logger.LogInformation("Backend detection complete: {Backends}", _availableBackends);
    }

    /// <summary>
    /// Selects the optimal compute backend for the given workload.
    /// </summary>
    /// <param name="workload">Workload characteristics to analyze.</param>
    /// <returns>The recommended compute backend.</returns>
    public ComputeBackend SelectBackend(WorkloadCharacteristics workload)
    {
        ArgumentNullException.ThrowIfNull(workload);

        // Always have CPU as fallback
        if (_availableBackends == AvailableBackends.CpuSimd)
        {
            _logger.LogDebug("Only CPU backend available, using CPU SIMD");
            return ComputeBackend.CpuSimd;
        }

        // Check if GPU is beneficial for this workload
        var gpuBeneficial = IsGpuBeneficial(workload);

        if (!gpuBeneficial)
        {
            _logger.LogDebug("GPU not beneficial for workload {Workload}, using CPU", workload);
            return ComputeBackend.CpuSimd;
        }

        // Prefer CUDA if available
        if (_availableBackends.HasFlag(AvailableBackends.Cuda))
        {
            _logger.LogDebug("Selected CUDA backend for workload {Workload}", workload);
            return ComputeBackend.Cuda;
        }

        // Fall back to Metal if available
        if (_availableBackends.HasFlag(AvailableBackends.Metal))
        {
            _logger.LogDebug("Selected Metal backend for workload {Workload}", workload);
            return ComputeBackend.Metal;
        }

        // Final fallback to CPU
        _logger.LogDebug("No GPU backend beneficial, using CPU for workload {Workload}", workload);
        return ComputeBackend.CpuSimd;
    }

    /// <summary>
    /// Determines if GPU acceleration is beneficial for the given workload.
    /// </summary>
    /// <param name="workload">Workload characteristics to analyze.</param>
    /// <returns>True if GPU is expected to provide better performance than CPU.</returns>
    public bool IsGpuBeneficial(WorkloadCharacteristics workload)
    {
        ArgumentNullException.ThrowIfNull(workload);

        // Rule 1: Data size threshold
        if (workload.DataSize < MinGpuDataSize)
        {
            _logger.LogTrace("Workload too small for GPU: {Size} < {Threshold}",
                workload.DataSize, MinGpuDataSize);
            return false;
        }

        // Rule 2: Data already on device - no transfer overhead
        if (workload.IsDataAlreadyOnDevice && workload.IsResultConsumedOnDevice)
        {
            _logger.LogTrace("Data already on device, GPU beneficial");
            return true;
        }

        // Rule 3: Estimate transfer overhead vs computation time
        var transferOverhead = EstimateTransferOverhead(workload.DataSize, workload.IsDataAlreadyOnDevice,
            workload.IsResultConsumedOnDevice);
        var computeCost = workload.EstimateComputationalCost();

        // GPU is beneficial if compute cost significantly exceeds transfer overhead
        // Use 2x multiplier to account for GPU efficiency gains
        var worthTransfer = computeCost > (transferOverhead.TotalMilliseconds * 2.0);

        if (!worthTransfer)
        {
            _logger.LogTrace("Transfer overhead too high: {Transfer}ms vs compute {Compute}",
                transferOverhead.TotalMilliseconds, computeCost);
            return false;
        }

        // Rule 4: Compute intensity must be at least Medium
        if (workload.Intensity < ComputeIntensity.Medium)
        {
            _logger.LogTrace("Compute intensity too low: {Intensity}", workload.Intensity);
            return false;
        }

        // Rule 5: Certain operations benefit more from GPU
        var gpuFriendlyOperation = workload.PrimaryOperation switch
        {
            OperationType.Map => workload.DataSize > OptimalGpuDataSize / 2, // Maps scale well
            OperationType.Reduce => workload.DataSize > OptimalGpuDataSize / 10, // Reductions need more data
            OperationType.Filter => workload.DataSize > OptimalGpuDataSize, // Filter requires compaction
            OperationType.Scan => workload.DataSize > OptimalGpuDataSize / 5, // Scans have dependencies
            OperationType.OrderBy => workload.DataSize > OptimalGpuDataSize / 2, // Sorts parallelize well
            OperationType.Join => workload.DataSize > OptimalGpuDataSize / 10, // Joins are compute-heavy
            OperationType.GroupBy => workload.DataSize > OptimalGpuDataSize / 5, // Grouping benefits from GPU
            _ => workload.DataSize > OptimalGpuDataSize // Conservative for unknown ops
        };

        if (!gpuFriendlyOperation)
        {
            _logger.LogTrace("Operation {Op} not beneficial for GPU at size {Size}",
                workload.PrimaryOperation, workload.DataSize);
            return false;
        }

        // Rule 6: High parallelism degree favors GPU
        if (workload.ParallelismDegree < 1000)
        {
            _logger.LogTrace("Insufficient parallelism: {Degree} < 1000", workload.ParallelismDegree);
            return false;
        }

        _logger.LogDebug("GPU beneficial for workload: Size={Size}, Intensity={Intensity}, Op={Op}, " +
                        "ParallelismDegree={Parallelism}, TransferOverhead={Transfer}ms",
            workload.DataSize, workload.Intensity, workload.PrimaryOperation,
            workload.ParallelismDegree, transferOverhead.TotalMilliseconds);

        return true;
    }

    /// <summary>
    /// Detects which compute backends are available on the current system.
    /// </summary>
    /// <returns>Flags indicating available backends.</returns>
    public static AvailableBackends DetectAvailableBackends()
    {
        // Use cached result if available
        if (_cachedBackends.HasValue)
        {
            return _cachedBackends.Value;
        }

        lock (_detectionLock)
        {
            // Double-check after acquiring lock
            if (_cachedBackends.HasValue)
            {
                return _cachedBackends.Value;
            }

            var backends = AvailableBackends.CpuSimd; // CPU always available

            // Check CUDA availability
            try
            {
                // Try to create a CUDA accelerator - if this succeeds, CUDA is available
                var cudaTest = new CudaAccelerator(deviceId: 0, logger: null);
                if (cudaTest.IsAvailable)
                {
                    backends |= AvailableBackends.Cuda;
                }
                cudaTest.DisposeAsync().AsTask().Wait();
            }
            catch
            {
                // CUDA not available - this is expected on systems without NVIDIA GPUs
            }

            // Check Metal availability (macOS only)
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    // Try to create a Metal accelerator - if this succeeds, Metal is available
                    var metalOptions = new MetalAcceleratorOptions();
                    var options = Microsoft.Extensions.Options.Options.Create(metalOptions);
                    var metalLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalAccelerator>.Instance;
                    var metalTest = new MetalAccelerator(options, metalLogger);
                    // If we got here without throwing, Metal is available
                    if (metalTest.Info is not null)
                    {
                        backends |= AvailableBackends.Metal;
                    }
                    metalTest.DisposeAsync().AsTask().Wait();
                }
            }
            catch
            {
                // Metal not available - this is expected on non-macOS systems or systems without Metal support
            }

            _cachedBackends = backends;
            return backends;
        }
    }

    /// <summary>
    /// Estimates the data transfer overhead for GPU execution.
    /// </summary>
    /// <param name="dataSize">Number of elements to transfer.</param>
    /// <param name="dataOnDevice">True if data is already on the device.</param>
    /// <param name="resultOnDevice">True if result will be consumed on the device.</param>
    /// <returns>Estimated transfer time.</returns>
    public static TimeSpan EstimateTransferOverhead(int dataSize, bool dataOnDevice = false,
        bool resultOnDevice = false)
    {
        var totalMs = KernelLaunchOverheadMs;

        // Estimate bytes assuming typical data types (float32/int32)
        const int bytesPerElement = 4;
        var totalBytes = (long)dataSize * bytesPerElement;

        // Host-to-Device transfer (input)
        if (!dataOnDevice)
        {
            var h2dSeconds = totalBytes / (PcieBandwidthGBps * 1_000_000_000.0);
            totalMs += h2dSeconds * 1000.0;
        }

        // Device-to-Host transfer (output)
        if (!resultOnDevice)
        {
            var d2hSeconds = totalBytes / (PcieBandwidthGBps * 1_000_000_000.0);
            totalMs += d2hSeconds * 1000.0;
        }

        return TimeSpan.FromMilliseconds(totalMs);
    }

    /// <summary>
    /// Gets the available backends on this system.
    /// </summary>
    public AvailableBackends GetAvailableBackends() => _availableBackends;

    /// <summary>
    /// Checks if a specific backend is available.
    /// </summary>
    /// <param name="backend">The backend to check.</param>
    /// <returns>True if the backend is available.</returns>
    public bool IsBackendAvailable(ComputeBackend backend)
    {
        return backend switch
        {
            ComputeBackend.CpuSimd => _availableBackends.HasFlag(AvailableBackends.CpuSimd),
            ComputeBackend.Cuda => _availableBackends.HasFlag(AvailableBackends.Cuda),
            ComputeBackend.Metal => _availableBackends.HasFlag(AvailableBackends.Metal),
            _ => false
        };
    }

    /// <summary>
    /// Provides recommendations for optimizing a workload.
    /// </summary>
    /// <param name="workload">Workload to analyze.</param>
    /// <returns>A list of optimization recommendations.</returns>
    public IReadOnlyList<string> GetOptimizationRecommendations(WorkloadCharacteristics workload)
    {
        var recommendations = new List<string>();

        if (workload.DataSize < MinGpuDataSize && workload.Intensity >= ComputeIntensity.High)
        {
            recommendations.Add($"Consider batching operations - data size ({workload.DataSize}) is below GPU threshold ({MinGpuDataSize})");
        }

        if (workload.IsMemoryBound && !workload.IsFusible)
        {
            recommendations.Add("Memory-bound operation detected - consider fusing with adjacent operations to reduce memory traffic");
        }

        if (workload.HasRandomAccess && workload.DataSize > OptimalGpuDataSize)
        {
            recommendations.Add("Random memory access pattern detected - consider data restructuring for better cache locality");
        }

        if (!workload.IsDataAlreadyOnDevice && workload.DataSize > OptimalGpuDataSize)
        {
            recommendations.Add("Large data transfer detected - consider keeping data resident on device for multiple operations");
        }

        if (workload.ParallelismDegree < workload.DataSize / 10)
        {
            recommendations.Add($"Low parallelism degree ({workload.ParallelismDegree}) - consider algorithm redesign for more parallelism");
        }

        return recommendations;
    }
}
