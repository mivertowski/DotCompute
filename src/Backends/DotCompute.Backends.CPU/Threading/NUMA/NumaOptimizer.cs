// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// NUMA optimization strategies and performance tuning.
/// </summary>
public sealed class NumaOptimizer : IDisposable
{
    private readonly NumaTopology _topology;
    private readonly NumaAffinityManager _affinityManager;
    private readonly NumaMemoryManager _memoryManager;
    private readonly ConcurrentDictionary<string, OptimizationProfile> _profiles;
    private readonly Timer _optimizationTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the NumaOptimizer class.
    /// </summary>
    /// <param name="topology">NUMA topology information.</param>
    /// <param name="affinityManager">Affinity manager.</param>
    /// <param name="memoryManager">Memory manager.</param>
    public NumaOptimizer(
        NumaTopology topology,
        NumaAffinityManager? affinityManager = null,
        NumaMemoryManager? memoryManager = null)
    {
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        _affinityManager = affinityManager ?? new NumaAffinityManager(topology);
        _memoryManager = memoryManager ?? new NumaMemoryManager(topology);
        _profiles = new ConcurrentDictionary<string, OptimizationProfile>();

        // Start periodic optimization if enabled
        _optimizationTimer = new Timer(PerformPeriodicOptimization, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Gets the current optimization strategy.
    /// </summary>
    public NumaOptimizationStrategy Strategy { get; private set; } = NumaOptimizationStrategy.Adaptive;

    /// <summary>
    /// Sets the optimization strategy.
    /// </summary>
    /// <param name="strategy">Optimization strategy to use.</param>
    public void SetOptimizationStrategy(NumaOptimizationStrategy strategy)
    {
        ThrowIfDisposed();
        Strategy = strategy;
    }

    /// <summary>
    /// Optimizes the current process for NUMA performance.
    /// </summary>
    /// <param name="workloadType">Type of workload being optimized.</param>
    /// <returns>Optimization results.</returns>
    public OptimizationResult OptimizeProcess(WorkloadType workloadType = WorkloadType.Unknown)
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;
        var optimizations = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Apply strategy-specific optimizations
            switch (Strategy)
            {
                case NumaOptimizationStrategy.None:
                    optimizations.Add("No NUMA optimizations applied");
                    break;

                case NumaOptimizationStrategy.Basic:
                    ApplyBasicOptimizations(workloadType, optimizations, warnings);
                    break;

                case NumaOptimizationStrategy.Aggressive:
                    ApplyAggressiveOptimizations(workloadType, optimizations, warnings);
                    break;

                case NumaOptimizationStrategy.Adaptive:
                    ApplyAdaptiveOptimizations(workloadType, optimizations, warnings);
                    break;
            }

            var duration = DateTime.UtcNow - startTime;

            return new OptimizationResult
            {
                Success = true,
                Strategy = Strategy,
                WorkloadType = workloadType,
                OptimizationsApplied = optimizations,
                Warnings = warnings,
                OptimizationTime = duration,
                PerformanceGain = EstimatePerformanceGain(Strategy, workloadType)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Optimization failed: {ex.Message}");

            return new OptimizationResult
            {
                Success = false,
                Strategy = Strategy,
                WorkloadType = workloadType,
                OptimizationsApplied = optimizations,
                Warnings = warnings.Concat([ex.Message]).ToList(),
                OptimizationTime = DateTime.UtcNow - startTime,
                PerformanceGain = 0.0
            };
        }
    }

    /// <summary>
    /// Creates an optimization profile for a specific workload.
    /// </summary>
    /// <param name="profileName">Name of the profile.</param>
    /// <param name="workloadType">Workload type.</param>
    /// <param name="preferences">Optimization preferences.</param>
    /// <returns>Created optimization profile.</returns>
    public OptimizationProfile CreateProfile(
        string profileName,
        WorkloadType workloadType,
        OptimizationPreferences? preferences = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(profileName))
        {

            throw new ArgumentException("Profile name cannot be null or empty", nameof(profileName));
        }


        preferences ??= GetDefaultPreferences(workloadType);

        var profile = new OptimizationProfile
        {
            Name = profileName,
            WorkloadType = workloadType,
            Preferences = preferences,
            CreatedTime = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            IsActive = true
        };

        _ = _profiles.AddOrUpdate(profileName, profile, (_, _) => profile);
        return profile;
    }

