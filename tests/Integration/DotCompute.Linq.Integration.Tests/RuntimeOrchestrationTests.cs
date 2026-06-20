// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using DotCompute.Linq;            // AsComputeQueryable / ComputeSelect / ComputeWhere / ToComputeArray
using DotCompute.Linq.Extensions; // AddDotComputeLinq / IComputeLinqProvider
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

// NOTE: this test type is deliberately placed OUTSIDE the "DotCompute.Linq.*" namespace tree.
// A test namespace beginning with "DotCompute.Linq." shadows the product's "DotCompute.Linq"
// namespace for unqualified lookups, which hides IComputeLinqProvider / AddDotComputeLinq and the
// AsComputeQueryable extension methods. Using a separate namespace keeps `using DotCompute.Linq;`
// effective so the real shipped API resolves cleanly.
namespace DotCompute.Tests.Linq.Integration.Orchestration;

/// <summary>
/// Integration tests for the shipped LINQ provider surface
/// (<see cref="ServiceCollectionExtensions.AddDotComputeLinq"/>,
/// <see cref="IComputeLinqProvider.CreateComputeQueryable{T}"/> and the
/// <c>AsComputeQueryable</c> / <c>ComputeSelect</c> / <c>ComputeWhere</c> / <c>ToComputeArray</c>
/// extension pipeline). These exercise the real query provider end-to-end, which compiles
/// the expression tree and executes it with GPU acceleration where available and a CPU
/// fallback otherwise.
/// </summary>
/// <remarks>
/// The previous version of this file targeted an aspirational async LINQ orchestration API
/// (RuntimeIntegratedLinqProvider / queryable.ExecuteAsync() / PrecompileExpressionsAsync /
/// ValidateLinqRuntimeIntegration) that was never implemented in the shipping product — only
/// the synchronous compile-and-execute pipeline below exists. The tests were retargeted to the
/// real surface so they provide genuine, executable integration coverage.
/// </remarks>
public sealed class RuntimeOrchestrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public RuntimeOrchestrationTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Registers IComputeLinqProvider -> ComputeLinqProvider.
        services.AddDotComputeLinq();

        _serviceProvider = services.BuildServiceProvider();
    }

    #region Service Provider Configuration Tests

    [Fact]
    public void AddDotComputeLinq_Registers_ComputeLinqProvider()
    {
        var provider = _serviceProvider.GetService<IComputeLinqProvider>();

        provider.Should().NotBeNull("AddDotComputeLinq should register IComputeLinqProvider");
    }

    [Fact]
    public void AddDotComputeLinq_Is_Idempotent()
    {
        // TryAddSingleton is used internally, so calling twice must not register a duplicate.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotComputeLinq();
        services.AddDotComputeLinq();

        services.Count(d => d.ServiceType == typeof(IComputeLinqProvider))
            .Should().Be(1, "AddDotComputeLinq should be idempotent");
    }

    #endregion

    #region Queryable Creation Tests

    [Fact]
    public void CreateComputeQueryable_From_Array_Produces_Typed_Queryable()
    {
        var provider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var data = new[] { 1, 2, 3, 4, 5 };

        var queryable = provider.CreateComputeQueryable(data);

        queryable.Should().NotBeNull();
        queryable.ElementType.Should().Be<int>();
        queryable.ToComputeArray().Should().Equal(data);
    }

    [Fact]
    public void CreateComputeQueryable_From_Enumerable_Materializes_All_Elements()
    {
        var provider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();
        var data = Enumerable.Range(1, 10);

        var queryable = provider.CreateComputeQueryable(data);
        var result = queryable.ToComputeArray();

        result.Should().HaveCount(10);
        result.Should().Equal(Enumerable.Range(1, 10).ToArray());
    }

    [Fact]
    public void AsComputeQueryable_RoundTrips_Source_Data()
    {
        var data = new[] { 1, 2, 3, 4, 5 };

        var result = data.AsQueryable().AsComputeQueryable().ToComputeArray();

        result.Should().Equal(data);
    }

    #endregion

    #region Query Execution Tests

    [Fact]
    public void ComputeSelect_Doubles_Each_Element()
    {
        var data = new[] { 1, 2, 3, 4, 5 };

        var result = data.AsQueryable()
            .AsComputeQueryable()
            .ComputeSelect(x => x * 2)
            .ToComputeArray();

        result.Should().Equal(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public void ComputeWhere_Filters_Elements()
    {
        var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var result = data.AsQueryable()
            .AsComputeQueryable()
            .ComputeWhere(x => x > 5)
            .ToComputeArray();

        result.Should().Equal(new[] { 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Chained_Where_Select_Where_Produces_Expected_Result()
    {
        var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var result = data.AsQueryable()
            .AsComputeQueryable()
            .ComputeWhere(x => x > 3)
            .ComputeSelect(x => x * 2)
            .ComputeWhere(x => x < 16)
            .ToComputeArray();

        // (4,5,6,7) -> (8,10,12,14), all < 16
        result.Should().Equal(new[] { 8, 10, 12, 14 });
    }

    [Fact]
    public void Float_Select_Executes_Through_Pipeline()
    {
        var data = new[] { 1.0f, 2.0f, 3.0f, 4.0f };

        var result = data.AsQueryable()
            .AsComputeQueryable()
            .ComputeSelect(x => x * 1.5f)
            .ToComputeArray();

        result.Should().Equal(new[] { 1.5f, 3.0f, 4.5f, 6.0f });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Empty_Source_Produces_Empty_Result()
    {
        var data = Array.Empty<int>();

        var result = data.AsQueryable()
            .AsComputeQueryable()
            .ComputeSelect(x => x + 1)
            .ToComputeArray();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Integer_Division_RoundTrips_When_Denominator_NonZero()
    {
        var data = new[] { 10, 20, 30, 40 };

        var result = data.AsQueryable()
            .AsComputeQueryable()
            .ComputeSelect(x => x / 2)
            .ToComputeArray();

        result.Should().Equal(new[] { 5, 10, 15, 20 });
    }

    [Fact]
    public void CreateComputeQueryable_Null_Source_Throws()
    {
        var provider = _serviceProvider.GetRequiredService<IComputeLinqProvider>();

        var act = () => provider.CreateComputeQueryable<int>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    public void Dispose() => _serviceProvider.Dispose();
}

/// <summary>
/// XUnit logger provider for test output.
/// </summary>
internal sealed class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output) => _output = output;

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
            // Ignore logging errors in tests.
        }
    }
}
