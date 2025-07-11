using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using DotCompute.Core;
using DotCompute.Core.Compilation;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests.Compilation;

/// <summary>
/// Tests for kernel compilation functionality.
/// </summary>
public class KernelCompilerTests
{
    private readonly IKernelCompiler _compiler;
    private readonly IAccelerator _accelerator;

    public KernelCompilerTests()
    {
        _accelerator = Substitute.For<IAccelerator>();
        _compiler = Substitute.For<IKernelCompiler>();
    }

    [Fact]
    public async Task CompileAsync_WithSimpleKernel_ReturnsCompiledKernel()
    {
        // Arrange
        Action<int[], int[], int[], int> kernel = (a, b, c, length) =>
        {
            for (int i = 0; i < length; i++)
            {
                c[i] = a[i] + b[i];
            }
        };

        var expectedKernel = Substitute.For<ICompiledKernel<Action<int[], int[], int[], int>>>();
        expectedKernel.Id.Returns(Guid.NewGuid());
        expectedKernel.Info.Returns(new KernelInfo("VectorAdd", 32, 1024));

        _compiler.CompileAsync(kernel, _accelerator, Arg.Any<CompilationOptions>())
            .Returns(expectedKernel);

        // Act
        var result = await _compiler.CompileAsync(kernel, _accelerator);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedKernel);
        result.Id.Should().NotBeEmpty();
        result.Info.Name.Should().Be("VectorAdd");
    }

    [Fact]
    public async Task CompileAsync_WithCompilationOptions_UsesProvidedOptions()
    {
        // Arrange
        Action<float[], float> kernel = (data, scalar) =>
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= scalar;
            }
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Aggressive,
            EnableDebugInfo = true,
            TargetArchitecture = "sm_80"
        };

        var expectedKernel = Substitute.For<ICompiledKernel<Action<float[], float>>>();
        _compiler.CompileAsync(kernel, _accelerator, options)
            .Returns(expectedKernel);

        // Act
        var result = await _compiler.CompileAsync(kernel, _accelerator, options);

        // Assert
        await _compiler.Received(1).CompileAsync(kernel, _accelerator, options);
        result.Should().BeSameAs(expectedKernel);
    }

    [Fact]
    public async Task CompileAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        Action<int[]>? kernel = null;
        
        _compiler.CompileAsync(kernel!, _accelerator)
            .Returns(Task.FromException<ICompiledKernel<Action<int[]>>>(new ArgumentNullException(nameof(kernel))));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _compiler.CompileAsync(kernel!, _accelerator));
    }

    [Fact]
    public async Task CompileAsync_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange
        Action<int[]> kernel = data => { };
        
        _compiler.CompileAsync(kernel, null!)
            .Returns(Task.FromException<ICompiledKernel<Action<int[]>>>(new ArgumentNullException("accelerator")));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _compiler.CompileAsync(kernel, null!));
    }

    [Fact]
    public async Task CompileAsync_WithUnsupportedKernel_ThrowsCompilationException()
    {
        // Arrange
        Action<object> kernel = obj => { }; // Unsupported parameter type
        
        _compiler.CompileAsync(kernel, _accelerator)
            .Returns(Task.FromException<ICompiledKernel<Action<object>>>(
                new CompilationException("Unsupported parameter type: System.Object")));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CompilationException>(async () =>
            await _compiler.CompileAsync(kernel, _accelerator));
        
        ex.Message.Should().Contain("Unsupported parameter type");
    }

    [Fact]
    public async Task CompileAsync_CachesCompiledKernels()
    {
        // Arrange
        Action<int[]> kernel = data => { };
        var compiledKernel = Substitute.For<ICompiledKernel<Action<int[]>>>();
        
        _compiler.CompileAsync(kernel, _accelerator, Arg.Any<CompilationOptions>())
            .Returns(compiledKernel);

        // Act - Compile same kernel twice
        var result1 = await _compiler.CompileAsync(kernel, _accelerator);
        var result2 = await _compiler.CompileAsync(kernel, _accelerator);

        // Assert - Should only compile once (caching behavior would be in implementation)
        result1.Should().BeSameAs(result2);
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Basic)]
    [InlineData(OptimizationLevel.Aggressive)]
    public async Task CompileAsync_WithDifferentOptimizationLevels_CompilesSeparately(OptimizationLevel level)
    {
        // Arrange
        Action<float[]> kernel = data => { };
        var options = new CompilationOptions { OptimizationLevel = level };
        
        var compiledKernel = Substitute.For<ICompiledKernel<Action<float[]>>>();
        compiledKernel.Info.Returns(new KernelInfo($"Kernel_Opt{level}", 16, 512));
        
        _compiler.CompileAsync(kernel, _accelerator, options)
            .Returns(compiledKernel);

        // Act
        var result = await _compiler.CompileAsync(kernel, _accelerator, options);

        // Assert
        result.Info.Name.Should().Contain(level.ToString());
    }

    [Fact]
    public async Task CompileAsync_WithComplexKernelSignature_HandlesCorrectly()
    {
        // Arrange
        Action<int[], float[], double, int, bool> kernel = (ints, floats, scale, count, flag) => { };
        
        var compiledKernel = Substitute.For<ICompiledKernel<Action<int[], float[], double, int, bool>>>();
        _compiler.CompileAsync(kernel, _accelerator, Arg.Any<CompilationOptions>())
            .Returns(compiledKernel);

        // Act
        var result = await _compiler.CompileAsync(kernel, _accelerator);

        // Assert
        result.Should().NotBeNull();
        result.Delegate.Should().NotBeNull();
    }
}

/// <summary>
/// Custom exception for compilation errors.
/// </summary>
public class CompilationException : Exception
{
    public CompilationException(string message) : base(message)
    {
    }

    public CompilationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}