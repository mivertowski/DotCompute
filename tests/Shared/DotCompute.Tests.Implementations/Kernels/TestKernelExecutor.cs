using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Implementations.Kernels;


/// <summary>
/// Test kernel executor that simulates kernel execution.
/// </summary>
public sealed class TestKernelExecutor(int maxConcurrentExecutions = 4)
{
    private readonly ConcurrentQueue<KernelExecution> _executionQueue = new();
    private readonly ConcurrentDictionary<string, KernelStatistics> _statistics = new();
    private readonly SemaphoreSlim _executionSemaphore = new(maxConcurrentExecutions, maxConcurrentExecutions);
    private long _totalExecutions;
    private TimeSpan _totalExecutionTime;
    private bool _disposed;

    /// <summary>
    /// Gets the total executions.
    /// </summary>
    /// <value>
    /// The total executions.
    /// </value>
    public long TotalExecutions => _totalExecutions;

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    /// <value>
    /// The total execution time.
    /// </value>
    public TimeSpan TotalExecutionTime => _totalExecutionTime;

    /// <summary>
    /// Gets the queued executions.
    /// </summary>
    /// <value>
    /// The queued executions.
    /// </value>
    public int QueuedExecutions => _executionQueue.Count;

    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <value>
    /// The statistics.
    /// </value>
    public IReadOnlyDictionary<string, KernelStatistics> Statistics => _statistics;

    /// <summary>
    /// Executes the asynchronous.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public async Task<KernelExecutionResult> ExecuteAsync(
        ICompiledKernel kernel,
        KernelArguments arguments,
        KernelConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
            if (!_executionQueue.TryDequeue(out var dequeuedExecution) || dequeuedExecution.Id != execution.Id)
            {
                // Find our execution in the queue(shouldn't happen in normal flow)
                _ = _executionQueue.TryDequeue(out _);
            }

            execution.StartedAt = DateTime.UtcNow;
            var result = await ExecuteKernelAsync(execution, cancellationToken);
            execution.CompletedAt = DateTime.UtcNow;

            UpdateStatistics(kernel.Name, execution, result);

            return result;
        }
        finally
        {
            _ = _executionSemaphore.Release();
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
            var gridDim = (Dim3)(execution.Configuration.Options.TryGetValue("GridDimension", out var grid) ? grid : new Dim3(1));
            var blockDim = (Dim3)(execution.Configuration.Options.TryGetValue("BlockDimension", out var block) ? block : new Dim3(256));
            var totalThreads = gridDim.X * gridDim.Y * gridDim.Z * blockDim.X * blockDim.Y * blockDim.Z;

            // Simulate kernel execution with parallel processing
            await Task.Run(() =>
            {
                var partitioner = Partitioner.Create(0, totalThreads);
                _ = Parallel.ForEach(partitioner, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, range =>
                {
                    for (var threadId = range.Item1; threadId < range.Item2; threadId++)
                    {
                        // Simulate compute work for each thread
                        SimulateThreadExecution(threadId, execution);
                    }
                });
            }, cancellationToken);

            stopwatch.Stop();

            _ = Interlocked.Increment(ref _totalExecutions);
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
        catch (Exception ex)
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

    private static void SimulateThreadExecution(int threadId, KernelExecution execution)
    {
        // Calculate thread coordinates
        var gridDim = (Dim3)(execution.Configuration.Options.TryGetValue("GridDimension", out var grid) ? grid : new Dim3(1));
        var blockDim = (Dim3)(execution.Configuration.Options.TryGetValue("BlockDimension", out var block) ? block : new Dim3(256));

        var threadsPerBlock = blockDim.X * blockDim.Y * blockDim.Z;
        var blockId = threadId / threadsPerBlock;
        var localThreadId = threadId % threadsPerBlock;

        // Use values to avoid IDE0059 warnings
        _ = gridDim;
        _ = blockId;
        _ = localThreadId;

        // Simulate different workloads based on kernel name
        if (execution.KernelName.Contains("MatrixMultiply", StringComparison.OrdinalIgnoreCase))
        {
            SimulateMatrixMultiply(threadId);
        }
        else if (execution.KernelName.Contains("Reduction", StringComparison.OrdinalIgnoreCase))
        {
            SimulateReduction(threadId);
        }
        else if (execution.KernelName.Contains("Convolution", StringComparison.OrdinalIgnoreCase))
        {
            SimulateConvolution(threadId);
        }
        else
        {
            SimulateGenericCompute(threadId);
        }
    }

    private static void SimulateMatrixMultiply(int threadId)
    {
        // Simulate matrix multiplication workload
        double sum = 0;
        for (var i = 0; i < 100; i++)
        {
            sum += Math.Sin(threadId * i * 0.001) * Math.Cos(threadId * i * 0.001);
        }
    }

    private static void SimulateReduction(int threadId)
    {
        // Simulate reduction workload
        double value = threadId;
        for (var i = 0; i < 10; i++)
        {
            value = Math.Sqrt(value + 1);
        }
    }

    private static void SimulateConvolution(int threadId)
    {
        // Simulate convolution workload
        double result = 0;
        for (var i = -2; i <= 2; i++)
        {
            for (var j = -2; j <= 2; j++)
            {
                result += Math.Exp(-(i * i + j * j) / 2.0) * threadId;
            }
        }
    }

    private static void SimulateGenericCompute(int threadId)
    {
        // Generic compute simulation
        double result = threadId;
        for (var i = 0; i < 50; i++)
        {
            result = Math.Sin(result) + Math.Cos(result);
        }
    }

    private static long EstimateMemoryUsage(KernelArguments arguments)
    {
        long totalSize = 0;

        for (var i = 0; i < arguments.Length; i++)
        {
            var arg = arguments.Arguments.ToArray()[i];
            if (arg is IMemoryBuffer buffer)
            {
                totalSize += buffer.SizeInBytes;
            }
            else if (arg is Array array)
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

    private static double CalculateThroughput(int threads, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds == 0)
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

        if (result.Success)
        {
            stats.SuccessfulExecutions++;
            stats.TotalThreadsExecuted += result.ThreadsExecuted;
            stats.TotalMemoryUsed += result.MemoryUsed;

            if (result.ExecutionTimeMs < stats.MinExecutionTimeMs || stats.MinExecutionTimeMs == 0)
                stats.MinExecutionTimeMs = result.ExecutionTimeMs;

            if (result.ExecutionTimeMs > stats.MaxExecutionTimeMs)
                stats.MaxExecutionTimeMs = result.ExecutionTimeMs;
        }
        else
        {
            stats.FailedExecutions++;
        }

        stats.LastExecutionTime = execution.CompletedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Waits for completion asynchronous.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        while (!_executionQueue.IsEmpty)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _executionSemaphore?.Dispose();
        }
    }
}

