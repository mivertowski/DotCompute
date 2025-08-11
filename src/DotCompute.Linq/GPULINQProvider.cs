// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Core.Extensions;
using DotCompute.Core.Kernels;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using Microsoft.Extensions.Logging;
using ManagedCompiledKernel = DotCompute.Core.Kernels.ManagedCompiledKernel;

namespace DotCompute.Linq;

/// <summary>
/// Adapter to use ILogger GPULINQProvider as ILogger KernelManager.
/// </summary>
internal class KernelManagerLoggerWrapper : ILogger<KernelManager>
{
    private readonly ILogger<GPULINQProvider> _innerLogger;

    public KernelManagerLoggerWrapper(ILogger<GPULINQProvider> innerLogger)
    {
        _innerLogger = innerLogger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _innerLogger.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// Adapter for DefaultKernelFactory logger.
/// </summary>
internal class KernelFactoryLoggerWrapper : ILogger<DefaultKernelFactory>
{
    private readonly ILogger<GPULINQProvider> _innerLogger;

    public KernelFactoryLoggerWrapper(ILogger<GPULINQProvider> innerLogger)
    {
        _innerLogger = innerLogger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _innerLogger.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// Adapter for ExpressionToKernelCompiler logger.
/// </summary>
internal class ExpressionCompilerLoggerWrapper : ILogger<ExpressionToKernelCompiler>
{
    private readonly ILogger<GPULINQProvider> _innerLogger;

    public ExpressionCompilerLoggerWrapper(ILogger<GPULINQProvider> innerLogger)
    {
        _innerLogger = innerLogger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _innerLogger.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// GPU-accelerated LINQ provider that uses kernel infrastructure with dynamic compilation.
/// </summary>
public sealed partial class GPULINQProvider : IQueryProvider, IDisposable
{
    private readonly IAccelerator _accelerator;
    private readonly KernelManager _kernelManager;
    private readonly IExpressionToKernelCompiler _expressionCompiler;
    private readonly IExpressionOptimizer _optimizer;
    private readonly ILogger<GPULINQProvider> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GPULINQProvider"/> class.
    /// </summary>
    public GPULINQProvider(IAccelerator accelerator, ILogger<GPULINQProvider> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create wrapper logger for KernelManager
        var kernelLogger = new KernelManagerLoggerWrapper(logger);
        _kernelManager = new KernelManager(kernelLogger);
        
        // Initialize expression compilation pipeline
        _optimizer = new ExpressionOptimizer(logger);
        var kernelFactory = new DefaultKernelFactory(new KernelFactoryLoggerWrapper(logger));
        _expressionCompiler = new ExpressionToKernelCompiler(kernelFactory, _optimizer, 
            new ExpressionCompilerLoggerWrapper(logger));
    }

    /// <summary>
    /// Initializes a new instance with custom components for testing.
    /// </summary>
    internal GPULINQProvider(
        IAccelerator accelerator,
        IExpressionToKernelCompiler expressionCompiler,
        IExpressionOptimizer optimizer,
        ILogger<GPULINQProvider> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _expressionCompiler = expressionCompiler ?? throw new ArgumentNullException(nameof(expressionCompiler));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var kernelLogger = new KernelManagerLoggerWrapper(logger);
        _kernelManager = new KernelManager(kernelLogger);
    }

    /// <inheritdoc/>
    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        
        var elementType = GetElementType(expression.Type);
        var queryableType = typeof(GPUQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    /// <inheritdoc/>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new GPUQueryable<TElement>(this, expression);
    }

    /// <inheritdoc/>
    public object? Execute(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        
        // Try GPU execution first
        try
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            var task = ExecuteOnGPUAsync(expression, CancellationToken.None);
            if (task.IsCompleted)
            {
                return task.Result;
            }
            return task.AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        catch (Exception ex)
        {
            LogGPUExecutionFailed(ex.Message);
            
            // Fallback to CPU
            return ExecuteOnCPU(expression);
        }
    }

    /// <inheritdoc/>
    public TResult Execute<TResult>(Expression expression)
    {
        var result = Execute(expression);
        return (TResult)result!;
    }

    /// <summary>
    /// Executes an expression on the GPU asynchronously using dynamic kernel compilation.
    /// </summary>
    public async ValueTask<object?> ExecuteOnGPUAsync(Expression expression, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting GPU execution for expression: {ExpressionType}", expression.NodeType);
            
            // Check if expression can be compiled
            if (!_expressionCompiler.CanCompileExpression(expression))
            {
                LogExpressionNotGPUCompatible("Expression cannot be compiled to GPU kernel");
                return ExecuteOnCPU(expression);
            }

            // Get resource estimation
            var estimate = _expressionCompiler.EstimateResources(expression);
            _logger.LogDebug("Resource estimate - Memory: {Memory} bytes, Compilation time: {CompilationTime}", 
                estimate.EstimatedMemoryUsage, estimate.EstimatedCompilationTime);

            // Compile expression to kernel
            var compilationOptions = new CompilationOptions
            {
                EnableOperatorFusion = true,
                EnableMemoryCoalescing = true,
                EnableParallelExecution = true,
                MaxThreadsPerBlock = Math.Min(_accelerator.Info.MaxWorkGroupSize, 256)
            };

            var kernel = await _expressionCompiler.CompileExpressionAsync(
                expression, _accelerator, compilationOptions, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                // Prepare execution parameters
                var workItems = CalculateWorkItems(expression, _accelerator);
                var parameters = await PrepareExecutionParametersAsync(expression, cancellationToken)
                    .ConfigureAwait(false);

                // Execute the compiled kernel
                await kernel.ExecuteAsync(workItems, parameters, cancellationToken).ConfigureAwait(false);

                // Extract results
                var result = ExtractExecutionResult(parameters, expression);
                
                _logger.LogDebug("Successfully executed GPU kernel for expression: {ExpressionType}", expression.NodeType);
                return result;
            }
            finally
            {
                kernel.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPU execution failed for expression: {ExpressionType}", expression.NodeType);
            LogGPUExecutionFailed(ex.Message);
            return ExecuteOnCPU(expression);
        }
    }

    private WorkItems CalculateWorkItems(Expression expression, IAccelerator accelerator)
    {
        // Estimate work items based on expression complexity and data size
        var dataSize = EstimateDataSize(expression);
        var maxWorkGroupSize = accelerator.Info.MaxWorkGroupSize;
        
        // Calculate global work size based on data size
        var globalSize = Math.Max(1, (int)Math.Ceiling(dataSize / 4.0)); // Assume 4 bytes per element
        
        // Calculate local work size for optimal performance
        var localSize = Math.Min(256, maxWorkGroupSize);
        
        // Ensure global size is a multiple of local size
        globalSize = ((globalSize + localSize - 1) / localSize) * localSize;
        
        return new WorkItems
        {
            GlobalWorkSize = new[] { globalSize },
            LocalWorkSize = new[] { localSize }
        };
    }

    private static long EstimateDataSize(Expression expression)
    {
        // Simple heuristic to estimate data size
        var visitor = new DataSizeEstimator();
        visitor.Visit(expression);
        return Math.Max(1000, visitor.EstimatedSize); // Minimum 1000 elements
    }

    private async ValueTask<Dictionary<string, object>> PrepareExecutionParametersAsync(
        Expression expression,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object>();
        var visitor = new ParameterExtractor(parameters, _accelerator);
        
        visitor.Visit(expression);
        
        // Allocate output buffer based on expression type
        var outputSize = EstimateOutputSize(expression);
        var outputBuffer = await AllocateOutputBufferAsync(expression.Type, outputSize, cancellationToken)
            .ConfigureAwait(false);
        
        parameters["output"] = outputBuffer;
        parameters["outputSize"] = outputSize;
        
        return parameters;
    }

    private async ValueTask<IMemoryBuffer> AllocateOutputBufferAsync(Type outputType, long size, CancellationToken cancellationToken)
    {
        var elementSize = GetElementSize(outputType);
        var totalSize = size * elementSize;
        
        return await _accelerator.Memory.AllocateAsync(totalSize, MemoryOptions.None, cancellationToken)
            .ConfigureAwait(false);
    }

    private static long EstimateOutputSize(Expression expression)
    {
        // Estimate output size based on expression type
        return expression.NodeType switch
        {
            ExpressionType.Call when IsAggregateMethod(expression) => 1, // Single result
            ExpressionType.Call => EstimateDataSize(expression), // Same size as input
            _ => 1000 // Default size
        };
    }

    private static bool IsAggregateMethod(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            return methodName is "Sum" or "Average" or "Min" or "Max" or "Count";
        }
        return false;
    }

    private static object? ExtractExecutionResult(Dictionary<string, object> parameters, Expression expression)
    {
        if (!parameters.TryGetValue("output", out var outputBuffer))
        {
            return null;
        }

        // Handle different output types
        if (IsAggregateMethod(expression))
        {
            // For aggregate methods, return single value
            return ExtractSingleValue(outputBuffer, expression.Type);
        }
        else
        {
            // For array operations, return array
            return ExtractArrayResult(outputBuffer, expression.Type);
        }
    }

    private static object? ExtractSingleValue(object buffer, Type expectedType)
    {
        if (buffer is IMemoryBuffer memoryBuffer)
        {
            // Read single value from device memory
            var elementSize = GetElementSize(expectedType);
            var hostArray = Array.CreateInstance(expectedType, 1);
            
            // This would need to be implemented based on the actual memory buffer type
            // For now, return a default value
            return expectedType.IsValueType ? Activator.CreateInstance(expectedType) : null;
        }
        
        return buffer;
    }

    private static object? ExtractArrayResult(object buffer, Type expectedType)
    {
        if (buffer is IMemoryBuffer memoryBuffer)
        {
            // This would need proper implementation to read from device memory
            // For now, return empty array of the expected type
            return Array.CreateInstance(expectedType, 0);
        }
        
        return buffer;
    }


    private object? ExecuteOnCPU(Expression expression)
    {
        LogFallbackToCPU();
        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }

    private static Type GetElementType(Type type)
    {
        var queryableType = type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>));
        
        if (queryableType != null)
        {
            return queryableType.GetGenericArguments()[0];
        }

        if (typeof(IQueryable).IsAssignableFrom(type))
        {
            return typeof(object);
        }

        throw new ArgumentException($"Cannot determine element type for {type}");
    }

    private static int GetElementSize(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte => 1,
            TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => 8,
            _ => 4 // Default to 4 bytes
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _kernelManager.Dispose();
            _disposed = true;
        }
    }

    #region Logging

    private partial class Log
    {
        [LoggerMessage(1, LogLevel.Warning, "GPU execution failed: {Reason}. Falling back to CPU.")]
        public static partial void GPUExecutionFailed(ILogger logger, string reason);

        [LoggerMessage(2, LogLevel.Debug, "Expression not GPU compatible: {Reason}")]
        public static partial void ExpressionNotGPUCompatible(ILogger logger, string reason);

        [LoggerMessage(3, LogLevel.Debug, "Falling back to CPU execution")]
        public static partial void FallbackToCPU(ILogger logger);
    }

    private void LogGPUExecutionFailed(string reason) => Log.GPUExecutionFailed(_logger, reason);
    private void LogExpressionNotGPUCompatible(string reason) => Log.ExpressionNotGPUCompatible(_logger, reason);
    private void LogFallbackToCPU() => Log.FallbackToCPU(_logger);

    #endregion
}

/// <summary>
/// GPU-accelerated queryable implementation.
/// </summary>
public sealed class GPUQueryable<T> : IOrderedQueryable<T>
{
    private readonly GPULINQProvider _provider;
    private readonly Expression _expression;

    /// <summary>
    /// Initializes a new instance of the <see cref="GPUQueryable{T}"/> class.
    /// </summary>
    public GPUQueryable(GPULINQProvider provider, Expression expression)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <inheritdoc/>
    public Type ElementType => typeof(T);

    /// <inheritdoc/>
    public Expression Expression => _expression;

    /// <inheritdoc/>
    public IQueryProvider Provider => _provider;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        var result = _provider.Execute<IEnumerable<T>>(_expression);
        return result.GetEnumerator();
    }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Expression analysis result.
/// </summary>
internal sealed class ExpressionAnalysis
{
    public bool CanExecuteOnGPU { get; set; }
    public string? Reason { get; set; }
    public string? OperationType { get; set; }
    public Type[]? InputTypes { get; set; }
    public Type? OutputType { get; set; }
    public int EstimatedComplexity { get; set; }
}

/// <summary>
/// Expression visitor that analyzes GPU compatibility.
/// </summary>
internal sealed class GPUCompatibilityVisitor : ExpressionVisitor
{
    private readonly ExpressionAnalysis _analysis = new() { CanExecuteOnGPU = true };
    private readonly List<Type> _inputTypes = [];
    private int _complexity;

    public ExpressionAnalysis GetAnalysis()
    {
        _analysis.EstimatedComplexity = _complexity;
        _analysis.InputTypes = [.. _inputTypes];
        return _analysis;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _complexity++;
        
        // Check for LINQ methods
        if (node.Method.DeclaringType == typeof(Enumerable) || node.Method.DeclaringType == typeof(Queryable))
        {
            switch (node.Method.Name)
            {
                case "Select":
                    _analysis.OperationType = "Map";
                    break;
                case "Where":
                    _analysis.OperationType = "Filter";
                    break;
                case "Sum":
                case "Average":
                case "Min":
                case "Max":
                    _analysis.OperationType = "Reduce";
                    break;
                default:
                    _analysis.CanExecuteOnGPU = false;
                    _analysis.Reason = $"LINQ method '{node.Method.Name}' not supported on GPU";
                    break;
            }
        }
        
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _complexity++;
        
        // All basic binary operations are supported
        switch (node.NodeType)
        {
            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            case ExpressionType.Divide:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
                break;
            default:
                _analysis.CanExecuteOnGPU = false;
                _analysis.Reason = $"Binary operation '{node.NodeType}' not supported on GPU";
                break;
        }
        
        return base.VisitBinary(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value != null)
        {
            var type = node.Value.GetType();
            if (type.IsArray)
            {
                _inputTypes.Add(type);
            }
        }
        
        return base.VisitConstant(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _analysis.OutputType = node.ReturnType;
        return base.VisitLambda(node);
    }
}

/// <summary>
/// Visitor for extracting parameters and preparing kernel arguments.
/// </summary>
internal class ParameterExtractor : ExpressionVisitor
{
    private readonly Dictionary<string, object> _parameters;
    private readonly IAccelerator _accelerator;
    private int _parameterIndex = 0;

    public ParameterExtractor(Dictionary<string, object> parameters, IAccelerator accelerator)
    {
        _parameters = parameters;
        _accelerator = accelerator;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value != null)
        {
            var paramName = $"param_{_parameterIndex++}";
            _parameters[paramName] = node.Value;
            
            // If it's an array, we might need to copy to device memory
            if (node.Value is Array array)
            {
                // This would be implemented to properly handle device memory allocation
                _parameters[$"{paramName}_size"] = array.Length;
            }
        }
        return base.VisitConstant(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        // Handle parameter references
        var paramName = node.Name ?? $"param_{_parameterIndex++}";
        if (!_parameters.ContainsKey(paramName))
        {
            _parameters[paramName] = CreateDefaultValue(node.Type);
        }
        return base.VisitParameter(node);
    }

    private static object? CreateDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

/// <summary>
/// Visitor for estimating data size in expressions.
/// </summary>
internal class DataSizeEstimator : ExpressionVisitor
{
    public long EstimatedSize { get; private set; } = 0;

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is Array array)
        {
            EstimatedSize = Math.Max(EstimatedSize, array.Length);
        }
        else if (node.Value is ICollection collection)
        {
            EstimatedSize = Math.Max(EstimatedSize, collection.Count);
        }
        else if (node.Value is IQueryable queryable)
        {
            try
            {
                // Try to get count if possible
                var count = queryable.Cast<object>().Count();
                EstimatedSize = Math.Max(EstimatedSize, count);
            }
            catch
            {
                // Fallback to default size
                EstimatedSize = Math.Max(EstimatedSize, 1000);
            }
        }
        
        return base.VisitConstant(node);
    }
}