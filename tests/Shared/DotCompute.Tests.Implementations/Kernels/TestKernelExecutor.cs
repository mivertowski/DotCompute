using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Shared.Kernels;

/// <summary>
/// Test kernel executor that simulates kernel execution.
/// </summary>
public class TestKernelExecutor
{
    private readonly ConcurrentQueue<KernelExecution> _executionQueue;
    private readonly ConcurrentDictionary<string, KernelStatistics> _statistics;
    private readonly SemaphoreSlim _executionSemaphore;
    private long _totalExecutions;
    private TimeSpan _totalExecutionTime;
    private bool _disposed;

    public TestKernelExecutor(int maxConcurrentExecutions = 4)
    {
        _executionQueue = new ConcurrentQueue<KernelExecution>();
        _statistics = new ConcurrentDictionary<string, KernelStatistics>();
        _executionSemaphore = new SemaphoreSlim(maxConcurrentExecutions, maxConcurrentExecutions);
    }

    public long TotalExecutions => _totalExecutions;
    public TimeSpan TotalExecutionTime => _totalExecutionTime;
    public int QueuedExecutions => _executionQueue.Count;
    public IReadOnlyDictionary<string, KernelStatistics> Statistics => _statistics;

    public async Task<KernelExecutionResult> ExecuteAsync(
        ICompiledKernel kernel,
        KernelArguments arguments,
        KernelConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        if(_disposed)
        {
            throw new ObjectDisposedException(nameof(TestKernelExecutor));
        }

        var config = configuration ?? new KernelConfiguration(new Dim3(256), new Dim3(1));
        
        var execution = new KernelExecution
        {
            Id = Guid.NewGuid(),
            KernelName = kernel.Name,
            Arguments = arguments,
            Configuration = config,
            QueuedAt = DateTime.UtcNow
        };

        _executionQueue.Enqueue(execution);

        await _executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if(!_executionQueue.TryDequeue(out var dequeuedExecution) || dequeuedExecution.Id != execution.Id)
            {
                // Find our execution in the queue(shouldn't happen in normal flow)
                _executionQueue.TryDequeue(out _);
            }

            execution.StartedAt = DateTime.UtcNow;
            var result = await ExecuteKernelAsync(execution, cancellationToken);
            execution.CompletedAt = DateTime.UtcNow;

            UpdateStatistics(kernel.Name, execution, result);
            
            return result;
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    private async Task<KernelExecutionResult> ExecuteKernelAsync(
        KernelExecution execution,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Calculate total threads
            var totalThreads = execution.Configuration.GridDimensions.X * 
                             execution.Configuration.GridDimensions.Y * 
                             execution.Configuration.GridDimensions.Z *
                             execution.Configuration.BlockDimensions.X * 
                             execution.Configuration.BlockDimensions.Y * 
                             execution.Configuration.BlockDimensions.Z;

            // Simulate kernel execution with parallel processing
            await Task.Run(() =>
            {
                var partitioner = Partitioner.Create(0, totalThreads);
                Parallel.ForEach(partitioner, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, range =>
                {
                    for(int threadId = range.Item1; threadId < range.Item2; threadId++)
                    {
                        // Simulate compute work for each thread
                        SimulateThreadExecution(threadId, execution);
                    }
                });
            }, cancellationToken);

            stopwatch.Stop();
            
            Interlocked.Increment(ref _totalExecutions);
            var elapsed = stopwatch.Elapsed;
            _totalExecutionTime = _totalExecutionTime.Add(elapsed);

            return new KernelExecutionResult
            {
                Success = true,
                ExecutionTimeMs = elapsed.TotalMilliseconds,
                ThreadsExecuted = totalThreads,
                MemoryUsed = EstimateMemoryUsage(execution.Arguments),
                Throughput = CalculateThroughput(totalThreads, elapsed)
            };
        }
        catch(Exception ex)
        {
            stopwatch.Stop();
            
            return new KernelExecutionResult
            {
                Success = false,
                ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                ErrorMessage = ex.Message,
                ErrorType = ex.GetType().Name
            };
        }
    }

    private void SimulateThreadExecution(int threadId, KernelExecution execution)
    {
        // Calculate thread coordinates
        var blockDim = execution.Configuration.BlockDimensions;
        var gridDim = execution.Configuration.GridDimensions;
        
        var threadsPerBlock = blockDim.X * blockDim.Y * blockDim.Z;
        var blockId = threadId / threadsPerBlock;
        var localThreadId = threadId % threadsPerBlock;
        
        // Simulate different workloads based on kernel name
        if(execution.KernelName.Contains("MatrixMultiply", StringComparison.OrdinalIgnoreCase))
        {
            SimulateMatrixMultiply(threadId);
        }
        else if(execution.KernelName.Contains("Reduction", StringComparison.OrdinalIgnoreCase))
        {
            SimulateReduction(threadId);
        }
        else if(execution.KernelName.Contains("Convolution", StringComparison.OrdinalIgnoreCase))
        {
            SimulateConvolution(threadId);
        }
        else
        {
            SimulateGenericCompute(threadId);
        }
    }

