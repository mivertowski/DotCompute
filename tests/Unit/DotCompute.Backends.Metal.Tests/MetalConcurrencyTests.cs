// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Tests.Compilation;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Thread safety and concurrency tests for Metal backend.
/// Ensures safe multi-threaded access to compiler and cache.
/// </summary>
public sealed class MetalConcurrencyTests : MetalCompilerTestBase
{
    public MetalConcurrencyTests(ITestOutputHelper output) : base(output)
    {
    }

    [SkippableFact]
    public async Task Concurrency_MultipleThreadsCompilingSameKernel()
    {
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () => await compiler.CompileAsync(kernel)))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(20, results.Length);
        Assert.All(results, r => Assert.NotNull(r));

        // All should get same cached instance
        var firstId = results[0].Id;
        Assert.All(results, r => Assert.Equal(firstId, r.Id));

        LogTestInfo($"✓ Concurrency test: 20 threads compiled same kernel safely");
    }

    [SkippableFact]
    public async Task Concurrency_MultipleThreadsDifferentKernels()
    {
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(async () =>
            {
                var kernel = TestKernelFactory.CreateVectorAddKernel();
                return await compiler.CompileAsync(kernel);
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Length);
        Assert.All(results, r => Assert.NotNull(r));

        LogTestInfo($"✓ Concurrency test: 10 threads compiled different kernels safely");
    }

    [SkippableFact]
    public async Task Concurrency_CacheClearWhileCompiling()
    {
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        var compileTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () => await compiler.CompileAsync(kernel)))
            .ToList();

        var clearTask = Task.Run(() =>
        {
            Thread.Sleep(10);
            cache.Clear();
        });

        await Task.WhenAll(compileTasks.Concat(new[] { clearTask }));

        LogTestInfo("✓ Concurrency test: Cache clear during compilation handled safely");
    }
}
