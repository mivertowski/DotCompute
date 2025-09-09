// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Analysis;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Interfaces;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;

namespace DotCompute.Linq.Providers;

/// <summary>
/// Production implementation of IComputeLinqProvider that fully integrates with the runtime orchestrator.
/// This is the primary entry point for LINQ-to-GPU functionality.
/// </summary>
public class RuntimeIntegratedLinqProvider : IComputeLinqProvider
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly IntegratedComputeQueryProvider _queryProvider;
    private readonly IExpressionOptimizer _optimizer;
    private readonly GPUCompatibilityVisitor _compatibilityVisitor;
    private readonly ILogger<RuntimeIntegratedLinqProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeIntegratedLinqProvider"/> class.
    /// </summary>
    /// <param name="orchestrator">The compute orchestrator.</param>
    /// <param name="queryProvider">The integrated query provider.</param>
    /// <param name="optimizer">The expression optimizer.</param>
    /// <param name="logger">The logger instance.</param>
    public RuntimeIntegratedLinqProvider(
        IComputeOrchestrator orchestrator,
        IntegratedComputeQueryProvider queryProvider,
        IExpressionOptimizer optimizer,
        ILogger<RuntimeIntegratedLinqProvider> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _queryProvider = queryProvider ?? throw new ArgumentNullException(nameof(queryProvider));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compatibilityVisitor = new GPUCompatibilityVisitor();
    }

    /// <inheritdoc />
    public IQueryable<T> CreateQueryable<T>(IEnumerable<T> source, IAccelerator? accelerator = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        _logger.LogDebugMessage("Creating queryable for IEnumerable<{ElementType}>", typeof(T).Name);

        // If accelerator is specified, we might need to create a specialized provider
        if (accelerator != null)
        {
            _logger.LogDebugMessage("Creating queryable with specific accelerator: {AcceleratorType}", accelerator.Info.DeviceType);
            return new AcceleratorSpecificQueryable<T>(_queryProvider, source, accelerator);
        }

        return new IntegratedComputeQueryable<T>(_queryProvider, source);
    }

    /// <inheritdoc />
    public IQueryable<T> CreateQueryable<T>(T[] source, IAccelerator? accelerator = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        _logger.LogDebugMessage("Creating queryable for array of {ElementType} with {Length} elements", typeof(T).Name, source.Length);

        // Arrays can be optimized for GPU memory transfer
        if (accelerator != null)
        {
            return new AcceleratorSpecificQueryable<T>(_queryProvider, source, accelerator);
        }

        return new IntegratedComputeQueryable<T>(_queryProvider, source);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebugMessage("Executing expression asynchronously via orchestrator");

        try
        {
            return await _queryProvider.ExecuteAsync<T>(expression);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to execute LINQ expression");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Expression expression, IAccelerator preferredAccelerator)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(preferredAccelerator);

        _logger.LogDebugMessage("Executing expression with preferred accelerator: {AcceleratorType}", preferredAccelerator.Info.DeviceType);

        try
        {
            return await _queryProvider.ExecuteAsync<T>(expression, preferredAccelerator.Info.DeviceType);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to execute LINQ expression with preferred accelerator");
            throw;
        }
    }

    /// <inheritdoc />
    public IEnumerable<Interfaces.OptimizationSuggestion> GetOptimizationSuggestions(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebugMessage("Analyzing expression for optimization suggestions");

        var suggestions = new List<Interfaces.OptimizationSuggestion>();

        try
        {
            // Analyze expression structure
            var analysis = ExpressionAnalysis.AnalyzeComplexity(expression);
            
            // Check for CPU-bound operations
            if (!_compatibilityVisitor.IsGpuCompatible(expression))
            {
                suggestions.Add(new Interfaces.OptimizationSuggestion
                {
                    Category = "GPU Compatibility",
                    Message = "Expression contains operations that are not GPU-compatible and will fall back to CPU execution",
                    Severity = Interfaces.SuggestionSeverity.Warning,
                    EstimatedImpact = 0.3
                });
            }

            // Check for data size estimation
            var dataSizeEstimator = new DataSizeEstimator();
            var estimatedSize = dataSizeEstimator.EstimateDataSize(expression);
            
            if (estimatedSize < 1000) // Less than 1K elements
            {
                suggestions.Add(new Interfaces.OptimizationSuggestion
                {
                    Category = "Data Size",
                    Message = "Small dataset may not benefit from GPU acceleration due to overhead",
                    Severity = Interfaces.SuggestionSeverity.Info,
                    EstimatedImpact = -0.2
                });
            }
            else if (estimatedSize > 1000000) // More than 1M elements
            {
                suggestions.Add(new Interfaces.OptimizationSuggestion
                {
                    Category = "Memory",
                    Message = "Large dataset may benefit from memory pooling and streaming",
                    Severity = Interfaces.SuggestionSeverity.High,
                    EstimatedImpact = 0.4
                });
            }

            // Check for operation fusion opportunities
            if (analysis.ChainLength > 2)
            {
                suggestions.Add(new Interfaces.OptimizationSuggestion
                {
                    Category = "Operation Fusion",
                    Message = "Multiple chained operations detected. Consider operation fusion for better performance",
                    Severity = Interfaces.SuggestionSeverity.High,
                    EstimatedImpact = 0.25
                });
            }

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage("Failed to analyze expression for optimization suggestions: {Error}", ex.Message);
            return suggestions; // Return what we have
        }
    }

    /// <inheritdoc />
    public bool IsGpuCompatible(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            return _compatibilityVisitor.IsGpuCompatible(expression);
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage("GPU compatibility check failed: {Error}", ex.Message);
            return false; // Assume not compatible if analysis fails
        }
    }

    /// <inheritdoc />
    public async Task PrecompileExpressionsAsync(IEnumerable<Expression> expressions)
    {
        ArgumentNullException.ThrowIfNull(expressions);

        var expressionList = expressions.ToList();
        _logger.LogInformation("Pre-compiling {Count} expressions", expressionList.Count);

        var precompileTasks = expressionList.Select(async expression =>
        {
            try
            {
                // Get the kernels that would be used for this expression
                var translator = new LinqToKernelTranslator(_logger);
                var kernelOperations = translator.TranslateToKernelOperations(expression);

                // Pre-compile each kernel operation
                foreach (var operation in kernelOperations)
                {
                    await _orchestrator.PrecompileKernelAsync(operation.KernelName);
                }

                _logger.LogDebugMessage("Pre-compiled expression with {OperationCount} kernel operations", kernelOperations.Count());
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage("Failed to pre-compile expression: {Error}", ex.Message);
            }
        });

        await Task.WhenAll(precompileTasks);
        _logger.LogInformation("Completed pre-compilation of expressions");
    }
}

