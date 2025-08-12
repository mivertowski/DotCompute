using DotCompute.Core.Types;
using DotCompute.Core.Memory;
using Xunit;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for Core memory types and components.
/// </summary>
public class CoreMemoryTypesTests
{
    [Fact]
    public void MemoryType_EnumValues_ShouldBeCorrect()
    {
        Assert.True(Enum.IsDefined(typeof(MemoryType), MemoryType.DeviceLocal));
        Assert.True(Enum.IsDefined(typeof(MemoryType), MemoryType.HostVisible));
        Assert.True(Enum.IsDefined(typeof(MemoryType), MemoryType.Shared));
    }

    [Theory]
    [InlineData(MemoryType.DeviceLocal)]
    [InlineData(MemoryType.HostVisible)]
    [InlineData(MemoryType.Shared)]
    public void MemoryType_AllValues_ShouldBeValid(MemoryType memoryType)
    {
        // Test that all enum values are defined
        Assert.True(Enum.IsDefined(typeof(MemoryType), memoryType));
        
        // Test that we can convert to string and back
        var stringValue = memoryType.ToString();
        Assert.NotNull(stringValue);
        Assert.NotEmpty(stringValue);
        
        var parsedValue = Enum.Parse<MemoryType>(stringValue);
        Assert.Equal(memoryType, parsedValue);
    }

    [Fact]
    public void MemoryType_DeviceLocal_ShouldHaveCorrectValue()
    {
        Assert.Equal(0, (int)MemoryType.DeviceLocal);
    }

    [Fact]
    public void MemoryType_HostVisible_ShouldHaveCorrectValue()
    {
        Assert.Equal(1, (int)MemoryType.HostVisible);
    }

    [Fact]
    public void MemoryType_Shared_ShouldHaveCorrectValue()
    {
        Assert.Equal(2, (int)MemoryType.Shared);
    }

    [Fact]
    public void MemoryType_ToString_ShouldReturnCorrectStrings()
    {
        Assert.Equal("DeviceLocal", MemoryType.DeviceLocal.ToString());
        Assert.Equal("HostVisible", MemoryType.HostVisible.ToString());
        Assert.Equal("Shared", MemoryType.Shared.ToString());
    }

    [Fact]
    public void MemoryType_Parse_ShouldWorkCorrectly()
    {
        Assert.Equal(MemoryType.DeviceLocal, Enum.Parse<MemoryType>("DeviceLocal"));
        Assert.Equal(MemoryType.HostVisible, Enum.Parse<MemoryType>("HostVisible"));
        Assert.Equal(MemoryType.Shared, Enum.Parse<MemoryType>("Shared"));
    }

    [Fact]
    public void MemoryType_Parse_IgnoreCase_ShouldWorkCorrectly()
    {
        Assert.Equal(MemoryType.DeviceLocal, Enum.Parse<MemoryType>("devicelocal", true));
        Assert.Equal(MemoryType.HostVisible, Enum.Parse<MemoryType>("HOSTVISIBLE", true));
        Assert.Equal(MemoryType.Shared, Enum.Parse<MemoryType>("shared", true));
    }

    [Fact]
    public void MemoryType_TryParse_ValidValues_ShouldReturnTrue()
    {
        Assert.True(Enum.TryParse<MemoryType>("DeviceLocal", out var result1));
        Assert.Equal(MemoryType.DeviceLocal, result1);
        
        Assert.True(Enum.TryParse<MemoryType>("HostVisible", out var result2));
        Assert.Equal(MemoryType.HostVisible, result2);
        
        Assert.True(Enum.TryParse<MemoryType>("Shared", out var result3));
        Assert.Equal(MemoryType.Shared, result3);
    }

    [Fact]
    public void MemoryType_TryParse_InvalidValue_ShouldReturnFalse()
    {
        Assert.False(Enum.TryParse<MemoryType>("InvalidMemoryType", out var result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void MemoryType_GetValues_ShouldReturnAllValues()
    {
        var values = Enum.GetValues<MemoryType>();
        
        Assert.Equal(3, values.Length);
        Assert.Contains(MemoryType.DeviceLocal, values);
        Assert.Contains(MemoryType.HostVisible, values);
        Assert.Contains(MemoryType.Shared, values);
    }

    [Fact]
    public void MemoryType_GetNames_ShouldReturnCorrectNames()
    {
        var names = Enum.GetNames<MemoryType>();
        
        Assert.Equal(3, names.Length);
        Assert.Contains("DeviceLocal", names);
        Assert.Contains("HostVisible", names);
        Assert.Contains("Shared", names);
    }

    [Fact]
    public void MemoryType_Comparison_ShouldWorkCorrectly()
    {
        Assert.True(MemoryType.DeviceLocal < MemoryType.HostVisible);
        Assert.True(MemoryType.HostVisible < MemoryType.Shared);
        Assert.True(MemoryType.DeviceLocal < MemoryType.Shared);
    }

    [Fact]
    public void MemoryType_Equality_ShouldWorkCorrectly()
    {
        var deviceLocal1 = MemoryType.DeviceLocal;
        var deviceLocal2 = MemoryType.DeviceLocal;
        var hostVisible = MemoryType.HostVisible;
        var shared = MemoryType.Shared;
        
        Assert.True(deviceLocal1 == deviceLocal2);
        Assert.False(deviceLocal1 == hostVisible);
        Assert.False(hostVisible == shared);
    }
}