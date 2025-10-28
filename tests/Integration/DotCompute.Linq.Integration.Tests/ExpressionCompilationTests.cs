using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using DotCompute.Memory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Integration tests for expression compilation to compute kernels.
/// Tests the complete pipeline from LINQ expressions to executable kernels.
/// </summary>
public class ExpressionCompilationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IExpressionCompiler _compiler;
    private readonly IKernelCache _cache;
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<ICompiledKernel> _mockKernel;

    public ExpressionCompilationTests()
    {
        var services = new ServiceCollection();
        
        // Setup mocks
        _mockAccelerator = new Mock<IAccelerator>();
        _mockKernel = new Mock<ICompiledKernel>();
        
        // Configure services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IExpressionCompiler, ExpressionCompiler>();
        services.AddSingleton<IKernelCache, KernelCache>();
        services.AddSingleton<IOptimizationPipeline, OptimizationPipeline>();
        services.AddSingleton(_mockAccelerator.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _compiler = _serviceProvider.GetRequiredService<IExpressionCompiler>();
        _cache = _serviceProvider.GetRequiredService<IKernelCache>();
        
        // Setup mock behavior
        _mockAccelerator.Setup(x => x.CompileKernel(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>()))
            .ReturnsAsync(_mockKernel.Object);
        
        _mockKernel.Setup(x => x.ExecuteAsync(It.IsAny<object[]>()))
            .ReturnsAsync(new KernelExecutionResult { Success = true });
    }

    [Fact]
    public async Task CompileSimpleExpression_ShouldGenerateCorrectKernel()
    {
        // Arrange
        Expression<Func<float[], float[]>> expression = x => x.Select(v => v * 2.0f).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            OptimizationLevel = OptimizationLevel.Balanced
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompiledKernel.Should().NotBeNull();
        result.GeneratedCode.Should().Contain("mul");
        result.OptimizationMetrics.Should().NotBeNull();
    }

    [Fact]
    public async Task CompileComplexExpression_ShouldOptimizeCorrectly()
    {
        // Arrange - Complex expression with multiple operations
        Expression<Func<float[], float[], float[]>> expression = 
            (a, b) => a.Zip(b, (x, y) => x * y + 1.0f).Select(z => z / 2.0f).ToArray();
        
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.GPU,
            OptimizationLevel = OptimizationLevel.O3,
            EnableKernelFusion = true
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OptimizationMetrics.KernelsFused.Should().BeGreaterThan(0);
        result.GeneratedCode.Should().Contain("mad"); // Multiply-add optimization
    }

    [Fact]
    public async Task CompileWithCaching_ShouldReuseCompiledKernels()
    {
        // Arrange
        Expression<Func<int[], int[]>> expression = x => x.Select(v => v + 1).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            EnableCaching = true
        };

        // Act - First compilation
        var result1 = await _compiler.CompileAsync(expression, compilationOptions);
        var result2 = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result1.CompiledKernel.Should().NotBeNull();
        result2.CompiledKernel.Should().NotBeNull();
        result2.CacheHit.Should().BeTrue();
        
        // Verify cache was used
        _cache.TryGet(result1.CacheKey!, out var cachedKernel).Should().BeTrue();
        cachedKernel.Should().NotBeNull();
    }

    [Fact]
    public async Task CompileInvalidExpression_ShouldReturnError()
    {
        // Arrange - Expression with unsupported operation
        Expression<Func<string[], string[]>> expression = x => x.Select(s => s.ToUpper()).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.GPU
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("unsupported"));
    }

    [Fact]
    public async Task CompileWithDifferentOptimizationLevels_ShouldProduceDifferentResults()
    {
        // Arrange
        Expression<Func<float[], float[]>> expression = 
            x => x.Select(v => v * v).Where(v => v > 0).Select(v => v + 1).ToArray();

        var conservativeOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            OptimizationLevel = OptimizationLevel.Conservative
        };

        var aggressiveOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            OptimizationLevel = OptimizationLevel.O3,
            EnableKernelFusion = true,
            EnableVectorization = true
        };

        // Act
        var conservativeResult = await _compiler.CompileAsync(expression, conservativeOptions);
        var aggressiveResult = await _compiler.CompileAsync(expression, aggressiveOptions);

        // Assert
        conservativeResult.Success.Should().BeTrue();
        aggressiveResult.Success.Should().BeTrue();
        
        // Aggressive optimization should produce different (optimized) code
        aggressiveResult.GeneratedCode.Should().NotBe(conservativeResult.GeneratedCode);
        aggressiveResult.OptimizationMetrics.OptimizationsApplied
            .Should().BeGreaterThan(conservativeResult.OptimizationMetrics.OptimizationsApplied);
    }

    [Fact]
    public async Task CompileForGpuBackend_ShouldGenerateCudaCode()
    {
        // Arrange
        Expression<Func<double[], double[]>> expression = x => x.Select(v => Math.Sqrt(v)).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.GPU,
            GpuArchitecture = "sm_75"
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.GeneratedCode.Should().Contain("__global__");
        result.GeneratedCode.Should().Contain("sqrt");
    }

    [Fact]
    public async Task CompileWithMemoryOptimization_ShouldMinimizeAllocations()
    {
        // Arrange
        Expression<Func<float[], float[]>> expression = 
            x => x.Select(v => v * 2).Select(v => v + 1).Select(v => v / 2).ToArray();
        
        var optimizedOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            OptimizationLevel = OptimizationLevel.O3,
            EnableMemoryOptimization = true,
            EnableKernelFusion = true
        };

        // Act
        var result = await _compiler.CompileAsync(expression, optimizedOptions);

        // Assert
        result.Success.Should().BeTrue();
        result.OptimizationMetrics.MemoryAllocationsOptimized.Should().BeGreaterThan(0);
        result.OptimizationMetrics.KernelsFused.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompileParallelExpression_ShouldGenerateThreadedCode()
    {
        // Arrange
        Expression<Func<int[], int[]>> expression = 
            x => x.AsParallel().Select(v => v * v).OrderBy(v => v).ToArray();
        
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            EnableParallelization = true,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedCode.Should().Contain("parallel");
        result.OptimizationMetrics.ParallelismLevel.Should().BeGreaterThan(1);
    }

    [Theory]
    [InlineData(ComputeBackend.CPU)]
    [InlineData(ComputeBackend.GPU)]
    public async Task CompileForDifferentBackends_ShouldSucceed(ComputeBackend backend)
    {
        // Arrange
        Expression<Func<float[], float[]>> expression = x => x.Select(v => v + 1.0f).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = backend,
            OptimizationLevel = OptimizationLevel.Balanced
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TargetBackend.Should().Be(backend);
    }

    [Fact]
    public async Task CompileWithCustomKernelAttributes_ShouldApplyAttributes()
    {
        // Arrange
        Expression<Func<float[], float[]>> expression = x => x.Select(v => v * 2.0f).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.GPU,
            KernelAttributes = new Dictionary<string, object>
            {
                ["MaxThreadsPerBlock"] = 256,
                ["SharedMemorySize"] = 1024
            }
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Success.Should().BeTrue();
        result.KernelDefinition.Attributes.Should().ContainKey("MaxThreadsPerBlock");
        result.KernelDefinition.Attributes["MaxThreadsPerBlock"].Should().Be(256);
    }

    [Fact]
    public async Task CompileWithError_ShouldProvideDetailedDiagnostics()
    {
        // Arrange - Force compilation error by mocking accelerator failure
        _mockAccelerator.Setup(x => x.CompileKernel(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>()))
            .ThrowsAsync(new InvalidOperationException("Compilation failed"));

        Expression<Func<float[], float[]>> expression = x => x.Select(v => v * 2.0f).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.GPU
        };

        // Act
        var result = await _compiler.CompileAsync(expression, compilationOptions);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("Compilation failed"));
        result.CompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CompileAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        Expression<Func<float[], float[]>> expression = x => x.Select(v => v * 2.0f).ToArray();
        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(expression, compilationOptions, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CompileConcurrentExpressions_ShouldBeThreadSafe()
    {
        // Arrange
        var expressions = Enumerable.Range(0, 10)
            .Select(i => (Expression<Func<float[], float[]>>)(x => x.Select(v => v * i).ToArray()))
            .ToArray();

        var compilationOptions = new CompilationOptions
        {
            TargetBackend = ComputeBackend.CPU,
            EnableCaching = true
        };

        // Act
        var tasks = expressions.Select(expr => _compiler.CompileAsync(expr, compilationOptions)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.Success);
        
        // Verify no race conditions in caching
        foreach (var result in results)
        {
            result.CacheKey.Should().NotBeNull();
        }
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}