/// <summary>
/// Queryable implementation that is bound to a specific accelerator.
/// </summary>
/// <typeparam name="T">The element type</typeparam>
public class AcceleratorSpecificQueryable<T> : IntegratedComputeQueryable<T>
{
    private readonly IAccelerator _accelerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcceleratorSpecificQueryable{T}"/> class.
    /// </summary>
    /// <param name="provider">The query provider</param>
    /// <param name="source">The data source</param>
    /// <param name="accelerator">The specific accelerator to use</param>
    public AcceleratorSpecificQueryable(
        IntegratedComputeQueryProvider provider, 
        IEnumerable<T> source,
        IAccelerator accelerator)
        : base(provider, source)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <summary>
    /// Gets the specific accelerator bound to this queryable.
    /// </summary>
    public IAccelerator BoundAccelerator => _accelerator;

    /// <summary>
    /// Executes the query on the bound accelerator.
    /// </summary>
    /// <returns>The query result</returns>
    public async Task<IEnumerable<T>> ExecuteOnBoundAcceleratorAsync()
    {
        var provider = (IntegratedComputeQueryProvider)Provider;
        return await provider.ExecuteAsync<IEnumerable<T>>(Expression, _accelerator.Info.DeviceType);
    }
}

// ExpressionComplexityAnalysis is defined in Analysis/ExpressionAnalysis.cs
