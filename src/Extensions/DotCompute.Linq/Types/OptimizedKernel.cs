// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Compute.Enums;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators.Execution;
using System.Collections.Immutable;
using ICompiledKernel = DotCompute.Linq.Operators.Execution.ICompiledKernel;
namespace DotCompute.Linq.Types
{
    /// <summary>
    /// Represents an optimized kernel with performance characteristics and execution metadata.
    /// </summary>
    /// <remarks>
    /// This class extends the basic compiled kernel concept with optimization-specific
    /// information, performance metrics, and execution strategies to enable intelligent
    /// runtime decisions and performance monitoring.
    /// </remarks>
    public sealed class OptimizedKernel : IDisposable
    {
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizedKernel"/> class.
        /// </summary>
        /// <param name="compiledKernel">The underlying compiled kernel.</param>
        /// <param name="optimizationLevel">The optimization level applied.</param>
        /// <param name="backend">The target backend type.</param>
        public OptimizedKernel(
            ICompiledKernel compiledKernel,
            OptimizationLevel optimizationLevel,
            ComputeBackendType backend)
        {
            CompiledKernel = compiledKernel ?? throw new ArgumentNullException(nameof(compiledKernel));
            OptimizationLevel = optimizationLevel;
            Backend = backend;
            OptimizationHints = ImmutableArray<OptimizationHint>.Empty;
            PerformanceMetrics = new KernelPerformanceMetrics();
            CreatedAt = DateTimeOffset.UtcNow;
        }
        /// <summary>
        /// Gets the underlying compiled kernel.
        /// </summary>
        public ICompiledKernel CompiledKernel { get; }

        /// <summary>
        /// Gets the optimization level applied to this kernel.
        /// </summary>
        public OptimizationLevel OptimizationLevel { get; }

        /// <summary>
        /// Gets the target backend type.
        /// </summary>
        public ComputeBackendType Backend { get; }

        /// <summary>
        /// Gets the optimization hints that were applied.
        /// </summary>
        public ImmutableArray<OptimizationHint> OptimizationHints { get; init; }

        /// <summary>
        /// Gets the performance metrics for this optimized kernel.
        /// </summary>
        public KernelPerformanceMetrics PerformanceMetrics { get; }

        /// <summary>
        /// Gets the estimated execution cost relative to baseline.
        /// </summary>
        public double RelativePerformance { get; init; } = 1.0;

        /// <summary>
        /// Gets the memory efficiency score (0.0 to 1.0).
        /// </summary>
        public double MemoryEfficiency { get; init; } = 1.0;

        /// <summary>
        /// Gets the optimization strategy used.
        /// </summary>
        public OptimizationStrategy Strategy { get; init; } = OptimizationStrategy.Balanced;

        /// <summary>
        /// Gets a value indicating whether this kernel supports concurrent execution.
        /// </summary>
        public bool SupportsConcurrency { get; init; } = true;

        /// <summary>
        /// Gets the recommended workgroup size for optimal performance.
        /// </summary>
        public int RecommendedWorkgroupSize { get; init; }

        /// <summary>
        /// Gets the timestamp when this optimized kernel was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }
        /// <summary>
        /// Gets optimization metadata as key-value pairs.
        /// </summary>
        public ImmutableDictionary<string, object> Metadata { get; init; } =
            ImmutableDictionary<string, object>.Empty;

        /// <summary>
        /// Gets the list of optimization techniques that were applied to this kernel.
        /// </summary>
        public ImmutableArray<string> AppliedOptimizations { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Gets a value indicating whether this kernel is valid and ready for execution.
        /// </summary>
        public bool IsValid => !_disposed && CompiledKernel != null;
        /// <summary>
        /// Disposes the optimized kernel and its resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                CompiledKernel?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Defines optimization strategies for kernel execution.
    /// </summary>
    public enum OptimizationStrategy
    {
        /// <summary>Optimize for maximum performance regardless of resource usage.</summary>
        Performance,
        /// <summary>Optimize for minimal memory usage.</summary>
        Memory,
        /// <summary>Optimize for minimal power consumption.</summary>
        Power,
        /// <summary>Balance performance, memory, and power considerations.</summary>
        Balanced,
        /// <summary>Optimize for concurrent execution scenarios.</summary>
        Concurrency,
        /// <summary>Custom optimization strategy defined by user hints.</summary>
        Custom
    }
    /// <summary>
    /// Tracks performance metrics for an optimized kernel.
    /// </summary>
    public sealed class KernelPerformanceMetrics
    {
        private readonly List<TimeSpan> _executionTimes = [];
        private readonly List<double> _throughputs = [];
        private long _totalExecutions;
        private long _failedExecutions;
        /// <summary>
        /// Gets the total number of executions.
        /// </summary>
        public long TotalExecutions => _totalExecutions;

        /// <summary>
        /// Gets the number of failed executions.
        /// </summary>
        public long FailedExecutions => _failedExecutions;

        /// <summary>
        /// Gets the success rate as a percentage.
        /// </summary>
        public double SuccessRate => _totalExecutions > 0 ?
            ((double)(_totalExecutions - _failedExecutions) / _totalExecutions) * 100.0 : 0.0;

        /// <summary>
        /// Gets the average execution time.
        /// </summary>
        public TimeSpan AverageExecutionTime => _executionTimes.Count > 0 ?
            TimeSpan.FromTicks((long)_executionTimes.Average(t => t.Ticks)) : TimeSpan.Zero;
        /// <summary>
        /// Records a successful execution.
        /// </summary>
        /// <param name="executionTime">The execution time.</param>
        /// <param name="workItems">The number of work items processed.</param>
        public void RecordExecution(TimeSpan executionTime, long workItems)
        {
            Interlocked.Increment(ref _totalExecutions);
            lock (_executionTimes)
            {
                _executionTimes.Add(executionTime);
                if (executionTime.TotalSeconds > 0)
                {
                    _throughputs.Add(workItems / executionTime.TotalSeconds);
                }
                // Keep only recent executions to avoid unbounded growth
                if (_executionTimes.Count > 1000)
                {
                    _executionTimes.RemoveRange(0, 100);
                    _throughputs.RemoveRange(0, Math.Min(100, _throughputs.Count));
                }
            }
        }
        /// <summary>
        /// Records a failed execution.
        /// </summary>
        /// <param name="executionTime">The time until failure.</param>
        public void RecordFailure(TimeSpan executionTime)
        {
            Interlocked.Increment(ref _failedExecutions);
        }
        /// <summary>
        /// Resets all performance metrics.
        /// </summary>
        public void Reset()
        {
            lock (_executionTimes)
            {
                _executionTimes.Clear();
                _throughputs.Clear();
                _totalExecutions = 0;
                _failedExecutions = 0;
            }
        }
    }
}
