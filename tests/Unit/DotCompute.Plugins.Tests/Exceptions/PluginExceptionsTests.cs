// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Exceptions;
using DotCompute.Plugins.Exceptions.Configuration;
using DotCompute.Plugins.Exceptions.Core;
using DotCompute.Plugins.Exceptions.Loading;
using DotCompute.Plugins.Exceptions.Security;
using DotCompute.Plugins.Exceptions.Validation;

namespace DotCompute.Plugins.Tests.Exceptions;

/// <summary>
/// Comprehensive tests for plugin exception types covering error handling scenarios.
/// </summary>
public sealed class PluginExceptionsTests
{
    [Fact]
    public void PluginException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Test plugin exception";

        // Act
        var exception = new PluginException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginException_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "Test plugin exception";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new PluginException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void PluginLoadException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginLoadException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginLoadException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Failed to load plugin";

        // Act
        var exception = new PluginLoadException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginLoadException_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "Failed to load plugin";
        var innerException = new FileNotFoundException("Plugin file not found");

        // Act
        var exception = new PluginLoadException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void PluginTimeoutException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginTimeoutException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginTimeoutException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Plugin operation timed out";

        // Act
        var exception = new PluginTimeoutException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginInitializationException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginInitializationException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginInitializationException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Plugin initialization failed";

        // Act
        var exception = new PluginInitializationException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginValidationException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginValidationException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginValidationException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Plugin validation failed";

        // Act
        var exception = new PluginValidationException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginSecurityException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginSecurityException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginSecurityException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Security check failed";

        // Act
        var exception = new PluginSecurityException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginDependencyException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginDependencyException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginDependencyException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Dependency resolution failed";

        // Act
        var exception = new PluginDependencyException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginConfigurationException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginConfigurationException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginConfigurationException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Configuration error";

        // Act
        var exception = new PluginConfigurationException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Theory]
    [InlineData("Error message 1")]
    [InlineData("Error message 2")]
    [InlineData("Error message 3")]
    public void PluginException_WithDifferentMessages_ShouldSetCorrectly(string message)
    {
        // Arrange & Act
        var exception = new PluginException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void PluginException_CanBeCaught_AsPluginException()
    {
        // Arrange
        var exception = new PluginLoadException("Test");

        // Act & Assert
        exception.Should().BeAssignableTo<PluginException>();
    }

    [Fact]
    public void PluginLoadException_CanBeCaught_AsException()
    {
        // Arrange
        var exception = new PluginLoadException("Test");

        // Act & Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void AllPluginExceptions_ShouldInheritFromPluginException()
    {
        // Arrange & Act & Assert
        new PluginLoadException().Should().BeAssignableTo<PluginException>();
        new PluginTimeoutException().Should().BeAssignableTo<PluginException>();
        new PluginInitializationException().Should().BeAssignableTo<PluginException>();
        new PluginValidationException().Should().BeAssignableTo<PluginException>();
        new PluginSecurityException().Should().BeAssignableTo<PluginException>();
        new PluginDependencyException().Should().BeAssignableTo<PluginException>();
        new PluginConfigurationException().Should().BeAssignableTo<PluginException>();
    }

    [Fact]
    public void PluginNotFoundException_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var exception = new PluginNotFoundException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginNotFoundException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Plugin not found";

        // Act
        var exception = new PluginNotFoundException(message);

        // Assert
        exception.Message.Should().Be(message);
    }
}
