// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Kernels.Types;
using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Compilation.Context;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Linq.Compilation.Validation;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using DotCompute.Linq.Operators.Types;
using KernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
using ParameterDirection = DotCompute.Linq.Operators.Parameters.ParameterDirection;
using DotCompute.Linq.Compilation.Execution;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
namespace DotCompute.Linq.Compilation;
{
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
        {
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
        _logger.LogDebugMessage("Compiling expression of type {context.Expression.NodeType}");
        // Validate the expression
        var validationResult = Validate(context.Expression);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Expression validation failed: {validationResult.ErrorMessage}");
        }
        // Optimize the expression tree
        // Convert Abstractions.CompilationOptions to Linq.CompilationOptions
        var linqOptions = new Compilation.CompilationOptions
        {
            EnableOptimizations = context.Options.OptimizationLevel != OptimizationLevel.None,
            UseSharedMemory = true, // Default value
            EnableCaching = true // Default value
        };
        var optimizedExpression = _optimizer.Optimize(context.Expression, linqOptions);
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

    public DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var errors = new List<ValidationIssue>();
        var validator = new ExpressionValidator();
        try
        {
            validator.Visit(expression, errors);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Expression validation failed with exception");
            errors.Add(new ValidationIssue("VALIDATION_ERROR", ex.Message, ValidationSeverity.Error));
        }
        if (errors.Count > 0)
        {
            var message = $"Expression validation failed with {errors.Count} errors";
            return DotCompute.Abstractions.Validation.UnifiedValidationResult.Failure(message);
        }
        return DotCompute.Abstractions.Validation.UnifiedValidationResult.Success();
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
        }
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
        }
        public new List<IComputeStage> Visit(Expression expression)
        {
            _ = base.Visit(expression);
            return _stages;
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            _logger.LogDebugMessage("Visiting method call: {node.Method.Name}");
            // Handle LINQ operators
            if (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
            {
                }
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
                        _logger.LogWarningMessage("Unsupported LINQ method: {node.Method.Name}");
                        break;
                }
            return node != null ? base.VisitMethodCall(node) : node!;
        }
        private Expression VisitSelect(MethodCallExpression node)
        {
            // Visit the source
            _ = Visit(node.Arguments[0]);
            // Extract the selector lambda
            var selectorLambda = GetLambdaOperand(node.Arguments[1]);
            var inputType = selectorLambda.Parameters[0].Type;
            var outputType = selectorLambda.Body.Type;
            // Create a map kernel
            var kernelDefinition = new KernelDefinition
                Name = $"Select_{_stageCounter++}",
                Parameters =
                [
                    new KernelParameter("input", CreateArrayType(inputType), ParameterDirection.In),
                new KernelParameter("output", CreateArrayType(outputType), ParameterDirection.Out),
                new KernelParameter("count", typeof(int), ParameterDirection.In)
                ],
                Language = DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp
                $"stage_{_stageCounter}",
                kernel,
                new[] { "input" },
                "output",
                new ExecutionConfiguration
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
            // Extract the predicate lambda
            var predicateLambda = GetLambdaOperand(node.Arguments[1]);
            var elementType = predicateLambda.Parameters[0].Type;
            // Create a filter kernel
                Name = $"Where_{_stageCounter++}",
                    new KernelParameter("input", CreateArrayType(elementType), ParameterDirection.In),
                new KernelParameter("output", CreateArrayType(elementType), ParameterDirection.Out),
                new KernelParameter("predicate_results", typeof(bool[]), ParameterDirection.Out),
                    GridDimensions = (1, 1, 1)
            _outputType = CreateArrayType(elementType);
            _estimatedMemoryUsage += EstimateTypeSize(elementType) * 2000; // Input + output
            _estimatedMemoryUsage += sizeof(bool) * 1000; // Predicate results
        }
        private Expression VisitAggregate(MethodCallExpression node)
        {
            var elementType = node.Arguments[0].Type.GetGenericArguments()[0];
            var resultType = node.Type;
            // Create an aggregation kernel
                Name = $"{node.Method.Name}_{_stageCounter++}",
                new KernelParameter("result", resultType, ParameterDirection.Out),
                "result",
                    GridDimensions = (1, 1, 1),
                    SharedMemorySize = (int)(256 * EstimateTypeSize(elementType)) // For reduction
            _outputType = resultType;
            _estimatedMemoryUsage += EstimateTypeSize(elementType) * 1000;
            _estimatedMemoryUsage += EstimateTypeSize(resultType);
        }
        private Expression VisitOrderBy(MethodCallExpression node)
        {
            var keySelectorLambda = GetLambdaOperand(node.Arguments[1]);
            var elementType = keySelectorLambda.Parameters[0].Type;
            var keyType = keySelectorLambda.Body.Type;
            // Create a sort kernel
                new KernelParameter("keys", CreateArrayType(keyType), ParameterDirection.InOut),
            _estimatedMemoryUsage += EstimateTypeSize(keyType) * 1000; // Keys
        }
        private static LambdaExpression GetLambdaOperand(Expression expression)
        {
            if (expression is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
                return lambda;
            if (expression is LambdaExpression directLambda)
                return directLambda;
            throw new InvalidOperationException($"Expected lambda expression, got {expression.GetType()}");
        }
        private static long EstimateTypeSize(Type type)
        {
            if (type.IsPrimitive)
                return type.Name switch
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
            if (type.IsValueType)
                // Estimate for structs
                return type.GetFields().Sum(f => EstimateTypeSize(f.FieldType));
            // Reference types
            return IntPtr.Size;
        [RequiresDynamicCode("MakeArrayType requires dynamic code generation")]
        private static Type CreateArrayType(Type elementType) => elementType.MakeArrayType();
        private Operators.Interfaces.IKernel CreateKernelFromDefinition(Operators.Types.KernelDefinition kernelDefinition)
            // Convert LINQ KernelDefinition to Core KernelDefinition
            var coreDefinition = Operators.Adapters.KernelDefinitionAdapter.ConvertToCoreDefinition(kernelDefinition);
            // Create a GeneratedKernel from the definition
            var generatedKernel = new Operators.Generation.GeneratedKernel
                Name = kernelDefinition.Name,
                Source = kernelDefinition.Source ?? string.Empty,
                Parameters = kernelDefinition.Parameters.Select(p => new Operators.Generation.GeneratedKernelParameter
                    Name = p.Name,
                    Type = p.Type,
                    IsInput = p.Direction != Operators.Parameters.ParameterDirection.Out,
                    IsOutput = p.Direction != Operators.Parameters.ParameterDirection.In
                }).ToArray()
            return new Operators.Kernels.DynamicCompiledKernel(generatedKernel, _accelerator, _logger);
    /// Validates expressions for GPU compatibility.
    private class ExpressionValidator : ExpressionVisitor
    {
        private List<ValidationIssue> _errors = [];
        }
        public void Visit(Expression expression, List<ValidationIssue> errors)
        {
            _errors = errors;
            // Check for unsupported method calls
            if (node.Method.DeclaringType?.Namespace?.StartsWith("System.IO") == true)
                _errors.Add(new ValidationIssue("UNSUPPORTED_IO", "I/O operations are not supported in GPU queries", ValidationSeverity.Error));
            if (node?.Method.DeclaringType?.Namespace?.StartsWith("System.Net") == true)
                _errors.Add(new ValidationIssue("UNSUPPORTED_NETWORK", "Network operations are not supported in GPU queries", ValidationSeverity.Error));
        }
        protected override Expression VisitNew(NewExpression node)
        {
            // Check for unsupported types
            if (!IsGpuCompatibleType(node.Type))
                _errors.Add(new ValidationIssue("UNSUPPORTED_TYPE", $"Type {node?.Type} is not GPU-compatible", ValidationSeverity.Error));
            return node != null ? base.VisitNew(node) : node!;
        }
        private static bool IsGpuCompatibleType(Type type)
        {
            // Primitive types are GPU-compatible
            if (type.IsPrimitive || type == typeof(decimal))
            {
                return true;
            }
            // Arrays of primitives are compatible
            var elementType = type.GetElementType();
            if (type.IsArray && elementType != null && IsGpuCompatibleType(elementType))
            {
                return true;
            }

            // Simple structs without references are compatible
            if (type.IsValueType && !type.GetFields().Any(f => !IsGpuCompatibleType(f.FieldType)))
            {
                return true;
            }

            return false;
        }
    }
}
