// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Tests.Compilation;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Stress tests for Metal backend stability under heavy load.
/// Validates reliability and resource management.
/// </summary>
public sealed class MetalStressTests : MetalCompilerTestBase
{
    public MetalStressTests(ITestOutputHelper output) : base(output)
    {
    }

    [SkippableFact]
    public async Task Stress_ManySequentialCompilations()
    {
        RequireMetalSupport();
        var compiler = CreateCompiler();
        const int iterations = 100;

        for (int i = 0; i < iterations; i++)
        {
            var kernel = TestKernelFactory.CreateVectorAddKernel();
            var compiled = await compiler.CompileAsync(kernel);
            Assert.NotNull(compiled);
        }

        LogTestInfo($"✓ Stress test: {iterations} sequential compilations completed");
    }

    [SkippableFact]
    public async Task Stress_ManyConcurrentCompilations()
    {
        RequireMetalSupport();
        var compiler = CreateCompiler();
        const int concurrentCount = 50;

        var kernels = Enumerable.Range(0, concurrentCount)
            .Select(_ => TestKernelFactory.CreateVectorAddKernel())
            .ToList();

        var tasks = kernels.Select(k => compiler.CompileAsync(k).AsTask());
        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrentCount, results.Length);
        Assert.All(results, r => Assert.NotNull(r));

        LogTestInfo($"✓ Stress test: {concurrentCount} concurrent compilations completed");
    }

    [SkippableFact]
    public async Task Stress_LargeKernelCompilation()
    {
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var largeKernel = TestKernelFactory.CreateLargeKernel(500);

        var compiled = await compiler.CompileAsync(largeKernel);
        Assert.NotNull(compiled);

        LogTestInfo("✓ Stress test: Large kernel (500+ ops) compiled successfully");
    }
}
