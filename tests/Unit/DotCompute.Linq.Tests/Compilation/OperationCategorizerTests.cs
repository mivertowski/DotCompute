// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using Xunit;

namespace DotCompute.Linq.Tests.Compilation;

/// <summary>
/// Comprehensive unit tests for <see cref="OperationCategorizer"/>.
/// </summary>
public class OperationCategorizerTests
{
    private readonly OperationCategorizer _categorizer = new();

    #region Operation Categorization Tests (8 tests)

    [Fact]
    public void Categorize_MapOperation_ReturnsCorrectCategory()
    {
        // Arrange
        var expression = CreateMethodCallExpression("Select");

        // Act
        var result = _categorizer.Categorize(expression);

        // Assert
        Assert.Equal(OperationType.Map, result);
    }

    [Fact]
    public void Categorize_FilterOperation_ReturnsCorrectCategory()
    {
        // Arrange
        var expression = CreateMethodCallExpression("Where");

        // Act
        var result = _categorizer.Categorize(expression);

        // Assert
        Assert.Equal(OperationType.Filter, result);
    }

    [Fact]
    public void Categorize_ReduceOperation_ReturnsCorrectCategory()
    {
        // Arrange - Test multiple reduce operations
        var aggregateExpr = CreateMethodCallExpression("Aggregate");
        var sumExpr = CreateMethodCallExpression("Sum");
        var avgExpr = CreateMethodCallExpression("Average");

        // Act
        var aggregateResult = _categorizer.Categorize(aggregateExpr);
        var sumResult = _categorizer.Categorize(sumExpr);
        var avgResult = _categorizer.Categorize(avgExpr);

        // Assert
        Assert.Equal(OperationType.Reduce, aggregateResult);
        Assert.Equal(OperationType.Reduce, sumResult);
        Assert.Equal(OperationType.Reduce, avgResult);
    }

    [Fact(Skip = "Scan is not a standard Queryable method - test creates invalid expression")]
    public void Categorize_ScanOperation_ReturnsCorrectCategory()
    {
        // Arrange
        var expression = CreateMethodCallExpression("Scan");

        // Act
        var result = _categorizer.Categorize(expression);

        // Assert
        Assert.Equal(OperationType.Scan, result);
    }

    [Fact]
    public void Categorize_JoinOperation_ReturnsCorrectCategory()
    {
        // Arrange
        var joinExpr = CreateMethodCallExpression("Join");
        var groupJoinExpr = CreateMethodCallExpression("GroupJoin");

        // Act
        var joinResult = _categorizer.Categorize(joinExpr);
        var groupJoinResult = _categorizer.Categorize(groupJoinExpr);

        // Assert
        Assert.Equal(OperationType.Join, joinResult);
        Assert.Equal(OperationType.Join, groupJoinResult);
    }

    [Fact]
    public void Categorize_GroupByOperation_ReturnsCorrectCategory()
    {
        // Arrange
        var expression = CreateMethodCallExpression("GroupBy");

        // Act
        var result = _categorizer.Categorize(expression);

        // Assert
        Assert.Equal(OperationType.GroupBy, result);
    }

    [Fact]
    public void Categorize_OrderByOperation_ReturnsCorrectCategory()
    {
        // Arrange
        var orderByExpr = CreateMethodCallExpression("OrderBy");
        var orderByDescExpr = CreateMethodCallExpression("OrderByDescending");

        // Act
        var orderByResult = _categorizer.Categorize(orderByExpr);
        var orderByDescResult = _categorizer.Categorize(orderByDescExpr);

        // Assert
        Assert.Equal(OperationType.OrderBy, orderByResult);
        Assert.Equal(OperationType.OrderBy, orderByDescResult);
    }

    [Fact]
    public void Categorize_AggregateOperation_ReturnsCorrectCategory()
    {
        // Arrange - Test various aggregate operations
        var minExpr = CreateMethodCallExpression("Min");
        var maxExpr = CreateMethodCallExpression("Max");
        var countExpr = CreateMethodCallExpression("Count");

        // Act
        var minResult = _categorizer.Categorize(minExpr);
        var maxResult = _categorizer.Categorize(maxExpr);
        var countResult = _categorizer.Categorize(countExpr);

        // Assert
        Assert.Equal(OperationType.Reduce, minResult);
        Assert.Equal(OperationType.Reduce, maxResult);
        Assert.Equal(OperationType.Reduce, countResult);
    }

    #endregion

    #region Parallelization Strategy Selection Tests (8 tests)

