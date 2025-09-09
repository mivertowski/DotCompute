// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
namespace DotCompute.Linq.Operators;


/// <summary>
/// Generates GPU kernels from LINQ expressions with advanced optimization support.
/// </summary>
public class ExpressionKernelGenerator
{
    private readonly KernelGenerationContext _context;
    private readonly ILogger _logger;
    private readonly Dictionary<ExpressionType, IExpressionHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionKernelGenerator"/> class.
    /// </summary>
    /// <param name="context">The generation context.</param>
    /// <param name="logger">The logger instance.</param>
    public ExpressionKernelGenerator(KernelGenerationContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = [];

        InitializeHandlers();
    }

    /// <summary>
    /// Generates a kernel from an expression tree.
    /// </summary>
    /// <param name="expression">The expression to generate from.</param>
    /// <param name="accelerator">The target accelerator.</param>
    /// <returns>A generated kernel.</returns>
    public GeneratedKernel Generate(Expression expression, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(accelerator);

        _logger.LogDebugMessage("Generating kernel from expression {expression.NodeType}");

        var kernelName = GenerateKernelName(expression);
        var parameters = ExtractParameters(expression);
        var sourceCode = GenerateSourceCode(expression, accelerator);

        var kernel = new GeneratedKernel
        {
            Name = kernelName,
            Source = sourceCode,
            Language = SelectTargetLanguage(accelerator),
            Parameters = parameters,
            RequiredWorkGroupSize = CalculateWorkGroupSize(expression),
            SharedMemorySize = CalculateSharedMemorySize(expression),
            OptimizationMetadata = ExtractOptimizationMetadata(expression),
            SourceExpression = expression
        };

        _logger.LogInfoMessage($"Generated kernel '{kernelName}' with {parameters.Length} parameters");

        return kernel;
    }

    private void InitializeHandlers()
    {
        _handlers[ExpressionType.Call] = new MethodCallHandler();
        _handlers[ExpressionType.Lambda] = new LambdaHandler();
        _handlers[ExpressionType.Add] = new BinaryOperationHandler();
        _handlers[ExpressionType.Subtract] = new BinaryOperationHandler();
        _handlers[ExpressionType.Multiply] = new BinaryOperationHandler();
        _handlers[ExpressionType.Divide] = new BinaryOperationHandler();
        _handlers[ExpressionType.Equal] = new ComparisonHandler();
        _handlers[ExpressionType.NotEqual] = new ComparisonHandler();
        _handlers[ExpressionType.LessThan] = new ComparisonHandler();
        _handlers[ExpressionType.LessThanOrEqual] = new ComparisonHandler();
        _handlers[ExpressionType.GreaterThan] = new ComparisonHandler();
        _handlers[ExpressionType.GreaterThanOrEqual] = new ComparisonHandler();
    }

    private static string GenerateKernelName(Expression expression)
    {
        var baseType = expression.NodeType.ToString().ToLowerInvariant();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var hash = expression.ToString().GetHashCode().ToString("X");
        return $"generated_kernel_{baseType}_{timestamp}_{hash}";
    }

    private static GeneratedKernelParameter[] ExtractParameters(Expression expression)
    {
        var parameters = new List<GeneratedKernelParameter>();
        var visitor = new ParameterExtractionVisitor(parameters);
        _ = visitor.Visit(expression);

        // Add standard parameters
        parameters.Add(new GeneratedKernelParameter
        {
            Name = "output",
            Type = expression.Type,
            IsOutput = true,
            IsInput = false
        });

        parameters.Add(new GeneratedKernelParameter
        {
            Name = "size",
            Type = typeof(int),
            IsInput = true,
            IsOutput = false
        });

        return [.. parameters];
    }

    private string GenerateSourceCode(Expression expression, IAccelerator accelerator)
    {
        var generator = new KernelSourceGenerator(accelerator.Type);
        return generator.GenerateFromExpression(expression, _context);
    }

    private static DotCompute.Abstractions.Types.KernelLanguage SelectTargetLanguage(IAccelerator accelerator)
    {
        return accelerator.Type switch
        {
            AcceleratorType.CUDA => KernelLanguage.CUDA,
            AcceleratorType.OpenCL => KernelLanguage.OpenCL,
            AcceleratorType.Metal => KernelLanguage.Metal,
            AcceleratorType.CPU => KernelLanguage.CSharpIL,
            _ => KernelLanguage.CSharpIL
        };
    }

    private int[] CalculateWorkGroupSize(Expression expression)
    {
        // Use context work group dimensions or calculate based on expression complexity
        if (_context.WorkGroupDimensions != null && _context.WorkGroupDimensions.Length >= 3)
        {
            return _context.WorkGroupDimensions;
        }

        // Default work group size based on expression complexity
        var complexity = CalculateComplexity(expression);
        var workGroupSize = Math.Min(256, Math.Max(32, complexity * 8));

        return [workGroupSize, 1, 1];
    }

