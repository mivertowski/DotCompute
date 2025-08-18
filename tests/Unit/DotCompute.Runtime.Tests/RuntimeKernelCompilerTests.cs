// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using DotCompute.Tests;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Runtime.Tests;

/// <summary>
/// Comprehensive unit tests for RuntimeKernelCompiler to achieve 90%+ coverage.
/// Tests all public methods, properties, error conditions, and edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Mock)]
[Trait("Category", TestCategories.CI)]
public sealed class RuntimeKernelCompilerTests : IDisposable
{
    private readonly Mock<ILogger<RuntimeKernelCompiler>> _mockLogger;
    private readonly RuntimeKernelCompiler _compiler;

    public RuntimeKernelCompilerTests()
    {
        _mockLogger = new Mock<ILogger<RuntimeKernelCompiler>>();
        _compiler = new RuntimeKernelCompiler(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Constructor_WithValidLogger_ShouldSucceed()
    {
        // Arrange & Act
        var compiler = new RuntimeKernelCompiler(_mockLogger.Object);

        // Assert
        compiler.Should().NotBeNull();
        compiler.Name.Should().Be("Runtime Kernel Compiler");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new RuntimeKernelCompiler(null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("logger");
    }

    #endregion

    #region Property Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Name_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var name = _compiler.Name;

        // Assert
        name.Should().Be("Runtime Kernel Compiler");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SupportedSourceTypes_ShouldReturnExpectedTypes()
    {
        // Arrange & Act
        var supportedTypes = _compiler.SupportedSourceTypes;

        // Assert
        supportedTypes.Should().NotBeNull();
        supportedTypes.Should().Contain(KernelSourceType.ExpressionTree);
        supportedTypes.Should().Contain(KernelSourceType.CUDA);
        supportedTypes.Should().Contain(KernelSourceType.OpenCL);
        supportedTypes.Should().Contain(KernelSourceType.HLSL);
        supportedTypes.Should().Contain(KernelSourceType.Binary);
        supportedTypes.Length.Should().Be(5);
    }

    #endregion

    #region CompileAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();

        // Act
        var compiledKernel = await _compiler.CompileAsync(definition);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be(definition.Name);
        compiledKernel.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = async () => await _compiler.CompileAsync(null!);
        var result = await act.Should().ThrowAsync<ArgumentNullException>();
        result.Which.ParamName.Should().Be("definition");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithCompilationOptions_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true,
            UnrollLoops = true
        };

        // Act
        var compiledKernel = await _compiler.CompileAsync(definition, options);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be(definition.Name);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithNullOptions_ShouldUseDefaultOptions()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();

        // Act
        var compiledKernel = await _compiler.CompileAsync(definition, null);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be(definition.Name);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithInvalidDefinition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition
        {
            Name = "", // Invalid: empty name
            Code = new byte[] { 1, 2, 3 }
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(invalidDefinition);
        var result = await act.Should().ThrowAsync<InvalidOperationException>();
        result.Which.Message.Should().Contain("Kernel validation failed");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithEmptyCode_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition
        {
            Name = "TestKernel",
            Code = Array.Empty<byte>(), // Invalid: empty code
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(invalidDefinition);
        var result = await act.Should().ThrowAsync<InvalidOperationException>();
        result.Which.Message.Should().Contain("Kernel validation failed");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithNullCode_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition
        {
            Name = "TestKernel",
            Code = null!, // Invalid: null code
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(invalidDefinition);
        var result = await act.Should().ThrowAsync<InvalidOperationException>();
        result.Which.Message.Should().Contain("Kernel validation failed");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_SameDefinition_ShouldReturnCachedKernel()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();

        // Act
        var kernel1 = await _compiler.CompileAsync(definition);
        var kernel2 = await _compiler.CompileAsync(definition);

        // Assert
        kernel1.Should().BeSameAs(kernel2);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_DifferentOptimizationLevels_ShouldCreateDifferentCacheEntries()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();
        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };

        // Act
        var kernel1 = await _compiler.CompileAsync(definition, options1);
        var kernel2 = await _compiler.CompileAsync(definition, options2);

        // Assert
        kernel1.Should().NotBeSameAs(kernel2);
    }

    #endregion

    #region Validate Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Validate_WithValidDefinition_ShouldReturnSuccess()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Validate_WithEmptyName_ShouldReturnFailure()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "",
            Code = new byte[] { 1, 2, 3 },
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kernel name cannot be empty");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Validate_WithNullName_ShouldReturnFailure()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = null!,
            Code = new byte[] { 1, 2, 3 },
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kernel name cannot be empty");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Validate_WithNullCode_ShouldReturnFailure()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "TestKernel",
            Code = null!,
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kernel code cannot be null or empty");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Validate_WithEmptyCode_ShouldReturnFailure()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "TestKernel",
            Code = Array.Empty<byte>(),
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kernel code cannot be null or empty");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompileAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        const int taskCount = 10;
        var definition = CreateValidKernelDefinition();

        // Act
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => _compiler.CompileAsync(definition))
            .ToArray();

        var kernels = await Task.WhenAll(tasks);

        // Assert
        kernels.Should().HaveCount(taskCount);
        kernels.Should().OnlyContain(k => k.Name == definition.Name);
        // All kernels should be the same instance due to caching
        kernels.Should().OnlyContain(k => ReferenceEquals(k, kernels[0]));
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompileAsync_ConcurrentDifferentKernels_ShouldCreateDifferentKernels()
    {
        // Arrange
        const int taskCount = 5;
        var definitions = Enumerable.Range(0, taskCount)
            .Select(i => CreateValidKernelDefinition($"Kernel{i}"))
            .ToArray();

        // Act
        var tasks = definitions.Select(def => _compiler.CompileAsync(def)).ToArray();
        var kernels = await Task.WhenAll(tasks);

        // Assert
        kernels.Should().HaveCount(taskCount);
        kernels.Should().OnlyHaveUniqueItems();
        for (int i = 0; i < taskCount; i++)
        {
            kernels[i].Name.Should().Be($"Kernel{i}");
        }
    }

    #endregion

    #region Cache Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_CacheEviction_ShouldWorkCorrectly()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();
        
        // Act - Compile and let the weak reference get collected
        var kernel1 = await _compiler.CompileAsync(definition);
        _ = new WeakReference(kernel1);
        kernel1 = null!;
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Compile again
        var kernel2 = await _compiler.CompileAsync(definition);

        // Assert
        kernel2.Should().NotBeNull();
        kernel2.Name.Should().Be(definition.Name);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_DifferentCodeSameNameAndOptions_ShouldCreateDifferentKernels()
    {
        // Arrange
        var definition1 = CreateValidKernelDefinition("TestKernel", new byte[] { 1, 2, 3 });
        var definition2 = CreateValidKernelDefinition("TestKernel", new byte[] { 4, 5, 6 });

        // Act
        var kernel1 = await _compiler.CompileAsync(definition1);
        var kernel2 = await _compiler.CompileAsync(definition2);

        // Assert
        kernel1.Should().NotBeSameAs(kernel2);
    }

    #endregion

    #region Performance Tests

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_Performance_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var kernel = await _compiler.CompileAsync(definition);

        // Assert
        stopwatch.Stop();
        kernel.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.Unit)]
    public async Task CompileAsync_CachedCompilation_ShouldBeFaster()
    {
        // Arrange
        var definition = CreateValidKernelDefinition();

        // Act - First compilation
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        await _compiler.CompileAsync(definition);
        stopwatch1.Stop();

        // Act - Second compilation (cached)
        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        await _compiler.CompileAsync(definition);
        stopwatch2.Stop();

        // Assert
        stopwatch2.ElapsedMilliseconds.Should().BeLessThan(stopwatch1.ElapsedMilliseconds);
    }

