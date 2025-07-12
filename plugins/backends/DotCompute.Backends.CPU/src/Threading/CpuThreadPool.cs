// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
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
        // Platform-specific thread affinity implementation
        try
        {
            // Set thread priority as a hint for better scheduling
            Thread.CurrentThread.Priority = _options.ThreadPriority;
            
            // On Windows, you would use SetThreadAffinityMask
            // On Linux, you would use pthread_setaffinity_np
            // For now, we'll use processor affinity hints
            if (OperatingSystem.IsWindows())
            {
                // Windows-specific affinity would go here
                // SetThreadAffinityMask(GetCurrentThread(), (UIntPtr)(1UL << (threadIndex % Environment.ProcessorCount)));
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux-specific affinity would go here
                // pthread_setaffinity_np equivalent
            }
        }
        catch
        {
            // Ignore affinity setting errors
        }
    }
    
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