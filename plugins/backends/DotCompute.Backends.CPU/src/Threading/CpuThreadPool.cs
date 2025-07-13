// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.CPU.Threading;

/// <summary>
/// Configuration options for the CPU thread pool.
/// </summary>
public sealed class CpuThreadPoolOptions
{
    /// <summary>
    /// Gets or sets the number of worker threads. 
    /// If null, defaults to Environment.ProcessorCount.
    /// </summary>
    public int? WorkerThreads { get; set; }

    /// <summary>
    /// Gets or sets whether to use thread affinity for better cache locality.
    /// </summary>
    public bool UseThreadAffinity { get; set; } = true;

    /// <summary>
    /// Gets or sets the thread priority for worker threads.
    /// </summary>
    public ThreadPriority ThreadPriority { get; set; } = ThreadPriority.Normal;

    /// <summary>
    /// Gets or sets the maximum number of queued work items.
    /// </summary>
    public int MaxQueuedItems { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets whether to use work stealing for load balancing.
    /// </summary>
    public bool EnableWorkStealing { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of steal attempts per thread.
    /// </summary>
    public int MaxStealAttempts { get; set; } = 4;

    /// <summary>
    /// Gets or sets the work stealing back-off delay in milliseconds.
    /// </summary>
    public int WorkStealingBackoffMs { get; set; } = 1;
}

/// <summary>
/// High-performance thread pool optimized for CPU compute workloads with work-stealing support.
/// </summary>
public sealed class CpuThreadPool : IAsyncDisposable
{
    private readonly CpuThreadPoolOptions _options;
    private readonly Channel<WorkItem> _globalWorkQueue;
    private readonly ConcurrentQueue<WorkItem>[] _localWorkQueues;
    private readonly Thread[] _threads;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly TaskCompletionSource _shutdownTcs;
    private readonly Random _random = new();
    private int _disposed;

    public CpuThreadPool(IOptions<CpuThreadPoolOptions> options)
    {
        _options = options.Value;
        
        var threadCount = _options.WorkerThreads ?? Environment.ProcessorCount;
        _threads = new Thread[threadCount];
        _shutdownCts = new CancellationTokenSource();
        _shutdownTcs = new TaskCompletionSource();
        // Random initialized inline

        var channelOptions = new BoundedChannelOptions(_options.MaxQueuedItems)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _globalWorkQueue = Channel.CreateBounded<WorkItem>(channelOptions);
        
        // Initialize local work queues for work-stealing
        _localWorkQueues = new ConcurrentQueue<WorkItem>[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            _localWorkQueues[i] = new ConcurrentQueue<WorkItem>();
        }

        // Start worker threads
        for (int i = 0; i < threadCount; i++)
        {
            var thread = new Thread(WorkerThreadProc)
            {
                Name = $"DotCompute-CPU-Worker-{i}",
                Priority = _options.ThreadPriority,
                IsBackground = false
            };

            _threads[i] = thread;
            thread.Start(i);
        }
    }

    /// <summary>
    /// Gets the number of worker threads.
    /// </summary>
    public int WorkerCount => _threads.Length;

    /// <summary>
    /// Enqueues work to be executed on the thread pool.
    /// </summary>
    public ValueTask EnqueueAsync(Action work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuThreadPool));

        var workItem = new WorkItem(work, cancellationToken);
        
        // Use work-stealing: try to enqueue to a less busy local queue first
        if (_options.EnableWorkStealing)
        {
            var targetThread = FindLeastBusyThread();
            if (targetThread >= 0)
            {
                _localWorkQueues[targetThread].Enqueue(workItem);
                return ValueTask.CompletedTask;
            }
        }
        
        // Fall back to global queue
        return _globalWorkQueue.Writer.WriteAsync(workItem, cancellationToken);
    }