    [Theory]
    [InlineData(100, ParallelizationStrategy.Sequential)]
    [InlineData(1_000, ParallelizationStrategy.Sequential)]
    [InlineData(9_999, ParallelizationStrategy.Sequential)]
    public void GetStrategy_SmallDataSize_SelectsSequentialStrategy(int dataSize, ParallelizationStrategy expected)
    {
        // Arrange
        var graph = CreateOperationGraphWithDataSize(dataSize, ComputeIntensity.Low);

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(10_000, ComputeIntensity.Low, ParallelizationStrategy.DataParallel)]
    [InlineData(50_000, ComputeIntensity.Low, ParallelizationStrategy.DataParallel)]
    [InlineData(100_000, ComputeIntensity.Medium, ParallelizationStrategy.DataParallel)]
    public void GetStrategy_MediumDataSizeWithLowToMediumIntensity_SelectsDataParallelStrategy(
        int dataSize, ComputeIntensity intensity, ParallelizationStrategy expected)
    {
        // Arrange
        var graph = CreateOperationGraphWithDataSize(dataSize, intensity);

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1_000_001, ComputeIntensity.Medium, ParallelizationStrategy.TaskParallel)]
    [InlineData(5_000_000, ComputeIntensity.Medium, ParallelizationStrategy.TaskParallel)]
    public void GetStrategy_LargeDataWithMediumIntensity_SelectsTaskParallelStrategy(
        int dataSize, ComputeIntensity intensity, ParallelizationStrategy expected)
    {
        // Arrange
        var graph = CreateOperationGraphWithDataSize(dataSize, intensity);

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1_000_001, ComputeIntensity.High, ParallelizationStrategy.GpuParallel)]
    [InlineData(10_000_000, ComputeIntensity.VeryHigh, ParallelizationStrategy.GpuParallel)]
    public void GetStrategy_LargeDataWithHighIntensity_SelectsGpuParallelStrategy(
        int dataSize, ComputeIntensity intensity, ParallelizationStrategy expected)
    {
        // Arrange
        var graph = CreateOperationGraphWithDataSize(dataSize, intensity);

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetStrategy_MixedWorkload_SelectsHybridStrategy()
    {
        // Arrange - Create graph with multiple different operation types (3+)
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, EstimatedCost = 1.0 },
                new() { Id = "op2", Type = OperationType.Filter, EstimatedCost = 1.0 },
                new() { Id = "op3", Type = OperationType.Reduce, EstimatedCost = 1.0 },
                new() { Id = "op4", Type = OperationType.Join, EstimatedCost = 1.0 }
            },
            Metadata = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { ["EstimatedDataSize"] = 100_000 })
        };

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(ParallelizationStrategy.Hybrid, result);
    }

    [Fact]
    public void GetStrategy_WithSideEffects_SelectsSequentialStrategy()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new()
                {
                    Id = "op1",
                    Type = OperationType.Map,
                    EstimatedCost = 5.0,
                    Metadata = new Dictionary<string, object> { ["HasSideEffects"] = true }
                }
            },
            Metadata = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { ["EstimatedDataSize"] = 1_000_000 })
        };

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(ParallelizationStrategy.Sequential, result);
    }

    [Fact]
    public void GetStrategy_OrderByOperation_SelectsSequentialStrategy()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.OrderBy, EstimatedCost = 5.0 }
            },
            Metadata = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { ["EstimatedDataSize"] = 1_000_000 })
        };

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(ParallelizationStrategy.Sequential, result);
    }

    [Fact]
    public void GetStrategy_CustomMetadataOverride_RespectsMetadata()
    {
        // Arrange - Even with large data, low estimated cost should favor DataParallel
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, EstimatedCost = 0.5 }
            },
            Metadata = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { ["EstimatedDataSize"] = 100_000 })
        };

        // Act
        var result = _categorizer.GetStrategy(graph);

        // Assert
        Assert.Equal(ParallelizationStrategy.DataParallel, result);
    }

    #endregion

    #region Fusion Opportunity Detection Tests (7 tests)

    [Fact]
    public void DetectFusionOpportunities_SelectWhereFusion_DetectsOpportunity()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Filter, Dependencies = new Collection<string> { "op1" } }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.NotEmpty(fusions);
        var selectWhereFusion = fusions.FirstOrDefault(f => f.FusionType == "SelectWhere");
        Assert.NotNull(selectWhereFusion);
        Assert.Equal(2, selectWhereFusion.OperationIds.Count);
        Assert.Contains("op1", selectWhereFusion.OperationIds);
        Assert.Contains("op2", selectWhereFusion.OperationIds);
        Assert.Equal(1.5, selectWhereFusion.EstimatedSpeedup);
    }

    [Fact]
    public void DetectFusionOpportunities_WhereSelectFusion_DetectsOpportunity()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Filter, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Map, Dependencies = new Collection<string> { "op1" } }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.NotEmpty(fusions);
        var whereSelectFusion = fusions.FirstOrDefault(f => f.FusionType == "WhereSelect");
        Assert.NotNull(whereSelectFusion);
        Assert.Equal(2, whereSelectFusion.OperationIds.Count);
        Assert.Equal(1.8, whereSelectFusion.EstimatedSpeedup);
    }

    [Fact]
    public void DetectFusionOpportunities_MapReduceFusion_DetectsOpportunity()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Reduce, Dependencies = new Collection<string> { "op1" } }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.NotEmpty(fusions);
        var mapReduceFusion = fusions.FirstOrDefault(f => f.FusionType == "MapReduce");
        Assert.NotNull(mapReduceFusion);
        Assert.Equal(2, mapReduceFusion.OperationIds.Count);
        Assert.Equal(2.0, mapReduceFusion.EstimatedSpeedup);
    }

    [Fact]
    public void DetectFusionOpportunities_ConsecutiveSelects_DoesNotDetectFusion()
    {
        // Arrange - Two consecutive Map operations (not a defined fusion pattern)
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Map, Dependencies = new Collection<string> { "op1" } }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.Empty(fusions);
    }

    [Fact]
    public void DetectFusionOpportunities_ConsecutiveWheres_DetectsCombinedFusion()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Filter, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Filter, Dependencies = new Collection<string> { "op1" } },
                new() { Id = "op3", Type = OperationType.Map, Dependencies = new Collection<string> { "op2" } }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        var combinedWhereFusion = fusions.FirstOrDefault(f => f.FusionType == "CombinedWhere");
        Assert.NotNull(combinedWhereFusion);
        Assert.Equal(2, combinedWhereFusion.OperationIds.Count);
    }

    [Fact]
    public void DetectFusionOpportunities_NoOpportunities_ReturnsEmpty()
    {
        // Arrange - Single operation, no fusion possible
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Reduce, Dependencies = new Collection<string>() }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.Empty(fusions);
    }

    [Fact]
    public void DetectFusionOpportunities_ComplexGraph_DetectsMultipleFusions()
    {
        // Arrange - Graph with multiple fusion opportunities
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Filter, Dependencies = new Collection<string> { "op1" } },
                new() { Id = "op3", Type = OperationType.Map, Dependencies = new Collection<string> { "op2" } },
                new() { Id = "op4", Type = OperationType.Reduce, Dependencies = new Collection<string> { "op3" } }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.NotEmpty(fusions);
        Assert.True(fusions.Count >= 2); // Should detect SelectWhere and MapReduce
    }

    #endregion

    #region Dependency Graph Building Tests (5 tests)

    [Fact]
    public void BuildDependencies_SingleOperation_BuildsCorrectGraph()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() }
            }
        };

        // Act
        var result = _categorizer.BuildDependencies(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.BaseGraph);
        Assert.Empty(result.DataDependencies["op1"]);
    }

    [Fact]
    public void BuildDependencies_ChainedOperations_BuildsCorrectDependencies()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Filter, Dependencies = new Collection<string> { "op1" } },
                new() { Id = "op3", Type = OperationType.Reduce, Dependencies = new Collection<string> { "op2" } }
            }
        };

        // Act
        var result = _categorizer.BuildDependencies(graph);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.DataDependencies["op2"].Count);
        Assert.Equal(2, result.DataDependencies["op3"].Count);

        var op2Deps = result.BaseGraph.GetDependencies("op2");
        Assert.Contains("op1", op2Deps);

        var op3Deps = result.BaseGraph.GetDependencies("op3");
        Assert.Contains("op2", op3Deps);
    }

    [Fact]
    public void BuildDependencies_ParallelOperations_IdentifiesParallelizableGroups()
    {
        // Arrange - Two independent operations
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Map, Dependencies = new Collection<string>() }
            }
        };

        // Act
        var result = _categorizer.BuildDependencies(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.ParallelizableGroups);
        var parallelGroup = result.ParallelizableGroups[0];
        Assert.NotNull(parallelGroup);
        Assert.Equal(2, parallelGroup.Count);
    }

    [Fact]
    public void BuildDependencies_ComplexGraphWithBranches_BuildsCorrectStructure()
    {
        // Arrange - Diamond pattern: op1 -> op2, op3 -> op4
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Filter, Dependencies = new Collection<string> { "op1" } },
                new() { Id = "op3", Type = OperationType.Filter, Dependencies = new Collection<string> { "op1" } },
                new() { Id = "op4", Type = OperationType.Reduce, Dependencies = new Collection<string> { "op2", "op3" } }
            }
        };

        // Act
        var result = _categorizer.BuildDependencies(graph);

        // Assert
        Assert.NotNull(result);
        var op4Deps = result.BaseGraph.GetDependencies("op4");
        Assert.Equal(2, op4Deps.Count);
        Assert.Contains("op2", op4Deps);
        Assert.Contains("op3", op4Deps);
    }

    [Fact]
    public void BuildDependencies_DataDependencyTypes_CorrectlyIdentifiesTypes()
    {
        // Arrange - MapReduce pattern (ReadAfterWrite, blocking)
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Reduce, Dependencies = new Collection<string> { "op1" } }
            }
        };

        // Act
        var result = _categorizer.BuildDependencies(graph);

        // Assert
        Assert.NotNull(result);
        var op2DataDeps = result.DataDependencies["op2"];
        Assert.NotEmpty(op2DataDeps);
        var dataDep = op2DataDeps.First();
        Assert.Equal(DependencyType.ReadAfterWrite, dataDep.Type);
        Assert.True(dataDep.IsBlocking);
    }

    #endregion

    #region Edge Cases Tests (2 tests)

    [Fact]
    public void Categorize_NullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        Expression? nullExpression = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _categorizer.Categorize(nullExpression!));
    }

    [Fact]
    public void GetStrategy_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        OperationGraph? nullGraph = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _categorizer.GetStrategy(nullGraph!));
    }

    [Fact]
    public void BuildDependencies_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        OperationGraph? nullGraph = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _categorizer.BuildDependencies(nullGraph!));
    }

    [Fact]
    public void DetectFusionOpportunities_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        OperationGraph? nullGraph = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _categorizer.DetectFusionOpportunities(nullGraph!));
    }

    [Fact]
    public void AnalyzeDataFlow_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        OperationGraph? nullGraph = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _categorizer.AnalyzeDataFlow(nullGraph!));
    }

    [Fact]
    public void GetStrategy_EmptyGraph_ReturnsDefaultStrategy()
    {
        // Arrange
        var emptyGraph = new OperationGraph
        {
            Operations = new Collection<Operation>(),
            Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
        };

        // Act
        var result = _categorizer.GetStrategy(emptyGraph);

        // Assert - Should return default strategy (DataParallel)
        Assert.Equal(ParallelizationStrategy.DataParallel, result);
    }

    #endregion

    #region Additional Comprehensive Tests

    [Fact]
    public void AnalyzeDataFlow_CompleteAnalysis_ReturnsCorrectResults()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, EstimatedCost = 2.5 }
            },
            Metadata = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { ["EstimatedDataSize"] = 100_000 })
        };

        // Act
        var result = _categorizer.AnalyzeDataFlow(graph);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.HasSideEffects);
        Assert.True(result.IsDataParallelizable);
        Assert.Equal(100_000, result.EstimatedDataSize);
        Assert.Equal(ComputeIntensity.Medium, result.Intensity);
    }

    [Fact]
    public void Categorize_UnaryExpression_RecursivelyCategorizes()
    {
        // Arrange - Create a unary expression wrapping a method call
        var methodCall = CreateMethodCallExpression("Select");
        var unaryExpr = Expression.Convert(methodCall, typeof(object));

        // Act
        var result = _categorizer.Categorize(unaryExpr);

        // Assert - Should traverse through unary to underlying Select operation
        Assert.Equal(OperationType.Map, result);
    }

    [Fact]
    public void Categorize_BinaryExpression_ReturnsCorrectCategory()
    {
        // Arrange - Arithmetic operations should be Reduce
        var addExpr = Expression.Add(Expression.Constant(1), Expression.Constant(2));
        var equalExpr = Expression.Equal(Expression.Constant(1), Expression.Constant(2));

        // Act
        var addResult = _categorizer.Categorize(addExpr);
        var equalResult = _categorizer.Categorize(equalExpr);

        // Assert
        Assert.Equal(OperationType.Reduce, addResult);
        Assert.Equal(OperationType.Filter, equalResult);
    }

    [Fact]
    public void DetectFusionOpportunities_NonConsecutiveOperations_DoesNotFuse()
    {
        // Arrange - Map and Filter without dependency
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new() { Id = "op1", Type = OperationType.Map, Dependencies = new Collection<string>() },
                new() { Id = "op2", Type = OperationType.Filter, Dependencies = new Collection<string>() }
            }
        };

        // Act
        var fusions = _categorizer.DetectFusionOpportunities(graph);

        // Assert
        Assert.Empty(fusions);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a method call expression with the specified method name.
    /// </summary>
    private static MethodCallExpression CreateMethodCallExpression(string methodName)
    {
        var queryable = Enumerable.Range(1, 10).AsQueryable();
        var param = Expression.Parameter(typeof(int), "x");

        return methodName switch
        {
            "Select" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Select),
                new[] { typeof(int), typeof(int) },
                queryable.Expression,
                Expression.Lambda(param, param)),

            "Where" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Where),
                new[] { typeof(int) },
                queryable.Expression,
                Expression.Lambda(Expression.Constant(true), param)),

            "OrderBy" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.OrderBy),
                new[] { typeof(int), typeof(int) },
                queryable.Expression,
                Expression.Lambda(param, param)),

            "OrderByDescending" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.OrderByDescending),
                new[] { typeof(int), typeof(int) },
                queryable.Expression,
                Expression.Lambda(param, param)),

            "GroupBy" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.GroupBy),
                new[] { typeof(int), typeof(int) },
                queryable.Expression,
                Expression.Lambda(param, param)),

            _ => CreateAggregateMethodCall(methodName, queryable, param)
        };
    }

    private static MethodCallExpression CreateAggregateMethodCall(
        string methodName, IQueryable<int> queryable, ParameterExpression param)
    {
        return methodName switch
        {
            "Sum" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Sum),
                null,
                queryable.Expression),

            "Average" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Average),
                null,
                queryable.Expression),

            "Min" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Min),
                new[] { typeof(int) },
                queryable.Expression),

            "Max" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Max),
                new[] { typeof(int) },
                queryable.Expression),

            "Count" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Count),
                new[] { typeof(int) },
                queryable.Expression),

            "Aggregate" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Aggregate),
                new[] { typeof(int), typeof(int) },
                queryable.Expression,
                Expression.Constant(0),
                Expression.Lambda(
                    Expression.Add(param, Expression.Parameter(typeof(int), "y")),
                    param,
                    Expression.Parameter(typeof(int), "y"))),

            "Join" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Join),
                new[] { typeof(int), typeof(int), typeof(int), typeof(int) },
                queryable.Expression,
                queryable.Expression,
                Expression.Lambda(param, param),
                Expression.Lambda(Expression.Parameter(typeof(int), "y"), Expression.Parameter(typeof(int), "y")),
                Expression.Lambda(
                    Expression.Parameter(typeof(int), "result"),
                    Expression.Parameter(typeof(int), "outer"),
                    Expression.Parameter(typeof(int), "inner"))),

            "GroupJoin" => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.GroupJoin),
                new[] { typeof(int), typeof(int), typeof(int), typeof(IEnumerable<int>) },
                queryable.Expression,
                queryable.Expression,
                Expression.Lambda(param, param),
                Expression.Lambda(param, param),
                Expression.Lambda(
                    Expression.Parameter(typeof(IEnumerable<int>)),
                    param,
                    Expression.Parameter(typeof(IEnumerable<int>), "inner"))),

            "Scan" or "Accumulate" => Expression.Call(
                typeof(Queryable),
                "Scan", // Custom extension method
                new[] { typeof(int), typeof(int) },
                queryable.Expression,
                Expression.Constant(0),
                Expression.Lambda(
                    Expression.Add(param, Expression.Parameter(typeof(int), "y")),
                    param,
                    Expression.Parameter(typeof(int), "y"))),

            _ => throw new ArgumentException($"Unknown method name: {methodName}", nameof(methodName))
        };
    }

    /// <summary>
    /// Creates an operation graph with specified data size and compute intensity.
    /// </summary>
    private static OperationGraph CreateOperationGraphWithDataSize(int dataSize, ComputeIntensity intensity)
    {
        var costValue = intensity switch
        {
            ComputeIntensity.Low => 0.5,
            ComputeIntensity.Medium => 2.0,
            ComputeIntensity.High => 7.0,
            ComputeIntensity.VeryHigh => 15.0,
            _ => 1.0
        };

        return new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new()
                {
                    Id = "op1",
                    Type = OperationType.Map,
                    EstimatedCost = costValue,
                    Dependencies = new Collection<string>()
                }
            },
            Metadata = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { ["EstimatedDataSize"] = dataSize })
        };
    }

    #endregion
}
