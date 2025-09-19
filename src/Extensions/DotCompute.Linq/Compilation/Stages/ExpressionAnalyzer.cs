using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Analysis;
using DotCompute.Linq.Analysis;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Core.Analysis;
using BackendType = DotCompute.Linq.Types.BackendType;
using PipelineOperatorInfo = DotCompute.Linq.Compilation.Analysis.PipelineOperatorInfo;
using PipelineComplexityMetrics = DotCompute.Linq.Pipelines.Analysis.ComplexityMetrics;
using CompilationITypeAnalyzer = DotCompute.Linq.Compilation.Analysis.ITypeAnalyzer;
using CompilationIOperatorAnalyzer = DotCompute.Linq.Compilation.Analysis.IOperatorAnalyzer;
using AnalysisIOperatorAnalyzer = DotCompute.Linq.Analysis.IOperatorAnalyzer;
using CompilationDependencyInfo = DotCompute.Linq.Compilation.Analysis.DependencyInfo;
using DotCompute.Core.Optimization.Enums;
using CompilationDependencyType = DotCompute.Linq.Compilation.Analysis.DependencyType;
using AnalysisDependencyInfo = DotCompute.Linq.Analysis.DependencyInfo;
using AnalysisDependencyType = DotCompute.Linq.Analysis.DependencyType;

namespace DotCompute.Linq.Compilation.Stages;

/// <summary>
/// Analyzes LINQ expression trees to extract structural information,
/// dependencies, and optimization opportunities.
/// </summary>
public sealed class ExpressionAnalyzer
{
    private readonly ILogger<ExpressionAnalyzer> _logger;
    private readonly Dictionary<Type, CompilationITypeAnalyzer> _typeAnalyzers;
    private readonly Dictionary<ExpressionType, AnalysisIOperatorAnalyzer> _operatorAnalyzers;

    public ExpressionAnalyzer(ILogger<ExpressionAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _typeAnalyzers = InitializeTypeAnalyzers();
        _operatorAnalyzers = InitializeOperatorAnalyzers();
    }

    /// <summary>
    /// Performs comprehensive analysis of an expression tree.
    /// </summary>
    public async Task<DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult> AnalyzeAsync(
        Expression expression,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting expression analysis for {ExpressionType}", expression.Type);

        var visitor = new AnalysisVisitor(_logger, _typeAnalyzers, _operatorAnalyzers);
        var context = new AnalysisContext(options ?? CompilationOptions.Default);

        // Traverse the expression tree

        visitor.Visit(expression, context);

        // Perform post-processing analysis

        await PerformPostAnalysisAsync(context, cancellationToken);


        var result = BuildAnalysisResult(context, expression);


        _logger.LogDebug("Expression analysis completed: {OperatorCount} operators, complexity {Complexity}",
            result.OperatorChain.Count, result.ComplexityMetrics.OverallComplexity);


        return result;
    }

    private async Task PerformPostAnalysisAsync(AnalysisContext context, CancellationToken cancellationToken)
    {
        // Analyze data flow patterns
        AnalyzeDataFlow(context);

        // Detect parallelization opportunities

        await AnalyzeParallelizationOpportunitiesAsync(context, cancellationToken);

        // Analyze memory access patterns

        AnalyzeMemoryAccess(context);

        // Calculate complexity metrics

        CalculateComplexityMetrics(context);
    }

    private void AnalyzeDataFlow(AnalysisContext context)
    {
        var dataFlowAnalyzer = new DataFlowAnalyzer();
        var operatorInfoList = context.OperatorChain.Select(op => new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
        {
            Name = op.Name,
            OperatorType = UnifiedOperatorType.Custom, // Default to Custom since op.OperatorType is System.Type
            InputTypes = [typeof(object)], // Default input type
            OutputType = typeof(object) // Default output type
        }).ToList();
        context.DataFlowGraph = dataFlowAnalyzer.BuildDataFlowGraph(operatorInfoList);

        // Identify bottlenecks

        context.DataFlowBottlenecks = dataFlowAnalyzer.IdentifyBottlenecks(context.DataFlowGraph);


        _logger.LogDebug("Data flow analysis completed: {NodeCount} nodes, {BottleneckCount} bottlenecks",
            context.DataFlowGraph.Nodes.Count, context.DataFlowBottlenecks.Count);
    }

