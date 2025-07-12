// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
public class ThreadPoolBenchmarks
{
    private readonly ConcurrentQueue<int> _workItems = new();
    private readonly SemaphoreSlim _semaphore = new(Environment.ProcessorCount);
    private readonly ActionBlock<int> _actionBlock;
    private readonly TaskScheduler _scheduler = TaskScheduler.Default;
    
    [Params(100, 1000, 10000)]
    public int WorkItemCount { get; set; }

    [Params(1, 4, 8, 16)]
    public int ConcurrencyLevel { get; set; }

    public ThreadPoolBenchmarks()
    {
        _actionBlock = new ActionBlock<int>(
            item => ProcessWorkItem(item),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                BoundedCapacity = 1000
            });
    }

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < WorkItemCount; i++)
        {
            _workItems.Enqueue(i);
        }
    }

    [Benchmark(Baseline = true)]
    public async Task ThreadPoolQueueUserWorkItem()
    {
        var tasks = new List<Task>();
        var countdown = new CountdownEvent(WorkItemCount);
        
        for (int i = 0; i < WorkItemCount; i++)
        {
            var workItem = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                ProcessWorkItem(workItem);
                countdown.Signal();
            });
        }
        
        await Task.Run(() => countdown.Wait());
    }

    [Benchmark]
    public async Task TaskRun()
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < WorkItemCount; i++)
        {
            var workItem = i;
            tasks.Add(Task.Run(() => ProcessWorkItem(workItem)));
        }
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task TaskFactory()
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < WorkItemCount; i++)
        {
            var workItem = i;
            tasks.Add(Task.Factory.StartNew(() => ProcessWorkItem(workItem)));
        }
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ParallelFor()
    {
        await Task.Run(() =>
        {
            Parallel.For(0, WorkItemCount, new ParallelOptions
            {
                MaxDegreeOfParallelism = ConcurrencyLevel
            }, i => ProcessWorkItem(i));
        });
    }

    [Benchmark]
    public async Task ParallelForEach()
    {
        var items = Enumerable.Range(0, WorkItemCount);
        
        await Task.Run(() =>
        {
            Parallel.ForEach(items, new ParallelOptions
            {
                MaxDegreeOfParallelism = ConcurrencyLevel
            }, ProcessWorkItem);
        });
    }

    [Benchmark]
    public async Task SemaphoreThrottled()
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < WorkItemCount; i++)
        {
            var workItem = i;
            tasks.Add(ProcessWithSemaphore(workItem));
        }
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task DataflowPipeline()
    {
        for (int i = 0; i < WorkItemCount; i++)
        {
            await _actionBlock.SendAsync(i);
        }
        
        _actionBlock.Complete();
        await _actionBlock.Completion;
    }

    [Benchmark]
    public async Task ProducerConsumerPattern()
    {
        var channel = System.Threading.Channels.Channel.CreateBounded<int>(1000);
        var writer = channel.Writer;
        var reader = channel.Reader;
        
        // Producer
        var producer = Task.Run(async () =>
        {
            for (int i = 0; i < WorkItemCount; i++)
            {
                await writer.WriteAsync(i);
            }
            writer.Complete();
        });
        
        // Consumers
        var consumers = Enumerable.Range(0, ConcurrencyLevel)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var item in reader.ReadAllAsync())
                {
                    ProcessWorkItem(item);
                }
            }))
            .ToArray();
        
        await Task.WhenAll(producer);
        await Task.WhenAll(consumers);
    }

    [Benchmark]
    public async Task CustomThreadPoolBenchmark()
    {
        var customPool = new CustomThreadPool(ConcurrencyLevel);
        var tasks = new List<Task>();
        
        for (int i = 0; i < WorkItemCount; i++)
        {
            var workItem = i;
            tasks.Add(customPool.QueueWorkItem(() => ProcessWorkItem(workItem)));
        }
        
        await Task.WhenAll(tasks);
        customPool.Dispose();
    }

    [Benchmark]
    public async Task BatchProcessing()
    {
        const int batchSize = 100;
        var batches = new List<List<int>>();
        
        for (int i = 0; i < WorkItemCount; i += batchSize)
        {
            var batch = Enumerable.Range(i, Math.Min(batchSize, WorkItemCount - i)).ToList();
            batches.Add(batch);
        }
        
        await Task.WhenAll(batches.Select(batch => Task.Run(() =>
        {
            foreach (var item in batch)
            {
                ProcessWorkItem(item);
            }
        })));
    }

    private void ProcessWorkItem(int item)
    {
        // Simulate CPU-intensive work
        var result = 0;
        for (int i = 0; i < 1000; i++)
        {
            result += i * item;
        }
        
        // Simulate some memory allocation
        var buffer = new byte[1024];
        buffer[0] = (byte)(result % 256);
    }

    private async Task ProcessWithSemaphore(int item)
    {
        await _semaphore.WaitAsync();
        try
        {
            ProcessWorkItem(item);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private class CustomThreadPool : IDisposable
    {
        private readonly Thread[] _threads;
        private readonly ConcurrentQueue<Action> _workQueue = new();
        private readonly AutoResetEvent _workEvent = new(false);
        private volatile bool _shutdown;

        public CustomThreadPool(int threadCount)
        {
            _threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                _threads[i] = new Thread(WorkerThread)
                {
                    IsBackground = true,
                    Name = $"CustomThreadPool-{i}"
                };
                _threads[i].Start();
            }
        }

        public Task QueueWorkItem(Action workItem)
        {
            var tcs = new TaskCompletionSource<bool>();
            _workQueue.Enqueue(() =>
            {
                try
                {
                    workItem();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            _workEvent.Set();
            return tcs.Task;
        }

        private void WorkerThread()
        {
            while (!_shutdown)
            {
                _workEvent.WaitOne();
                while (_workQueue.TryDequeue(out var workItem))
                {
                    workItem();
                }
            }
        }

        public void Dispose()
        {
            _shutdown = true;
            _workEvent.Set();
            foreach (var thread in _threads)
            {
                thread.Join(1000);
            }
            _workEvent.Dispose();
        }
    }
}