// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Runtime.Logging;

namespace DotCompute.Runtime.Services
{
    /// <summary>
    /// Production kernel execution service with advanced scheduling, monitoring, and optimization.
    /// </summary>
    public sealed class ProductionKernelExecutor : IDisposable
    {
        private readonly ILogger<ProductionKernelExecutor> _logger;
        private readonly IAccelerator _accelerator;
        private readonly ConcurrentDictionary<Guid, ExecutingKernel> _executingKernels = new();
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly Timer _cleanupTimer;
        private readonly KernelExecutionStatistics _statistics = new();
        private bool _disposed;
        /// <summary>
        /// Gets or sets the statistics.
        /// </summary>
        /// <value>The statistics.</value>

        public KernelExecutionStatistics Statistics => _statistics;
        /// <summary>
        /// Initializes a new instance of the ProductionKernelExecutor class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="maxConcurrentExecutions">The max concurrent executions.</param>

        public ProductionKernelExecutor(ILogger<ProductionKernelExecutor> logger, IAccelerator accelerator, int maxConcurrentExecutions = 16)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            _executionSemaphore = new SemaphoreSlim(maxConcurrentExecutions, maxConcurrentExecutions);

            // Setup periodic cleanup of completed kernels
            _cleanupTimer = new Timer(PerformPeriodicCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInfoMessage($"Production kernel executor initialized with {maxConcurrentExecutions} concurrent executions");
        }