    /// <summary>
    /// Enqueues work and returns a task that completes when the work is done.
    /// </summary>
    public async ValueTask<T> EnqueueAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuThreadPool));

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        void WorkWrapper()
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                var result = work();
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException ex)
            {
                tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        await EnqueueAsync(WorkWrapper, cancellationToken).ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Enqueues multiple work items for batch processing.
    /// </summary>
    public async ValueTask EnqueueBatchAsync(IEnumerable<Action> workItems, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuThreadPool));

        var tasks = new List<ValueTask>();
        
        foreach (var work in workItems)
        {
            tasks.Add(EnqueueAsync(work, cancellationToken));
        }

        foreach (var task in tasks)
        {
            await task.ConfigureAwait(false);
        }
    }

    private void WorkerThreadProc(object? state)
    {
        var threadIndex = (int)state!;
        
        // Set thread affinity if requested
        if (_options.UseThreadAffinity)
        {
            SetThreadAffinity(threadIndex);
        }

        var globalReader = _globalWorkQueue.Reader;
        var localQueue = _localWorkQueues[threadIndex];
        var cancellationToken = _shutdownCts.Token;
        var consecutiveStealFailures = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool hasWork = false;
                
                // 1. Try local queue first (no contention)
                if (localQueue.TryDequeue(out WorkItem workItem))
                {
                    hasWork = true;
                    consecutiveStealFailures = 0;
                }
                // 2. Try global queue
                else if (globalReader.TryRead(out workItem))
                {
                    hasWork = true;
                    consecutiveStealFailures = 0;
                }
                // 3. Try work stealing from other threads
                else if (_options.EnableWorkStealing && TryStealWork(threadIndex, out workItem))
                {
                    hasWork = true;
                    consecutiveStealFailures = 0;
                }
                else
                {
                    consecutiveStealFailures++;
                }
                
                if (hasWork)
                {
                    ExecuteWorkItem(workItem);
                    continue;
                }

                // Implement exponential backoff for work stealing
                if (consecutiveStealFailures > 0)
                {
                    var backoffMs = Math.Min(_options.WorkStealingBackoffMs * consecutiveStealFailures, 100);
                    Thread.Sleep(backoffMs);
                }

                // No work available, wait for new work
                try
                {
                    // Use timeout to avoid blocking forever
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(100);
                        
                        var waitTask = globalReader.WaitToReadAsync(cts.Token).AsTask();
                        try
                        {
                            // ConfigureAwait(false) and GetAwaiter().GetResult() to avoid deadlock
                            var canRead = waitTask.ConfigureAwait(false).GetAwaiter().GetResult();
                            if (canRead && globalReader.TryRead(out workItem))
                            {
                                ExecuteWorkItem(workItem);
                                consecutiveStealFailures = 0;
                            }
                        }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                        {
                            // Timeout - continue loop
                            continue;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        finally
        {
            // Process any remaining work items during shutdown
            while (localQueue.TryDequeue(out var workItem))
            {
                if (!workItem.CancellationToken.IsCancellationRequested)
                {
                    ExecuteWorkItem(workItem);
                }
            }
            
            while (globalReader.TryRead(out var workItem))
            {
                if (!workItem.CancellationToken.IsCancellationRequested)
                {
                    ExecuteWorkItem(workItem);
                }
            }
        }
    }

    private static void ExecuteWorkItem(WorkItem workItem)
    {
        if (workItem.CancellationToken.IsCancellationRequested)
            return;

        try
        {
            workItem.Work();
        }
        catch
        {
            // Work items should handle their own exceptions
            // We don't want to crash the worker thread
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindLeastBusyThread()
    {
        var minCount = int.MaxValue;
        var bestThread = -1;
        
        for (int i = 0; i < _localWorkQueues.Length; i++)
        {
            var count = _localWorkQueues[i].Count;
            if (count < minCount)
            {
                minCount = count;
                bestThread = i;
                
                // If we find an empty queue, use it immediately
                if (count == 0)
                {
                    break;
                }
            }
        }
        
        return bestThread;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryStealWork(int currentThread, out WorkItem workItem)
    {
        // Try to steal work from a random other thread
        // This reduces contention compared to checking all threads in order
        var attempts = Math.Min(_options.MaxStealAttempts, _localWorkQueues.Length - 1);
        
        for (int i = 0; i < attempts; i++)
        {
            var targetThread = _random.Next(_localWorkQueues.Length);
            if (targetThread == currentThread)
            {
                continue;
            }
                
            if (_localWorkQueues[targetThread].TryDequeue(out workItem))
            {
                return true;
            }
        }
        
        workItem = default;
        return false;
    }
    
    private void SetThreadAffinity(int threadIndex)
    {
        try
        {
            // Set thread priority as a hint for better scheduling
            Thread.CurrentThread.Priority = _options.ThreadPriority;
            
            // Get NUMA topology for intelligent affinity assignment
            var topology = NumaInfo.Topology;
            var affinityInfo = CalculateOptimalAffinity(threadIndex, topology);
            
            // Apply NUMA-aware thread affinity
            if (OperatingSystem.IsWindows())
            {
                SetThreadAffinityWindows(affinityInfo);
            }
            else if (OperatingSystem.IsLinux())
            {
                SetThreadAffinityLinux(affinityInfo);
            }
        }
        catch
        {
            // Ignore affinity setting errors - not critical for functionality
        }
    }
    
    private NumaThreadAffinityInfo CalculateOptimalAffinity(int threadIndex, NumaTopology topology)
    {
        // Distribute threads across NUMA nodes for optimal performance
        var totalThreads = _threads.Length;
        var threadsPerNode = Math.Max(1, totalThreads / topology.NodeCount);
        var targetNodeId = threadIndex / threadsPerNode;
        
        // Ensure we don't exceed available nodes
        if (targetNodeId >= topology.NodeCount)
        {
            targetNodeId = threadIndex % topology.NodeCount;
        }
        
        var targetNode = topology.Nodes[targetNodeId];
        var cpusInNode = targetNode.GetCpuList().ToArray();
        
        // Select specific CPU within the node
        var cpuIndex = threadIndex % Math.Max(1, cpusInNode.Length);
        var targetCpu = cpusInNode.Length > 0 ? cpusInNode[cpuIndex] : threadIndex % Environment.ProcessorCount;
        
        return new NumaThreadAffinityInfo
        {
            ThreadIndex = threadIndex,
            NodeId = targetNodeId,
            CpuId = targetCpu,
            ProcessorMask = 1UL << targetCpu,
            Group = targetNode.Group
        };
    }
    
    private void SetThreadAffinityWindows(NumaThreadAffinityInfo affinityInfo)
    {
        try
        {
            // Use SetThreadAffinityMask for Windows
            var mask = new UIntPtr(affinityInfo.ProcessorMask);
            SetThreadAffinityMask(GetCurrentThread(), mask);
            
            // Set ideal processor for additional scheduling hints
            SetThreadIdealProcessor(GetCurrentThread(), (uint)affinityInfo.CpuId);
        }
        catch
        {
            // Fallback: just set priority
        }
    }
    
    private void SetThreadAffinityLinux(NumaThreadAffinityInfo affinityInfo)
    {
        try
        {
            // Use pthread_setaffinity_np for Linux
            var cpuSet = new cpu_set_t();
            CPU_ZERO(ref cpuSet);
            CPU_SET(affinityInfo.CpuId, ref cpuSet);
            
            pthread_setaffinity_np(pthread_self(), Marshal.SizeOf<cpu_set_t>(), ref cpuSet);
        }
        catch
        {
            // Fallback: attempt to use taskset-style affinity if available
            try
            {
                var mask = affinityInfo.ProcessorMask;
                sched_setaffinity(0, IntPtr.Size, ref mask);
            }
            catch
            {
                // Give up on affinity setting
            }
        }
    }
    
    #region Windows Thread Affinity API
    
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();
    
    [DllImport("kernel32.dll")]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);
    
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadIdealProcessor(IntPtr hThread, uint dwIdealProcessor);
    
    #endregion
    
    #region Linux Thread Affinity API
    
    [StructLayout(LayoutKind.Sequential)]
    private struct cpu_set_t
    {
        private const int CPU_SETSIZE = 1024;
        private const int NCPUBITS = 8 * sizeof(ulong);
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CPU_SETSIZE / NCPUBITS)]
        public ulong[] __bits;
        
        public cpu_set_t()
        {
            __bits = new ulong[CPU_SETSIZE / NCPUBITS];
        }
    }
    
    private static void CPU_ZERO(ref cpu_set_t set)
    {
        set.__bits = new ulong[set.__bits.Length];
    }
    
    private static void CPU_SET(int cpu, ref cpu_set_t set)
    {
        if (cpu >= 0 && cpu < 1024)
        {
            int index = cpu / 64;
            int bit = cpu % 64;
            if (index < set.__bits.Length)
            {
                set.__bits[index] |= 1UL << bit;
            }
        }
    }
    
    [DllImport("pthread", EntryPoint = "pthread_self")]
    private static extern IntPtr pthread_self();
    
    [DllImport("pthread", EntryPoint = "pthread_setaffinity_np")]
    private static extern int pthread_setaffinity_np(IntPtr thread, int cpusetsize, ref cpu_set_t cpuset);
    
    [DllImport("c", EntryPoint = "sched_setaffinity")]
    private static extern int sched_setaffinity(int pid, int cpusetsize, ref ulong mask);
    
    #endregion
    
    /// <summary>
    /// Gets statistics about the thread pool.
    /// </summary>
    public ThreadPoolStatistics GetStatistics()
    {
        var globalQueueCount = 0;
        try
        {
            // This is approximate since Channel doesn't expose count
            globalQueueCount = _globalWorkQueue.Reader.TryPeek(out _) ? 1 : 0;
        }
        catch
        {
            // Ignore errors
        }
        
        var localQueueCounts = new int[_localWorkQueues.Length];
        for (int i = 0; i < _localWorkQueues.Length; i++)
        {
            localQueueCounts[i] = _localWorkQueues[i].Count;
        }
        
        return new ThreadPoolStatistics
        {
            ThreadCount = _threads.Length,
            GlobalQueueCount = globalQueueCount,
            LocalQueueCounts = localQueueCounts,
            TotalQueuedItems = globalQueueCount + localQueueCounts.Sum()
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Signal shutdown
        _shutdownCts.Cancel();
        _globalWorkQueue.Writer.TryComplete();

        // Wait for all threads to complete
        await Task.Run(() =>
        {
            foreach (var thread in _threads)
            {
                thread.Join(TimeSpan.FromSeconds(5));
            }
        }).ConfigureAwait(false);

        _shutdownCts.Dispose();
        _shutdownTcs.SetResult();
    }
    
    private readonly struct WorkItem
    {
        public readonly Action Work;
        public readonly CancellationToken CancellationToken;

        public WorkItem(Action work, CancellationToken cancellationToken)
        {
            Work = work;
            CancellationToken = cancellationToken;
        }
    }
}

/// <summary>
/// Thread pool statistics.
/// </summary>
public sealed class ThreadPoolStatistics
{
    public required int ThreadCount { get; init; }
    public required int GlobalQueueCount { get; init; }
    public required int[] LocalQueueCounts { get; init; }
    public required int TotalQueuedItems { get; init; }
    
    public double AverageLocalQueueSize => LocalQueueCounts.Length > 0 ? LocalQueueCounts.Average() : 0;
    public int MaxLocalQueueSize => LocalQueueCounts.Length > 0 ? LocalQueueCounts.Max() : 0;
    public int MinLocalQueueSize => LocalQueueCounts.Length > 0 ? LocalQueueCounts.Min() : 0;
}

/// <summary>
/// Information about NUMA-aware thread affinity assignment.
/// </summary>
internal sealed class NumaThreadAffinityInfo
{
    /// <summary>
    /// Gets the thread index.
    /// </summary>
    public required int ThreadIndex { get; init; }
    
    /// <summary>
    /// Gets the NUMA node ID this thread is assigned to.
    /// </summary>
    public required int NodeId { get; init; }
    
    /// <summary>
    /// Gets the specific CPU ID this thread is assigned to.
    /// </summary>
    public required int CpuId { get; init; }
    
    /// <summary>
    /// Gets the processor affinity mask.
    /// </summary>
    public required ulong ProcessorMask { get; init; }
    
    /// <summary>
    /// Gets the processor group (Windows only).
    /// </summary>
    public required ushort Group { get; init; }
}