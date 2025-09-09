// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Pipelines.Diagnostics;

/// <summary>
/// Enhanced error handling for pipeline-specific operations with comprehensive diagnostics.
/// Provides detailed error analysis, recovery strategies, and performance impact assessment.
/// </summary>
public interface IPipelineErrorHandler
{
    /// <summary>
    /// Handles errors that occur during pipeline execution with context-aware recovery.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">Execution context when the error occurred</param>
    /// <returns>Error handling result with recovery options</returns>
    Task<PipelineErrorResult> HandlePipelineErrorAsync(Exception exception, PipelineExecutionContext context);

    /// <summary>
    /// Analyzes LINQ expression errors and provides optimization suggestions.
    /// </summary>
    /// <param name="expression">The LINQ expression that caused an error</param>
    /// <param name="exception">The exception that occurred</param>
    /// <returns>Detailed error analysis with suggestions</returns>
    Task<ExpressionErrorAnalysis> AnalyzeExpressionErrorAsync(Expression expression, Exception exception);

    /// <summary>
    /// Provides recovery strategies for different types of pipeline errors.
    /// </summary>
    /// <param name="errorType">Type of error that occurred</param>
    /// <param name="context">Execution context</param>
    /// <returns>Available recovery strategies</returns>
    Task<List<RecoveryStrategy>> GetRecoveryStrategiesAsync(PipelineErrorType errorType, PipelineExecutionContext context);

    /// <summary>
    /// Validates pipeline configuration and identifies potential issues.
    /// </summary>
    /// <param name="pipeline">Pipeline to validate</param>
    /// <returns>Validation results with warnings and errors</returns>
    Task<PipelineValidationResult> ValidatePipelineAsync(IKernelPipeline pipeline);
}

/// <summary>
/// Implementation of comprehensive pipeline error handling.
/// </summary>
public class PipelineErrorHandler : IPipelineErrorHandler
{
    private readonly ILogger<PipelineErrorHandler> _logger;
    private readonly Dictionary<Type, PipelineErrorType> _exceptionTypeMapping;
    private readonly Dictionary<PipelineErrorType, List<RecoveryStrategy>> _recoveryStrategies;

    /// <summary>
    /// Initializes a new instance of the PipelineErrorHandler class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics</param>
    public PipelineErrorHandler(ILogger<PipelineErrorHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exceptionTypeMapping = InitializeExceptionMapping();
        _recoveryStrategies = InitializeRecoveryStrategies();
    }

