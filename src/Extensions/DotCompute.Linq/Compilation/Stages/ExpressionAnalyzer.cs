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
using DotCompute.Linq.Pipelines.Analysis;
using CompilationOperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;
using PipelineOperatorInfo = DotCompute.Linq.Pipelines.Analysis.OperatorInfo;
using PipelineComplexityMetrics = DotCompute.Linq.Pipelines.Analysis.ComplexityMetrics;

namespace DotCompute.Linq.Compilation.Stages;

/// <summary>
/// Analyzes LINQ expression trees to extract structural information,
/// dependencies, and optimization opportunities.
/// </summary>
public sealed class ExpressionAnalyzer
{
    private readonly ILogger<ExpressionAnalyzer> _logger;
    private readonly Dictionary<Type, ITypeAnalyzer> _typeAnalyzers;
    private readonly Dictionary<ExpressionType, IOperatorAnalyzer> _operatorAnalyzers;

    public ExpressionAnalyzer(ILogger<ExpressionAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _typeAnalyzers = InitializeTypeAnalyzers();
        _operatorAnalyzers = InitializeOperatorAnalyzers();
    }

    /// <summary>
    /// Performs comprehensive analysis of an expression tree.
    /// </summary>
    public async Task<ExpressionAnalysisResult> AnalyzeAsync(
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
        context.DataFlowGraph = dataFlowAnalyzer.BuildDataFlowGraph(context.OperatorChain);
        
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
            var opportunity = await parallelAnalyzer.AnalyzeOperatorAsync(op, cancellationToken);
            return new { Operator = op, Opportunity = opportunity };
        });

        var results = await Task.WhenAll(tasks);
        
        context.ParallelizationOpportunities = results
            .Where(r => r.Opportunity.IsSuitable)
            .ToDictionary(r => r.Operator, r => r.Opportunity);
            
        _logger.LogDebug("Parallelization analysis completed: {OpportunityCount} suitable operators",
            context.ParallelizationOpportunities.Count);
    }

    private void AnalyzeMemoryAccess(AnalysisContext context)
    {
        var memoryAnalyzer = new MemoryAccessAnalyzer();
        
        // Analyze access patterns for each operator
        foreach (var op in context.OperatorChain)
        {
            var pattern = memoryAnalyzer.AnalyzeOperator(op);
            context.MemoryAccessPatterns[op] = pattern;
        }
        
        // Detect global patterns
        context.GlobalMemoryPattern = memoryAnalyzer.DetectGlobalPattern(
            context.MemoryAccessPatterns.Values);
            
        _logger.LogDebug("Memory access analysis completed: pattern {Pattern}",
            context.GlobalMemoryPattern.PatternType);
    }

    private void CalculateComplexityMetrics(AnalysisContext context)
    {
        var calculator = new ComplexityCalculator();
        
        context.ComplexityMetrics = calculator.Calculate(
            context.OperatorChain,
            context.TypeUsage,
            context.DataFlowGraph);
            
        _logger.LogDebug("Complexity metrics calculated: overall {Overall}, computational {Computational}",
            context.ComplexityMetrics.OverallComplexity,
            context.ComplexityMetrics.ComputationalComplexity);
    }

    private ExpressionAnalysisResult BuildAnalysisResult(AnalysisContext context, Expression expression)
    {
        return new ExpressionAnalysisResult(
            OperationSignature: GenerateOperationSignature(expression, context),
            OperatorChain: context.OperatorChain.ToList(),
            TypeUsage: new Dictionary<Type, TypeUsageInfo>(context.TypeUsage),
            Dependencies: context.Dependencies.ToList(),
            ComplexityMetrics: context.ComplexityMetrics,
            ParallelizationInfo: new ParallelizationInfo(
                context.ParallelizationOpportunities,
                context.DataFlowBottlenecks),
            MemoryAccessPattern: context.GlobalMemoryPattern,
            OptimizationHints: GenerateOptimizationHints(context));
    }

    private string GenerateOperationSignature(Expression expression, AnalysisContext context)
    {
        var signatureBuilder = new OperationSignatureBuilder();
        return signatureBuilder.Build(expression, context.OperatorChain);
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
        if (context.GlobalMemoryPattern.HasCoalescingOpportunities)
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

    private Dictionary<Type, ITypeAnalyzer> InitializeTypeAnalyzers()
    {
        return new Dictionary<Type, ITypeAnalyzer>
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

    private Dictionary<ExpressionType, IOperatorAnalyzer> InitializeOperatorAnalyzers()
    {
        return new Dictionary<ExpressionType, IOperatorAnalyzer>
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
}

/// <summary>
/// Context object that accumulates analysis information during expression traversal.
/// </summary>
internal class AnalysisContext
{
    public CompilationOptions Options { get; }
    public List<PipelineOperatorInfo> OperatorChain { get; } = new();
    public Dictionary<Type, TypeUsageInfo> TypeUsage { get; } = new();
    public HashSet<DependencyInfo> Dependencies { get; } = new();
    public Dictionary<PipelineOperatorInfo, ParallelizationOpportunity> ParallelizationOpportunities { get; set; } = new();
    public Dictionary<PipelineOperatorInfo, MemoryAccessPattern> MemoryAccessPatterns { get; } = new();
    public DataFlowGraph DataFlowGraph { get; set; } = new();
    public List<DataFlowBottleneck> DataFlowBottlenecks { get; set; } = new();
    public GlobalMemoryAccessPattern GlobalMemoryPattern { get; set; } = new();
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
    private readonly Dictionary<Type, ITypeAnalyzer> _typeAnalyzers;
    private readonly Dictionary<ExpressionType, IOperatorAnalyzer> _operatorAnalyzers;
    private AnalysisContext _context = null!;

    public AnalysisVisitor(
        ILogger logger,
        Dictionary<Type, ITypeAnalyzer> typeAnalyzers,
        Dictionary<ExpressionType, IOperatorAnalyzer> operatorAnalyzers)
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
            var operatorInfo = analyzer.Analyze(node, _context);
            _context.OperatorChain.Add(operatorInfo);
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
        var operatorInfo = analyzer.Analyze(node, _context);
        _context.OperatorChain.Add(operatorInfo);

        // Analyze parameter and return types
        foreach (var arg in node.Arguments)
        {
            AnalyzeType(arg.Type);
        }
        AnalyzeType(node.Type);

        // Add method dependency
        _context.Dependencies.Add(new DependencyInfo(
            DependencyType.Method,
            node.Method.DeclaringType?.FullName ?? "Unknown",
            node.Method.Name));

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
        var operatorInfo = analyzer.Analyze(node, _context);
        _context.OperatorChain.Add(operatorInfo);

        AnalyzeType(node.Type);
        AnalyzeType(node.Test.Type);

        return base.VisitConditional(node);
    }

    private void AnalyzeType(Type type)
    {
        if (_context.TypeUsage.ContainsKey(type))
        {
            _context.TypeUsage[type] = _context.TypeUsage[type] with 
            { 
                UsageCount = _context.TypeUsage[type].UsageCount + 1 
            };
            return;
        }

        var analyzer = GetTypeAnalyzer(type);
        var typeInfo = analyzer.Analyze(type, _context);
        _context.TypeUsage[type] = typeInfo;
    }

    private ITypeAnalyzer GetTypeAnalyzer(Type type)
    {
        // Try exact match first
        if (_typeAnalyzers.TryGetValue(type, out var analyzer))
            return analyzer;

        // Try generic type definition
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (_typeAnalyzers.TryGetValue(genericTypeDef, out analyzer))
                return analyzer;
        }

        // Try base types and interfaces
        var currentType = type.BaseType;
        while (currentType != null)
        {
            if (_typeAnalyzers.TryGetValue(currentType, out analyzer))
                return analyzer;
            currentType = currentType.BaseType;
        }

        // Default analyzer
        return new DefaultTypeAnalyzer();
    }
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
}