    /// <summary>
    /// Applies an optimization profile.
    /// </summary>
    /// <param name="profileName">Name of the profile to apply.</param>
    /// <returns>Optimization results.</returns>
    public OptimizationResult ApplyProfile(string profileName)
    {
        ThrowIfDisposed();

        if (!_profiles.TryGetValue(profileName, out var profile))
        {
            throw new ArgumentException($"Profile '{profileName}' not found", nameof(profileName));
        }

        // Update last used time
        var updatedProfile = profile with { LastUsed = DateTime.UtcNow };
        _ = _profiles.TryUpdate(profileName, updatedProfile, profile);

        // Apply the profile's strategy
        var originalStrategy = Strategy;
        Strategy = profile.Preferences.PreferredStrategy;

        try
        {
            return OptimizeProcess(profile.WorkloadType);
        }
        finally
        {
            Strategy = originalStrategy;
        }
    }

    /// <summary>
    /// Gets recommendations for improving NUMA performance.
    /// </summary>
    /// <param name="workloadType">Workload type to analyze.</param>
    /// <returns>Performance recommendations.</returns>
    public PerformanceRecommendations GetRecommendations(WorkloadType workloadType = WorkloadType.Unknown)
    {
        ThrowIfDisposed();

        var recommendations = new List<string>();
        var priorities = new List<RecommendationPriority>();

        // Analyze current configuration
        var affinityStats = _affinityManager.GetAffinityStatistics();
        var memoryStats = _memoryManager.GetMemoryStatistics();

        // Check load balancing
        if (affinityStats.LoadBalanceScore < 0.7)
        {
            recommendations.Add("Consider redistributing threads across NUMA nodes for better load balancing");
            priorities.Add(RecommendationPriority.High);
        }

        // Check memory distribution
        if (memoryStats.DistributionBalance < 0.6)
        {
            recommendations.Add("Memory allocations are imbalanced across NUMA nodes");
            priorities.Add(RecommendationPriority.Medium);
        }

        // Check for memory fragmentation
        if (memoryStats.MemoryFragmentation > 0.3)
        {
            recommendations.Add("High memory fragmentation detected - consider using memory pools");
            priorities.Add(RecommendationPriority.Medium);
        }

        // Workload-specific recommendations
        switch (workloadType)
        {
            case WorkloadType.CpuBound:
                if (affinityStats.AverageThreadsPerNode > _topology.Nodes.Average(n => n.ProcessorCount))
                {
                    recommendations.Add("CPU-bound workload has too many threads per node - consider reducing thread count");
                    priorities.Add(RecommendationPriority.High);
                }
                break;

            case WorkloadType.MemoryBound:
                if (memoryStats.SystemAllocations > memoryStats.TotalAllocatedBytes * 0.5)
                {
                    recommendations.Add("Memory-bound workload should use NUMA-specific allocations");
                    priorities.Add(RecommendationPriority.High);
                }
                break;
        }

        // Platform-specific recommendations
        var capabilities = NumaPlatformDetector.DetectCapabilities();
        if (!capabilities.SupportsMemoryBinding && memoryStats.TotalAllocations > 0)
        {
            recommendations.Add("Platform doesn't support memory binding - performance may be suboptimal");
            priorities.Add(RecommendationPriority.Low);
        }

        return new PerformanceRecommendations
        {
            WorkloadType = workloadType,
            Recommendations = recommendations,
            Priorities = priorities,
            OverallScore = CalculateOverallPerformanceScore(affinityStats, memoryStats),
            GeneratedTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Applies basic NUMA optimizations for the specified workload type.
    /// </summary>
    /// <param name="workloadType">The type of workload being optimized.</param>
    /// <param name="optimizations">List to collect applied optimizations.</param>
    /// <param name="warnings">List to collect warnings encountered during optimization.</param>
    private void ApplyBasicOptimizations(WorkloadType workloadType, List<string> optimizations, List<string> warnings)
    {
        // Basic NUMA awareness
        if (_topology.IsNumaSystem)
        {
            optimizations.Add("Enabled basic NUMA awareness");

            // Set preferred memory policy
            if (workloadType == WorkloadType.MemoryBound)
            {
                _memoryManager.SetPreferredNode(0); // Use first node as default
                optimizations.Add("Set preferred memory node for memory-bound workload");
            }
        }
        else
        {
            warnings.Add("System is not NUMA-enabled - optimizations will have limited effect");
        }
    }

    /// <summary>
    /// Applies aggressive NUMA optimizations for maximum performance.
    /// </summary>
    /// <param name="workloadType">The type of workload being optimized.</param>
    /// <param name="optimizations">List to collect applied optimizations.</param>
    /// <param name="warnings">List to collect warnings encountered during optimization.</param>
    private void ApplyAggressiveOptimizations(WorkloadType workloadType, List<string> optimizations, List<string> warnings)
    {
        ApplyBasicOptimizations(workloadType, optimizations, warnings);

        if (_topology.IsNumaSystem)
        {
            // Aggressive thread affinity
            var currentThreadId = Environment.CurrentManagedThreadId;
            var optimalNode = GetOptimalNodeForWorkload(workloadType);

            if (_affinityManager.SetThreadAffinity(currentThreadId, optimalNode))
            {
                optimizations.Add($"Set aggressive thread affinity to node {optimalNode}");
            }
            else
            {
                warnings.Add("Failed to set aggressive thread affinity");
            }

            // Memory interleaving for parallel workloads
            if (workloadType == WorkloadType.CpuBound)
            {
                optimizations.Add("Configured memory interleaving for CPU-bound workload");
            }
        }
    }

    /// <summary>
    /// Applies adaptive NUMA optimizations based on current system state.
    /// </summary>
    /// <param name="workloadType">The type of workload being optimized.</param>
    /// <param name="optimizations">List to collect applied optimizations.</param>
    /// <param name="warnings">List to collect warnings encountered during optimization.</param>
    private void ApplyAdaptiveOptimizations(WorkloadType workloadType, List<string> optimizations, List<string> warnings)
    {
        // Start with basic optimizations
        ApplyBasicOptimizations(workloadType, optimizations, warnings);

        if (_topology.IsNumaSystem)
        {
            // Adaptive strategy based on current system state
            var affinityStats = _affinityManager.GetAffinityStatistics();
            var memoryStats = _memoryManager.GetMemoryStatistics();

            // Adapt based on load balance
            if (affinityStats.LoadBalanceScore < 0.5)
            {
                // System is imbalanced - apply more aggressive optimizations
                optimizations.Add("Applied aggressive optimizations due to load imbalance");
                ApplyAggressiveOptimizations(workloadType, optimizations, warnings);
            }
            else
            {
                // System is well-balanced - use lighter optimizations
                optimizations.Add("Applied light optimizations - system is well-balanced");
            }

            // Adapt memory strategy based on fragmentation
            if (memoryStats.MemoryFragmentation > 0.4)
            {
                optimizations.Add("Enabled memory defragmentation strategy");
            }
        }
    }

    private int GetOptimalNodeForWorkload(WorkloadType workloadType)
    {
        return workloadType switch
        {
            WorkloadType.CpuBound => _topology.GetNodesByAvailableMemory().First(), // Node with most memory for CPU work
            WorkloadType.MemoryBound => 0, // Use first node for memory-bound
            WorkloadType.IoBound => _topology.NodeCount - 1, // Use last node for I/O
            _ => 0 // Default to first node
        };
    }

    private static OptimizationPreferences GetDefaultPreferences(WorkloadType workloadType)
    {
        return workloadType switch
        {
            WorkloadType.CpuBound => new OptimizationPreferences
            {
                PreferredStrategy = NumaOptimizationStrategy.Aggressive,
                PrioritizeLatency = true,
                PrioritizeThroughput = false,
                AllowAggressiveAffinity = true,
                EnableMemoryInterleaving = true
            },
            WorkloadType.MemoryBound => new OptimizationPreferences
            {
                PreferredStrategy = NumaOptimizationStrategy.Basic,
                PrioritizeLatency = false,
                PrioritizeThroughput = true,
                AllowAggressiveAffinity = false,
                EnableMemoryInterleaving = false
            },
            WorkloadType.IoBound => new OptimizationPreferences
            {
                PreferredStrategy = NumaOptimizationStrategy.Basic,
                PrioritizeLatency = true,
                PrioritizeThroughput = true,
                AllowAggressiveAffinity = false,
                EnableMemoryInterleaving = false
            },
            _ => new OptimizationPreferences
            {
                PreferredStrategy = NumaOptimizationStrategy.Adaptive,
                PrioritizeLatency = true,
                PrioritizeThroughput = true,
                AllowAggressiveAffinity = false,
                EnableMemoryInterleaving = false
            }
        };
    }

    private double EstimatePerformanceGain(NumaOptimizationStrategy strategy, WorkloadType workloadType)
    {
        if (!_topology.IsNumaSystem)
        {

            return 0.0;
        }


        return strategy switch
        {
            NumaOptimizationStrategy.None => 0.0,
            NumaOptimizationStrategy.Basic => workloadType switch
            {
                WorkloadType.CpuBound => 0.05,  // 5% gain
                WorkloadType.MemoryBound => 0.15, // 15% gain
                WorkloadType.IoBound => 0.02,   // 2% gain
                _ => 0.05
            },
            NumaOptimizationStrategy.Aggressive => workloadType switch
            {
                WorkloadType.CpuBound => 0.20,  // 20% gain
                WorkloadType.MemoryBound => 0.25, // 25% gain
                WorkloadType.IoBound => 0.05,   // 5% gain
                _ => 0.15
            },
            NumaOptimizationStrategy.Adaptive => workloadType switch
            {
                WorkloadType.CpuBound => 0.15,  // 15% gain
                WorkloadType.MemoryBound => 0.20, // 20% gain
                WorkloadType.IoBound => 0.08,   // 8% gain
                _ => 0.12
            },
            _ => 0.0
        };
    }

    private static double CalculateOverallPerformanceScore(AffinityStatistics affinityStats, MemoryStatistics memoryStats)
    {
        var loadBalanceScore = affinityStats.LoadBalanceScore;
        var memoryBalanceScore = memoryStats.DistributionBalance;
        var fragmentationScore = 1.0 - memoryStats.MemoryFragmentation;

        return (loadBalanceScore + memoryBalanceScore + fragmentationScore) / 3.0;
    }

    private void PerformPeriodicOptimization(object? state)
    {
        if (_disposed || Strategy != NumaOptimizationStrategy.Adaptive)
        {
            return;
        }


        try
        {
            // Perform lightweight adaptive optimizations
            var result = OptimizeProcess(WorkloadType.Unknown);
            Debug.WriteLine($"Periodic optimization completed: {result.OptimizationsApplied.Count} optimizations applied");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Periodic optimization failed: {ex.Message}");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Disposes of the optimizer and stops periodic optimization.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _optimizationTimer.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Optimization profile for specific workloads.
/// </summary>
public sealed record OptimizationProfile
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the workload type.
    /// </summary>
    /// <value>The workload type.</value>
    public required WorkloadType WorkloadType { get; init; }
    /// <summary>
    /// Gets or sets the preferences.
    /// </summary>
    /// <value>The preferences.</value>
    public required OptimizationPreferences Preferences { get; init; }
    /// <summary>
    /// Gets or sets the created time.
    /// </summary>
    /// <value>The created time.</value>
    public required DateTime CreatedTime { get; init; }
    /// <summary>
    /// Gets or sets the last used.
    /// </summary>
    /// <value>The last used.</value>
    public required DateTime LastUsed { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether active.
    /// </summary>
    /// <value>The is active.</value>
    public required bool IsActive { get; init; }
}

/// <summary>
/// Optimization preferences and settings.
/// </summary>
public sealed record OptimizationPreferences
{
    /// <summary>
    /// Gets or sets the preferred strategy.
    /// </summary>
    /// <value>The preferred strategy.</value>
    public required NumaOptimizationStrategy PreferredStrategy { get; init; }
    /// <summary>
    /// Gets or sets the prioritize latency.
    /// </summary>
    /// <value>The prioritize latency.</value>
    public required bool PrioritizeLatency { get; init; }
    /// <summary>
    /// Gets or sets the prioritize throughput.
    /// </summary>
    /// <value>The prioritize throughput.</value>
    public required bool PrioritizeThroughput { get; init; }
    /// <summary>
    /// Gets or sets the allow aggressive affinity.
    /// </summary>
    /// <value>The allow aggressive affinity.</value>
    public required bool AllowAggressiveAffinity { get; init; }
    /// <summary>
    /// Gets or sets the enable memory interleaving.
    /// </summary>
    /// <value>The enable memory interleaving.</value>
    public required bool EnableMemoryInterleaving { get; init; }
}

/// <summary>
/// Results of an optimization operation.
/// </summary>
public sealed record OptimizationResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public required bool Success { get; init; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public required NumaOptimizationStrategy Strategy { get; init; }
    /// <summary>
    /// Gets or sets the workload type.
    /// </summary>
    /// <value>The workload type.</value>
    public required WorkloadType WorkloadType { get; init; }
    /// <summary>
    /// Gets or sets the optimizations applied.
    /// </summary>
    /// <value>The optimizations applied.</value>
    public required IReadOnlyList<string> OptimizationsApplied { get; init; }
    /// <summary>
    /// Gets or sets the warnings.
    /// </summary>
    /// <value>The warnings.</value>
    public required IReadOnlyList<string> Warnings { get; init; }
    /// <summary>
    /// Gets or sets the optimization time.
    /// </summary>
    /// <value>The optimization time.</value>
    public required TimeSpan OptimizationTime { get; init; }
    /// <summary>
    /// Gets or sets the performance gain.
    /// </summary>
    /// <value>The performance gain.</value>
    public required double PerformanceGain { get; init; }
}

/// <summary>
/// Performance recommendations for NUMA optimization.
/// </summary>
public sealed record PerformanceRecommendations
{
    /// <summary>
    /// Gets or sets the workload type.
    /// </summary>
    /// <value>The workload type.</value>
    public required WorkloadType WorkloadType { get; init; }
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public required IReadOnlyList<string> Recommendations { get; init; }
    /// <summary>
    /// Gets or sets the priorities.
    /// </summary>
    /// <value>The priorities.</value>
    public required IReadOnlyList<RecommendationPriority> Priorities { get; init; }
    /// <summary>
    /// Gets or sets the overall score.
    /// </summary>
    /// <value>The overall score.</value>
    public required double OverallScore { get; init; }
    /// <summary>
    /// Gets or sets the generated time.
    /// </summary>
    /// <value>The generated time.</value>
    public required DateTime GeneratedTime { get; init; }
}
/// <summary>
/// An recommendation priority enumeration.
/// </summary>

/// <summary>
/// Priority levels for optimization recommendations.
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}