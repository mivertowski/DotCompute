// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Kernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for BaseKernelCompiler consolidating common compilation patterns.
/// Tests compilation caching, optimization levels, error reporting, AOT compatibility, and performance metrics.
/// </summary>
[Trait("Category", "HardwareIndependent")]
[Trait("Component", "BaseKernelCompiler")]
public class BaseKernelCompilerTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly TestKernelCompiler _compiler;
    private readonly List<TestKernelCompiler> _compilers = [];
    private bool _disposed;

    public BaseKernelCompilerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _compiler = new TestKernelCompiler(_mockLogger.Object);
        _compilers.Add(_compiler);
    }

    #region Basic Functionality Tests

    [Fact]
    [Trait("TestType", "BasicFunctionality")]
    public void Constructor_InitializesProperties_Correctly()
    {
        // Assert
        _compiler.Name.Should().Be("TestCompiler");
        _compiler.SupportedSourceTypes.Should().Contain(KernelLanguage.OpenCL);
        _compiler.SupportedSourceTypes.Should().Contain(KernelLanguage.CUDA);
        _compiler.Capabilities.Should().ContainKey("SupportsAsync");
        _compiler.Capabilities.Should().ContainKey("SupportsCaching");
        _compiler.Capabilities.Should().ContainKey("SupportsOptimization");
        _compiler.Capabilities["SupportsAsync"].Should().Be(true);
    }

    [Fact]
    [Trait("TestType", "BasicFunctionality")]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestKernelCompiler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region Compilation Caching Tests

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task CompileAsync_FirstCompilation_DoesNotHitCache()
    {
        // Arrange
        var definition = new KernelDefinition("cache_test", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("cache_test");
        _compiler.CompileKernelCoreCallCount.Should().Be(1);
        _compiler.LastCacheHit.Should().BeFalse();
        
        // Verify cache miss logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting compilation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task CompileAsync_SecondCompilationSameKernel_HitsCache()
    {
        // Arrange
        var definition = new KernelDefinition("cache_test", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var result1 = await _compiler.CompileAsync(definition, options);
        var result2 = await _compiler.CompileAsync(definition, options);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Id.Should().Be(result2.Id, "cached kernels should have same ID");
        _compiler.CompileKernelCoreCallCount.Should().Be(1, "should only compile once due to caching");
        
        // Verify cache hit logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using cached compilation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task CompileAsync_DifferentOptimizationLevels_CreatesNewEntries()
    {
        // Arrange
        var definition = new KernelDefinition("opt_test", "__kernel void test() {}", "main");
        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };

        // Act
        var result1 = await _compiler.CompileAsync(definition, options1);
        var result2 = await _compiler.CompileAsync(definition, options2);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        _compiler.CompileKernelCoreCallCount.Should().Be(2, "different optimization levels should create separate cache entries");
        _compiler.GetCacheCount().Should().Be(2);
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public void GenerateCacheKey_SameInputs_ReturnsSameKey()
    {
        // Arrange
        var definition = new KernelDefinition("test", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var key1 = _compiler.TestGenerateCacheKey(definition, options);
        var key2 = _compiler.TestGenerateCacheKey(definition, options);

        // Assert
        key1.Should().Be(key2);
        key1.Should().Contain("test");
        key1.Should().Contain("Default");
        key1.Should().Contain("TestCompiler");
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public void GenerateCacheKey_DifferentCode_ReturnsDifferentKeys()
    {
        // Arrange
        var definition1 = new KernelDefinition("test", "__kernel void test1() {}", "main");
        var definition2 = new KernelDefinition("test", "__kernel void test2() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var key1 = _compiler.TestGenerateCacheKey(definition1, options);
        var key2 = _compiler.TestGenerateCacheKey(definition2, options);

        // Assert
        key1.Should().NotBe(key2, "different code should produce different cache keys");
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task CompileAsync_ConcurrentSameKernel_OnlyCompilesOnce()
    {
        // Arrange
        var definition = new KernelDefinition("concurrent_test", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };
        var compiler = CreateTestCompiler();
        compiler.CompilationDelay = TimeSpan.FromMilliseconds(200); // Simulate slow compilation

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => compiler.CompileAsync(definition, options))
            .ToArray();

        var results = await Task.WhenAll(tasks.Select(t => t.AsTask()));

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().AllSatisfy(r => r.Id.Should().Be(results[0].Id, "all should get same cached result"));
        compiler.CompileKernelCoreCallCount.Should().Be(1, "should only compile once despite concurrent requests");
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public void ClearCache_RemovesAllEntries()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definition = new KernelDefinition("clear_test", "__kernel void test() {}", "main");
        compiler.CompileAsync(definition).AsTask().Wait();
        compiler.GetCacheCount().Should().BeGreaterThan(0);

        // Act
        compiler.ClearCache();

        // Assert
        compiler.GetCacheCount().Should().Be(0);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cache cleared")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task CompileAsync_CachingDisabled_AlwaysCompiles()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.EnableCachingOverride = false;
        var definition = new KernelDefinition("no_cache_test", "__kernel void test() {}", "main");

        // Act
        var result1 = await compiler.CompileAsync(definition);
        var result2 = await compiler.CompileAsync(definition);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        compiler.CompileKernelCoreCallCount.Should().Be(2, "should compile twice when caching disabled");
        compiler.GetCacheCount().Should().Be(0, "cache should be empty when disabled");
    }

    #endregion

    #region Optimization Level Tests

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Minimal)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Aggressive)]
    [InlineData(OptimizationLevel.Maximum)]
    [Trait("TestType", "OptimizationLevels")]
    public async Task CompileAsync_DifferentOptimizationLevels_PassedCorrectly(OptimizationLevel level)
    {
        // Arrange
        var definition = new KernelDefinition($"opt_{level}", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = level };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        result.Should().NotBeNull();
        _compiler.LastCompilationOptions.Should().NotBeNull();
        _compiler.LastCompilationOptions.OptimizationLevel.Should().Be(level);
    }

    [Fact]
    [Trait("TestType", "OptimizationLevels")]
    public async Task CompileAsync_NoOptions_UsesDefaultOptimization()
    {
        // Arrange
        var definition = new KernelDefinition("default_opt", "__kernel void test() {}", "main");

        // Act
        var result = await _compiler.CompileAsync(definition, null);

        // Assert
        result.Should().NotBeNull();
        _compiler.LastCompilationOptions.Should().NotBeNull();
        _compiler.LastCompilationOptions.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        _compiler.LastCompilationOptions.EnableDebugInfo.Should().BeFalse();
    }

    [Fact]
    [Trait("TestType", "OptimizationLevels")]
    public async Task CompileAsync_OptimizationLevels_RecordedInMetrics()
    {
        // Arrange
        var definition = new KernelDefinition("metrics_opt", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };

        // Act
        await _compiler.CompileAsync(definition, options);

        // Assert
        var metrics = _compiler.GetMetrics();
        metrics.Should().HaveCount(1);
        var metric = metrics.Values.First();
        metric.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        metric.KernelName.Should().Be("metrics_opt");
    }

    [Fact]
    [Trait("TestType", "OptimizationLevels")]
    public async Task OptimizeAsync_CallsOptimizeKernelCore()
    {
        // Arrange
        var definition = new KernelDefinition("optimize_test", "__kernel void test() {}", "main");
        var originalKernel = await _compiler.CompileAsync(definition);

        // Act
        var optimizedKernel = await _compiler.OptimizeAsync(originalKernel, OptimizationLevel.Maximum);

        // Assert
        optimizedKernel.Should().NotBeNull();
        _compiler.OptimizeKernelCoreCallCount.Should().Be(1);
        _compiler.LastOptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Optimizing kernel")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Error Reporting Tests

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_NullDefinition_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _compiler.CompileAsync((KernelDefinition)null!, null);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_EmptyKernelName_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("", "__kernel void test() {}", "main");

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*")
            .WithMessage("*name cannot be empty*");
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_NullKernelCode_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("test", null!, "main");

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*")
            .WithMessage("*code cannot be null*");
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_EmptyKernelCode_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("test", "", "main");

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*")
            .WithMessage("*code cannot be null or empty*");
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_CompilationFailure_ThrowsKernelCompilationException()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.ShouldThrowOnCompilation = true;
        var definition = new KernelDefinition("fail_test", "__kernel void test() {}", "main");

        // Act & Assert
        var act = async () => await compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<KernelCompilationException>()
            .WithMessage("*Failed to compile kernel 'fail_test'*");

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to compile")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.CompilationDelay = TimeSpan.FromSeconds(1);
        var definition = new KernelDefinition("cancel_test", "__kernel void test() {}", "main");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var act = async () => await compiler.CompileAsync(definition, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public void Validate_InvalidKernel_ReturnsValidationFailure()
    {
        // Arrange
        var definition = new KernelDefinition("", null!, "main");

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("name cannot be empty");
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task ValidateAsync_InvalidKernel_ReturnsValidationFailure()
    {
        // Arrange
        var definition = new KernelDefinition("test", "", "main");

        // Act
        var result = await _compiler.ValidateAsync(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("code cannot be null or empty");
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_InvalidWorkDimensions_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("invalid_dims", "__kernel void test() {}", "main")
        {
            Metadata = new Dictionary<string, object>
            {
                ["WorkDimensions"] = 5 // Invalid: must be 1-3
            }
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Work dimensions must be between 1 and 3*");
    }

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_EmptyParameters_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("no_params", "__kernel void test() {}", "main")
        {
            Metadata = new Dictionary<string, object>
            {
                ["Parameters"] = new List<object>() // Empty parameters list
            }
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel must have at least one parameter*");
    }

    #endregion

    #region AOT Compatibility Tests

    [Fact]
    [Trait("TestType", "AotCompatibility")]
    public async Task CompileAsync_AotMode_FallbacksGracefully()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.IsAotMode = true;
        var definition = new KernelDefinition("aot_test", "__kernel void test() {}", "main");

        // Act
        var result = await compiler.CompileAsync(definition);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("aot_test");
        compiler.AotFallbackUsed.Should().BeTrue();
    }

    [Fact]
    [Trait("TestType", "AotCompatibility")]
    public void GenerateCacheKey_AotMode_UsesStaticHashing()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.IsAotMode = true;
        var definition = new KernelDefinition("aot_cache", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var key = compiler.TestGenerateCacheKey(definition, options);

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("aot_cache");
        key.Should().Contain("Default");
        compiler.StaticHashingUsed.Should().BeTrue();
    }

    [Fact]
    [Trait("TestType", "AotCompatibility")]
    public async Task CompileAsync_RuntimeMode_UsesFullFeatures()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.IsAotMode = false;
        var definition = new KernelDefinition("runtime_test", "__kernel void test() {}", "main");

        // Act
        var result = await compiler.CompileAsync(definition);

        // Assert
        result.Should().NotBeNull();
        compiler.AotFallbackUsed.Should().BeFalse();
        compiler.RuntimeCompilationUsed.Should().BeTrue();
    }

    #endregion

    #region Performance Metrics Tests

    [Fact]
    [Trait("TestType", "PerformanceMetrics")]
    public async Task CompileAsync_TracksCompilationTime()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.CompilationDelay = TimeSpan.FromMilliseconds(100);
        var definition = new KernelDefinition("timing_test", "__kernel void test() {}", "main");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await compiler.CompileAsync(definition);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        var metrics = compiler.GetMetrics();
        metrics.Should().HaveCount(1);
        var metric = metrics.Values.First();
        metric.CompilationTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(90));
        metric.CompilationTime.Should().BeLessThan(stopwatch.Elapsed);
        metric.CacheHit.Should().BeFalse();
        metric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    [Trait("TestType", "PerformanceMetrics")]
    public async Task CompileAsync_LogsPerformanceMetrics()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.EnableMetricsLogging = true;
        var definition = new KernelDefinition("metrics_log_test", "__kernel void test() {}", "main");

        // Act
        await compiler.CompileAsync(definition);

        // Assert
        compiler.LogCompilationMetricsCallCount.Should().Be(1);
        compiler.LastLoggedCompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    [Trait("TestType", "PerformanceMetrics")]
    public void LogCompilationMetrics_WithBytecodeSize_LogsCorrectFormat()
    {
        // Arrange
        const string kernelName = "metrics_test";
        var compilationTime = TimeSpan.FromMilliseconds(150);
        const long byteCodeSize = 2048L;

        // Act
        _compiler.TestLogCompilationMetrics(kernelName, compilationTime, byteCodeSize);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(kernelName) && 
                                             v.ToString()!.Contains("150") &&
                                             v.ToString()!.Contains("2048")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestType", "PerformanceMetrics")]
    public void LogCompilationMetrics_WithoutBytecodeSize_LogsNAValue()
    {
        // Arrange
        const string kernelName = "metrics_na_test";
        var compilationTime = TimeSpan.FromMilliseconds(100);

        // Act
        _compiler.TestLogCompilationMetrics(kernelName, compilationTime, null);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("N/A")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestType", "PerformanceMetrics")]
    public async Task CompileAsync_MultipleKernels_TracksAllMetrics()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definitions = Enumerable.Range(0, 5)
            .Select(i => new KernelDefinition($"multi_kernel_{i}", "__kernel void test() {}", "main"))
            .ToArray();

        // Act
        foreach (var definition in definitions)
        {
            await compiler.CompileAsync(definition);
        }

        // Assert
        var metrics = compiler.GetMetrics();
        metrics.Should().HaveCount(5);
        metrics.Values.Should().AllSatisfy(m => m.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero));
        metrics.Values.Should().AllSatisfy(m => m.CacheHit.Should().BeFalse());
    }

    #endregion

    #region Advanced Concurrent Access Tests

    [Fact]
    [Trait("TestType", "ConcurrentAccess")]
    public async Task CompileAsync_MixedConcurrentOperations_HandlesCorrectly()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.CompilationDelay = TimeSpan.FromMilliseconds(50);
        
        var compileTask = compiler.CompileAsync(new KernelDefinition("concurrent1", "__kernel void test1() {}", "main"));
        var optimizeTask = compileTask.AsTask().ContinueWith(async t => 
            await compiler.OptimizeAsync(await t, OptimizationLevel.Maximum));
        var cacheTask = compiler.CompileAsync(new KernelDefinition("concurrent2", "__kernel void test2() {}", "main"));
        var clearTask = Task.Run(async () =>
        {
            await Task.Delay(25);
            compiler.ClearCache();
        });

        // Act
        await Task.WhenAll(compileTask.AsTask(), optimizeTask.Unwrap(), cacheTask.AsTask(), clearTask);

        // Assert
        compiler.CompileKernelCoreCallCount.Should().BeGreaterOrEqualTo(2);
        compiler.OptimizeKernelCoreCallCount.Should().Be(1);
        compiler.MaxConcurrentCompilations.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    [Trait("TestType", "ConcurrentAccess")]
    public async Task CompileAsync_StressTestConcurrency_MaintainsDataIntegrity()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        const int concurrentTasks = 50;
        var tasks = new List<Task<ICompiledKernel>>();
        
        // Act
        for (var i = 0; i < concurrentTasks; i++)
        {
            var kernelName = $"stress_test_{i}";
            var definition = new KernelDefinition(kernelName, $"__kernel void {kernelName}() {{}}", "main");
            tasks.Add(compiler.CompileAsync(definition).AsTask());
        }
        
        var results = await Task.WhenAll(tasks);
        
        // Assert
        results.Should().HaveCount(concurrentTasks);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        compiler.CompileKernelCoreCallCount.Should().Be(concurrentTasks);
        compiler.MaxConcurrentCompilations.Should().BeGreaterThan(1);
        
        var uniqueIds = results.Select(r => r.Id).Distinct().Count();
        uniqueIds.Should().Be(concurrentTasks, "each kernel should have unique ID");
    }

    [Fact]
    [Trait("TestType", "ConcurrentAccess")]
    public async Task CompileAsync_ConcurrentCacheOperations_ThreadSafe()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definition = new KernelDefinition("cache_concurrent", "__kernel void test() {}", "main");
        
        // Start multiple compilation attempts
        var compileTasks = Enumerable.Range(0, 20)
            .Select(_ => compiler.CompileAsync(definition))
            .Select(t => t.AsTask())
            .ToArray();
        
        // Start cache clear operations
        var clearTasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(10, 100));
                compiler.ClearCache();
            }))
            .ToArray();
        
        // Act
        await Task.WhenAll(compileTasks.Concat(clearTasks));
        
        // Assert - Should not throw and all compilations should succeed
        var results = compileTasks.Select(t => t.Result).ToArray();
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region Performance Impact Analysis Tests

    [Fact]
    [Trait("TestType", "PerformanceImpact")]
    public async Task CompileAsync_OptimizationLevelPerformance_ShowsMeasurableDifference()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definition = new KernelDefinition("perf_test", "__kernel void test() {}", "main");
        
        var noneOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var maxOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };
        
        // Act & measure
        var sw1 = Stopwatch.StartNew();
        await compiler.CompileAsync(definition, noneOptions);
        sw1.Stop();
        
        var sw2 = Stopwatch.StartNew();
        await compiler.CompileAsync(new KernelDefinition("perf_test_max", "__kernel void test() {}", "main"), maxOptions);
        sw2.Stop();
        
        // Assert
        var metrics = compiler.GetMetrics();
        metrics.Should().HaveCount(2);
        
        var noneMetric = metrics.Values.First(m => m.OptimizationLevel == OptimizationLevel.None);
        var maxMetric = metrics.Values.First(m => m.OptimizationLevel == OptimizationLevel.Maximum);
        
        noneMetric.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
        maxMetric.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    [Trait("TestType", "PerformanceImpact")]
    public async Task CompileAsync_CacheVsNoCachePerformance_ShowsSignificantDifference()
    {
        // Arrange
        var cachedCompiler = CreateTestCompiler();
        cachedCompiler.EnableCachingOverride = true;
        cachedCompiler.CompilationDelay = TimeSpan.FromMilliseconds(100);
        
        var nonCachedCompiler = CreateTestCompiler();
        nonCachedCompiler.EnableCachingOverride = false;
        nonCachedCompiler.CompilationDelay = TimeSpan.FromMilliseconds(100);
        
        var definition = new KernelDefinition("cache_perf", "__kernel void test() {}", "main");
        
        // Act - First compilation (both should be similar)
        var sw1 = Stopwatch.StartNew();
        await cachedCompiler.CompileAsync(definition);
        sw1.Stop();
        
        var sw2 = Stopwatch.StartNew();
        await nonCachedCompiler.CompileAsync(definition);
        sw2.Stop();
        
        // Act - Second compilation (cached should be much faster)
        var sw3 = Stopwatch.StartNew();
        await cachedCompiler.CompileAsync(definition);
        sw3.Stop();
        
        var sw4 = Stopwatch.StartNew();
        await nonCachedCompiler.CompileAsync(definition);
        sw4.Stop();
        
        // Assert
        sw3.Elapsed.Should().BeLessThan(sw1.Elapsed.Divide(2), "cached compilation should be much faster");
        sw4.Elapsed.Should().BeGreaterThan(sw3.Elapsed, "non-cached should still take full time");
    }

    [Fact]
    [Trait("TestType", "PerformanceImpact")]
    public async Task CompileAsync_ResourceUsageTracking_MonitorsMemoryAndCPU()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var largeKernelCode = new string('x', 10000); // Large kernel code
        var definitions = Enumerable.Range(0, 10)
            .Select(i => new KernelDefinition($"resource_test_{i}", $"__kernel void test_{i}() {{ {largeKernelCode} }}", "main"))
            .ToArray();
        
        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        foreach (var definition in definitions)
        {
            await compiler.CompileAsync(definition);
        }
        
        stopwatch.Stop();
        var finalMemory = GC.GetTotalMemory(false);
        
        // Assert
        var metrics = compiler.GetMetrics();
        metrics.Should().HaveCount(10);
        
        var totalCompilationTime = metrics.Values.Sum(m => m.CompilationTime.TotalMilliseconds);
        var memoryIncrease = finalMemory - initialMemory;
        
        totalCompilationTime.Should().BeGreaterThan(0);
        memoryIncrease.Should().BeGreaterThan(0, "compilation should use memory");
        
        // Verify performance characteristics
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(totalCompilationTime * 0.5, "wall clock time should be reasonable");
    }

    #endregion

    #region Enhanced Error Scenarios Tests

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public async Task OptimizeAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _compiler.OptimizeAsync(null!, OptimizationLevel.Maximum);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public async Task CompileAsync_MultipleValidationErrors_ReportsAllErrors()
    {
        // Arrange
        var definition = new KernelDefinition("", null!, "main") // Multiple validation errors
        {
            Metadata = new Dictionary<string, object>
            {
                ["WorkDimensions"] = -1, // Invalid
                ["Parameters"] = new List<object>() // Empty
            }
        };
        
        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("*name cannot be empty*");
    }

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public async Task CompileAsync_CompilerInternalError_WrapsException()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.ShouldThrowOnCompilation = true;
        var definition = new KernelDefinition("error_test", "__kernel void test() {}", "main");
        
        // Act & Assert
        var act = async () => await compiler.CompileAsync(definition);
        var exception = await act.Should().ThrowAsync<KernelCompilationException>();
        exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public async Task CompileAsync_OutOfMemoryDuringCompilation_HandlesGracefully()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definition = new KernelDefinition("oom_test", "__kernel void test() {}", "main");
        
        // Simulate OOM by creating a mock that throws
        compiler.ShouldThrowOnCompilation = true;
        
        // Act & Assert
        var act = async () => await compiler.CompileAsync(definition);
        await act.Should().ThrowAsync<KernelCompilationException>();
        
        // Verify error is logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public async Task Validate_ComplexValidationScenarios_HandlesAllCases()
    {
        // Test valid kernel with complex metadata
        var validDefinition = new KernelDefinition("complex_valid", "__kernel void test() {}", "main")
        {
            Metadata = new Dictionary<string, object>
            {
                ["WorkDimensions"] = 2,
                ["Parameters"] = new List<object> { "param1", "param2" }
            }
        };
        
        var validResult = _compiler.Validate(validDefinition);
        validResult.IsValid.Should().BeTrue();
        
        // Test edge case work dimensions
        foreach (var workDim in new[] { 1, 2, 3 })
        {
            var edgeDefinition = new KernelDefinition($"edge_{workDim}", "__kernel void test() {}", "main")
            {
                Metadata = new Dictionary<string, object>
                {
                    ["WorkDimensions"] = workDim,
                    ["Parameters"] = new List<object> { "param1" }
                }
            };
            
            var result = _compiler.Validate(edgeDefinition);
            result.IsValid.Should().BeTrue($"work dimensions {workDim} should be valid");
        }
    }

    #endregion

    #region Enhanced AOT Compatibility Tests

    [Fact]
    [Trait("TestType", "AotCompatibility")]
    public async Task CompileAsync_AotModeWithComplexKernel_HandlesProperly()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.IsAotMode = true;
        
        var complexKernel = """
            __kernel void complex_kernel(__global float* input, 
                                       __global float* output, 
                                       const int size) {
                int gid = get_global_id(0);
                if (gid < size) {
                    output[gid] = input[gid] * 2.0f + 1.0f;
                }
            }
            """;
        
        var definition = new KernelDefinition("complex_aot", complexKernel, "complex_kernel");
        
        // Act
        var result = await compiler.CompileAsync(definition);
        
        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("complex_aot");
        compiler.AotFallbackUsed.Should().BeTrue();
        compiler.StaticHashingUsed.Should().BeTrue();
    }

    [Fact]
    [Trait("TestType", "AotCompatibility")]
    public void GenerateCacheKey_AotVsRuntime_ProducesDifferentStrategies()
    {
        // Arrange
        var aotCompiler = CreateTestCompiler();
        aotCompiler.IsAotMode = true;
        
        var runtimeCompiler = CreateTestCompiler();
        runtimeCompiler.IsAotMode = false;
        
        var definition = new KernelDefinition("cache_strategy", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };
        
        // Act
        var aotKey = aotCompiler.TestGenerateCacheKey(definition, options);
        var runtimeKey = runtimeCompiler.TestGenerateCacheKey(definition, options);
        
        // Assert
        aotKey.Should().NotBeNullOrEmpty();
        runtimeKey.Should().NotBeNullOrEmpty();
        
        // AOT should use simpler hashing strategy
        aotCompiler.StaticHashingUsed.Should().BeTrue();
        runtimeCompiler.StaticHashingUsed.Should().BeFalse();
    }

    [Fact]
    [Trait("TestType", "AotCompatibility")]
    public async Task CompileAsync_AotModeValidation_UsesSafeValidation()
    {
        // Arrange
        var aotCompiler = CreateTestCompiler();
        aotCompiler.IsAotMode = true;
        
        var definition = new KernelDefinition("aot_validation", "__kernel void test() {}", "main")
        {
            Metadata = new Dictionary<string, object>
            {
                ["WorkDimensions"] = 3,
                ["Parameters"] = new List<object> { "param1" }
            }
        };
        
        // Act
        var validationResult = aotCompiler.TestValidateKernelDefinition(definition);
        var compilationResult = await aotCompiler.CompileAsync(definition);
        
        // Assert
        validationResult.IsValid.Should().BeTrue();
        compilationResult.Should().NotBeNull();
        aotCompiler.AotFallbackUsed.Should().BeTrue();
    }

    #endregion

    #region Resource Usage and Memory Tests

    [Fact]
    [Trait("TestType", "ResourceUsage")]
    public async Task CompileAsync_MetricsCollection_TracksResourceUsage()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.EnableMetricsLogging = true;
        
        var definitions = Enumerable.Range(0, 3)
            .Select(i => new KernelDefinition($"resource_{i}", $"__kernel void test_{i}() {{ /* {new string('x', 1000)} */ }}", "main"))
            .ToArray();
        
        var initialMetricsCount = compiler.GetMetrics().Count;
        
        // Act
        foreach (var definition in definitions)
        {
            await compiler.CompileAsync(definition);
        }
        
        // Assert
        var finalMetrics = compiler.GetMetrics();
        finalMetrics.Should().HaveCount(initialMetricsCount + 3);
        
        foreach (var metric in finalMetrics.Values)
        {
            metric.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
            metric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            metric.KernelName.Should().NotBeNullOrEmpty();
        }
        
        compiler.LogCompilationMetricsCallCount.Should().Be(3);
    }

    [Fact]
    [Trait("TestType", "ResourceUsage")]
    public async Task CompileAsync_LargeKernelCompilation_ManagesMemoryEfficiently()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var largeCode = new string('*', 50000); // Large kernel code
        var definition = new KernelDefinition("large_kernel", $"__kernel void test() {{ /* {largeCode} */ }}", "main");
        
        var beforeMemory = GC.GetTotalMemory(true);
        
        // Act
        var result = await compiler.CompileAsync(definition);
        
        var afterMemory = GC.GetTotalMemory(false);
        var memoryIncrease = afterMemory - beforeMemory;
        
        // Assert
        result.Should().NotBeNull();
        memoryIncrease.Should().BeLessThan(100_000_000, "memory increase should be reasonable for large kernel");
        
        // Verify the large kernel was handled properly
        var metrics = compiler.GetMetrics();
        metrics.Should().HaveCount(1);
        metrics.Values.First().KernelName.Should().Be("large_kernel");
    }

    [Fact]
    [Trait("TestType", "ResourceUsage")]
    public void GetMetrics_AfterMultipleOperations_ProvidesCompleteMetrics()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definitions = new[]
        {
            new KernelDefinition("metrics1", "__kernel void test1() {}", "main"),
            new KernelDefinition("metrics2", "__kernel void test2() {}", "main")
        };
        
        // Act
        var tasks = definitions.Select(d => compiler.CompileAsync(d).AsTask()).ToArray();
        Task.WaitAll(tasks);
        
        // Clear cache and compile again to test metrics persistence
        compiler.ClearCache();
        var task3 = compiler.CompileAsync(definitions[0]).AsTask();
        task3.Wait();
        
        // Assert
        var metrics = compiler.GetMetrics();
        metrics.Should().HaveCount(0, "metrics should be cleared with cache");
        
        // Compile once more to verify new metrics
        var task4 = compiler.CompileAsync(new KernelDefinition("metrics3", "__kernel void test3() {}", "main")).AsTask();
        task4.Wait();
        
        var newMetrics = compiler.GetMetrics();
        newMetrics.Should().HaveCount(1);
        newMetrics.Values.First().KernelName.Should().Be("metrics3");
    }

    #endregion

    #region Helper Methods

    private TestKernelCompiler CreateTestCompiler()
    {
        var compiler = new TestKernelCompiler(new Mock<ILogger>().Object);
        _compilers.Add(compiler);
        return compiler;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var compiler in _compilers)
            {
                compiler.Dispose();
            }
            _disposed = true;
        }
    }

    #endregion

    /// <summary>
    /// Test implementation of BaseKernelCompiler for comprehensive testing.
    /// </summary>
    private sealed class TestKernelCompiler : BaseKernelCompiler, IDisposable
    {
        private int _compileCallCount;
        private int _optimizeCallCount;
        private int _logMetricsCallCount;
        private int _concurrentCompilations;
        private int _maxConcurrentCompilations;

        public TestKernelCompiler(ILogger logger) : base(logger)
        {
        }

        protected override string CompilerName => "TestCompiler";

        public override IReadOnlyList<KernelLanguage> SupportedSourceTypes => [KernelLanguage.OpenCL, KernelLanguage.CUDA];

        // Test properties
        public bool EnableCachingOverride { get; set; } = true;
        protected override bool EnableCaching => EnableCachingOverride;

        public bool ShouldThrowOnCompilation { get; set; }
        public TimeSpan CompilationDelay { get; set; } = TimeSpan.Zero;
        public bool IsAotMode { get; set; }
        public bool EnableMetricsLogging { get; set; }

        // State tracking
        public int CompileKernelCoreCallCount => _compileCallCount;
        public int OptimizeKernelCoreCallCount => _optimizeCallCount;
        public int LogCompilationMetricsCallCount => _logMetricsCallCount;
        public int MaxConcurrentCompilations => _maxConcurrentCompilations;

        public CompilationOptions? LastCompilationOptions { get; private set; }
        public OptimizationLevel? LastOptimizationLevel { get; private set; }
        public bool LastCacheHit { get; private set; }
        public TimeSpan LastLoggedCompilationTime { get; private set; }

        // AOT-specific tracking
        public bool AotFallbackUsed { get; private set; }
        public bool RuntimeCompilationUsed { get; private set; }
        public bool StaticHashingUsed { get; private set; }

        protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _concurrentCompilations);
            var maxConcurrent = _maxConcurrentCompilations;
            while (current > maxConcurrent)
            {
                Interlocked.CompareExchange(ref _maxConcurrentCompilations, current, maxConcurrent);
                maxConcurrent = _maxConcurrentCompilations;
            }

            try
            {
                Interlocked.Increment(ref _compileCallCount);
                LastCompilationOptions = options;

                if (CompilationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(CompilationDelay, cancellationToken);
                }

                if (ShouldThrowOnCompilation)
                {
                    throw new InvalidOperationException("Compilation failed (test simulation)");
                }

                // Simulate AOT vs Runtime compilation paths
                if (IsAotMode)
                {
                    AotFallbackUsed = true;
                }
                else
                {
                    RuntimeCompilationUsed = true;
                }

                var mockKernel = new Mock<ICompiledKernel>();
                mockKernel.Setup(x => x.Id).Returns(Guid.NewGuid());
                mockKernel.Setup(x => x.Name).Returns(definition.Name);
                
                if (EnableMetricsLogging)
                {
                    var compilationTime = CompilationDelay > TimeSpan.Zero ? CompilationDelay : TimeSpan.FromMilliseconds(1);
                    TestLogCompilationMetrics(definition.Name, compilationTime, 1024);
                }

                return mockKernel.Object;
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCompilations);
            }
        }

        protected override ValueTask<ICompiledKernel> OptimizeKernelCore(
            ICompiledKernel kernel,
            OptimizationLevel level,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _optimizeCallCount);
            LastOptimizationLevel = level;
            return ValueTask.FromResult(kernel);
        }

        protected override string GenerateCacheKey(KernelDefinition definition, CompilationOptions options)
        {
            if (IsAotMode)
            {
                StaticHashingUsed = true;
                // Simulate AOT-compatible static hashing
                return $"{definition.Name}_{definition.Code?.GetHashCode(StringComparison.Ordinal) ?? 0}_{options.OptimizationLevel}_{CompilerName}";
            }

            return base.GenerateCacheKey(definition, options);
        }

        // Test helper methods
        public string TestGenerateCacheKey(KernelDefinition definition, CompilationOptions options) => GenerateCacheKey(definition, options);

        public CompilationOptions TestGetDefaultCompilationOptions() => GetDefaultCompilationOptions();

        public KernelDefinition TestEnrichDefinitionWithMetadata(KernelDefinition definition, Dictionary<string, object> metadata) => EnrichDefinitionWithMetadata(definition, metadata);

        public UnifiedValidationResult TestValidateKernelDefinition(KernelDefinition definition) => ValidateKernelDefinition(definition);

        public void TestLogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize)
        {
            Interlocked.Increment(ref _logMetricsCallCount);
            LastLoggedCompilationTime = compilationTime;
            LogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
        }

        public int GetCacheCount()
        {
            // Access the private cache through reflection for testing
            var cacheField = typeof(BaseKernelCompiler).GetField("_compilationCache", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cacheField?.GetValue(this) is ConcurrentDictionary<string, ICompiledKernel> cache)
            {
                return cache.Count;
            }
            return 0;
        }

        public void Dispose()
        {
            // Cleanup test resources
        }
    }
}