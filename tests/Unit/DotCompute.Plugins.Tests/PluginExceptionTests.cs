// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Exceptions;
using FluentAssertions;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for plugin exception classes covering serialization, inheritance, and specific properties.
/// </summary>
public class PluginExceptionTests
{
    [Fact]
    public void PluginException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new PluginException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
        exception.PluginId.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void PluginException_WithMessage_SetsMessage()
    {
        // Arrange
        const string message = "Test plugin error";

        // Act
        var exception = new PluginException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().BeNull();
    }

    [Fact]
    public void PluginException_WithMessageAndInnerException_SetsProperties()
    {
        // Arrange
        const string message = "Test plugin error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new PluginException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
        exception.PluginId.Should().BeNull();
    }

    [Fact]
    public void PluginException_WithMessageAndPluginId_SetsProperties()
    {
        // Arrange
        const string message = "Test plugin error";
        const string pluginId = "test-plugin";

        // Act
        var exception = new PluginException(message, pluginId);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
    }

    [Fact]
    public void PluginException_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        const string message = "Test plugin error";
        const string pluginId = "test-plugin";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new PluginException(message, pluginId, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void PluginLoadException_WithFilePathAndPluginId_SetsProperties()
    {
        // Arrange
        const string message = "Failed to load plugin";
        const string pluginId = "test-plugin";
        const string filePath = "/path/to/plugin.dll";

        // Act
        var exception = new PluginLoadException(message, pluginId, filePath);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void PluginLoadException_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        const string message = "Failed to load plugin";
        const string pluginId = "test-plugin";
        const string filePath = "/path/to/plugin.dll";
        var innerException = new FileNotFoundException("File not found");

        // Act
        var exception = new PluginLoadException(message, pluginId, filePath, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.FilePath.Should().Be(filePath);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void PluginInitializationException_WithPluginId_SetsProperties()
    {
        // Arrange
        const string message = "Initialization failed";
        const string pluginId = "test-plugin";

        // Act
        var exception = new PluginInitializationException(message, pluginId);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
    }

    [Fact]
    public void PluginDependencyException_WithDependencyInfo_SetsProperties()
    {
        // Arrange
        const string message = "Dependency not satisfied";
        const string pluginId = "test-plugin";
        const string dependencyId = "required-plugin";
        const string requiredVersion = "1.0.0";
        const string actualVersion = "0.9.0";

        // Act
        var exception = new PluginDependencyException(message, pluginId, dependencyId, requiredVersion, actualVersion);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.DependencyId.Should().Be(dependencyId);
        exception.RequiredVersion.Should().Be(requiredVersion);
        exception.ActualVersion.Should().Be(actualVersion);
    }

    [Fact]
    public void PluginValidationException_WithValidationErrors_SetsProperties()
    {
        // Arrange
        const string message = "Validation failed";
        const string pluginId = "test-plugin";
        var errors = new List<string> { "Error 1", "Error 2" };

        // Act
        var exception = new PluginValidationException(message, pluginId, errors);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.ValidationErrors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void PluginTimeoutException_WithTimeoutInfo_SetsProperties()
    {
        // Arrange
        const string message = "Operation timed out";
        const string pluginId = "test-plugin";
        const string operation = "Initialize";
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var exception = new PluginTimeoutException(message, pluginId, timeout, operation);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.Timeout.Should().Be(timeout);
        exception.Operation.Should().Be(operation);
    }

    [Fact]
    public void PluginSecurityException_WithSecurityViolation_SetsProperties()
    {
        // Arrange
        const string message = "Security violation detected";
        const string pluginId = "test-plugin";
        const string securityViolation = "Attempted to access restricted API";

        // Act
        var exception = new PluginSecurityException(message, pluginId, securityViolation);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.SecurityViolation.Should().Be(securityViolation);
    }

    [Fact]
    public void PluginConfigurationException_WithConfigurationInfo_SetsProperties()
    {
        // Arrange
        const string message = "Invalid configuration";
        const string pluginId = "test-plugin";
        const string configurationKey = "MaxThreads";
        const int invalidValue = -1;

        // Act
        var exception = new PluginConfigurationException(message, pluginId, configurationKey, invalidValue);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.ConfigurationKey.Should().Be(configurationKey);
        exception.InvalidValue.Should().Be(invalidValue);
    }

    [Fact]
    public void PluginNotFoundException_WithPluginName_SetsProperties()
    {
        // Arrange
        const string message = "Plugin not found";
        const string pluginId = "test-plugin";
        const string pluginName = "Test Plugin";

        // Act
        var exception = new PluginNotFoundException(message, pluginId, pluginName);

        // Assert
        exception.Message.Should().Be(message);
        exception.PluginId.Should().Be(pluginId);
        exception.PluginName.Should().Be(pluginName);
    }

    [Theory]
    [InlineData(typeof(PluginException))]
    [InlineData(typeof(PluginLoadException))]
    [InlineData(typeof(PluginInitializationException))]
    [InlineData(typeof(PluginDependencyException))]
    [InlineData(typeof(PluginValidationException))]
    [InlineData(typeof(PluginTimeoutException))]
    [InlineData(typeof(PluginSecurityException))]
    [InlineData(typeof(PluginConfigurationException))]
    [InlineData(typeof(PluginNotFoundException))]
    public void AllPluginExceptions_InheritFromPluginException(Type exceptionType)
    {
        // Assert
        exceptionType.Should().BeAssignableTo<PluginException>();
    }

    [Theory]
    [InlineData(typeof(PluginException))]
    [InlineData(typeof(PluginLoadException))]
    [InlineData(typeof(PluginInitializationException))]
    [InlineData(typeof(PluginDependencyException))]
    [InlineData(typeof(PluginValidationException))]
    [InlineData(typeof(PluginTimeoutException))]
    [InlineData(typeof(PluginSecurityException))]
    [InlineData(typeof(PluginConfigurationException))]
    [InlineData(typeof(PluginNotFoundException))]
    public void AllPluginExceptions_AreSerializable(Type exceptionType)
    {
        // Assert
        exceptionType.Should().BeDecoratedWith<SerializableAttribute>();
    }

    [Fact]
    public void PluginException_ToString_IncludesPluginId()
    {
        // Arrange
        const string message = "Test error";
        const string pluginId = "test-plugin";
        var exception = new PluginException(message, pluginId);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain(message);
        result.Should().Contain(typeof(PluginException).FullName!);
    }

    [Fact]
    public void PluginLoadException_ToString_IncludesFilePath()
    {
        // Arrange
        const string message = "Load failed";
        const string pluginId = "test-plugin";
        const string filePath = "/path/to/plugin.dll";
        var exception = new PluginLoadException(message, pluginId, filePath);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain(message);
        result.Should().Contain(typeof(PluginLoadException).FullName!);
    }

    [Fact]
    public void PluginDependencyException_ToString_IncludesDependencyInfo()
    {
        // Arrange
        const string message = "Dependency error";
        const string pluginId = "test-plugin";
        const string dependencyId = "required-plugin";
        var exception = new PluginDependencyException(message, pluginId, dependencyId);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain(message);
        result.Should().Contain(typeof(PluginDependencyException).FullName!);
    }

    [Fact]
    public void PluginValidationException_WithNullValidationErrors_HandlesGracefully()
    {
        // Arrange & Act
        var exception = new PluginValidationException("Test message", "test-plugin", new List<string>());

        // Assert
        exception.ValidationErrors.Should().NotBeNull();
        exception.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void PluginTimeoutException_WithZeroTimeout_HandlesCorrectly()
    {
        // Arrange & Act
        var exception = new PluginTimeoutException("Timeout", "test-plugin", TimeSpan.Zero, "Test");

        // Assert
        exception.Timeout.Should().Be(TimeSpan.Zero);
        exception.Operation.Should().Be("Test");
    }

    [Fact]
    public void PluginConfigurationException_WithNullInvalidValue_HandlesCorrectly()
    {
        // Arrange & Act
        var exception = new PluginConfigurationException("Config error", "test-plugin", "key", null!);

        // Assert
        exception.ConfigurationKey.Should().Be("key");
        exception.InvalidValue.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void PluginNotFoundException_WithNullOrEmptyPluginName_HandlesCorrectly(string? pluginName)
    {
        // Arrange & Act
        var exception = new PluginNotFoundException("Not found", "test-plugin", pluginName!);

        // Assert
        exception.PluginName.Should().Be(pluginName);
    }

    [Fact]
    public void PluginException_WithChainedExceptions_PreservesChain()
    {
        // Arrange
        var rootCause = new ArgumentException("Root cause");
        var intermediate = new InvalidOperationException("Intermediate", rootCause);
        var pluginException = new PluginException("Plugin error", "test-plugin", intermediate);

        // Act & Assert
        pluginException.InnerException.Should().BeSameAs(intermediate);
        pluginException.InnerException!.InnerException.Should().BeSameAs(rootCause);
    }

    [Fact]
    public void AllPluginExceptions_CanBeCaughtAsPluginException()
    {
        // Arrange
        var exceptions = new Exception[]
        {
            new PluginLoadException("Load error", "plugin", "path"),
            new PluginInitializationException("Init error", "plugin"),
            new PluginDependencyException("Dep error", "plugin", "dep"),
            new PluginValidationException("Validation error", "plugin", new List<string>()),
            new PluginTimeoutException("Timeout error", "plugin", TimeSpan.FromSeconds(1), "op"),
            new PluginSecurityException("Security error", "plugin", "violation"),
            new PluginConfigurationException("Config error", "plugin", "key"),
            new PluginNotFoundException("Not found error", "plugin")
        };

        // Act & Assert
        foreach (var exception in exceptions)
        {
            exception.Should().BeAssignableTo<PluginException>();
        }
    }
}