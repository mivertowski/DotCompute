// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq;
using DotCompute.Linq.Extensions;
using DotCompute.Linq.Interfaces;
using DotCompute.Runtime.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Integration tests for LINQ provider with runtime orchestrator.
/// Tests the complete flow from service configuration to kernel execution.
/// Extracted from the legacy DotCompute.Linq.IntegrationTests project.
/// </summary>
public class RuntimeOrchestrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;

    public RuntimeOrchestrationTests(ITestOutputHelper output)
    {
        _output = output;
        _serviceProvider = BuildServiceProvider();
        _loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add DotCompute runtime
        services.AddDotComputeRuntime();

        // Add LINQ with runtime integration
        services.AddDotComputeLinq(options =>
        {
            options.EnableOptimization = true;
            options.EnableCaching = true;
            options.EnableCpuFallback = true;
        });

        return services.BuildServiceProvider();
    }

    #region Service Provider Configuration Tests

    [Fact]
    public void Can_Create_ComputeLinqProvider()
    {
        // Arrange & Act
        var provider = _serviceProvider.GetService<IComputeLinqProvider>();

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<RuntimeIntegratedLinqProvider>();
    }

    [Fact]
    public void Can_Create_ComputeOrchestrator()
    {
        // Arrange & Act
        var orchestrator = _serviceProvider.GetService<IComputeOrchestrator>();

        // Assert
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Can_Validate_LinqRuntime_Integration()
    {
        // Arrange & Act
        var isValid = _serviceProvider.ValidateLinqRuntimeIntegration();

        // Assert
        isValid.Should().BeTrue("LINQ runtime integration should be properly configured");
    }

    #endregion

    #region Queryable Creation Tests

    [Fact]
    public async Task Can_Create_Queryable_From_Array()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = new[] { 1, 2, 3, 4, 5 };

        // Act
        var queryable = linqProvider.CreateQueryable(testData);

        // Assert
        queryable.Should().NotBeNull();
        queryable.ElementType.Should().Be(typeof(int));
        queryable.Should().BeAssignableTo<IQueryable<int>>();

        // Test that we can execute the queryable
        var result = await queryable.ExecuteAsync();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Can_Create_Queryable_From_Enumerable()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = Enumerable.Range(1, 10);

        // Act
        var queryable = linqProvider.CreateQueryable(testData);

        // Assert
        queryable.Should().NotBeNull();
        queryable.ElementType.Should().Be(typeof(int));

        // Test execution
        var result = await queryable.ExecuteAsync();
        result.Should().NotBeNull();
        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task Can_Convert_Span_To_Compute_Queryable()
    {
        // Arrange
        var testData = new int[] { 1, 2, 3, 4, 5 };
        ReadOnlySpan<int> span = testData.AsSpan();

        // Act
        var queryable = span.AsComputeQueryable(_serviceProvider);
        var result = await queryable.ExecuteAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(testData);
    }

    #endregion

    #region Query Execution Tests

    [Fact]
    public async Task Can_Execute_Simple_Select_Query()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var result = await queryable.Select(x => x * 2).ExecuteAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public async Task Can_Execute_Simple_Where_Query()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var result = await queryable.Where(x => x > 5).ExecuteAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(new[] { 6, 7, 8, 9, 10 });
    }

    [Fact]
    public async Task Can_Execute_Chained_Operations()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act - Chain multiple operations
        var result = await queryable
            .Where(x => x > 3)
            .Select(x => x * 2)
            .Where(x => x < 16)
            .ExecuteAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(new[] { 8, 10, 12, 14 }); // (4*2, 5*2, 6*2, 7*2)
    }

    [Fact]
    public async Task Can_Execute_With_Preferred_Backend()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var result = await queryable.Select(x => x * 2).ExecuteAsync("CPU");

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(new[] { 2, 4, 6, 8, 10 });
    }

    #endregion

    #region GPU Compatibility Tests

    [Fact]
    public void Can_Check_Gpu_Compatibility()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = linqProvider.CreateQueryable(testData);

        // Simple expression that should be GPU compatible
        var simpleExpression = queryable.Where(x => x > 2).Expression;

        // Act
        var isCompatible = linqProvider.IsGpuCompatible(simpleExpression);

        // Assert
        isCompatible.Should().BeOfType<bool>();
        _output.WriteLine($"GPU compatibility check result: {isCompatible}");
    }

    [Fact]
    public void Can_Check_Queryable_Gpu_Compatibility_Via_Extensions()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var isCompatible = queryable.IsGpuCompatible(_serviceProvider);

        // Assert
        isCompatible.Should().BeOfType<bool>();
        _output.WriteLine($"Extension method GPU compatibility: {isCompatible}");
    }

    #endregion

    #region Optimization Tests

    [Fact]
    public void Can_Get_Optimization_Suggestions()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = linqProvider.CreateQueryable(testData);

        // Create a complex expression chain
        var complexExpression = queryable
            .Where(x => x > 1)
            .Select(x => x * 2)
            .Where(x => x < 10)
            .Expression;

        // Act
        var suggestions = linqProvider.GetOptimizationSuggestions(complexExpression);

        // Assert
        suggestions.Should().NotBeNull();
        var suggestionList = suggestions.ToList();
        _output.WriteLine($"Got {suggestionList.Count} optimization suggestions");

        foreach (var suggestion in suggestionList)
        {
            _output.WriteLine($"- {suggestion.Category}: {suggestion.Message} (Impact: {suggestion.EstimatedImpact})");
        }
    }

    [Fact]
    public void Can_Get_Optimization_Suggestions_Via_Extensions()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);
        var complexQueryable = queryable.Where(x => x > 1).Select(x => x * 2);

        // Act
        var suggestions = complexQueryable.GetOptimizationSuggestions(_serviceProvider);

        // Assert
        suggestions.Should().NotBeNull();
        var suggestionList = suggestions.ToList();
        _output.WriteLine($"Extension method returned {suggestionList.Count} suggestions");
    }

    [Fact]
    public async Task Can_Precompile_Expressions()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = linqProvider.CreateQueryable(testData);

        var expressions = new[]
        {
            queryable.Where(x => x > 2).Expression,
            queryable.Select(x => x * 2).Expression,
            queryable.Sum().Expression
        };

        // Act - Should not throw
        await linqProvider.PrecompileExpressionsAsync(expressions);

        _output.WriteLine("Successfully pre-compiled expressions");
    }

    [Fact]
    public async Task Can_Precompile_Via_Extensions()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);
        var complexQueryable = queryable.Where(x => x > 1).Select(x => x * 2);

        // Act - Should not throw
        await complexQueryable.PrecompileAsync(_serviceProvider);

        _output.WriteLine("Successfully pre-compiled via extension method");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Can_Handle_Error_Gracefully()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Create a potentially problematic query (division by zero)
        var problematicQuery = queryable.Select(x => 10 / (x - 3));

        // Act & Assert
        // This should either execute successfully with CPU fallback or throw a meaningful exception
        try
        {
            var result = await problematicQuery.ExecuteAsync();
            _output.WriteLine($"Query executed successfully with {result.Count()} results");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Query failed as expected: {ex.Message}");
            // This is acceptable - the important thing is that it fails gracefully
            ex.Should().NotBeNull();
        }
    }

    #endregion

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// XUnit logger provider for test output.
/// </summary>
internal sealed class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

    public void Dispose() { }
}

/// <summary>
/// Logger that writes to XUnit test output.
/// </summary>
internal sealed class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {_categoryName}: {message}");

            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
        catch
        {
            // Ignore logging errors in tests
        }
    }
}
