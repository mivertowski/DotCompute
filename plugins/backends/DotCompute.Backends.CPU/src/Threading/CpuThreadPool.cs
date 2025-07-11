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
}

/// <summary>
/// High-performance thread pool optimized for CPU compute workloads.
/// </summary>
public sealed class CpuThreadPool : IAsyncDisposable
{
    private readonly CpuThreadPoolOptions _options;
    private readonly Channel<WorkItem> _workQueue;
    private readonly Thread[] _threads;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly TaskCompletionSource _shutdownTcs;
    private int _disposed;

    public CpuThreadPool(IOptions<CpuThreadPoolOptions> options)
    {
        _options = options.Value;
        
        var threadCount = _options.WorkerThreads ?? Environment.ProcessorCount;
        _threads = new Thread[threadCount];
        _shutdownCts = new CancellationTokenSource();
        _shutdownTcs = new TaskCompletionSource();

        var channelOptions = new BoundedChannelOptions(_options.MaxQueuedItems)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _workQueue = Channel.CreateBounded<WorkItem>(channelOptions);

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
        return _workQueue.Writer.WriteAsync(workItem, cancellationToken);
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

    private void WorkerThreadProc(object? state)
    {
        var threadIndex = (int)state!;
        
        // Set thread affinity if requested
        if (_options.UseThreadAffinity && OperatingSystem.IsWindows())
        {
            // Note: Thread affinity is platform-specific and requires P/Invoke
            // This is a placeholder for the actual implementation
        }

        var reader = _workQueue.Reader;
        var cancellationToken = _shutdownCts.Token;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (reader.TryRead(out var workItem))
                {
                    ExecuteWorkItem(workItem);
                    continue;
                }

                // Use async wait with timeout to periodically check for shutdown
                var waitTask = reader.WaitToReadAsync(cancellationToken).AsTask();
                if (waitTask.Wait(100))
                {
                    if (waitTask.Result && reader.TryRead(out workItem))
                    {
                        ExecuteWorkItem(workItem);
                    }
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
            while (reader.TryRead(out var workItem))
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Signal shutdown
        _shutdownCts.Cancel();
        _workQueue.Writer.TryComplete();

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