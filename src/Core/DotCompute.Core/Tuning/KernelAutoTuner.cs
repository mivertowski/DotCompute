// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Tuning;

/// <summary>
/// Auto-tuner for kernel launch configurations and backend selection.
/// </summary>
/// <remarks>
/// <para>
/// Automatically tunes kernel execution parameters based on:
/// <list type="bullet">
/// <item><description>Workload size and characteristics</description></item>
/// <item><description>Hardware capabilities and current load</description></item>
/// <item><description>Historical performance data</description></item>
/// <item><description>Real-time profiling results</description></item>
/// </list>
/// </para>
/// <para>
/// Tunable parameters include:
/// <list type="bullet">
/// <item><description>Block/thread group sizes</description></item>
/// <item><description>Grid dimensions</description></item>
/// <item><description>Shared memory usage</description></item>
/// <item><description>Backend selection (CPU/GPU)</description></item>
/// <item><description>Memory transfer strategies</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class KernelAutoTuner : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, KernelTuningProfile> _profiles;
    private readonly ConcurrentDictionary<string, TuningSession> _activeSessions;
    private readonly TuningOptions _options;
    private readonly SemaphoreSlim _tuningLock;
    private bool _disposed;

    /// <summary>
    /// Initializes a new kernel auto-tuner.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Tuning options.</param>
    public KernelAutoTuner(ILogger logger, TuningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options ?? new TuningOptions();
        _profiles = new ConcurrentDictionary<string, KernelTuningProfile>();
        _activeSessions = new ConcurrentDictionary<string, TuningSession>();
        _tuningLock = new SemaphoreSlim(1, 1);

        _logger.LogInfoMessage("Kernel auto-tuner initialized");
    }

    /// <summary>
    /// Gets optimal launch configuration for a kernel.
    /// </summary>
    /// <param name="kernelId">Unique kernel identifier.</param>
    /// <param name="workloadSize">Total work items to process.</param>
    /// <param name="device">Target device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Optimal launch configuration.</returns>
    public ValueTask<LaunchConfiguration> GetOptimalConfigurationAsync(
        string kernelId,
        long workloadSize,
        IAccelerator device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelId);
        ArgumentNullException.ThrowIfNull(device);

        // Check for cached configuration
        var cacheKey = GetCacheKey(kernelId, workloadSize, device);
        if (_profiles.TryGetValue(cacheKey, out var profile) && profile.IsValid)
        {
            _logger.LogDebugMessage(
                $"Using cached configuration for kernel {kernelId}: " +
                $"Block={profile.OptimalBlockSize}, Grid={profile.OptimalGridSize}");
            return ValueTask.FromResult(profile.ToLaunchConfiguration());
        }

        // Generate default configuration based on device and workload
        var config = GenerateDefaultConfiguration(workloadSize, device);

        _logger.LogDebugMessage(
            $"Generated default configuration for kernel {kernelId}: " +
            $"Block={config.BlockSize}, Grid={config.GridSize}");

        return ValueTask.FromResult(config);
    }

    /// <summary>
    /// Starts a tuning session for a kernel.
    /// </summary>
    /// <param name="kernelId">Unique kernel identifier.</param>
    /// <param name="device">Target device.</param>
    /// <param name="workloadSizes">Workload sizes to tune for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tuning session.</returns>
    public async ValueTask<TuningSession> StartTuningSessionAsync(
        string kernelId,
        IAccelerator device,
        long[] workloadSizes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelId);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(workloadSizes);

        await _tuningLock.WaitAsync(cancellationToken);
        try
        {
            var session = new TuningSession
            {
                SessionId = Guid.NewGuid().ToString(),
                KernelId = kernelId,
                DeviceId = device.Info.Id,
                WorkloadSizes = workloadSizes.ToList(),
                Status = TuningStatus.Running,
                StartTime = DateTimeOffset.UtcNow,
                ConfigurationsToTest = GenerateConfigurationSpace(device)
            };

            _activeSessions[session.SessionId] = session;

            _logger.LogInfoMessage(
                $"Started tuning session {session.SessionId} for kernel {kernelId} " +
                $"with {session.ConfigurationsToTest.Count} configurations");

            return session;
        }
        finally
        {
            _tuningLock.Release();
        }
    }

    /// <summary>
    /// Records the execution time for a configuration.
    /// </summary>
    /// <param name="sessionId">Tuning session ID.</param>
    /// <param name="config">Configuration that was tested.</param>
    /// <param name="workloadSize">Workload size.</param>
    /// <param name="executionTime">Measured execution time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask RecordExecutionAsync(
        string sessionId,
        LaunchConfiguration config,
        long workloadSize,
        TimeSpan executionTime,
        CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarningMessage($"Unknown tuning session: {sessionId}");
            return ValueTask.CompletedTask;
        }

        var measurement = new TuningMeasurement
        {
            Configuration = config,
            WorkloadSize = workloadSize,
            ExecutionTime = executionTime,
            Throughput = workloadSize / executionTime.TotalSeconds,
            Timestamp = DateTimeOffset.UtcNow
        };

        session.Measurements.Add(measurement);

        _logger.LogDebugMessage(
            $"Recorded measurement: Block={config.BlockSize}, Grid={config.GridSize}, " +
            $"Time={executionTime.TotalMilliseconds:F3}ms, Throughput={measurement.Throughput:F0} items/s");

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Completes a tuning session and saves the optimal configuration.
    /// </summary>
    /// <param name="sessionId">Tuning session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tuning result.</returns>
    public ValueTask<TuningResult> CompleteTuningSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryRemove(sessionId, out var session))
        {
            return ValueTask.FromResult(new TuningResult
            {
                IsSuccessful = false,
                ErrorMessage = $"Unknown tuning session: {sessionId}"
            });
        }

        if (session.Measurements.Count == 0)
        {
            return ValueTask.FromResult(new TuningResult
            {
                IsSuccessful = false,
                ErrorMessage = "No measurements recorded"
            });
        }

        // Find best configuration for each workload size
        var bestConfigs = new Dictionary<long, LaunchConfiguration>();
        foreach (var workloadSize in session.WorkloadSizes)
        {
            var relevantMeasurements = session.Measurements
                .Where(m => m.WorkloadSize == workloadSize)
                .ToList();

            if (relevantMeasurements.Count == 0)
            {
                continue;
            }

            var best = relevantMeasurements.OrderBy(m => m.ExecutionTime).First();
            bestConfigs[workloadSize] = best.Configuration;

            // Save profile
            var cacheKey = GetCacheKey(session.KernelId, workloadSize, session.DeviceId);
            var profile = new KernelTuningProfile
            {
                KernelId = session.KernelId,
                DeviceId = session.DeviceId,
                WorkloadSize = workloadSize,
                OptimalBlockSize = best.Configuration.BlockSize,
                OptimalGridSize = best.Configuration.GridSize,
                OptimalSharedMemoryBytes = best.Configuration.SharedMemoryBytes,
                BestExecutionTime = best.ExecutionTime,
                BestThroughput = best.Throughput,
                TuningIterations = relevantMeasurements.Count,
                LastTuned = DateTimeOffset.UtcNow
            };

            _profiles[cacheKey] = profile;
        }

        session.Status = TuningStatus.Completed;
        session.EndTime = DateTimeOffset.UtcNow;

        var result = new TuningResult
        {
            IsSuccessful = true,
            SessionId = sessionId,
            KernelId = session.KernelId,
            TotalMeasurements = session.Measurements.Count,
            BestConfigurations = bestConfigs,
            TuningDuration = session.EndTime.Value - session.StartTime,
            SpeedupAchieved = CalculateSpeedup(session)
        };

        _logger.LogInfoMessage(
            $"Completed tuning session {sessionId}: " +
            $"{result.TotalMeasurements} measurements, " +
            $"{result.SpeedupAchieved:P1} speedup achieved");

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Selects the optimal backend for a workload.
    /// </summary>
    /// <param name="workloadSize">Size of the workload.</param>
    /// <param name="availableDevices">Available devices to choose from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recommended device.</returns>
    public ValueTask<BackendRecommendation> SelectOptimalBackendAsync(
        long workloadSize,
        IAccelerator[] availableDevices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availableDevices);

        if (availableDevices.Length == 0)
        {
            return ValueTask.FromResult(new BackendRecommendation
            {
                IsSuccessful = false,
                ErrorMessage = "No devices available"
            });
        }

        // Simple heuristic-based selection
        // In production, this would use ML models and historical data
        IAccelerator? recommended = null;
        double bestScore = 0;
        var reason = "";

        foreach (var device in availableDevices)
        {
            var score = CalculateDeviceScore(workloadSize, device);
            if (score > bestScore)
            {
                bestScore = score;
                recommended = device;
            }
        }

        if (recommended == null)
        {
            return ValueTask.FromResult(new BackendRecommendation
            {
                IsSuccessful = false,
                ErrorMessage = "Could not determine optimal backend"
            });
        }

        // Determine reason
        reason = workloadSize switch
        {
            < 10000 => "Small workload - CPU overhead would dominate GPU transfer time",
            < 100000 => "Medium workload - GPU may provide moderate speedup",
            _ => "Large workload - GPU parallelism provides significant speedup"
        };

        return ValueTask.FromResult(new BackendRecommendation
        {
            IsSuccessful = true,
            RecommendedDevice = recommended,
            ConfidenceScore = bestScore,
            Reason = reason,
            AlternativeDevices = availableDevices.Where(d => d != recommended).ToArray()
        });
    }

    /// <summary>
    /// Gets tuning statistics.
    /// </summary>
    public TuningStatistics GetStatistics()
    {
        return new TuningStatistics
        {
            TotalProfiles = _profiles.Count,
            ActiveSessions = _activeSessions.Count,
            AverageSpeedup = _profiles.Values.Count > 0
                ? _profiles.Values.Average(p => p.BestThroughput / (p.WorkloadSize / 1000.0))
                : 0,
            MostTunedKernels = _profiles.Values
                .GroupBy(p => p.KernelId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private LaunchConfiguration GenerateDefaultConfiguration(long workloadSize, IAccelerator device)
    {
        // Default heuristics for launch configuration
        var maxThreadsPerBlock = device.Info.MaxThreadsPerBlock;
        var multiprocessorCount = Math.Max(1, device.Info.MaxComputeUnits);

        // Common block sizes for good occupancy
        int blockSize;
        if (workloadSize < 256)
        {
            blockSize = Math.Min(32, (int)workloadSize);
        }
        else if (workloadSize < 1024)
        {
            blockSize = 128;
        }
        else if (workloadSize < 10000)
        {
            blockSize = 256;
        }
        else
        {
            blockSize = Math.Min(512, maxThreadsPerBlock);
        }

        // Ensure block size is a power of 2 for optimal performance
        blockSize = RoundToPowerOfTwo(blockSize);

        // Calculate grid size to cover all work items
        var gridSize = (int)Math.Ceiling((double)workloadSize / blockSize);

        // Limit grid size to reasonable maximum (can launch multiple batches if needed)
        gridSize = Math.Min(gridSize, multiprocessorCount * 32);

        return new LaunchConfiguration
        {
            BlockSize = blockSize,
            GridSize = gridSize,
            SharedMemoryBytes = 0, // Default to no shared memory
            PreferredBackend = device.Info.DeviceType
        };
    }

    private List<LaunchConfiguration> GenerateConfigurationSpace(IAccelerator device)
    {
        var configs = new List<LaunchConfiguration>();
        var maxThreads = device.Info.MaxThreadsPerBlock;

        // Generate configurations with different block sizes
        int[] blockSizes = [32, 64, 128, 256, 512];
        int[] gridMultipliers = [1, 2, 4, 8, 16];

        foreach (var blockSize in blockSizes.Where(b => b <= maxThreads))
        {
            foreach (var gridMult in gridMultipliers)
            {
                configs.Add(new LaunchConfiguration
                {
                    BlockSize = blockSize,
                    GridSize = device.Info.MaxComputeUnits * gridMult,
                    SharedMemoryBytes = 0,
                    PreferredBackend = device.Info.DeviceType
                });

                // Also test with shared memory
                if (device.Info.MaxSharedMemoryPerBlock > 0)
                {
                    configs.Add(new LaunchConfiguration
                    {
                        BlockSize = blockSize,
                        GridSize = device.Info.MaxComputeUnits * gridMult,
                        SharedMemoryBytes = (int)(device.Info.MaxSharedMemoryPerBlock / 2),
                        PreferredBackend = device.Info.DeviceType
                    });
                }
            }
        }

        return configs;
    }

    private double CalculateDeviceScore(long workloadSize, IAccelerator device)
    {
        // Base score from compute capability
        double score = device.Info.MaxComputeUnits * device.Info.MaxThreadsPerBlock;

        // Adjust for workload size
        if (workloadSize < 10000 && device.Info.DeviceType != "CPU")
        {
            // Small workload penalty for GPU (transfer overhead)
            score *= 0.1;
        }
        else if (workloadSize > 1000000 && device.Info.DeviceType != "CPU")
        {
            // Large workload bonus for GPU
            score *= 2.0;
        }

        // Memory bandwidth consideration
        if (device.Info.TotalMemory > 0)
        {
            score *= Math.Log10(device.Info.TotalMemory / (1024.0 * 1024.0 * 1024.0) + 1);
        }

        return score;
    }

    private double CalculateSpeedup(TuningSession session)
    {
        if (session.Measurements.Count < 2)
        {
            return 0;
        }

        // Compare first (usually default) to best configuration
        var first = session.Measurements.First();
        var best = session.Measurements.OrderBy(m => m.ExecutionTime).First();

        if (best.ExecutionTime.TotalSeconds > 0)
        {
            return first.ExecutionTime / best.ExecutionTime - 1.0;
        }

        return 0;
    }

    private static string GetCacheKey(string kernelId, long workloadSize, IAccelerator device)
        => $"{kernelId}:{workloadSize}:{device.Info.Id}";

    private static string GetCacheKey(string kernelId, long workloadSize, string deviceId)
        => $"{kernelId}:{workloadSize}:{deviceId}";

    private static int RoundToPowerOfTwo(int value)
    {
        if (value <= 0)
        {
            return 1;
        }

        var power = (int)Math.Ceiling(Math.Log2(value));
        return 1 << power;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _activeSessions.Clear();
        _tuningLock.Dispose();

        _logger.LogInfoMessage(
            $"Kernel auto-tuner disposed: {_profiles.Count} profiles cached");

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Launch configuration for kernel execution.
/// </summary>
public sealed class LaunchConfiguration
{
    /// <summary>Gets the block/thread group size.</summary>
    public int BlockSize { get; init; }

    /// <summary>Gets the grid/work group size.</summary>
    public int GridSize { get; init; }

    /// <summary>Gets the shared memory allocation in bytes.</summary>
    public int SharedMemoryBytes { get; init; }

    /// <summary>Gets the preferred backend type.</summary>
    public string? PreferredBackend { get; init; }

    /// <summary>Gets the 3D block dimensions (if different from 1D).</summary>
    public (int X, int Y, int Z)? BlockDim { get; init; }

    /// <summary>Gets the 3D grid dimensions (if different from 1D).</summary>
    public (int X, int Y, int Z)? GridDim { get; init; }
}

/// <summary>
/// Stored tuning profile for a kernel configuration.
/// </summary>
public sealed class KernelTuningProfile
{
    /// <summary>Gets the kernel identifier.</summary>
    public required string KernelId { get; init; }

    /// <summary>Gets the device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the workload size this profile was tuned for.</summary>
    public long WorkloadSize { get; init; }

    /// <summary>Gets the optimal block size.</summary>
    public int OptimalBlockSize { get; init; }

    /// <summary>Gets the optimal grid size.</summary>
    public int OptimalGridSize { get; init; }

    /// <summary>Gets the optimal shared memory allocation.</summary>
    public int OptimalSharedMemoryBytes { get; init; }

    /// <summary>Gets the best execution time achieved.</summary>
    public TimeSpan BestExecutionTime { get; init; }

    /// <summary>Gets the best throughput achieved (items/second).</summary>
    public double BestThroughput { get; init; }

    /// <summary>Gets the number of tuning iterations.</summary>
    public int TuningIterations { get; init; }

    /// <summary>Gets when this profile was last tuned.</summary>
    public DateTimeOffset LastTuned { get; init; }

    /// <summary>Gets whether this profile is valid.</summary>
    public bool IsValid => OptimalBlockSize > 0 && LastTuned > DateTimeOffset.MinValue;

    /// <summary>Converts to a launch configuration.</summary>
    public LaunchConfiguration ToLaunchConfiguration() => new()
    {
        BlockSize = OptimalBlockSize,
        GridSize = OptimalGridSize,
        SharedMemoryBytes = OptimalSharedMemoryBytes
    };
}

/// <summary>
/// Active tuning session.
/// </summary>
public sealed class TuningSession
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the kernel being tuned.</summary>
    public required string KernelId { get; init; }

    /// <summary>Gets the target device.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the workload sizes being tuned.</summary>
    public required List<long> WorkloadSizes { get; init; }

    /// <summary>Gets the tuning status.</summary>
    public TuningStatus Status { get; set; }

    /// <summary>Gets the session start time.</summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>Gets the session end time.</summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>Gets the configurations to test.</summary>
    public List<LaunchConfiguration> ConfigurationsToTest { get; init; } = [];

    /// <summary>Gets the measurements collected.</summary>
    public List<TuningMeasurement> Measurements { get; } = [];
}

/// <summary>
/// Single tuning measurement.
/// </summary>
public sealed class TuningMeasurement
{
    /// <summary>Gets the configuration that was tested.</summary>
    public required LaunchConfiguration Configuration { get; init; }

    /// <summary>Gets the workload size.</summary>
    public long WorkloadSize { get; init; }

    /// <summary>Gets the measured execution time.</summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>Gets the throughput (items/second).</summary>
    public double Throughput { get; init; }

    /// <summary>Gets the measurement timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Tuning session status.
/// </summary>
public enum TuningStatus
{
    /// <summary>Session is pending.</summary>
    Pending,

    /// <summary>Session is running.</summary>
    Running,

    /// <summary>Session completed successfully.</summary>
    Completed,

    /// <summary>Session failed.</summary>
    Failed,

    /// <summary>Session was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Result of a tuning session.
/// </summary>
public sealed class TuningResult
{
    /// <summary>Gets whether tuning was successful.</summary>
    public bool IsSuccessful { get; init; }

    /// <summary>Gets the error message if tuning failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the session identifier.</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the kernel identifier.</summary>
    public string? KernelId { get; init; }

    /// <summary>Gets the total measurements collected.</summary>
    public int TotalMeasurements { get; init; }

    /// <summary>Gets the best configurations for each workload size.</summary>
    public Dictionary<long, LaunchConfiguration> BestConfigurations { get; init; } = [];

    /// <summary>Gets the total tuning duration.</summary>
    public TimeSpan TuningDuration { get; init; }

    /// <summary>Gets the speedup achieved (ratio of improvement).</summary>
    public double SpeedupAchieved { get; init; }
}

/// <summary>
/// Backend selection recommendation.
/// </summary>
public sealed class BackendRecommendation
{
    /// <summary>Gets whether recommendation was successful.</summary>
    public bool IsSuccessful { get; init; }

    /// <summary>Gets the error message if recommendation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the recommended device.</summary>
    public IAccelerator? RecommendedDevice { get; init; }

    /// <summary>Gets the confidence score (0-1).</summary>
    public double ConfidenceScore { get; init; }

    /// <summary>Gets the reason for the recommendation.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets alternative devices in order of preference.</summary>
    public IAccelerator[]? AlternativeDevices { get; init; }
}

/// <summary>
/// Tuning statistics.
/// </summary>
public sealed class TuningStatistics
{
    /// <summary>Gets the total number of cached profiles.</summary>
    public int TotalProfiles { get; init; }

    /// <summary>Gets the number of active tuning sessions.</summary>
    public int ActiveSessions { get; init; }

    /// <summary>Gets the average speedup achieved across all profiles.</summary>
    public double AverageSpeedup { get; init; }

    /// <summary>Gets the most frequently tuned kernels.</summary>
    public Dictionary<string, int> MostTunedKernels { get; init; } = [];
}

/// <summary>
/// Options for kernel auto-tuning.
/// </summary>
public sealed class TuningOptions
{
    /// <summary>Gets or sets the minimum measurements before saving a profile.</summary>
    public int MinimumMeasurements { get; set; } = 5;

    /// <summary>Gets or sets the maximum tuning iterations per session.</summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>Gets or sets whether to enable adaptive tuning.</summary>
    public bool EnableAdaptiveTuning { get; set; } = true;

    /// <summary>Gets or sets the profile expiration time.</summary>
    public TimeSpan ProfileExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets or sets the warmup iterations before measuring.</summary>
    public int WarmupIterations { get; set; } = 3;

    /// <summary>Gets or sets whether to persist profiles to disk.</summary>
    public bool PersistProfiles { get; set; } = true;
}
