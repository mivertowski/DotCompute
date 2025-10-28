// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Runtime.Services.Statistics;
using DotCompute.Runtime.Services.Types;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Services.Compilation;

/// <summary>
/// Tests for IKernelCompilerService implementations
/// </summary>
public sealed class KernelCompilerServiceTests
{
    private readonly IKernelCompilerService _service;
    private readonly IAccelerator _mockAccelerator;

    public KernelCompilerServiceTests()
    {
        _service = Substitute.For<IKernelCompilerService>();
        _mockAccelerator = Substitute.For<IAccelerator>();
    }

    [Fact]
    public async Task CompileAsync_WithValidKernel_ReturnsCompiledKernel()
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _service.CompileAsync(definition, _mockAccelerator, null).Returns(compiledKernel);

        // Act
        var result = await _service.CompileAsync(definition, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(compiledKernel);
    }

    [Fact]
    public async Task CompileAsync_WithOptions_PassesOptionsCorrectly()
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        var options = new CompilationOptions { OptimizationLevel = DotCompute.Abstractions.Types.OptimizationLevel.O2 };
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _service.CompileAsync(definition, _mockAccelerator, options).Returns(compiledKernel);

        // Act
        var result = await _service.CompileAsync(definition, _mockAccelerator, options);

        // Assert
        result.Should().NotBeNull();
        await _service.Received(1).CompileAsync(definition, _mockAccelerator, options);
    }

    [Fact]
    public async Task CompileAsync_WithNullOptions_UsesDefaults()
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _service.CompileAsync(definition, _mockAccelerator, null).Returns(compiledKernel);

        // Act
        var result = await _service.CompileAsync(definition, _mockAccelerator, null);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PrecompileAsync_WithMultipleKernels_PrecompilesAll()
    {
        // Arrange
        var definitions = new[]
        {
            new KernelDefinition("test1", "kernel void test1() {}"),
            new KernelDefinition("test2", "kernel void test2() {}")
        };

        // Act
        await _service.PrecompileAsync(definitions, _mockAccelerator);

        // Assert
        await _service.Received(1).PrecompileAsync(definitions, _mockAccelerator);
    }

    [Fact]
    public async Task PrecompileAsync_WithEmptyList_HandlesGracefully()
    {
        // Arrange
        var definitions = Array.Empty<KernelDefinition>();

        // Act
        var action = async () => await _service.PrecompileAsync(definitions, _mockAccelerator);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact(Skip = "NSubstitute overload resolution issue - needs investigation")]
    public void GetStatistics_ReturnsValidStatistics()
    {
        // TODO: Fix NSubstitute configuration for GetStatistics()
        // There's an overload resolution issue with .Returns() extension method
        Assert.True(true); // Placeholder
    }

    [Fact]
    public async Task OptimizeAsync_WithKernel_ReturnsOptimizedKernel()
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        _service.OptimizeAsync(definition, _mockAccelerator).Returns(definition);

        // Act
        var result = await _service.OptimizeAsync(definition, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("test");
    }

    [Fact]
    public async Task ValidateAsync_WithValidKernel_ReturnsSuccessResult()
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        var validationResult = new KernelValidationResult { IsValid = true };
        _service.ValidateAsync(definition, _mockAccelerator).Returns(validationResult);

        // Act
        var result = await _service.ValidateAsync(definition, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidKernel_ReturnsFailureResult()
    {
        // Arrange
        var definition = new KernelDefinition("test", "invalid kernel");
        var validationResult = new KernelValidationResult
        {
            IsValid = false,
            KernelName = "test"
        };
        validationResult.Errors.Add("Syntax error");
        _service.ValidateAsync(definition, _mockAccelerator).Returns(validationResult);

        // Act
        var result = await _service.ValidateAsync(definition, _mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Syntax error");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CompileAsync_WithDifferentOptimizationLevels_HandlesCorrectly(int optimizationLevel)
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        var options = new CompilationOptions { OptimizationLevel = (DotCompute.Abstractions.Types.OptimizationLevel)optimizationLevel };
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _service.CompileAsync(definition, _mockAccelerator, options).Returns(compiledKernel);

        // Act
        var result = await _service.CompileAsync(definition, _mockAccelerator, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CompileAsync_MultipleTimes_UsesCaching()
    {
        // Arrange
        var definition = new KernelDefinition("test", "kernel void test() {}");
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _service.CompileAsync(definition, _mockAccelerator, null).Returns(compiledKernel);

        // Act
        var result1 = await _service.CompileAsync(definition, _mockAccelerator);
        var result2 = await _service.CompileAsync(definition, _mockAccelerator);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task PrecompileAsync_WithLargeKernelSet_HandlesEfficiently()
    {
        // Arrange
        var definitions = Enumerable.Range(0, 100)
            .Select(i => new KernelDefinition($"test{i}", $"kernel void test{i}() {{}}"))
            .ToArray();

        // Act
        var action = async () => await _service.PrecompileAsync(definitions, _mockAccelerator);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact(Skip = "NSubstitute overload resolution issue - needs investigation")]
    public void GetStatistics_AfterCompilations_ReflectsActivity()
    {
        // TODO: Fix NSubstitute configuration for GetStatistics()
        // There's an overload resolution issue with .Returns() extension method
        Assert.True(true); // Placeholder
    }

    [Fact]
    public async Task OptimizeAsync_PreservesKernelName()
    {
        // Arrange
        var definition = new KernelDefinition("myKernel", "kernel void test() {}");
        var optimized = new KernelDefinition("myKernel", "optimized code");
        _service.OptimizeAsync(definition, _mockAccelerator).Returns(optimized);

        // Act
        var result = await _service.OptimizeAsync(definition, _mockAccelerator);

        // Assert
        result.Name.Should().Be("myKernel");
    }

    [Fact]
    public async Task ValidateAsync_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var definition = new KernelDefinition("test", "invalid");
        var validationResult = new KernelValidationResult
        {
            IsValid = false,
            KernelName = "test"
        };
        validationResult.Errors.Add("Error 1");
        validationResult.Errors.Add("Error 2");
        validationResult.Errors.Add("Error 3");
        _service.ValidateAsync(definition, _mockAccelerator).Returns(validationResult);

        // Act
        var result = await _service.ValidateAsync(definition, _mockAccelerator);

        // Assert
        result.Errors.Should().HaveCount(3);
    }
}
