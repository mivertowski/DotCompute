using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq;

/// <summary>
/// Provides LINQ extensions for compute operations
/// </summary>
public static class ComputeQueryableExtensions
{
    /// <summary>
    /// Converts an IQueryable to a compute-enabled queryable
    /// </summary>
    public static IQueryable<T> AsComputeQueryable<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ComputeQueryable<T>(source.Expression, new ComputeQueryProvider());
    }

    /// <summary>
    /// Executes a compute operation on GPU if available, otherwise falls back to CPU
    /// </summary>
    public static T[] ToComputeArray<T>(this IQueryable<T> source)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);

        // Execute the query provider which now uses the compilation pipeline
        if (source.Provider is ComputeQueryProvider provider)
        {
            var result = provider.Execute<IEnumerable<T>>(source.Expression);
            return result.ToArray();
        }

        // Fallback for non-compute providers
        return source.ToArray();
    }

    /// <summary>
    /// Maps elements using compute acceleration
    /// </summary>
    public static IQueryable<TResult> ComputeSelect<TSource, TResult>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        return source.Select(selector);
    }

    /// <summary>
    /// Filters elements using compute acceleration
    /// </summary>
    public static IQueryable<T> ComputeWhere<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return source.Where(predicate);
    }
}

/// <summary>
/// Minimal compute queryable implementation
/// </summary>
internal class ComputeQueryable<T> : IQueryable<T>
{
    public ComputeQueryable(Expression expression, IQueryProvider provider)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider { get; }

