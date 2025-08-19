using Xunit;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests;

public sealed class BasicTests
{
    [Fact]
    public void Core_Assembly_Loads()
    {
        var assembly = typeof(IAccelerator).Assembly;
        Assert.NotNull(assembly);
        Assert.Contains("DotCompute.Core", assembly.FullName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(10, 20, 30)]
    [InlineData(-5, 5, 0)]
    public void Basic_Math_Works(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Platform_Detection_Works()
    {
        var platform = Environment.OSVersion.Platform;
        Assert.True(
            platform is PlatformID.Unix or
            PlatformID.Win32NT or
            PlatformID.MacOSX or
            PlatformID.Other
        );
    }
}