    private async Task AnalyzeParallelizationOpportunitiesAsync(AnalysisContext context, CancellationToken cancellationToken)
    {
        var parallelAnalyzer = new ParallelizationAnalyzer();

        // Analyze each operator for parallelization potential

        var tasks = context.OperatorChain.Select(async op =>
        {
            // Create a dummy expression for the operator analysis
            var dummyExpression = Expression.Constant(op.Name);
            var opportunity = await parallelAnalyzer.AnalyzeOperatorAsync(dummyExpression, cancellationToken);
            return new { Operator = op, Opportunity = opportunity };
        });

        var results = await Task.WhenAll(tasks);


        context.ParallelizationOpportunities = results
            .Where(r => r.Opportunity.VectorizationSuitable || r.Opportunity.SupportsParallelExecution)
            .ToDictionary(r => r.Operator, r => ConvertToAnalysisParallelizationOpportunity(r.Opportunity));


        _logger.LogDebug("Parallelization analysis completed: {OpportunityCount} suitable operators",
            context.ParallelizationOpportunities.Count);
    }

    private void AnalyzeMemoryAccess(AnalysisContext context)
    {
        var memoryAnalyzer = new MemoryAccessAnalyzer();

        // Analyze access patterns for each operator

        foreach (var op in context.OperatorChain)
        {
            var operatorInfo = ConvertToOperatorInfo(op);
            var analysisResult = memoryAnalyzer.AnalyzeOperator(operatorInfo);
            context.MemoryAccessPatterns[op] = analysisResult.AccessPattern;
        }

        // Detect global patterns

        context.GlobalMemoryPattern = memoryAnalyzer.DetectGlobalPattern(
            context.OperatorChain.Select(ConvertToOperatorInfo));


        _logger.LogDebug("Memory access analysis completed: pattern {Pattern}",
            context.GlobalMemoryPattern.PatternType);
    }

    private void CalculateComplexityMetrics(AnalysisContext context)
    {
        var calculator = new ComplexityCalculator();


        var compilationMetrics = calculator.Calculate(
            context.OperatorChain.Select(ConvertToOperatorInfo),
            context.TypeUsage.Values,
            context.DataFlowGraph);
        
        context.ComplexityMetrics = ConvertToPipelineComplexityMetrics(compilationMetrics);


        _logger.LogDebug("Complexity metrics calculated: overall {Overall}, computational {Computational}",
            context.ComplexityMetrics.OverallComplexity,
            context.ComplexityMetrics.ComputationalComplexity);
    }

    private DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult BuildAnalysisResult(AnalysisContext context, Expression expression)
    {
        return new DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult(
            OperationSignature: GenerateOperationSignature(expression, context),
            OperatorChain: context.OperatorChain.ToList(),
            TypeUsage: context.TypeUsage.ToDictionary(kv => kv.Key, kv => kv.Value),
            Dependencies: context.Dependencies.ToList(),
            ComplexityMetrics: ConvertToPipelineComplexityMetrics(context.ComplexityMetrics),
            ParallelizationInfo: new DotCompute.Linq.Compilation.Analysis.ParallelizationInfo(
                new List<object>(), // TODO: Fix ParallelizationOpportunities conversion
                context.DataFlowBottlenecks),
            MemoryAccessPattern: context.GlobalMemoryPattern,
            OptimizationHints: GenerateOptimizationHints(context));
    }

