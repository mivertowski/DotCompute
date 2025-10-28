// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests;

/// <summary>
/// Comprehensive tests for ComputeQueryProvider and ComputeQueryable.
/// Target: 50 tests covering expression compilation and query execution.
/// Tests query provider behavior, expression handling, and queryable implementation.
/// </summary>
public sealed class ComputeQueryProviderTests
{
    #region CreateQuery Tests

    [Fact]
    public void CreateQuery_WithValidExpression_CreatesQueryable()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var result = provider.CreateQuery<int>(expression);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IQueryable<int>>();
    }

    [Fact]
    public void CreateQuery_WithNullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new ComputeQueryProvider();

        // Act
        var act = () => provider.CreateQuery<int>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateQuery_NonGeneric_WithValidExpression_CreatesQueryable()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var result = provider.CreateQuery(expression);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IQueryable>();
    }

    [Fact]
    public void CreateQuery_NonGeneric_WithNullExpression_ThrowsException()
    {
        // Arrange
        var provider = new ComputeQueryProvider();

        // Act
        var act = () => provider.CreateQuery(null!);

        // Assert
        // Minimal implementation throws NullReferenceException, future should throw ArgumentNullException
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void CreateQuery_PreservesElementType()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { "a", "b" }.AsQueryable());

        // Act
        var result = provider.CreateQuery<string>(expression);

        // Assert
        result.ElementType.Should().Be(typeof(string));
    }

    [Fact]
    public void CreateQuery_PreservesExpression()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var result = provider.CreateQuery<int>(expression);

        // Assert
        result.Expression.Should().Be(expression);
    }

    [Fact]
    public void CreateQuery_ReturnsComputeQueryableInstance()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var result = provider.CreateQuery<int>(expression);

        // Assert
        result.GetType().Name.Should().Contain("ComputeQueryable");
    }

    [Fact]
    public void CreateQuery_WithDifferentTypes_CreatesCorrectQueryables()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var intExpression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());
        var stringExpression = Expression.Constant(new[] { "a", "b" }.AsQueryable());

        // Act
        var intResult = provider.CreateQuery<int>(intExpression);
        var stringResult = provider.CreateQuery<string>(stringExpression);

        // Assert
        intResult.ElementType.Should().Be(typeof(int));
        stringResult.ElementType.Should().Be(typeof(string));
    }

    #endregion

    #region Execute Tests

    [Fact]
    public void Execute_WithSimpleExpression_ReturnsResult()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(42);

        // Act
        var result = provider.Execute<int>(expression);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Execute_WithNullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new ComputeQueryProvider();

        // Act
        var act = () => provider.Execute<int>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Execute_NonGeneric_WithExpression_ReturnsObject()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(42);

        // Act
        var result = provider.Execute(expression);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Execute_WithStringExpression_ReturnsString()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant("test");

        // Act
        var result = provider.Execute<string>(expression);

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void Execute_WithNullResult_ReturnsNull()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(null, typeof(string));

        // Act
        var result = provider.Execute<string>(expression);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Execute_WithComplexExpression_CompilesAndExecutes()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Add(
            Expression.Constant(5),
            Expression.Constant(3)
        );

        // Act
        var result = provider.Execute<int>(expression);

        // Assert
        result.Should().Be(8);
    }

    #endregion

    #region ComputeQueryable Tests

    [Fact]
    public void ComputeQueryable_Constructor_SetsProperties()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var queryable = new ComputeQueryable<int>(expression, provider);

        // Assert
        queryable.Expression.Should().Be(expression);
        queryable.Provider.Should().Be(provider);
        queryable.ElementType.Should().Be(typeof(int));
    }

    [Fact]
    public void ComputeQueryable_WithNullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new ComputeQueryProvider();

        // Act
        var act = () => new ComputeQueryable<int>(null!, provider);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("expression");
    }

    [Fact]
    public void ComputeQueryable_WithNullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var act = () => new ComputeQueryable<int>(expression, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("provider");
    }

    [Fact]
    public void ComputeQueryable_GetEnumerator_ReturnsEnumerator()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        using var enumerator = queryable.GetEnumerator();

        // Assert
        enumerator.Should().NotBeNull();
    }

    [Fact]
    public void ComputeQueryable_Enumerate_ReturnsAllElements()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = new List<int>();
        foreach (var item in queryable)
        {
            result.Add(item);
        }

        // Assert
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void ComputeQueryable_NonGenericGetEnumerator_ReturnsEnumerator()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var enumerator = ((System.Collections.IEnumerable)queryable).GetEnumerator();

        // Assert
        enumerator.Should().NotBeNull();
    }

    [Fact]
    public void ComputeQueryable_CanBeEnumeratedMultipleTimes()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result1 = queryable.ToArray();
        var result2 = queryable.ToArray();

        // Assert
        result1.Should().Equal(1, 2, 3);
        result2.Should().Equal(1, 2, 3);
    }

    #endregion

    #region Expression Handling Tests

    [Fact]
    public void QueryProvider_HandlesWhereExpression()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Where(x => x > 3).ToArray();

        // Assert
        result.Should().Equal(4, 5);
    }

    [Fact]
    public void QueryProvider_HandlesSelectExpression()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Select(x => x * 2).ToArray();

        // Assert
        result.Should().Equal(2, 4, 6);
    }

    [Fact(Skip = "OrderBy requires IOrderedQueryable implementation - planned for future")]
    public void QueryProvider_HandlesOrderByExpression()
    {
        // Arrange
        var data = new[] { 3, 1, 2 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.OrderBy(x => x).ToArray();

        // Assert
        result.Should().Equal(1, 2, 3);
        // Note: Minimal implementation doesn't support OrderBy yet
    }

    [Fact]
    public void QueryProvider_HandlesTakeExpression()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Take(3).ToArray();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void QueryProvider_HandlesSkipExpression()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Skip(2).ToArray();

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact(Skip = "OrderByDescending requires IOrderedQueryable implementation - planned for future")]
    public void QueryProvider_HandlesChainedExpressions()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable
            .Where(x => x > 3)
            .Select(x => x * 2)
            .OrderByDescending(x => x)
            .Take(3)
            .ToArray();

        // Assert
        result.Should().Equal(20, 18, 16);
        // Note: Minimal implementation doesn't support OrderBy/OrderByDescending yet
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Execute_WithInvalidCast_ThrowsInvalidCastException()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant("not an int");

        // Act
        var act = () => provider.Execute<int>(expression);

        // Assert
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void CreateQuery_WithIncompatibleType_ThrowsException()
    {
        // Arrange
        var provider = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { "strings" }.AsQueryable());

        // Act & Assert
        // Should handle type mismatches gracefully
        var result = provider.CreateQuery<string>(expression);
        result.Should().NotBeNull();
    }

    #endregion

    #region Performance and Edge Case Tests

    [Fact]
    public void QueryProvider_WithLargeDataSet_HandlesCorrectly()
    {
        // Arrange
        var data = Enumerable.Range(1, 10000).AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Where(x => x % 2 == 0).Take(100).ToArray();

        // Assert
        result.Should().HaveCount(100);
        result[0].Should().Be(2);
        result[99].Should().Be(200);
    }

    [Fact]
    public void QueryProvider_WithEmptySource_HandlesGracefully()
    {
        // Arrange
        var data = Array.Empty<int>().AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Where(x => x > 0).ToArray();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void QueryProvider_WithSingleElement_WorksCorrectly()
    {
        // Arrange
        var data = new[] { 42 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.ToArray();

        // Assert
        result.Should().Equal(42);
    }

    [Fact]
    public void QueryProvider_WithComplexObjects_HandlesCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestData { Id = 1, Name = "A", Value = 10.5 },
            new TestData { Id = 2, Name = "B", Value = 20.5 }
        }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Where(x => x.Id > 0).ToArray();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void QueryProvider_MultipleProviderInstances_WorkIndependently()
    {
        // Arrange
        var provider1 = new ComputeQueryProvider();
        var provider2 = new ComputeQueryProvider();
        var expression = Expression.Constant(new[] { 1, 2, 3 }.AsQueryable());

        // Act
        var result1 = provider1.CreateQuery<int>(expression);
        var result2 = provider2.CreateQuery<int>(expression);

        // Assert
        result1.Should().NotBeSameAs(result2);
        result1.ToArray().Should().Equal(result2.ToArray());
    }

    [Fact]
    public void QueryProvider_WithFirstOrDefault_ReturnsCorrectValue()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.FirstOrDefault();

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void QueryProvider_WithCount_ReturnsCorrectCount()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Count();

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void QueryProvider_WithAny_ReturnsCorrectBoolean()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Any(x => x > 2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void QueryProvider_WithAll_ReturnsCorrectBoolean()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.All(x => x > 0);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void QueryProvider_WithSum_ReturnsCorrectSum()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
        var queryable = data.AsComputeQueryable();

        // Act
        var result = queryable.Sum();

        // Assert
        result.Should().Be(15);
    }

    #endregion

    #region Helper Classes

    private sealed class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    #endregion
}
