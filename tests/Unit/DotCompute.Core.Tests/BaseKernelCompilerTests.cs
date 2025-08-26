// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for the BaseKernelCompiler consolidation that eliminated 65% of duplicate code.
/// </summary>
public class BaseKernelCompilerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly TestKernelCompiler _compiler;

    public BaseKernelCompilerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _compiler = new TestKernelCompiler(_mockLogger.Object);
    }

    [Fact]
    public async Task CompileAsync_ValidatesKernelDefinition()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition("", null!, null!);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _compiler.CompileAsync(invalidDefinition));
    }

    [Fact]
    public async Task CompileAsync_ValidatesEmptyKernelName()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition("", "invalid source", "main");
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _compiler.CompileAsync(invalidDefinition));
        Assert.Contains("Kernel name cannot be empty", ex.Message);
    }

    [Fact]
    public async Task CompileAsync_ValidatesNullKernelCode()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition("test", null!, "main");
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _compiler.CompileAsync(invalidDefinition));
        Assert.Contains("Kernel code cannot be null or empty", ex.Message);
    }

    [Fact]
    public async Task CompileAsync_ValidatesWorkDimensions()
    {
        // Arrange
        var definition = new KernelDefinition("test", "test source", "main")
        {
            Metadata = new Dictionary<string, object> { ["WorkDimensions"] = 4 }
        };
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _compiler.CompileAsync(definition));
        Assert.Contains("Work dimensions must be between 1 and 3", ex.Message);
    }

    [Fact]
    public async Task CompileAsync_CachesCompiledKernels_WhenCachingEnabled()
    {
        // Arrange
        var definition = new KernelDefinition("test", "test source", "main");
        
        // Act
        var result1 = await _compiler.CompileAsync(definition);
        var result2 = await _compiler.CompileAsync(definition);
        
        // Assert
        Assert.Same(result1, result2); // Should return cached instance
        Assert.Equal(1, _compiler.CompileCallCount); // Should only compile once
    }

    [Fact]
    public async Task CompileAsync_DoesNotCache_WhenCachingDisabled()
    {
        // Arrange
        _compiler.DisableCaching();
        var definition = new KernelDefinition("test", "test source", "main");
        
        // Act
        var result1 = await _compiler.CompileAsync(definition);
        var result2 = await _compiler.CompileAsync(definition);
        
        // Assert
        Assert.NotSame(result1, result2); // Should create new instances
        Assert.Equal(2, _compiler.CompileCallCount); // Should compile twice
    }

    [Fact]
    public async Task CompileAsync_LogsCompilationMetrics()
    {
        // Arrange
        var definition = new KernelDefinition("test", "test source", "main");
        
        // Act
        await _compiler.CompileAsync(definition);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully compiled kernel")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CompileAsync_RecordsMetrics()
    {
        // Arrange
        var definition = new KernelDefinition("test", "test source", "main");
        
        // Act
        await _compiler.CompileAsync(definition);
        
        // Assert
        var metrics = _compiler.GetMetrics();
        Assert.Single(metrics);
        Assert.Contains(definition.Name, metrics.First().Value.KernelName);
    }

    [Fact]
    public async Task CompileAsync_HandlesCompilationErrors()
    {
        // Arrange
        _compiler.ShouldThrowOnCompile = true;
        var definition = new KernelDefinition("test", "test source", "main");
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<KernelCompilationException>(
            async () => await _compiler.CompileAsync(definition));
        Assert.Contains("Failed to compile kernel", ex.Message);
    }

    [Fact]
    public async Task ClearCache_RemovesAllCachedKernels()
    {
        // Arrange
        var definition1 = new KernelDefinition("test1", "source1", "main");
        var definition2 = new KernelDefinition("test2", "source2", "main");
        
        // Act
        await _compiler.CompileAsync(definition1);
        await _compiler.CompileAsync(definition2);
        _compiler.ClearCache();
        await _compiler.CompileAsync(definition1);
        
        // Assert
        Assert.Equal(3, _compiler.CompileCallCount); // Should recompile after cache clear
    }

    [Fact]
    public void GenerateCacheKey_CreatesDifferentKeys_ForDifferentDefinitions()
    {
        // Arrange
        var definition1 = new KernelDefinition("test1", "source1", "main");
        var definition2 = new KernelDefinition("test2", "source2", "main");
        var options = new CompilationOptions();
        
        // Act
        var key1 = _compiler.TestGenerateCacheKey(definition1, options);
        var key2 = _compiler.TestGenerateCacheKey(definition2, options);
        
        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_CreatesDifferentKeys_ForDifferentOptimizationLevels()
    {
        // Arrange
        var definition = new KernelDefinition("test", "source1", "main");
        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Aggressive };
        
        // Act
        var key1 = _compiler.TestGenerateCacheKey(definition, options1);
        var key2 = _compiler.TestGenerateCacheKey(definition, options2);
        
        // Assert
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Test implementation of BaseKernelCompiler for testing purposes.
    /// </summary>
    private sealed class TestKernelCompiler : BaseKernelCompiler
    {
        private bool _enableCaching = true;
        
        public int CompileCallCount { get; private set; }
        public bool ShouldThrowOnCompile { get; set; }

        public TestKernelCompiler(ILogger logger) : base(logger)
        {
        }

        protected override string CompilerName => "TestCompiler";

        public override IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; } = 
            new List<KernelLanguage> { KernelLanguage.CSharpIL }.AsReadOnly();

        protected override bool EnableCaching => _enableCaching;

        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            CompileCallCount++;
            
            if (ShouldThrowOnCompile)
            {
                throw new InvalidOperationException("Compilation failed");
            }
            
            var mockKernel = new Mock<ICompiledKernel>();
            mockKernel.Setup(x => x.Name).Returns(definition.Name);
            return ValueTask.FromResult(mockKernel.Object);
        }

        public void DisableCaching() => _enableCaching = false;
        
        public string TestGenerateCacheKey(KernelDefinition definition, CompilationOptions options)
            => GenerateCacheKey(definition, options);
    }
}