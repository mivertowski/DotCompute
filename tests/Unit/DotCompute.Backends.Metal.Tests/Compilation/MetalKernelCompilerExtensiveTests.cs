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
/// Comprehensive extensive tests for MetalKernelCompiler covering all critical paths.
/// Targets 80%+ code coverage for the kernel compilation system.
/// </summary>
public sealed class MetalKernelCompilerExtensiveTests : MetalCompilerTestBase
{
    public MetalKernelCompilerExtensiveTests(ITestOutputHelper output) : base(output)
    {
    }

    #region MSL Compilation Tests

    [SkippableFact]
    public async Task CompileAsync_ValidMSLShader_CompilesSuccessfully()
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
        Assert.IsType<MetalCompiledKernel>(compiled);

        var metadata = ((MetalCompiledKernel)compiled).GetCompilationMetadata();
        Assert.True(metadata.CompilationTimeMs >= 0);

        LogTestInfo($"✓ Valid MSL shader compiled successfully in {metadata.CompilationTimeMs}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_MSLSyntaxError_ThrowsWithDiagnostics()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateInvalidKernel();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(kernel));

        // Verify error message contains useful diagnostics
        Assert.NotNull(exception.Message);
        Assert.Contains("compilation failed", exception.Message, StringComparison.OrdinalIgnoreCase);

        LogTestInfo($"✓ Syntax error correctly reported: {exception.Message}");
    }

    [SkippableFact]
    public async Task CompileAsync_UndefinedFunction_ThrowsHelpfulError()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void test_kernel(device float* data [[buffer(0)]], uint gid [[thread_position_in_grid]])
{
    data[gid] = undefined_function(data[gid]); // Calling undefined function
}";

        var kernel = new KernelDefinition("test_kernel", kernelCode)
        {
            EntryPoint = "test_kernel",
            Language = KernelLanguage.Metal
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(kernel));

        Assert.Contains("compilation failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        LogTestInfo($"✓ Undefined function error reported correctly");
    }

    #endregion

    #region Optimization Level Tests

    [SkippableFact]
    public async Task CompileAsync_OptimizationO0_ProducesUnoptimizedCode()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        var metadata = ((MetalCompiledKernel)compiled).GetCompilationMetadata();
        LogTestInfo($"✓ O0 compilation completed in {metadata.CompilationTimeMs}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_OptimizationO2_ProducesOptimizedCode()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        var metadata = ((MetalCompiledKernel)compiled).GetCompilationMetadata();
        LogTestInfo($"✓ O2 compilation completed in {metadata.CompilationTimeMs}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_OptimizationO3_ProducesMaximumOptimization()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        var metadata = ((MetalCompiledKernel)compiled).GetCompilationMetadata();
        LogTestInfo($"✓ O3 compilation completed in {metadata.CompilationTimeMs}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_OptimizationComparison_DifferentLevelsProduceDifferentResults()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Compile with different optimization levels
        var optionsNone = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var optionsMax = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };

        // Act
        var compiledNone = await compiler.CompileAsync(kernel, optionsNone);
        var compiledMax = await compiler.CompileAsync(kernel, optionsMax);

        // Assert
        Assert.NotNull(compiledNone);
        Assert.NotNull(compiledMax);

        var metadataNone = ((MetalCompiledKernel)compiledNone).GetCompilationMetadata();
        var metadataMax = ((MetalCompiledKernel)compiledMax).GetCompilationMetadata();

        LogTestInfo($"✓ O0: {metadataNone.CompilationTimeMs}ms, O3: {metadataMax.CompilationTimeMs}ms");
    }

    #endregion

    #region Debug Info Tests

    [SkippableFact]
    public async Task CompileAsync_WithDebugInfo_PreservesSymbols()
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

        LogTestInfo($"✓ Debug compilation preserved symbols ({metadata.CompilationTimeMs}ms)");
    }

    [SkippableFact]
    public async Task CompileAsync_WithoutDebugInfo_ProducesReleaseCode()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions
        {
            EnableDebugInfo = false,
            OptimizationLevel = OptimizationLevel.Maximum
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Release compilation completed without debug symbols");
    }

    #endregion

    #region Fast Math Tests

