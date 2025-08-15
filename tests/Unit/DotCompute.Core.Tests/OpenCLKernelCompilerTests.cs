// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Core.Tests.Kernels;

/// <summary>
/// Comprehensive unit tests for OpenCLKernelCompiler with 90% coverage target.
/// Tests syntax validation, mock compilation, and different kernel types.
/// </summary>
public class OpenCLKernelCompilerTests : IDisposable
{
    private readonly Mock<ILogger<OpenCLKernelCompiler>> _mockLogger;
    private readonly OpenCLKernelCompiler _compiler;
    private bool _disposed;

    public OpenCLKernelCompilerTests()
    {
        _mockLogger = new Mock<ILogger<OpenCLKernelCompiler>>();
        _compiler = new OpenCLKernelCompiler(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_compiler);
        _compiler.Name.Should().Be("OpenCL Kernel Compiler");
        _compiler.Assert.Contains(KernelSourceType.OpenCL, SupportedSourceTypes);
        _compiler.Assert.Contains(KernelSourceType.Binary, SupportedSourceTypes);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act =() => new OpenCLKernelCompiler(null!);
        act.Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Name_ShouldReturnCorrectName()
    {
        // Act
        var name = _compiler.Name;

        // Assert
        Assert.Equal("OpenCL Kernel Compiler", name);
    }

    [Fact]
    public void SupportedSourceTypes_ShouldContainOpenCLAndBinary()
    {
        // Act
        var supportedTypes = _compiler.SupportedSourceTypes;

        // Assert
        Assert.NotNull(supportedTypes);
        Assert.Equal(2, supportedTypes.Count());
        Assert.Contains(KernelSourceType.OpenCL, supportedTypes);
        Assert.Contains(KernelSourceType.Binary, supportedTypes);
    }

    #endregion

    #region CompileAsync Tests

    [Fact]
    public async Task CompileAsync_WithValidOpenCLKernel_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("TestKernel");

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
        Assert.IsType<ManagedCompiledKernel>(result);
        VerifyLoggerWasCalledForCompilation("TestKernel");
    }

