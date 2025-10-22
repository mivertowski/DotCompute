using System.Globalization;
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Core.Pipelines.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

// Type aliases to resolve ambiguous references
using KernelChainExecutionResult = DotCompute.Abstractions.Interfaces.Pipelines.KernelChainExecutionResult;
using KernelStepMetrics = DotCompute.Abstractions.Interfaces.Pipelines.KernelStepMetrics;
using KernelChainMemoryMetrics = DotCompute.Abstractions.Interfaces.Pipelines.KernelChainMemoryMetrics;
using ErrorHandlingStrategy = DotCompute.Abstractions.Interfaces.Pipelines.ErrorHandlingStrategy;
using KernelChainValidationResult = DotCompute.Abstractions.Interfaces.Pipelines.KernelChainValidationResult;

namespace DotCompute.Core.Pipelines
{
    /// <summary>
    /// Implementation of the fluent kernel chain builder that leverages existing DotCompute pipeline infrastructure.
    /// This class provides the core functionality for building and executing kernel chains with method chaining syntax.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the KernelChainBuilder class.
    /// </remarks>
    /// <param name="orchestrator">The compute orchestrator for kernel execution</param>
    /// <param name="kernelResolver">Optional kernel resolver for name-to-kernel mapping</param>
    /// <param name="profiler">Optional profiler for performance monitoring</param>
    /// <param name="validator">Optional validator for chain validation</param>
    /// <param name="cacheService">Optional cache service for result caching</param>
    /// <param name="logger">Optional logger for diagnostic information</param>
    public sealed partial class KernelChainBuilder(
        IComputeOrchestrator orchestrator,
        IKernelResolver? kernelResolver = null,
        IKernelChainProfiler? profiler = null,
        IKernelChainValidator? validator = null,
        IKernelChainCacheService? cacheService = null,
        ILogger<KernelChainBuilder>? logger = null) : IKernelChainBuilder
    {
        // LoggerMessage delegates - Event ID range 19000-19099 for KernelChainBuilder (Pipeline module)
        private static readonly Action<ILogger, string, int, Exception?> _logKernelAdded =
            LoggerMessage.Define<string, int>(
                MsLogLevel.Debug,
                new EventId(19000, nameof(Kernel)),
                "Added kernel '{KernelName}' to chain at position {Position}");

        private static readonly Action<ILogger, int, int, Exception?> _logParallelKernelsAdded =
            LoggerMessage.Define<int, int>(
                MsLogLevel.Debug,
                new EventId(19001, nameof(Parallel)),
                "Added {Count} kernels for parallel execution at position {Position}");

        private static readonly Action<ILogger, int, Exception?> _logBranchAdded =
            LoggerMessage.Define<int>(
                MsLogLevel.Debug,
                new EventId(19002, nameof(Branch)),
                "Added branch step at position {Position}");

        private static readonly Action<ILogger, string, string, Exception?> _logCacheAdded =
            LoggerMessage.Define<string, string>(
                MsLogLevel.Debug,
                new EventId(19003, nameof(Cache)),
                "Added caching to step '{StepId}' with key '{CacheKey}'");

        private static readonly Action<ILogger, string, Exception?> _logBackendSet =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(19004, nameof(OnBackend)),
                "Set preferred backend to '{Backend}'");