    private static GlobalMemoryAccessPattern ConvertToGlobalMemoryAccessPattern(MemoryAccessPattern pattern)
    {
        return new GlobalMemoryAccessPattern
        {
            AccessType = pattern switch
            {
                MemoryAccessPattern.Sequential => MemoryAccessType.Sequential,
                MemoryAccessPattern.Random => MemoryAccessType.Random,
                MemoryAccessPattern.Strided => MemoryAccessType.Strided,
                _ => MemoryAccessType.Sequential
            },
            IsCoalesced = pattern == MemoryAccessPattern.Sequential,
            CacheEfficiency = pattern == MemoryAccessPattern.Sequential ? 0.9 : 0.5
        };
    }

    private static MemoryAccessPattern ConvertToMemoryAccessPattern(GlobalMemoryAccessPattern pattern)
    {
        return pattern.AccessType switch
        {
            MemoryAccessType.Sequential => MemoryAccessPattern.Sequential,
            MemoryAccessType.Random => MemoryAccessPattern.Random,
            MemoryAccessType.Strided => MemoryAccessPattern.Strided,
            _ => MemoryAccessPattern.Sequential
        };
    }

    private string GenerateOperationSignature(Expression expression, AnalysisContext context)
    {
        var signatureBuilder = new OperationSignatureBuilder();
        return signatureBuilder.Build(expression, context.OperatorChain);
    }

    private MemoryAccessPattern ConvertToCompilationMemoryAccessPattern(GlobalMemoryAccessPattern pattern)
    {
        return pattern.AccessType switch
        {
            MemoryAccessType.Sequential => MemoryAccessPattern.Sequential,
            MemoryAccessType.Random => MemoryAccessPattern.Random,
            MemoryAccessType.Strided => MemoryAccessPattern.Strided,
            MemoryAccessType.Coalesced => MemoryAccessPattern.Coalesced,
            MemoryAccessType.Scattered => MemoryAccessPattern.Scattered,
            _ => MemoryAccessPattern.Sequential
        };
    }