    public IEnumerator<T> GetEnumerator() => Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Compute query provider that uses the full compilation pipeline for GPU acceleration.
/// </summary>
internal sealed class ComputeQueryProvider : IQueryProvider, IDisposable
{
    private readonly ExpressionTreeVisitor _visitor;
    private readonly OperationCategorizer _categorizer;
    private readonly TypeInferenceEngine _typeInference;
    private readonly CompilationPipeline _pipeline;
    private readonly RuntimeExecutor _executor;
    private readonly BackendSelector _backendSelector;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ComputeQueryProvider with the full compilation pipeline.
    /// </summary>
    public ComputeQueryProvider()
    {
        // Initialize the compilation pipeline components
        _visitor = new ExpressionTreeVisitor();
        _categorizer = new OperationCategorizer();
        _typeInference = new TypeInferenceEngine();

        var kernelCache = new KernelCache(maxEntries: 100);

        var kernelGenerator = new CpuKernelGenerator();
        _pipeline = new CompilationPipeline(kernelCache, kernelGenerator);
        _executor = new RuntimeExecutor();
        _backendSelector = new BackendSelector();
    }

    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "IQueryProvider interface from framework cannot be annotated")]
    [RequiresDynamicCode("Creating generic types at runtime requires dynamic code generation.")]
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? expression.Type;
        var queryableType = typeof(ComputeQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, expression, this)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new ComputeQueryable<TElement>(expression, this);
    }

    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "IQueryProvider interface from framework cannot be annotated")]
    [RequiresDynamicCode("Expression compilation requires dynamic code generation.")]
    public object? Execute(Expression expression)
    {
        // Try to extract element type from the expression
        var resultType = expression.Type;

        // For IEnumerable<T>, extract T
        if (resultType.IsGenericType &&
            (resultType.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             resultType.GetGenericTypeDefinition() == typeof(IQueryable<>)))
        {
            var elementType = resultType.GetGenericArguments()[0];

            // Use reflection to call ExecuteTyped<T>
            var method = typeof(ComputeQueryProvider).GetMethod(nameof(ExecuteTyped),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var genericMethod = method.MakeGenericMethod(elementType);
            return genericMethod.Invoke(this, new object[] { expression });
        }

        // Fallback to expression compilation for non-collection results
        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke();
    }

    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "IQueryProvider interface from framework cannot be annotated")]
    [RequiresDynamicCode("Expression compilation requires dynamic code generation.")]
    public TResult Execute<TResult>(Expression expression)
    {
        var result = Execute(expression);
        return result is TResult typed ? typed : (TResult)result!;
    }

    /// <summary>
    /// Executes a query expression using the full compilation pipeline.
    /// </summary>
    private IEnumerable<T> ExecuteTyped<T>(Expression expression) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Stage 1: Analyze the expression tree to build operation graph
            var operationGraph = _visitor.Visit(expression);

            if (operationGraph.Operations.Count == 0)
            {
                // Empty query, return empty result
                return Array.Empty<T>();
            }

            // Stage 2: Infer types and validate
            var elementType = typeof(T);
            var typeInfo = _typeInference.InferType(expression);

            if (!typeInfo.IsSimdCapable && elementType.IsValueType)
            {
                // Type is not SIMD-capable, fall back to standard LINQ
                return ExecuteFallback<T>(expression);
            }

            // Stage 3: Determine optimal backend
            var workload = BuildWorkloadCharacteristics(operationGraph);
            var backend = _backendSelector.SelectBackend(workload);

            // Stage 4: Compile to executable kernel
            var metadata = new TypeMetadata
            {
                InputType = elementType,
                ResultType = elementType,
                IntermediateTypes = new Dictionary<string, Type>(),
                IsSimdCompatible = typeInfo.IsSimdCapable,
                RequiresUnsafe = typeInfo.RequiresUnsafe,
                HasNullableTypes = typeInfo.IsNullable
            };

            var compilationOptions = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Balanced,
                GenerateDebugInfo = false,
                CacheTtl = TimeSpan.FromHours(1)
            };

            var compiledKernel = _pipeline.CompileToDelegate<T, T>(
                operationGraph,
                metadata,
                compilationOptions);

            // Stage 5: Execute the kernel
            // TODO: Get actual input data from expression
            var inputData = ExtractInputData<T>(expression);

            var (results, metrics) = _executor.ExecuteAsync(
                compiledKernel,
                inputData,
                backend,
                CancellationToken.None).GetAwaiter().GetResult();

            return results;
        }
        catch (Exception)
        {
            // On any compilation or execution error, fall back to standard LINQ
            return ExecuteFallback<T>(expression);
        }
    }

    /// <summary>
    /// Extracts input data from a LINQ expression tree.
    /// Enhanced to handle multiple expression patterns including:
    /// - Constant expressions (arrays, lists, queryables)
    /// - Method calls (LINQ operations)
    /// - Member access (fields, properties)
    /// - Array initializers
    /// - Nested and complex expressions
    /// </summary>
    private static T[] ExtractInputData<T>(Expression expression)
    {
        var visited = new HashSet<Expression>();
        return ExtractDataRecursive<T>(expression, visited) ?? Array.Empty<T>();
    }

    /// <summary>
    /// Recursively extracts data from expression tree with cycle detection.
    /// </summary>
    private static T[]? ExtractDataRecursive<T>(Expression? expression, HashSet<Expression> visited)
    {
        if (expression == null || !visited.Add(expression))
        {
            return null; // Prevent infinite loops on circular references
        }

        switch (expression)
        {
            // Handle constant expressions (most common case)
            case ConstantExpression constant:
                return ExtractFromConstant<T>(constant);

            // Handle method calls (LINQ operations like Where, Select, etc.)
            case MethodCallExpression methodCall:
                return ExtractFromMethodCall<T>(methodCall, visited);

            // Handle unary expressions (conversions, type casts)
            case UnaryExpression unary:
                return ExtractDataRecursive<T>(unary.Operand, visited);

            // Handle member access (accessing fields or properties)
            case MemberExpression member:
                return ExtractFromMember<T>(member);

            // Handle array initializers (new[] { 1, 2, 3 })
            case NewArrayExpression newArray:
                return ExtractFromNewArray<T>(newArray);

            // Handle lambda expressions (unwrap the body)
            case LambdaExpression lambda:
                return ExtractDataRecursive<T>(lambda.Body, visited);

            default:
                return null;
        }
    }

    /// <summary>
    /// Extracts data from constant expressions.
    /// </summary>
    private static T[]? ExtractFromConstant<T>(ConstantExpression constant)
    {
        // Direct IEnumerable<T> match
        if (constant.Value is IEnumerable<T> enumerable)
        {
            return enumerable.ToArray();
        }

        // IQueryable<T> (includes our ComputeQueryable<T>)
        if (constant.Value is IQueryable<T> queryable)
        {
            return queryable.ToArray();
        }

        return null;
    }

    /// <summary>
    /// Extracts data from method call expressions by checking all arguments.
    /// </summary>
    private static T[]? ExtractFromMethodCall<T>(MethodCallExpression methodCall, HashSet<Expression> visited)
    {
        // Check all arguments (not just the first one)
        foreach (var argument in methodCall.Arguments)
        {
            var data = ExtractDataRecursive<T>(argument, visited);
            if (data != null && data.Length > 0)
            {
                return data;
            }
        }

        // Check the object instance for instance method calls
        if (methodCall.Object != null)
        {
            return ExtractDataRecursive<T>(methodCall.Object, visited);
        }

        return null;
    }

    /// <summary>
    /// Extracts data from member access expressions (fields/properties).
    /// </summary>
    private static T[]? ExtractFromMember<T>(MemberExpression member)
    {
        // Try to compile and evaluate the member expression
        try
        {
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(member, typeof(object)));
            var compiled = lambda.Compile();
            var value = compiled();

            if (value is IEnumerable<T> enumerable)
            {
                return enumerable.ToArray();
            }

            if (value is IQueryable<T> queryable)
            {
                return queryable.ToArray();
            }
        }
        catch
        {
            // If evaluation fails, return null (caller will try other strategies)
        }

        return null;
    }

    /// <summary>
    /// Extracts data from array initializer expressions.
    /// </summary>
    private static T[]? ExtractFromNewArray<T>(NewArrayExpression newArray)
    {
        // Handle array initializers like new[] { 1, 2, 3 }
        if (newArray.Type == typeof(T[]) || newArray.Type.IsAssignableFrom(typeof(T[])))
        {
            try
            {
                var lambda = Expression.Lambda<Func<T[]>>(newArray);
                var compiled = lambda.Compile();
                return compiled();
            }
            catch
            {
                // If compilation fails, return null
            }
        }

        return null;
    }

    /// <summary>
    /// Builds workload characteristics from an operation graph.
    /// </summary>
    private CodeGeneration.WorkloadCharacteristics BuildWorkloadCharacteristics(OperationGraph graph)
    {
        var strategy = _categorizer.GetStrategy(graph);
        var dataFlow = _categorizer.AnalyzeDataFlow(graph);

        // Determine primary operation type
        var primaryOp = graph.Root?.Type ?? OperationType.Map;

        // Convert Optimization.ComputeIntensity to CodeGeneration.ComputeIntensity
        var intensity = (CodeGeneration.ComputeIntensity)(int)dataFlow.Intensity;

        return new CodeGeneration.WorkloadCharacteristics
        {
            DataSize = dataFlow.EstimatedDataSize,
            Intensity = intensity,
            IsFusible = true,
            PrimaryOperation = primaryOp,
            OperationsPerElement = graph.Operations.Count,
            IsMemoryBound = dataFlow.Intensity == Optimization.ComputeIntensity.Low,
            HasRandomAccess = false,
            ParallelismDegree = dataFlow.EstimatedDataSize
        };
    }

    /// <summary>
    /// Fallback to standard LINQ execution when compilation fails.
    /// </summary>
    private static IEnumerable<T> ExecuteFallback<T>(Expression expression)
    {
        var lambda = Expression.Lambda<Func<IEnumerable<T>>>(expression);
        var compiled = lambda.Compile();
        return compiled();
    }

    /// <summary>
    /// Disposes resources used by the query provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _pipeline?.Dispose();
        _executor?.Dispose();
        _disposed = true;
    }
}