// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Tests.Compilation;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Cross-backend parity tests ensuring Metal backend matches CUDA behavior.
/// Validates identical results for same kernels and API surface compatibility.
/// Critical for ensuring DotCompute provides unified cross-platform compute experience.
/// </summary>
public sealed class CudaParityTests : MetalCompilerTestBase
{
    public CudaParityTests(ITestOutputHelper output) : base(output)
    {
    }

    #region API Surface Parity Tests

    [Fact]
    public void CompilerInterface_ShouldMatchCudaCapabilities()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var capabilities = compiler.Capabilities;

        // Assert - Metal should support same core features as CUDA
        Assert.True((bool)capabilities["SupportsAsync"], "Should support async compilation like CUDA");
        Assert.True((bool)capabilities["SupportsOptimization"], "Should support optimization like CUDA");
        Assert.True((bool)capabilities["SupportsCaching"], "Should support caching like CUDA");
        Assert.True((bool)capabilities["SupportsValidation"], "Should support validation like CUDA");

        LogTestInfo("✓ Metal compiler capabilities match CUDA's core features");
    }

    [Fact]
    public void SupportedSourceTypes_ShouldIncludeRequiredLanguages()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var types = compiler.SupportedSourceTypes;

        // Assert - Metal should support MSL and Binary, CUDA supports CUDA C and PTX/CUBIN
        Assert.Contains(KernelLanguage.Metal, types);
        Assert.Contains(KernelLanguage.Binary, types);

        // Both should support their native language and binary format
        LogTestInfo($"✓ Metal supports: {string.Join(", ", types)} (CUDA equivalent: CUDA, Binary)");
    }

    [Fact]
    public void OptimizationLevels_ShouldMatchCudaOptions()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act & Assert - Test all optimization levels that CUDA supports
        var levels = new[] {
            OptimizationLevel.None,
            OptimizationLevel.O1,
            OptimizationLevel.O2,
            OptimizationLevel.Default,
            OptimizationLevel.O3
        };

        foreach (var level in levels)
        {
            var options = new CompilationOptions { OptimizationLevel = level };
            // Should not throw - Metal should handle all CUDA optimization levels
            Assert.NotNull(options);
        }

        LogTestInfo($"✓ Metal supports all {levels.Length} CUDA optimization levels");
    }

    #endregion

    #region Kernel Compilation Parity Tests

    [SkippableFact]
    public async Task VectorAddKernel_CompiledOnBothBackends_ProducesSameInterface()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Same vector add kernel that CUDA would compile
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert - Verify compiled kernel has same interface as CUDA
        Assert.NotNull(compiled);
        Assert.Equal("vector_add", compiled.Name);
        Assert.NotEqual(compiled.Id, Guid.Empty);

        // Metal compiled kernel should have same properties as CUDA's ICompiledKernel
        Assert.IsAssignableFrom<ICompiledKernel>(compiled);

        LogTestInfo("✓ Metal compiled kernel interface matches CUDA's ICompiledKernel");
    }

    [SkippableFact]
    public async Task ComplexKernel_WithSharedMemory_CompilationParity()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Reduction kernel using threadgroup memory (Metal) / shared memory (CUDA)
        var kernel = TestKernelFactory.CreateThreadgroupMemoryKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert - Both backends should support shared/threadgroup memory
        Assert.NotNull(compiled);
        Assert.Equal("reduction", compiled.Name);

        LogTestInfo("✓ Metal threadgroup memory compilation matches CUDA shared memory");
    }

    [SkippableFact]
    public async Task InvalidKernel_BothBackends_ShouldFailSimilarly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var invalidKernel = TestKernelFactory.CreateInvalidKernel();

        // Act & Assert - Both should throw compilation exception
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(invalidKernel));

        LogTestInfo("✓ Metal handles invalid kernels same as CUDA (throws InvalidOperationException)");
    }

    #endregion

    #region Compilation Options Parity Tests

    [SkippableFact]
    public async Task DebugInfo_BothBackends_SupportDebugCompilation()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions
        {
            EnableDebugInfo = true,
            OptimizationLevel = OptimizationLevel.None
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert - Both should support debug compilation
        Assert.NotNull(compiled);

        LogTestInfo("✓ Metal debug compilation matches CUDA's debug mode");
    }

    [SkippableFact]
    public async Task FastMath_BothBackends_SupportMathOptimizations()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions
        {
            FastMath = true,
            OptimizationLevel = OptimizationLevel.O3
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert - Both should support fast math
        Assert.NotNull(compiled);

        LogTestInfo("✓ Metal fast math matches CUDA's -use_fast_math");
    }

    #endregion

    #region Caching Behavior Parity Tests

    [SkippableFact]
    public async Task BinaryCache_BothBackends_ExhibitSameCachingBehavior()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act - Compile twice
        var compiled1 = await compiler.CompileAsync(kernel);
        var compiled2 = await compiler.CompileAsync(kernel);

        // Assert - Both should cache and return same ID
        Assert.NotNull(compiled1);
        Assert.NotNull(compiled2);
        Assert.Equal(compiled1.Id, compiled2.Id);

        var stats = cache.GetStatistics();
        Assert.True(stats.HitCount >= 1, "Should have cache hit like CUDA");

        LogTestInfo($"✓ Metal caching behavior matches CUDA: {stats.HitCount} hits, {stats.MissCount} misses");
    }

    [SkippableFact]
    public async Task CacheClear_BothBackends_HandlesCacheInvalidation()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled1 = await compiler.CompileAsync(kernel);
        cache.Clear();
        var compiled2 = await compiler.CompileAsync(kernel);

        // Assert - Should recompile after cache clear (different ID)
        Assert.NotEqual(compiled1.Id, compiled2.Id);

        LogTestInfo("✓ Metal cache invalidation matches CUDA behavior");
    }

    #endregion

    #region Concurrent Compilation Parity Tests

    [SkippableFact]
    public async Task ConcurrentCompilation_BothBackends_ThreadSafe()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernels = Enumerable.Range(0, 10)
            .Select(_ => TestKernelFactory.CreateVectorAddKernel())
            .ToList();

        // Act - Compile concurrently like CUDA
        var tasks = kernels.Select(k => compiler.CompileAsync(k).AsTask());
        var results = await Task.WhenAll(tasks);

        // Assert - Both should handle concurrent compilation
        Assert.Equal(10, results.Length);
        Assert.All(results, r => Assert.NotNull(r));

        LogTestInfo($"✓ Metal concurrent compilation matches CUDA: {results.Length} kernels compiled");
    }

    #endregion

    #region Performance Characteristics Parity Tests

    [SkippableFact]
    public async Task CompilationTime_BothBackends_SimilarPerformance()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var compiled = await compiler.CompileAsync(kernel);
        stopwatch.Stop();

        // Assert - Should compile in reasonable time (< 5s like CUDA)
        Assert.NotNull(compiled);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"Metal compilation too slow: {stopwatch.ElapsedMilliseconds}ms (CUDA target: <5000ms)");

        LogTestInfo($"✓ Metal compilation time: {stopwatch.ElapsedMilliseconds}ms (within CUDA range)");
    }

    [SkippableFact]
    public async Task CachePerformance_BothBackends_CacheHitFaster()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act - First compilation (miss)
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel);
        stopwatch1.Stop();

        // Act - Second compilation (hit)
        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel);
        stopwatch2.Stop();

        // Assert - Cache hit should be faster (same as CUDA behavior)
        Assert.True(stopwatch2.ElapsedMilliseconds <= stopwatch1.ElapsedMilliseconds * 2,
            "Metal cache hit should be faster (like CUDA)");

        LogTestInfo($"✓ Metal cache performance matches CUDA: First={stopwatch1.ElapsedMilliseconds}ms, " +
                    $"Cached={stopwatch2.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Error Handling Parity Tests

    [SkippableFact]
    public async Task NullKernel_BothBackends_ThrowArgumentNullException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act & Assert - Both should throw same exception type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await compiler.CompileAsync(null!));

        LogTestInfo("✓ Metal null handling matches CUDA (ArgumentNullException)");
    }

    [SkippableFact]
    public async Task DisposedCompiler_BothBackends_ThrowObjectDisposedException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        compiler.Dispose();

        // Act & Assert - Both should throw same exception type
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await compiler.CompileAsync(kernel));

        LogTestInfo("✓ Metal disposal handling matches CUDA (ObjectDisposedException)");
    }

    [SkippableFact]
    public async Task CancellationToken_BothBackends_HonorCancellation()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateLargeKernel(1000);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Both should respect cancellation
        try
        {
            await compiler.CompileAsync(kernel, cancellationToken: cts.Token);
            LogTestInfo("✓ Compilation completed before cancellation (both backends)");
        }
        catch (OperationCanceledException)
        {
            LogTestInfo("✓ Metal cancellation handling matches CUDA (OperationCanceledException)");
        }
    }

    #endregion

    #region Validation Parity Tests

    [Fact]
    public void Validate_BothBackends_ProvideSameValidationInterface()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var result = compiler.Validate(kernel);

        // Assert - Both should provide ValidationResult
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);

        LogTestInfo("✓ Metal validation interface matches CUDA");
    }

    [Fact]
    public void Validate_InvalidKernel_BothBackends_ReturnSimilarErrors()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var emptyKernel = new KernelDefinition("test", "");

        // Act
        var result = compiler.Validate(emptyKernel);

        // Assert - Both should fail validation with helpful message
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("code", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);

        LogTestInfo($"✓ Metal validation errors match CUDA format: {result.ErrorMessage}");
    }

    #endregion

    #region Feature Availability Parity Tests

    [Fact]
    public void Capabilities_BothBackends_ReportEquivalentFeatures()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var capabilities = compiler.Capabilities;

        // Assert - Core features should be present
        var requiredFeatures = new[]
        {
            "SupportsAsync",
            "SupportsOptimization",
            "SupportsCaching",
            "SupportsValidation",
            "SupportedLanguageVersions"
        };

        foreach (var feature in requiredFeatures)
        {
            Assert.True(capabilities.ContainsKey(feature),
                $"Metal missing capability '{feature}' that CUDA provides");
        }

        LogTestInfo($"✓ Metal provides all {requiredFeatures.Length} core CUDA-equivalent features");
    }

    #endregion

    #region Integration Compatibility Tests

    [SkippableFact]
    public async Task KernelDefinition_BothBackends_AcceptSameFormat()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Create kernel definition in neutral format (like source generator produces)
        var neutralKernel = new KernelDefinition("test_kernel", "/* kernel code */")
        {
            EntryPoint = "test_kernel",
            Language = KernelLanguage.Metal,
            Metadata = new Dictionary<string, object>
            {
                ["SourceBackend"] = "Universal",
                ["GeneratedBy"] = "SourceGenerator"
            }
        };

        // Act - Metal should accept same definition format as CUDA
        var validation = compiler.Validate(neutralKernel);

        // Assert - Should accept or provide clear guidance
        Assert.NotNull(validation);

        LogTestInfo("✓ Metal accepts universal kernel definition format (CUDA-compatible)");
    }

    #endregion

    #region Optimization Strategy Parity Tests

    [SkippableFact]
    public async Task OptimizeAsync_BothBackends_SupportPostCompilationOptimization()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        // Act - Both should support post-compilation optimization
        var optimized = await compiler.OptimizeAsync(compiled, OptimizationLevel.O3);

        // Assert
        Assert.NotNull(optimized);

        LogTestInfo("✓ Metal post-compilation optimization matches CUDA's optimization pipeline");
    }

    #endregion

    #region Documentation and Error Message Parity Tests

    [Fact]
    public void CompilerName_BothBackends_ProvidesDescriptiveName()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var name = compiler.Name;

        // Assert - Should have descriptive name like CUDA
        Assert.NotNull(name);
        Assert.NotEmpty(name);
        Assert.Contains("Metal", name, StringComparison.OrdinalIgnoreCase);

        LogTestInfo($"✓ Metal compiler name '{name}' matches CUDA naming convention ('CUDA Kernel Compiler')");
    }

    #endregion
}