        /// <summary>
        /// Executes a kernel asynchronously with comprehensive monitoring and error handling.
        /// </summary>
        public async Task<KernelExecutionResult> ExecuteKernelAsync(
            ICompiledKernel kernel,
            IKernelExecutionParameters parameters,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(parameters);

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProductionKernelExecutor));
            }

            var executionId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            await _executionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var executingKernel = new ExecutingKernel(executionId, kernel.Name, stopwatch);
                _ = _executingKernels.TryAdd(executionId, executingKernel);

                _logger.LogInfoMessage($"Starting kernel execution {executionId} for {kernel.Name}");

                try
                {
                    // Validate parameters before execution
                    ValidateExecutionParameters(parameters);

                    // Execute the kernel
                    var kernelArgs = new KernelArguments(parameters.Arguments);
                    await kernel.ExecuteAsync(kernelArgs, cancellationToken).ConfigureAwait(false);

                    // Synchronize to ensure completion
                    await _accelerator.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

                    stopwatch.Stop();
                    executingKernel.Complete(stopwatch.Elapsed);

                    // Update statistics
                    _ = Interlocked.Increment(ref _statistics._successfulExecutions);
                    _ = Interlocked.Add(ref _statistics._totalExecutionTime, stopwatch.ElapsedTicks);

                    _logger.LogInfoMessage($"Kernel execution {executionId} completed successfully in {stopwatch.Elapsed.TotalMilliseconds}ms");

                    return KernelExecutionResult.Success(executionId, stopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    executingKernel.Fail(ex, stopwatch.Elapsed);

                    _ = Interlocked.Increment(ref _statistics._failedExecutions);

                    _logger.LogErrorMessage(ex, $"Kernel execution {executionId} failed after {stopwatch.Elapsed.TotalMilliseconds}ms");

                    return KernelExecutionResult.Failure(executionId, ex, stopwatch.Elapsed);
                }
                finally
                {
                    // Mark as completed in tracking
                    if (_executingKernels.TryGetValue(executionId, out var tracked))
                    {
                        tracked.MarkCompleted();
                    }
                }
            }
            finally
            {
                _ = _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a batch of kernels with optimized scheduling.
        /// </summary>
        public async Task<IReadOnlyList<KernelExecutionResult>> ExecuteBatchAsync(
            IEnumerable<(ICompiledKernel Kernel, IKernelExecutionParameters Parameters)> kernelBatch,
            BatchExecutionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernelBatch);

            options ??= new BatchExecutionOptions();
            var kernels = kernelBatch.ToList();

            if (kernels.Count == 0)
            {
                return Array.Empty<KernelExecutionResult>();
            }

            _logger.LogInfoMessage($"Starting batch execution of {kernels.Count} kernels");

            var results = new List<KernelExecutionResult>();
            var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);

            try
            {
                var tasks = kernels.Select(async kernel =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        return await ExecuteKernelAsync(kernel.Kernel, kernel.Parameters, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _ = semaphore.Release();
                    }
                });

                var batchResults = await Task.WhenAll(tasks).ConfigureAwait(false);
                results.AddRange(batchResults);

                var successCount = batchResults.Count(r => r.IsSuccess);
                _logger.LogInfoMessage($"Batch execution completed: {successCount}/{kernels.Count} kernels succeeded");

                return results.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Batch execution failed");
                throw;
            }
            finally
            {
                semaphore.Dispose();
            }
        }

        /// <summary>
        /// Gets the current status of all executing kernels.
        /// </summary>
        public IReadOnlyDictionary<Guid, KernelExecutionStatus> GetExecutionStatus()
        {
            return _executingKernels.ToDictionary(
                kvp => kvp.Key,
                kvp => new KernelExecutionStatus(
                    kvp.Value.Id,
                    kvp.Value.KernelName,
                    kvp.Value.State,
                    kvp.Value.StartTime,
                    kvp.Value.ElapsedTime,
                    kvp.Value.LastError));
        }

        /// <summary>
        /// Cancels a specific kernel execution.
        /// </summary>
        public bool CancelExecution(Guid executionId)
        {
            if (_executingKernels.TryGetValue(executionId, out var executingKernel))
            {
                executingKernel.Cancel();
                _logger.LogWarningMessage($"Kernel execution {executionId} cancelled");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates kernel execution parameters.
        /// </summary>
        private static void ValidateExecutionParameters(IKernelExecutionParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            // Validate kernel arguments if present
            if (parameters.Arguments == null || parameters.Arguments.Length == 0)
            {
                throw new ArgumentException("Kernel parameters must include arguments", nameof(parameters));
            }
        }

        /// <summary>
        /// Performs periodic cleanup of completed kernel executions.
        /// </summary>
        private void PerformPeriodicCleanup(object? state)
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Keep completed executions for 5 minutes
                var completedKeys = _executingKernels
                    .Where(kvp => kvp.Value.IsCompleted && kvp.Value.CompletionTime < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in completedKeys)
                {
                    _ = _executingKernels.TryRemove(key, out _);
                }

                if (completedKeys.Count > 0)
                {
                    _logger.LogDebugMessage($"Cleaned up {completedKeys.Count} completed kernel executions");
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during periodic cleanup");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cleanupTimer.Dispose();
                _executionSemaphore.Dispose();
                _executingKernels.Clear();
                _logger.LogInfoMessage("Production kernel executor disposed");
            }
        }
    }

    /// <summary>
    /// Represents a kernel that is currently executing.
    /// </summary>
    public sealed class ExecutingKernel(Guid id, string kernelName, Stopwatch stopwatch)
    {
        private readonly Stopwatch _stopwatch = stopwatch;
        private volatile KernelExecutionState _state = KernelExecutionState.Running;
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>

        public Guid Id { get; } = id;
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        /// <value>The kernel name.</value>
        public string KernelName { get; } = kernelName;
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; } = DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the elapsed time.
        /// </summary>
        /// <value>The elapsed time.</value>
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public KernelExecutionState State => _state;
        /// <summary>
        /// Gets or sets the last error.
        /// </summary>
        /// <value>The last error.</value>
        public Exception? LastError { get; private set; }
        /// <summary>
        /// Gets or sets the completion time.
        /// </summary>
        /// <value>The completion time.</value>
        public DateTime? CompletionTime { get; private set; }
        /// <summary>
        /// Gets or sets a value indicating whether completed.
        /// </summary>
        /// <value>The is completed.</value>
        public bool IsCompleted => _state is KernelExecutionState.Completed or KernelExecutionState.Failed or KernelExecutionState.Cancelled;
        /// <summary>
        /// Performs complete.
        /// </summary>
        /// <param name="executionTime">The execution time.</param>

        public void Complete(TimeSpan executionTime)
        {
            _state = KernelExecutionState.Completed;
            CompletionTime = DateTime.UtcNow;
        }
        /// <summary>
        /// Performs fail.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <param name="executionTime">The execution time.</param>

        public void Fail(Exception error, TimeSpan executionTime)
        {
            _state = KernelExecutionState.Failed;
            LastError = error;
            CompletionTime = DateTime.UtcNow;
        }
        /// <summary>
        /// Determines whether cel.
        /// </summary>

        public void Cancel()
        {
            _state = KernelExecutionState.Cancelled;
            CompletionTime = DateTime.UtcNow;
        }
        /// <summary>
        /// Performs mark completed.
        /// </summary>

        public void MarkCompleted()
        {
            if (_state == KernelExecutionState.Running)
            {
                _state = KernelExecutionState.Completed;
            }
            CompletionTime ??= DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Statistics for kernel execution performance tracking.
    /// </summary>
    public sealed class KernelExecutionStatistics
    {
        internal long _successfulExecutions;
        internal long _failedExecutions;
        internal long _totalExecutionTime; // In ticks
        /// <summary>
        /// Gets or sets the successful executions.
        /// </summary>
        /// <value>The successful executions.</value>

        public long SuccessfulExecutions => _successfulExecutions;
        /// <summary>
        /// Gets or sets the failed executions.
        /// </summary>
        /// <value>The failed executions.</value>
        public long FailedExecutions => _failedExecutions;
        /// <summary>
        /// Gets or sets the total executions.
        /// </summary>
        /// <value>The total executions.</value>
        public long TotalExecutions => _successfulExecutions + _failedExecutions;
        /// <summary>
        /// Gets or sets the success rate.
        /// </summary>
        /// <value>The success rate.</value>
        public double SuccessRate => TotalExecutions == 0 ? 0.0 : (double)_successfulExecutions / TotalExecutions;
        /// <summary>
        /// Gets or sets the average execution time.
        /// </summary>
        /// <value>The average execution time.</value>
        public TimeSpan AverageExecutionTime => TotalExecutions == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(_totalExecutionTime / TotalExecutions);
        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        /// <value>The total execution time.</value>
        public TimeSpan TotalExecutionTime => TimeSpan.FromTicks(_totalExecutionTime);
    }

    /// <summary>
    /// Represents the result of a kernel execution.
    /// </summary>
    public sealed class KernelExecutionResult
    {
        private KernelExecutionResult(Guid executionId, bool isSuccess, Exception? error, TimeSpan executionTime)
        {
            ExecutionId = executionId;
            IsSuccess = isSuccess;
            Error = error;
            ExecutionTime = executionTime;
        }
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        /// <value>The execution id.</value>

        public Guid ExecutionId { get; }
        /// <summary>
        /// Gets or sets a value indicating whether success.
        /// </summary>
        /// <value>The is success.</value>
        public bool IsSuccess { get; }
        /// <summary>
        /// Gets or sets the error.
        /// </summary>
        /// <value>The error.</value>
        public Exception? Error { get; }
        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        /// <value>The execution time.</value>
        public TimeSpan ExecutionTime { get; }
        /// <summary>
        /// Gets success.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="executionTime">The execution time.</param>
        /// <returns>The result of the operation.</returns>

        public static KernelExecutionResult Success(Guid executionId, TimeSpan executionTime)
            => new(executionId, true, null, executionTime);
        /// <summary>
        /// Gets failure.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="error">The error.</param>
        /// <param name="executionTime">The execution time.</param>
        /// <returns>The result of the operation.</returns>

        public static KernelExecutionResult Failure(Guid executionId, Exception error, TimeSpan executionTime)
            => new(executionId, false, error, executionTime);
    }

    /// <summary>
    /// Represents the current status of a kernel execution.
    /// </summary>
    public sealed class KernelExecutionStatus(Guid id, string kernelName, KernelExecutionState state, DateTime startTime, TimeSpan elapsedTime, Exception? lastError)
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; } = id;
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        /// <value>The kernel name.</value>
        public string KernelName { get; } = kernelName;
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public KernelExecutionState State { get; } = state;
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; } = startTime;
        /// <summary>
        /// Gets or sets the elapsed time.
        /// </summary>
        /// <value>The elapsed time.</value>
        public TimeSpan ElapsedTime { get; } = elapsedTime;
        /// <summary>
        /// Gets or sets the last error.
        /// </summary>
        /// <value>The last error.</value>
        public Exception? LastError { get; } = lastError;
    }

    /// <summary>
    /// Options for batch kernel execution.
    /// </summary>
    public sealed class BatchExecutionOptions
    {
        /// <summary>
        /// Gets or sets the max concurrency.
        /// </summary>
        /// <value>The max concurrency.</value>
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
        /// <summary>
        /// Gets or sets the continue on error.
        /// </summary>
        /// <value>The continue on error.</value>
        public bool ContinueOnError { get; set; } = true;
        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        /// <value>The timeout.</value>
        public TimeSpan? Timeout { get; set; }
    }
    /// <summary>
    /// An kernel execution state enumeration.
    /// </summary>

    /// <summary>
    /// Represents the state of a kernel execution.
    /// </summary>
    public enum KernelExecutionState
    {
        Running,
        Completed,
        Failed,
        Cancelled
    }
}