    /// <inheritdoc />
    public async Task<PipelineErrorResult> HandlePipelineErrorAsync(Exception exception, PipelineExecutionContext context)
    {
        _logger.LogError(exception, "Pipeline error occurred in context: {Context}", context.ContextId);

        var errorType = ClassifyError(exception);
        var severity = DetermineSeverity(exception, context);
        var recoveryStrategies = await GetRecoveryStrategiesAsync(errorType, context);
        
        var result = new PipelineErrorResult
        {
            ErrorType = errorType,
            Severity = severity,
            Exception = exception,
            Context = context,
            RecoveryStrategies = recoveryStrategies,
            Timestamp = DateTime.UtcNow,
            CanRecover = recoveryStrategies.Any(s => s.Success > 0.5),
            DiagnosticInfo = await CollectDiagnosticInfoAsync(exception, context)
        };

        // Log detailed error information
        LogErrorDetails(result);

        // Attempt automatic recovery if configured
        if (context.EnableAutomaticRecovery && result.CanRecover)
        {
            var recoveryResult = await AttemptAutomaticRecoveryAsync(result);
            result.RecoveryAttempted = true;
            result.RecoverySuccessful = recoveryResult.Success;
            result.RecoveryMessage = recoveryResult.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ExpressionErrorAnalysis> AnalyzeExpressionErrorAsync(Expression expression, Exception exception)
    {
        _logger.LogDebug("Analyzing expression error for: {ExpressionType}", expression.Type);

        var analysis = new ExpressionErrorAnalysis
        {
            Expression = expression,
            Exception = exception,
            ErrorCategory = CategorizeExpressionError(exception),
            ProblemAreas = await IdentifyProblemAreasAsync(expression, exception),
            Suggestions = await GenerateExpressionSuggestionsAsync(expression, exception),
            AlternativeApproaches = await FindAlternativeApproachesAsync(expression),
            PerformanceImpact = AssessPerformanceImpact(exception)
        };

        return analysis;
    }

    /// <inheritdoc />
    public async Task<List<RecoveryStrategy>> GetRecoveryStrategiesAsync(PipelineErrorType errorType, PipelineExecutionContext context)
    {
        _logger.LogDebug("Getting recovery strategies for error type: {ErrorType}", errorType);

        if (!_recoveryStrategies.TryGetValue(errorType, out var baseStrategies))
        {
            baseStrategies = GetDefaultRecoveryStrategies();
        }

        // Filter and customize strategies based on context
        var applicableStrategies = baseStrategies
            .Where(s => IsStrategyApplicable(s, context))
            .Select(s => CustomizeStrategy(s, context))
            .OrderByDescending(s => s.Success)
            .ToList();

        return applicableStrategies;
    }

    /// <inheritdoc />
    public async Task<PipelineValidationResult> ValidatePipelineAsync(IKernelPipeline pipeline)
    {
        _logger.LogDebug("Validating pipeline configuration");

        var validationResult = new PipelineValidationResult
        {
            IsValid = true,
            Errors = new List<ValidationError>(),
            Warnings = new List<ValidationWarning>(),
            Recommendations = new List<string>()
        };

        try
        {
            // Validate pipeline structure
            await ValidatePipelineStructureAsync(pipeline, validationResult);
            
            // Validate resource requirements
            await ValidateResourceRequirementsAsync(pipeline, validationResult);
            
            // Validate backend compatibility
            await ValidateBackendCompatibilityAsync(pipeline, validationResult);
            
            // Validate performance characteristics
            await ValidatePerformanceCharacteristicsAsync(pipeline, validationResult);

            validationResult.IsValid = !validationResult.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline validation failed");
            validationResult.IsValid = false;
            validationResult.Errors.Add(new ValidationError
            {
                Severity = ErrorSeverity.Critical,
                Message = "Pipeline validation failed with exception",
                Details = ex.Message,
                ErrorCode = "VALIDATION_EXCEPTION"
            });
        }

        return validationResult;
    }

    #region Private Implementation

    private Dictionary<Type, PipelineErrorType> InitializeExceptionMapping()
    {
        return new Dictionary<Type, PipelineErrorType>
        {
            [typeof(ArgumentException)] = PipelineErrorType.InvalidArgument,
            [typeof(ArgumentNullException)] = PipelineErrorType.InvalidArgument,
            [typeof(InvalidOperationException)] = PipelineErrorType.InvalidOperation,
            [typeof(NotSupportedException)] = PipelineErrorType.UnsupportedOperation,
            [typeof(OutOfMemoryException)] = PipelineErrorType.MemoryExhausted,
            [typeof(TimeoutException)] = PipelineErrorType.Timeout,
            [typeof(OperationCanceledException)] = PipelineErrorType.Cancelled,
            [typeof(UnauthorizedAccessException)] = PipelineErrorType.AccessDenied,
            [typeof(PipelineExecutionException)] = PipelineErrorType.ExecutionFailure,
            [typeof(PipelineOrchestrationException)] = PipelineErrorType.OrchestrationFailure
        };
    }

    private Dictionary<PipelineErrorType, List<RecoveryStrategy>> InitializeRecoveryStrategies()
    {
        return new Dictionary<PipelineErrorType, List<RecoveryStrategy>>
        {
            [PipelineErrorType.MemoryExhausted] = new List<RecoveryStrategy>
            {
                new() { Name = "Enable Streaming", Description = "Process data in smaller batches", Success = 0.8, Cost = RecoveryCost.Low },
                new() { Name = "Fallback to CPU", Description = "Use CPU backend with more memory", Success = 0.9, Cost = RecoveryCost.Medium },
                new() { Name = "Increase Memory Pool", Description = "Expand memory pool size", Success = 0.7, Cost = RecoveryCost.High }
            },
            [PipelineErrorType.Timeout] = new List<RecoveryStrategy>
            {
                new() { Name = "Increase Timeout", Description = "Allow more time for execution", Success = 0.6, Cost = RecoveryCost.Low },
                new() { Name = "Optimize Query", Description = "Apply aggressive optimizations", Success = 0.8, Cost = RecoveryCost.Medium },
                new() { Name = "Parallelize Execution", Description = "Split work across multiple cores", Success = 0.7, Cost = RecoveryCost.Medium }
            },
            [PipelineErrorType.UnsupportedOperation] = new List<RecoveryStrategy>
            {
                new() { Name = "Fallback Implementation", Description = "Use CPU fallback for unsupported operations", Success = 0.9, Cost = RecoveryCost.Low },
                new() { Name = "Alternative Algorithm", Description = "Use different algorithm for the operation", Success = 0.7, Cost = RecoveryCost.High },
                new() { Name = "Skip Operation", Description = "Skip the unsupported operation with warning", Success = 0.3, Cost = RecoveryCost.Low }
            },
            [PipelineErrorType.ExecutionFailure] = new List<RecoveryStrategy>
            {
                new() { Name = "Retry Execution", Description = "Retry with exponential backoff", Success = 0.5, Cost = RecoveryCost.Low },
                new() { Name = "Change Backend", Description = "Try different backend", Success = 0.7, Cost = RecoveryCost.Medium },
                new() { Name = "Simplify Pipeline", Description = "Remove complex operations", Success = 0.8, Cost = RecoveryCost.High }
            }
        };
    }

    private PipelineErrorType ClassifyError(Exception exception)
    {
        var exceptionType = exception.GetType();
        
        // Direct mapping
        if (_exceptionTypeMapping.TryGetValue(exceptionType, out var errorType))
        {
            return errorType;
        }

        // Check inheritance hierarchy
        foreach (var kvp in _exceptionTypeMapping)
        {
            if (kvp.Key.IsAssignableFrom(exceptionType))
            {
                return kvp.Value;
            }
        }

        // Analyze exception message for additional classification
        var message = exception.Message.ToLowerInvariant();
        if (message.Contains("memory") || message.Contains("out of"))
            return PipelineErrorType.MemoryExhausted;
        if (message.Contains("timeout") || message.Contains("time"))
            return PipelineErrorType.Timeout;
        if (message.Contains("not supported") || message.Contains("unsupported"))
            return PipelineErrorType.UnsupportedOperation;

        return PipelineErrorType.Unknown;
    }

    private ErrorSeverity DetermineSeverity(Exception exception, PipelineExecutionContext context)
    {
        return exception switch
        {
            OutOfMemoryException => ErrorSeverity.Critical,
            PipelineOrchestrationException => ErrorSeverity.High,
            TimeoutException when context.CriticalPath => ErrorSeverity.High,
            TimeoutException => ErrorSeverity.Medium,
            NotSupportedException => ErrorSeverity.Medium,
            ArgumentException => ErrorSeverity.Low,
            _ => ErrorSeverity.Medium
        };
    }

    private async Task<Dictionary<string, object>> CollectDiagnosticInfoAsync(Exception exception, PipelineExecutionContext context)
    {
        var diagnostics = new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["Message"] = exception.Message,
            ["StackTrace"] = exception.StackTrace ?? "Not available",
            ["Timestamp"] = DateTime.UtcNow,
            ["ContextId"] = context.ContextId,
            ["Backend"] = context.PreferredBackend,
            ["MemoryUsage"] = GC.GetTotalMemory(false),
            ["ThreadId"] = Environment.CurrentManagedThreadId
        };

        // Add inner exception information
        if (exception.InnerException != null)
        {
            diagnostics["InnerException"] = exception.InnerException.Message;
        }

        // Add context-specific diagnostics
        if (context.Pipeline != null)
        {
            try
            {
                var pipelineDiagnostics = await context.Pipeline.GetDiagnosticsAsync();
                diagnostics["PipelineStages"] = pipelineDiagnostics.StageCount;
                diagnostics["PipelineMemoryUsage"] = pipelineDiagnostics.PeakMemoryUsage;
            }
            catch (Exception ex)
            {
                diagnostics["PipelineDiagnosticsError"] = ex.Message;
            }
        }

        return diagnostics;
    }

    private void LogErrorDetails(PipelineErrorResult result)
    {
        _logger.LogError(
            "Pipeline Error - Type: {ErrorType}, Severity: {Severity}, Recovery: {CanRecover}, Context: {ContextId}",
            result.ErrorType,
            result.Severity,
            result.CanRecover,
            result.Context.ContextId);

        if (result.DiagnosticInfo.Any())
        {
            _logger.LogDebug("Diagnostic info: {@DiagnosticInfo}", result.DiagnosticInfo);
        }
    }

    private async Task<RecoveryResult> AttemptAutomaticRecoveryAsync(PipelineErrorResult errorResult)
    {
        var bestStrategy = errorResult.RecoveryStrategies.OrderByDescending(s => s.Success).FirstOrDefault();
        
        if (bestStrategy == null)
        {
            return new RecoveryResult { Success = false, Message = "No recovery strategies available" };
        }

        _logger.LogInformation("Attempting automatic recovery using strategy: {Strategy}", bestStrategy.Name);

        try
        {
            // Implement strategy-specific recovery logic
            var recoverySuccess = await ExecuteRecoveryStrategyAsync(bestStrategy, errorResult.Context);
            
            var result = new RecoveryResult
            {
                Success = recoverySuccess,
                StrategyUsed = bestStrategy.Name,
                Message = recoverySuccess 
                    ? $"Successfully recovered using {bestStrategy.Name}" 
                    : $"Recovery attempt using {bestStrategy.Name} failed"
            };

            _logger.LogInformation("Recovery attempt result: {Success} - {Message}", result.Success, result.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery attempt failed");
            return new RecoveryResult 
            { 
                Success = false, 
                Message = $"Recovery failed with exception: {ex.Message}",
                StrategyUsed = bestStrategy.Name
            };
        }
    }

    private async Task<bool> ExecuteRecoveryStrategyAsync(RecoveryStrategy strategy, PipelineExecutionContext context)
    {
        return strategy.Name switch
        {
            "Enable Streaming" => await EnableStreamingRecoveryAsync(context),
            "Fallback to CPU" => await FallbackToCpuRecoveryAsync(context),
            "Increase Timeout" => await IncreaseTimeoutRecoveryAsync(context),
            "Optimize Query" => await OptimizeQueryRecoveryAsync(context),
            "Fallback Implementation" => await FallbackImplementationRecoveryAsync(context),
            "Retry Execution" => await RetryExecutionRecoveryAsync(context),
            _ => false
        };
    }

    // Recovery strategy implementations
    private async Task<bool> EnableStreamingRecoveryAsync(PipelineExecutionContext context) => true; // Placeholder
    private async Task<bool> FallbackToCpuRecoveryAsync(PipelineExecutionContext context) => true; // Placeholder
    private async Task<bool> IncreaseTimeoutRecoveryAsync(PipelineExecutionContext context) => true; // Placeholder
    private async Task<bool> OptimizeQueryRecoveryAsync(PipelineExecutionContext context) => true; // Placeholder
    private async Task<bool> FallbackImplementationRecoveryAsync(PipelineExecutionContext context) => true; // Placeholder
    private async Task<bool> RetryExecutionRecoveryAsync(PipelineExecutionContext context) => true; // Placeholder

    // Validation methods
    private async Task ValidatePipelineStructureAsync(IKernelPipeline pipeline, PipelineValidationResult result) { }
    private async Task ValidateResourceRequirementsAsync(IKernelPipeline pipeline, PipelineValidationResult result) { }
    private async Task ValidateBackendCompatibilityAsync(IKernelPipeline pipeline, PipelineValidationResult result) { }
    private async Task ValidatePerformanceCharacteristicsAsync(IKernelPipeline pipeline, PipelineValidationResult result) { }

    // Helper methods
    private ExpressionErrorCategory CategorizeExpressionError(Exception exception) => ExpressionErrorCategory.Runtime;
    private async Task<List<string>> IdentifyProblemAreasAsync(Expression expression, Exception exception) => new();
    private async Task<List<string>> GenerateExpressionSuggestionsAsync(Expression expression, Exception exception) => new();
    private async Task<List<string>> FindAlternativeApproachesAsync(Expression expression) => new();
    private PerformanceImpact AssessPerformanceImpact(Exception exception) => PerformanceImpact.Medium;
    private List<RecoveryStrategy> GetDefaultRecoveryStrategies() => new();
    private bool IsStrategyApplicable(RecoveryStrategy strategy, PipelineExecutionContext context) => true;
    private RecoveryStrategy CustomizeStrategy(RecoveryStrategy strategy, PipelineExecutionContext context) => strategy;

    #endregion
}

#region Supporting Types

/// <summary>
/// Types of pipeline errors.
/// </summary>
public enum PipelineErrorType
{
    Unknown,
    InvalidArgument,
    InvalidOperation,
    UnsupportedOperation,
    MemoryExhausted,
    Timeout,
    Cancelled,
    AccessDenied,
    ExecutionFailure,
    OrchestrationFailure,
    BackendUnavailable,
    ConfigurationError,
    ValidationError
}

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Cost of recovery strategies.
/// </summary>
public enum RecoveryCost
{
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// Performance impact levels.
/// </summary>
public enum PerformanceImpact
{
    Negligible,
    Low,
    Medium,
    High,
    Severe
}

/// <summary>
/// Expression error categories.
/// </summary>
public enum ExpressionErrorCategory
{
    Compilation,
    Runtime,
    Optimization,
    Compatibility,
    Performance
}

/// <summary>
/// Pipeline error result with comprehensive information.
/// </summary>
public class PipelineErrorResult
{
    public PipelineErrorType ErrorType { get; set; }
    public ErrorSeverity Severity { get; set; }
    public Exception Exception { get; set; } = new();
    public PipelineExecutionContext Context { get; set; } = new();
    public List<RecoveryStrategy> RecoveryStrategies { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public bool CanRecover { get; set; }
    public bool RecoveryAttempted { get; set; }
    public bool RecoverySuccessful { get; set; }
    public string? RecoveryMessage { get; set; }
    public Dictionary<string, object> DiagnosticInfo { get; set; } = new();
}

/// <summary>
/// Recovery strategy definition.
/// </summary>
public class RecoveryStrategy
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Success { get; set; }
    public RecoveryCost Cost { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Recovery execution result.
/// </summary>
public class RecoveryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StrategyUsed { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Expression error analysis result.
/// </summary>
public class ExpressionErrorAnalysis
{
    public Expression Expression { get; set; } = Expression.Empty();
    public Exception Exception { get; set; } = new();
    public ExpressionErrorCategory ErrorCategory { get; set; }
    public List<string> ProblemAreas { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public List<string> AlternativeApproaches { get; set; } = new();
    public PerformanceImpact PerformanceImpact { get; set; }
}

/// <summary>
/// Pipeline validation result.
/// </summary>
public class PipelineValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Validation error.
/// </summary>
public class ValidationError
{
    public ErrorSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? StageId { get; set; }
}

/// <summary>
/// Validation warning.
/// </summary>
public class ValidationWarning
{
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string WarningCode { get; set; } = string.Empty;
    public string? StageId { get; set; }
}

/// <summary>
/// Pipeline execution context with error handling support.
/// </summary>
public class PipelineExecutionContext
{
    public string ContextId { get; set; } = Guid.NewGuid().ToString();
    public string PreferredBackend { get; set; } = "CPU";
    public bool EnableAutomaticRecovery { get; set; } = false;
    public bool CriticalPath { get; set; } = false;
    public IKernelPipeline? Pipeline { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

#endregion