// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotCompute.Linq.Tests.Extensions;

/// <summary>
/// Tests for LINQ service registration extensions.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDotComputeLinq_WithDefaultOptions_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotComputeLinq();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(serviceProvider.GetService<LinqServiceOptions>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Services.QueryCacheOptions>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Execution.IQueryCache>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Services.DefaultMemoryManagerFactory>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Expressions.IExpressionOptimizer>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Compilation.IQueryCompiler>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Execution.IQueryExecutor>());
        Assert.NotNull(serviceProvider.GetService<DotCompute.Linq.Interfaces.IComputeLinqProvider>());
        Assert.NotNull(serviceProvider.GetService<IComputeQueryProviderFactory>());
    }

    [Fact]
    public void AddDotComputeLinq_WithCustomOptions_ConfiguresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        const int expectedCacheEntries = 2000;
        const bool expectedCaching = false;

        // Act
        services.AddDotComputeLinq(options =>
        {
            options.CacheMaxEntries = expectedCacheEntries;
            options.EnableCaching = expectedCaching;
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<LinqServiceOptions>();

        // Assert
        Assert.Equal(expectedCacheEntries, options.CacheMaxEntries);
        Assert.Equal(expectedCaching, options.EnableCaching);
    }

    [Fact]
    public void AddDotComputeLinqAdvanced_WithFlags_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotComputeLinqAdvanced(
            enableCaching: false,
            enableOptimization: true,
            enableProfiling: true);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<LinqServiceOptions>();

        // Assert
        Assert.False(options.EnableCaching);
        Assert.True(options.EnableOptimization);
        Assert.True(options.EnableProfiling);
    }

    [Fact]
    public void QueryCacheOptions_MapsFromLinqServiceOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var expectedExpiration = TimeSpan.FromMinutes(45);

        // Act
        services.AddDotComputeLinq(options =>
        {
            options.CacheExpiration = expectedExpiration;
            options.EnableCacheExpiration = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var cacheOptions = serviceProvider.GetRequiredService<DotCompute.Linq.Services.QueryCacheOptions>();

        // Assert
        Assert.Equal(expectedExpiration, cacheOptions.DefaultExpiration);
        Assert.False(cacheOptions.EnableExpiration);
    }

    [Fact]
    public void LinqServiceOptions_HasExpectedDefaults()
    {
        // Arrange & Act
        var options = new LinqServiceOptions();

        // Assert
        Assert.True(options.EnableCaching);
        Assert.Equal(1000, options.CacheMaxEntries);
        Assert.True(options.EnableCacheExpiration);
        Assert.Equal(TimeSpan.FromHours(1), options.CacheExpiration);
        Assert.True(options.EnableOptimization);
        Assert.False(options.EnableProfiling);
        Assert.True(options.EnableCpuFallback);
        Assert.Null(options.DefaultTimeout);
    }

    [Fact]
    public void ServiceProvider_GetComputeLinqProvider_ReturnsCorrectInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDotComputeLinq();
        
        // Add mock orchestrator to prevent DI errors
        // Note: Using simple mock implementation for now
        services.AddSingleton<DotCompute.Abstractions.Interfaces.IComputeOrchestrator>(new MockComputeOrchestrator());
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var linqProvider = serviceProvider.GetComputeLinqProvider();

        // Assert
        Assert.NotNull(linqProvider);
        Assert.IsAssignableFrom<DotCompute.Linq.Interfaces.IComputeLinqProvider>(linqProvider);
    }
}