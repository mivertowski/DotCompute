// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Atomics;

namespace DotCompute.Abstractions.Tests.Atomics;

/// <summary>
/// Unit tests for <see cref="MemoryScope"/> enum.
/// </summary>
public class MemoryScopeTests
{
    [Fact]
    public void MemoryScope_HasExpectedValues()
    {
        // Assert - verify enum values are as documented
        ((int)MemoryScope.Workgroup).Should().Be(0);
        ((int)MemoryScope.Device).Should().Be(1);
        ((int)MemoryScope.System).Should().Be(2);
    }

    [Fact]
    public void MemoryScope_HasThreeValues()
    {
        // Arrange
        var values = Enum.GetValues<MemoryScope>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(MemoryScope.Workgroup, "Workgroup")]
    [InlineData(MemoryScope.Device, "Device")]
    [InlineData(MemoryScope.System, "System")]
    public void MemoryScope_ToString_ReturnsCorrectName(MemoryScope scope, string expectedName)
    {
        // Act
        var name = scope.ToString();

        // Assert
        name.Should().Be(expectedName);
    }

    [Fact]
    public void MemoryScope_Default_IsWorkgroup()
    {
        // Arrange
        var defaultScope = default(MemoryScope);

        // Assert
        defaultScope.Should().Be(MemoryScope.Workgroup);
    }

    [Theory]
    [InlineData("Workgroup", MemoryScope.Workgroup)]
    [InlineData("Device", MemoryScope.Device)]
    [InlineData("System", MemoryScope.System)]
    public void MemoryScope_Parse_ReturnsCorrectValue(string name, MemoryScope expected)
    {
        // Act
        var parsed = Enum.Parse<MemoryScope>(name);

        // Assert
        parsed.Should().Be(expected);
    }

    [Fact]
    public void MemoryScope_WorkgroupIsNarrowest()
    {
        // Workgroup has the lowest value, indicating narrowest scope
        ((int)MemoryScope.Workgroup).Should().BeLessThan((int)MemoryScope.Device);
        ((int)MemoryScope.Workgroup).Should().BeLessThan((int)MemoryScope.System);
    }

    [Fact]
    public void MemoryScope_SystemIsWidest()
    {
        // System has the highest value, indicating widest scope
        ((int)MemoryScope.System).Should().BeGreaterThan((int)MemoryScope.Workgroup);
        ((int)MemoryScope.System).Should().BeGreaterThan((int)MemoryScope.Device);
    }

    [Fact]
    public void MemoryScope_DeviceIsBetween()
    {
        // Device should be between Workgroup and System
        ((int)MemoryScope.Device).Should().BeGreaterThan((int)MemoryScope.Workgroup);
        ((int)MemoryScope.Device).Should().BeLessThan((int)MemoryScope.System);
    }

    [Fact]
    public void MemoryScope_ScopeHierarchyIsCorrect()
    {
        // Verify the hierarchy: Workgroup < Device < System
        // This represents increasing scope of memory visibility
        var ordered = new[] { MemoryScope.Workgroup, MemoryScope.Device, MemoryScope.System };

        for (var i = 0; i < ordered.Length - 1; i++)
        {
            ((int)ordered[i]).Should().BeLessThan((int)ordered[i + 1]);
        }
    }
}
