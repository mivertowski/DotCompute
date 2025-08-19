using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

public sealed class KernelArgumentsTests
{
    [Fact]
    public void Create_WithCapacity_ShouldInitializeWithCorrectLength()
    {
        // Act
        var args = KernelArguments.Create(5);

        // Assert
        args.Length.Should().Be(5);
    }

    [Fact]
    public void Set_And_Get_WithValidIndex_ShouldStoreAndRetrieveValue()
    {
        // Arrange
        var args = KernelArguments.Create(3);
        var testValue = 42;
        var testString = "test";
        var testArray = new float[] { 1.0f, 2.0f, 3.0f };

        // Act
        args.Set(0, testValue);
        args.Set(1, testString);
        args.Set(2, testArray);

        // Assert
        args.Get(0).Should().Be(testValue);
        args.Get(1).Should().Be(testString);
        args.Get(2).Should().BeEquivalentTo(testArray);
    }

    [Fact]
    public void Get_WithInvalidIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var args = KernelArguments.Create(2);

        // Act & Assert
        var act = () => args.Get(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Fact]
    public void Get_WithoutInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var args = new KernelArguments();

        // Act & Assert
        var act = () => args.Get(0);
        Assert.Throws<InvalidOperationException>(() => act());
    }

    [Fact]
    public void Length_WithoutInitialization_ShouldReturnZero()
    {
        // Arrange
        var args = new KernelArguments();

        // Assert
        args.Length.Should().Be(0);
    }

    [Fact]
    public void Set_WithNullValue_ShouldStoreNull()
    {
        // Arrange
        var args = KernelArguments.Create(1);

        // Act
        args.Set(0, null!);

        // Assert
        args.Get(0).Should().BeNull();
    }

    [Fact]
    public void Set_WithDifferentTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var args = KernelArguments.Create(6);

        // Act
        args.Set(0, (byte)255);
        args.Set(1, (short)32767);
        args.Set(2, 2147483647);
        args.Set(3, 9223372036854775807L);
        args.Set(4, 3.14f);
        args.Set(5, 3.14159265359);

        // Assert
        args.Get(0).Should().Be((byte)255);
        args.Get(1).Should().Be((short)32767);
        args.Get(2).Should().Be(2147483647);
        args.Get(3).Should().Be(9223372036854775807L);
        args.Get(4).Should().Be(3.14f);
        args.Get(5).Should().Be(3.14159265359);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Create_WithVariousCapacities_ShouldWorkCorrectly(int capacity)
    {
        // Act
        var args = KernelArguments.Create(capacity);

        // Assert
        args.Length.Should().Be(capacity);
    }

    [Fact]
    public void Set_WithGenericMethod_ShouldInferType()
    {
        // Arrange
        var args = KernelArguments.Create(3);
        var intValue = 42;
        var floatValue = 3.14f;
        var stringValue = "test";

        // Act
        args.Set(0, intValue);
        args.Set(1, floatValue);
        args.Set(2, stringValue);

        // Assert
        args.Get(0).Should().Be(intValue);
        args.Get(1).Should().Be(floatValue);
        args.Get(2).Should().Be(stringValue);
    }
}
}
