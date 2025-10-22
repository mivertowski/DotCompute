// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Enhanced compute orchestrator with integrated debugging capabilities.
/// Provides transparent debugging and validation without affecting normal execution flow.
/// </summary>
public partial class DebugIntegratedOrchestrator(
    IComputeOrchestrator baseOrchestrator,
    IKernelDebugService debugService,
    ILogger<DebugIntegratedOrchestrator> logger,
    DebugExecutionOptions? options = null) : IComputeOrchestrator, IDisposable
{
    private readonly IComputeOrchestrator _baseOrchestrator = baseOrchestrator ?? throw new ArgumentNullException(nameof(baseOrchestrator));
    private readonly IKernelDebugService _debugService = debugService ?? throw new ArgumentNullException(nameof(debugService));
    private readonly ILogger<DebugIntegratedOrchestrator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DebugExecutionOptions _options = options ?? new DebugExecutionOptions();
    private bool _disposed;
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (!_options.EnableDebugHooks)
        {
            return await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
        }

        var result = await ExecuteWithDebugHooksAsync<T>(kernelName, args);
        return result!;
    }
    /// <summary>
    /// Gets execute with buffers asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="buffers">The buffers.</param>
    /// <param name="scalarArgs">The scalar args.</param>
    /// <returns>The result of the operation.</returns>


    public async Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (!_options.EnableDebugHooks)
        {
            return await _baseOrchestrator.ExecuteWithBuffersAsync<T>(kernelName, buffers, scalarArgs);
        }

        var allArgs = buffers.Concat(scalarArgs).ToArray();
        var result = await ExecuteWithDebugHooksAsync<T>(kernelName, allArgs);
        return result!;
    }
    /// <summary>
    /// Gets the optimal accelerator async.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>The optimal accelerator async.</returns>

    public async Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.GetOptimalAcceleratorAsync(kernelName);
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="preferredBackend">The preferred backend.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.EnableDebugHooks)
        {
            return await _baseOrchestrator.ExecuteAsync<T>(kernelName, preferredBackend, args);
        }

        var result = await ExecuteWithDebugHooksAsync<T>(kernelName, args);
        return result!;
    }
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.EnableDebugHooks)
        {
            return await _baseOrchestrator.ExecuteAsync<T>(kernelName, accelerator, args);
        }

        var result = await ExecuteWithDebugHooksAsync<T>(kernelName, args);
        return result!;
    }
    /// <summary>
    /// Gets precompile kernel asynchronously.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="accelerator">The accelerator.</param>
    /// <returns>The result of the operation.</returns>

    public async Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _baseOrchestrator.PrecompileKernelAsync(kernelName, accelerator);
    }
    /// <summary>
    /// Gets the supported accelerators async.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>The supported accelerators async.</returns>

    public async Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.GetSupportedAcceleratorsAsync(kernelName);
    }

    private async Task<T?> ExecuteWithDebugHooksAsync<T>(string kernelName, object[] args)
    {
        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        LogDebugExecutionStarting(_logger, kernelName, executionId);

        try
        {
            // Pre-execution validation (if enabled)
            ValidationResult? preValidation = null;
            if (_options.ValidateBeforeExecution)
            {
                preValidation = await ValidateKernelPreExecutionAsync(kernelName, args);
                if (preValidation.HasCriticalIssues)
                {
                    LogPreExecutionValidationFailed(_logger, kernelName, string.Join(", ", preValidation.Issues));


                    if (_options.FailOnValidationErrors)
                    {
                        throw new InvalidOperationException(
                            $"Kernel {kernelName} failed pre-execution validation: {preValidation.Issues.First()}");
                    }
                }
            }

            // Execute the kernel
            var executionStart = stopwatch.Elapsed;
            var result = await _baseOrchestrator.ExecuteAsync<T>(kernelName, args);
            var executionTime = stopwatch.Elapsed - executionStart;

            LogKernelExecuted(_logger, kernelName, executionTime.TotalMilliseconds, executionId);

            // Post-execution validation (if enabled)
            if (_options.ValidateAfterExecution)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ValidateKernelPostExecutionAsync(kernelName, args, result, executionId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogPostExecutionValidationError(_logger, ex, kernelName);
                    }
                });
            }

            // Cross-backend validation (if enabled)
            if (_options.EnableCrossBackendValidation && _options.CrossValidationProbability > 0)
            {
                // Determine if we should perform cross-validation
#pragma warning disable CA5394 // Random is used for probabilistic sampling in debugging, not security
                var shouldValidate = Random.Shared.NextDouble() < _options.CrossValidationProbability;
#pragma warning restore CA5394
                if (shouldValidate)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await PerformCrossBackendValidationAsync(kernelName, args, executionId).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogCrossBackendValidationError(_logger, ex, kernelName);
                        }
                    });
                }
            }

            // Performance monitoring (if enabled)
            if (_options.EnablePerformanceMonitoring)
            {
                await LogPerformanceMetricsAsync(kernelName, executionTime, args.Length, executionId);
            }

            return result;
        }
        catch (Exception ex)
        {
            LogDebugExecutionError(_logger, ex, kernelName, executionId);

            // Error analysis (if enabled)
            if (_options.AnalyzeErrorsOnFailure)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await AnalyzeExecutionErrorAsync(kernelName, args, ex, executionId).ConfigureAwait(false);
                    }
                    catch (Exception analysisEx)
                    {
                        LogErrorAnalysisError(_logger, analysisEx, kernelName);
                    }
                });
            }

            throw;
        }
        finally
        {
            LogDebugExecutionCompleted(_logger, kernelName, stopwatch.Elapsed.TotalMilliseconds, executionId);
        }
    }

    private async Task<ValidationResult> ValidateKernelPreExecutionAsync(string kernelName, object[] args)
    {
        try
        {
            // Quick validation - check if kernel can be executed on multiple backends
            var backends = await _debugService.GetAvailableBackendsAsync();
            var availableBackends = backends.Where(b => b.IsAvailable).ToList();

            var issues = new List<string>();


            if (availableBackends.Count == 0)
            {
                issues.Add("No backends available for execution");
            }

            // Check for common argument issues

            if (args.Any(arg => arg == null))
            {
                issues.Add("Null arguments detected");
            }

            // Validate memory requirements
            var estimatedMemory = EstimateMemoryRequirements(args);
            if (estimatedMemory > 1024 * 1024 * 1024) // 1GB threshold
            {
                issues.Add($"High memory usage estimated: {estimatedMemory / (1024 * 1024)}MB");
            }

            return new ValidationResult
            {
                Issues = issues,
                HasCriticalIssues = issues.Any(i => i.Contains("No backends", StringComparison.OrdinalIgnoreCase) || i.Contains("Null arguments", StringComparison.OrdinalIgnoreCase))
            };
        }
        catch (Exception ex)
        {
            LogPreExecutionValidationWarning(_logger, ex, kernelName);
            return new ValidationResult
            {
                Issues = [$"Validation error: {ex.Message}"],
                HasCriticalIssues = false
            };
        }
    }

    private async Task ValidateKernelPostExecutionAsync<T>(string kernelName, object[] args, T? result, Guid executionId)
    {
        try
        {
            LogPostExecutionValidationStarting(_logger, kernelName, executionId);

            // Validate result is not null when expected
            if (typeof(T) != typeof(object) && result == null)
            {
                LogNullResultWarning(_logger, kernelName, executionId);
            }

            // Check for determinism (if configured)
            if (_options.TestDeterminism)
            {
                var determinismReport = await _debugService.ValidateDeterminismAsync(kernelName, args, 3);
                if (!determinismReport.IsDeterministic)
                {
                    LogNonDeterministicBehavior(_logger, kernelName, determinismReport.NonDeterminismSource ?? "Unknown", executionId);
                }
            }
        }
        catch (Exception ex)
        {
            LogPostExecutionValidationWarning(_logger, ex, kernelName, executionId);
        }
    }

    private async Task PerformCrossBackendValidationAsync(string kernelName, object[] args, Guid executionId)
    {
        try
        {
            LogCrossBackendValidationStarting(_logger, kernelName, executionId);

            var validationResult = await _debugService.ValidateKernelAsync(kernelName, args, _options.ValidationTolerance);

            if (!validationResult.IsValid)
            {
                var criticalIssues = validationResult.Issues.Where(i => i.Severity == ValidationSeverity.Critical);
                var errorIssues = validationResult.Issues.Where(i => i.Severity == ValidationSeverity.Error);

                if (criticalIssues.Any())
                {
                    LogCriticalCrossBackendIssues(_logger, kernelName, string.Join(", ", criticalIssues.Select(i => i.Message)), executionId);
                }
                else if (errorIssues.Any())
                {
                    LogCrossBackendValidationErrors(_logger, kernelName, string.Join(", ", errorIssues.Select(i => i.Message)), executionId);
                }
            }
            else
            {
                LogCrossBackendValidationPassed(_logger, kernelName, validationResult.RecommendedBackend ?? "Unknown", executionId);
            }
        }
        catch (Exception ex)
        {
            LogCrossBackendValidationWarning(_logger, ex, kernelName, executionId);
        }
    }

    private async Task LogPerformanceMetricsAsync(string kernelName, TimeSpan executionTime, int argCount, Guid executionId)
    {
        try
        {
            var metricsData = new
            {
                KernelName = kernelName,
                ExecutionId = executionId,
                ExecutionTimeMs = executionTime.TotalMilliseconds,
                ArgumentCount = argCount,
                Timestamp = DateTimeOffset.UtcNow
            };

            LogPerformanceMetrics(_logger, kernelName, metricsData.ToString() ?? string.Empty, executionId);

            // Store metrics for trend analysis (if configured)
            if (_options.StorePerformanceHistory)
            {
                // This would integrate with a metrics storage system in production
                await Task.CompletedTask; // Placeholder
            }
        }
        catch (Exception ex)
        {
            LogPerformanceMetricsWarning(_logger, ex, kernelName, executionId);
        }
    }

    private async Task AnalyzeExecutionErrorAsync(string kernelName, object[] args, Exception error, Guid executionId)
    {
        try
        {
            LogErrorAnalysisStarting(_logger, kernelName, executionId);

            var errorAnalysis = new
            {
                KernelName = kernelName,
                ExecutionId = executionId,
                ErrorType = error.GetType().Name,
                ErrorMessage = error.Message,
                ArgumentCount = args.Length,
                ArgumentTypes = args.Select(a => a?.GetType().Name ?? "null").ToArray(),
                StackTrace = error.StackTrace,
                Timestamp = DateTimeOffset.UtcNow
            };

            LogErrorAnalysis(_logger, kernelName, errorAnalysis.ToString() ?? string.Empty, executionId);

            // Attempt to determine if error is kernel-specific or systemic
            var backends = await _debugService.GetAvailableBackendsAsync();
            var availableCount = backends.Count(b => b.IsAvailable);

            if (availableCount == 0)
            {
                LogSystemBackendFailure(_logger, executionId);
            }
        }
        catch (Exception ex)
        {
            LogErrorAnalysisWarning(_logger, ex, kernelName, executionId);
        }
    }

    private static long EstimateMemoryRequirements(object[] args)
        // Simplified memory estimation

        => args.Length * 1024; // 1KB per argument as baseline

    /// <inheritdoc />
    public async Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {
        // Delegate to base orchestrator for validation
        if (_baseOrchestrator != null)
        {
            return await _baseOrchestrator.ValidateKernelArgsAsync(kernelName, args);
        }

        // Basic validation - just check if kernel exists and args are not null

        return !string.IsNullOrEmpty(kernelName) && args != null;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteKernelAsync(string kernelName, IKernelExecutionParameters executionParameters)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.EnableDebugHooks)
        {
            return await _baseOrchestrator.ExecuteKernelAsync(kernelName, executionParameters);
        }

        var args = executionParameters.Arguments?.ToArray() ?? [];
        var result = await ExecuteWithDebugHooksAsync<object>(kernelName, args);
        return result;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteKernelAsync(string kernelName, object[] args, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.EnableDebugHooks)
        {
            return await _baseOrchestrator.ExecuteKernelAsync(kernelName, args, cancellationToken);
        }

        var result = await ExecuteWithDebugHooksAsync<object>(kernelName, args);
        return result;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources
            (_baseOrchestrator as IDisposable)?.Dispose();
            (_debugService as IDisposable)?.Dispose();
        }

        _disposed = true;
    }

    private class ValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether sues.
        /// </summary>
        /// <value>The issues.</value>
        public IList<string> Issues { get; init; } = [];
        /// <summary>
        /// Gets or sets a value indicating whether critical issues.
        /// </summary>
        /// <value>The has critical issues.</value>
        public bool HasCriticalIssues { get; set; }
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 11200,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Starting debug-enhanced execution of {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogDebugExecutionStarting(ILogger logger, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11201,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Pre-execution validation failed for {KernelName}: {Issues}")]
    public static partial void LogPreExecutionValidationFailed(ILogger logger, string kernelName, string issues);

    [LoggerMessage(
        EventId = 11202,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Kernel {KernelName} executed in {ExecutionTimeMs}ms [ID: {ExecutionId}]")]
    public static partial void LogKernelExecuted(ILogger logger, string kernelName, double executionTimeMs, Guid executionId);

    [LoggerMessage(
        EventId = 11203,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error during kernel post-execution validation for {KernelName}")]
    public static partial void LogPostExecutionValidationError(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 11204,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error during cross-backend validation for {KernelName}")]
    public static partial void LogCrossBackendValidationError(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 11205,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error during debug-enhanced execution of {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogDebugExecutionError(ILogger logger, Exception ex, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11206,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error during execution error analysis for {KernelName}")]
    public static partial void LogErrorAnalysisError(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 11207,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Debug-enhanced execution of {KernelName} completed in {TotalTimeMs}ms [ID: {ExecutionId}]")]
    public static partial void LogDebugExecutionCompleted(ILogger logger, string kernelName, double totalTimeMs, Guid executionId);

    [LoggerMessage(
        EventId = 11208,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Error during pre-execution validation for {KernelName}")]
    public static partial void LogPreExecutionValidationWarning(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 11209,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Starting post-execution validation for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogPostExecutionValidationStarting(ILogger logger, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11210,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Kernel {KernelName} returned null result [ID: {ExecutionId}]")]
    public static partial void LogNullResultWarning(ILogger logger, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11211,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Non-deterministic behavior detected in {KernelName}: {NonDeterminismSource} [ID: {ExecutionId}]")]
    public static partial void LogNonDeterministicBehavior(ILogger logger, string kernelName, string nonDeterminismSource, Guid executionId);

    [LoggerMessage(
        EventId = 11212,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Error during post-execution validation for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogPostExecutionValidationWarning(ILogger logger, Exception ex, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11213,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Starting cross-backend validation for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogCrossBackendValidationStarting(ILogger logger, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11214,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Critical cross-backend validation issues for {KernelName}: {Issues} [ID: {ExecutionId}]")]
    public static partial void LogCriticalCrossBackendIssues(ILogger logger, string kernelName, string issues, Guid executionId);

    [LoggerMessage(
        EventId = 11215,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Cross-backend validation errors for {KernelName}: {Issues} [ID: {ExecutionId}]")]
    public static partial void LogCrossBackendValidationErrors(ILogger logger, string kernelName, string issues, Guid executionId);

    [LoggerMessage(
        EventId = 11216,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Cross-backend validation passed for {KernelName} (recommended: {Backend}) [ID: {ExecutionId}]")]
    public static partial void LogCrossBackendValidationPassed(ILogger logger, string kernelName, string backend, Guid executionId);

    [LoggerMessage(
        EventId = 11217,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Error during cross-backend validation for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogCrossBackendValidationWarning(ILogger logger, Exception ex, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11218,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Performance metrics for {KernelName}: {MetricsData} [ID: {ExecutionId}]")]
    public static partial void LogPerformanceMetrics(ILogger logger, string kernelName, string metricsData, Guid executionId);

    [LoggerMessage(
        EventId = 11219,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Error logging performance metrics for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogPerformanceMetricsWarning(ILogger logger, Exception ex, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11220,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Starting error analysis for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogErrorAnalysisStarting(ILogger logger, string kernelName, Guid executionId);

    [LoggerMessage(
        EventId = 11221,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Error analysis for {KernelName}: {ErrorAnalysis} [ID: {ExecutionId}]")]
    public static partial void LogErrorAnalysis(ILogger logger, string kernelName, string errorAnalysis, Guid executionId);

    [LoggerMessage(
        EventId = 11222,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "System-wide backend failure detected during error analysis [ID: {ExecutionId}]")]
    public static partial void LogSystemBackendFailure(ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 11223,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Error during error analysis for {KernelName} [ID: {ExecutionId}]")]
    public static partial void LogErrorAnalysisWarning(ILogger logger, Exception ex, string kernelName, Guid executionId);

    #endregion
}

/// <summary>
/// Configuration options for debug-enhanced execution.
/// </summary>
public class DebugExecutionOptions
{
    /// <summary>
    /// Whether to enable debug hooks during kernel execution.
    /// </summary>
    public bool EnableDebugHooks { get; set; } = true;

    /// <summary>
    /// Whether to validate kernels before execution.
    /// </summary>
    public bool ValidateBeforeExecution { get; set; } = true;

    /// <summary>
    /// Whether to validate kernels after execution.
    /// </summary>
    public bool ValidateAfterExecution { get; set; }

    /// <summary>
    /// Whether to fail execution if validation errors occur.
    /// </summary>
    public bool FailOnValidationErrors { get; set; }

    /// <summary>
    /// Whether to enable cross-backend validation.
    /// </summary>
    public bool EnableCrossBackendValidation { get; set; }

    /// <summary>
    /// Probability (0-1) of performing cross-backend validation.
    /// </summary>
    public double CrossValidationProbability { get; set; } = 0.1; // 10% of executions

    /// <summary>
    /// Tolerance for cross-backend validation comparisons.
    /// </summary>
    public float ValidationTolerance { get; set; } = 1e-6f;

    /// <summary>
    /// Whether to enable performance monitoring.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Whether to store performance history for trend analysis.
    /// </summary>
    public bool StorePerformanceHistory { get; set; }

    /// <summary>
    /// Whether to test for deterministic behavior.
    /// </summary>
    public bool TestDeterminism { get; set; }

    /// <summary>
    /// Whether to analyze errors when execution fails.
    /// </summary>
    public bool AnalyzeErrorsOnFailure { get; set; } = true;
}