    private void SimulateMatrixMultiply(int threadId)
    {
        // Simulate matrix multiplication workload
        double sum = 0;
        for(int i = 0; i < 100; i++)
        {
            sum += Math.Sin(threadId * i * 0.001) * Math.Cos(threadId * i * 0.001);
        }
    }

    private void SimulateReduction(int threadId)
    {
        // Simulate reduction workload
        double value = threadId;
        for(int i = 0; i < 10; i++)
        {
            value = Math.Sqrt(value + 1);
        }
    }

    private void SimulateConvolution(int threadId)
    {
        // Simulate convolution workload
        double result = 0;
        for(int i = -2; i <= 2; i++)
        {
            for(int j = -2; j <= 2; j++)
            {
                result += Math.Exp(-(i * i + j * j) / 2.0) * threadId;
            }
        }
    }

    private void SimulateGenericCompute(int threadId)
    {
        // Generic compute simulation
        double result = threadId;
        for(int i = 0; i < 50; i++)
        {
            result = Math.Sin(result) + Math.Cos(result);
        }
    }

    private long EstimateMemoryUsage(KernelArguments arguments)
    {
        long totalSize = 0;
        
        for(int i = 0; i < arguments.Length; i++)
        {
            var arg = arguments.Arguments.ToArray()[i];
            if(arg is IMemoryBuffer buffer)
            {
                totalSize += buffer.SizeInBytes;
            }
            else if(arg is Array array)
            {
                totalSize += array.Length * 8; // Rough estimate
            }
            else
            {
                totalSize += 8; // Size of a pointer/value
            }
        }
        
        return totalSize;
    }

    private double CalculateThroughput(int threads, TimeSpan elapsed)
    {
        if(elapsed.TotalSeconds == 0)
            return 0;
            
        return threads / elapsed.TotalSeconds;
    }

    private void UpdateStatistics(string kernelName, KernelExecution execution, KernelExecutionResult result)
    {
        var stats = _statistics.AddOrUpdate(kernelName,
            k => new KernelStatistics { KernelName = k },
           (k, existing) => existing);

        stats.ExecutionCount++;
        stats.TotalExecutionTime = stats.TotalExecutionTime.Add(TimeSpan.FromMilliseconds(result.ExecutionTimeMs));
        
        if(result.Success)
        {
            stats.SuccessfulExecutions++;
            stats.TotalThreadsExecuted += result.ThreadsExecuted;
            stats.TotalMemoryUsed += result.MemoryUsed;
            
            if(result.ExecutionTimeMs < stats.MinExecutionTimeMs || stats.MinExecutionTimeMs == 0)
                stats.MinExecutionTimeMs = result.ExecutionTimeMs;
                
            if(result.ExecutionTimeMs > stats.MaxExecutionTimeMs)
                stats.MaxExecutionTimeMs = result.ExecutionTimeMs;
        }
        else
        {
            stats.FailedExecutions++;
        }
        
        stats.LastExecutionTime = execution.CompletedAt ?? DateTime.UtcNow;
    }

    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while(!_executionQueue.IsEmpty)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    public void Dispose()
    {
        if(!_disposed)
        {
            _disposed = true;
            _executionSemaphore?.Dispose();
        }
    }
}

/// <summary>
/// Represents a kernel execution.
/// </summary>
public class KernelExecution
{
    public Guid Id { get; set; }
    public string KernelName { get; set; } = "";
    public KernelArguments Arguments { get; set; }
    public KernelConfiguration Configuration { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Result of kernel execution.
/// </summary>
public class KernelExecutionResult
{
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public int ThreadsExecuted { get; set; }
    public long MemoryUsed { get; set; }
    public double Throughput { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorType { get; set; }
}

/// <summary>
/// Statistics for kernel executions.
/// </summary>
public class KernelStatistics
{
    public string KernelName { get; set; } = "";
    public long ExecutionCount { get; set; }
    public long SuccessfulExecutions { get; set; }
    public long FailedExecutions { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public double MinExecutionTimeMs { get; set; }
    public double MaxExecutionTimeMs { get; set; }
    public long TotalThreadsExecuted { get; set; }
    public long TotalMemoryUsed { get; set; }
    public DateTime LastExecutionTime { get; set; }
    
    public double AverageExecutionTimeMs => 
        ExecutionCount > 0 ? TotalExecutionTime.TotalMilliseconds / ExecutionCount : 0;
    
    public double SuccessRate => 
        ExecutionCount > 0 ?(double)SuccessfulExecutions / ExecutionCount * 100 : 0;
}
