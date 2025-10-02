// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Enhanced compute orchestrator with integrated debugging capabilities.
/// Provides transparent debugging and validation without affecting normal execution flow.
/// </summary>
public class DebugIntegratedOrchestrator : IComputeOrchestrator, IDisposable
{
    private readonly IComputeOrchestrator _baseOrchestrator;
    private readonly IKernelDebugService _debugService;
    private readonly ILogger<DebugIntegratedOrchestrator> _logger;
    private readonly DebugExecutionOptions _options;
    private bool _disposed;

    public DebugIntegratedOrchestrator(
        IComputeOrchestrator baseOrchestrator,
        IKernelDebugService debugService,
        ILogger<DebugIntegratedOrchestrator> logger,
        DebugExecutionOptions? options = null)
    {
        _baseOrchestrator = baseOrchestrator ?? throw new ArgumentNullException(nameof(baseOrchestrator));
        _debugService = debugService ?? throw new ArgumentNullException(nameof(debugService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new DebugExecutionOptions();
    }

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

    public async Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.GetOptimalAcceleratorAsync(kernelName);
    }

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

    public async Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _baseOrchestrator.PrecompileKernelAsync(kernelName, accelerator);
    }

    public async Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _baseOrchestrator.GetSupportedAcceleratorsAsync(kernelName);
    }

    private async Task<T?> ExecuteWithDebugHooksAsync<T>(string kernelName, object[] args)
    {
        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();


        _logger.LogDebugMessage($"Starting debug-enhanced execution of {kernelName} [ID: {executionId}]");

        try
        {
            // Pre-execution validation (if enabled)
            ValidationResult? preValidation = null;
            if (_options.ValidateBeforeExecution)
            {
                preValidation = await ValidateKernelPreExecution(kernelName, args);
                if (preValidation.HasCriticalIssues)
                {
                    _logger.LogError("Pre-execution validation failed for {KernelName}: {Issues}",

                        kernelName, string.Join(", ", preValidation.Issues));


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

            _logger.LogDebugMessage($"Kernel {kernelName} executed in {executionTime.TotalMilliseconds}ms [ID: {executionId}]");

            // Post-execution validation (if enabled)
            if (_options.ValidateAfterExecution)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ValidateKernelPostExecution(kernelName, args, result, executionId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during kernel post-execution validation for {KernelName}", kernelName);
                    }
                });
            }

            // Cross-backend validation (if enabled)
            if (_options.EnableCrossBackendValidation && _options.CrossValidationProbability > 0)
            {
                var shouldValidate = new Random().NextDouble() < _options.CrossValidationProbability;
                if (shouldValidate)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await PerformCrossBackendValidation(kernelName, args, executionId).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during cross-backend validation for {KernelName}", kernelName);
                        }
                    });
                }
            }

            // Performance monitoring (if enabled)
            if (_options.EnablePerformanceMonitoring)
            {
                await LogPerformanceMetrics(kernelName, executionTime, args.Length, executionId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error during debug-enhanced execution of {kernelName} [ID: {executionId}]");

            // Error analysis (if enabled)
            if (_options.AnalyzeErrorsOnFailure)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await AnalyzeExecutionError(kernelName, args, ex, executionId).ConfigureAwait(false);
                    }
                    catch (Exception analysisEx)
                    {
                        _logger.LogError(analysisEx, "Error during execution error analysis for {KernelName}", kernelName);
                    }
                });
            }

            throw;
        }
        finally
        {
            _logger.LogTrace("Debug-enhanced execution of {KernelName} completed in {TotalTime}ms [ID: {ExecutionId}]",

                kernelName, stopwatch.Elapsed.TotalMilliseconds, executionId);
        }
    }

    private async Task<ValidationResult> ValidateKernelPreExecution(string kernelName, object[] args)
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
                HasCriticalIssues = issues.Any(i => i.Contains("No backends") || i.Contains("Null arguments"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during pre-execution validation for {KernelName}", kernelName);
            return new ValidationResult
            {

                Issues = [$"Validation error: {ex.Message}"],
                HasCriticalIssues = false

            };
        }
    }

    private async Task ValidateKernelPostExecution<T>(string kernelName, object[] args, T? result, Guid executionId)
    {
        try
        {
            _logger.LogTrace("Starting post-execution validation for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);

            // Validate result is not null when expected
            if (typeof(T) != typeof(object) && result == null)
            {
                _logger.LogWarningMessage($"Kernel {kernelName} returned null result [ID: {executionId}]");
            }

            // Check for determinism (if configured)
            if (_options.TestDeterminism)
            {
                var determinismReport = await _debugService.ValidateDeterminismAsync(kernelName, args, 3);
                if (!determinismReport.IsDeterministic)
                {
                    _logger.LogWarningMessage($"Non-deterministic behavior detected in {kernelName}: {determinismReport.NonDeterminismSource} [ID: {executionId}]");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during post-execution validation for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);
        }
    }

    private async Task PerformCrossBackendValidation(string kernelName, object[] args, Guid executionId)
    {
        try
        {
            _logger.LogTrace("Starting cross-backend validation for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);

            var validationResult = await _debugService.ValidateKernelAsync(kernelName, args, _options.ValidationTolerance);


            if (!validationResult.IsValid)
            {
                var criticalIssues = validationResult.Issues.Where(i => i.Severity == ValidationSeverity.Critical);
                var errorIssues = validationResult.Issues.Where(i => i.Severity == ValidationSeverity.Error);


                if (criticalIssues.Any())
                {
                    _logger.LogError("Critical cross-backend validation issues for {KernelName}: {Issues} [ID: {ExecutionId}]",

                        kernelName, string.Join(", ", criticalIssues.Select(i => i.Message)), executionId);
                }
                else if (errorIssues.Any())
                {
                    _logger.LogWarningMessage($"Cross-backend validation errors for {kernelName}: {string.Join(", ", errorIssues.Select(i => i.Message))} [ID: {executionId}]");
                }
            }
            else
            {
                _logger.LogTrace("Cross-backend validation passed for {KernelName} (recommended: {Backend}) [ID: {ExecutionId}]",

                    kernelName, validationResult.RecommendedBackend, executionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during cross-backend validation for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);
        }
    }

    private async Task LogPerformanceMetrics(string kernelName, TimeSpan executionTime, int argCount, Guid executionId)
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

            _logger.LogInfoMessage($"Performance metrics for {kernelName}: {metricsData} [ID: {executionId}]");

            // Store metrics for trend analysis (if configured)
            if (_options.StorePerformanceHistory)
            {
                // This would integrate with a metrics storage system in production
                await Task.CompletedTask; // Placeholder
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error logging performance metrics for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);
        }
    }

    private async Task AnalyzeExecutionError(string kernelName, object[] args, Exception error, Guid executionId)
    {
        try
        {
            _logger.LogTrace("Starting error analysis for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);

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

            _logger.LogInfoMessage($"Error analysis for {kernelName}: {errorAnalysis} [ID: {executionId}]");

            // Attempt to determine if error is kernel-specific or systemic
            var backends = await _debugService.GetAvailableBackendsAsync();
            var availableCount = backends.Count(b => b.IsAvailable);


            if (availableCount == 0)
            {
                _logger.LogWarningMessage("System-wide backend failure detected during error analysis [ID: {executionId}]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during error analysis for {KernelName} [ID: {ExecutionId}]",

                kernelName, executionId);
        }
    }

    private static long EstimateMemoryRequirements(object[] args)
    {
        // Simplified memory estimation
        return args.Length * 1024; // 1KB per argument as baseline
    }

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

        var result = await ExecuteWithDebugHooksAsync<object>(kernelName, executionParameters.Arguments);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        (_baseOrchestrator as IDisposable)?.Dispose();
        (_debugService as IDisposable)?.Dispose();


        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class ValidationResult
    {
        public List<string> Issues { get; set; } = [];
        public bool HasCriticalIssues { get; set; }
    }
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
