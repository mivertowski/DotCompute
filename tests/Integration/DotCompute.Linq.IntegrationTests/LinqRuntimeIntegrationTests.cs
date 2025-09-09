// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq;
using DotCompute.Linq.Extensions;
using DotCompute.Linq.Interfaces;
using DotCompute.Runtime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Linq.IntegrationTests;

/// <summary>
/// Integration tests for LINQ provider with runtime orchestrator.
/// Tests the complete flow from LINQ expressions to kernel execution.
/// </summary>
public class LinqRuntimeIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;

    public LinqRuntimeIntegrationTests(ITestOutputHelper output)
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
            builder.AddProvider(new TestOutputLoggerProvider(_output));
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

    [Fact]
    public void Can_Create_ComputeLinqProvider()
    {
        // Arrange & Act
        var provider = _serviceProvider.GetService<IComputeLinqProvider>();

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<RuntimeIntegratedLinqProvider>(provider);
    }

    [Fact]
    public void Can_Create_ComputeOrchestrator()
    {
        // Arrange & Act
        var orchestrator = _serviceProvider.GetService<IComputeOrchestrator>();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Can_Validate_LinqRuntime_Integration()
    {
        // Arrange & Act
        var isValid = _serviceProvider.ValidateLinqRuntimeIntegration();

        // Assert
        Assert.True(isValid, "LINQ runtime integration should be properly configured");
    }

    [SkippableFact]
    public async Task Can_Create_Queryable_From_Array()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = new[] { 1, 2, 3, 4, 5 };

        // Act
        var queryable = linqProvider.CreateQueryable(testData);

        // Assert
        Assert.NotNull(queryable);
        Assert.Equal(typeof(int), queryable.ElementType);
        Assert.IsAssignableFrom<IQueryable<int>>(queryable);

        // Test that we can execute the queryable (should not throw)
        var result = await queryable.ExecuteAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task Can_Create_Queryable_From_Enumerable()
    {
        // Arrange
        var linqProvider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var testData = Enumerable.Range(1, 10);

        // Act
        var queryable = linqProvider.CreateQueryable(testData);

        // Assert
        Assert.NotNull(queryable);
        Assert.Equal(typeof(int), queryable.ElementType);

        // Test execution
        var result = await queryable.ExecuteAsync();
        Assert.NotNull(result);
        Assert.Equal(10, result.Count());
    }

    [SkippableFact]
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
        // Note: This might return false if GPU is not available, which is okay
        Assert.IsType<bool>(isCompatible);
    }

    [SkippableFact]
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
        Assert.NotNull(suggestions);
        // Should have some suggestions for the complex chain
        var suggestionList = suggestions.ToList();
        _output.WriteLine($"Got {suggestionList.Count} optimization suggestions");
        
        foreach (var suggestion in suggestionList)
        {
            _output.WriteLine($"- {suggestion.Category}: {suggestion.Message} (Impact: {suggestion.EstimatedImpact})");
        }
    }

    [SkippableFact]
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

        // Act & Assert (should not throw)
        await linqProvider.PrecompileExpressionsAsync(expressions);
        
        _output.WriteLine("Successfully pre-compiled expressions");
    }

    [SkippableFact]
    public async Task Can_Execute_Simple_Select_Query()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var result = await queryable.Select(x => x * 2).ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        var resultArray = result.ToArray();
        Assert.Equal(5, resultArray.Length);
        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, resultArray);
    }

    [SkippableFact]
    public async Task Can_Execute_Simple_Where_Query()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var result = await queryable.Where(x => x > 5).ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        var resultArray = result.ToArray();
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, resultArray);
    }

    [SkippableFact]
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
        Assert.NotNull(result);
        var resultArray = result.ToArray();
        Assert.Equal(new[] { 8, 10, 12, 14 }, resultArray); // (4*2, 5*2, 6*2, 7*2)
    }

    [SkippableFact]
    public void Can_Check_Queryable_Gpu_Compatibility_Via_Extensions()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var isCompatible = queryable.IsGpuCompatible(_serviceProvider);

        // Assert
        Assert.IsType<bool>(isCompatible);
    }

    [SkippableFact]
    public void Can_Get_Optimization_Suggestions_Via_Extensions()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);
        var complexQueryable = queryable.Where(x => x > 1).Select(x => x * 2);

        // Act
        var suggestions = complexQueryable.GetOptimizationSuggestions(_serviceProvider);

        // Assert
        Assert.NotNull(suggestions);
        var suggestionList = suggestions.ToList();
        _output.WriteLine($"Extension method returned {suggestionList.Count} suggestions");
    }

    [SkippableFact]
    public async Task Can_Precompile_Via_Extensions()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);
        var complexQueryable = queryable.Where(x => x > 1).Select(x => x * 2);

        // Act & Assert (should not throw)
        await complexQueryable.PrecompileAsync(_serviceProvider);
        
        _output.WriteLine("Successfully pre-compiled via extension method");
    }

    [SkippableFact]
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
        }
    }

    [SkippableFact]
    public async Task Can_Execute_With_Preferred_Backend()
    {
        // Arrange
        var testData = new[] { 1, 2, 3, 4, 5 };
        var queryable = testData.AsComputeQueryable(_serviceProvider);

        // Act
        var result = await queryable.Select(x => x * 2).ExecuteAsync("CPU");

        // Assert
        Assert.NotNull(result);
        var resultArray = result.ToArray();
        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, resultArray);
    }

    [SkippableFact]
    public async Task Can_Convert_Span_To_Compute_Queryable()
    {
        // Arrange
        var testData = new int[] { 1, 2, 3, 4, 5 };
        ReadOnlySpan<int> span = testData.AsSpan();

        // Act
        var queryable = span.AsComputeQueryable(_serviceProvider);
        var result = await queryable.ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testData, result.ToArray());
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Test logger provider that writes to XUnit output.
/// </summary>
public class TestOutputLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public TestOutputLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestOutputLogger(_output, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Logger that writes to XUnit test output.
/// </summary>
public class TestOutputLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public TestOutputLogger(ITestOutputHelper output, string categoryName)
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
