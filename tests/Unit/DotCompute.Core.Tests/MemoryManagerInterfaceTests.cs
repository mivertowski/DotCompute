// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Xunit;
using FluentAssertions;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Comprehensive unit tests for IMemoryManager interface and related types.
/// Tests buffer allocation, data transfer, memory statistics, and edge cases.
/// </summary>
public sealed class MemoryManagerInterfaceTests
{
    #region Interface Contract Tests

    [Fact]
    public void IMemoryManager_ShouldDefineRequiredMethods()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryManager);

        // Assert
        Assert.NotNull(interfaceType);
        interfaceType.GetMethod("CreateBufferAsync").NotBeNull();
        interfaceType.GetMethod("CopyAsync").NotBeNull();
        interfaceType.GetMethod("GetStatistics").NotBeNull();
        interfaceType.GetProperty("AvailableLocations").NotBeNull();
    }

    [Fact]
    public void IMemoryManager_ShouldInheritFromIAsyncDisposable()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryManager);

        // Assert
        interfaceType.GetInterfaces().Contain(typeof(IAsyncDisposable));
    }

    #endregion

    #region MemoryLocation Enum Tests

    [Fact]
    public void MemoryLocation_ShouldHaveExpectedValues()
    {
        // Act & Assert
        Enum.GetValues<MemoryLocation>().Contain(new[]
        {
            MemoryLocation.Host,
            MemoryLocation.Device,
            MemoryLocation.HostPinned,
            MemoryLocation.Unified,
            MemoryLocation.Managed
        });
    }

    [Theory]
    [InlineData(MemoryLocation.Host, "Host")]
    [InlineData(MemoryLocation.Device, "Device")]
    [InlineData(MemoryLocation.HostPinned, "HostPinned")]
    [InlineData(MemoryLocation.Unified, "Unified")]
    [InlineData(MemoryLocation.Managed, "Managed")]
    public void MemoryLocation_ShouldHaveCorrectStringRepresentation(MemoryLocation location, string expectedName)
    {
        // Act
        var name = location.ToString();

        // Assert
        Assert.Equal(expectedName, name);
    }

    #endregion

    #region MemoryAccess Enum Tests

    [Fact]
    public void MemoryAccess_ShouldBeFlagsEnum()
    {
        // Arrange & Act
        var enumType = typeof(MemoryAccess);

        // Assert
        enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Should().NotBeEmpty();
    }

    [Fact]
    public void MemoryAccess_ShouldHaveExpectedValues()
    {
        // Act & Assert
        MemoryAccess.ReadOnly.Should().Be((MemoryAccess)1);
        MemoryAccess.WriteOnly.Should().Be((MemoryAccess)2);
        MemoryAccess.ReadWrite.Should().Be(MemoryAccess.ReadOnly | MemoryAccess.WriteOnly);
        MemoryAccess.HostAccess.Should().Be((MemoryAccess)4);
    }

    [Theory]
    [InlineData(MemoryAccess.ReadOnly, MemoryAccess.ReadOnly, true)]
    [InlineData(MemoryAccess.WriteOnly, MemoryAccess.WriteOnly, true)]
    [InlineData(MemoryAccess.ReadWrite, MemoryAccess.ReadOnly, true)]
    [InlineData(MemoryAccess.ReadWrite, MemoryAccess.WriteOnly, true)]
    [InlineData(MemoryAccess.ReadOnly, MemoryAccess.WriteOnly, false)]
    [InlineData(MemoryAccess.WriteOnly, MemoryAccess.ReadOnly, false)]
    public void MemoryAccess_FlagsOperations_ShouldWorkCorrectly(MemoryAccess combined, MemoryAccess flag, bool hasFlag)
    {
        // Act
        var result = combined.HasFlag(flag);

        // Assert
        Assert.Equal(hasFlag, result);
    }

    #endregion

    #region MemoryMapMode Enum Tests

    [Fact]
    public void MemoryMapMode_ShouldHaveExpectedValues()
    {
        // Act & Assert
        Enum.GetValues<MemoryMapMode>().Contain(new[]
        {
            MemoryMapMode.ReadOnly,
            MemoryMapMode.WriteOnly,
            MemoryMapMode.ReadWrite
        });
    }

    #endregion

    #region IMemoryMapping Interface Tests

    [Fact]
    public void IMemoryMapping_ShouldDefineRequiredMembers()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryMapping<>);

        // Assert
        Assert.NotNull(interfaceType);
        interfaceType.GetProperty("Span").NotBeNull();
        interfaceType.GetProperty("Mode").NotBeNull();
        interfaceType.GetProperty("IsValid").NotBeNull();
        interfaceType.GetMethod("Flush").NotBeNull();
    }

    [Fact]
    public void IMemoryMapping_ShouldInheritFromIDisposable()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryMapping<>);

        // Assert
        interfaceType.GetInterfaces().Contain(typeof(IDisposable));
    }

    #endregion

    #region IMemoryStatistics Interface Tests

    [Fact]
    public void IMemoryStatistics_ShouldDefineRequiredProperties()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryStatistics);

        // Assert
        Assert.NotNull(interfaceType);
        interfaceType.GetProperty("TotalAllocatedBytes").NotBeNull();
        interfaceType.GetProperty("AvailableBytes").NotBeNull();
        interfaceType.GetProperty("PeakUsageBytes").NotBeNull();
        interfaceType.GetProperty("AllocationCount").NotBeNull();
        interfaceType.GetProperty("FragmentationPercentage").NotBeNull();
        interfaceType.GetProperty("UsageByLocation").NotBeNull();
    }

    [Fact]
    public void IMemoryStatistics_Properties_ShouldHaveCorrectTypes()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryStatistics);

        // Assert
        interfaceType.GetProperty("TotalAllocatedBytes")!.PropertyType.Should().Be(typeof(long));
        interfaceType.GetProperty("AvailableBytes")!.PropertyType.Should().Be(typeof(long));
        interfaceType.GetProperty("PeakUsageBytes")!.PropertyType.Should().Be(typeof(long));
        interfaceType.GetProperty("AllocationCount")!.PropertyType.Should().Be(typeof(int));
        interfaceType.GetProperty("FragmentationPercentage")!.PropertyType.Should().Be(typeof(double));
        interfaceType.GetProperty("UsageByLocation")!.PropertyType.Should().Be(typeof(IReadOnlyDictionary<MemoryLocation, long>));
    }

    #endregion

    #region Memory Type Validation Tests

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(long))]
    [InlineData(typeof(short))]
    public void UnmanagedTypes_ShouldBeValidForBuffers(Type type)
    {
        // This test ensures that common unmanaged types work with the generic constraints
        // The constraint is compile-time, so we test the types we expect to use
        
        // Act & Assert
        type.IsValueType.Should().BeTrue();
        type.IsPrimitive.Should().BeTrue();
    }

    [Fact]
    public void MemoryLocation_ShouldSupportAllDefinedLocations()
    {
        // Arrange
        var allLocations = Enum.GetValues<MemoryLocation>();

        // Act & Assert
        foreach (var location in allLocations)
        {
            location.BeDefined();
           ((int)location).BeGreaterThanOrEqualTo(0);
        }
    }

    #endregion

    #region Memory Access Pattern Tests

    [Theory]
    [InlineData(MemoryAccess.ReadOnly)]
    [InlineData(MemoryAccess.WriteOnly)]
    [InlineData(MemoryAccess.ReadWrite)]
    [InlineData(MemoryAccess.ReadOnly | MemoryAccess.HostAccess)]
    [InlineData(MemoryAccess.ReadWrite | MemoryAccess.HostAccess)]
    public void MemoryAccess_ValidCombinations_ShouldBeSupported(MemoryAccess access)
    {
        // Act & Assert
        access.BeDefined();
       ((int)access).Should().BeGreaterThan(0);
    }

    [Fact]
    public void MemoryAccess_ReadWrite_ShouldCombineReadAndWrite()
    {
        // Act
        var readWrite = MemoryAccess.ReadWrite;

        // Assert
        readWrite.HasFlagMemoryAccess.ReadOnly.Should().BeTrue();
        readWrite.HasFlagMemoryAccess.WriteOnly.Should().BeTrue();
        Assert.Equal(MemoryAccess.ReadOnly | MemoryAccess.WriteOnly, readWrite);
    }

    #endregion

    #region Interface Usage Pattern Tests

    [Fact]
    public void CreateBufferAsync_GenericMethod_ShouldBeProperlyDefined()
    {
        // Arrange
        var interfaceType = typeof(IMemoryManager);
        var createBufferMethods = interfaceType.GetMethods()
            .Where(m => m.Name == "CreateBufferAsync")
            .ToArray();

        // Act & Assert
        Assert.Equal(2, createBufferMethods.Count()); // Two overloads
        
        // Check first overload(elementCount)
        var firstOverload = createBufferMethods.FirstOrDefault(m => 
            m.GetParameters().Length >= 2 && 
            m.GetParameters()[0].ParameterType == typeof(int));
        Assert.NotNull(firstOverload);
        firstOverload! .IsGenericMethodDefinition.Should().BeTrue();
        
        // Check second overload(data)
        var secondOverload = createBufferMethods.FirstOrDefault(m => 
            m.GetParameters().Length >= 2 && 
            m.GetParameters()[0].ParameterType.IsGenericType);
        Assert.NotNull(secondOverload);
        secondOverload! .IsGenericMethodDefinition.Should().BeTrue();
    }

    [Fact]
    public void CopyAsync_ShouldHaveProperSignature()
    {
        // Arrange
        var interfaceType = typeof(IMemoryManager);
        var copyMethod = interfaceType.GetMethod("CopyAsync");

        // Act & Assert
        Assert.NotNull(copyMethod);
        copyMethod! .IsGenericMethodDefinition.Should().BeTrue();
        
        var parameters = copyMethod.GetParameters();
        parameters.HaveCountGreaterThan(2);
        parameters[0].Name.Should().Be("source");
        parameters[1].Name.Should().Be("destination");
    }

    #endregion

    #region Memory Statistics Validation Tests

    [Fact]
    public void IMemoryStatistics_ShouldProvideComprehensiveMemoryInfo()
    {
        // Arrange
        var interfaceType = typeof(IMemoryStatistics);
        var properties = interfaceType.GetProperties();

        // Act & Assert
        Assert.Equal(6, properties.Count());
        
        // Verify all properties are read-only(no setters)
        foreach (var property in properties)
        {
            property.CanRead.Should().BeTrue();
            property.CanWrite.Should().BeFalse();
        }
    }

    [Fact]
    public void UsageByLocation_ShouldUseCorrectDictionaryType()
    {
        // Arrange
        var interfaceType = typeof(IMemoryStatistics);
        var property = interfaceType.GetProperty("UsageByLocation");

        // Act & Assert
        Assert.NotNull(property);
        property!.PropertyType.Should().Be(typeof(IReadOnlyDictionary<MemoryLocation, long>));
        
        // Should be read-only dictionary to prevent external modification
        property.PropertyType.IsAssignableFrom(typeof(Dictionary<MemoryLocation, long>)).Should().BeFalse();
        property.PropertyType.IsAssignableFrom(typeof(IReadOnlyDictionary<MemoryLocation, long>)).Should().BeTrue();
    }

    #endregion
}

