// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Linq.Tests;

/// <summary>
/// Phase 8 (v1.0.0): End-to-end integration tests for Join, GroupBy, and OrderBy operators
/// on the CPU SIMD backend. Validates that the generators produce correct results when
/// compiled and run through the full RuntimeExecutor pipeline.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "JoinGroupByOrderBy")]
public sealed class JoinGroupByOrderByIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly RuntimeExecutor _executor;

    public JoinGroupByOrderByIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _executor = new RuntimeExecutor();
    }

    #region OrderBy - CPU Execution Tests

    [Fact]
    public async Task OrderBy_AscendingIntegers_SortedCorrectly()
    {
        var input = new[] { 5, 2, 8, 1, 9, 3, 7, 4, 6 };
        Func<int[], int[]> kernel = data => data.OrderBy(x => x).ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, results);
    }

    [Fact]
    public async Task OrderBy_Descending_ReverseOrder()
    {
        var input = new[] { 5, 2, 8, 1, 9, 3, 7, 4, 6 };
        Func<int[], int[]> kernel = data => data.OrderByDescending(x => x).ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 }, results);
    }

    [Fact]
    public async Task OrderBy_EmptyInput_ReturnsEmpty()
    {
        var input = Array.Empty<int>();
        Func<int[], int[]> kernel = data => data.OrderBy(x => x).ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Empty(results);
    }

    [Fact]
    public async Task OrderBy_AlreadySorted_IdempotentResult()
    {
        var input = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        Func<int[], int[]> kernel = data => data.OrderBy(x => x).ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(input, results);
    }

    [Fact]
    public async Task OrderBy_Floats_SortedCorrectly()
    {
        var input = new[] { 3.14f, 2.71f, 1.41f, 1.73f, 1.62f };
        Func<float[], float[]> kernel = data => data.OrderBy(x => x).ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 1.41f, 1.62f, 1.73f, 2.71f, 3.14f }, results);
    }

    [Fact]
    public async Task OrderBy_LargeArray_SortedCorrectly()
    {
        var random = new Random(42);
        var input = Enumerable.Range(0, 10_000).Select(_ => random.Next(0, 1_000_000)).ToArray();
        var expected = input.OrderBy(x => x).ToArray();

        Func<int[], int[]> kernel = data => data.OrderBy(x => x).ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(expected, results);
        _output.WriteLine($"Sorted {input.Length} elements in {metrics.ExecutionTime.TotalMilliseconds}ms");
    }

    #endregion

    #region GroupBy - CPU Execution Tests

    [Fact]
    public async Task GroupBy_Count_TallyPerKey()
    {
        var input = new[] { 0, 1, 2, 3, 4, 5, 6, 1, 4 };
        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x % 3)
            .OrderBy(g => g.Key)
            .Select(g => g.Count())
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 3, 4, 2 }, results);
    }

    [Fact]
    public async Task GroupBy_Sum_AccumulatesByKey()
    {
        var input = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x % 2)
            .OrderBy(g => g.Key)
            .Select(g => g.Sum())
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 20, 25 }, results);
    }

    [Fact]
    public async Task GroupBy_Min_FindsMinimumPerGroup()
    {
        var input = new[] { 5, 1, 7, 3, 9, 2, 8, 4, 6 };
        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x % 2)
            .OrderBy(g => g.Key)
            .Select(g => g.Min())
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 2, 1 }, results);
    }

    [Fact]
    public async Task GroupBy_Max_FindsMaximumPerGroup()
    {
        var input = new[] { 5, 1, 7, 3, 9, 2, 8, 4, 6 };
        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x % 2)
            .OrderBy(g => g.Key)
            .Select(g => g.Max())
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 8, 9 }, results);
    }

    [Fact]
    public async Task GroupBy_EmptyInput_ReturnsEmpty()
    {
        var input = Array.Empty<int>();
        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x % 3)
            .Select(g => g.Count())
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GroupBy_AllSameKey_OneGroup()
    {
        var input = new[] { 42, 42, 42, 42, 42 };
        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x)
            .Select(g => g.Count())
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Single(results);
        Assert.Equal(5, results[0]);
    }

    #endregion

    #region Join - CPU Execution Tests

    [Fact]
    public async Task Join_InnerJoin_MatchesOnKey()
    {
        var outer = new[] { 1, 2, 3, 4, 5 };
        var inner = new[] { 3, 4, 5, 6, 7 };

        Func<int[], int[]> kernel = data =>
        {
            return data
                .Join(inner, o => o, i => i, (o, i) => o * 10 + i)
                .ToArray();
        };

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, outer, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Equal(new[] { 33, 44, 55 }, results.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Join_EmptyInput_ReturnsEmpty()
    {
        var input = Array.Empty<int>();
        var other = new[] { 1, 2, 3 };

        Func<int[], int[]> kernel = data => data
            .Join(other, o => o, i => i, (o, i) => o + i)
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Join_NoMatches_ReturnsEmpty()
    {
        var input = new[] { 1, 2, 3 };
        var other = new[] { 4, 5, 6 };

        Func<int[], int[]> kernel = data => data
            .Join(other, o => o, i => i, (o, i) => o + i)
            .ToArray();

        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.True(metrics.Success);
        Assert.Empty(results);
    }

    #endregion

    #region Cross-Validation Tests

    [Fact]
    public async Task OrderBy_CrossValidation_MatchesLinq()
    {
        var random = new Random(2026);
        var input = Enumerable.Range(0, 1000).Select(_ => random.Next(0, 10000)).ToArray();
        var expected = input.OrderBy(x => x).ToArray();

        Func<int[], int[]> kernel = data => data.OrderBy(x => x).ToArray();

        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.Equal(expected, results);
    }

    [Fact]
    public async Task GroupBy_CrossValidation_MatchesLinqCount()
    {
        var input = Enumerable.Range(0, 500).ToArray();
        var expected = input
            .GroupBy(x => x % 7)
            .OrderBy(g => g.Key)
            .Select(g => g.Count())
            .ToArray();

        Func<int[], int[]> kernel = data => data
            .GroupBy(x => x % 7)
            .OrderBy(g => g.Key)
            .Select(g => g.Count())
            .ToArray();

        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        Assert.Equal(expected, results);
    }

    #endregion

    public void Dispose()
    {
        _executor?.Dispose();
    }
}
