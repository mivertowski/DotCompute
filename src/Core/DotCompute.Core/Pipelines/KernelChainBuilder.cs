// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Core.Pipelines.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Core.Pipelines
{
    /// <summary>
    /// Implementation of the fluent kernel chain builder that leverages existing DotCompute pipeline infrastructure.
    /// This class provides the core functionality for building and executing kernel chains with method chaining syntax.
    /// </summary>
    public sealed class KernelChainBuilder : IKernelChainBuilder
    {
        private readonly IComputeOrchestrator _orchestrator;
        private readonly IKernelResolver? _kernelResolver;
        private readonly IKernelChainProfiler? _profiler;
        private readonly IKernelChainValidator? _validator;
        private readonly IKernelChainCacheService? _cacheService;
        private readonly ILogger<KernelChainBuilder>? _logger;

        private readonly List<KernelChainStep> _steps;
        private readonly Dictionary<string, object> _context;
        private readonly List<Func<Exception, ErrorHandlingStrategy>> _errorHandlers;

        private string? _preferredBackend;
        private IAccelerator? _preferredAccelerator;
        private bool _profilingEnabled;
        private string? _profileName;
        private TimeSpan? _timeout;
        private bool _validationEnabled = true;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the KernelChainBuilder class.
        /// </summary>
        /// <param name="orchestrator">The compute orchestrator for kernel execution</param>
        /// <param name="kernelResolver">Optional kernel resolver for name-to-kernel mapping</param>
        /// <param name="profiler">Optional profiler for performance monitoring</param>
        /// <param name="validator">Optional validator for chain validation</param>
        /// <param name="cacheService">Optional cache service for result caching</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        public KernelChainBuilder(
            IComputeOrchestrator orchestrator,
            IKernelResolver? kernelResolver = null,
            IKernelChainProfiler? profiler = null,
            IKernelChainValidator? validator = null,
            IKernelChainCacheService? cacheService = null,
            ILogger<KernelChainBuilder>? logger = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _kernelResolver = kernelResolver;
            _profiler = profiler;
            _validator = validator;
            _cacheService = cacheService;
            _logger = logger;

            _steps = new List<KernelChainStep>();
            _context = new Dictionary<string, object>();
            _errorHandlers = new List<Func<Exception, ErrorHandlingStrategy>>();
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Kernel(string kernelName, params object[] args)
        {
            ThrowIfDisposed();

            var step = new KernelChainStep
            {
                Type = KernelChainStepType.Sequential,
                KernelName = kernelName ?? throw new ArgumentNullException(nameof(kernelName)),
                Arguments = args ?? Array.Empty<object>(),
                StepId = Guid.NewGuid().ToString(),
                ExecutionOrder = _steps.Count
            };

            _steps.Add(step);
            _logger?.LogDebug("Added kernel '{KernelName}' to chain at position {Position}", kernelName, _steps.Count - 1);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Then(string kernelName, params object[] args)
        {
            return Kernel(kernelName, args);
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Parallel(params (string kernelName, object[] args)[] kernels)
        {
            ThrowIfDisposed();

            if (kernels == null || kernels.Length == 0)
                throw new ArgumentException("At least one kernel must be specified for parallel execution", nameof(kernels));

            var parallelStep = new KernelChainStep
            {
                Type = KernelChainStepType.Parallel,
                StepId = Guid.NewGuid().ToString(),
                ExecutionOrder = _steps.Count,
                ParallelKernels = kernels.Select((k, i) => new KernelChainStep
                {
                    Type = KernelChainStepType.Sequential,
                    KernelName = k.kernelName,
                    Arguments = k.args,
                    StepId = Guid.NewGuid().ToString(),
                    ExecutionOrder = i
                }).ToList()
            };

            _steps.Add(parallelStep);
            _logger?.LogDebug("Added {Count} kernels for parallel execution at position {Position}", 
                kernels.Length, _steps.Count - 1);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Branch<T>(Func<T, bool> condition,
            Func<IKernelChainBuilder, IKernelChainBuilder> truePath,
            Func<IKernelChainBuilder, IKernelChainBuilder>? falsePath = null)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(condition);
            ArgumentNullException.ThrowIfNull(truePath);

            var trueChain = new KernelChainBuilder(_orchestrator, _kernelResolver, _profiler, _validator, _cacheService, _logger);
            truePath(trueChain);

            KernelChainBuilder? falseChain = null;
            if (falsePath != null)
            {
                falseChain = new KernelChainBuilder(_orchestrator, _kernelResolver, _profiler, _validator, _cacheService, _logger);
                falsePath(falseChain);
            }

            var branchStep = new KernelChainStep
            {
                Type = KernelChainStepType.Branch,
                StepId = Guid.NewGuid().ToString(),
                ExecutionOrder = _steps.Count,
                BranchCondition = new BranchCondition<T>
                {
                    Condition = condition,
                    TruePath = trueChain._steps,
                    FalsePath = falseChain?._steps ?? new List<KernelChainStep>()
                }
            };

            _steps.Add(branchStep);
            _logger?.LogDebug("Added branch step at position {Position}", _steps.Count - 1);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Cache(string key, TimeSpan? ttl = null)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            if (_steps.Count == 0)
                throw new InvalidOperationException("Cannot add caching to empty chain. Add a kernel step first.");

            var lastStep = _steps[^1];
            lastStep.CacheKey = key;
            lastStep.CacheTtl = ttl;

            _logger?.LogDebug("Added caching to step '{StepId}' with key '{CacheKey}'", lastStep.StepId, key);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder OnBackend(string backendName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(backendName))
                throw new ArgumentException("Backend name cannot be null or whitespace", nameof(backendName));

            _preferredBackend = backendName;
            _preferredAccelerator = null; // Clear accelerator preference when backend is set

            _logger?.LogDebug("Set preferred backend to '{Backend}'", backendName);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder OnAccelerator(IAccelerator accelerator)
        {
            ThrowIfDisposed();

            _preferredAccelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            _preferredBackend = null; // Clear backend preference when accelerator is set

            _logger?.LogDebug("Set preferred accelerator to '{AcceleratorId}'", accelerator.Info.Id);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder WithProfiling(string? profileName = null)
        {
            ThrowIfDisposed();

            _profilingEnabled = true;
            _profileName = profileName ?? $"KernelChain_{Guid.NewGuid():N}";

            _logger?.LogDebug("Enabled profiling with name '{ProfileName}'", _profileName);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder WithTimeout(TimeSpan timeout)
        {
            ThrowIfDisposed();

            if (timeout <= TimeSpan.Zero)
                throw new ArgumentException("Timeout must be positive", nameof(timeout));

            _timeout = timeout;

            _logger?.LogDebug("Set execution timeout to {Timeout}", timeout);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder OnError(Func<Exception, ErrorHandlingStrategy> errorHandler)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(errorHandler);

            _errorHandlers.Add(errorHandler);

            _logger?.LogDebug("Added error handler (total: {Count})", _errorHandlers.Count);

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder WithValidation(bool validateInputs = true)
        {
            ThrowIfDisposed();

            _validationEnabled = validateInputs;

            _logger?.LogDebug("Set validation enabled: {ValidationEnabled}", _validationEnabled);

            return this;
        }

        /// <inheritdoc/>
        public async Task<T> ExecuteAsync<T>(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var result = await ExecuteWithMetricsAsync(cancellationToken);

            if (!result.Success)
            {
                var errors = result.Errors ?? new List<Exception>();
                var aggregateException = new AggregateException(
                    "Kernel chain execution failed", errors);
                throw aggregateException;
            }

            if (result.Result is T typedResult)
                return typedResult;

            if (result.Result == null)
                return default(T)!;

            // Attempt type conversion
            try
            {
                return (T)Convert.ChangeType(result.Result, typeof(T));
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"Cannot convert result of type {result.Result.GetType().Name} to {typeof(T).Name}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<KernelChainExecutionResult> ExecuteWithMetricsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_steps.Count == 0)
            {
                throw new InvalidOperationException("Cannot execute empty kernel chain. Add at least one kernel step.");
            }

            var stopwatch = Stopwatch.StartNew();
            var stepMetrics = new List<KernelStepMetrics>();
            var errors = new List<Exception>();
            object? finalResult = null;
            string usedBackend = "Unknown";

            try
            {
                // Validate chain if enabled
                if (_validationEnabled && _validator != null)
                {
                    var validation = await _validator.ValidateChainAsync(_steps, cancellationToken);
                    if (!validation.IsValid)
                    {
                        var validationErrors = validation.Errors ?? new List<string>();
                        throw new InvalidOperationException(
                            $"Kernel chain validation failed: {string.Join(", ", validationErrors)}");
                    }
                }

                // Start profiling if enabled
                if (_profilingEnabled && _profiler != null)
                {
                    await _profiler.StartProfilingAsync(_profileName ?? "DefaultProfile", cancellationToken);
                }

                // Execute steps
                finalResult = await ExecuteStepsAsync(_steps, stepMetrics, errors, cancellationToken);

                // Determine backend used
                usedBackend = DetermineUsedBackend();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                _logger?.LogError(ex, "Error during kernel chain execution");
            }
            finally
            {
                // Stop profiling
                if (_profilingEnabled && _profiler != null)
                {
                    try
                    {
                        await _profiler.StopProfilingAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error stopping profiler");
                    }
                }

                stopwatch.Stop();
            }

            return new KernelChainExecutionResult
            {
                Success = errors.Count == 0,
                Result = finalResult,
                ExecutionTime = stopwatch.Elapsed,
                StepMetrics = stepMetrics,
                Errors = errors.Count > 0 ? errors : null,
                Backend = usedBackend,
                MemoryMetrics = GetMemoryMetrics()
            };
        }

        /// <inheritdoc/>
        public async Task<KernelChainValidationResult> ValidateAsync()
        {
            ThrowIfDisposed();

            if (_validator == null)
            {
                return new KernelChainValidationResult
                {
                    IsValid = true,
                    Warnings = new[] { "No validator available - skipping validation" }
                };
            }

            return await _validator.ValidateChainAsync(_steps);
        }

        /// <summary>
        /// Executes a list of kernel chain steps.
        /// </summary>
        private async Task<object?> ExecuteStepsAsync(
            IEnumerable<KernelChainStep> steps,
            List<KernelStepMetrics> stepMetrics,
            List<Exception> errors,
            CancellationToken cancellationToken)
        {
            object? currentResult = null;

            foreach (var step in steps)
            {
                try
                {
                    currentResult = await ExecuteStepAsync(step, currentResult, stepMetrics, cancellationToken);
                }
                catch (Exception ex)
                {
                    var handlerResult = HandleStepError(ex);
                    
                    switch (handlerResult)
                    {
                        case ErrorHandlingStrategy.Continue:
                            _logger?.LogWarning(ex, "Continuing after error in step {StepId}", step.StepId);
                            continue;
                        case ErrorHandlingStrategy.Skip:
                            _logger?.LogWarning(ex, "Skipping step {StepId} after error", step.StepId);
                            continue;
                        case ErrorHandlingStrategy.Abort:
                            errors.Add(ex);
                            throw;
                        case ErrorHandlingStrategy.Fallback:
                            currentResult = GetFallbackValue(step);
                            _logger?.LogWarning(ex, "Using fallback value for step {StepId}", step.StepId);
                            continue;
                        case ErrorHandlingStrategy.Retry:
                            // Simple retry logic - could be enhanced with exponential backoff
                            try
                            {
                                currentResult = await ExecuteStepAsync(step, currentResult, stepMetrics, cancellationToken);
                            }
                            catch
                            {
                                errors.Add(ex);
                                throw;
                            }
                            break;
                        default:
                            errors.Add(ex);
                            throw;
                    }
                }
            }

            return currentResult;
        }

        /// <summary>
        /// Executes a single kernel chain step.
        /// </summary>
        private async Task<object?> ExecuteStepAsync(
            KernelChainStep step,
            object? previousResult,
            List<KernelStepMetrics> stepMetrics,
            CancellationToken cancellationToken)
        {
            var stepStopwatch = Stopwatch.StartNew();
            object? result = null;
            bool wasCached = false;
            long memoryUsed = 0;

            try
            {
                // Check cache first
                if (!string.IsNullOrEmpty(step.CacheKey) && _cacheService != null)
                {
                    var cachedResult = await _cacheService.GetAsync<object>(step.CacheKey, cancellationToken);
                    if (cachedResult != null)
                    {
                        result = cachedResult;
                        wasCached = true;
                        _logger?.LogDebug("Using cached result for step {StepId}", step.StepId);
                    }
                }

                if (!wasCached)
                {
                    result = step.Type switch
                    {
                        KernelChainStepType.Sequential => await ExecuteSequentialStepAsync(step, cancellationToken),
                        KernelChainStepType.Parallel => await ExecuteParallelStepAsync(step, cancellationToken),
                        KernelChainStepType.Branch => await ExecuteBranchStepAsync(step, previousResult, cancellationToken),
                        _ => throw new NotSupportedException($"Step type {step.Type} is not supported")
                    };

                    // Cache result if configured
                    if (!string.IsNullOrEmpty(step.CacheKey) && _cacheService != null && result != null)
                    {
                        await _cacheService.SetAsync(step.CacheKey, result, step.CacheTtl, cancellationToken);
                    }
                }

                return result;
            }
            finally
            {
                stepStopwatch.Stop();

                stepMetrics.Add(new KernelStepMetrics
                {
                    KernelName = step.KernelName ?? $"{step.Type}Step",
                    StepIndex = step.ExecutionOrder,
                    ExecutionTime = stepStopwatch.Elapsed,
                    Backend = DetermineUsedBackend(),
                    WasCached = wasCached,
                    MemoryUsed = memoryUsed
                });
            }
        }

        /// <summary>
        /// Executes a sequential kernel step.
        /// </summary>
        private async Task<object?> ExecuteSequentialStepAsync(KernelChainStep step, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.KernelName))
                throw new InvalidOperationException("Kernel name is required for sequential steps");

            // Use the appropriate orchestrator method based on preferences
            if (_preferredAccelerator != null)
            {
                return await _orchestrator.ExecuteAsync<object>(step.KernelName, _preferredAccelerator, step.Arguments);
            }
            
            if (!string.IsNullOrEmpty(_preferredBackend))
            {
                return await _orchestrator.ExecuteAsync<object>(step.KernelName, _preferredBackend, step.Arguments);
            }

            return await _orchestrator.ExecuteAsync<object>(step.KernelName, step.Arguments);
        }

        /// <summary>
        /// Executes parallel kernel steps.
        /// </summary>
        private async Task<object?> ExecuteParallelStepAsync(KernelChainStep step, CancellationToken cancellationToken)
        {
            if (step.ParallelKernels == null || step.ParallelKernels.Count == 0)
                return null;

            var tasks = step.ParallelKernels.Select(async parallelStep =>
            {
                if (string.IsNullOrEmpty(parallelStep.KernelName))
                    throw new InvalidOperationException("Kernel name is required for parallel steps");

                if (_preferredAccelerator != null)
                {
                    return await _orchestrator.ExecuteAsync<object>(parallelStep.KernelName, _preferredAccelerator, parallelStep.Arguments);
                }
                
                if (!string.IsNullOrEmpty(_preferredBackend))
                {
                    return await _orchestrator.ExecuteAsync<object>(parallelStep.KernelName, _preferredBackend, parallelStep.Arguments);
                }

                return await _orchestrator.ExecuteAsync<object>(parallelStep.KernelName, parallelStep.Arguments);
            });

            var results = await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// Executes a branch step with conditional logic.
        /// </summary>
        private async Task<object?> ExecuteBranchStepAsync(KernelChainStep step, object? previousResult, CancellationToken cancellationToken)
        {
            if (step.BranchCondition == null)
                throw new InvalidOperationException("Branch condition is required for branch steps");

            var stepMetrics = new List<KernelStepMetrics>();
            var errors = new List<Exception>();

            // Evaluate condition
            bool conditionResult = step.BranchCondition.EvaluateCondition(previousResult);
            
            var pathToExecute = conditionResult ? step.BranchCondition.TruePath : step.BranchCondition.FalsePath;
            
            if (pathToExecute?.Count > 0)
            {
                return await ExecuteStepsAsync(pathToExecute, stepMetrics, errors, cancellationToken);
            }

            return previousResult;
        }

        /// <summary>
        /// Handles errors that occur during step execution.
        /// </summary>
        private ErrorHandlingStrategy HandleStepError(Exception ex)
        {
            foreach (var handler in _errorHandlers)
            {
                try
                {
                    var strategy = handler(ex);
                    if (strategy != ErrorHandlingStrategy.Continue)
                    {
                        return strategy;
                    }
                }
                catch (Exception handlerEx)
                {
                    _logger?.LogWarning(handlerEx, "Error in error handler");
                }
            }

            return ErrorHandlingStrategy.Abort; // Default behavior
        }

        /// <summary>
        /// Gets a fallback value for a failed step.
        /// </summary>
        private object? GetFallbackValue(KernelChainStep step)
        {
            // This could be enhanced to support configurable fallback values
            return null;
        }

        /// <summary>
        /// Determines the backend used for execution.
        /// </summary>
        private string DetermineUsedBackend()
        {
            if (_preferredAccelerator != null)
                return _preferredAccelerator.Info.Name ?? _preferredAccelerator.GetType().Name;
            
            if (!string.IsNullOrEmpty(_preferredBackend))
                return _preferredBackend;

            return "Auto";
        }

        /// <summary>
        /// Gets memory usage metrics for the execution.
        /// </summary>
        private KernelChainMemoryMetrics? GetMemoryMetrics()
        {
            // This could be enhanced to collect actual memory metrics
            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);

            return new KernelChainMemoryMetrics
            {
                PeakMemoryUsage = GC.GetTotalMemory(false),
                TotalMemoryAllocated = GC.GetTotalMemory(false),
                GarbageCollections = gc0 + gc1 + gc2,
                MemoryPoolingUsed = false // Could be determined by checking if memory pools are configured
            };
        }

        /// <summary>
        /// Throws if the builder has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KernelChainBuilder));
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            try
            {
                // Clean up any resources
                _steps.Clear();
                _context.Clear();
                _errorHandlers.Clear();

                // Stop any active profiling
                if (_profilingEnabled && _profiler != null)
                {
                    try
                    {
                        await _profiler.StopProfilingAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error stopping profiler during disposal");
                    }
                }

                _disposed = true;
                _logger?.LogDebug("KernelChainBuilder disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during KernelChainBuilder disposal");
                throw;
            }
        }
    }
}
