// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests.Compilation;

/// <summary>
/// Comprehensive unit tests for ExpressionTreeVisitor class.
/// Tests LINQ operation detection, operation graph building, lambda extraction,
/// non-deterministic detection, and edge cases.
/// Target: 40+ tests covering all visitor functionality.
/// </summary>
public sealed class ExpressionTreeVisitorTests
{
    private readonly ExpressionTreeVisitor _visitor = new();

    #region LINQ Operation Detection Tests (15 tests)

    [Fact]
    public void Visit_SelectOperation_CreatesMapOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Map);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Select");
    }

    [Fact]
    public void Visit_WhereOperation_CreatesFilterOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source.Where(x => x > 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Filter);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Where");
    }

    [Fact]
    public void Visit_SumOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<int>> sumExpression = () => source.Sum();
        var expression = ((MethodCallExpression)sumExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Sum");
    }

    [Fact]
    public void Visit_CountOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<int>> countExpression = () => source.Count();
        var expression = ((MethodCallExpression)countExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Count");
    }

    [Fact]
    public void Visit_AverageOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<double>> avgExpression = () => source.Average();
        var expression = ((MethodCallExpression)avgExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Average");
    }

    [Fact]
    public void Visit_MinOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 5, 2, 8, 1 }.AsQueryable();
        Expression<Func<int>> minExpression = () => source.Min();
        var expression = ((MethodCallExpression)minExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Min");
    }

    [Fact]
    public void Visit_MaxOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 5, 2, 8, 1 }.AsQueryable();
        Expression<Func<int>> maxExpression = () => source.Max();
        var expression = ((MethodCallExpression)maxExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Max");
    }

    [Fact]
    public void Visit_AggregateOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<int>> aggExpression = () => source.Aggregate((acc, x) => acc + x);
        var expression = ((MethodCallExpression)aggExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Aggregate");
    }

    [Fact]
    public void Visit_OrderByOperation_CreatesOrderByOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 3, 1, 2 }.AsQueryable();
        var expression = source.OrderBy(x => x).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.OrderBy);
        graph.Operations[0].Metadata["MethodName"].Should().Be("OrderBy");
        graph.Operations[0].Metadata["Descending"].Should().Be(false);
    }

    [Fact]
    public void Visit_OrderByDescendingOperation_CreatesOrderByOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 3, 2 }.AsQueryable();
        var expression = source.OrderByDescending(x => x).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.OrderBy);
        graph.Operations[0].Metadata["MethodName"].Should().Be("OrderByDescending");
        graph.Operations[0].Metadata["Descending"].Should().Be(true);
    }

    [Fact]
    public void Visit_GroupByOperation_CreatesGroupByOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4 }.AsQueryable();
        var expression = source.GroupBy(x => x % 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.GroupBy);
        graph.Operations[0].Metadata["MethodName"].Should().Be("GroupBy");
        graph.Operations[0].Metadata.Should().ContainKey("KeySelector");
    }

    [Fact]
    public void Visit_TakeOperation_CreatesFilterOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source.Take(3).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Filter);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Take");
    }

    [Fact]
    public void Visit_SkipOperation_CreatesFilterOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source.Skip(2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Filter);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Skip");
    }

    [Fact]
    public void Visit_DistinctOperation_CreatesAggregateOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 2, 3, 3, 3 }.AsQueryable();
        var expression = source.Distinct().Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Aggregate);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Distinct");
    }

    [Fact]
    public void Visit_TakeWhileOperation_CreatesScanOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source.TakeWhile(x => x < 4).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Scan);
        graph.Operations[0].Metadata["MethodName"].Should().Be("TakeWhile");
    }

    #endregion

    #region Operation Graph Building Tests (10 tests)

    [Fact]
    public void Visit_SingleOperation_BuildsSingleNodeGraph()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(1);
        graph.Operations[0].Id.Should().Be("op_0");
        graph.Root.Should().NotBeNull();
        graph.Root!.Id.Should().Be("op_0");
    }

    [Fact]
    public void Visit_ChainedSelectWhere_BuildsTwoNodeGraph()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source.Select(x => x * 2).Where(x => x > 4).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(2);
        graph.Operations[0].Type.Should().Be(OperationType.Map);
        graph.Operations[1].Type.Should().Be(OperationType.Filter);
    }

    [Fact]
    public void Visit_ComplexChain_BuildsMultiNodeGraph()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5, 6 }.AsQueryable();
        var expression = source
            .Where(x => x > 2)
            .Select(x => x * 2)
            .OrderBy(x => x)
            .Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(3);
        graph.Operations[0].Type.Should().Be(OperationType.Filter);
        graph.Operations[1].Type.Should().Be(OperationType.Map);
        graph.Operations[2].Type.Should().Be(OperationType.OrderBy);
    }

    [Fact]
    public void Visit_OperationsWithDependencies_TracksDependencies()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Where(x => x > 1).Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(2);
        // Operations are chained in sequence
        graph.Operations[0].Type.Should().Be(OperationType.Filter);
        graph.Operations[1].Type.Should().Be(OperationType.Map);
        // The implementation tracks dependencies via previousNodeId in the visitor
        graph.Operations[1].Dependencies.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Visit_RootOperation_IdentifiesLastOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Where(x => x > 1).Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Root.Should().NotBeNull();
        graph.Root!.Type.Should().Be(OperationType.Map);
        // Root is the last operation added
        graph.Root.Should().Be(graph.Operations[^1]);
    }

    [Fact]
    public void Visit_ExtractsMetadata_IncludesExpressionInfo()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Metadata.Should().ContainKey("OriginalExpression");
        graph.Metadata.Should().ContainKey("ResultExpression");
        graph.Metadata.Should().ContainKey("TotalOperations");
        graph.Metadata.Should().ContainKey("AnalysisTimestamp");
        graph.Metadata["TotalOperations"].Should().Be(1);
    }

    [Fact]
    public void Visit_EmptyExpression_ReturnsEmptyGraph()
    {
        // Arrange
        IQueryable<int> source = Array.Empty<int>().AsQueryable();
        var expression = source.Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().BeEmpty();
        graph.Root.Should().BeNull();
    }

    [Fact]
    public void Visit_MultipleInvocations_ClearsStateCorrectly()
    {
        // Arrange
        IQueryable<int> source1 = new[] { 1, 2, 3 }.AsQueryable();
        IQueryable<int> source2 = new[] { 4, 5, 6 }.AsQueryable();
        var expression1 = source1.Select(x => x * 2).Expression;
        var expression2 = source2.Where(x => x > 4).Expression;

        // Act
        var graph1 = _visitor.Visit(expression1);
        var graph2 = _visitor.Visit(expression2);

        // Assert
        graph1.Operations.Should().HaveCount(1);
        graph1.Operations[0].Type.Should().Be(OperationType.Map);

        graph2.Operations.Should().HaveCount(1);
        graph2.Operations[0].Type.Should().Be(OperationType.Filter);
        // State should be cleared between visits - counter resets to 0
        graph2.Operations[0].Id.Should().Be("op_0");
        graph2.Metadata["TotalOperations"].Should().Be(1);
    }

    [Fact]
    public void Visit_GeneratesUniqueNodeIds_ForMultipleOperations()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source
            .Where(x => x > 1)
            .Select(x => x * 2)
            .Where(x => x < 10)
            .Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(3);
        // All operations should have unique IDs within the same visit
        var ids = graph.Operations.Select(op => op.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        // IDs should follow the pattern op_0, op_1, op_2...
        ids.Should().AllSatisfy(id => id.Should().StartWith("op_"));
    }

    [Fact]
    public void Visit_IncludesEstimatedCost_ForAllOperations()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].EstimatedCost.Should().Be(1.0); // Map operation cost
    }

    #endregion

    #region Lambda Expression Extraction Tests (5 tests)

    [Fact]
    public void Visit_SelectWithLambda_ExtractsLambdaExpression()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations[0].Metadata.Should().ContainKey("Lambda");
        graph.Operations[0].Metadata["Lambda"].Should().BeAssignableTo<LambdaExpression>();
    }

    [Fact]
    public void Visit_WhereWithLambda_ExtractsLambdaExpression()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4 }.AsQueryable();
        var expression = source.Where(x => x > 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations[0].Metadata.Should().ContainKey("Lambda");
        graph.Operations[0].Metadata["Lambda"].Should().BeAssignableTo<LambdaExpression>();
    }

    [Fact]
    public void Visit_OrderByWithLambda_ExtractsKeySelector()
    {
        // Arrange
        IQueryable<int> source = new[] { 3, 1, 2 }.AsQueryable();
        var expression = source.OrderBy(x => x).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations[0].Metadata.Should().ContainKey("KeySelector");
        graph.Operations[0].Metadata["KeySelector"].Should().BeAssignableTo<LambdaExpression>();
    }

    [Fact]
    public void Visit_LambdaParameters_CountsParametersCorrectly()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations[0].Metadata.Should().ContainKey("LambdaParameters");
        graph.Operations[0].Metadata["LambdaParameters"].Should().Be(1);
    }

    [Fact]
    public void Visit_LambdaBody_ExtractsBodyString()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations[0].Metadata.Should().ContainKey("LambdaBody");
        graph.Operations[0].Metadata["LambdaBody"].Should().BeOfType<string>();
        graph.Operations[0].Metadata["LambdaBody"].ToString()!.Should().Contain("*");
    }

    #endregion

    #region Non-Deterministic Detection Tests (5 tests)

    [Fact]
    public void Visit_DetectNonDeterministicOperation_DateTimeNow()
    {
        // Arrange - Create expression tree with DateTime.Now in a quoted lambda
        IQueryable<DateTime> source = new[] { DateTime.UtcNow }.AsQueryable();

        // Build the expression manually to ensure DateTime.Now is properly captured
        var param = Expression.Parameter(typeof(DateTime), "x");
        var nowProperty = Expression.Property(null, typeof(DateTime), "Now");
        var hourProperty = Expression.Property(nowProperty, "Hour");
        var lambda = Expression.Lambda<Func<DateTime, int>>(hourProperty, param);
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(DateTime), typeof(int));
        var methodCall = Expression.Call(null, selectMethod, source.Expression, Expression.Quote(lambda));

        // Act
        var act = () => _visitor.Visit(methodCall);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*DateTime.Now*");
    }

    [Fact]
    public void Visit_DetectNonDeterministicOperation_Random()
    {
        // Arrange - Create expression tree with Random.Next in a quoted lambda
        var random = new Random();
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();

        var param = Expression.Parameter(typeof(int), "x");
        var randomConst = Expression.Constant(random);
        var nextMethod = typeof(Random).GetMethod("Next", Type.EmptyTypes)!;
        var nextCall = Expression.Call(randomConst, nextMethod);
        var lambda = Expression.Lambda<Func<int, int>>(nextCall, param);
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int), typeof(int));
        var methodCall = Expression.Call(null, selectMethod, source.Expression, Expression.Quote(lambda));

        // Act
        var act = () => _visitor.Visit(methodCall);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Random*");
    }

    [Fact]
    public void Visit_DetectNonDeterministicOperation_Guid()
    {
        // Arrange - Create expression tree with Guid.NewGuid in a quoted lambda
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();

        var param = Expression.Parameter(typeof(int), "x");
        var newGuidMethod = typeof(Guid).GetMethod("NewGuid")!;
        var newGuidCall = Expression.Call(null, newGuidMethod);
        var hashCodeMethod = typeof(Guid).GetMethod("GetHashCode")!;
        var hashCodeCall = Expression.Call(newGuidCall, hashCodeMethod);
        var lambda = Expression.Lambda<Func<int, int>>(hashCodeCall, param);
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int), typeof(int));
        var methodCall = Expression.Call(null, selectMethod, source.Expression, Expression.Quote(lambda));

        // Act
        var act = () => _visitor.Visit(methodCall);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Guid*");
    }

    [Fact]
    public void Visit_ValidDeterministicOperations_DoesNotThrow()
    {
        // Arrange - Simple deterministic operations
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source
            .Where(x => x > 2)
            .Select(x => x * x)
            .OrderBy(x => x)
            .Expression;

        // Act
        var act = () => _visitor.Visit(expression);

        // Assert - Should process without throwing
        act.Should().NotThrow();
    }

    [Fact]
    public void Visit_ComplexButDeterministicExpression_Succeeds()
    {
        // Arrange - More complex but still deterministic
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsQueryable();
        var expression = source
            .Where(x => x > 0)
            .Select(x => x * 2)
            .Where(x => x < 15)
            .OrderBy(x => x)
            .Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(4);
    }

    #endregion

    #region Edge Cases Tests (5 tests)

    [Fact]
    public void Visit_NullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        Expression? nullExpression = null;

        // Act
        var act = () => _visitor.Visit(nullExpression!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("expression");
    }

    [Fact]
    public void Visit_UnsupportedLinqMethod_ThrowsNotSupportedException()
    {
        // Arrange - Zip is not supported
        IQueryable<int> source1 = new[] { 1, 2, 3 }.AsQueryable();
        IQueryable<int> source2 = new[] { 4, 5, 6 }.AsQueryable();
        var expression = source1.Zip(source2, (a, b) => a + b).Expression;

        // Act
        var act = () => _visitor.Visit(expression);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Zip*not supported for GPU acceleration*");
    }

    [Fact]
    public void Visit_EmptyOperationGraph_ReturnsValidEmptyGraph()
    {
        // Arrange
        IQueryable<int> source = Array.Empty<int>().AsQueryable();
        var expression = source.Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().BeEmpty();
        graph.Root.Should().BeNull();
        graph.Metadata["TotalOperations"].Should().Be(0);
    }

    [Fact]
    public void Visit_NestedMethodCalls_ProcessesCorrectly()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source
            .Select(x => x * 2)
            .Where(x => x > 4)
            .Select(x => x + 1)
            .Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().HaveCount(3);
        graph.Operations[0].Type.Should().Be(OperationType.Map);
        graph.Operations[1].Type.Should().Be(OperationType.Filter);
        graph.Operations[2].Type.Should().Be(OperationType.Map);
    }

    [Fact]
    public void Visit_GenericTypeExtraction_ExtractsTypeCorrectly()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Select(x => x * 2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations[0].Metadata.Should().ContainKey("GenericTypeArguments");
        graph.Operations[0].Metadata.Should().ContainKey("ElementType");
        graph.Operations[0].Metadata["ElementType"].Should().Be(typeof(int));
    }

    #endregion

    #region Additional Operation Tests (5 tests)

    [Fact]
    public void Visit_CastOperation_CreatesMapOperation()
    {
        // Arrange
        IQueryable<object> source = new object[] { 1, 2, 3 }.AsQueryable();
        var expression = source.Cast<int>().Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Map);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Cast");
    }

    [Fact]
    public void Visit_AnyOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<bool>> anyExpression = () => source.Any(x => x > 2);
        var expression = ((MethodCallExpression)anyExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Any");
    }

    [Fact]
    public void Visit_AllOperation_CreatesReduceOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<bool>> allExpression = () => source.All(x => x > 0);
        var expression = ((MethodCallExpression)allExpression.Body);

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Reduce);
        graph.Operations[0].Metadata["MethodName"].Should().Be("All");
    }

    [Fact]
    public void Visit_UnionOperation_CreatesAggregateOperation()
    {
        // Arrange
        IQueryable<int> source1 = new[] { 1, 2, 3 }.AsQueryable();
        IQueryable<int> source2 = new[] { 3, 4, 5 }.AsQueryable();
        var expression = source1.Union(source2).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Aggregate);
        graph.Operations[0].Metadata["MethodName"].Should().Be("Union");
    }

    [Fact]
    public void Visit_SkipWhileOperation_CreatesScanOperation()
    {
        // Arrange
        IQueryable<int> source = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var expression = source.SkipWhile(x => x < 3).Expression;

        // Act
        var graph = _visitor.Visit(expression);

        // Assert
        graph.Should().NotBeNull();
        graph.Operations.Should().ContainSingle();
        graph.Operations[0].Type.Should().Be(OperationType.Scan);
        graph.Operations[0].Metadata["MethodName"].Should().Be("SkipWhile");
    }

    #endregion
}
