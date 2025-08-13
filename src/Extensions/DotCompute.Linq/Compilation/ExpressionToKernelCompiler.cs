// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Compiles LINQ expression trees into GPU kernels with support for expression fusion and optimization.
/// </summary>
public sealed class ExpressionToKernelCompiler : IExpressionToKernelCompiler, IDisposable
{
    private readonly IKernelFactory _kernelFactory;
    private readonly IExpressionOptimizer _optimizer;
    private readonly ILogger<ExpressionToKernelCompiler> _logger;
    private readonly Dictionary<string, KernelTemplate> _templateCache;
    private readonly SemaphoreSlim _compilationSemaphore;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionToKernelCompiler"/> class.
    /// </summary>
    public ExpressionToKernelCompiler(
        IKernelFactory kernelFactory,
        IExpressionOptimizer optimizer,
        ILogger<ExpressionToKernelCompiler> logger)
    {
        _kernelFactory = kernelFactory ?? throw new ArgumentNullException(nameof(kernelFactory));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateCache = new Dictionary<string, KernelTemplate>();
        _compilationSemaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    /// <summary>
    /// Compiles an expression tree into a GPU kernel.
    /// </summary>
    public async Task<Operators.IKernel> CompileExpressionAsync(
        Expression expression,
        IAccelerator accelerator,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CompilationOptions();
        
        _logger.LogDebug("Compiling expression {ExpressionType} for {AcceleratorType}", 
            expression.NodeType, accelerator.Type);

        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Step 1: Optimize the expression tree
            var optimizedExpression = _optimizer.Optimize(expression, options);
            
            // Step 2: Analyze the optimized expression
            var analysis = AnalyzeExpression(optimizedExpression);
            
            // Step 3: Check for fusion opportunities
            var fusionContext = ExtractFusionContext(optimizedExpression);
            
            // Step 4: Generate kernel based on analysis
            if (fusionContext != null)
            {
                return await CompileFusedExpressionAsync(optimizedExpression, accelerator, fusionContext, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                return await CompileSimpleExpressionAsync(optimizedExpression, accelerator, analysis, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _compilationSemaphore.Release();
        }
    }

    /// <summary>
    /// Validates whether an expression can be compiled to a kernel.
    /// </summary>
    public bool CanCompileExpression(Expression expression)
    {
        try
        {
            var analysis = AnalyzeExpression(expression);
            return analysis.IsCompilable && analysis.SupportedOperations.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets resource estimation for compiling an expression.
    /// </summary>
    public ExpressionResourceEstimate EstimateResources(Expression expression)
    {
        var analysis = AnalyzeExpression(expression);
        
        return new ExpressionResourceEstimate
        {
            EstimatedMemoryUsage = CalculateMemoryUsage(analysis),
            EstimatedCompilationTime = CalculateCompilationTime(analysis),
            EstimatedExecutionTime = CalculateExecutionTime(analysis),
            ComplexityScore = analysis.ComplexityScore,
            ParallelizationFactor = CalculateParallelizationFactor(analysis)
        };
    }

    private Task<Operators.IKernel> CompileFusedExpressionAsync(
        Expression expression,
        IAccelerator accelerator,
        FusionContext fusionContext,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Compiling fused expression with {OperationCount} operations", 
            fusionContext.FusedOperations.Count);

        // Create kernel definition for fused operations
        var definition = CreateFusedKernelDefinition(fusionContext, expression);
        
        // Add fusion metadata
        definition.Metadata["FusionMetadata"] = fusionContext.Metadata;
        definition.Metadata["OperationType"] = $"Fused_{string.Join("_", fusionContext.FusedOperations)}";
        
        // Create kernel using enhanced factory
        if (_kernelFactory is DefaultKernelFactory enhancedFactory)
        {
            var context = CreateGenerationContext(accelerator, options, fusionContext.Metadata);
            return Task.FromResult(enhancedFactory.CreateKernelFromExpression(accelerator, expression, context));
        }
        
        return Task.FromResult(_kernelFactory.CreateKernel(accelerator, definition));
    }

    private async Task<Operators.IKernel> CompileSimpleExpressionAsync(
        Expression expression,
        IAccelerator accelerator,
        ExpressionAnalysisResult analysis,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Compiling simple expression {ExpressionType}", expression.NodeType);

        // Check for template match
        var templateKey = GenerateTemplateKey(analysis);
        if (_templateCache.TryGetValue(templateKey, out var template))
        {
            _logger.LogDebug("Using cached template for expression compilation");
            return await CompileFromTemplate(template, accelerator, expression, options, cancellationToken)
                .ConfigureAwait(false);
        }

        // Create new kernel definition
        var definition = CreateKernelDefinition(analysis, expression);
        
        // Create kernel
        if (_kernelFactory is DefaultKernelFactory enhancedFactory)
        {
            var context = CreateGenerationContext(accelerator, options);
            return enhancedFactory.CreateKernelFromExpression(accelerator, expression, context);
        }
        
        return _kernelFactory.CreateKernel(accelerator, definition);
    }

    /// <summary>
    /// Disposes the compiler and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _compilationSemaphore?.Dispose();
            _disposed = true;
        }
    }

    private static ExpressionAnalysisResult AnalyzeExpression(Expression expression)
    {
        var visitor = new ExpressionAnalysisVisitor();
        visitor.Visit(expression);
        return visitor.GetResult();
    }

    private FusionContext? ExtractFusionContext(Expression expression)
    {
        // Check if expression has fusion metadata
        var key = expression.ToString();
        var metadata = FusionMetadataStore.Instance.GetMetadata(key);
        
        if (metadata != null)
        {
            var operations = metadata.GetValueOrDefault("FusedOperations") as string[] ?? [];
            var fusionType = metadata.GetValueOrDefault("FusionType")?.ToString() ?? "Generic";
            
            return new FusionContext
            {
                FusedOperations = operations,
                FusionType = fusionType,
                Metadata = metadata,
                EstimatedSpeedup = (double)(metadata.GetValueOrDefault("EstimatedSpeedup", 1.2))
            };
        }
        
        return null;
    }

    private Operators.KernelDefinition CreateFusedKernelDefinition(FusionContext fusionContext, Expression expression)
    {
        var name = $"fused_kernel_{string.Join("_", fusionContext.FusedOperations).ToLowerInvariant()}_{Guid.NewGuid():N}";
        
        // Analyze expression to extract parameter types
        var parameterTypes = ExtractParameterTypes(expression);
        var outputType = expression.Type;
        
        var parameters = new List<Operators.KernelParameter>();
        
        // Add input parameters
        for (int i = 0; i < parameterTypes.Count; i++)
        {
            parameters.Add(new Operators.KernelParameter($"input_{i}", parameterTypes[i], ParameterDirection.In));
        }
        
        // Add output parameter
        parameters.Add(new Operators.KernelParameter("output", outputType, ParameterDirection.Out));
        
        // Add size parameter
        parameters.Add(new Operators.KernelParameter("size", typeof(int), ParameterDirection.In));
        
        return new Operators.KernelDefinition
        {
            Name = name,
            Parameters = parameters.ToArray(),
            Language = Operators.KernelLanguage.CSharp,
            Metadata = new Dictionary<string, object>
            {
                ["FusionType"] = fusionContext.FusionType,
                ["FusedOperations"] = fusionContext.FusedOperations.ToArray(),
                ["EstimatedSpeedup"] = fusionContext.EstimatedSpeedup
            }
        };
    }

    private static Operators.KernelDefinition CreateKernelDefinition(ExpressionAnalysisResult analysis, Expression expression)
    {
        var name = $"expression_kernel_{expression.NodeType.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
        var parameters = new List<Operators.KernelParameter>();
        
        // Add parameters based on analysis
        foreach (var paramType in analysis.ParameterTypes)
        {
            parameters.Add(new Operators.KernelParameter($"param_{parameters.Count}", paramType, ParameterDirection.In));
        }
        
        // Add output parameter
        parameters.Add(new Operators.KernelParameter("output", analysis.OutputType, ParameterDirection.Out));
        
        return new Operators.KernelDefinition
        {
            Name = name,
            Parameters = parameters.ToArray(),
            Language = Operators.KernelLanguage.CSharp,
            Metadata = new Dictionary<string, object>
            {
                ["OperationType"] = DetermineOperationType(analysis),
                ["ComplexityScore"] = analysis.ComplexityScore,
                ["SupportedOperations"] = analysis.SupportedOperations.ToArray()
            }
        };
    }

    private static Operators.KernelGenerationContext CreateGenerationContext(
        IAccelerator accelerator, 
        CompilationOptions options,
        Dictionary<string, object>? additionalMetadata = null)
    {
        var context = new Operators.KernelGenerationContext
        {
            DeviceInfo = accelerator.Info,
            UseSharedMemory = options.EnableMemoryCoalescing,
            UseVectorTypes = true,
            Precision = Operators.PrecisionMode.Single,
            WorkGroupDimensions = new[] { options.MaxThreadsPerBlock, 1, 1 },
            Metadata = new Dictionary<string, object>
            {
                ["EnableOperatorFusion"] = options.EnableOperatorFusion,
                ["EnableMemoryCoalescing"] = options.EnableMemoryCoalescing,
                ["EnableParallelExecution"] = options.EnableParallelExecution,
                ["GenerateDebugInfo"] = options.GenerateDebugInfo
            }
        };

        if (additionalMetadata != null)
        {
            foreach (var kvp in additionalMetadata)
            {
                context.Metadata[kvp.Key] = kvp.Value;
            }
        }

        return context;
    }

    private static List<Type> ExtractParameterTypes(Expression expression)
    {
        var types = new List<Type>();
        var visitor = new ParameterTypeExtractor(types);
        visitor.Visit(expression);
        return types;
    }

    private static string GenerateTemplateKey(ExpressionAnalysisResult analysis)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(analysis.OutputType.Name);
        keyBuilder.Append('_');
        keyBuilder.AppendJoin("_", analysis.SupportedOperations);
        keyBuilder.Append('_');
        keyBuilder.Append(analysis.ComplexityScore);
        return keyBuilder.ToString();
    }

    private async Task<Operators.IKernel> CompileFromTemplate(
        KernelTemplate template,
        IAccelerator accelerator,
        Expression expression,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        // Apply template to create kernel definition
        var definition = template.CreateDefinition(expression);
        await Task.CompletedTask; // Satisfy async signature
        return _kernelFactory.CreateKernel(accelerator, definition);
    }

    private static string DetermineOperationType(ExpressionAnalysisResult analysis)
    {
        if (analysis.SupportedOperations.Contains("Select"))
            return "Map";
        if (analysis.SupportedOperations.Contains("Where"))
            return "Filter";
        if (analysis.SupportedOperations.Contains("Sum") || analysis.SupportedOperations.Contains("Average"))
            return "Reduce";
        if (analysis.SupportedOperations.Contains("OrderBy"))
            return "Sort";
        
        return "Custom";
    }

    private static long CalculateMemoryUsage(ExpressionAnalysisResult analysis)
    {
        // Estimate memory based on parameter types and operations
        long baseMemory = analysis.ParameterTypes.Sum(t => EstimateTypeSize(t));
        long operationOverhead = analysis.SupportedOperations.Count * 1024; // 1KB per operation
        return baseMemory + operationOverhead;
    }

    private static TimeSpan CalculateCompilationTime(ExpressionAnalysisResult analysis)
    {
        // Estimate compilation time based on complexity
        var baseTime = TimeSpan.FromMilliseconds(100); // Base compilation time
        var complexityMultiplier = Math.Max(1.0, analysis.ComplexityScore / 10.0);
        return TimeSpan.FromTicks((long)(baseTime.Ticks * complexityMultiplier));
    }

    private static TimeSpan CalculateExecutionTime(ExpressionAnalysisResult analysis)
    {
        // Rough estimation based on operations and data size
        var baseExecution = TimeSpan.FromMicroseconds(50);
        var operationCount = analysis.SupportedOperations.Count;
        return TimeSpan.FromTicks(baseExecution.Ticks * Math.Max(1, operationCount));
    }

    private static double CalculateParallelizationFactor(ExpressionAnalysisResult analysis)
    {
        // Estimate how well the expression can be parallelized
        var parallelizableOps = new[] { "Select", "Where", "Sum", "Average" };
        var parallelCount = analysis.SupportedOperations.Count(op => parallelizableOps.Contains(op));
        return Math.Min(1.0, parallelCount / (double)analysis.SupportedOperations.Count);
    }

    private static long EstimateTypeSize(Type type)
    {
        if (type.IsPrimitive)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte => 1,
                TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char => 2,
                TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
                TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => 8,
                TypeCode.Decimal => 16,
                _ => IntPtr.Size
            };
        }
        
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType != null ? EstimateTypeSize(elementType) * 1000 : IntPtr.Size * 1000;
        }
        
        return IntPtr.Size; // Default size for reference types
    }
}

