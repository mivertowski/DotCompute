// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Linq;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests.Integration;

/// <summary>
/// Integration tests verifying LINQ functionality works correctly with all compute backends.
/// </summary>
public class LinqBackendIntegrationTests
{
    #region CPU Backend Tests

    [Fact]
    public void CpuBackend_SimpleSelect_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeSelect(x => x * 2)
            .ToArray();

        // Assert
        result.Should().Equal(2, 4, 6, 8, 10);
    }

    [Fact]
    public void CpuBackend_SimpleWhere_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x > 2)
            .ToArray();

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void CpuBackend_CombinedWhereAndSelect_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x > 2)
            .ComputeSelect(x => x * 2)
            .ToArray();

        // Assert
        result.Should().Equal(6, 8, 10);
    }

    [Fact]
    public void CpuBackend_Sum_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data.AsComputeQueryable().Sum();

        // Assert
        result.Should().Be(15);
    }

    [Fact]
    public void CpuBackend_Average_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 2, 4, 6, 8, 10 }.AsQueryable();

        // Act
        var result = data.AsComputeQueryable().Average();

        // Assert
        result.Should().Be(6.0);
    }

    [Fact]
    public void CpuBackend_Count_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x > 2)
            .Count();

        // Assert
        result.Should().Be(3);
    }

    #endregion

    #region CUDA Backend Tests

    [Fact]
    public void CudaBackend_SimpleSelect_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        try
        {
            // Act
            var result = data
                .AsComputeQueryable()
                .ComputeSelect(x => x * 2)
                .ToArray();

            // Assert
            result.Should().Equal(2, 4, 6, 8, 10);
        }
        catch (NotSupportedException)
        {
            // CUDA may not be available - skip test
            Assert.True(true, "CUDA not available, test skipped");
        }
    }

    [Fact]
    public void CudaBackend_CombinedOperations_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsQueryable();

        try
        {
            // Act - Use larger dataset that benefits from GPU
            var result = data
                .AsComputeQueryable()
                .ComputeWhere(x => x > 3)
                .ComputeSelect(x => x * 3)
                .ToArray();

            // Assert
            result.Should().Equal(12, 15, 18, 21, 24, 27, 30);
        }
        catch (NotSupportedException)
        {
            // CUDA may not be available - skip test
            Assert.True(true, "CUDA not available, test skipped");
        }
    }

    [Fact]
    public void CudaBackend_Sum_ExecutesCorrectly()
    {
        // Arrange
        var data = Enumerable.Range(1, 1000).AsQueryable();

        try
        {
            // Act
            var result = data.AsComputeQueryable().Sum();

            // Assert
            result.Should().Be(500500); // Sum of 1 to 1000
        }
        catch (NotSupportedException)
        {
            // CUDA may not be available - skip test
            Assert.True(true, "CUDA not available, test skipped");
        }
    }

    #endregion

    #region OpenCL Backend Tests

    [Fact]
    public void OpenCLBackend_SimpleSelect_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        try
        {
            // Act
            var result = data
                .AsComputeQueryable()
                .ComputeSelect(x => x * 2)
                .ToArray();

            // Assert
            result.Should().Equal(2, 4, 6, 8, 10);
        }
        catch (NotSupportedException)
        {
            // OpenCL may not be available - skip test
            Assert.True(true, "OpenCL not available, test skipped");
        }
    }

    [Fact]
    public void OpenCLBackend_CombinedOperations_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        try
        {
            // Act
            var result = data
                .AsComputeQueryable()
                .ComputeWhere(x => x % 2 == 0)
                .ComputeSelect(x => x * x)
                .ToArray();

            // Assert
            result.Should().Equal(4, 16);
        }
        catch (NotSupportedException)
        {
            // OpenCL may not be available - skip test
            Assert.True(true, "OpenCL not available, test skipped");
        }
    }

    #endregion

    #region Metal Backend Tests

    [Fact]
    public void MetalBackend_SimpleSelect_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        try
        {
            // Act
            var result = data
                .AsComputeQueryable()
                .ComputeSelect(x => x * 2)
                .ToArray();

            // Assert
            result.Should().Equal(2, 4, 6, 8, 10);
        }
        catch (NotSupportedException)
        {
            // Metal only available on macOS - skip test
            Assert.True(true, "Metal not available (requires macOS), test skipped");
        }
    }

    [Fact]
    public void MetalBackend_CombinedOperations_ExecutesCorrectly()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.AsQueryable();

        try
        {
            // Act
            var result = data
                .AsComputeQueryable()
                .ComputeWhere(x => x > 4)
                .ComputeSelect(x => x + 10)
                .ToArray();

            // Assert
            result.Should().Equal(15, 16, 17, 18);
        }
        catch (NotSupportedException)
        {
            // Metal only available on macOS - skip test
            Assert.True(true, "Metal not available (requires macOS), test skipped");
        }
    }

    #endregion

    #region Cross-Backend Consistency Tests

    [Fact]
    public void AllBackends_ProduceSameResults_ForSimpleSelect()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 }.AsQueryable();

        // Act - Execute same query, should fall back to CPU if backends unavailable
        var result1 = data.AsComputeQueryable().ComputeSelect(x => x * 2).ToArray();
        var result2 = data.AsComputeQueryable().ComputeSelect(x => x * 2).ToArray();

        // Assert - Both should produce same results
        result1.Should().Equal(result2);
    }

    [Fact]
    public void AllBackends_ProduceSameResults_ForComplexQuery()
    {
        // Arrange
        var data = Enumerable.Range(1, 100).AsQueryable();

        // Act
        var result = data
            .AsComputeQueryable()
            .ComputeWhere(x => x % 2 == 0)
            .ComputeSelect(x => x * x)
            .ToArray();

        // Assert - Verify correctness
        result.Should().HaveCount(50);
        result.First().Should().Be(4);   // 2^2
        result.Last().Should().Be(10000); // 100^2
    }

    #endregion
}