/// <summary>
/// Represents a kernel execution.
/// </summary>
public sealed class KernelExecution
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the kernel.
    /// </summary>
    /// <value>
    /// The name of the kernel.
    /// </value>
    public string KernelName { get; set; } = "";

    /// <summary>
    /// Gets or sets the arguments.
    /// </summary>
    /// <value>
    /// The arguments.
    /// </value>
    public KernelArguments Arguments { get; set; } = null!;

    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    /// <value>
    /// The configuration.
    /// </value>
    public KernelConfiguration Configuration { get; set; } = null!;

    /// <summary>
    /// Gets or sets the queued at.
    /// </summary>
    /// <value>
    /// The queued at.
    /// </value>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// Gets or sets the started at.
    /// </summary>
    /// <value>
    /// The started at.
    /// </value>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the completed at.
    /// </summary>
    /// <value>
    /// The completed at.
    /// </value>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Result of kernel execution.
/// </summary>
public sealed class KernelExecutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="KernelExecutionResult"/> is success.
    /// </summary>
    /// <value>
    ///   <c>true</c> if success; otherwise, <c>false</c>.
    /// </value>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    /// <value>
    /// The execution time ms.
    /// </value>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the threads executed.
    /// </summary>
    /// <value>
    /// The threads executed.
    /// </value>
    public int ThreadsExecuted { get; set; }

    /// <summary>
    /// Gets or sets the memory used.
    /// </summary>
    /// <value>
    /// The memory used.
    /// </value>
    public long MemoryUsed { get; set; }

    /// <summary>
    /// Gets or sets the throughput.
    /// </summary>
    /// <value>
    /// The throughput.
    /// </value>
    public double Throughput { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>
    /// The error message.
    /// </value>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the type of the error.
    /// </summary>
    /// <value>
    /// The type of the error.
    /// </value>
    public string? ErrorType { get; set; }
}

/// <summary>
/// Statistics for kernel executions.
/// </summary>
public sealed class KernelStatistics
{
    /// <summary>
    /// Gets or sets the name of the kernel.
    /// </summary>
    /// <value>
    /// The name of the kernel.
    /// </value>
    public string KernelName { get; set; } = "";

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>
    /// The execution count.
    /// </value>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the successful executions.
    /// </summary>
    /// <value>
    /// The successful executions.
    /// </value>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the failed executions.
    /// </summary>
    /// <value>
    /// The failed executions.
    /// </value>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    /// <value>
    /// The total execution time.
    /// </value>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time ms.
    /// </summary>
    /// <value>
    /// The minimum execution time ms.
    /// </value>
    public double MinExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time ms.
    /// </summary>
    /// <value>
    /// The maximum execution time ms.
    /// </value>
    public double MaxExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the total threads executed.
    /// </summary>
    /// <value>
    /// The total threads executed.
    /// </value>
    public long TotalThreadsExecuted { get; set; }

    /// <summary>
    /// Gets or sets the total memory used.
    /// </summary>
    /// <value>
    /// The total memory used.
    /// </value>
    public long TotalMemoryUsed { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>
    /// The last execution time.
    /// </value>
    public DateTime LastExecutionTime { get; set; }

    /// <summary>
    /// Gets the average execution time ms.
    /// </summary>
    /// <value>
    /// The average execution time ms.
    /// </value>
    public double AverageExecutionTimeMs
        => ExecutionCount > 0 ? TotalExecutionTime.TotalMilliseconds / ExecutionCount : 0;

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    /// <value>
    /// The success rate.
    /// </value>
    public double SuccessRate
        => ExecutionCount > 0 ? (double)SuccessfulExecutions / ExecutionCount * 100 : 0;
}
