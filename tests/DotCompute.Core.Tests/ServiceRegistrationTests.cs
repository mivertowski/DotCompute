using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Core.Compilation;
using DotCompute.Core.Diagnostics;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for DotCompute service registration and DI integration.
/// </summary>
public class ServiceRegistrationTests
{
    [Fact]
    public void AddDotCompute_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotCompute();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IAcceleratorManager>().Should().NotBeNull();
        provider.GetService<IKernelCompiler>().Should().NotBeNull();
        provider.GetService<IPluginManager>().Should().NotBeNull();
        provider.GetService<IDiagnosticsManager>().Should().NotBeNull();
    }

    [Fact]
    public void AddDotCompute_WithConfiguration_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurationApplied = false;

        // Act
        services.AddDotCompute(options =>
        {
            configurationApplied = true;
            options.EnableDiagnostics = true;
            options.EnableAutoDiscovery = true;
            options.MaxConcurrentKernels = 10;
        });

        // Assert
        configurationApplied.Should().BeTrue();
    }

    [Fact]
    public void AddDotCompute_RegistersSingletonServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotCompute();

        // Assert
        var acceleratorManagerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAcceleratorManager));
        var kernelCompilerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IKernelCompiler));
        var pluginManagerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IPluginManager));

        acceleratorManagerDescriptor.Should().NotBeNull();
        acceleratorManagerDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);

        kernelCompilerDescriptor.Should().NotBeNull();
        kernelCompilerDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);

        pluginManagerDescriptor.Should().NotBeNull();
        pluginManagerDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddDotCompute_CalledMultipleTimes_DoesNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotCompute();
        services.AddDotCompute();
        services.AddDotCompute();

        // Assert
        services.Count(s => s.ServiceType == typeof(IAcceleratorManager)).Should().Be(1);
        services.Count(s => s.ServiceType == typeof(IKernelCompiler)).Should().Be(1);
        services.Count(s => s.ServiceType == typeof(IPluginManager)).Should().Be(1);
    }

    [Fact]
    public void ServiceProvider_CanResolveAllRegisteredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDotCompute();
        var provider = services.BuildServiceProvider();

        // Act & Assert - This should not throw
        using (var scope = provider.CreateScope())
        {
            var acceleratorManager = scope.ServiceProvider.GetRequiredService<IAcceleratorManager>();
            var kernelCompiler = scope.ServiceProvider.GetRequiredService<IKernelCompiler>();
            var pluginManager = scope.ServiceProvider.GetRequiredService<IPluginManager>();
            var diagnosticsManager = scope.ServiceProvider.GetRequiredService<IDiagnosticsManager>();

            acceleratorManager.Should().NotBeNull();
            kernelCompiler.Should().NotBeNull();
            pluginManager.Should().NotBeNull();
            diagnosticsManager.Should().NotBeNull();
        }
    }

    [Fact]
    public void AddDotCompute_WithLogging_IntegratesWithLogging()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddDotCompute();
        var provider = services.BuildServiceProvider();

        // Assert - Services should be created with logging support
        var acceleratorManager = provider.GetRequiredService<IAcceleratorManager>();
        acceleratorManager.Should().NotBeNull();
    }

    [Fact]
    public void AddDotCompute_RegistersOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotCompute(options =>
        {
            options.EnableDiagnostics = true;
            options.MaxConcurrentKernels = 20;
            options.DefaultBackend = "CPU";
        });
        
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<DotComputeOptions>();
        options.Should().NotBeNull();
        options!.EnableDiagnostics.Should().BeTrue();
        options.MaxConcurrentKernels.Should().Be(20);
        options.DefaultBackend.Should().Be("CPU");
    }

    [Fact]
    public void AddDotCompute_WithCustomServices_AllowsOverrides()
    {
        // Arrange
        var services = new ServiceCollection();
        var customAcceleratorManager = new TestAcceleratorManager();
        
        // Register custom implementation first
        services.AddSingleton<IAcceleratorManager>(customAcceleratorManager);

        // Act
        services.AddDotCompute();
        var provider = services.BuildServiceProvider();

        // Assert - Should use the custom implementation
        var resolved = provider.GetRequiredService<IAcceleratorManager>();
        resolved.Should().BeSameAs(customAcceleratorManager);
    }

    private class TestAcceleratorManager : IAcceleratorManager
    {
        public Task<IAccelerator> GetDefaultAcceleratorAsync() => throw new NotImplementedException();
        public Task<IAccelerator> GetAcceleratorAsync(string name) => throw new NotImplementedException();
        public Task<IReadOnlyList<IAccelerator>> GetAvailableAcceleratorsAsync() => throw new NotImplementedException();
        public void RegisterAccelerator(string name, IAccelerator accelerator) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}