/// <summary>
/// Interface for expression-to-kernel compilation.
/// </summary>
public interface IExpressionToKernelCompiler
{
    /// <summary>
    /// Compiles an expression tree into a GPU kernel.
    /// </summary>
    Task<Operators.IKernel> CompileExpressionAsync(
        Expression expression,
        IAccelerator accelerator,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether an expression can be compiled to a kernel.
    /// </summary>
    bool CanCompileExpression(Expression expression);

    /// <summary>
    /// Gets resource estimation for compiling an expression.
    /// </summary>
    ExpressionResourceEstimate EstimateResources(Expression expression);
}

/// <summary>
/// Represents analysis results for an expression.
/// </summary>
public sealed class ExpressionAnalysisResult
{
    private readonly List<string> _supportedOperations = new();
    private readonly List<Type> _parameterTypes = new();
    
    public bool IsCompilable { get; set; }
    public IReadOnlyList<string> SupportedOperations => _supportedOperations;
    public IReadOnlyList<Type> ParameterTypes => _parameterTypes;
    public Type OutputType { get; set; } = typeof(object);
    public int ComplexityScore { get; set; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    internal List<string> SupportedOperationsInternal => _supportedOperations;
    internal List<Type> ParameterTypesInternal => _parameterTypes;
}

/// <summary>
/// Represents fusion context for multiple operations.
/// </summary>
public sealed class FusionContext
{
    public IReadOnlyList<string> FusedOperations { get; init; } = new List<string>();
    public string FusionType { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public double EstimatedSpeedup { get; init; } = 1.0;
}

/// <summary>
/// Represents resource estimation for expression compilation.
/// </summary>
public sealed class ExpressionResourceEstimate
{
    public long EstimatedMemoryUsage { get; init; }
    public TimeSpan EstimatedCompilationTime { get; init; }
    public TimeSpan EstimatedExecutionTime { get; init; }
    public int ComplexityScore { get; init; }
    public double ParallelizationFactor { get; init; }
}

/// <summary>
/// Template for generating kernels from similar expressions.
/// </summary>
public sealed class KernelTemplate
{
    public string Name { get; init; } = string.Empty;
    public Func<Expression, Operators.KernelDefinition> CreateDefinition { get; init; } = _ => new Operators.KernelDefinition();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Visitor for analyzing expressions to determine compilation feasibility.
/// </summary>
internal class ExpressionAnalysisVisitor : ExpressionVisitor
{
    private readonly ExpressionAnalysisResult _result = new() { IsCompilable = true };
    private int _complexityScore = 0;

