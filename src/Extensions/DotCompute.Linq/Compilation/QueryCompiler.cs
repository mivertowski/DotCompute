// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Compiles LINQ expression trees into executable compute plans.
/// </summary>
public class QueryCompiler : IQueryCompiler
{
    private readonly IKernelFactory _kernelFactory;
    private readonly IExpressionOptimizer _optimizer;
    private readonly ILogger<QueryCompiler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCompiler"/> class.
    /// </summary>
    /// <param name="kernelFactory">The kernel factory for creating compute kernels.</param>
    /// <param name="optimizer">The expression optimizer.</param>
    /// <param name="logger">The logger instance.</param>
    public QueryCompiler(
        IKernelFactory kernelFactory,
        IExpressionOptimizer optimizer,
        ILogger<QueryCompiler> logger)
    {
        _kernelFactory = kernelFactory ?? throw new ArgumentNullException(nameof(kernelFactory));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IComputePlan Compile(CompilationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug("Compiling expression of type {ExpressionType}", context.Expression.NodeType);

        // Validate the expression
        var validationResult = Validate(context.Expression);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Expression validation failed: {validationResult.ErrorMessage}");
        }

        // Optimize the expression tree
        var optimizedExpression = _optimizer.Optimize(context.Expression, context.Options);

        // Visit the expression tree and build compute stages
        var visitor = new ComputePlanVisitor(_kernelFactory, context.Accelerator, _logger);
        var stages = visitor.Visit(optimizedExpression);

        // Create the compute plan
        var plan = new ComputePlan(
            stages,
            visitor.InputParameters,
            visitor.OutputType,
            visitor.EstimatedMemoryUsage);

        _logger.LogInformation(
            "Compiled expression into compute plan with {StageCount} stages, estimated memory: {MemoryMB:F2} MB",
            plan.Stages.Count,
            plan.EstimatedMemoryUsage / (1024.0 * 1024.0));

        return plan;
    }

    /// <inheritdoc/>
    public DotCompute.Abstractions.ValidationResult Validate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var errors = new List<ValidationError>();
        var validator = new ExpressionValidator();
        
        try
        {
            validator.Visit(expression, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expression validation failed with exception");
            errors.Add(new ValidationError("VALIDATION_ERROR", ex.Message, expression));
        }

        if (errors.Count > 0)
        {
            var message = $"Expression validation failed with {errors.Count} errors";
            return DotCompute.Abstractions.ValidationResult.Failure(message);
        }

        return DotCompute.Abstractions.ValidationResult.Success();
    }

    /// <summary>
    /// Visitor that builds compute stages from expression trees.
    /// </summary>
    private class ComputePlanVisitor : ExpressionVisitor
    {
        private readonly IKernelFactory _kernelFactory;
        private readonly IAccelerator _accelerator;
        private readonly ILogger _logger;
        private readonly List<IComputeStage> _stages = [];
        private readonly Dictionary<string, Type> _inputParameters = [];
        private Type _outputType = typeof(object);
        private long _estimatedMemoryUsage;
        private int _stageCounter;

        public ComputePlanVisitor(IKernelFactory kernelFactory, IAccelerator accelerator, ILogger logger)
        {
            _kernelFactory = kernelFactory;
            _accelerator = accelerator;
            _logger = logger;
        }

        public IReadOnlyList<IComputeStage> Stages => _stages;
        public IReadOnlyDictionary<string, Type> InputParameters => _inputParameters;
        public Type OutputType => _outputType;
        public long EstimatedMemoryUsage => _estimatedMemoryUsage;

