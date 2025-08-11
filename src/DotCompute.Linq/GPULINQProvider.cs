// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Core.Extensions;
using DotCompute.Core.Kernels;
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
/// GPU-accelerated LINQ provider that uses kernel infrastructure.
/// </summary>
public sealed partial class GPULINQProvider : IQueryProvider, IDisposable
{
    private readonly IAccelerator _accelerator;
    private readonly KernelManager _kernelManager;
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
    /// Executes an expression on the GPU asynchronously.
    /// </summary>
    public async ValueTask<object?> ExecuteOnGPUAsync(Expression expression, CancellationToken cancellationToken)
    {
        // Analyze expression to determine operation type
        var analysis = AnalyzeExpression(expression);
        
        if (!analysis.CanExecuteOnGPU)
        {
            LogExpressionNotGPUCompatible(analysis.Reason ?? "Unknown");
            return ExecuteOnCPU(expression);
        }

        // Generate and compile kernel
        var context = new KernelGenerationContext
        {
            DeviceInfo = _accelerator.Info,
            UseSharedMemory = true,
            UseVectorTypes = true,
            Precision = PrecisionMode.Single
        };

        ManagedCompiledKernel managedKernel;
        if (analysis.OperationType != null)
        {
            // Use predefined operation kernel
            managedKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
                analysis.OperationType,
                analysis.InputTypes ?? [],
                analysis.OutputType ?? typeof(object),
                _accelerator,
                context,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Generate custom kernel from expression
            managedKernel = await _kernelManager.GetOrCompileKernelAsync(
                expression,
                _accelerator,
                context,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        // Prepare kernel arguments
        var arguments = await PrepareKernelArgumentsAsync(expression, analysis, cancellationToken).ConfigureAwait(false);

        // Execute kernel
        var result = await _kernelManager.ExecuteKernelAsync(
            managedKernel,
            arguments,
            _accelerator,
            null,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Kernel execution failed: {result.ErrorMessage}");
        }

        // Extract and return results
        return await ExtractResultsAsync(arguments, analysis, cancellationToken).ConfigureAwait(false);
    }

    private static ExpressionAnalysis AnalyzeExpression(Expression expression)
    {
        var visitor = new GPUCompatibilityVisitor();
        visitor.Visit(expression);
        return visitor.GetAnalysis();
    }

    private async ValueTask<KernelArgument[]> PrepareKernelArgumentsAsync(
        Expression expression, 
        ExpressionAnalysis analysis,
        CancellationToken cancellationToken)
    {
        var arguments = new List<KernelArgument>();

        // Extract data from expression
        if (expression is MethodCallExpression methodCall)
        {
            foreach (var arg in methodCall.Arguments)
            {
                if (arg is ConstantExpression constant && constant.Value != null)
                {
                    var kernelArg = await CreateKernelArgumentAsync(constant.Value, cancellationToken).ConfigureAwait(false);
                    arguments.Add(kernelArg);
                }
            }
        }

        return [.. arguments];
    }

    private async ValueTask<KernelArgument> CreateKernelArgumentAsync(object value, CancellationToken cancellationToken)
    {
        if (value is Array array)
        {
            // Allocate device memory and copy data
            var elementType = array.GetType().GetElementType() ?? typeof(object);
            var size = array.Length * GetElementSize(elementType);
            
            var buffer = await _accelerator.Memory.AllocateAsync(size, MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            // Copy data based on element type
            if (array is float[] floatArray)
            {
                await buffer.WriteAsync(floatArray, 0, cancellationToken).ConfigureAwait(false);
            }
            else if (array is double[] doubleArray)
            {
                await buffer.WriteAsync(doubleArray, 0, cancellationToken).ConfigureAwait(false);
            }
            else if (array is int[] intArray)
            {
                await buffer.WriteAsync(intArray, 0, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new NotSupportedException($"Array type {array.GetType()} is not supported for GPU copy.");
            }

            return new KernelArgument
            {
                Name = "input",
                Value = buffer,
                Type = array.GetType(),
                IsDeviceMemory = true,
                MemoryBuffer = buffer,
                SizeInBytes = size
            };
        }
        else
        {
            return new KernelArgument
            {
                Name = "scalar",
                Value = value,
                Type = value.GetType(),
                IsDeviceMemory = false
            };
        }
    }

    private static async ValueTask<object?> ExtractResultsAsync(
        KernelArgument[] arguments,
        ExpressionAnalysis analysis,
        CancellationToken cancellationToken)
    {
        // Find output buffer
        var outputArg = arguments.FirstOrDefault(a => a.Name.Contains("output", StringComparison.OrdinalIgnoreCase));
        if (outputArg?.MemoryBuffer != null)
        {
            // Read results from device memory
            var elementType = analysis.OutputType ?? typeof(float);
            var elementSize = GetElementSize(elementType);
            var elementCount = (int)(outputArg.MemoryBuffer.SizeInBytes / elementSize);
            
            var result = Array.CreateInstance(elementType, elementCount);
            if (result is float[] floatArray)
            {
                await outputArg.MemoryBuffer.CopyToHostAsync(floatArray.AsMemory(), 0, cancellationToken).ConfigureAwait(false);
            }
            else if (result is double[] doubleArray)
            {
                await outputArg.MemoryBuffer.CopyToHostAsync(doubleArray.AsMemory(), 0, cancellationToken).ConfigureAwait(false);
            }
            else if (result is int[] intArray)
            {
                await outputArg.MemoryBuffer.CopyToHostAsync(intArray.AsMemory(), 0, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new NotSupportedException($"Array type {result.GetType()} is not supported for GPU copy.");
            }
            
            return result;
        }

        return null;
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