    private int CalculateSharedMemorySize(Expression expression)
    {
        // Calculate shared memory requirements based on context and expression
        if (_context.UseSharedMemory)
        {
            var complexity = CalculateComplexity(expression);
            return Math.Min(48 * 1024, complexity * 1024); // Max 48KB shared memory
        }

        return 0;
    }

    private Dictionary<string, object> ExtractOptimizationMetadata(Expression expression)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ComplexityScore"] = CalculateComplexity(expression),
            ["UseSharedMemory"] = _context.UseSharedMemory,
            ["UseVectorTypes"] = _context.UseVectorTypes,
            ["Precision"] = _context.Precision.ToString(),
            ["GeneratedAt"] = DateTime.UtcNow,
            ["ExpressionType"] = expression.NodeType.ToString()
        };

        // Add fusion metadata if available
        var fusionMetadata = Expressions.FusionMetadataStore.Instance.GetMetadata(expression.ToString());
        if (fusionMetadata != null)
        {
            metadata["FusionData"] = fusionMetadata;
        }

        return metadata;
    }

    private static int CalculateComplexity(Expression expression)
    {
        var visitor = new ComplexityCalculationVisitor();
        _ = visitor.Visit(expression);
        return visitor.Complexity;
    }
}

/// <summary>
/// Visitor that extracts parameters from expressions.
/// </summary>
internal class ParameterExtractionVisitor : ExpressionVisitor
{
    private readonly List<GeneratedKernelParameter> _parameters;
    private readonly HashSet<string> _seenNames = [];

    public ParameterExtractionVisitor(List<GeneratedKernelParameter> parameters)
    {
        _parameters = parameters;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (!_seenNames.Contains(node.Name!))
        {
            _parameters.Add(new GeneratedKernelParameter
            {
                Name = node.Name!,
                Type = node.Type,
                IsInput = true,
                IsOutput = false
            });
            _ = _seenNames.Add(node.Name!);
        }
        return base.VisitParameter(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value != null)
        {
            var paramName = $"const_{_parameters.Count}";
            if (!_seenNames.Contains(paramName))
            {
                _parameters.Add(new GeneratedKernelParameter
                {
                    Name = paramName,
                    Type = node.Type,
                    IsInput = true,
                    IsOutput = false
                });
                _ = _seenNames.Add(paramName);
            }
        }
        return base.VisitConstant(node);
    }
}

/// <summary>
/// Visitor that calculates expression complexity.
/// </summary>
internal class ComplexityCalculationVisitor : ExpressionVisitor
{
    public int Complexity { get; private set; }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        Complexity += 2;
        return base.VisitBinary(node);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        Complexity += 1;
        return base.VisitUnary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        Complexity += 3;
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        Complexity += 5;
        return base.VisitConditional(node);
    }
}


/// <summary>
/// Handler for method call expressions.
/// </summary>
internal class MethodCallHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression) => expression.NodeType == ExpressionType.Call;

    public string Handle(Expression expression, KernelGenerationContext context)
    {
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            return methodName switch
            {
                "Select" => "/* Map operation */",
                "Where" => "/* Filter operation */",
                "Sum" => "/* Reduce operation */",
                "Average" => "/* Average operation */",
                _ => $"/* Method: {methodName} */"
            };
        }
        return "/* Unknown method call */";
    }
}

/// <summary>
/// Handler for lambda expressions.
/// </summary>
internal class LambdaHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression) => expression.NodeType == ExpressionType.Lambda;

    public string Handle(Expression expression, KernelGenerationContext context) => "/* Lambda expression */";
}

/// <summary>
/// Handler for binary operations.
/// </summary>
internal class BinaryOperationHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression)
    {
        return expression.NodeType is ExpressionType.Add or ExpressionType.Subtract
                                   or ExpressionType.Multiply or ExpressionType.Divide;
    }

    public string Handle(Expression expression, KernelGenerationContext context)
    {
        if (expression is BinaryExpression binary)
        {
            return binary.NodeType switch
            {
                ExpressionType.Add => "/* Addition */",
                ExpressionType.Subtract => "/* Subtraction */",
                ExpressionType.Multiply => "/* Multiplication */",
                ExpressionType.Divide => "/* Division */",
                _ => $"/* Binary operation: {binary.NodeType} */"
            };
        }
        return "/* Unknown binary operation */";
    }
}

/// <summary>
/// Handler for comparison operations.
/// </summary>
internal class ComparisonHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression)
    {
        return expression.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                                   or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
                                   or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual;
    }

    public string Handle(Expression expression, KernelGenerationContext context)
    {
        if (expression is BinaryExpression binary)
        {
            return binary.NodeType switch
            {
                ExpressionType.Equal => "/* Equality comparison */",
                ExpressionType.NotEqual => "/* Inequality comparison */",
                ExpressionType.LessThan => "/* Less than comparison */",
                ExpressionType.LessThanOrEqual => "/* Less than or equal comparison */",
                ExpressionType.GreaterThan => "/* Greater than comparison */",
                ExpressionType.GreaterThanOrEqual => "/* Greater than or equal comparison */",
                _ => $"/* Comparison: {binary.NodeType} */"
            };
        }
        return "/* Unknown comparison */";
    }
}
