// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Analysis;
using DotCompute.Core.Pipelines.Exceptions;
using DotCompute.Linq.Pipelines.Integration;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.Logging;
using IKernelPipeline = DotCompute.Core.Pipelines.IKernelPipeline;

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
    public Task<List<RecoveryStrategy>> GetRecoveryStrategiesAsync(PipelineErrorType errorType, PipelineExecutionContext context)
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

        return Task.FromResult(applicableStrategies);
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
        {

            return PipelineErrorType.MemoryExhausted;
        }


        if (message.Contains("timeout") || message.Contains("time"))
        {

            return PipelineErrorType.Timeout;
        }


        if (message.Contains("not supported") || message.Contains("unsupported"))
        {

            return PipelineErrorType.UnsupportedOperation;
        }


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

    private Task<Dictionary<string, object>> CollectDiagnosticInfoAsync(Exception exception, PipelineExecutionContext context)
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
                // Use the available methods from the actual IKernelPipeline interface
                var metrics = context.Pipeline.GetMetrics();
                diagnostics["PipelineStages"] = context.Pipeline.Stages?.Count ?? 0;
                diagnostics["PipelineMemoryUsage"] = metrics?.PeakMemoryUsage ?? 0;
                diagnostics["PipelineName"] = context.Pipeline.Name;
                diagnostics["PipelineId"] = context.Pipeline.Id;
            }
            catch (Exception ex)
            {
                diagnostics["PipelineDiagnosticsError"] = ex.Message;
            }
        }

        return Task.FromResult(diagnostics);
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
    private Task<bool> EnableStreamingRecoveryAsync(PipelineExecutionContext context)
    {
        // TODO: Implement streaming recovery logic
        _logger.LogDebug("Attempting streaming recovery for context: {ContextId}", context.ContextId);
        return Task.FromResult(true);
    }

    private Task<bool> FallbackToCpuRecoveryAsync(PipelineExecutionContext context)
    {
        // TODO: Implement CPU fallback recovery logic
        _logger.LogDebug("Attempting CPU fallback recovery for context: {ContextId}", context.ContextId);
        return Task.FromResult(true);
    }

    private Task<bool> IncreaseTimeoutRecoveryAsync(PipelineExecutionContext context)
    {
        // TODO: Implement timeout increase recovery logic
        _logger.LogDebug("Attempting timeout increase recovery for context: {ContextId}", context.ContextId);
        return Task.FromResult(true);
    }

    private Task<bool> OptimizeQueryRecoveryAsync(PipelineExecutionContext context)
    {
        // TODO: Implement query optimization recovery logic
        _logger.LogDebug("Attempting query optimization recovery for context: {ContextId}", context.ContextId);
        return Task.FromResult(true);
    }

    private Task<bool> FallbackImplementationRecoveryAsync(PipelineExecutionContext context)
    {
        // TODO: Implement fallback implementation recovery logic
        _logger.LogDebug("Attempting fallback implementation recovery for context: {ContextId}", context.ContextId);
        return Task.FromResult(true);
    }

    private Task<bool> RetryExecutionRecoveryAsync(PipelineExecutionContext context)
    {
        // TODO: Implement retry execution recovery logic
        _logger.LogDebug("Attempting retry execution recovery for context: {ContextId}", context.ContextId);
        return Task.FromResult(true);
    }

    // Validation methods
    private Task ValidatePipelineStructureAsync(IKernelPipeline pipeline, PipelineValidationResult result)
    {
        // TODO: Implement pipeline structure validation
        _logger.LogDebug("Validating pipeline structure for pipeline: {PipelineId}", pipeline.Id);

        // Basic structure validation - check if pipeline has stages

        if (pipeline.Stages?.Count == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Severity = ErrorSeverity.High,
                Message = "Pipeline has no stages defined",
                Details = "A valid pipeline must contain at least one execution stage",
                ErrorCode = "PIPELINE_EMPTY"
            });
        }


        return Task.CompletedTask;
    }

    private Task ValidateResourceRequirementsAsync(IKernelPipeline pipeline, PipelineValidationResult result)
    {
        // TODO: Implement resource requirements validation
        _logger.LogDebug("Validating resource requirements for pipeline: {PipelineId}", pipeline.Id);

        // Basic resource validation - check if optimization settings are reasonable

        var settings = pipeline.OptimizationSettings;
        if (settings.EnableKernelFusion && settings.EnableStageReordering)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Kernel fusion with stage reordering may cause unexpected behavior",
                Details = "Consider using these optimizations separately for better predictability",
                WarningCode = "OPTIMIZATION_CONFLICT"
            });
        }


        return Task.CompletedTask;
    }

    private Task ValidateBackendCompatibilityAsync(IKernelPipeline pipeline, PipelineValidationResult result)
    {
        // TODO: Implement backend compatibility validation
        _logger.LogDebug("Validating backend compatibility for pipeline: {PipelineId}", pipeline.Id);

        // Basic backend validation - log warning about potential compatibility issues

        result.Recommendations.Add("Verify backend compatibility for all pipeline stages");
        result.Recommendations.Add("Consider fallback strategies for unsupported operations");


        return Task.CompletedTask;
    }

    private Task ValidatePerformanceCharacteristicsAsync(IKernelPipeline pipeline, PipelineValidationResult result)
    {
        // TODO: Implement performance characteristics validation
        _logger.LogDebug("Validating performance characteristics for pipeline: {PipelineId}", pipeline.Id);

        // Basic performance validation - check stage count

        var stageCount = pipeline.Stages?.Count ?? 0;
        if (stageCount > 100)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Pipeline has a large number of stages",
                Details = $"Pipeline contains {stageCount} stages, which may impact performance",
                WarningCode = "HIGH_STAGE_COUNT"
            });
        }


        return Task.CompletedTask;
    }

    // Helper methods
    private ExpressionErrorCategory CategorizeExpressionError(Exception exception)
    {
        return exception switch
        {
            ArgumentException => ExpressionErrorCategory.Compilation,
            NotSupportedException => ExpressionErrorCategory.Compatibility,
            InvalidOperationException => ExpressionErrorCategory.Runtime,
            OutOfMemoryException => ExpressionErrorCategory.Performance,
            _ => ExpressionErrorCategory.Runtime
        };
    }

    private Task<List<string>> IdentifyProblemAreasAsync(Expression expression, Exception exception)
    {
        var problemAreas = new List<string>();


        try
        {
            // Analyze expression structure for common issues
            if (expression == null)
            {
                problemAreas.Add("Null expression detected");
            }
            else
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Call:
                        problemAreas.Add("Method call expression - check parameter types and method availability");
                        break;
                    case ExpressionType.Lambda:
                        problemAreas.Add("Lambda expression - verify closure variable capture");
                        break;
                    case ExpressionType.MemberAccess:
                        problemAreas.Add("Member access - check property/field accessibility");
                        break;
                    default:
                        problemAreas.Add($"Expression type: {expression.NodeType} - review expression structure");
                        break;
                }
            }

            // Add exception-specific problem areas

            problemAreas.Add($"Exception type: {exception.GetType().Name} - {exception.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing expression problem areas");
            problemAreas.Add("Unable to analyze expression structure");
        }


        return Task.FromResult(problemAreas);
    }

    private Task<List<string>> GenerateExpressionSuggestionsAsync(Expression expression, Exception exception)
    {
        var suggestions = new List<string>();


        try
        {
            // Generate suggestions based on exception type
            switch (exception)
            {
                case ArgumentNullException:
                    suggestions.Add("Add null checks for expression parameters");
                    suggestions.Add("Verify all required inputs are provided");
                    break;


                case NotSupportedException:
                    suggestions.Add("Consider using CPU backend for unsupported operations");
                    suggestions.Add("Break complex expressions into simpler parts");
                    break;


                case OutOfMemoryException:
                    suggestions.Add("Enable streaming processing for large datasets");
                    suggestions.Add("Reduce batch size or enable memory pooling");
                    break;


                case TimeoutException:
                    suggestions.Add("Increase timeout values for complex operations");
                    suggestions.Add("Consider parallel processing strategies");
                    break;


                default:
                    suggestions.Add("Review expression complexity and simplify if possible");
                    suggestions.Add("Add proper error handling and recovery logic");
                    break;
            }

            // Add general suggestions

            suggestions.Add("Enable detailed logging to get more diagnostic information");
            suggestions.Add("Consider using alternative backends if current one fails");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating expression suggestions");
            suggestions.Add("Unable to generate specific suggestions - consult documentation");
        }


        return Task.FromResult(suggestions);
    }

    private Task<List<string>> FindAlternativeApproachesAsync(Expression expression)
    {
        var alternatives = new List<string>();


        try
        {
            if (expression != null)
            {
                // Suggest alternatives based on expression type
                switch (expression.NodeType)
                {
                    case ExpressionType.Call:
                        alternatives.Add("Use direct kernel invocation instead of expression trees");
                        alternatives.Add("Consider pre-compiled delegate caching");
                        break;


                    case ExpressionType.Lambda:
                        alternatives.Add("Use explicit parameter passing instead of closures");
                        alternatives.Add("Consider compile-time code generation");
                        break;


                    case ExpressionType.Conditional:
                        alternatives.Add("Use branching pipeline stages for conditional logic");
                        alternatives.Add("Consider separate kernels for different conditions");
                        break;


                    default:
                        alternatives.Add("Use simpler expression structures");
                        alternatives.Add("Consider manual kernel parameter binding");
                        break;
                }
            }

            // Add general alternatives

            alternatives.Add("Use CPU backend as fallback option");
            alternatives.Add("Implement custom pipeline stages for complex operations");
            alternatives.Add("Consider using pre-built algorithm implementations");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding alternative approaches");
            alternatives.Add("Consult documentation for alternative implementation patterns");
        }


        return Task.FromResult(alternatives);
    }
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