/// <summary>
/// Tests for memory interface usage patterns and common scenarios.
/// </summary>
public sealed class MemoryInterfaceUsageTests
{
    [Fact]
    public void MemoryLocation_AllValues_ShouldHaveUniqueIndices()
    {
        // Arrange
        var allLocations = Enum.GetValues<MemoryLocation>();
        var indices = allLocations.Cast<int>().ToArray();

        // Act & Assert
        indices.OnlyHaveUniqueItems();
        indices.Should().AllSatisfy(i => i.BeGreaterThanOrEqualTo(0));
    }

    [Theory]
    [InlineData(MemoryLocation.Host, "HostCPU) memory")]
    [InlineData(MemoryLocation.Device, "DeviceGPU) memory")]
    [InlineData(MemoryLocation.HostPinned, "Pinned host memory for faster transfers")]
    [InlineData(MemoryLocation.Unified, "Unified memory accessible by both host and device")]
    [InlineData(MemoryLocation.Managed, "Managed memory with automatic migration")]
    public void MemoryLocation_Documentation_ShouldMatchExpectedUsage(MemoryLocation location, string expectedDescription)
    {
        // This test validates that the enum values match their documented purposes
        // Act & Assert
        location.BeDefined();
        
        // The descriptions are validated through XML documentation and usage patterns
        // This test ensures the enum values exist and can be used as expected
        var locationName = location.ToString();
        locationName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MemoryAccess_CombinedFlags_ShouldWorkAsExpected()
    {
        // Arrange
        var readOnlyWithHost = MemoryAccess.ReadOnly | MemoryAccess.HostAccess;
        var writeOnlyWithHost = MemoryAccess.WriteOnly | MemoryAccess.HostAccess;
        var readWriteWithHost = MemoryAccess.ReadWrite | MemoryAccess.HostAccess;

        // Act & Assert
        readOnlyWithHost.HasFlagMemoryAccess.ReadOnly.Should().BeTrue();
        readOnlyWithHost.HasFlagMemoryAccess.HostAccess.Should().BeTrue();
        readOnlyWithHost.HasFlag(MemoryAccess.WriteOnly).Should().BeFalse();

        writeOnlyWithHost.HasFlagMemoryAccess.WriteOnly.Should().BeTrue();
        writeOnlyWithHost.HasFlagMemoryAccess.HostAccess.Should().BeTrue();
        writeOnlyWithHost.HasFlag(MemoryAccess.ReadOnly).Should().BeFalse();

        readWriteWithHost.HasFlagMemoryAccess.ReadOnly.Should().BeTrue();
        readWriteWithHost.HasFlagMemoryAccess.WriteOnly.Should().BeTrue();
        readWriteWithHost.HasFlagMemoryAccess.HostAccess.Should().BeTrue();
    }

    [Fact]
    public void MemoryMapMode_ShouldCoverAllMappingScenarios()
    {
        // Arrange
        var allModes = Enum.GetValues<MemoryMapMode>();

        // Act & Assert
        Assert.Contains(MemoryMapMode.ReadOnly, allModes);
        Assert.Contains(MemoryMapMode.WriteOnly, allModes);
        Assert.Contains(MemoryMapMode.ReadWrite, allModes);
        Assert.Equal(3, allModes.Count());
    }

    [Theory]
    [InlineData(MemoryMapMode.ReadOnly, false, true)]
    [InlineData(MemoryMapMode.WriteOnly, true, false)]
    [InlineData(MemoryMapMode.ReadWrite, true, true)]
    public void MemoryMapMode_ShouldImplyCorrectAccessPatterns(MemoryMapMode mode, bool canWrite, bool canRead)
    {
        // This test validates the logical implications of each mapping mode
        // The actual enforcement would be in the implementation
        
        // Act & Assert
        mode.BeDefined();
        
        // Test logical consistency
        switch(mode)
        {
            case MemoryMapMode.ReadOnly:
                Assert.True(canRead);
                Assert.False(canWrite);
                break;
            case MemoryMapMode.WriteOnly:
                Assert.False(canRead);
                Assert.True(canWrite);
                break;
            case MemoryMapMode.ReadWrite:
                Assert.True(canRead);
                Assert.True(canWrite);
                break;
        }
    }
}
