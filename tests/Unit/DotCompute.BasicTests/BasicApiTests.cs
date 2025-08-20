using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

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
        Assert.Contains(AcceleratorType.CPU, values);
        Assert.Contains(AcceleratorType.CUDA, values);
        Assert.Contains(AcceleratorType.Metal, values);
    }

    [Fact]
    public void KernelArguments_Create_ReturnsValidInstance()
    {
        // Arrange & Act
        var args = KernelArguments.Create(3);

        // Assert
        _ = args.Length.Should().Be(3);
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
        Assert.Equal(testValue, retrieved);
    }

    [Fact]
    public void MemoryOptions_Enum_HasExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<MemoryOptions>();

        // Assert
        Assert.Contains(MemoryOptions.None, values);
        Assert.Contains(MemoryOptions.ReadOnly, values);
        Assert.Contains(MemoryOptions.WriteOnly, values);
        Assert.Contains(MemoryOptions.HostVisible, values);
        Assert.Contains(MemoryOptions.Cached, values);
        Assert.Contains(MemoryOptions.Atomic, values);
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
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.None);
        _ = options.FastMath.Should().BeTrue();
        _ = options.UnrollLoops.Should().BeTrue();
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
        _ = result.Should().NotBeNullOrEmpty();
        _ = result.Should().Be(type.ToString());
    }

    [Fact]
    public void MemoryOptions_FlagCombinations_WorkCorrectly()
    {
        // Arrange
        var combined = MemoryOptions.ReadOnly | MemoryOptions.HostVisible;

        // Act & Assert
        _ = combined.Should().HaveFlag(MemoryOptions.ReadOnly);
        _ = combined.Should().HaveFlag(MemoryOptions.HostVisible);
        _ = combined.Should().NotHaveFlag(MemoryOptions.WriteOnly);
    }

    [Fact]
    public void OptimizationLevel_Enum_HasExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<OptimizationLevel>();

        // Assert
        Assert.Contains(OptimizationLevel.None, values);
        Assert.Contains(OptimizationLevel.Default, values);
        _ = (values.Length > 1).Should().BeTrue();
    }

    [Fact]
    public void KernelArguments_Empty_HandlesCorrectly()
    {
        // Arrange & Act
        var args = KernelArguments.Create(0);

        // Assert
        _ = args.Length.Should().Be(0);
    }

    [Fact]
    public void KernelArguments_MultipleValues_WorkCorrectly()
    {
        // Arrange
        var args = KernelArguments.Create(3);
        var values = new object[] { 42, 3.14f, "test" };

        // Act
        for (var i = 0; i < values.Length; i++)
        {
            args.Set(i, values[i]);
        }

        // Assert
        for (var i = 0; i < values.Length; i++)
        {
            _ = args.Get(i).Should().Be(values[i]);
        }
    }
}