        private static readonly Action<ILogger, string, Exception?> _logAcceleratorSet =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(19005, nameof(OnAccelerator)),
                "Set preferred accelerator to '{AcceleratorId}'");

        private static readonly Action<ILogger, string, Exception?> _logProfilingEnabled =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(19006, nameof(WithProfiling)),
                "Enabled profiling with name '{ProfileName}'");

        private static readonly Action<ILogger, TimeSpan, Exception?> _logTimeoutSet =
            LoggerMessage.Define<TimeSpan>(
                MsLogLevel.Debug,
                new EventId(19007, nameof(WithTimeout)),
                "Set execution timeout to {Timeout}");

        private static readonly Action<ILogger, int, Exception?> _logErrorHandlerAdded =
            LoggerMessage.Define<int>(
                MsLogLevel.Debug,
                new EventId(19008, nameof(OnError)),
                "Added error handler (total: {Count})");

        private static readonly Action<ILogger, bool, Exception?> _logValidationSet =
            LoggerMessage.Define<bool>(
                MsLogLevel.Debug,
                new EventId(19009, nameof(WithValidation)),
                "Set validation enabled: {ValidationEnabled}");

        private static readonly Action<ILogger, Exception> _logExecutionError =
            LoggerMessage.Define(
                MsLogLevel.Error,
                new EventId(19010, "ExecutionError"),
                "Error during kernel chain execution");

        private static readonly Action<ILogger, Exception> _logProfilerStopError =
            LoggerMessage.Define(
                MsLogLevel.Warning,
                new EventId(19011, "ProfilerStopError"),
                "Error stopping profiler");

        private static readonly Action<ILogger, string, Exception> _logStepContinueAfterError =
            LoggerMessage.Define<string>(
                MsLogLevel.Warning,
                new EventId(19012, "StepContinueAfterError"),
                "Continuing after error in step {StepId}");

        private static readonly Action<ILogger, string, Exception> _logStepSkipAfterError =
            LoggerMessage.Define<string>(
                MsLogLevel.Warning,
                new EventId(19013, "StepSkipAfterError"),
                "Skipping step {StepId} after error");

        private static readonly Action<ILogger, string, Exception> _logStepFallbackValue =
            LoggerMessage.Define<string>(
                MsLogLevel.Warning,
                new EventId(19014, "StepFallbackValue"),
                "Using fallback value for step {StepId}");

        private static readonly Action<ILogger, string, Exception?> _logCachedResult =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(19015, "CachedResult"),
                "Using cached result for step {StepId}");

        private static readonly Action<ILogger, Exception> _logErrorHandlerException =
            LoggerMessage.Define(
                MsLogLevel.Warning,
                new EventId(19016, "ErrorHandlerException"),
                "Error in error handler");

        private static readonly Action<ILogger, Exception> _logProfilerStopErrorDisposal =
            LoggerMessage.Define(
                MsLogLevel.Warning,
                new EventId(19017, "ProfilerStopErrorDisposal"),
                "Error stopping profiler during disposal");

        private static readonly Action<ILogger, Exception?> _logBuilderDisposed =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(19018, "BuilderDisposed"),
                "KernelChainBuilder disposed successfully");

        private static readonly Action<ILogger, Exception> _logDisposalError =
            LoggerMessage.Define(
                MsLogLevel.Error,
                new EventId(19019, "DisposalError"),
                "Error during KernelChainBuilder disposal");

        // Wrapper methods
        private static void LogKernelAdded(ILogger logger, string kernelName, int position)
            => _logKernelAdded(logger, kernelName, position, null);

        private static void LogParallelKernelsAdded(ILogger logger, int count, int position)
            => _logParallelKernelsAdded(logger, count, position, null);

        private static void LogBranchAdded(ILogger logger, int position)
            => _logBranchAdded(logger, position, null);

        private static void LogCacheAdded(ILogger logger, string stepId, string cacheKey)
            => _logCacheAdded(logger, stepId, cacheKey, null);

        private static void LogBackendSet(ILogger logger, string backend)
            => _logBackendSet(logger, backend, null);

        private static void LogAcceleratorSet(ILogger logger, string acceleratorId)
            => _logAcceleratorSet(logger, acceleratorId, null);

        private static void LogProfilingEnabled(ILogger logger, string profileName)
            => _logProfilingEnabled(logger, profileName, null);

        private static void LogTimeoutSet(ILogger logger, TimeSpan timeout)
            => _logTimeoutSet(logger, timeout, null);

        private static void LogErrorHandlerAdded(ILogger logger, int count)
            => _logErrorHandlerAdded(logger, count, null);

        private static void LogValidationSet(ILogger logger, bool validationEnabled)
            => _logValidationSet(logger, validationEnabled, null);

        private static void LogExecutionError(ILogger logger, Exception ex)
            => _logExecutionError(logger, ex);

        private static void LogProfilerStopError(ILogger logger, Exception ex)
            => _logProfilerStopError(logger, ex);

        private static void LogStepContinueAfterError(ILogger logger, string stepId, Exception ex)
            => _logStepContinueAfterError(logger, stepId, ex);

        private static void LogStepSkipAfterError(ILogger logger, string stepId, Exception ex)
            => _logStepSkipAfterError(logger, stepId, ex);

        private static void LogStepFallbackValue(ILogger logger, string stepId, Exception ex)
            => _logStepFallbackValue(logger, stepId, ex);

        private static void LogCachedResult(ILogger logger, string stepId)
            => _logCachedResult(logger, stepId, null);

        private static void LogErrorHandlerException(ILogger logger, Exception ex)
            => _logErrorHandlerException(logger, ex);

        private static void LogProfilerStopErrorDisposal(ILogger logger, Exception ex)
            => _logProfilerStopErrorDisposal(logger, ex);

        private static void LogBuilderDisposed(ILogger logger)
            => _logBuilderDisposed(logger, null);

        private static void LogDisposalError(ILogger logger, Exception ex)
            => _logDisposalError(logger, ex);
        private readonly IComputeOrchestrator _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        private readonly IKernelResolver? _kernelResolver = kernelResolver;
        private readonly IKernelChainProfiler? _profiler = profiler;
        private readonly IKernelChainValidator? _validator = validator;
        private readonly IKernelChainCacheService? _cacheService = cacheService;
        private readonly ILogger<KernelChainBuilder>? _logger = logger;

        private readonly List<KernelChainStep> _steps = [];
        private readonly Dictionary<string, object> _context = [];
        private readonly List<Func<Exception, ErrorHandlingStrategy>> _errorHandlers = [];

        private string? _preferredBackend;
        private IAccelerator? _preferredAccelerator;
        private bool _profilingEnabled;
        private string? _profileName;
        private TimeSpan? _timeout;
        private bool _validationEnabled = true;
        private bool _disposed;

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
            if (_logger != null)
            {
                LogKernelAdded(_logger, kernelName, _steps.Count - 1);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Then(string kernelName, params object[] args) => Kernel(kernelName, args);

        /// <inheritdoc/>
        public IKernelChainBuilder ThenExecute(string kernelName, params object[] args) => Kernel(kernelName, args);

        /// <inheritdoc/>
        public IKernelChainBuilder Parallel(params (string kernelName, object[] args)[] kernels)
        {
            ThrowIfDisposed();

            if (kernels == null || kernels.Length == 0)
            {

                throw new ArgumentException("At least one kernel must be specified for parallel execution", nameof(kernels));
            }


            var parallelStep = new KernelChainStep
            {
                Type = KernelChainStepType.Parallel,
                StepId = Guid.NewGuid().ToString(),
                ExecutionOrder = _steps.Count,
                ParallelKernels = [.. kernels.Select((k, i) => new KernelChainStep
                {
                    Type = KernelChainStepType.Sequential,
                    KernelName = k.kernelName,
                    Arguments = k.args,
                    StepId = Guid.NewGuid().ToString(),
                    ExecutionOrder = i
                })]
            };

            _steps.Add(parallelStep);
            if (_logger != null)
            {
                LogParallelKernelsAdded(_logger, kernels.Length, _steps.Count - 1);
            }

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
            _ = truePath(trueChain);

            KernelChainBuilder? falseChain = null;
            if (falsePath != null)
            {
                falseChain = new KernelChainBuilder(_orchestrator, _kernelResolver, _profiler, _validator, _cacheService, _logger);
                _ = falsePath(falseChain);
            }

            var branchCondition = new BranchCondition<T>
            {
                Condition = condition,
                TruePath = trueChain._steps,
                FalsePath = falseChain?._steps ?? []
            };

            var branchStep = new KernelChainStep
            {
                Type = KernelChainStepType.Branch,
                StepId = Guid.NewGuid().ToString(),
                ExecutionOrder = _steps.Count,
                BranchCondition = branchCondition
            };

            _steps.Add(branchStep);
            if (_logger != null)
            {
                LogBranchAdded(_logger, _steps.Count - 1);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder Cache(string key, TimeSpan? ttl = null)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(key))
            {

                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));
            }


            if (_steps.Count == 0)
            {

                throw new InvalidOperationException("Cannot add caching to empty chain. Add a kernel step first.");
            }


            var lastStep = _steps[^1];
            lastStep.CacheKey = key;
            lastStep.CacheTtl = ttl;

            if (_logger != null)
            {
                LogCacheAdded(_logger, lastStep.StepId, key);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder OnBackend(string backendName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(backendName))
            {

                throw new ArgumentException("Backend name cannot be null or whitespace", nameof(backendName));
            }


            _preferredBackend = backendName;
            _preferredAccelerator = null; // Clear accelerator preference when backend is set

            if (_logger != null)
            {
                LogBackendSet(_logger, backendName);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder OnAccelerator(IAccelerator accelerator)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(accelerator);


            _preferredAccelerator = accelerator;
            _preferredBackend = null; // Clear backend preference when accelerator is set

            if (_logger != null)
            {
                LogAcceleratorSet(_logger, accelerator.Info.Id);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder WithProfiling(string? profileName = null)
        {
            ThrowIfDisposed();

            _profilingEnabled = true;
            _profileName = profileName ?? string.Format(CultureInfo.InvariantCulture, "KernelChain_{0:N}", Guid.NewGuid());

            if (_logger != null)
            {
                LogProfilingEnabled(_logger, _profileName);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder WithTimeout(TimeSpan timeout)
        {
            ThrowIfDisposed();

            if (timeout <= TimeSpan.Zero)
            {

                throw new ArgumentException("Timeout must be positive", nameof(timeout));
            }


            _timeout = timeout;

            if (_logger != null)
            {
                LogTimeoutSet(_logger, timeout);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder OnError(Func<Exception, ErrorHandlingStrategy> errorHandler)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(errorHandler);

            _errorHandlers.Add(errorHandler);

            if (_logger != null)
            {
                LogErrorHandlerAdded(_logger, _errorHandlers.Count);
            }

            return this;
        }

        /// <inheritdoc/>
        public IKernelChainBuilder WithValidation(bool validateInputs = true)
        {
            ThrowIfDisposed();

            _validationEnabled = validateInputs;

            if (_logger != null)
            {
                LogValidationSet(_logger, _validationEnabled);
            }

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
            {
                return typedResult;
            }


            if (result.Result == null)
            {

                return default!;
            }

            // Attempt type conversion

            try
            {
                return (T)Convert.ChangeType(result.Result, typeof(T), CultureInfo.InvariantCulture);
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
            var usedBackend = "Unknown";

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
                if (_logger != null)
                {
                    LogExecutionError(_logger, ex);
                }
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
                        if (_logger != null)
                        {
                            LogProfilerStopError(_logger, ex);
                        }
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

            var validationResult = await _validator.ValidateChainAsync(_steps);
            return ConvertToInterfacesKernelChainValidationResult(validationResult);
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
                            if (_logger != null)
                            {
                                LogStepContinueAfterError(_logger, step.StepId, ex);
                            }
                            continue;
                        case ErrorHandlingStrategy.Skip:
                            if (_logger != null)
                            {
                                LogStepSkipAfterError(_logger, step.StepId, ex);
                            }
                            continue;
                        case ErrorHandlingStrategy.Abort:
                            errors.Add(ex);
                            throw;
                        case ErrorHandlingStrategy.Fallback:
                            currentResult = GetFallbackValue(step);
                            if (_logger != null)
                            {
                                LogStepFallbackValue(_logger, step.StepId, ex);
                            }
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
            var wasCached = false;
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
                        if (_logger != null)
                        {
                            LogCachedResult(_logger, step.StepId);
                        }
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
                    StepName = step.KernelName ?? $"{step.Type}Step",
                    StepIndex = step.ExecutionOrder,
                    ExecutionTime = stepStopwatch.Elapsed,
                    Success = true, // Add required Success property
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
            {

                throw new InvalidOperationException("Kernel name is required for sequential steps");
            }

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
            {

                return null;
            }

            var tasks = step.ParallelKernels.Select(async parallelStep =>
            {
                if (string.IsNullOrEmpty(parallelStep.KernelName))
                {
                    throw new InvalidOperationException("Kernel name is required for parallel steps");
                }


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
            {

                throw new InvalidOperationException("Branch condition is required for branch steps");
            }


            var stepMetrics = new List<KernelStepMetrics>();
            var errors = new List<Exception>();

            // Evaluate condition
            var conditionResult = step.BranchCondition.EvaluateCondition(previousResult);


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
                    if (_logger != null)
                    {
                        LogErrorHandlerException(_logger, handlerEx);
                    }
                }
            }

            return ErrorHandlingStrategy.Abort; // Default behavior
        }

        /// <summary>
        /// Gets a fallback value for a failed step.
        /// </summary>
        private static object? GetFallbackValue(KernelChainStep step)
            // This could be enhanced to support configurable fallback values

            => null;

        /// <summary>
        /// Determines the backend used for execution.
        /// </summary>
        private string DetermineUsedBackend()
        {
            if (_preferredAccelerator != null)
            {

                return _preferredAccelerator.Info.Name ?? _preferredAccelerator.GetType().Name;
            }


            if (!string.IsNullOrEmpty(_preferredBackend))
            {

                return _preferredBackend;
            }


            return "Auto";
        }

        /// <summary>
        /// Gets memory usage metrics for the execution.
        /// </summary>
        private static KernelChainMemoryMetrics? GetMemoryMetrics()
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
        /// Converts Results KernelChainValidationResult to Interfaces KernelChainValidationResult
        /// </summary>
        private static KernelChainValidationResult ConvertToInterfacesKernelChainValidationResult(AbstractionsMemory.Pipelines.Results.KernelChainValidationResult resultsValidation)
        {
            return new KernelChainValidationResult
            {
                IsValid = resultsValidation.IsValid,
                Errors = resultsValidation.Errors?.ToList() ?? [],
                Warnings = resultsValidation.Warnings?.ToList() ?? []
            };
        }

        /// <summary>
        /// Throws if the builder has been disposed.
        /// </summary>
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }


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
                        if (_logger != null)
                        {
                            LogProfilerStopErrorDisposal(_logger, ex);
                        }
                    }
                }

                _disposed = true;
                if (_logger != null)
                {
                    LogBuilderDisposed(_logger);
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogDisposalError(_logger, ex);
                }
                throw;
            }
        }
    }
}