    private List<OptimizationHint> GenerateOptimizationHints(AnalysisContext context)
    {
        var hints = new List<OptimizationHint>();

        // Vectorization hints

        if (context.ParallelizationOpportunities.Any(kv => kv.Value.VectorizationSuitable))
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.Vectorization,
                "Expression contains operations suitable for SIMD vectorization",
                OptimizationImpact.High));
        }

        // Memory optimization hints

        if (context.GlobalMemoryPattern.Pattern == MemoryAccessType.Sequential)
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.MemoryCoalescing,
                "Memory access pattern can benefit from coalescing optimization",
                OptimizationImpact.Medium));
        }

        // Kernel fusion hints

        if (context.OperatorChain.Count > 3)
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.KernelFusion,
                "Multiple operators can be fused into a single kernel",
                OptimizationImpact.High));
        }

        // Type specialization hints

        if (context.TypeUsage.Values.Any(t => t.RequiresSpecialization))
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.TypeSpecialization,
                "Generic operations can benefit from type specialization",
                OptimizationImpact.Medium));
        }


        return hints;
    }

    private Dictionary<Type, CompilationITypeAnalyzer> InitializeTypeAnalyzers()
    {
        return new Dictionary<Type, CompilationITypeAnalyzer>
        {
            [typeof(int)] = new NumericTypeAnalyzer<int>(),
            [typeof(float)] = new NumericTypeAnalyzer<float>(),
            [typeof(double)] = new NumericTypeAnalyzer<double>(),
            [typeof(long)] = new NumericTypeAnalyzer<long>(),
            [typeof(decimal)] = new DecimalTypeAnalyzer(),
            [typeof(string)] = new StringTypeAnalyzer(),
            [typeof(bool)] = new BooleanTypeAnalyzer()
        };
    }

    private Dictionary<ExpressionType, AnalysisIOperatorAnalyzer> InitializeOperatorAnalyzers()
    {
        return new Dictionary<ExpressionType, AnalysisIOperatorAnalyzer>
        {
            [ExpressionType.Add] = new ArithmeticOperatorAnalyzer(),
            [ExpressionType.Subtract] = new ArithmeticOperatorAnalyzer(),
            [ExpressionType.Multiply] = new ArithmeticOperatorAnalyzer(),
            [ExpressionType.Divide] = new ArithmeticOperatorAnalyzer(),
            [ExpressionType.Equal] = new ComparisonOperatorAnalyzer(),
            [ExpressionType.NotEqual] = new ComparisonOperatorAnalyzer(),
            [ExpressionType.LessThan] = new ComparisonOperatorAnalyzer(),
            [ExpressionType.GreaterThan] = new ComparisonOperatorAnalyzer(),
            [ExpressionType.AndAlso] = new LogicalOperatorAnalyzer(),
            [ExpressionType.OrElse] = new LogicalOperatorAnalyzer(),
            [ExpressionType.Call] = new MethodCallOperatorAnalyzer(),
            [ExpressionType.Conditional] = new ConditionalOperatorAnalyzer()
        };
    }

    /// <summary>
    /// Converts a compilation analysis parallelization opportunity to the analysis version.
    /// </summary>
    private static DotCompute.Linq.Analysis.ParallelizationOpportunity ConvertToAnalysisParallelizationOpportunity(
        DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity compilationOpportunity)
    {
        return new DotCompute.Linq.Analysis.ParallelizationOpportunity
        {
            VectorizationSuitable = compilationOpportunity.VectorizationSuitable,
            SupportsParallelExecution = compilationOpportunity.SupportsParallelExecution,
            RecommendedParallelism = compilationOpportunity.RecommendedParallelism,
            DataDependencies = [], // Will need to map if properties exist
            MemoryRequirements = 0 // Default value
        };
    }

    /// <summary>
    /// Converts PipelineOperatorInfo to OperatorInfo.
    /// </summary>
    private static DotCompute.Linq.Compilation.Analysis.OperatorInfo ConvertToOperatorInfo(PipelineOperatorInfo pipelineOp)
    {
        return new DotCompute.Linq.Compilation.Analysis.OperatorInfo
        {
            Operator = ConvertToExpressionType(pipelineOp.Name),
            Complexity = pipelineOp.ComplexityScore,
            IsCommutative = IsCommutativeOperator(pipelineOp.Name),
            IsAssociative = IsAssociativeOperator(pipelineOp.Name),
            AccessPattern = MemoryAccessPattern.Sequential,
            ParallelizationOpportunity = new DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity
            {
                VectorizationSuitable = pipelineOp.SupportsGpu,
                SupportsParallelExecution = pipelineOp.CanParallelize,
                RecommendedParallelism = pipelineOp.CanParallelize ? 4 : 1
            }
        };
    }

    /// <summary>
    /// Converts operator name to ExpressionType.
    /// </summary>
    private static ExpressionType ConvertToExpressionType(string operatorName)
    {
        return operatorName switch
        {
            "Addition" => ExpressionType.Add,
            "Subtraction" => ExpressionType.Subtract,
            "Multiplication" => ExpressionType.Multiply,
            "Division" => ExpressionType.Divide,
            "Equal" => ExpressionType.Equal,
            "NotEqual" => ExpressionType.NotEqual,
            "LessThan" => ExpressionType.LessThan,
            "GreaterThan" => ExpressionType.GreaterThan,
            "AndAlso" => ExpressionType.AndAlso,
            "OrElse" => ExpressionType.OrElse,
            "Conditional" => ExpressionType.Conditional,
            "MethodCall" => ExpressionType.Call,
            _ => ExpressionType.Call
        };
    }

    /// <summary>
    /// Determines if an operator is commutative.
    /// </summary>
    private static bool IsCommutativeOperator(string operatorName)
    {
        return operatorName switch
        {
            "Addition" or "Multiplication" or "Equal" or "NotEqual" => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an operator is associative.
    /// </summary>
    private static bool IsAssociativeOperator(string operatorName)
    {
        return operatorName switch
        {
            "Addition" or "Multiplication" => true,
            _ => false
        };
    }

    /// <summary>
    /// Converts Compilation.Analysis.PipelineComplexityMetrics to Pipelines.Analysis.ComplexityMetrics.
    /// </summary>
    private static PipelineComplexityMetrics ConvertToPipelineComplexityMetrics(
        DotCompute.Linq.Compilation.Analysis.PipelineComplexityMetrics compilationMetrics)
    {
        return new PipelineComplexityMetrics
        {
            OverallComplexity = compilationMetrics.OverallComplexity,
            OperationCount = compilationMetrics.OperationCount,
            MemoryUsage = compilationMetrics.MemoryUsage,
            ParallelizationPotential = (int)compilationMetrics.ParallelizationPotential,
            GpuRecommended = compilationMetrics.GpuRecommended,
            ComplexityByCategory = compilationMetrics.ComplexityByCategory.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    /// <summary>
    /// Converts Pipelines.Analysis.ComplexityMetrics to Compilation.Analysis.PipelineComplexityMetrics.
    /// </summary>
    private static DotCompute.Linq.Compilation.Analysis.PipelineComplexityMetrics ConvertToPipelineComplexityMetrics(
        DotCompute.Linq.Pipelines.Analysis.ComplexityMetrics pipelineMetrics)
    {
        return new DotCompute.Linq.Compilation.Analysis.PipelineComplexityMetrics
        {
            OverallComplexity = pipelineMetrics.OverallComplexity,
            OperationCount = (int)pipelineMetrics.OperationCount,
            MemoryUsage = pipelineMetrics.MemoryUsage,
            ParallelizationPotential = pipelineMetrics.ParallelizationPotential,
            GpuRecommended = pipelineMetrics.GpuRecommended,
            ComplexityByCategory = pipelineMetrics.ComplexityByCategory.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    /// <summary>
    /// Converts Analysis.ParallelizationOpportunity to Compilation.Analysis.ParallelizationOpportunity.
    /// </summary>
    private static DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity ConvertParallelizationOpportunity(
        DotCompute.Linq.Analysis.ParallelizationOpportunity analysisOpportunity)
    {
        return new DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity
        {
            VectorizationSuitable = analysisOpportunity.VectorizationSuitable,
            SupportsParallelExecution = analysisOpportunity.SupportsParallelExecution,
            RecommendedParallelism = analysisOpportunity.RecommendedParallelism
        };
    }

    /// <summary>
    /// Converts compilation operator type to unified operator type.
    /// Updated to use UnifiedOperatorType instead of obsolete types.
    /// </summary>
    private static UnifiedOperatorType ConvertOperatorType(
        UnifiedOperatorType compilationOperatorType)
    {
        return compilationOperatorType; // Already unified type, no conversion needed
    }
}

/// <summary>
/// Context object that accumulates analysis information during expression traversal.
/// </summary>
internal class AnalysisContext
{
    public CompilationOptions Options { get; }
    public List<PipelineOperatorInfo> OperatorChain { get; } = [];
    public Dictionary<Type, TypeUsageInfo> TypeUsage { get; } = [];
    public HashSet<CompilationDependencyInfo> Dependencies { get; } = [];
    public Dictionary<PipelineOperatorInfo, DotCompute.Linq.Analysis.ParallelizationOpportunity> ParallelizationOpportunities { get; set; } = [];
    public Dictionary<PipelineOperatorInfo, DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern> MemoryAccessPatterns { get; } = [];
    public DotCompute.Linq.Analysis.DataFlowGraph DataFlowGraph { get; set; } = new();
    public List<DotCompute.Linq.Analysis.DataFlowBottleneck> DataFlowBottlenecks { get; set; } = [];
    public DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern GlobalMemoryPattern { get; set; } = new();
    public PipelineComplexityMetrics ComplexityMetrics { get; set; } = new();

    public AnalysisContext(CompilationOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }
}

/// <summary>
/// Visitor that traverses expression trees and gathers analysis information.
/// </summary>
internal class AnalysisVisitor : ExpressionVisitor
{
    private readonly ILogger _logger;
    private readonly Dictionary<Type, CompilationITypeAnalyzer> _typeAnalyzers;
    private readonly Dictionary<ExpressionType, AnalysisIOperatorAnalyzer> _operatorAnalyzers;
    private AnalysisContext _context = null!;

    public AnalysisVisitor(
        ILogger logger,
        Dictionary<Type, CompilationITypeAnalyzer> typeAnalyzers,
        Dictionary<ExpressionType, AnalysisIOperatorAnalyzer> operatorAnalyzers)
    {
        _logger = logger;
        _typeAnalyzers = typeAnalyzers;
        _operatorAnalyzers = operatorAnalyzers;
    }

    public void Visit(Expression expression, AnalysisContext context)
    {
        _context = context;
        Visit(expression);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Analyze the binary operation
        if (_operatorAnalyzers.TryGetValue(node.NodeType, out var analyzer))
        {
            var analysisResult = analyzer.Analyze(node, _context);
            var pipelineOperatorInfo = ConvertToPipelineOperatorInfo(analysisResult, node);
            _context.OperatorChain.Add(pipelineOperatorInfo);
        }

        // Analyze operand types
        AnalyzeType(node.Left.Type);
        AnalyzeType(node.Right.Type);
        AnalyzeType(node.Type);

        return base.VisitBinary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Analyze the method call
        var analyzer = _operatorAnalyzers.GetValueOrDefault(ExpressionType.Call, new MethodCallOperatorAnalyzer());
        var analysisResult = analyzer.Analyze(node, _context);
        var pipelineOperatorInfo = ConvertToPipelineOperatorInfo(analysisResult, node);
        _context.OperatorChain.Add(pipelineOperatorInfo);

        // Analyze parameter and return types
        foreach (var arg in node.Arguments)
        {
            AnalyzeType(arg.Type);
        }
        AnalyzeType(node.Type);

        // Add method dependency
        _context.Dependencies.Add(new CompilationDependencyInfo
        {
            Type = CompilationDependencyType.Method,
            DependentOperation = node.Method.DeclaringType?.FullName ?? "Unknown",
            Dependencies = [node.Method.Name]
        });

        return base.VisitMethodCall(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        // Analyze lambda parameters
        foreach (var parameter in node.Parameters)
        {
            AnalyzeType(parameter.Type);
        }

        return base.VisitLambda(node);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        var analyzer = _operatorAnalyzers.GetValueOrDefault(ExpressionType.Conditional, new ConditionalOperatorAnalyzer());
        var analysisResult = analyzer.Analyze(node, _context);
        var pipelineOperatorInfo = ConvertToPipelineOperatorInfo(analysisResult, node);
        _context.OperatorChain.Add(pipelineOperatorInfo);

        AnalyzeType(node.Type);
        AnalyzeType(node.Test.Type);

        return base.VisitConditional(node);
    }

    private void AnalyzeType(Type type)
    {
        if (_context.TypeUsage.ContainsKey(type))
        {
            _context.TypeUsage[type].UsageCount++;
            return;
        }

        var analyzer = GetTypeAnalyzer(type);
        // Create a default TypeUsageInfo since we don't have the full expression context here
        var typeInfo = new TypeUsageInfo
        {
            Type = type,
            UsageCount = 1,
            IsGpuCompatible = analyzer.SupportsVectorization(),
            TypeSize = System.Runtime.InteropServices.Marshal.SizeOf(type.IsValueType ? type : typeof(IntPtr))
        };
        _context.TypeUsage[type] = typeInfo;
    }

    private CompilationITypeAnalyzer GetTypeAnalyzer(Type type)
    {
        // Try exact match first
        if (_typeAnalyzers.TryGetValue(type, out var analyzer))
        {

            return analyzer;
        }

        // Try generic type definition

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (_typeAnalyzers.TryGetValue(genericTypeDef, out analyzer))
            {

                return analyzer;
            }

        }

        // Try base types and interfaces
        var currentType = type.BaseType;
        while (currentType != null)
        {
            if (_typeAnalyzers.TryGetValue(currentType, out analyzer))
            {

                return analyzer;
            }


            currentType = currentType.BaseType;
        }

        // Default analyzer
        return new DefaultTypeAnalyzer(type);
    }

    /// <summary>
    /// Converts an OperatorAnalysisResult to a PipelineOperatorInfo.
    /// </summary>
    private static PipelineOperatorInfo ConvertToPipelineOperatorInfo(OperatorAnalysisResult analysisResult, Expression expression)
    {
        return new PipelineOperatorInfo
        {
            Name = GetOperatorName(analysisResult.OperatorType),
            OperatorType = expression.Type, // System.Type
            ComplexityScore = (int)analysisResult.Complexity,
            CanParallelize = analysisResult.SupportsVectorization,
            MemoryRequirement = analysisResult.OptimalVectorWidth * 8, // Estimate based on vector width
            Metadata = new Dictionary<string, object>
            {
                ["ExpressionType"] = analysisResult.OperatorType,
                ["OperandTypes"] = analysisResult.OperandTypes ?? Array.Empty<Type>(),
                ["SupportsVectorization"] = analysisResult.SupportsVectorization,
                ["OptimalVectorWidth"] = analysisResult.OptimalVectorWidth
            }
        };
    }

    /// <summary>
    /// Gets a human-readable name for the operator.
    /// </summary>
    private static string GetOperatorName(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add => "Addition",
        ExpressionType.Subtract => "Subtraction",

        ExpressionType.Multiply => "Multiplication",
        ExpressionType.Divide => "Division",
        ExpressionType.Modulo => "Modulo",
        ExpressionType.Power => "Power",
        ExpressionType.Negate => "Negation",
        ExpressionType.Call => "MethodCall",
        ExpressionType.Conditional => "Conditional",
        _ => operatorType.ToString()
    };
}

/// <summary>
/// Builds operation signatures for caching and identification.
/// </summary>
internal class OperationSignatureBuilder
{
    public string Build(Expression expression, IReadOnlyList<PipelineOperatorInfo> operatorChain)
    {
        var parts = new List<string>
        {
            expression.Type.Name,
            operatorChain.Count.ToString()
        };

        foreach (var op in operatorChain)
        {
            parts.Add($"{op.OperatorType}({string.Join(",", op.InputTypes.Select(t => t.Name))})");
        }

        return string.Join("|", parts);
    }

    /// <summary>
    /// Converts an OperatorAnalysisResult to a PipelineOperatorInfo.
    /// </summary>
    private static PipelineOperatorInfo ConvertToPipelineOperatorInfo(OperatorAnalysisResult analysisResult, Expression expression)
    {
        return new PipelineOperatorInfo
        {
            OperatorType = typeof(object), // Will be properly set based on expression type
            Name = GetOperatorName(analysisResult.OperatorType),
            InputTypes = analysisResult.OperandTypes?.ToList() ?? [],
            OutputType = expression.Type,
            ComplexityScore = (int)analysisResult.Complexity,
            SupportsGpu = analysisResult.BackendCompatibility.GetValueOrDefault(BackendType.CUDA)?.IsSupported ?? false,
            SupportsCpu = analysisResult.BackendCompatibility.GetValueOrDefault(BackendType.CPU)?.IsSupported ?? true,
            IsNativelySupported = analysisResult.SupportsVectorization,
            Implementation = analysisResult.Implementation.ToString(),
            PerformanceCost = analysisResult.OptimalVectorWidth * 0.1,
            Accuracy = (double)(int)analysisResult.Accuracy.AccuracyLevel
        };
    }

    /// <summary>
    /// Converts ExpressionType to UnifiedOperatorType.
    /// Updated to use UnifiedOperatorType instead of obsolete types.
    /// </summary>
    private static UnifiedOperatorType ConvertExpressionTypeToUnifiedOperatorType(ExpressionType expressionType)
    {
        return expressionType switch
        {
            ExpressionType.Add => UnifiedOperatorType.Add,
            ExpressionType.Subtract => UnifiedOperatorType.Subtract,
            ExpressionType.Multiply => UnifiedOperatorType.Multiply,
            ExpressionType.Divide => UnifiedOperatorType.Divide,
            ExpressionType.Power => UnifiedOperatorType.Power,
            ExpressionType.Modulo => UnifiedOperatorType.Modulo,
            ExpressionType.Equal => UnifiedOperatorType.Equal,
            ExpressionType.NotEqual => UnifiedOperatorType.NotEqual,
            ExpressionType.LessThan => UnifiedOperatorType.LessThan,
            ExpressionType.LessThanOrEqual => UnifiedOperatorType.LessThanOrEqual,
            ExpressionType.GreaterThan => UnifiedOperatorType.GreaterThan,
            ExpressionType.GreaterThanOrEqual => UnifiedOperatorType.GreaterThanOrEqual,
            ExpressionType.AndAlso or ExpressionType.And => UnifiedOperatorType.LogicalAnd,
            ExpressionType.OrElse or ExpressionType.Or => UnifiedOperatorType.LogicalOr,
            ExpressionType.ExclusiveOr => UnifiedOperatorType.LogicalXor,
            ExpressionType.Not => UnifiedOperatorType.LogicalNot,
            ExpressionType.Conditional => UnifiedOperatorType.Conditional,
            ExpressionType.Convert or ExpressionType.ConvertChecked => UnifiedOperatorType.Conversion,
            ExpressionType.Call => UnifiedOperatorType.MethodCall,
            _ => UnifiedOperatorType.Unknown
        };
    }

    /// <summary>
    /// Gets a human-readable name for the operator.
    /// </summary>
    private static string GetOperatorName(ExpressionType operatorType) => operatorType switch
    {
        ExpressionType.Add => "Addition",
        ExpressionType.Subtract => "Subtraction",
        ExpressionType.Multiply => "Multiplication",
        ExpressionType.Divide => "Division",
        ExpressionType.Modulo => "Modulo",
        ExpressionType.Power => "Power",
        ExpressionType.Negate => "Negation",
        ExpressionType.Call => "MethodCall",
        ExpressionType.Conditional => "Conditional",
        _ => operatorType.ToString()
    };

    /// <summary>
    /// Converts PipelineOperatorInfo list to OperatorInfo list for data flow analysis.
    /// </summary>
    public static List<DotCompute.Linq.Pipelines.Analysis.OperatorInfo> ConvertToOperatorInfo(List<PipelineOperatorInfo> pipelineOperators)
    {
        return pipelineOperators.Select(op => new DotCompute.Linq.Pipelines.Analysis.OperatorInfo
        {
            Name = op.Name,
            OperatorType = UnifiedOperatorType.Custom, // Default to custom
            InputTypes = [op.OperatorType],
            OutputType = typeof(object) // Default output type
        }).ToList();
    }

    /// <summary>
    /// Checks if a memory access pattern has coalescing opportunities.
    /// </summary>
    private static bool HasCoalescingOpportunities(DotCompute.Linq.Compilation.Analysis.GlobalMemoryAccessPattern pattern)
    {
        return pattern.PredominantPattern == DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Random ||
               pattern.PredominantPattern == DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Strided;
    }
}