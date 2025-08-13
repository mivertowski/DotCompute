// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive unit tests for the Dim3 struct.
/// </summary>
public class Dim3Tests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithSingleParameter_ShouldSetXAndDefaultYZ()
    {
        // Arrange & Act
        var dim3 = new Dim3(5);

        // Assert
        dim3.X.Should().Be(5);
        dim3.Y.Should().Be(1);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithTwoParameters_ShouldSetXYAndDefaultZ()
    {
        // Arrange & Act
        var dim3 = new Dim3(10, 20);

        // Assert
        dim3.X.Should().Be(10);
        dim3.Y.Should().Be(20);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithThreeParameters_ShouldSetAllDimensions()
    {
        // Arrange & Act
        var dim3 = new Dim3(100, 200, 300);

        // Assert
        dim3.X.Should().Be(100);
        dim3.Y.Should().Be(200);
        dim3.Z.Should().Be(300);
    }

    [Fact]
    public void Constructor_WithZeroValues_ShouldAcceptZero()
    {
        // Arrange & Act
        var dim3 = new Dim3(0, 0, 0);

        // Assert
        dim3.X.Should().Be(0);
        dim3.Y.Should().Be(0);
        dim3.Z.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNegativeValues_ShouldAcceptNegativeValues()
    {
        // Arrange & Act
        var dim3 = new Dim3(-1, -2, -3);

        // Assert
        dim3.X.Should().Be(-1);
        dim3.Y.Should().Be(-2);
        dim3.Z.Should().Be(-3);
    }

    [Fact]
    public void Constructor_WithMaxValues_ShouldHandleMaxIntValues()
    {
        // Arrange & Act
        var dim3 = new Dim3(int.MaxValue, int.MaxValue, int.MaxValue);

        // Assert
        dim3.X.Should().Be(int.MaxValue);
        dim3.Y.Should().Be(int.MaxValue);
        dim3.Z.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Constructor_WithMinValues_ShouldHandleMinIntValues()
    {
        // Arrange & Act
        var dim3 = new Dim3(int.MinValue, int.MinValue, int.MinValue);

        // Assert
        dim3.X.Should().Be(int.MinValue);
        dim3.Y.Should().Be(int.MinValue);
        dim3.Z.Should().Be(int.MinValue);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromInt_ShouldCreateDim3WithXOnly()
    {
        // Arrange & Act
        Dim3 dim3 = 42;

        // Assert
        dim3.X.Should().Be(42);
        dim3.Y.Should().Be(1);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromTwoComponentTuple_ShouldCreateDim3WithXY()
    {
        // Arrange & Act
        Dim3 dim3 = (10, 20);

        // Assert
        dim3.X.Should().Be(10);
        dim3.Y.Should().Be(20);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromThreeComponentTuple_ShouldCreateDim3WithXYZ()
    {
        // Arrange & Act
        Dim3 dim3 = (100, 200, 300);

        // Assert
        dim3.X.Should().Be(100);
        dim3.Y.Should().Be(200);
        dim3.Z.Should().Be(300);
    }

    [Fact]
    public void ImplicitConversion_FromZeroInt_ShouldCreateDim3WithZero()
    {
        // Arrange & Act
        Dim3 dim3 = 0;

        // Assert
        dim3.X.Should().Be(0);
        dim3.Y.Should().Be(1);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromNegativeInt_ShouldCreateDim3WithNegativeX()
    {
        // Arrange & Act
        Dim3 dim3 = -5;

        // Assert
        dim3.X.Should().Be(-5);
        dim3.Y.Should().Be(1);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromTupleWithZeros_ShouldCreateDim3WithZeros()
    {
        // Arrange & Act
        Dim3 dim3XY = (0, 0);
        Dim3 dim3XYZ = (0, 0, 0);

        // Assert
        dim3XY.X.Should().Be(0);
        dim3XY.Y.Should().Be(0);
        dim3XY.Z.Should().Be(1);

        dim3XYZ.X.Should().Be(0);
        dim3XYZ.Y.Should().Be(0);
        dim3XYZ.Z.Should().Be(0);
    }

    [Fact]
    public void ImplicitConversion_FromTupleWithNegatives_ShouldCreateDim3WithNegatives()
    {
        // Arrange & Act
        Dim3 dim3XY = (-1, -2);
        Dim3 dim3XYZ = (-10, -20, -30);

        // Assert
        dim3XY.X.Should().Be(-1);
        dim3XY.Y.Should().Be(-2);
        dim3XY.Z.Should().Be(1);

        dim3XYZ.X.Should().Be(-10);
        dim3XYZ.Y.Should().Be(-20);
        dim3XYZ.Z.Should().Be(-30);
    }

    #endregion

    #region Static Factory Method Tests

    [Fact]
    public void FromInt32_ShouldCreateDim3FromInt()
    {
        // Arrange & Act
        var dim3 = Dim3.FromInt32(15);

        // Assert
        dim3.X.Should().Be(15);
        dim3.Y.Should().Be(1);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void FromValueTuple_WithTwoComponents_ShouldCreateDim3()
    {
        // Arrange & Act
        var dim3 = Dim3.FromValueTuple((25, 35));

        // Assert
        dim3.X.Should().Be(25);
        dim3.Y.Should().Be(35);
        dim3.Z.Should().Be(1);
    }

    [Fact]
    public void FromValueTuple_WithThreeComponents_ShouldCreateDim3()
    {
        // Arrange & Act
        var dim3 = Dim3.FromValueTuple((45, 55, 65));

        // Assert
        dim3.X.Should().Be(45);
        dim3.Y.Should().Be(55);
        dim3.Z.Should().Be(65);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(-50)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void FromInt32_WithVariousValues_ShouldCreateCorrectDim3(int value)
    {
        // Act
        var dim3 = Dim3.FromInt32(value);

        // Assert
        dim3.X.Should().Be(value);
        dim3.Y.Should().Be(1);
        dim3.Z.Should().Be(1);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);

        // Act & Assert
        dim3.Equals(dim3).Should().BeTrue();
        (dim3 == dim3).Should().BeTrue();
        (dim3 != dim3).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var dim3a = new Dim3(10, 20, 30);
        var dim3b = new Dim3(10, 20, 30);

        // Act & Assert
        dim3a.Equals(dim3b).Should().BeTrue();
        (dim3a == dim3b).Should().BeTrue();
        (dim3a != dim3b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentXValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(2, 2, 3);

        // Act & Assert
        dim3a.Equals(dim3b).Should().BeFalse();
        (dim3a == dim3b).Should().BeFalse();
        (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentYValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(1, 3, 3);

        // Act & Assert
        dim3a.Equals(dim3b).Should().BeFalse();
        (dim3a == dim3b).Should().BeFalse();
        (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentZValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(1, 2, 4);

        // Act & Assert
        dim3a.Equals(dim3b).Should().BeFalse();
        (dim3a == dim3b).Should().BeFalse();
        (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithAllDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(4, 5, 6);

        // Act & Assert
        dim3a.Equals(dim3b).Should().BeFalse();
        (dim3a == dim3b).Should().BeFalse();
        (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);

        // Act & Assert
        dim3.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);
        var otherObject = "not a dim3";

        // Act & Assert
        dim3.Equals(otherObject).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithBoxedValue_ShouldReturnCorrectResult()
    {
        // Arrange
        var dim3a = new Dim3(5, 10, 15);
        var dim3b = new Dim3(5, 10, 15);
        object boxedDim3b = dim3b;

        // Act & Assert
        dim3a.Equals(boxedDim3b).Should().BeTrue();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_WithSameInstance_ShouldReturnSameHashCode()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);

        // Act
        var hashCode1 = dim3.GetHashCode();
        var hashCode2 = dim3.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Fact]
    public void GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var dim3a = new Dim3(10, 20, 30);
        var dim3b = new Dim3(10, 20, 30);

        // Act
        var hashCode1 = dim3a.GetHashCode();
        var hashCode2 = dim3b.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Fact]
    public void GetHashCode_WithDifferentInstances_ShouldReturnDifferentHashCodes()
    {
        // Note: Hash codes aren't guaranteed to be different, but should be different most of the time
        
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(4, 5, 6);

        // Act
        var hashCode1 = dim3a.GetHashCode();
        var hashCode2 = dim3b.GetHashCode();

        // Assert
        hashCode1.Should().NotBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_WithOnlyOneDimensionDifferent_ShouldReturnDifferentHashCodes()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(1, 2, 4); // Only Z is different

        // Act
        var hashCode1 = dim3a.GetHashCode();
        var hashCode2 = dim3b.GetHashCode();

        // Assert
        hashCode1.Should().NotBe(hashCode2);
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void EqualityOperator_ShouldWorkCorrectly()
    {
        // Arrange
        var dim3a = new Dim3(5, 10, 15);
        var dim3b = new Dim3(5, 10, 15);
        var dim3c = new Dim3(5, 10, 16);

        // Act & Assert
        (dim3a == dim3b).Should().BeTrue();
        (dim3a == dim3c).Should().BeFalse();
        (dim3a == dim3a).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_ShouldWorkCorrectly()
    {
        // Arrange
        var dim3a = new Dim3(5, 10, 15);
        var dim3b = new Dim3(5, 10, 15);
        var dim3c = new Dim3(5, 10, 16);

        // Act & Assert
        (dim3a != dim3b).Should().BeFalse();
        (dim3a != dim3c).Should().BeTrue();
        (dim3a != dim3a).Should().BeFalse();
    }

    #endregion

    #region Property Access Tests

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange
        var dim3 = new Dim3(100, 200, 300);

        // Act & Assert - Properties should only have getters
        var xProperty = typeof(Dim3).GetProperty(nameof(Dim3.X));
        var yProperty = typeof(Dim3).GetProperty(nameof(Dim3.Y));
        var zProperty = typeof(Dim3).GetProperty(nameof(Dim3.Z));

        xProperty!.CanRead.Should().BeTrue();
        xProperty.CanWrite.Should().BeFalse();
        yProperty!.CanRead.Should().BeTrue();
        yProperty.CanWrite.Should().BeFalse();
        zProperty!.CanRead.Should().BeTrue();
        zProperty.CanWrite.Should().BeFalse();

        // Verify values are accessible
        dim3.X.Should().Be(100);
        dim3.Y.Should().Be(200);
        dim3.Z.Should().Be(300);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void Dim3_WithZeroForAllDimensions_ShouldWork()
    {
        // Arrange & Act
        var dim3 = new Dim3(0, 0, 0);

        // Assert
        dim3.X.Should().Be(0);
        dim3.Y.Should().Be(0);
        dim3.Z.Should().Be(0);
    }

    [Fact]
    public void Dim3_WithMixedPositiveNegativeZero_ShouldWork()
    {
        // Arrange & Act
        var dim3 = new Dim3(-100, 0, 100);

        // Assert
        dim3.X.Should().Be(-100);
        dim3.Y.Should().Be(0);
        dim3.Z.Should().Be(100);
    }

    [Fact]
    public void Dim3_WithTypicalGPUBlockSizes_ShouldWork()
    {
        // Test with common GPU block sizes
        var testCases = new[]
        {
            new Dim3(256),
            new Dim3(16, 16),
            new Dim3(8, 8, 8),
            new Dim3(32, 32),
            new Dim3(1024)
        };

        foreach (var dim3 in testCases)
        {
            // Should not throw and should have reasonable values
            dim3.X.Should().BeGreaterThan(0);
            dim3.Y.Should().BeGreaterThan(0);
            dim3.Z.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region IEquatable Implementation Tests

    [Fact]
    public void Dim3_ShouldImplementIEquatable()
    {
        // Arrange & Act
        var type = typeof(Dim3);

        // Assert
        type.Should().BeAssignableTo<IEquatable<Dim3>>();
    }

    #endregion

    #region Conversion Consistency Tests

    [Fact]
    public void AllConversionMethods_ShouldProduceSameResults()
    {
        // Test that all ways of creating equivalent Dim3 instances produce the same results
        
        // Single int conversion
        Dim3 fromInt1 = 42;
        Dim3 fromInt2 = Dim3.FromInt32(42);
        var fromConstructor1 = new Dim3(42);

        fromInt1.Should().Be(fromInt2);
        fromInt1.Should().Be(fromConstructor1);
        fromInt2.Should().Be(fromConstructor1);

        // Two component tuple conversion
        Dim3 fromTuple2a = (10, 20);
        Dim3 fromTuple2b = Dim3.FromValueTuple((10, 20));
        var fromConstructor2 = new Dim3(10, 20);

        fromTuple2a.Should().Be(fromTuple2b);
        fromTuple2a.Should().Be(fromConstructor2);
        fromTuple2b.Should().Be(fromConstructor2);

        // Three component tuple conversion
        Dim3 fromTuple3a = (100, 200, 300);
        Dim3 fromTuple3b = Dim3.FromValueTuple((100, 200, 300));
        var fromConstructor3 = new Dim3(100, 200, 300);

        fromTuple3a.Should().Be(fromTuple3b);
        fromTuple3a.Should().Be(fromConstructor3);
        fromTuple3b.Should().Be(fromConstructor3);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void Dim3_PropertyAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var dim3 = new Dim3(123, 456, 789);
        var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

        // Act - Multiple threads accessing properties
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var x = dim3.X;
                var y = dim3.Y;
                var z = dim3.Z;
                var hashCode = dim3.GetHashCode();
                var equals = dim3.Equals(dim3);

                x.Should().Be(123);
                y.Should().Be(456);
                z.Should().Be(789);
                equals.Should().BeTrue();
            }));
        }

        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

        // Assert - No exceptions should be thrown
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    #endregion
}