        public new List<IComputeStage> Visit(Expression expression)
        {
            base.Visit(expression);
            return _stages;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            _logger.LogDebug("Visiting method call: {MethodName}", node.Method.Name);

            // Handle LINQ operators
            if (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
            {
                switch (node.Method.Name)
                {
                    case "Select":
                        return VisitSelect(node);
                    case "Where":
                        return VisitWhere(node);
                    case "Sum":
                    case "Average":
                    case "Min":
                    case "Max":
                        return VisitAggregate(node);
                    case "OrderBy":
                    case "OrderByDescending":
                    case "ThenBy":
                    case "ThenByDescending":
                        return VisitOrderBy(node);
                    default:
                        _logger.LogWarning("Unsupported LINQ method: {Method}", node.Method.Name);
                        break;
                }
            }

            return base.VisitMethodCall(node);
        }

        private Expression VisitSelect(MethodCallExpression node)
        {
            // Visit the source
            Visit(node.Arguments[0]);

            // Extract the selector lambda
            var selectorLambda = GetLambdaOperand(node.Arguments[1]);
            var inputType = selectorLambda.Parameters[0].Type;
            var outputType = selectorLambda.Body.Type;

            // Create a map kernel
            var kernelDefinition = new Operators.KernelDefinition
            {
                Name = $"Select_{_stageCounter++}",
                Parameters =
                [
                    new Operators.KernelParameter("input", CreateArrayType(inputType), Operators.ParameterDirection.In),
                    new Operators.KernelParameter("output", CreateArrayType(outputType), Operators.ParameterDirection.Out),
                    new Operators.KernelParameter("count", typeof(int), Operators.ParameterDirection.In)
                ],
                Language = Operators.KernelLanguage.CSharp
            };

            var kernel = _kernelFactory.CreateKernel(_accelerator, kernelDefinition);
            
            var stage = new ComputeStage(
                $"stage_{_stageCounter}",
                kernel,
                new[] { "input" },
                "output",
                new ExecutionConfiguration
                {
                    BlockDimensions = (256, 1, 1),
                    GridDimensions = (1, 1, 1) // Will be calculated at runtime
                });

            _stages.Add(stage);
            _outputType = CreateArrayType(outputType);

            // Estimate memory usage
            _estimatedMemoryUsage += EstimateTypeSize(inputType) * 1000; // Assume 1000 elements
            _estimatedMemoryUsage += EstimateTypeSize(outputType) * 1000;

            return node;
        }

        private Expression VisitWhere(MethodCallExpression node)
        {
            // Visit the source
            Visit(node.Arguments[0]);

            // Extract the predicate lambda
            var predicateLambda = GetLambdaOperand(node.Arguments[1]);
            var elementType = predicateLambda.Parameters[0].Type;

            // Create a filter kernel
            var kernelDefinition = new Operators.KernelDefinition
            {
                Name = $"Where_{_stageCounter++}",
                Parameters =
                [
                    new Operators.KernelParameter("input", CreateArrayType(elementType), Operators.ParameterDirection.In),
                    new Operators.KernelParameter("output", CreateArrayType(elementType), Operators.ParameterDirection.Out),
                    new Operators.KernelParameter("predicate_results", typeof(bool[]), Operators.ParameterDirection.Out),
                    new Operators.KernelParameter("count", typeof(int), Operators.ParameterDirection.In)
                ],
                Language = Operators.KernelLanguage.CSharp
            };

            var kernel = _kernelFactory.CreateKernel(_accelerator, kernelDefinition);
            
            var stage = new ComputeStage(
                $"stage_{_stageCounter}",
                kernel,
                new[] { "input" },
                "output",
                new ExecutionConfiguration
                {
                    BlockDimensions = (256, 1, 1),
                    GridDimensions = (1, 1, 1)
                });

            _stages.Add(stage);
            _outputType = CreateArrayType(elementType);

            // Estimate memory usage
            _estimatedMemoryUsage += EstimateTypeSize(elementType) * 2000; // Input + output
            _estimatedMemoryUsage += sizeof(bool) * 1000; // Predicate results

            return node;
        }

        private Expression VisitAggregate(MethodCallExpression node)
        {
            // Visit the source
            Visit(node.Arguments[0]);

            var elementType = node.Arguments[0].Type.GetGenericArguments()[0];
            var resultType = node.Type;

            // Create an aggregation kernel
            var kernelDefinition = new Operators.KernelDefinition
            {
                Name = $"{node.Method.Name}_{_stageCounter++}",
                Parameters =
                [
                    new Operators.KernelParameter("input", CreateArrayType(elementType), Operators.ParameterDirection.In),
                    new Operators.KernelParameter("result", resultType, Operators.ParameterDirection.Out),
                    new Operators.KernelParameter("count", typeof(int), Operators.ParameterDirection.In)
                ],
                Language = Operators.KernelLanguage.CSharp
            };

            var kernel = _kernelFactory.CreateKernel(_accelerator, kernelDefinition);
            
            var stage = new ComputeStage(
                $"stage_{_stageCounter}",
                kernel,
                new[] { "input" },
                "result",
                new ExecutionConfiguration
                {
                    BlockDimensions = (256, 1, 1),
                    GridDimensions = (1, 1, 1),
                    SharedMemorySize = (int)(256 * EstimateTypeSize(elementType)) // For reduction
                });

            _stages.Add(stage);
            _outputType = resultType;

            // Estimate memory usage
            _estimatedMemoryUsage += EstimateTypeSize(elementType) * 1000;
            _estimatedMemoryUsage += EstimateTypeSize(resultType);

            return node;
        }

        private Expression VisitOrderBy(MethodCallExpression node)
        {
            // Visit the source
            Visit(node.Arguments[0]);

            var keySelectorLambda = GetLambdaOperand(node.Arguments[1]);
            var elementType = keySelectorLambda.Parameters[0].Type;
            var keyType = keySelectorLambda.Body.Type;

            // Create a sort kernel
            var kernelDefinition = new Operators.KernelDefinition
            {
                Name = $"{node.Method.Name}_{_stageCounter++}",
                Parameters =
                [
                    new Operators.KernelParameter("input", CreateArrayType(elementType), Operators.ParameterDirection.In),
                    new Operators.KernelParameter("output", CreateArrayType(elementType), Operators.ParameterDirection.Out),
                    new Operators.KernelParameter("keys", CreateArrayType(keyType), Operators.ParameterDirection.InOut),
                    new Operators.KernelParameter("count", typeof(int), Operators.ParameterDirection.In)
                ],
                Language = Operators.KernelLanguage.CSharp
            };

            var kernel = _kernelFactory.CreateKernel(_accelerator, kernelDefinition);
            
            var stage = new ComputeStage(
                $"stage_{_stageCounter}",
                kernel,
                new[] { "input" },
                "output",
                new ExecutionConfiguration
                {
                    BlockDimensions = (256, 1, 1),
                    GridDimensions = (1, 1, 1)
                });

            _stages.Add(stage);
            _outputType = CreateArrayType(elementType);

            // Estimate memory usage
            _estimatedMemoryUsage += EstimateTypeSize(elementType) * 2000; // Input + output
            _estimatedMemoryUsage += EstimateTypeSize(keyType) * 1000; // Keys

            return node;
        }

        private static LambdaExpression GetLambdaOperand(Expression expression)
        {
            if (expression is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
            {
                return lambda;
            }
            
            if (expression is LambdaExpression directLambda)
            {
                return directLambda;
            }

            throw new InvalidOperationException($"Expected lambda expression, got {expression.GetType()}");
        }

        private static long EstimateTypeSize(Type type)
        {
            if (type.IsPrimitive)
            {
                return type.Name switch
                {
                    "Boolean" => sizeof(bool),
                    "Byte" => sizeof(byte),
                    "SByte" => sizeof(sbyte),
                    "Int16" => sizeof(short),
                    "UInt16" => sizeof(ushort),
                    "Int32" => sizeof(int),
                    "UInt32" => sizeof(uint),
                    "Int64" => sizeof(long),
                    "UInt64" => sizeof(ulong),
                    "Single" => sizeof(float),
                    "Double" => sizeof(double),
                    "Decimal" => sizeof(decimal),
                    "Char" => sizeof(char),
                    _ => IntPtr.Size
                };
            }

            if (type.IsValueType)
            {
                // Estimate for structs
                return type.GetFields().Sum(f => EstimateTypeSize(f.FieldType));
            }

            // Reference types
            return IntPtr.Size;
        }
        
        [RequiresDynamicCode("MakeArrayType requires dynamic code generation")]
        private static Type CreateArrayType(Type elementType)
        {
            return elementType.MakeArrayType();
        }
    }

    /// <summary>
    /// Validates expressions for GPU compatibility.
    /// </summary>
    private class ExpressionValidator : ExpressionVisitor
    {
        private List<ValidationError> _errors = [];

        public void Visit(Expression expression, List<ValidationError> errors)
        {
            _errors = errors;
            base.Visit(expression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check for unsupported method calls
            if (node.Method.DeclaringType?.Namespace?.StartsWith("System.IO") == true)
            {
                _errors.Add(new ValidationError("UNSUPPORTED_IO", "I/O operations are not supported in GPU queries", node));
            }

            if (node.Method.DeclaringType?.Namespace?.StartsWith("System.Net") == true)
            {
                _errors.Add(new ValidationError("UNSUPPORTED_NETWORK", "Network operations are not supported in GPU queries", node));
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitNew(NewExpression node)
        {
            // Check for unsupported types
            if (!IsGpuCompatibleType(node.Type))
            {
                _errors.Add(new ValidationError("UNSUPPORTED_TYPE", $"Type {node.Type} is not GPU-compatible", node));
            }

            return base.VisitNew(node);
        }

        private static bool IsGpuCompatibleType(Type type)
        {
            // Primitive types are GPU-compatible
            if (type.IsPrimitive || type == typeof(decimal))
                return true;

            // Arrays of primitives are compatible
            var elementType = type.GetElementType();
            if (type.IsArray && elementType != null && IsGpuCompatibleType(elementType))
                return true;

            // Simple structs without references are compatible
            if (type.IsValueType && !type.GetFields().Any(f => !IsGpuCompatibleType(f.FieldType)))
                return true;

            return false;
        }
    }
}

/// <summary>
/// Implementation of a compute plan.
/// </summary>
internal class ComputePlan : IComputePlan
{
    public ComputePlan(
        IReadOnlyList<IComputeStage> stages,
        IReadOnlyDictionary<string, Type> inputParameters,
        Type outputType,
        long estimatedMemoryUsage)
    {
        Id = Guid.NewGuid();
        Stages = stages;
        InputParameters = inputParameters;
        OutputType = outputType;
        EstimatedMemoryUsage = estimatedMemoryUsage;
        Metadata = new Dictionary<string, object>
        {
            ["CreatedAt"] = DateTime.UtcNow,
            ["Version"] = "1.0"
        };
    }

    public Guid Id { get; }
    public IReadOnlyList<IComputeStage> Stages { get; }
    public IReadOnlyDictionary<string, Type> InputParameters { get; }
    public Type OutputType { get; }
    public long EstimatedMemoryUsage { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Implementation of a compute stage.
/// </summary>
internal class ComputeStage : IComputeStage
{
    public ComputeStage(
        string id,
        Operators.IKernel kernel,
        IReadOnlyList<string> inputBuffers,
        string outputBuffer,
        ExecutionConfiguration configuration)
    {
        Id = id;
        Kernel = kernel;
        InputBuffers = inputBuffers;
        OutputBuffer = outputBuffer;
        Configuration = configuration;
    }

    public string Id { get; }
    public Operators.IKernel Kernel { get; }
    public IReadOnlyList<string> InputBuffers { get; }
    public string OutputBuffer { get; }
    public ExecutionConfiguration Configuration { get; }
}