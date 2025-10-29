// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Tests.Compilation;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Performance benchmark tests for Metal backend compilation and execution.
/// Validates performance targets and identifies regressions.
/// </summary>
public sealed class MetalPerformanceBenchmarkTests : MetalCompilerTestBase
{
    public MetalPerformanceBenchmarkTests(ITestOutputHelper output) : base(output)
    {
    }

    [SkippableFact]
    public async Task Benchmark_SimpleKernel_CompilationTime()
    {
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        var times = new List<long>();
        for (int i = 0; i < 5; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await compiler.CompileAsync(kernel);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }

        var avgTime = times.Average();
        var maxTime = times.Max();

        Assert.True(maxTime < 5000, $"Compilation took too long: {maxTime}ms");

        LogTestInfo($"✓ Compilation benchmark: Avg={avgTime:F2}ms, Max={maxTime}ms");
    }

    [SkippableFact]
    public async Task Benchmark_CachePerformance()
    {
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel);
        sw1.Stop();

        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel);
        sw2.Stop();

        var speedup = (double)sw1.ElapsedMilliseconds / sw2.ElapsedMilliseconds;

        LogTestInfo($"✓ Cache speedup: {speedup:F2}x (First={sw1.ElapsedMilliseconds}ms, Cached={sw2.ElapsedMilliseconds}ms)");
    }

    [SkippableFact]
    public async Task Benchmark_ConcurrentCompilation()
    {
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernels = Enumerable.Range(0, 10).Select(_ => TestKernelFactory.CreateVectorAddKernel()).ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = kernels.Select(k => compiler.CompileAsync(k).AsTask());
        await Task.WhenAll(tasks);
        sw.Stop();

        var avgPerKernel = sw.ElapsedMilliseconds / (double)kernels.Count;

        LogTestInfo($"✓ Concurrent compilation: {kernels.Count} kernels in {sw.ElapsedMilliseconds}ms (avg={avgPerKernel:F2}ms/kernel)");
    }
}
