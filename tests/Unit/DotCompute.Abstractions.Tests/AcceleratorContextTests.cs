// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for the AcceleratorContext struct.
/// </summary>
public sealed class AcceleratorContextTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var handle = new IntPtr(0x12345678);
        const int deviceId = 42;

        // Act
        var context = new AcceleratorContext(handle, deviceId);

        // Assert
        _ = context.Handle.Should().Be(handle);
        _ = context.DeviceId.Should().Be(deviceId);
        _ = context.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroHandle_ShouldCreateInvalidContext()
    {
        // Arrange & Act
        var context = new AcceleratorContext(IntPtr.Zero, 0);

        // Assert
        _ = context.Handle.Should().Be(IntPtr.Zero);
        _ = context.DeviceId.Should().Be(0);
        _ = context.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNegativeDeviceId_ShouldStillWork()
    {
        // Arrange
        var handle = new IntPtr(0x1000);
        const int negativeDeviceId = -1;

        // Act
        var context = new AcceleratorContext(handle, negativeDeviceId);

        // Assert
        _ = context.Handle.Should().Be(handle);
        _ = context.DeviceId.Should().Be(negativeDeviceId);
        _ = context.IsValid.Should().BeTrue(); // Valid because handle is non-zero
    }

    [Theory]
    [InlineData(0x1000, 0)]
    [InlineData(0x2000, 1)]
    [InlineData(0xFFFF, int.MaxValue)]
    [InlineData(long.MaxValue, int.MinValue)]
    public void Constructor_WithVariousValues_ShouldInitializeCorrectly(long handleValue, int deviceId)
    {
        // Arrange
        var handle = new IntPtr(handleValue);

        // Act
        var context = new AcceleratorContext(handle, deviceId);

        // Assert
        _ = context.Handle.Should().Be(handle);
        _ = context.DeviceId.Should().Be(deviceId);
        _ = context.IsValid.Should().Be(handle != IntPtr.Zero);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Handle_ShouldReturnCorrectValue()
    {
        // Arrange
        var expectedHandle = new IntPtr(0xDEADBEEF);
        var context = new AcceleratorContext(expectedHandle, 1);

        // Act
        var actualHandle = context.Handle;

        // Assert
        Assert.Equal(expectedHandle, actualHandle);
    }

    [Fact]
    public void DeviceId_ShouldReturnCorrectValue()
    {
        // Arrange
        const int expectedDeviceId = 123;
        var context = new AcceleratorContext(new IntPtr(0x1000), expectedDeviceId);

        // Act
        var actualDeviceId = context.DeviceId;

        // Assert
        Assert.Equal(expectedDeviceId, actualDeviceId);
    }

    [Fact]
    public void IsValid_WithNonZeroHandle_ShouldReturnTrue()
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(1), 0);

        // Act
        var isValid = context.IsValid;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WithZeroHandle_ShouldReturnFalse()
    {
        // Arrange
        var context = new AcceleratorContext(IntPtr.Zero, 0);

        // Act
        var isValid = context.IsValid;

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0x1000)]
    [InlineData(0xFFFFFFFF)]
    [InlineData(long.MaxValue)]
    public void IsValid_WithVariousNonZeroHandles_ShouldReturnTrue(long handleValue)
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(handleValue), 0);

        // Act
        var isValid = context.IsValid;

        // Assert
        Assert.True(isValid);
    }

    #endregion

    #region Static Invalid Tests

    [Fact]
    public void Invalid_ShouldReturnInvalidContext()
    {
        // Act
        var invalidContext = AcceleratorContext.Invalid;

        // Assert
        _ = invalidContext.Handle.Should().Be(IntPtr.Zero);
        _ = invalidContext.DeviceId.Should().Be(-1);
        _ = invalidContext.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_ShouldReturnSameInstanceEachTime()
    {
        // Act
        var invalid1 = AcceleratorContext.Invalid;
        var invalid2 = AcceleratorContext.Invalid;

        // Assert
        Assert.Equal(invalid2, invalid1);
        _ = invalid1.Equals(invalid2).Should().BeTrue();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(0x1000), 5);

        // Act & Assert
        _ = context.Equals(context).Should().BeTrue();
        _ = context.Equals(context).Should().BeTrue();
        _ = (!context.Equals(null)).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var handle = new IntPtr(0x2000);
        const int deviceId = 10;
        var context1 = new AcceleratorContext(handle, deviceId);
        var context2 = new AcceleratorContext(handle, deviceId);

        // Act & Assert
        _ = context1.Equals(context2).Should().BeTrue();
        _ = (context1 == context2).Should().BeTrue();
        _ = (context1 != context2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentHandles_ShouldReturnFalse()
    {
        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x2000), 5);

        // Act & Assert
        _ = context1.Equals(context2).Should().BeFalse();
        _ = (context1 == context2).Should().BeFalse();
        _ = (context1 != context2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentDeviceIds_ShouldReturnFalse()
    {
        // Arrange
        var handle = new IntPtr(0x1000);
        var context1 = new AcceleratorContext(handle, 5);
        var context2 = new AcceleratorContext(handle, 10);

        // Act & Assert
        _ = context1.Equals(context2).Should().BeFalse();
        _ = (context1 == context2).Should().BeFalse();
        _ = (context1 != context2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentHandlesAndDeviceIds_ShouldReturnFalse()
    {
        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x2000), 10);

        // Act & Assert
        _ = context1.Equals(context2).Should().BeFalse();
        _ = (context1 == context2).Should().BeFalse();
        _ = (context1 != context2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(0x1000), 5);

        // Act & Assert
        _ = context.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(0x1000), 5);
        var otherObject = "not a context";

        // Act & Assert
        _ = context.Equals(otherObject).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithBoxedValue_ShouldReturnCorrectResult()
    {
        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x1000), 5);
        object boxedContext2 = context2;

        // Act & Assert
        _ = context1.Equals(boxedContext2).Should().BeTrue();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_WithSameInstance_ShouldReturnSameHashCode()
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(0x1000), 5);

        // Act
        var hashCode1 = context.GetHashCode();
        var hashCode2 = context.GetHashCode();

        // Assert
        Assert.Equal(hashCode2, hashCode1);
    }

    [Fact]
    public void GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var handle = new IntPtr(0x2000);
        const int deviceId = 10;
        var context1 = new AcceleratorContext(handle, deviceId);
        var context2 = new AcceleratorContext(handle, deviceId);

        // Act
        var hashCode1 = context1.GetHashCode();
        var hashCode2 = context2.GetHashCode();

        // Assert
        Assert.Equal(hashCode2, hashCode1);
    }

    [Fact]
    public void GetHashCode_WithDifferentInstances_ShouldReturnDifferentHashCodes()
    {
        // Note: Hash codes aren't guaranteed to be different, but they should be different 
        // most of the time for good distribution

        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x2000), 10);

        // Act
        var hashCode1 = context1.GetHashCode();
        var hashCode2 = context2.GetHashCode();

        // Assert
        _ = hashCode1.Should().NotBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_WithOnlyHandleDifferent_ShouldReturnDifferentHashCodes()
    {
        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x2000), 5);

        // Act
        var hashCode1 = context1.GetHashCode();
        var hashCode2 = context2.GetHashCode();

        // Assert
        _ = hashCode1.Should().NotBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_WithOnlyDeviceIdDifferent_ShouldReturnDifferentHashCodes()
    {
        // Arrange
        var handle = new IntPtr(0x1000);
        var context1 = new AcceleratorContext(handle, 5);
        var context2 = new AcceleratorContext(handle, 10);

        // Act
        var hashCode1 = context1.GetHashCode();
        var hashCode2 = context2.GetHashCode();

        // Assert
        _ = hashCode1.Should().NotBe(hashCode2);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithValidContext_ShouldReturnFormattedString()
    {
        // Arrange
        var handle = new IntPtr(0x1234);
        const int deviceId = 42;
        var context = new AcceleratorContext(handle, deviceId);

        // Act
        var result = context.ToString();

        // Assert
        _ = result.Should().Be("AcceleratorContext(Device=42, Handle=1234)");
    }

    [Fact]
    public void ToString_WithInvalidContext_ShouldReturnInvalidString()
    {
        // Arrange
        var context = AcceleratorContext.Invalid;

        // Act
        var result = context.ToString();

        // Assert
        _ = result.Should().Be("AcceleratorContext(Invalid)");
    }

    [Fact]
    public void ToString_WithZeroHandleValidDeviceId_ShouldReturnInvalidString()
    {
        // Arrange
        var context = new AcceleratorContext(IntPtr.Zero, 5);

        // Act
        var result = context.ToString();

        // Assert
        _ = result.Should().Be("AcceleratorContext(Invalid)");
    }

    [Fact]
    public void ToString_WithLargeHandle_ShouldFormatCorrectly()
    {
        // Arrange
        var handle = new IntPtr(0xDEADBEEF);
        const int deviceId = 0;
        var context = new AcceleratorContext(handle, deviceId);

        // Act
        var result = context.ToString();

        // Assert
        _ = result.Should().Be("AcceleratorContext(Device=0, Handle=DEADBEEF)");
    }

    [Theory]
    [InlineData(0x0, 0, "AcceleratorContext(Invalid)")] // Zero handle = invalid
    [InlineData(0x1, 0, "AcceleratorContext(Device=0, Handle=1)")]
    [InlineData(0xABC, 123, "AcceleratorContext(Device=123, Handle=ABC)")]
    [InlineData(0x1000, -1, "AcceleratorContext(Device=-1, Handle=1000)")]
    public void ToString_WithVariousValues_ShouldFormatCorrectly(long handleValue, int deviceId, string expected)
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(handleValue), deviceId);

        // Act
        var result = context.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region IEquatable Implementation Tests

    [Fact]
    public void AcceleratorContext_ShouldImplementIEquatable()
    {
        // Arrange & Act
        var type = typeof(AcceleratorContext);

        // Assert
        _ = type.Should().BeAssignableTo<IEquatable<AcceleratorContext>>();
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void Constructor_WithMaxValues_ShouldWork()
    {
        // Arrange & Act
        var context = new AcceleratorContext(new IntPtr(long.MaxValue), int.MaxValue);

        // Assert
        _ = context.Handle.Should().Be(new IntPtr(long.MaxValue));
        _ = context.DeviceId.Should().Be(int.MaxValue);
        _ = context.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithMinValues_ShouldWork()
    {
        // Arrange & Act
        var context = new AcceleratorContext(new IntPtr(long.MinValue), int.MinValue);

        // Assert
        _ = context.Handle.Should().Be(new IntPtr(long.MinValue));
        _ = context.DeviceId.Should().Be(int.MinValue);
        _ = context.IsValid.Should().BeTrue(); // Non-zero handle
    }

    [Fact]
    public void Equality_WithManyDifferentContexts_ShouldWorkCorrectly()
    {
        // Arrange
        var contexts = new[]
        {
        new AcceleratorContext(new IntPtr(0x1000), 1),
        new AcceleratorContext(new IntPtr(0x2000), 1),
        new AcceleratorContext(new IntPtr(0x1000), 2),
        new AcceleratorContext(new IntPtr(0x2000), 2),
        AcceleratorContext.Invalid
    };

        // Act & Assert - Each context should only equal itself
        for (var i = 0; i < contexts.Length; i++)
        {
            for (var j = 0; j < contexts.Length; j++)
            {
                if (i == j)
                {
                    _ = contexts[i].Should().Be(contexts[j]);
                }
                else
                {
                    _ = contexts[i].Should().NotBe(contexts[j]);
                }
            }
        }
    }

    [Fact]
    public void IsValid_EdgeCases_ShouldWorkCorrectly()
    {
        // Test various edge cases for IsValid property
        var testCases = new[]
        {
       (IntPtr.Zero, false),
       (new IntPtr(1), true),
       (new IntPtr(-1), true),
       (new IntPtr(long.MaxValue), true),
       (new IntPtr(long.MinValue), true)
    };

        foreach (var (handle, expectedValid) in testCases)
        {
            var context = new AcceleratorContext(handle, 0);
            _ = context.IsValid.Should().Be(expectedValid); // Handle validity check
        }
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void AcceleratorContext_PropertyAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var context = new AcceleratorContext(new IntPtr(0x12345678), 42);
        var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

        // Act - Multiple threads accessing properties
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var handle = context.Handle;
                var deviceId = context.DeviceId;
                var isValid = context.IsValid;
                var toString = context.ToString();
                var hashCode = context.GetHashCode();

                _ = handle.Should().Be(new IntPtr(0x12345678));
                Assert.Equal(42, deviceId);
                Assert.True(isValid);
                _ = toString.Should().Be("AcceleratorContext(Device=42, Handle=12345678)");
                _ = hashCode.Should().Be(context.GetHashCode());
            }));
        }

        System.Threading.Tasks.Task.WaitAll([.. tasks]);

        // Assert - No exceptions should be thrown
        _ = tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void EqualityOperator_ShouldWorkCorrectly()
    {
        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context3 = new AcceleratorContext(new IntPtr(0x2000), 5);

        // Act & Assert
        _ = (context1 == context2).Should().BeTrue();
        _ = (context1 == context3).Should().BeFalse();
#pragma warning disable CS1718 // Comparison made to same variable
        _ = (context1 == context1).Should().BeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void InequalityOperator_ShouldWorkCorrectly()
    {
        // Arrange
        var context1 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context2 = new AcceleratorContext(new IntPtr(0x1000), 5);
        var context3 = new AcceleratorContext(new IntPtr(0x2000), 5);

        // Act & Assert
        _ = (context1 != context2).Should().BeFalse();
        _ = (context1 != context3).Should().BeTrue();
#pragma warning disable CS1718 // Comparison made to same variable
        _ = (context1 != context1).Should().BeFalse();
#pragma warning restore CS1718
    }

    #endregion
}
