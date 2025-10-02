// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Manages thread and process affinity for NUMA optimization.
/// </summary>
public sealed class NumaAffinityManager : IDisposable
{
    private readonly NumaTopology _topology;
    private readonly ConcurrentDictionary<int, AffinityInfo> _threadAffinities;
    private readonly ConcurrentDictionary<int, AffinityInfo> _processAffinities;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the NumaAffinityManager class.
    /// </summary>
    /// <param name="topology">NUMA topology information.</param>
    public NumaAffinityManager(NumaTopology topology)
    {
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        _threadAffinities = new ConcurrentDictionary<int, AffinityInfo>();
        _processAffinities = new ConcurrentDictionary<int, AffinityInfo>();
    }

    /// <summary>
    /// Sets thread affinity to a specific NUMA node.
    /// </summary>
    /// <param name="threadId">Thread ID (0 for current thread).</param>
    /// <param name="nodeId">NUMA node ID.</param>
    /// <returns>True if affinity was set successfully.</returns>
    public bool SetThreadAffinity(int threadId, int nodeId)
    {
        ThrowIfDisposed();

        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }

        try
        {
            var node = _topology.Nodes[nodeId];
            var threadHandle = GetThreadHandle(threadId);

            if (NumaInterop.SetThreadAffinity(threadHandle, node.ProcessorMask))
            {
                var affinityInfo = new AffinityInfo
                {
                    NodeId = nodeId,
                    ProcessorMask = node.ProcessorMask,
                    SetTime = DateTime.UtcNow,
                    IsActive = true
                };

                _ = _threadAffinities.AddOrUpdate(threadId, affinityInfo, (_, _) => affinityInfo);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set thread affinity: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Sets thread affinity to specific processors.
    /// </summary>
    /// <param name="threadId">Thread ID (0 for current thread).</param>
    /// <param name="processorMask">Processor mask.</param>
    /// <returns>True if affinity was set successfully.</returns>
    public bool SetThreadAffinityMask(int threadId, ulong processorMask)
    {
        ThrowIfDisposed();

        if (processorMask == 0)
        {
            throw new ArgumentException("Processor mask cannot be zero", nameof(processorMask));
        }

        try
        {
            var threadHandle = GetThreadHandle(threadId);

            if (NumaInterop.SetThreadAffinity(threadHandle, processorMask))
            {
                var nodeId = GetOptimalNodeForMask(processorMask);
                var affinityInfo = new AffinityInfo
                {
                    NodeId = nodeId,
                    ProcessorMask = processorMask,
                    SetTime = DateTime.UtcNow,
                    IsActive = true
                };

                _ = _threadAffinities.AddOrUpdate(threadId, affinityInfo, (_, _) => affinityInfo);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set thread affinity mask: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Sets process affinity to a specific NUMA node.
    /// </summary>
    /// <param name="processId">Process ID (0 for current process).</param>
    /// <param name="nodeId">NUMA node ID.</param>
    /// <returns>True if affinity was set successfully.</returns>
    public bool SetProcessAffinity(int processId, int nodeId)
    {
        ThrowIfDisposed();

        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }

        try
        {
            var node = _topology.Nodes[nodeId];
            var processHandle = GetProcessHandle(processId);

            if (NumaInterop.SetProcessAffinity(processHandle, node.ProcessorMask))
            {
                var affinityInfo = new AffinityInfo
                {
                    NodeId = nodeId,
                    ProcessorMask = node.ProcessorMask,
                    SetTime = DateTime.UtcNow,
                    IsActive = true
                };

                _ = _processAffinities.AddOrUpdate(processId, affinityInfo, (_, _) => affinityInfo);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set process affinity: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Runs the current thread on a specific NUMA node.
    /// </summary>
    /// <param name="nodeId">NUMA node ID.</param>
    /// <returns>True if the operation succeeded.</returns>
    public bool RunCurrentThreadOnNode(int nodeId)
    {
        ThrowIfDisposed();

        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }

        try
        {
            // First try platform-specific optimized method
            if (NumaInterop.RunOnNode(nodeId))
            {
                var affinityInfo = new AffinityInfo
                {
                    NodeId = nodeId,
                    ProcessorMask = _topology.Nodes[nodeId].ProcessorMask,
                    SetTime = DateTime.UtcNow,
                    IsActive = true
                };

                var currentThreadId = Environment.CurrentManagedThreadId;
                _ = _threadAffinities.AddOrUpdate(currentThreadId, affinityInfo, (_, _) => affinityInfo);
                return true;
            }

            // Fallback to setting thread affinity
            return SetThreadAffinity(Environment.CurrentManagedThreadId, nodeId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to run thread on node: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the current thread affinity information.
    /// </summary>
    /// <param name="threadId">Thread ID (0 for current thread).</param>
    /// <returns>Affinity information or null if not set.</returns>
    public AffinityInfo? GetThreadAffinity(int threadId)
    {
        ThrowIfDisposed();

        if (threadId == 0)
        {
            threadId = Environment.CurrentManagedThreadId;
        }

        return _threadAffinities.TryGetValue(threadId, out var affinity) ? affinity : null;
    }

    /// <summary>
    /// Gets the current process affinity information.
    /// </summary>
    /// <param name="processId">Process ID (0 for current process).</param>
    /// <returns>Affinity information or null if not set.</returns>
    public AffinityInfo? GetProcessAffinity(int processId)
    {
        ThrowIfDisposed();

        if (processId == 0)
        {
            processId = Environment.ProcessId;
        }

        return _processAffinities.TryGetValue(processId, out var affinity) ? affinity : null;
    }

    /// <summary>
    /// Clears thread affinity, allowing the thread to run on any processor.
    /// </summary>
    /// <param name="threadId">Thread ID (0 for current thread).</param>
    /// <returns>True if affinity was cleared successfully.</returns>
    public bool ClearThreadAffinity(int threadId)
    {
        ThrowIfDisposed();

        if (threadId == 0)
        {
            threadId = Environment.CurrentManagedThreadId;
        }

        try
        {
            // Set affinity to all processors
            var allProcessorsMask = (1UL << Math.Min(_topology.ProcessorCount, NumaConstants.Limits.MaxCpusInMask)) - 1;

            if (SetThreadAffinityMask(threadId, allProcessorsMask))
            {
                _ = _threadAffinities.TryRemove(threadId, out _);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear thread affinity: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Gets the optimal NUMA node for a set of processors.
    /// </summary>
    /// <param name="processorIds">Processor IDs.</param>
    /// <returns>Optimal node ID.</returns>
    public int GetOptimalNodeForProcessors(IEnumerable<int> processorIds)
    {
        ThrowIfDisposed();
        return _topology.GetOptimalNodeForProcessors(processorIds);
    }

    /// <summary>
    /// Gets the optimal NUMA node for a processor mask.
    /// </summary>
    /// <param name="processorMask">Processor mask.</param>
    /// <returns>Optimal node ID.</returns>
    public int GetOptimalNodeForMask(ulong processorMask)
    {
        ThrowIfDisposed();

        var bestNode = 0;
        var maxOverlap = 0UL;

        for (var i = 0; i < _topology.NodeCount; i++)
        {
            var overlap = processorMask & _topology.Nodes[i].ProcessorMask;
            var overlapCount = CpuUtilities.CountSetBits(overlap);

            if (overlapCount > CpuUtilities.CountSetBits(maxOverlap))
            {
                maxOverlap = overlap;
                bestNode = i;
            }
        }

        return bestNode;
    }

    /// <summary>
    /// Gets affinity statistics for monitoring.
    /// </summary>
    /// <returns>Affinity statistics.</returns>
    public AffinityStatistics GetAffinityStatistics()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var nodeDistribution = new int[_topology.NodeCount];
            var totalThreads = 0;
            var totalProcesses = 0;

            foreach (var affinity in _threadAffinities.Values.Where(a => a.IsActive))
            {
                if (affinity.NodeId < nodeDistribution.Length)
                {
                    nodeDistribution[affinity.NodeId]++;
                }
                totalThreads++;
            }

            foreach (var affinity in _processAffinities.Values.Where(a => a.IsActive))
            {
                totalProcesses++;
            }

            return new AffinityStatistics
            {
                TotalManagedThreads = totalThreads,
                TotalManagedProcesses = totalProcesses,
                NodeDistribution = nodeDistribution,
                AverageThreadsPerNode = totalThreads > 0 ? (double)totalThreads / _topology.NodeCount : 0.0,
                LoadBalanceScore = CalculateLoadBalanceScore(nodeDistribution)
            };
        }
    }

    /// <summary>
    /// Creates an affinity scope that automatically restores original affinity.
    /// </summary>
    /// <param name="nodeId">Node ID to set affinity to.</param>
    /// <returns>Disposable affinity scope.</returns>
    public IDisposable CreateAffinityScope(int nodeId)
    {
        ThrowIfDisposed();
        return new AffinityScope(this, nodeId);
    }

    private static IntPtr GetThreadHandle(int threadId)
    {
        if (threadId == 0 || threadId == Environment.CurrentManagedThreadId)
        {
            return NumaInterop.GetCurrentThread();
        }

        // For other threads, we'd need to use OpenThread on Windows
        // This is a simplified implementation
        return NumaInterop.GetCurrentThread();
    }

    private static IntPtr GetProcessHandle(int processId)
    {
        if (processId == 0 || processId == Environment.ProcessId)
        {
            return NumaInterop.GetCurrentProcess();
        }

        // For other processes, we'd need to use OpenProcess on Windows
        // This is a simplified implementation
        return NumaInterop.GetCurrentProcess();
    }

    private static double CalculateLoadBalanceScore(int[] nodeDistribution)
    {
        if (nodeDistribution.Length == 0)
        {
            return 1.0;
        }


        var total = nodeDistribution.Sum();
        if (total == 0)
        {
            return 1.0;
        }


        var expected = (double)total / nodeDistribution.Length;
        var variance = nodeDistribution.Select(count => Math.Pow(count - expected, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        // Score is inversely related to standard deviation (lower deviation = better balance)
        return Math.Max(0.0, 1.0 - (standardDeviation / expected));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NumaAffinityManager));
        }
    }

    /// <summary>
    /// Disposes of the affinity manager and clears all managed affinities.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clear all managed affinities
            foreach (var threadId in _threadAffinities.Keys.ToArray())
            {
                _ = ClearThreadAffinity(threadId);
            }

            _threadAffinities.Clear();
            _processAffinities.Clear();

            _disposed = true;
        }
    }
}

/// <summary>
/// Information about thread or process affinity.
/// </summary>
public sealed class AffinityInfo
{
    /// <summary>NUMA node ID.</summary>
    public required int NodeId { get; init; }

    /// <summary>Processor mask.</summary>
    public required ulong ProcessorMask { get; init; }

    /// <summary>Time when affinity was set.</summary>
    public required DateTime SetTime { get; init; }

    /// <summary>Whether the affinity is currently active.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Number of processors in the affinity mask.</summary>
    public int ProcessorCount => CpuUtilities.CountSetBits(ProcessorMask);

    /// <summary>Duration since affinity was set.</summary>
    public TimeSpan Duration => DateTime.UtcNow - SetTime;
}

/// <summary>
/// Statistics about affinity management.
/// </summary>
public sealed class AffinityStatistics
{
    /// <summary>Total number of managed threads.</summary>
    public required int TotalManagedThreads { get; init; }

    /// <summary>Total number of managed processes.</summary>
    public required int TotalManagedProcesses { get; init; }

    /// <summary>Distribution of threads across NUMA nodes.</summary>
    public required int[] NodeDistribution { get; init; }

    /// <summary>Average threads per node.</summary>
    public required double AverageThreadsPerNode { get; init; }

    /// <summary>Load balance score (0.0 to 1.0, higher is better).</summary>
    public required double LoadBalanceScore { get; init; }

    /// <summary>Gets the most loaded node.</summary>
    public int MostLoadedNode => NodeDistribution.Length > 0
        ? Array.IndexOf(NodeDistribution, NodeDistribution.Max())
        : 0;

    /// <summary>Gets the least loaded node.</summary>
    public int LeastLoadedNode => NodeDistribution.Length > 0
        ? Array.IndexOf(NodeDistribution, NodeDistribution.Min())
        : 0;
}

/// <summary>
/// Disposable scope that automatically restores thread affinity.
/// </summary>
internal sealed class AffinityScope : IDisposable
{
    private readonly NumaAffinityManager _manager;
    private readonly int _threadId;
    private readonly AffinityInfo? _originalAffinity;
    private bool _disposed;

    public AffinityScope(NumaAffinityManager manager, int nodeId)
    {
        _manager = manager;
        _threadId = Environment.CurrentManagedThreadId;
        _originalAffinity = manager.GetThreadAffinity(_threadId);

        // Set new affinity
        _ = manager.SetThreadAffinity(_threadId, nodeId);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Restore original affinity
            if (_originalAffinity != null)
            {
                _ = _manager.SetThreadAffinityMask(_threadId, _originalAffinity.ProcessorMask);
            }
            else
            {
                _ = _manager.ClearThreadAffinity(_threadId);
            }

            _disposed = true;
        }
    }
}