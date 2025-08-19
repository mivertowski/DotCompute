// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for the Dim3 struct.
/// </summary>
public sealed class Dim3Tests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithSingleParameter_ShouldSetXAndDefaultYZ()
    {
        // Arrange & Act
        var dim3 = new Dim3(5);

        // Assert
        _ = dim3.X.Should().Be(5);
        _ = dim3.Y.Should().Be(1);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithTwoParameters_ShouldSetXYAndDefaultZ()
    {
        // Arrange & Act
        var dim3 = new Dim3(10, 20);

        // Assert
        _ = dim3.X.Should().Be(10);
        _ = dim3.Y.Should().Be(20);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithThreeParameters_ShouldSetAllDimensions()
    {
        // Arrange & Act
        var dim3 = new Dim3(100, 200, 300);

        // Assert
        _ = dim3.X.Should().Be(100);
        _ = dim3.Y.Should().Be(200);
        _ = dim3.Z.Should().Be(300);
    }

    [Fact]
    public void Constructor_WithZeroValues_ShouldAcceptZero()
    {
        // Arrange & Act
        var dim3 = new Dim3(0, 0, 0);

        // Assert
        _ = dim3.X.Should().Be(0);
        _ = dim3.Y.Should().Be(0);
        _ = dim3.Z.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNegativeValues_ShouldAcceptNegativeValues()
    {
        // Arrange & Act
        var dim3 = new Dim3(-1, -2, -3);

        // Assert
        _ = dim3.X.Should().Be(-1);
        _ = dim3.Y.Should().Be(-2);
        _ = dim3.Z.Should().Be(-3);
    }

    [Fact]
    public void Constructor_WithMaxValues_ShouldHandleMaxIntValues()
    {
        // Arrange & Act
        var dim3 = new Dim3(int.MaxValue, int.MaxValue, int.MaxValue);

        // Assert
        _ = dim3.X.Should().Be(int.MaxValue);
        _ = dim3.Y.Should().Be(int.MaxValue);
        _ = dim3.Z.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Constructor_WithMinValues_ShouldHandleMinIntValues()
    {
        // Arrange & Act
        var dim3 = new Dim3(int.MinValue, int.MinValue, int.MinValue);

        // Assert
        _ = dim3.X.Should().Be(int.MinValue);
        _ = dim3.Y.Should().Be(int.MinValue);
        _ = dim3.Z.Should().Be(int.MinValue);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromInt_ShouldCreateDim3WithXOnly()
    {
        // Arrange & Act
        Dim3 dim3 = 42;

        // Assert
        _ = dim3.X.Should().Be(42);
        _ = dim3.Y.Should().Be(1);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromTwoComponentTuple_ShouldCreateDim3WithXY()
    {
        // Arrange & Act
        Dim3 dim3 = (10, 20);

        // Assert
        _ = dim3.X.Should().Be(10);
        _ = dim3.Y.Should().Be(20);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromThreeComponentTuple_ShouldCreateDim3WithXYZ()
    {
        // Arrange & Act
        Dim3 dim3 = (100, 200, 300);

        // Assert
        _ = dim3.X.Should().Be(100);
        _ = dim3.Y.Should().Be(200);
        _ = dim3.Z.Should().Be(300);
    }

    [Fact]
    public void ImplicitConversion_FromZeroInt_ShouldCreateDim3WithZero()
    {
        // Arrange & Act
        Dim3 dim3 = 0;

        // Assert
        _ = dim3.X.Should().Be(0);
        _ = dim3.Y.Should().Be(1);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromNegativeInt_ShouldCreateDim3WithNegativeX()
    {
        // Arrange & Act
        Dim3 dim3 = -5;

        // Assert
        _ = dim3.X.Should().Be(-5);
        _ = dim3.Y.Should().Be(1);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_FromTupleWithZeros_ShouldCreateDim3WithZeros()
    {
        // Arrange & Act
        Dim3 dim3XY = (0, 0);
        Dim3 dim3XYZ = (0, 0, 0);

        // Assert
        _ = dim3XY.X.Should().Be(0);
        _ = dim3XY.Y.Should().Be(0);
        _ = dim3XY.Z.Should().Be(1);

        _ = dim3XYZ.X.Should().Be(0);
        _ = dim3XYZ.Y.Should().Be(0);
        _ = dim3XYZ.Z.Should().Be(0);
    }

    [Fact]
    public void ImplicitConversion_FromTupleWithNegatives_ShouldCreateDim3WithNegatives()
    {
        // Arrange & Act
        Dim3 dim3XY = (-1, -2);
        Dim3 dim3XYZ = (-10, -20, -30);

        // Assert
        _ = dim3XY.X.Should().Be(-1);
        _ = dim3XY.Y.Should().Be(-2);
        _ = dim3XY.Z.Should().Be(1);

        _ = dim3XYZ.X.Should().Be(-10);
        _ = dim3XYZ.Y.Should().Be(-20);
        _ = dim3XYZ.Z.Should().Be(-30);
    }

    #endregion

    #region Static Factory Method Tests

    [Fact]
    public void FromInt32_ShouldCreateDim3FromInt()
    {
        // Arrange & Act
        var dim3 = Dim3.FromInt32(15);

        // Assert
        _ = dim3.X.Should().Be(15);
        _ = dim3.Y.Should().Be(1);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void FromValueTuple_WithTwoComponents_ShouldCreateDim3()
    {
        // Arrange & Act
        var dim3 = Dim3.FromValueTuple((25, 35));

        // Assert
        _ = dim3.X.Should().Be(25);
        _ = dim3.Y.Should().Be(35);
        _ = dim3.Z.Should().Be(1);
    }

    [Fact]
    public void FromValueTuple_WithThreeComponents_ShouldCreateDim3()
    {
        // Arrange & Act
        var dim3 = Dim3.FromValueTuple((45, 55, 65));

        // Assert
        _ = dim3.X.Should().Be(45);
        _ = dim3.Y.Should().Be(55);
        _ = dim3.Z.Should().Be(65);
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
        _ = dim3.X.Should().Be(value);
        _ = dim3.Y.Should().Be(1);
        _ = dim3.Z.Should().Be(1);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);

        // Act & Assert
        _ = dim3.Equals(dim3).Should().BeTrue();
        // Note: Self-comparison tests are always true by definition
#pragma warning disable CS1718 // Comparison made to same variable
        Assert.True(dim3 == dim3);
        Assert.False(dim3 != dim3);
#pragma warning restore CS1718
    }

    [Fact]
    public void Equals_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var dim3a = new Dim3(10, 20, 30);
        var dim3b = new Dim3(10, 20, 30);

        // Act & Assert
        _ = dim3a.Equals(dim3b).Should().BeTrue();
        _ = (dim3a == dim3b).Should().BeTrue();
        _ = (dim3a != dim3b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentXValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(2, 2, 3);

        // Act & Assert
        _ = dim3a.Equals(dim3b).Should().BeFalse();
        _ = (dim3a == dim3b).Should().BeFalse();
        _ = (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentYValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(1, 3, 3);

        // Act & Assert
        _ = dim3a.Equals(dim3b).Should().BeFalse();
        _ = (dim3a == dim3b).Should().BeFalse();
        _ = (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentZValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(1, 2, 4);

        // Act & Assert
        _ = dim3a.Equals(dim3b).Should().BeFalse();
        _ = (dim3a == dim3b).Should().BeFalse();
        _ = (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithAllDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var dim3a = new Dim3(1, 2, 3);
        var dim3b = new Dim3(4, 5, 6);

        // Act & Assert
        _ = dim3a.Equals(dim3b).Should().BeFalse();
        _ = (dim3a == dim3b).Should().BeFalse();
        _ = (dim3a != dim3b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);

        // Act & Assert
        _ = dim3.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var dim3 = new Dim3(1, 2, 3);
        var otherObject = "not a dim3";

        // Act & Assert
        _ = dim3.Equals(otherObject).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithBoxedValue_ShouldReturnCorrectResult()
    {
        // Arrange
        var dim3a = new Dim3(5, 10, 15);
        var dim3b = new Dim3(5, 10, 15);
        object boxedDim3b = dim3b;

        // Act & Assert
        _ = dim3a.Equals(boxedDim3b).Should().BeTrue();
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
        Assert.Equal(hashCode2, hashCode1);
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
        Assert.Equal(hashCode2, hashCode1);
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
        _ = hashCode1.Should().NotBe(hashCode2);
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
        _ = hashCode1.Should().NotBe(hashCode2);
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
        _ = (dim3a == dim3b).Should().BeTrue();
        _ = (dim3a == dim3c).Should().BeFalse();
#pragma warning disable CS1718 // Comparison made to same variable
        _ = (dim3a == dim3a).Should().BeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void InequalityOperator_ShouldWorkCorrectly()
    {
        // Arrange
        var dim3a = new Dim3(5, 10, 15);
        var dim3b = new Dim3(5, 10, 15);
        var dim3c = new Dim3(5, 10, 16);

        // Act & Assert
        _ = (dim3a != dim3b).Should().BeFalse();
        _ = (dim3a != dim3c).Should().BeTrue();
#pragma warning disable CS1718 // Comparison made to same variable
        _ = (dim3a != dim3a).Should().BeFalse();
#pragma warning restore CS1718
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

        _ = xProperty!.CanRead.Should().BeTrue();
        _ = xProperty.CanWrite.Should().BeFalse();
        _ = yProperty!.CanRead.Should().BeTrue();
        _ = yProperty.CanWrite.Should().BeFalse();
        _ = zProperty!.CanRead.Should().BeTrue();
        _ = zProperty.CanWrite.Should().BeFalse();

        // Verify values are accessible
        _ = dim3.X.Should().Be(100);
        _ = dim3.Y.Should().Be(200);
        _ = dim3.Z.Should().Be(300);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void Dim3_WithZeroForAllDimensions_ShouldWork()
    {
        // Arrange & Act
        var dim3 = new Dim3(0, 0, 0);

        // Assert
        _ = dim3.X.Should().Be(0);
        _ = dim3.Y.Should().Be(0);
        _ = dim3.Z.Should().Be(0);
    }

    [Fact]
    public void Dim3_WithMixedPositiveNegativeZero_ShouldWork()
    {
        // Arrange & Act
        var dim3 = new Dim3(-100, 0, 100);

        // Assert
        _ = dim3.X.Should().Be(-100);
        _ = dim3.Y.Should().Be(0);
        _ = dim3.Z.Should().Be(100);
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
            _ = (dim3.X > 0).Should().BeTrue();
            _ = (dim3.Y > 0).Should().BeTrue();
            _ = (dim3.Z > 0).Should().BeTrue();
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
        _ = type.Should().BeAssignableTo<IEquatable<Dim3>>();
    }

    #endregion

    #region Conversion Consistency Tests

    [Fact]
    public void AllConversionMethods_ShouldProduceSameResults()
    {
        // Test that all ways of creating equivalent Dim3 instances produce the same results

        // Single int conversion
        Dim3 fromInt1 = 42;
        var fromInt2 = Dim3.FromInt32(42);
        var fromConstructor1 = new Dim3(42);

        Assert.Equal(fromInt2, fromInt1);
        Assert.Equal(fromConstructor1, fromInt1);
        Assert.Equal(fromConstructor1, fromInt2);

        // Two component tuple conversion
        Dim3 fromTuple2a = (10, 20);
        var fromTuple2b = Dim3.FromValueTuple((10, 20));
        var fromConstructor2 = new Dim3(10, 20);

        Assert.Equal(fromTuple2b, fromTuple2a);
        Assert.Equal(fromConstructor2, fromTuple2a);
        Assert.Equal(fromConstructor2, fromTuple2b);

        // Three component tuple conversion
        Dim3 fromTuple3a = (100, 200, 300);
        var fromTuple3b = Dim3.FromValueTuple((100, 200, 300));
        var fromConstructor3 = new Dim3(100, 200, 300);

        Assert.Equal(fromTuple3b, fromTuple3a);
        Assert.Equal(fromConstructor3, fromTuple3a);
        Assert.Equal(fromConstructor3, fromTuple3b);
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
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var x = dim3.X;
                var y = dim3.Y;
                var z = dim3.Z;
                var hashCode = dim3.GetHashCode();
                var equals = dim3.Equals(dim3);

                Assert.Equal(123, x);
                Assert.Equal(456, y);
                Assert.Equal(789, z);
                Assert.True(equals);
            }));
        }

        System.Threading.Tasks.Task.WaitAll([.. tasks]);

        // Assert - No exceptions should be thrown
        _ = tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    #endregion
}