    #endregion

    #region Edge Cases

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task CompileAsync_VeryLargeCode_ShouldSucceed()
    {
        // Arrange
        var largeCode = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(largeCode);
        var definition = CreateValidKernelDefinition("LargeKernel", largeCode);

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task CompileAsync_VeryLongKernelName_ShouldSucceed()
    {
        // Arrange
        var longName = new string('A', 1000);
        var definition = CreateValidKernelDefinition(longName);

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task CompileAsync_AllSourceTypes_ShouldSucceed()
    {
        // Arrange & Act & Assert
        foreach (var sourceType in _compiler.SupportedSourceTypes)
        {
            var definition = new KernelDefinition
            {
                Name = $"TestKernel_{sourceType}",
                Code = new byte[] { 1, 2, 3 },
                SourceType = sourceType
            };

            var act = async () => await _compiler.CompileAsync(definition);
            await act.Should().NotThrowAsync($"Source type {sourceType} should be supported");
        }
    }

    #endregion

    #region Helper Methods

    private static KernelDefinition CreateValidKernelDefinition(string name = "TestKernel", byte[]? code = null)
    {
        return new KernelDefinition
        {
            Name = name,
            Code = code ?? new byte[] { 1, 2, 3, 4, 5 }
        };
    }

    #endregion

    public void Dispose()
    {
        // Compiler doesn't implement IDisposable, but keeping for consistency
    }
}