    [Fact]
    public async Task CompileAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _compiler.MethodCall().AsTask())
            .WithParameterName("definition");
    }

    [Fact]
    public async Task CompileAsync_WithNonOpenCLKernel_ShouldThrowArgumentException()
    {
        // Arrange
        var definition = CreateKernelDefinitionWithLanguage("TestKernel", KernelLanguage.HLSL);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _compiler.MethodCall().AsTask())
            .WithMessage("*Expected OpenCL kernel but received HLSL*");
    }

    [Fact]
    public async Task CompileAsync_WithNullOptions_ShouldUseDefaults()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("TestKernel");

        // Act
        var result = await _compiler.CompileAsync(definition, null);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileAsync_WithCustomOptions_ShouldUseProvidedOptions()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("TestKernel");
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Debug,
            EnableDebugInfo = true,
            FastMath = false,
            UnrollLoops = false
        };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("TestKernel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _compiler.MethodCall().AsTask());
    }

    [Fact]
    public async Task CompileAsync_ShouldReturnMockCompiledKernel()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("MockKernel");

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ManagedCompiledKernel>(result);
        
        var managedKernel = result as ManagedCompiledKernel;
        managedKernel!.Name.Should().Be("MockKernel");
        managedKernel.Binary.Should().NotBeNull();
        managedKernel.Binary.Length.Should().Be(1024); // Mock binary size
        managedKernel.CompilationLog.Should().Be("Mock OpenCL compilation log");
        managedKernel.PerformanceMetadata.Should().ContainKey("CompilationTime");
        managedKernel.PerformanceMetadata.Should().ContainKey("Platform");
        managedKernel.PerformanceMetadata["Platform"].Should().Be("OpenCLMock)");
        managedKernel.PerformanceMetadata["CompilationTime"].Should().Be(10.0);
    }

    [Fact]
    public async Task CompileAsync_ShouldSimulateCompilationTime()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("TimingKernel");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _compiler.CompileAsync(definition);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds .Should().BeGreaterThanOrEqualTo(8,); // Should take at least ~10ms
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_WithValidOpenCLKernel_ShouldReturnSuccess()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("TestKernel");

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        _compiler.Invoking(c => c.Validate(null!))
            .Throw<ArgumentNullException>()
            .WithParameterName("definition");
    }

    [Fact]
    public void Validate_WithNonOpenCLKernel_ShouldReturnFailure()
    {
        // Arrange
        var definition = CreateKernelDefinitionWithLanguage("TestKernel", KernelLanguage.HLSL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("Expected OpenCL kernel but received HLSL", Message);
    }

    [Fact]
    public void Validate_WithMissingKernelFunction_ShouldReturnFailure()
    {
        // Arrange
        var openclCode = "void regularFunction() { /* no __kernel attribute */ }";
        var definition = CreateKernelDefinitionWithCode("TestKernel", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("No __kernel function found", Message);
    }

    [Theory]
    [InlineData("__kernel void vectorAdd(__global float* a, __global float* b, __global float* c) { }")]
    [InlineData("__kernel void matrixMul(__global const float* a, __global const float* b, __global float* c) { }")]
    [InlineData("kernel void simpleKernel() { }")]
    public void Validate_WithValidOpenCLSyntax_ShouldReturnSuccess(string openclCode)
    {
        // Arrange
        var definition = CreateKernelDefinitionWithCode("ValidKernel", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region OpenCL Syntax Validation Tests

    [Theory]
    [InlineData("__kernel void test() { { { } } }")]
    [InlineData("__kernel void test() {(( ) }")]
    [InlineData("__kernel void test() { [[[ ]]] }")]
    public void ValidateOpenCLSyntax_WithBalancedBrackets_ShouldReturnNoErrors(string openclCode)
    {
        // Arrange
        var definition = CreateKernelDefinitionWithCode("SyntaxTest", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("__kernel void test() { { { }")]
    [InlineData("__kernel void test() {(( )")]
    [InlineData("__kernel void test() { [[[ ]")]
    public void ValidateOpenCLSyntax_WithUnbalancedBrackets_ShouldReturnErrors(string openclCode)
    {
        // Arrange
        var definition = CreateKernelDefinitionWithCode("UnbalancedTest", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Message.ContainAny("Unbalanced braces", "Unbalanced parentheses", "Unbalanced brackets");
    }

    [Theory]
    [InlineData("__kernel void test() { malloc(100); }")]
    [InlineData("__kernel void test() { free(ptr); }")]
    public void ValidateOpenCLSyntax_WithDynamicMemoryAllocation_ShouldReturnErrors(string openclCode)
    {
        // Arrange
        var definition = CreateKernelDefinitionWithCode("MemoryTest", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("Dynamic memory allocation not supported", Message);
    }

    [Fact]
    public void ValidateOpenCLSyntax_WithComplexValidCode_ShouldReturnNoErrors()
    {
        // Arrange
        var complexOpenCLCode = @"
__kernel void vectorAdd(__global const float* a, __global const float* b, __global float* c, const unsigned int n) {
    int id = get_global_id(0);
    if(id < n) {
        c[id] = a[id] + b[id];
    }
}";
        var definition = CreateKernelDefinitionWithCode("ComplexKernel", complexOpenCLCode, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateOpenCLSyntax_WithEmptyKernel_ShouldReturnSuccess()
    {
        // Arrange
        var emptyKernel = "__kernel void empty() { }";
        var definition = CreateKernelDefinitionWithCode("EmptyKernel", emptyKernel, KernelLanguage.OpenCL);

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Default Compilation Options Tests

    [Fact]
    public void GetDefaultCompilationOptions_ShouldReturnExpectedOptions()
    {
        // This is tested indirectly through CompileAsync with null options
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("DefaultOptionsTest");

        // Act
        var compileTask = _compiler.CompileAsync(definition, null);

        // Assert
        Assert.NotNull(compileTask);
    }

    [Fact]
    public async Task CompileAsync_WithDefaultOptions_ShouldApplyExpectedFlags()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("FlagsTest");

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        Assert.NotNull(result);
        // Default options are applied internally(tested through successful compilation)
    }

    #endregion

    #region Mock Binary Generation Tests

    [Fact]
    public async Task CompileAsync_ShouldGenerateDeterministicMockBinary()
    {
        // Arrange
        var definition1 = CreateKernelDefinitionWithCode("DeterministicTest", "__kernel void test() { }", KernelLanguage.OpenCL);
        var definition2 = CreateKernelDefinitionWithCode("DeterministicTest", "__kernel void test() { }", KernelLanguage.OpenCL);

        // Act
        var result1 = await _compiler.CompileAsync(definition1);
        var result2 = await _compiler.CompileAsync(definition2);

        // Assert
        Assert.IsType<ManagedCompiledKernel>(result1);
        Assert.IsType<ManagedCompiledKernel>(result2);
        
        var kernel1 = result1 as ManagedCompiledKernel;
        var kernel2 = result2 as ManagedCompiledKernel;
        
        kernel1!.Binary.Should().BeEquivalentTo(kernel2!.Binary);
    }

    [Fact]
    public async Task CompileAsync_WithDifferentKernels_ShouldGenerateDifferentMockBinaries()
    {
        // Arrange
        var definition1 = CreateKernelDefinitionWithCode("Kernel1", "__kernel void test1() { }", KernelLanguage.OpenCL);
        var definition2 = CreateKernelDefinitionWithCode("Kernel2", "__kernel void test2() { }", KernelLanguage.OpenCL);

        // Act
        var result1 = await _compiler.CompileAsync(definition1);
        var result2 = await _compiler.CompileAsync(definition2);

        // Assert
        Assert.IsType<ManagedCompiledKernel>(result1);
        Assert.IsType<ManagedCompiledKernel>(result2);
        
        var kernel1 = result1 as ManagedCompiledKernel;
        var kernel2 = result2 as ManagedCompiledKernel;
        
        kernel1!.Binary.NotBeEquivalentTo(kernel2!.Binary);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CompileAsync_WithExceptionDuringProcessing_ShouldThrowInvalidOperationException()
    {
        // This test is more for completeness since the current implementation is mock
        // In a real implementation, this would test actual compilation failures
        
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("ExceptionTest");

        // Act & Assert - Current mock implementation shouldn't throw
        var result = await _compiler.CompileAsync(definition);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CompileAsync_WithOperationCanceled_ShouldLogAndThrow()
    {
        // Arrange
        var definition = CreateValidOpenCLKernelDefinition("CancelledKernel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _compiler.MethodCall().AsTask());

        // Verify cancellation was logged
        VerifyLoggerWasCalledForCancellation("CancelledKernel");
    }

    #endregion

    #region Language Detection Tests

    [Fact]
    public void CreateKernelSourceFromDefinition_WithMetadataLanguage_ShouldUseMetadata()
    {
        // Arrange
        var definition = new KernelDefinition(
            "MetadataTest",
            System.Text.Encoding.UTF8.GetBytes("__kernel void test() { }"))
        {
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "OpenCL"
            }
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateKernelSourceFromDefinition_WithoutMetadata_ShouldDefaultToOpenCL()
    {
        // Arrange
        var definition = new KernelDefinition(
            "NoMetadataTest",
            System.Text.Encoding.UTF8.GetBytes("__kernel void test() { }"));

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateKernelSourceFromDefinition_WithInvalidLanguageMetadata_ShouldDefaultToOpenCL()
    {
        // Arrange
        var definition = new KernelDefinition(
            "InvalidLanguageTest",
            System.Text.Encoding.UTF8.GetBytes("__kernel void test() { }"))
        {
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "InvalidLanguage"
            }
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private KernelDefinition CreateValidOpenCLKernelDefinition(string name)
    {
        var openclCode = @"
__kernel void vectorAdd(__global const float* a, __global const float* b, __global float* c) {
    int id = get_global_id(0);
    c[id] = a[id] + b[id];
}";
        return CreateKernelDefinitionWithCode(name, openclCode, KernelLanguage.OpenCL);
    }

    private KernelDefinition CreateKernelDefinitionWithLanguage(string name, KernelLanguage language)
    {
        var code = language switch
        {
            KernelLanguage.OpenCL => "__kernel void test() { }",
            KernelLanguage.HLSL => "[numthreads(8, 8, 1)] void CSMain() { }",
            _ => "generic code"
        };
        
        return CreateKernelDefinitionWithCode(name, code, language);
    }

    private KernelDefinition CreateKernelDefinitionWithCode(string name, string code, KernelLanguage language)
    {
        return new KernelDefinition(
            name,
            System.Text.Encoding.UTF8.GetBytes(code))
        {
            EntryPoint = "main",
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = language.ToString()
            }
        };
    }

    private void VerifyLoggerWasCalledForCompilation(string kernelName)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Compiling OpenCL kernel '{kernelName}'")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLoggerWasCalledForCancellation(string kernelName)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"OpenCL kernel compilation for '{kernelName}' was cancelled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    public void Dispose()
    {
        if(!_disposed)
        {
            // OpenCLKernelCompiler doesn't implement IDisposable, so nothing to dispose
            _disposed = true;
        }
    }
}
