// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
namespace DotCompute.Linq.Pipelines.Analysis;
{
/// <summary>
/// Context for pipeline expression analysis operations.
/// </summary>
public class AnalysisContext
{
    /// <summary>Gets or sets the expression tree depth.</summary>
    public int Depth { get; set; }
    /// <summary>Gets or sets the current expression being analyzed.</summary>
    public Expression? CurrentExpression { get; set; }
    /// <summary>Gets or sets the parent expression.</summary>
    public Expression? ParentExpression { get; set; }
    /// <summary>Gets the operator chain in the expression tree.</summary>
    public List<ExpressionType> OperatorChain { get; } = [];
    /// <summary>Gets type usage information.</summary>
    public Dictionary<Type, object> TypeUsage { get; } = [];
    /// <summary>Gets parallelization opportunities.</summary>
    public Dictionary<Expression, object> ParallelizationOpportunities { get; } = [];
    /// <summary>Gets data flow bottlenecks.</summary>
    public List<string> DataFlowBottlenecks { get; } = [];
    /// <summary>Gets variable dependencies.</summary>
    public Dictionary<string, object> VariableDependencies { get; } = [];
    /// <summary>Gets method call information.</summary>
    public Dictionary<string, object> MethodCalls { get; } = [];
    /// <summary>Gets the estimated computational complexity.</summary>
    public double ComputationalComplexity { get; set; }
    /// <summary>Gets or sets whether the expression tree is suitable for GPU execution.</summary>
    public bool IsGpuSuitable { get; set; } = true;
    /// <summary>Gets collected optimization hints.</summary>
    public List<string> OptimizationHints { get; } = [];
    /// <summary>
    /// Creates a child analysis context for nested analysis.
    /// </summary>
    /// <param name="expression">The expression for the child context.</param>
    /// <returns>A new child analysis context.</returns>
    public AnalysisContext CreateChildContext(Expression expression)
    {
        return new AnalysisContext
        {
            Depth = Depth + 1,
            CurrentExpression = expression,
            ParentExpression = CurrentExpression,
            ComputationalComplexity = ComputationalComplexity,
            IsGpuSuitable = IsGpuSuitable
        };
    }
}
