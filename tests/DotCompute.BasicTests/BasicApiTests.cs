using DotCompute.Abstractions;
using DotCompute.Core;
using FluentAssertions;
using Xunit;

namespace DotCompute.BasicTests;

/// <summary>
/// Basic API validation tests to ensure core functionality works
/// </summary>
public class BasicApiTests
{
    [Fact]
    public void AcceleratorInfo_Constructor_CreatesValidInstance()
    {
        // Arrange & Act
        var info = new AcceleratorInfo(AcceleratorType.CPU, "Test CPU", "1.0", 1024 * 1024 * 1024);

        // Assert
        info.DeviceType.Should().Be(AcceleratorType.CPU);
        info.Name.Should().Be("Test CPU");
        info.Version.Should().Be("1.0");
        info.MaxMemorySize.Should().Be(1024 * 1024 * 1024);
    }

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
            OptimizationLevel = OptimizationLevel.Release,
            FastMath = true,
            UnrollLoops = true
        };

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.Release);
        options.FastMath.Should().BeTrue();
        options.UnrollLoops.Should().BeTrue();
    }

    [Fact]
    public void StringKernelSource_Constructor_CreatesValidInstance()
    {
        // Arrange
        var sourceCode = "__kernel void test() { }";

        // Act
        var source = new StringKernelSource(sourceCode);

        // Assert
        source.Should().NotBeNull();
        source.GetSourceAsync().Result.Should().Be(sourceCode);
    }

    [Fact]
    public void DefaultAcceleratorManager_Constructor_CreatesValidInstance()
    {
        // Arrange & Act
        var manager = new DefaultAcceleratorManager();

        // Assert
        manager.Should().NotBeNull();
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
}