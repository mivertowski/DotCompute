using DotCompute.Abstractions;
using FluentAssertions;
using Xunit;

namespace DotCompute.BasicTests;

/// <summary>
/// Basic API validation tests to ensure core functionality works
/// </summary>
public class BasicApiTests
{
    [Fact]
    public void AcceleratorType_Enum_HasExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<AcceleratorType>();

        // Assert
        values.Should().Contain(AcceleratorType.CPU);
        values.Should().Contain(AcceleratorType.CUDA);
        values.Should().Contain(AcceleratorType.Metal);
    }

    [Fact]
    public void KernelArguments_Create_ReturnsValidInstance()
    {
        // Arrange & Act
        var args = KernelArguments.Create(3);

        // Assert
        args.Length.Should().Be(3);
    }

    [Fact]
    public void KernelArguments_Set_And_Get_WorksCorrectly()
    {
        // Arrange
        var args = KernelArguments.Create(2);
        var testValue = 42;

        // Act
        args.Set(0, testValue);
        var retrieved = args.Get(0);

        // Assert
        retrieved.Should().Be(testValue);
    }

    [Fact]
    public void MemoryOptions_Enum_HasExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<MemoryOptions>();

        // Assert
        values.Should().Contain(MemoryOptions.None);
        values.Should().Contain(MemoryOptions.ReadOnly);
        values.Should().Contain(MemoryOptions.WriteOnly);
        values.Should().Contain(MemoryOptions.HostVisible);
        values.Should().Contain(MemoryOptions.Cached);
        values.Should().Contain(MemoryOptions.Atomic);
    }

    [Fact]
    public void CompilationOptions_Constructor_CreatesValidInstance()
    {
        // Arrange & Act
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.None,
            FastMath = true,
            UnrollLoops = true
        };

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.None);
        options.FastMath.Should().BeTrue();
        options.UnrollLoops.Should().BeTrue();
    }

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.CUDA)]
    [InlineData(AcceleratorType.Metal)]
    public void AcceleratorType_ToString_ReturnsExpectedString(AcceleratorType type)
    {
        // Arrange & Act
        var result = type.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(type.ToString());
    }

    [Fact]
    public void MemoryOptions_FlagCombinations_WorkCorrectly()
    {
        // Arrange
        var combined = MemoryOptions.ReadOnly | MemoryOptions.HostVisible;

        // Act & Assert
        combined.Should().HaveFlag(MemoryOptions.ReadOnly);
        combined.Should().HaveFlag(MemoryOptions.HostVisible);
        combined.Should().NotHaveFlag(MemoryOptions.WriteOnly);
    }

    [Fact]
    public void OptimizationLevel_Enum_HasExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<OptimizationLevel>();

        // Assert
        values.Should().Contain(OptimizationLevel.None);
        values.Should().Contain(OptimizationLevel.Default);
        values.Length.Should().BeGreaterThan(1);
    }

    [Fact]
    public void KernelArguments_Empty_HandlesCorrectly()
    {
        // Arrange & Act
        var args = KernelArguments.Create(0);

        // Assert
        args.Length.Should().Be(0);
    }

    [Fact]
    public void KernelArguments_MultipleValues_WorkCorrectly()
    {
        // Arrange
        var args = KernelArguments.Create(3);
        var values = new object[] { 42, 3.14f, "test" };

        // Act
        for (int i = 0; i < values.Length; i++)
        {
            args.Set(i, values[i]);
        }

        // Assert
        for (int i = 0; i < values.Length; i++)
        {
            args.Get(i).Should().Be(values[i]);
        }
    }
}