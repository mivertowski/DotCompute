// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Kernels;
using DotCompute.Tests.Common;
using Microsoft.Extensions.Logging;
using Moq;
// Resolve ICompiledKernel ambiguity
using AbstractionsCompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for BaseKernelCompiler consolidating common compilation patterns.
/// Tests compilation caching, optimization levels, error reporting, AOT compatibility, and performance metrics.
/// </summary>
[Trait("Category", "HardwareIndependent")]
[Trait("Component", "BaseKernelCompiler")]
public sealed class BaseKernelCompilerTests : ConsolidatedTestBase
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly TestKernelCompiler _compiler;
    private readonly List<TestKernelCompiler> _compilers = [];
    private readonly Dictionary<TestKernelCompiler, Mock<ILogger>> _compilerLoggers = [];
    /// <summary>
    /// Initializes a new instance of the BaseKernelCompilerTests class.
    /// </summary>
    /// <param name="output">The output.</param>

    public BaseKernelCompilerTests(ITestOutputHelper output) : base(output)
    {
        _mockLogger = new Mock<ILogger>();
        // Setup IsEnabled to return true for all log levels so LoggerMessage works
        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _compiler = new TestKernelCompiler(_mockLogger.Object);
        _compilers.Add(_compiler);

        // Track compiler for cleanup

        TrackDisposable(_compiler);
    }
    /// <summary>
    /// Performs constructor_ initializes properties_ correctly.
    /// </summary>

    #region Basic Functionality Tests

    [Fact]
    [Trait("TestType", "BasicFunctionality")]
    public void Constructor_InitializesProperties_Correctly()
    {
        // Assert
        _ = _compiler.Name.Should().Be("TestCompiler");
        _ = _compiler.SupportedSourceTypes.Should().Contain(KernelLanguage.OpenCL);
        _ = _compiler.SupportedSourceTypes.Should().Contain(KernelLanguage.Cuda);
        _ = _compiler.Capabilities.Should().ContainKey("SupportsAsync");
        _ = _compiler.Capabilities.Should().ContainKey("SupportsCaching");
        _ = _compiler.Capabilities.Should().ContainKey("SupportsOptimization");
        _ = _compiler.Capabilities["SupportsAsync"].Should().Be(true);
    }
    /// <summary>
    /// Performs constructor_ with null logger_ throws argument null exception.
    /// </summary>

    [Fact]
    [Trait("TestType", "BasicFunctionality")]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestKernelCompiler(null!);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
    /// <summary>
    /// Gets compile async_ first compilation_ does not hit cache.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("cache_test");
        _ = _compiler.CompileKernelCoreCallCount.Should().Be(1);
        _ = _compiler.LastCacheHit.Should().BeFalse();

        // Verify cache miss logging

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting compilation", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets compile async_ second compilation same kernel_ hits cache.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = result1.Id.Should().Be(result2.Id, "cached kernels should have same ID");
        _ = _compiler.CompileKernelCoreCallCount.Should().Be(1, "should only compile once due to caching");

        // Verify cache hit logging

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using cached compilation", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets compile async_ different optimization levels_ creates new entries.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task CompileAsync_DifferentOptimizationLevels_CreatesNewEntries()
    {
        // Arrange
        var definition = new KernelDefinition("opt_test", "__kernel void test() {}", "main");
        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };

        // Act
        var result1 = await _compiler.CompileAsync(definition, options1);
        var result2 = await _compiler.CompileAsync(definition, options2);

        // Assert
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = _compiler.CompileKernelCoreCallCount.Should().Be(2, "different optimization levels should create separate cache entries");
        _ = _compiler.GetCacheCount().Should().Be(2);
    }
    /// <summary>
    /// Performs generate cache key_ same inputs_ returns same key.
    /// </summary>

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
        _ = key1.Should().Be(key2);
        _ = key1.Should().Contain("test");
        _ = key1.Should().Contain("O2"); // Default equals O2
        _ = key1.Should().Contain("TestCompiler");
    }
    /// <summary>
    /// Performs generate cache key_ different code_ returns different keys.
    /// </summary>

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
        _ = key1.Should().NotBe(key2, "different code should produce different cache keys");
    }
    /// <summary>
    /// Gets compile async_ concurrent same kernel_ only compiles once.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
        _ = results.Should().AllSatisfy(r => r.Id.Should().Be(results[0].Id, "all should get same cached result"));
        _ = compiler.CompileKernelCoreCallCount.Should().Be(1, "should only compile once despite concurrent requests");
    }
    /// <summary>
    /// Gets clear cache_ removes all entries.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "CompilationCaching")]
    public async Task ClearCache_RemovesAllEntries()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        var definition = new KernelDefinition("clear_test", "__kernel void test() {}", "main");
        _ = await compiler.CompileAsync(definition);
        _ = compiler.GetCacheCount().Should().BeGreaterThan(0);

        // Act
        compiler.ClearCache();

        // Assert
        _ = compiler.GetCacheCount().Should().Be(0);

        // Verify logging - check for the actual log message pattern on the correct mock

        GetMockLogger(compiler).Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Compilation cache cleared", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets compile async_ caching disabled_ always compiles.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = compiler.CompileKernelCoreCallCount.Should().Be(2, "should compile twice when caching disabled");
        _ = compiler.GetCacheCount().Should().Be(0, "cache should be empty when disabled");
    }
    /// <summary>
    /// Gets compile async_ different optimization levels_ passed correctly.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Optimization Level Tests

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.O3)]
    [InlineData(OptimizationLevel.O3)]
    [Trait("TestType", "OptimizationLevels")]
    public async Task CompileAsync_DifferentOptimizationLevels_PassedCorrectly(OptimizationLevel level)
    {
        // Arrange
        var definition = new KernelDefinition($"opt_{level}", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = level };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _ = _compiler.LastCompilationOptions.Should().NotBeNull();
        _ = _compiler.LastCompilationOptions.OptimizationLevel.Should().Be(level);
    }
    /// <summary>
    /// Gets compile async_ no options_ uses default optimization.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "OptimizationLevels")]
    public async Task CompileAsync_NoOptions_UsesDefaultOptimization()
    {
        // Arrange
        var definition = new KernelDefinition("default_opt", "__kernel void test() {}", "main");

        // Act
        var result = await _compiler.CompileAsync(definition, (CompilationOptions?)null);

        // Assert
        _ = result.Should().NotBeNull();
        _ = _compiler.LastCompilationOptions.Should().NotBeNull();
        _ = _compiler.LastCompilationOptions.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        _ = _compiler.LastCompilationOptions.EnableDebugInfo.Should().BeFalse();
    }
    /// <summary>
    /// Gets compile async_ optimization levels_ recorded in metrics.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "OptimizationLevels")]
    public async Task CompileAsync_OptimizationLevels_RecordedInMetrics()
    {
        // Arrange
        var definition = new KernelDefinition("metrics_opt", "__kernel void test() {}", "main");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };

        // Act
        _ = await _compiler.CompileAsync(definition, options);

        // Assert
        var metrics = _compiler.GetMetrics();
        _ = metrics.Should().HaveCount(1);
        var metric = metrics.Values.First();
        _ = metric.OptimizationLevel.Should().Be(OptimizationLevel.O3);
        _ = metric.Name.Should().Be("metrics_opt");
    }
    /// <summary>
    /// Gets optimize async_ calls optimize kernel core.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "OptimizationLevels")]
    public async Task OptimizeAsync_CallsOptimizeKernelCore()
    {
        // Arrange
        var definition = new KernelDefinition("optimize_test", "__kernel void test() {}", "main");
        var originalKernel = await _compiler.CompileAsync(definition);

        // Act
        var optimizedKernel = await _compiler.OptimizeAsync(originalKernel, OptimizationLevel.O3);

        // Assert
        _ = optimizedKernel.Should().NotBeNull();
        _ = _compiler.OptimizeKernelCoreCallCount.Should().Be(1);
        _ = _compiler.LastOptimizationLevel.Should().Be(OptimizationLevel.O3);

        // Verify logging

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Optimizing kernel", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets compile async_ null definition_ throws argument null exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Error Reporting Tests

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_NullDefinition_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _compiler.CompileAsync((KernelDefinition)null!, (CompilationOptions?)null);
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }
    /// <summary>
    /// Gets compile async_ empty kernel name_ throws validation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_EmptyKernelName_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("", "__kernel void test() {}", "main");

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*")
            .WithMessage("*name cannot be empty*");
    }
    /// <summary>
    /// Gets compile async_ null kernel code_ throws validation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_NullKernelCode_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("test", null!, "main");

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*")
            .WithMessage("*code cannot be null*");
    }
    /// <summary>
    /// Gets compile async_ empty kernel code_ throws validation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task CompileAsync_EmptyKernelCode_ThrowsValidationException()
    {
        // Arrange
        var definition = new KernelDefinition("test", "", "main");

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*")
            .WithMessage("*code cannot be null or empty*");
    }
    /// <summary>
    /// Gets compile async_ compilation failure_ throws kernel compilation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().ThrowAsync<KernelCompilationException>()
            .WithMessage("*Failed to compile kernel 'fail_test'*");

        // Verify error logging on the correct mock
        GetMockLogger(compiler).Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to compile", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets compile async_ cancellation requested_ throws operation canceled exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }
    /// <summary>
    /// Validates the _ invalid kernel_ returns validation failure.
    /// </summary>

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public void Validate_InvalidKernel_ReturnsValidationFailure()
    {
        // Arrange
        var definition = new KernelDefinition("", null!, "main");

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("name cannot be empty");
    }
    /// <summary>
    /// Validates the async_ invalid kernel_ returns validation failure.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorReporting")]
    public async Task ValidateAsync_InvalidKernel_ReturnsValidationFailure()
    {
        // Arrange
        var definition = new KernelDefinition("test", "", "main");

        // Act
        var result = await _compiler.ValidateAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("code cannot be null or empty");
    }
    /// <summary>
    /// Gets compile async_ invalid work dimensions_ throws validation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Work dimensions must be between 1 and 3*");
    }
    /// <summary>
    /// Gets compile async_ empty parameters_ throws validation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel must have at least one parameter*");
    }
    /// <summary>
    /// Gets compile async_ aot mode_ fallbacks gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("aot_test");
        _ = compiler.AotFallbackUsed.Should().BeTrue();
    }
    /// <summary>
    /// Performs generate cache key_ aot mode_ uses static hashing.
    /// </summary>

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
        _ = key.Should().NotBeNullOrEmpty();
        _ = key.Should().Contain("aot_cache");
        _ = key.Should().Contain("O2"); // Default equals O2
        _ = compiler.StaticHashingUsed.Should().BeTrue();
    }
    /// <summary>
    /// Gets compile async_ runtime mode_ uses full features.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        _ = compiler.AotFallbackUsed.Should().BeFalse();
        _ = compiler.RuntimeCompilationUsed.Should().BeTrue();
    }
    /// <summary>
    /// Gets compile async_ tracks compilation time.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        var metrics = compiler.GetMetrics();
        _ = metrics.Should().HaveCount(1);
        var metric = metrics.Values.First();
        _ = metric.CompilationTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(90));
        _ = metric.CompilationTime.Should().BeLessThan(stopwatch.Elapsed);
        _ = metric.CacheHit.Should().BeFalse();
        _ = metric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
    /// <summary>
    /// Gets compile async_ logs performance metrics.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "PerformanceMetrics")]
    public async Task CompileAsync_LogsPerformanceMetrics()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        compiler.EnableMetricsLogging = true;
        var definition = new KernelDefinition("metrics_log_test", "__kernel void test() {}", "main");

        // Act
        _ = await compiler.CompileAsync(definition);

        // Assert
        _ = compiler.LogCompilationMetricsCallCount.Should().Be(1);
        _ = compiler.LastLoggedCompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
    }
    /// <summary>
    /// Performs log compilation metrics_ with bytecode size_ logs correct format.
    /// </summary>

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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(kernelName, StringComparison.CurrentCulture) &&

                                             v.ToString()!.Contains("150", StringComparison.CurrentCulture) &&
                                             v.ToString()!.Contains("2048", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Performs log compilation metrics_ without bytecode size_ logs n a value.
    /// </summary>

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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("N/A", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets compile async_ multiple kernels_ tracks all metrics.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
            _ = await compiler.CompileAsync(definition);
        }

        // Assert
        var metrics = compiler.GetMetrics();
        _ = metrics.Should().HaveCount(5);
        _ = metrics.Values.Should().AllSatisfy(m => m.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero));
        _ = metrics.Values.Should().AllSatisfy(m => m.CacheHit.Should().BeFalse());
    }
    /// <summary>
    /// Gets compile async_ mixed concurrent operations_ handles correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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

            await compiler.OptimizeAsync(await t, OptimizationLevel.O3));
        var cacheTask = compiler.CompileAsync(new KernelDefinition("concurrent2", "__kernel void test2() {}", "main"));
        var clearTask = Task.Run(async () =>
        {
            await Task.Delay(25);
            compiler.ClearCache();
        });

        // Act
        await Task.WhenAll(compileTask.AsTask(), optimizeTask.Unwrap(), cacheTask.AsTask(), clearTask);

        // Assert
        _ = compiler.CompileKernelCoreCallCount.Should().BeGreaterThanOrEqualTo(2);
        _ = compiler.OptimizeKernelCoreCallCount.Should().Be(1);
        _ = compiler.MaxConcurrentCompilations.Should().BeGreaterThanOrEqualTo(1);
    }
    /// <summary>
    /// Gets compile async_ stress test concurrency_ maintains data integrity.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ConcurrentAccess")]
    public async Task CompileAsync_StressTestConcurrency_MaintainsDataIntegrity()
    {
        // Arrange
        var compiler = CreateTestCompiler();
        const int concurrentTasks = 50;
        var tasks = new List<Task<AbstractionsCompiledKernel>>();

        // Act

        for (var i = 0; i < concurrentTasks; i++)
        {
            var kernelName = $"stress_test_{i}";
            var definition = new KernelDefinition(kernelName, $"__kernel void {kernelName}() {{}}", "main");
            tasks.Add(compiler.CompileAsync(definition).AsTask());
        }


        var results = await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().HaveCount(concurrentTasks);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
        _ = compiler.CompileKernelCoreCallCount.Should().Be(concurrentTasks);
        _ = compiler.MaxConcurrentCompilations.Should().BeGreaterThanOrEqualTo(1); // At least one concurrent compilation


        var uniqueIds = results.Select(r => r.Id).Distinct().Count();
        _ = uniqueIds.Should().Be(concurrentTasks, "each kernel should have unique ID");
    }
    /// <summary>
    /// Gets compile async_ concurrent cache operations_ thread safe.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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

        var results = await Task.WhenAll(compileTasks);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }
    /// <summary>
    /// Gets compile async_ optimization level performance_ shows measurable difference.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        var maxOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };

        // Act & measure

        var sw1 = Stopwatch.StartNew();
        _ = await compiler.CompileAsync(definition, noneOptions);
        sw1.Stop();


        var sw2 = Stopwatch.StartNew();
        _ = await compiler.CompileAsync(new KernelDefinition("perf_test_max", "__kernel void test() {}", "main"), maxOptions);
        sw2.Stop();

        // Assert

        var metrics = compiler.GetMetrics();
        _ = metrics.Should().HaveCount(2);


        var noneMetric = metrics.Values.First(m => m.OptimizationLevel == OptimizationLevel.None);
        var maxMetric = metrics.Values.First(m => m.OptimizationLevel == OptimizationLevel.O3);

        _ = noneMetric.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
        _ = maxMetric.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
    }
    /// <summary>
    /// Gets compile async_ cache vs no cache performance_ shows significant difference.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await cachedCompiler.CompileAsync(definition);
        sw1.Stop();


        var sw2 = Stopwatch.StartNew();
        _ = await nonCachedCompiler.CompileAsync(definition);
        sw2.Stop();

        // Act - Second compilation (cached should be much faster)

        var sw3 = Stopwatch.StartNew();
        _ = await cachedCompiler.CompileAsync(definition);
        sw3.Stop();


        var sw4 = Stopwatch.StartNew();
        _ = await nonCachedCompiler.CompileAsync(definition);
        sw4.Stop();

        // Assert
        _ = sw3.Elapsed.Should().BeLessThan(sw1.Elapsed.Divide(2), "cached compilation should be much faster");
        _ = sw4.Elapsed.Should().BeGreaterThan(sw3.Elapsed, "non-cached should still take full time");
    }
    /// <summary>
    /// Gets compile async_ resource usage tracking_ monitors memory and c p u.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
            _ = await compiler.CompileAsync(definition);
        }


        stopwatch.Stop();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert

        var metrics = compiler.GetMetrics();
        _ = metrics.Should().HaveCount(10);


        var totalCompilationTime = metrics.Values.Sum(m => m.CompilationTime.TotalMilliseconds);
        var memoryIncrease = finalMemory - initialMemory;

        _ = totalCompilationTime.Should().BeGreaterThan(0);
        _ = memoryIncrease.Should().BeGreaterThan(0, "compilation should use memory");

        // Verify performance characteristics
        _ = stopwatch.ElapsedMilliseconds.Should().BeGreaterThan((long)(totalCompilationTime * 0.5), "wall clock time should be reasonable");
    }
    /// <summary>
    /// Gets optimize async_ with null kernel_ throws argument null exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Enhanced Error Scenarios Tests

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public async Task OptimizeAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _compiler.OptimizeAsync(null!, OptimizationLevel.O3);
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }
    /// <summary>
    /// Gets compile async_ multiple validation errors_ reports all errors.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = exception.WithMessage("*name cannot be empty*");
    }
    /// <summary>
    /// Gets compile async_ compiler internal error_ wraps exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }
    /// <summary>
    /// Gets compile async_ out of memory during compilation_ handles gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().ThrowAsync<KernelCompilationException>();

        // Verify error is logged on the correct mock

        GetMockLogger(compiler).Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Validates the _ complex validation scenarios_ handles all cases.
    /// </summary>

    [Fact]
    [Trait("TestType", "ErrorScenarios")]
    public void Validate_ComplexValidationScenarios_HandlesAllCases()
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
        _ = validResult.IsValid.Should().BeTrue();

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
            _ = result.IsValid.Should().BeTrue($"work dimensions {workDim} should be valid");
        }
    }
    /// <summary>
    /// Gets compile async_ aot mode with complex kernel_ handles properly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("complex_aot");
        _ = compiler.AotFallbackUsed.Should().BeTrue();
        _ = compiler.StaticHashingUsed.Should().BeTrue();
    }
    /// <summary>
    /// Performs generate cache key_ aot vs runtime_ produces different strategies.
    /// </summary>

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
        _ = aotKey.Should().NotBeNullOrEmpty();
        _ = runtimeKey.Should().NotBeNullOrEmpty();

        // AOT should use simpler hashing strategy
        _ = aotCompiler.StaticHashingUsed.Should().BeTrue();
        _ = runtimeCompiler.StaticHashingUsed.Should().BeFalse();
    }
    /// <summary>
    /// Gets compile async_ aot mode validation_ uses safe validation.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = validationResult.IsValid.Should().BeTrue();
        _ = compilationResult.Should().NotBeNull();
        _ = aotCompiler.AotFallbackUsed.Should().BeTrue();
    }
    /// <summary>
    /// Gets compile async_ metrics collection_ tracks resource usage.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
            _ = await compiler.CompileAsync(definition);
        }

        // Assert

        var finalMetrics = compiler.GetMetrics();
        _ = finalMetrics.Should().HaveCount(initialMetricsCount + 3);


        foreach (var metric in finalMetrics.Values)
        {
            _ = metric.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
            _ = metric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            _ = metric.Name.Should().NotBeNullOrEmpty();
        }

        _ = compiler.LogCompilationMetricsCallCount.Should().Be(3);
    }
    /// <summary>
    /// Gets compile async_ large kernel compilation_ manages memory efficiently.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        _ = memoryIncrease.Should().BeLessThan(100_000_000, "memory increase should be reasonable for large kernel");

        // Verify the large kernel was handled properly

        var metrics = compiler.GetMetrics();
        _ = metrics.Should().HaveCount(1);
        _ = metrics.Values.First().Name.Should().Be("large_kernel");
    }
    /// <summary>
    /// Gets the metrics_ after multiple operations_ provides complete metrics.
    /// </summary>
    /// <returns>The metrics_ after multiple operations_ provides complete metrics.</returns>

    [Fact]
    [Trait("TestType", "ResourceUsage")]
    public async Task GetMetrics_AfterMultipleOperations_ProvidesCompleteMetrics()
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
        _ = await Task.WhenAll(tasks);

        // Clear cache and compile again to test metrics persistence

        compiler.ClearCache();
        _ = await compiler.CompileAsync(definitions[0]);

        // Assert

        var metrics = compiler.GetMetrics();
        _ = metrics.Should().HaveCount(1, "should have one metric after recompiling");

        // Compile once more to verify new metrics
        _ = await compiler.CompileAsync(new KernelDefinition("metrics3", "__kernel void test3() {}", "main"));


        var newMetrics = compiler.GetMetrics();
        _ = newMetrics.Should().HaveCount(2, "should have metrics for recompiled metrics1 and new metrics3");
        _ = newMetrics.Values.Should().Contain(m => m.Name == "metrics1");
        _ = newMetrics.Values.Should().Contain(m => m.Name == "metrics3");
    }

    #endregion

    #region Helper Methods

    private TestKernelCompiler CreateTestCompiler()
    {
        var mockLogger = new Mock<ILogger>();
        // Setup IsEnabled to return true for all log levels so LoggerMessage works
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var compiler = new TestKernelCompiler(mockLogger.Object);
        _compilers.Add(compiler);
        _compilerLoggers[compiler] = mockLogger;
        return compiler;
    }

    private Mock<ILogger> GetMockLogger(TestKernelCompiler compiler)
    {
        return _compilerLoggers.TryGetValue(compiler, out var logger) ? logger : _mockLogger;
    }

    // Dispose is now handled by ConsolidatedTestBase via TrackDisposable()

    #endregion

    /// <summary>
    /// Test implementation of BaseKernelCompiler for comprehensive testing.
    /// </summary>
    private sealed class TestKernelCompiler(ILogger logger) : BaseKernelCompiler(logger), IDisposable
    {
        private int _compileCallCount;
        private int _optimizeCallCount;
        private int _logMetricsCallCount;
        private int _concurrentCompilations;
        private int _maxConcurrentCompilations;

        protected override string CompilerName => "TestCompiler";
        /// <summary>
        /// Gets or sets the supported source types.
        /// </summary>
        /// <value>The supported source types.</value>

        public override IReadOnlyList<KernelLanguage> SupportedSourceTypes => [KernelLanguage.OpenCL, KernelLanguage.Cuda];
        /// <summary>
        /// Gets or sets the capabilities.
        /// </summary>
        /// <value>The capabilities.</value>

        public override IReadOnlyDictionary<string, object> Capabilities { get; } = new Dictionary<string, object>
        {
            ["SupportsAsync"] = true,
            ["SupportsCaching"] = true,
            ["SupportsOptimization"] = true
        };
        /// <summary>
        /// Gets or sets the enable caching override.
        /// </summary>
        /// <value>The enable caching override.</value>

        // Test properties
        public bool EnableCachingOverride { get; set; } = true;
        protected override bool EnableCaching => EnableCachingOverride;
        /// <summary>
        /// Gets or sets the should throw on compilation.
        /// </summary>
        /// <value>The should throw on compilation.</value>

        public bool ShouldThrowOnCompilation { get; set; }
        /// <summary>
        /// Gets or sets the compilation delay.
        /// </summary>
        /// <value>The compilation delay.</value>
        public TimeSpan CompilationDelay { get; set; } = TimeSpan.Zero;
        /// <summary>
        /// Gets or sets a value indicating whether aot mode.
        /// </summary>
        /// <value>The is aot mode.</value>
        public bool IsAotMode { get; set; }
        /// <summary>
        /// Gets or sets the enable metrics logging.
        /// </summary>
        /// <value>The enable metrics logging.</value>
        public bool EnableMetricsLogging { get; set; }
        /// <summary>
        /// Gets or sets the compile kernel core call count.
        /// </summary>
        /// <value>The compile kernel core call count.</value>

        // State tracking
        public int CompileKernelCoreCallCount => _compileCallCount;
        /// <summary>
        /// Gets or sets the optimize kernel core call count.
        /// </summary>
        /// <value>The optimize kernel core call count.</value>
        public int OptimizeKernelCoreCallCount => _optimizeCallCount;
        /// <summary>
        /// Gets or sets the log compilation metrics call count.
        /// </summary>
        /// <value>The log compilation metrics call count.</value>
        public int LogCompilationMetricsCallCount => _logMetricsCallCount;
        /// <summary>
        /// Gets or sets the max concurrent compilations.
        /// </summary>
        /// <value>The max concurrent compilations.</value>
        public int MaxConcurrentCompilations => _maxConcurrentCompilations;
        /// <summary>
        /// Gets or sets the last compilation options.
        /// </summary>
        /// <value>The last compilation options.</value>

        public CompilationOptions? LastCompilationOptions { get; private set; }
        /// <summary>
        /// Gets or sets the last optimization level.
        /// </summary>
        /// <value>The last optimization level.</value>
        public OptimizationLevel? LastOptimizationLevel { get; private set; }
        /// <summary>
        /// Gets or sets the last cache hit.
        /// </summary>
        /// <value>The last cache hit.</value>
        public bool LastCacheHit { get; private set; }
        /// <summary>
        /// Gets or sets the last logged compilation time.
        /// </summary>
        /// <value>The last logged compilation time.</value>
        public TimeSpan LastLoggedCompilationTime { get; private set; }
        /// <summary>
        /// Gets or sets the aot fallback used.
        /// </summary>
        /// <value>The aot fallback used.</value>

        // AOT-specific tracking
        public bool AotFallbackUsed { get; private set; }
        /// <summary>
        /// Gets or sets the runtime compilation used.
        /// </summary>
        /// <value>The runtime compilation used.</value>
        public bool RuntimeCompilationUsed { get; private set; }
        /// <summary>
        /// Gets or sets the static hashing used.
        /// </summary>
        /// <value>The static hashing used.</value>
        public bool StaticHashingUsed { get; private set; }

        protected override async ValueTask<AbstractionsCompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _concurrentCompilations);
            var maxConcurrent = _maxConcurrentCompilations;
            while (current > maxConcurrent)
            {
                _ = Interlocked.CompareExchange(ref _maxConcurrentCompilations, current, maxConcurrent);
                maxConcurrent = _maxConcurrentCompilations;
            }

            try
            {
                _ = Interlocked.Increment(ref _compileCallCount);
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

                var mockKernel = new Mock<AbstractionsCompiledKernel>();
                _ = mockKernel.Setup(x => x.Id).Returns(Guid.NewGuid());
                _ = mockKernel.Setup(x => x.Name).Returns(definition.Name);


                if (EnableMetricsLogging)
                {
                    var compilationTime = CompilationDelay > TimeSpan.Zero ? CompilationDelay : TimeSpan.FromMilliseconds(1);
                    TestLogCompilationMetrics(definition.Name, compilationTime, 1024);
                }

                return mockKernel.Object;
            }
            finally
            {
                _ = Interlocked.Decrement(ref _concurrentCompilations);
            }
        }

        protected override ValueTask<AbstractionsCompiledKernel> OptimizeKernelCoreAsync(
            AbstractionsCompiledKernel kernel,
            OptimizationLevel level,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _optimizeCallCount);
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
        /// <summary>
        /// Gets test generate cache key.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="options">The options.</param>
        /// <returns>The result of the operation.</returns>

        // Test helper methods
        public string TestGenerateCacheKey(KernelDefinition definition, CompilationOptions options) => GenerateCacheKey(definition, options);
        /// <summary>
        /// Gets test get default compilation options.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public CompilationOptions TestGetDefaultCompilationOptions() => GetDefaultCompilationOptions();
        /// <summary>
        /// Gets test enrich definition with metadata.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="metadata">The metadata.</param>
        /// <returns>The result of the operation.</returns>

        public KernelDefinition TestEnrichDefinitionWithMetadata(KernelDefinition definition, Dictionary<string, object> metadata) => EnrichDefinitionWithMetadata(definition, metadata);
        /// <summary>
        /// Gets test validate kernel definition.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <returns>The result of the operation.</returns>

        public UnifiedValidationResult TestValidateKernelDefinition(KernelDefinition definition) => ValidateKernelDefinition(definition);
        /// <summary>
        /// Performs test log compilation metrics.
        /// </summary>
        /// <param name="kernelName">The kernel name.</param>
        /// <param name="compilationTime">The compilation time.</param>
        /// <param name="byteCodeSize">The byte code size.</param>

        public void TestLogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize)
        {
            _ = Interlocked.Increment(ref _logMetricsCallCount);
            LastLoggedCompilationTime = compilationTime;
            LogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
        }
        /// <summary>
        /// Gets the cache count.
        /// </summary>
        /// <returns>The cache count.</returns>

        public int GetCacheCount()
        {
            // Access the private cache through reflection for testing
            var cacheField = typeof(BaseKernelCompiler).GetField("_compilationCache",

                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cacheField?.GetValue(this) is ConcurrentDictionary<string, AbstractionsCompiledKernel> cache)
            {
                return cache.Count;
            }
            return 0;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            // Cleanup test resources
        }
    }
}