    public ExpressionAnalysisResult GetResult()
    {
        _result.ComplexityScore = _complexityScore;
        return _result;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _complexityScore++;
        
        if (IsLinqMethod(node))
        {
            _result.SupportedOperationsInternal.Add(node.Method.Name);
        }
        else
        {
            // Check if it's a supported math operation
            if (IsMathMethod(node))
            {
                _result.SupportedOperationsInternal.Add($"Math.{node.Method.Name}");
            }
            else
            {
                // Unsupported method call
                _result.IsCompilable = false;
                _result.Metadata["UnsupportedMethod"] = node.Method.Name;
            }
        }
        
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _complexityScore++;
        _result.SupportedOperationsInternal.Add(node.NodeType.ToString());
        return base.VisitBinary(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (!_result.ParameterTypes.Contains(node.Type))
        {
            _result.ParameterTypesInternal.Add(node.Type);
        }
        return base.VisitParameter(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        if (_result.OutputType == typeof(object))
        {
            _result.OutputType = node.ReturnType;
        }
        return base.VisitLambda(node);
    }

    private static bool IsLinqMethod(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;
        return declaringType == typeof(Queryable) || declaringType == typeof(Enumerable);
    }

    private static bool IsMathMethod(MethodCallExpression node)
    {
        return node.Method.DeclaringType == typeof(Math) || node.Method.DeclaringType == typeof(MathF);
    }
}

/// <summary>
/// Visitor for extracting parameter types from expressions.
/// </summary>
internal class ParameterTypeExtractor : ExpressionVisitor
{
    private readonly List<Type> _types;

    public ParameterTypeExtractor(List<Type> types)
    {
        _types = types;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value != null && !_types.Contains(node.Type))
        {
            _types.Add(node.Type);
        }
        return base.VisitConstant(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (!_types.Contains(node.Type))
        {
            _types.Add(node.Type);
        }
        return base.VisitParameter(node);
    }
}