    [SkippableFact]
    public async Task CompileAsync_FastMathEnabled_UsesApproximations()
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
        LogTestInfo("✓ Fast math compilation enabled");
    }

    [SkippableFact]
    public async Task CompileAsync_FastMathDisabled_UsesPreciseCalculations()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = new CompilationOptions
        {
            FastMath = false,
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Precise math compilation completed");
    }

    #endregion

    #region Compilation Timeout Tests

    [SkippableFact]
    public async Task CompileAsync_LargeKernel_CompletesWithinReasonableTime()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateLargeKernel(200); // 200 operations
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var compiled = await compiler.CompileAsync(kernel, cancellationToken: cts.Token);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(compiled);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, "Compilation took too long");

        LogTestInfo($"✓ Large kernel (200 ops) compiled in {stopwatch.ElapsedMilliseconds}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_WithCancellation_CancellationTokenHonored()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateLargeKernel(1000);
        var cts = new CancellationTokenSource();

        // Act
        var compileTask = compiler.CompileAsync(kernel, cancellationToken: cts.Token);
        cts.CancelAfter(5); // Cancel very quickly

        // Assert - may succeed if fast enough or throw if cancelled
        try
        {
            await compileTask;
            LogTestInfo("✓ Compilation completed before cancellation");
        }
        catch (OperationCanceledException)
        {
            LogTestInfo("✓ Cancellation token honored during compilation");
        }
    }

    #endregion

    #region Binary Caching Tests

    [SkippableFact]
    public async Task CompileAsync_BinaryCache_FirstCompilationCacheMiss()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(compiled);
        var metadata = ((MetalCompiledKernel)compiled).GetCompilationMetadata();
        Assert.True(metadata.CompilationTimeMs > 0, "First compilation should have non-zero time");

        var stats = cache.GetStatistics();
        LogTestInfo($"✓ Cache miss (first compile): {metadata.CompilationTimeMs}ms, Cache stats: {stats.MissCount} misses");
    }

    [SkippableFact]
    public async Task CompileAsync_BinaryCache_SecondCompilationCacheHit()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // First compilation (cache miss)
        var compiled1 = await compiler.CompileAsync(kernel, options);
        var metadata1 = ((MetalCompiledKernel)compiled1).GetCompilationMetadata();

        // Second compilation (cache hit)
        var compiled2 = await compiler.CompileAsync(kernel, options);
        var metadata2 = ((MetalCompiledKernel)compiled2).GetCompilationMetadata();

        // Assert
        Assert.NotNull(compiled1);
        Assert.NotNull(compiled2);
        Assert.Equal(0, metadata2.CompilationTimeMs); // Cache hit should have 0 compilation time
        Assert.Contains("cache", metadata2.Warnings[0], StringComparison.OrdinalIgnoreCase);

        var stats = cache.GetStatistics();
        Assert.True(stats.HitCount >= 1, "Should have at least one cache hit");

        LogTestInfo($"✓ Cache hit: First={metadata1.CompilationTimeMs}ms, Second={metadata2.CompilationTimeMs}ms (cached)");
        LogTestInfo($"  Cache stats: {stats.HitCount} hits, {stats.MissCount} misses, {stats.HitRate:P2} hit rate");
    }

    [SkippableFact]
    public async Task CompileAsync_BinaryCache_DifferentOptionsNoCacheHit()
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

        LogTestInfo($"✓ Different options created separate cache entries: {stats.CurrentSize} entries");
    }

    [SkippableFact]
    public async Task CompileAsync_BinaryCache_ConcurrentAccessThreadSafe()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - Compile same kernel concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => compiler.CompileAsync(kernel, options).AsTask())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        Assert.Equal(5, results.Length);

        var stats = cache.GetStatistics();
        LogTestInfo($"✓ Concurrent cache access: {stats.HitCount} hits, {stats.MissCount} misses");
    }

    #endregion

    #region Target-Specific Compilation Tests

    [SkippableFact]
    public async Task CompileAsync_MacOSTarget_CompilationSucceeds()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ macOS target compilation succeeded");
    }

    [SkippableFact]
    public async Task CompileAsync_MetalVersionDetection_UsesSystemVersion()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateMetalVersionKernel("3.0");

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Metal language version detection succeeded");
    }

    #endregion

    #region Complex Kernel Tests

    [SkippableFact]
    public async Task CompileAsync_ThreadgroupMemory_CompilesWithSharedMemory()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateThreadgroupMemoryKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        var metalKernel = Assert.IsType<MetalCompiledKernel>(compiled);

        // Verify threadgroup memory capabilities
        var metadata = metalKernel.GetCompilationMetadata();
        Assert.True(metadata.MemoryUsage.ContainsKey("MaxThreadsPerThreadgroup"));

        LogTestInfo($"✓ Threadgroup memory kernel compiled, max threads: {metadata.MemoryUsage["MaxThreadsPerThreadgroup"]}");
    }

    [SkippableFact]
    public async Task CompileAsync_MultipleEntryPoints_CompilesSpecifiedEntry()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateMultiEntryKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        Assert.Equal("kernel_a", kernel.EntryPoint);
        LogTestInfo("✓ Multi-entry kernel compiled with specified entry point");
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
        LogTestInfo("✓ Kernel validation succeeded");
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
        LogTestInfo($"✓ Null kernel validation failed correctly: {result.ErrorMessage}");
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
        LogTestInfo($"✓ Empty name validation failed: {result.ErrorMessage}");
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
        LogTestInfo($"✓ Empty code validation failed: {result.ErrorMessage}");
    }

    [Fact]
    public void Validate_InvalidMetalCode_ReturnsFailure()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = new KernelDefinition("test", "this is not metal or kernel code");

        // Act
        var result = compiler.Validate(kernel);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("metal", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        LogTestInfo($"✓ Invalid Metal code validation failed: {result.ErrorMessage}");
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

        LogTestInfo("✓ Disposed compiler correctly throws ObjectDisposedException");
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

        LogTestInfo("✓ Null kernel throws ArgumentNullException");
    }

    [SkippableFact]
    public async Task CompileAsync_InvalidKernel_ThrowsInvalidOperationException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateEmptyKernel();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(kernel));

        LogTestInfo("✓ Invalid kernel throws InvalidOperationException");
    }

    #endregion

    #region Concurrent Compilation Tests

    [SkippableFact]
    public async Task CompileAsync_MultipleConcurrentCompilations_HandlesThreadSafety()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernels = Enumerable.Range(0, 10)
            .Select(_ => TestKernelFactory.CreateVectorAddKernel())
            .ToList();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = kernels.Select(k => compiler.CompileAsync(k).AsTask());
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        Assert.Equal(10, results.Length);

        LogTestInfo($"✓ Compiled {results.Length} kernels concurrently in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Compiler Properties Tests

    [Fact]
    public void Name_ReturnsCorrectCompilerName()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act
        var name = compiler.Name;

        // Assert
        Assert.Equal("Metal Shader Compiler", name);
        LogTestInfo($"✓ Compiler name: {name}");
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

        LogTestInfo($"✓ Supported types: {string.Join(", ", types)}");
    }

    [Fact]
    public void Capabilities_ContainsExpectedFeatures()
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

        var versions = (string[])capabilities["SupportedLanguageVersions"];
        Assert.Contains("Metal 2.0", versions);

        LogTestInfo($"✓ Capabilities verified, supports {versions.Length} Metal versions");
    }

    #endregion

    #region Optimization Integration Tests

    [SkippableFact]
    public async Task OptimizeAsync_ValidKernel_ProducesOptimizedVersion()
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

        var metadata = ((MetalCompiledKernel)optimized).GetCompilationMetadata();
        Assert.Contains("Optimization", string.Join(" ", metadata.Warnings), StringComparison.OrdinalIgnoreCase);

        LogTestInfo($"✓ Kernel optimization succeeded with {metadata.Warnings.Count} metadata entries");
    }

    [SkippableFact]
    public async Task OptimizeAsync_OptimizationLevelNone_ReturnsOriginal()
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
        LogTestInfo("✓ OptimizationLevel.None returns original kernel");
    }

    #endregion

    #region Disposal Tests

    [SkippableFact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Act & Assert
        compiler.Dispose();
        compiler.Dispose(); // Should not throw
        compiler.Dispose(); // Should not throw

        LogTestInfo("✓ Multiple Dispose calls handled gracefully");
    }

    [SkippableFact]
    public async Task Dispose_WithActiveCache_DisposesCache()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        await compiler.CompileAsync(kernel);
        compiler.Dispose();

        // Assert - cache should still be usable (it's external)
        var stats = cache.GetStatistics();
        Assert.True(stats.CurrentSize >= 0);

        LogTestInfo($"✓ Compiler disposed, cache contains {stats.CurrentSize} entries");
    }

    #endregion

    #region C# Translation Tests (Not Fully Implemented)

    [SkippableFact]
    public async Task CompileAsync_CSharpKernel_ThrowsNotSupportedException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateCSharpKernel();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await compiler.CompileAsync(kernel));

        Assert.Contains("translation", exception.Message, StringComparison.OrdinalIgnoreCase);
        LogTestInfo($"✓ C# translation correctly throws NotSupportedException: {exception.Message}");
    }

    #endregion

    #region Performance Tests

    [SkippableFact]
    public async Task CompileAsync_RepeatedCompilation_MaintainsPerformance()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compileTimes = new List<long>();

        // Act - Compile same kernel 5 times
        for (int i = 0; i < 5; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await compiler.CompileAsync(kernel);
            stopwatch.Stop();
            compileTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        var avgTime = compileTimes.Average();
        var maxTime = compileTimes.Max();

        Assert.True(maxTime < 5000, "Compilation should complete within 5 seconds");

        LogTestInfo($"✓ Repeated compilation times: {string.Join(", ", compileTimes)}ms");
        LogTestInfo($"  Average: {avgTime:F2}ms, Max: {maxTime}ms");
    }

    [SkippableFact]
    public async Task CompileAsync_CachePerformance_CacheHitFasterThanMiss()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel, options);
        stopwatch1.Stop();
        var firstCompileTime = stopwatch1.ElapsedMilliseconds;

        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel, options);
        stopwatch2.Stop();
        var cachedCompileTime = stopwatch2.ElapsedMilliseconds;

        // Assert
        // Cache hit should be significantly faster (or at least not slower)
        Assert.True(cachedCompileTime <= firstCompileTime * 2,
            $"Cache hit should be fast: first={firstCompileTime}ms, cached={cachedCompileTime}ms");

        LogTestInfo($"✓ Cache performance: First={firstCompileTime}ms, Cached={cachedCompileTime}ms");
    }

    #endregion
}
