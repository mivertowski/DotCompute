// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotCompute.Linq.Tests;

/// <summary>
/// Comprehensive tests for ServiceCollectionExtensions and ComputeLinqProvider.
/// Target: 40 tests covering dependency injection and service configuration.
/// Tests service registration, provider behavior, and integration with IServiceCollection.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    #region AddDotComputeLinq Tests

    [Fact]
    public void AddDotComputeLinq_WithValidServiceCollection_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        var linqProvider = provider.GetService<IComputeLinqProvider>();
        linqProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddDotComputeLinq_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? nullServices = null;

        // Act
        var act = () => nullServices!.AddDotComputeLinq();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddDotComputeLinq_RegistersSingletonProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        var instance1 = provider.GetService<IComputeLinqProvider>();
        var instance2 = provider.GetService<IComputeLinqProvider>();
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void AddDotComputeLinq_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var result = services.AddDotComputeLinq();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddDotComputeLinq_CanBeCalledMultipleTimes_RegistersOnlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddDotComputeLinq();
        services.AddDotComputeLinq();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        var providers = provider.GetServices<IComputeLinqProvider>().ToList();
        providers.Should().HaveCount(1); // TryAddSingleton should prevent duplicates
    }

    [Fact]
    public void AddDotComputeLinq_RegistersComputeLinqProviderImplementation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        var linqProvider = provider.GetService<IComputeLinqProvider>();
        linqProvider.Should().NotBeNull();
        linqProvider.GetType().Name.Should().Be("ComputeLinqProvider");
    }

    [Fact]
    public void AddDotComputeLinq_WithExistingServices_PreservesOtherServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestService>();

        // Act
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        var testService = provider.GetService<TestService>();
        var linqProvider = provider.GetService<IComputeLinqProvider>();
        testService.Should().NotBeNull();
        linqProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddDotComputeLinq_AllowsMethodChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services
            .AddLogging()
            .AddDotComputeLinq()
            .AddSingleton<TestService>();

        // Assert
        result.Should().BeSameAs(services);
        var provider = services.BuildServiceProvider();
        provider.GetService<IComputeLinqProvider>().Should().NotBeNull();
        provider.GetService<TestService>().Should().NotBeNull();
    }

    #endregion

    #region ComputeLinqProvider Tests

    [Fact]
    public void ComputeLinqProvider_WithValidLogger_CreatesInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Act
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();

        // Assert
        linqProvider.Should().NotBeNull();
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_WithValidSource_ReturnsQueryable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { 1, 2, 3, 4, 5 };

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IQueryable<int>>();
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();

        // Act
        var act = () => linqProvider.CreateComputeQueryable<int>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_WithEmptySource_ReturnsEmptyQueryable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = Array.Empty<int>();

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_PreservesElementType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { "a", "b", "c" };

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.ElementType.Should().Be(typeof(string));
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_WithComplexType_CreatesCorrectQueryable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[]
        {
            new TestData { Id = 1, Name = "Test" }
        };

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().NotBeNull();
        result.ElementType.Should().Be(typeof(TestData));
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_SupportsLinqOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { 1, 2, 3, 4, 5 };

        // Act
        var queryable = linqProvider.CreateComputeQueryable(data);
        var result = queryable.Where(x => x > 2).ToArray();

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void ComputeLinqProvider_CreateComputeQueryable_WithLargeDataSet_HandlesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = Enumerable.Range(1, 10000);

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().HaveCount(10000);
    }

    [Fact]
    public void ComputeLinqProvider_MultipleCallsCreateIndependentQueryables()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { 1, 2, 3 };

        // Act
        var queryable1 = linqProvider.CreateComputeQueryable(data);
        var queryable2 = linqProvider.CreateComputeQueryable(data);

        // Assert
        queryable1.Should().NotBeSameAs(queryable2);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_ServiceProvider_ResolvesComputeLinqProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Act
        var linqProvider = provider.GetService<IComputeLinqProvider>();

        // Assert
        linqProvider.Should().NotBeNull();
        linqProvider.Should().BeAssignableTo<IComputeLinqProvider>();
    }

    [Fact]
    public void Integration_CompleteWorkflow_WithDependencyInjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { 1, 2, 3, 4, 5 };

        // Act
        var queryable = linqProvider.CreateComputeQueryable(data);
        var result = queryable
            .Where(x => x > 2)
            .Select(x => x * 2)
            .ToArray();

        // Assert
        result.Should().Equal(6, 8, 10);
    }

    [Fact]
    public void Integration_WithCustomLoggerFactory_UsesProvidedLogger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Act
        var linqProvider = provider.GetService<IComputeLinqProvider>();

        // Assert
        linqProvider.Should().NotBeNull();
    }

    [Fact]
    public void Integration_MultipleScopesResolveSameSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Act
        IComputeLinqProvider? instance1;
        IComputeLinqProvider? instance2;

        using (var scope1 = provider.CreateScope())
        {
            instance1 = scope1.ServiceProvider.GetService<IComputeLinqProvider>();
        }

        using (var scope2 = provider.CreateScope())
        {
            instance2 = scope2.ServiceProvider.GetService<IComputeLinqProvider>();
        }

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void Integration_WithExistingLoggingConfiguration_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Act
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { 1, 2, 3 };
        var queryable = linqProvider.CreateComputeQueryable(data);

        // Assert
        queryable.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void ServiceCollection_WithoutLogging_StillRegistersProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        // Should throw because logger is required
        var act = () => provider.GetRequiredService<IComputeLinqProvider>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ComputeLinqProvider_WithListSource_CreatesQueryable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new List<int> { 1, 2, 3 };

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    [Fact]
    public void ComputeLinqProvider_WithHashSetSource_CreatesQueryable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = new HashSet<int> { 1, 2, 3 };

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    [Fact]
    public void AddDotComputeLinq_InComplexServiceConfiguration_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestService>();
        services.AddScoped<AnotherTestService>();

        // Act
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Assert
        var linqProvider = provider.GetService<IComputeLinqProvider>();
        var testService = provider.GetService<TestService>();
        var anotherService = provider.GetService<AnotherTestService>();

        linqProvider.Should().NotBeNull();
        testService.Should().NotBeNull();
        anotherService.Should().NotBeNull();
    }

    [Fact]
    public void ComputeLinqProvider_WithDifferentDataTypes_CreatesCorrectQueryables()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();

        // Act
        var intQueryable = linqProvider.CreateComputeQueryable(new[] { 1, 2, 3 });
        var stringQueryable = linqProvider.CreateComputeQueryable(new[] { "a", "b" });
        var doubleQueryable = linqProvider.CreateComputeQueryable(new[] { 1.5, 2.5 });

        // Assert
        intQueryable.ElementType.Should().Be(typeof(int));
        stringQueryable.ElementType.Should().Be(typeof(string));
        doubleQueryable.ElementType.Should().Be(typeof(double));
    }

    [Fact]
    public void ServiceProvider_CanResolveMultipleTimesInSameScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();

        // Act
        var instance1 = provider.GetService<IComputeLinqProvider>();
        var instance2 = provider.GetService<IComputeLinqProvider>();
        var instance3 = provider.GetService<IComputeLinqProvider>();

        // Assert
        instance1.Should().BeSameAs(instance2);
        instance2.Should().BeSameAs(instance3);
    }

    [Fact]
    public void ComputeLinqProvider_WithImmutableCollectionSource_CreatesQueryable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        var provider = services.BuildServiceProvider();
        var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();
        var data = System.Collections.Immutable.ImmutableArray.Create(1, 2, 3);

        // Act
        var result = linqProvider.CreateComputeQueryable(data);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    #endregion

    #region Helper Classes

    private sealed class TestService
    {
        public string Name => "TestService";
    }

    private sealed class AnotherTestService
    {
        public int Value => 42;
    }

    private sealed class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
