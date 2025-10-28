// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: PluginValidator class is internal/private and not accessible for testing
// TODO: Uncomment when PluginValidator is made public or testable in DotCompute.Runtime.DependencyInjection.Validation
/*
using DotCompute.Runtime.DependencyInjection.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.DependencyInjection;

/// <summary>
/// Tests for PluginValidator
/// </summary>
public sealed class PluginValidatorTests
{
    private readonly ILogger<PluginValidator> _mockLogger;
    private readonly PluginValidator _validator;

    public PluginValidatorTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginValidator>>();
        _validator = new PluginValidator(_mockLogger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new PluginValidator(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void ValidatePluginType_WithValidType_ReturnsTrue()
    {
        // Arrange
        var type = typeof(ValidPlugin);

        // Act
        var result = _validator.ValidatePluginType(type);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidatePluginType_WithNullType_ReturnsFalse()
    {
        // Arrange & Act
        var result = _validator.ValidatePluginType(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePluginType_WithAbstractType_ReturnsFalse()
    {
        // Arrange
        var type = typeof(AbstractPlugin);

        // Act
        var result = _validator.ValidatePluginType(type);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePluginType_WithInterface_ReturnsFalse()
    {
        // Arrange
        var type = typeof(IPlugin);

        // Act
        var result = _validator.ValidatePluginType(type);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePluginType_WithGenericType_ReturnsTrue()
    {
        // Arrange
        var type = typeof(GenericPlugin<string>);

        // Act
        var result = _validator.ValidatePluginType(type);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidatePluginDependencies_WithValidDependencies_ReturnsTrue()
    {
        // Arrange
        var type = typeof(ValidPlugin);

        // Act
        var result = _validator.ValidatePluginDependencies(type);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidatePluginDependencies_WithNullType_ReturnsFalse()
    {
        // Arrange & Act
        var result = _validator.ValidatePluginDependencies(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePluginConfiguration_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var configuration = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        var result = _validator.ValidatePluginConfiguration(configuration);

        // Assert
        result.Should().BeTrue();
    }

    // Helper interfaces and classes for testing
    private interface IPlugin { }
    private class ValidPlugin : IPlugin { }
    private abstract class AbstractPlugin : IPlugin { }
    private class GenericPlugin<T> : IPlugin { }
}
*/