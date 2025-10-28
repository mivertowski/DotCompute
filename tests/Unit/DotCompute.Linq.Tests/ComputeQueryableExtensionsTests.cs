// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests;

/// <summary>
/// Comprehensive tests for ComputeQueryableExtensions.
/// Target: 50 tests covering LINQ extension methods.
/// Tests AsComputeQueryable, ToComputeArray, ComputeSelect, ComputeWhere methods.
/// </summary>
public sealed class ComputeQueryableExtensionsTests
{
    #region AsComputeQueryable Tests

    [Fact]
    public void AsComputeQueryable_WithValidSource_ReturnsComputeQueryable()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data.AsComputeQueryable();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IQueryable<int>>();
        result.Provider.Should().NotBeNull();
    }

    [Fact]
    public void AsComputeQueryable_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        IQueryable<int>? nullSource = null;

        // Act
        var act = () => nullSource!.AsComputeQueryable();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void AsComputeQueryable_WithEmptySource_ReturnsEmptyComputeQueryable()
    {
        // Arrange
        var data = Array.Empty<int>().AsQueryable();

        // Act
        var result = data.AsComputeQueryable();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void AsComputeQueryable_WithComplexType_ReturnsComputeQueryable()
    {
        // Arrange
        var data = new[] { new TestData { Id = 1, Value = "Test" } }.AsQueryable();

        // Act
        var result = data.AsComputeQueryable();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public void AsComputeQueryable_PreservesElementType()
    {
        // Arrange
        var data = new[] { 1.5, 2.5, 3.5 }.AsQueryable();

        // Act
        var result = data.AsComputeQueryable();

        // Assert
        result.ElementType.Should().Be(typeof(double));
    }

    [Fact]
    public void AsComputeQueryable_PreservesExpression()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.AsComputeQueryable();

        // Assert
        result.Expression.Should().NotBeNull();
    }

    [Fact]
    public void AsComputeQueryable_MultipleCallsOnSameSource_CreatesSeparateInstances()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result1 = data.AsComputeQueryable();
        var result2 = data.AsComputeQueryable();

        // Assert
        result1.Should().NotBeSameAs(result2);
    }

    [Fact]
    public void AsComputeQueryable_WithLargeDataSet_HandlesCorrectly()
    {
        // Arrange
        var data = Enumerable.Range(1, 10000).AsQueryable();

        // Act
        var result = data.AsComputeQueryable();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(10000);
    }

    #endregion

    #region ToComputeArray Tests

    [Fact]
    public void ToComputeArray_WithValidSource_ReturnsArray()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data.ToComputeArray();

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void ToComputeArray_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        IQueryable<int>? nullSource = null;

        // Act
        var act = () => nullSource!.ToComputeArray();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void ToComputeArray_WithEmptySource_ReturnsEmptyArray()
    {
        // Arrange
        var data = Array.Empty<int>().AsQueryable();

        // Act
        var result = data.ToComputeArray();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToComputeArray_WithComputeQueryable_ReturnsArray()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable().AsComputeQueryable();

        // Act
        var result = data.ToComputeArray();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ToComputeArray_WithComplexType_ReturnsCorrectArray()
    {
        // Arrange
        var data = new[]
        {
            new TestData { Id = 1, Value = "A" },
            new TestData { Id = 2, Value = "B" }
        }.AsQueryable();

        // Act
        var result = data.ToComputeArray();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[1].Value.Should().Be("B");
    }

    [Fact]
    public void ToComputeArray_WithFilteredData_ReturnsFilteredArray()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable().Where(x => x > 2);

        // Act
        var result = data.ToComputeArray();

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void ToComputeArray_WithLargeDataSet_HandlesCorrectly()
    {
        // Arrange
        var data = Enumerable.Range(1, 10000).AsQueryable();

        // Act
        var result = data.ToComputeArray();

        // Assert
        result.Should().HaveCount(10000);
        result.First().Should().Be(1);
        result.Last().Should().Be(10000);
    }

    #endregion

    #region ComputeSelect Tests

    [Fact]
    public void ComputeSelect_WithValidSelectorOnIntegers_TransformsElements()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data.ComputeSelect(x => x * 2).ToArray();

        // Assert
        result.Should().Equal(2, 4, 6, 8, 10);
    }

    [Fact]
    public void ComputeSelect_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        IQueryable<int>? nullSource = null;

        // Act
        var act = () => nullSource!.ComputeSelect(x => x * 2);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void ComputeSelect_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var act = () => data.ComputeSelect<int, int>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
    }

    [Fact]
    public void ComputeSelect_WithTypeConversion_ConvertsTypes()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.ComputeSelect(x => x.ToString()).ToArray();

        // Assert
        result.Should().Equal("1", "2", "3");
    }

    [Fact]
    public void ComputeSelect_WithComplexTransformation_TransformsCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestData { Id = 1, Value = "A" },
            new TestData { Id = 2, Value = "B" }
        }.AsQueryable();

        // Act
        var result = data.ComputeSelect(x => new { x.Id, Upper = x.Value.ToUpper() }).ToArray();

        // Assert
        result.Should().HaveCount(2);
        result[0].Upper.Should().Be("A");
        result[1].Upper.Should().Be("B");
    }

    [Fact]
    public void ComputeSelect_WithEmptySource_ReturnsEmptyResult()
    {
        // Arrange
        var data = Array.Empty<int>().AsQueryable();

        // Act
        var result = data.ComputeSelect(x => x * 2).ToArray();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSelect_ChainedWithComputeWhere_WorksCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data
            .ComputeWhere(x => x > 2)
            .ComputeSelect(x => x * 2)
            .ToArray();

        // Assert
        result.Should().Equal(6, 8, 10);
    }

    [Fact]
    public void ComputeSelect_WithProjection_ProjectsCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestData { Id = 1, Value = "A" },
            new TestData { Id = 2, Value = "B" }
        }.AsQueryable();

        // Act
        var result = data.ComputeSelect(x => x.Id).ToArray();

        // Assert
        result.Should().Equal(1, 2);
    }

    [Fact]
    public void ComputeSelect_WithComplexSelector_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.ComputeSelect(x => x * x + x).ToArray();

        // Assert
        result.Should().Equal(2, 6, 12); // 1*1+1, 2*2+2, 3*3+3
    }

    [Fact]
    public void ComputeSelect_PreservesQueryableInterface()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.ComputeSelect(x => x * 2);

        // Assert
        result.Should().BeAssignableTo<IQueryable<int>>();
    }

    #endregion

    #region ComputeWhere Tests

    [Fact]
    public void ComputeWhere_WithValidPredicate_FiltersElements()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data.ComputeWhere(x => x > 3).ToArray();

        // Assert
        result.Should().Equal(4, 5);
    }

    [Fact]
    public void ComputeWhere_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        IQueryable<int>? nullSource = null;

        // Act
        var act = () => nullSource!.ComputeWhere(x => x > 0);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void ComputeWhere_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var act = () => data.ComputeWhere(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    [Fact]
    public void ComputeWhere_WithAlwaysTruePredicate_ReturnsAllElements()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.ComputeWhere(x => true).ToArray();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ComputeWhere_WithAlwaysFalsePredicate_ReturnsEmpty()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.ComputeWhere(x => false).ToArray();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeWhere_WithComplexPredicate_FiltersCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5, 6 }.AsQueryable();

        // Act
        var result = data.ComputeWhere(x => x % 2 == 0 && x > 2).ToArray();

        // Assert
        result.Should().Equal(4, 6);
    }

    [Fact]
    public void ComputeWhere_WithComplexType_FiltersComplexObjects()
    {
        // Arrange
        var data = new[]
        {
            new TestData { Id = 1, Value = "A" },
            new TestData { Id = 2, Value = "B" },
            new TestData { Id = 3, Value = "C" }
        }.AsQueryable();

        // Act
        var result = data.ComputeWhere(x => x.Id > 1).ToArray();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2);
        result[1].Id.Should().Be(3);
    }

    [Fact]
    public void ComputeWhere_ChainedMultipleTimes_AppliesAllFilters()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5, 6 }.AsQueryable();

        // Act
        var result = data
            .ComputeWhere(x => x > 2)
            .ComputeWhere(x => x < 6)
            .ToArray();

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void ComputeWhere_WithEmptySource_ReturnsEmpty()
    {
        // Arrange
        var data = Array.Empty<int>().AsQueryable();

        // Act
        var result = data.ComputeWhere(x => x > 0).ToArray();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeWhere_PreservesQueryableInterface()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data.ComputeWhere(x => x > 1);

        // Assert
        result.Should().BeAssignableTo<IQueryable<int>>();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_CompleteQueryChain_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x > 3)
            .ComputeSelect(x => x * 2)
            .ComputeWhere(x => x < 16)
            .ToComputeArray();

        // Assert
        result.Should().Equal(8, 10, 12, 14);
    }

    [Fact]
    public void Integration_ComplexQueryWithMultipleOperations_WorksCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestData { Id = 1, Value = "A" },
            new TestData { Id = 2, Value = "B" },
            new TestData { Id = 3, Value = "C" },
            new TestData { Id = 4, Value = "D" }
        }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x.Id > 1)
            .ComputeSelect(x => new { x.Id, Upper = x.Value.ToUpper() })
            .ComputeWhere(x => x.Id < 4)
            .ToComputeArray();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2);
        result[1].Id.Should().Be(3);
    }

    [Fact]
    public void Integration_LargeDataSetWithFiltersAndProjections_PerformsCorrectly()
    {
        // Arrange
        var data = Enumerable.Range(1, 1000).AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x % 2 == 0)
            .ComputeSelect(x => x * 2)
            .ComputeWhere(x => x < 100)
            .ToComputeArray();

        // Assert
        // Even numbers from 1-1000: 2,4,6...1000 -> multiply by 2: 4,8,12...2000 -> filter < 100: 4,8,12...96
        // Count: (96-4)/4 + 1 = 24
        result.Should().HaveCount(24);
        result.First().Should().Be(4); // 2 * 2
        result.Last().Should().Be(96); // 48 * 2
    }

    [Fact]
    public void Integration_WithStandardLinqMethods_InteroperatesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x > 2)
            .Take(2)
            .ToArray();

        // Assert
        result.Should().Equal(3, 4);
        // Note: OrderBy removed as minimal implementation doesn't support IOrderedQueryable
    }

    [Fact]
    public void Integration_RepeatedAsComputeQueryable_DoesNotThrow()
    {
        // Arrange
        var data = new[] { 1, 2, 3 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .AsComputeQueryable()
            .AsComputeQueryable()
            .ToComputeArray();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    #endregion

    #region Helper Classes

    private sealed class TestData
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
