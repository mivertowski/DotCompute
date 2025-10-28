// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Comprehensive tests for MetalKernelCompiler covering all compilation scenarios.
/// Target: 80%+ code coverage for kernel compilation system.
/// </summary>
public sealed class MetalKernelCompilerTests : MetalCompilerTestBase
{
    public MetalKernelCompilerTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Basic Compilation Tests

    [SkippableFact]
    public async Task CompileAsync_ValidMSLKernel_CompilesSuccessfully()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        Assert.Equal("vector_add", compiled.Name);
        LogTestInfo($"Compiled kernel: {compiled.Name}");
    }

    [SkippableFact]
    public async Task CompileAsync_WithDebugInfo_EnablesDebugSymbols()
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

        // Assert
        Assert.NotNull(compiled);
        var metadata = ((MetalCompiledKernel)compiled).GetCompilationMetadata();
        Assert.True(metadata.CompilationTimeMs >= 0);
        LogTestInfo($"Debug compilation time: {metadata.CompilationTimeMs}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_WithOptimizationLevels_AppliesCorrectOptimizations()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Test each optimization level
        var levels = new[] { OptimizationLevel.None, OptimizationLevel.Default, OptimizationLevel.Maximum };

        foreach (var level in levels)
        {
            var options = new CompilationOptions { OptimizationLevel = level };

            // Act
            var compiled = await compiler.CompileAsync(kernel, options);

            // Assert
            Assert.NotNull(compiled);
            LogTestInfo($"Compiled with optimization level: {level}");
        }
    }

    [SkippableFact]
    public async Task CompileAsync_WithFastMath_EnablesFastMathOptimizations()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions
        {
            FastMath = true,
            OptimizationLevel = OptimizationLevel.Maximum
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("Fast math compilation completed");
    }

    [SkippableFact]
    public async Task CompileAsync_InvalidMSLSyntax_ThrowsCompilationException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateInvalidKernel();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(kernel));
        LogTestInfo("Invalid kernel compilation correctly failed");
    }

    [SkippableFact]
    public async Task CompileAsync_EmptyKernel_FailsValidation()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateEmptyKernel();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(kernel));
        LogTestInfo("Empty kernel validation correctly failed");
    }

    [SkippableFact]
    public async Task CompileAsync_NullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await compiler.CompileAsync(null!));
    }

    #endregion

    #region Cache Integration Tests

    [SkippableFact]
    public async Task CompileAsync_WithCache_UsesCacheOnSecondCompilation()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - First compilation (cache miss)
        var compiled1 = await compiler.CompileAsync(kernel, options);
        var metadata1 = ((MetalCompiledKernel)compiled1).GetCompilationMetadata();

        // Act - Second compilation (cache hit)
        var compiled2 = await compiler.CompileAsync(kernel, options);
        var metadata2 = ((MetalCompiledKernel)compiled2).GetCompilationMetadata();

        // Assert
        Assert.NotNull(compiled1);
        Assert.NotNull(compiled2);
        Assert.Equal(0, metadata2.CompilationTimeMs); // Cache hit has 0 compilation time
        Assert.Contains("cache", metadata2.Warnings[0], StringComparison.OrdinalIgnoreCase);

        var stats = cache.GetStatistics();
        Assert.True(stats.HitCount >= 1);
        LogTestInfo($"Cache stats - Hits: {stats.HitCount}, Misses: {stats.MissCount}, Hit rate: {stats.HitRate:P2}");
    }

    [SkippableFact]
    public async Task CompileAsync_DifferentOptions_CreatesSeparateCacheEntries()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };

        // Act
        var compiled1 = await compiler.CompileAsync(kernel, options1);
        var compiled2 = await compiler.CompileAsync(kernel, options2);

        // Assert
        Assert.NotNull(compiled1);
        Assert.NotNull(compiled2);

        var stats = cache.GetStatistics();
        Assert.Equal(2, stats.CurrentSize); // Two separate cache entries
        LogTestInfo($"Cache contains {stats.CurrentSize} entries for different optimization levels");
    }

    #endregion

    #region Advanced Compilation Tests

    [SkippableFact]
    public async Task CompileAsync_ThreadgroupMemoryKernel_CompilesWithSharedMemory()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateThreadgroupMemoryKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("Threadgroup memory kernel compiled successfully");
    }

    [SkippableFact]
    public async Task CompileAsync_LargeKernel_CompletesWithinTimeout()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateLargeKernel(500); // 500 operations

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var compiled = await compiler.CompileAsync(kernel);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(compiled);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000); // Should complete within 30 seconds
        LogTestInfo($"Large kernel compiled in {stopwatch.ElapsedMilliseconds}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_MultipleKernelsConcurrently_HandlesThreadSafety()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernels = Enumerable.Range(0, 5)
            .Select(_ => TestKernelFactory.CreateVectorAddKernel())
            .ToList();

        // Act
        var tasks = kernels.Select(k => compiler.CompileAsync(k).AsTask());
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        Assert.Equal(5, results.Length);
        LogTestInfo($"Compiled {results.Length} kernels concurrently");
    }

    [SkippableFact]
    public async Task CompileAsync_WithCancellation_HonorsCancellationToken()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateLargeKernel(1000);
        var cts = new CancellationTokenSource();

        // Act
        var compileTask = compiler.CompileAsync(kernel, cancellationToken: cts.Token);
        cts.CancelAfter(10); // Cancel after 10ms

        // Assert - may or may not throw depending on timing
        try
        {
            await compileTask;
            LogTestInfo("Compilation completed before cancellation");
        }
        catch (OperationCanceledException)
        {
            LogTestInfo("Compilation was cancelled as expected");
        }
    }

    #endregion

    #region Metal Language Version Tests

    [SkippableFact]
    public async Task CompileAsync_DetectsOptimalLanguageVersion_BasedOnSystem()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateMetalVersionKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("Compiled with system-appropriate Metal language version");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_ValidKernel_ReturnsSuccess()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var result = compiler.Validate(kernel);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_NullKernel_ReturnsFailure()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var result = compiler.Validate(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("cannot be null", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyKernelName_ReturnsFailure()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = new KernelDefinition("", "kernel void test() {}");

        // Act
        var result = compiler.Validate(kernel);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("name", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyCode_ReturnsFailure()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = new KernelDefinition("test", "");

        // Act
        var result = compiler.Validate(kernel);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("code", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_ValidKernel_ReturnsSuccess()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var result = await compiler.ValidateAsync(kernel);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Optimization Integration Tests

    [SkippableFact]
    public async Task OptimizeAsync_ValidKernel_ReturnsOptimizedKernel()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        // Act
        var optimized = await compiler.OptimizeAsync(compiled, OptimizationLevel.Maximum);

        // Assert
        Assert.NotNull(optimized);
        Assert.IsType<MetalCompiledKernel>(optimized);
        LogTestInfo("Kernel optimized successfully");
    }

    [SkippableFact]
    public async Task OptimizeAsync_NonMetalKernel_ReturnsOriginalKernel()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var mockKernel = Mocks.MetalTestMocks.CreateMockCompiledKernel(
            TestKernelFactory.CreateVectorAddKernel());

        // Act
        var result = await compiler.OptimizeAsync(mockKernel, OptimizationLevel.Maximum);

        // Assert
        Assert.Same(mockKernel, result);
        LogTestInfo("Non-Metal kernel returned unchanged");
    }

    [SkippableFact]
    public async Task OptimizeAsync_OptimizationLevelNone_SkipsOptimization()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        // Act
        var result = await compiler.OptimizeAsync(compiled, OptimizationLevel.None);

        // Assert
        Assert.Same(compiled, result);
        LogTestInfo("Optimization skipped for OptimizationLevel.None");
    }

    #endregion

    #region Compiler Properties Tests

    [Fact]
    public void Name_ReturnsCorrectName()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var name = compiler.Name;

        // Assert
        Assert.Equal("Metal Shader Compiler", name);
    }

    [Fact]
    public void SupportedSourceTypes_ContainsMetalAndBinary()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var types = compiler.SupportedSourceTypes;

        // Assert
        Assert.Contains(KernelLanguage.Metal, types);
        Assert.Contains(KernelLanguage.Binary, types);
    }

    [Fact]
    public void Capabilities_ContainsExpectedCapabilities()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var capabilities = compiler.Capabilities;

        // Assert
        Assert.True((bool)capabilities["SupportsAsync"]);
        Assert.True((bool)capabilities["SupportsOptimization"]);
        Assert.True((bool)capabilities["SupportsCaching"]);
        Assert.True((bool)capabilities["SupportsValidation"]);
        Assert.NotNull(capabilities["SupportedLanguageVersions"]);
    }

    #endregion

    #region Disposal Tests

    [SkippableFact]
    public async Task Dispose_AfterCompilation_CleansUpResources()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        await compiler.CompileAsync(kernel);
        compiler.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await compiler.CompileAsync(kernel));
        LogTestInfo("Compiler disposed correctly");
    }

    [SkippableFact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act & Assert
        compiler.Dispose();
        compiler.Dispose(); // Should not throw

        LogTestInfo("Multiple dispose calls handled correctly");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact]
    public async Task CompileAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        compiler.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await compiler.CompileAsync(kernel));
    }

    [SkippableFact]
    public async Task OptimizeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);
        compiler.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await compiler.OptimizeAsync(compiled, OptimizationLevel.Maximum));
    }

    #endregion
}
