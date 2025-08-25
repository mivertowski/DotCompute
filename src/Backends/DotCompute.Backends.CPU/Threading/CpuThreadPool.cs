// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
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
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuThreadPool"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
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
        for (var i = 0; i < threadCount; i++)
        {
            _localWorkQueues[i] = new ConcurrentQueue<WorkItem>();
        }

        // Start worker threads
        for (var i = 0; i < threadCount; i++)
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

        ObjectDisposedException.ThrowIf(_disposed != 0, this);

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

        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        void WorkWrapper()
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _ = tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                var result = work();
                _ = tcs.TrySetResult(result);
            }
            catch (OperationCanceledException ex)
            {
                _ = tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                _ = tcs.TrySetException(ex);
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

        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        // Performance optimization: Convert to array once to avoid multiple enumeration
        var workArray = workItems as Action[] ?? [.. workItems];

        if (workArray.Length == 0)
        {
            return;
        }

        // Optimize for small batches - direct enqueue
        if (workArray.Length <= 4)
        {
            foreach (var work in workArray)
            {
                await EnqueueAsync(work, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        // For larger batches, distribute across local queues for better load balancing
        if (_options.EnableWorkStealing)
        {
            var itemsPerThread = Math.Max(1, workArray.Length / _localWorkQueues.Length);
            var currentIndex = 0;

            for (var threadIdx = 0; threadIdx < _localWorkQueues.Length && currentIndex < workArray.Length; threadIdx++)
            {
                var count = Math.Min(itemsPerThread, workArray.Length - currentIndex);
                if (threadIdx == _localWorkQueues.Length - 1)
                {
                    // Last thread gets all remaining items
                    count = workArray.Length - currentIndex;
                }

                for (var i = 0; i < count && currentIndex < workArray.Length; i++)
                {
                    var workItem = new WorkItem(workArray[currentIndex++], cancellationToken);
                    _localWorkQueues[threadIdx].Enqueue(workItem);
                }
            }
        }
        else
        {
            // Fall back to global queue for all items
            var writer = _globalWorkQueue.Writer;
            foreach (var work in workArray)
            {
                await writer.WriteAsync(new WorkItem(work, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
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
                var hasWork = false;

                // 1. Try local queue first (no contention)
                if (localQueue.TryDequeue(out var workItem))
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

                // Performance optimization: Use yield/spin wait for short delays
                if (consecutiveStealFailures > 0)
                {
                    if (consecutiveStealFailures <= 3)
                    {
                        // For short waits, use spin wait (keeps thread hot)
                        Thread.SpinWait(100 * consecutiveStealFailures);
                    }
                    else
                    {
                        // For longer waits, yield to other threads
                        var backoffMs = Math.Min(_options.WorkStealingBackoffMs * (consecutiveStealFailures - 3), 50);
                        if (backoffMs <= 1)
                        {
                            _ = Thread.Yield();
                        }
                        else
                        {
                            Thread.Sleep(backoffMs);
                        }
                    }
                }

                // No work available, wait for new work
                try
                {
                    // Use timeout to avoid blocking forever
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(100);

                    var waitTask = globalReader.WaitToReadAsync(cts.Token).AsTask();
                    try
                    {
                        // ConfigureAwait(false) and GetAwaiter().GetResult() to avoid deadlock
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                        var canRead = waitTask.ConfigureAwait(false).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
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
        {
            return;
        }

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
        // Performance optimization: Use round-robin with load checking
        // This reduces contention on queue count checking
        const int maxChecks = 3;
        var startIndex = (int)(((uint)Environment.TickCount * 2654435761U) >> 22) % _localWorkQueues.Length;
        var minCount = int.MaxValue;
        var bestThread = -1;

        // Check up to MaxChecks threads starting from a pseudo-random position
        for (var i = 0; i < Math.Min(maxChecks, _localWorkQueues.Length); i++)
        {
            var threadIdx = (startIndex + i) % _localWorkQueues.Length;
            var count = _localWorkQueues[threadIdx].Count;

            if (count == 0)
            {
                // Found empty queue, use immediately
                return threadIdx;
            }

            if (count < minCount)
            {
                minCount = count;
                bestThread = threadIdx;
            }
        }

        // If all checked queues are busy, just use round-robin
        return bestThread >= 0 ? bestThread : startIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryStealWork(int currentThread, out WorkItem workItem)
    {
        // Performance optimization: Use deterministic pseudo-random for better cache behavior
        var attempts = Math.Min(_options.MaxStealAttempts, _localWorkQueues.Length - 1);
        var hash = (uint)(currentThread + Environment.TickCount);

        // Try neighbors first (better cache locality)
        var leftNeighbor = (currentThread - 1 + _localWorkQueues.Length) % _localWorkQueues.Length;
        var rightNeighbor = (currentThread + 1) % _localWorkQueues.Length;

        if (_localWorkQueues[leftNeighbor].TryDequeue(out workItem))
        {
            return true;
        }

        if (_localWorkQueues[rightNeighbor].TryDequeue(out workItem))
        {
            return true;
        }

        // Then try random threads
        for (var i = 2; i < attempts; i++)
        {
            // Fast pseudo-random using multiplicative hash
            hash = hash * 1664525U + 1013904223U;
            var targetThread = (int)(hash % (uint)_localWorkQueues.Length);

            if (targetThread == currentThread ||
                targetThread == leftNeighbor ||
                targetThread == rightNeighbor)
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
        var cpusInNode = targetNode.CpuList.ToArray();

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

    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void SetThreadAffinityWindows(NumaThreadAffinityInfo affinityInfo)
    {
        try
        {
            // Use SetThreadAffinityMask for Windows
            var mask = new UIntPtr(affinityInfo.ProcessorMask);
            _ = SetThreadAffinityMask(GetCurrentThread(), mask);

            // Set ideal processor for additional scheduling hints
            _ = SetThreadIdealProcessor(GetCurrentThread(), (uint)affinityInfo.CpuId);
        }
        catch
        {
            // Fallback: just set priority
        }
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void SetThreadAffinityLinux(NumaThreadAffinityInfo affinityInfo)
    {
        try
        {
            // Use pthread_setaffinity_np for Linux
            var cpuSet = new cpu_set_t();
            CPU_ZERO(ref cpuSet);
            CPU_SET(affinityInfo.CpuId, ref cpuSet);

            var result = pthread_setaffinity_np(pthread_self(), Marshal.SizeOf<cpu_set_t>(), ref cpuSet);
            if (result != 0)
            {
                // Log or handle error if needed
            }
        }
        catch
        {
            // Fallback: attempt to use taskset-style affinity if available
            try
            {
                var mask = affinityInfo.ProcessorMask;
                var result = sched_setaffinity(0, IntPtr.Size, ref mask);
                if (result != 0)
                {
                    // Log or handle error if needed
                }
            }
            catch
            {
                // Give up on affinity setting
            }
        }
    }

    #region Windows Thread Affinity API

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint SetThreadIdealProcessor(IntPtr hThread, uint dwIdealProcessor);

    #endregion

    #region Linux Thread Affinity API

    [StructLayout(LayoutKind.Sequential)]
#pragma warning disable IDE1006 // Naming Styles
    private struct cpu_set_t
#pragma warning restore IDE1006 // Naming Styles
    {
        private const int CPU_SETSIZE = 1024;
        private const int NCPUBITS = 8 * sizeof(ulong);

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CPU_SETSIZE / NCPUBITS)]
#pragma warning disable IDE1006 // Naming Styles
        public ulong[] __bits;
#pragma warning restore IDE1006 // Naming Styles

        public cpu_set_t()
        {
            __bits = new ulong[CPU_SETSIZE / NCPUBITS];
        }
    }

    private static void CPU_ZERO(ref cpu_set_t set) => set.__bits = new ulong[set.__bits.Length];

    private static void CPU_SET(int cpu, ref cpu_set_t set)
    {
        if (cpu is >= 0 and < 1024)
        {
            var index = cpu / 64;
            var bit = cpu % 64;
            if (index < set.__bits.Length)
            {
                set.__bits[index] |= 1UL << bit;
            }
        }
    }

    [DllImport("pthread", EntryPoint = "pthread_self")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern IntPtr pthread_self();

    [DllImport("pthread", EntryPoint = "pthread_setaffinity_np")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int pthread_setaffinity_np(IntPtr thread, int cpusetsize, ref cpu_set_t cpuset);

    [DllImport("c", EntryPoint = "sched_setaffinity")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
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
        for (var i = 0; i < _localWorkQueues.Length; i++)
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

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Signal shutdown
#pragma warning disable CA1849 // Call async methods when in an async method - acceptable in DisposeAsync pattern
#pragma warning disable VSTHRD103 // Cancel synchronously blocks - acceptable in Dispose pattern
        _shutdownCts.Cancel();
#pragma warning restore VSTHRD103
#pragma warning restore CA1849
        _ = _globalWorkQueue.Writer.TryComplete();

        // Wait for all threads to complete
        await Task.Run(() =>
        {
            foreach (var thread in _threads)
            {
                _ = thread.Join(TimeSpan.FromSeconds(5));
            }
        }).ConfigureAwait(false);

        _shutdownCts.Dispose();
        _shutdownTcs.SetResult();
    }

    private readonly struct WorkItem(Action work, CancellationToken cancellationToken)
    {
        public readonly Action Work = work;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Thread pool statistics.
/// </summary>
public sealed class ThreadPoolStatistics
{
    /// <summary>
    /// Gets the thread count.
    /// </summary>
    /// <value>
    /// The thread count.
    /// </value>
    public required int ThreadCount { get; init; }

    /// <summary>
    /// Gets the global queue count.
    /// </summary>
    /// <value>
    /// The global queue count.
    /// </value>
    public required int GlobalQueueCount { get; init; }

#pragma warning disable CA1819 // Properties should not return arrays - Required for work queue statistics    
    /// <summary>
    /// Gets the local queue counts.
    /// </summary>
    /// <value>
    /// The local queue counts.
    /// </value>
    public required int[] LocalQueueCounts { get; init; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets the total queued items.
    /// </summary>
    /// <value>
    /// The total queued items.
    /// </value>
    public required int TotalQueuedItems { get; init; }

    /// <summary>
    /// Gets the average size of the local queue.
    /// </summary>
    /// <value>
    /// The average size of the local queue.
    /// </value>
    public double AverageLocalQueueSize => LocalQueueCounts.Length > 0 ? LocalQueueCounts.Average() : 0;

    /// <summary>
    /// Gets the maximum size of the local queue.
    /// </summary>
    /// <value>
    /// The maximum size of the local queue.
    /// </value>
    public int MaxLocalQueueSize => LocalQueueCounts.Length > 0 ? LocalQueueCounts.Max() : 0;

    /// <summary>
    /// Gets the minimum size of the local queue.
    /// </summary>
    /// <value>
    /// The minimum size of the local queue.
    /// </value>
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
