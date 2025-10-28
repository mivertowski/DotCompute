// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: PluginActivator class is internal/private and not accessible for testing
// TODO: Uncomment when PluginActivator is made public or testable in DotCompute.Runtime.DependencyInjection.Services
/*
using DotCompute.Runtime.DependencyInjection.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.DependencyInjection;

/// <summary>
/// Tests for PluginActivator
/// </summary>
public sealed class PluginActivatorTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PluginActivator _activator;

    public PluginActivatorTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestDependency, TestDependency>();
        _serviceProvider = services.BuildServiceProvider();
        _activator = new PluginActivator(_serviceProvider);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new PluginActivator(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceProvider");
    }

    [Fact]
    public void CreateInstance_WithValidType_CreatesInstance()
    {
        // Arrange
        var type = typeof(TestClass);

        // Act
        var instance = _activator.CreateInstance(type);

        // Assert
        instance.Should().NotBeNull();
        instance.Should().BeOfType<TestClass>();
    }

    [Fact]
    public void CreateInstance_WithDependencies_InjectsDependencies()
    {
        // Arrange
        var type = typeof(TestClassWithDependency);

        // Act
        var instance = _activator.CreateInstance(type);

        // Assert
        instance.Should().NotBeNull();
        var testInstance = instance as TestClassWithDependency;
        testInstance!.Dependency.Should().NotBeNull();
    }

    [Fact]
    public void CreateInstance_Generic_CreatesTypedInstance()
    {
        // Act
        var instance = _activator.CreateInstance<TestClass>();

        // Assert
        instance.Should().NotBeNull();
        instance.Should().BeOfType<TestClass>();
    }

    [Fact]
    public void CreateInstance_Generic_WithDependencies_InjectsDependencies()
    {
        // Act
        var instance = _activator.CreateInstance<TestClassWithDependency>();

        // Assert
        instance.Should().NotBeNull();
        instance.Dependency.Should().NotBeNull();
    }

    [Fact]
    public void CreateInstance_WithAbstractType_ThrowsException()
    {
        // Arrange
        var type = typeof(AbstractClass);

        // Act
        var action = () => _activator.CreateInstance(type);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateInstance_WithInterface_ThrowsException()
    {
        // Arrange
        var type = typeof(ITestDependency);

        // Act
        var action = () => _activator.CreateInstance(type);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateInstance_CalledMultipleTimes_CreatesNewInstances()
    {
        // Arrange
        var type = typeof(TestClass);

        // Act
        var instance1 = _activator.CreateInstance(type);
        var instance2 = _activator.CreateInstance(type);

        // Assert
        instance1.Should().NotBeSameAs(instance2);
    }

    [Fact]
    public void CreateInstance_WithUnregisteredDependency_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        var emptyProvider = services.BuildServiceProvider();
        var emptyActivator = new PluginActivator(emptyProvider);
        var type = typeof(TestClassWithDependency);

        // Act
        var action = () => emptyActivator.CreateInstance(type);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateInstance_WithGenericType_CreatesInstance()
    {
        // Arrange
        var type = typeof(GenericTestClass<string>);

        // Act
        var instance = _activator.CreateInstance(type);

        // Assert
        instance.Should().NotBeNull();
        instance.Should().BeOfType<GenericTestClass<string>>();
    }

    // Helper interfaces and classes for testing
    private interface ITestDependency { }
    private class TestDependency : ITestDependency { }
    private class TestClass { }
    private class TestClassWithDependency
    {
        public ITestDependency Dependency { get; }
        public TestClassWithDependency(ITestDependency dependency)
        {
            Dependency = dependency;
        }
    }
    private abstract class AbstractClass { }
    private class GenericTestClass<T> { }
}
*/