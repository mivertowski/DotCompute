// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Core.Tests.Simple;

/// <summary>
/// Simplified comprehensive unit tests for kernel compilers with 90% coverage target.
/// Tests compilation, validation, and error handling for DirectCompute and OpenCL compilers.
/// </summary>
public class KernelCompilerSimpleTests : IDisposable
{
    private readonly Mock<ILogger<DirectComputeKernelCompiler>> _mockDirectComputeLogger;
    private readonly Mock<ILogger<OpenCLKernelCompiler>> _mockOpenCLLogger;
    private readonly DirectComputeKernelCompiler _directComputeCompiler;
    private readonly OpenCLKernelCompiler _openCLCompiler;
    private bool _disposed;

    public KernelCompilerSimpleTests()
    {
        _mockDirectComputeLogger = new Mock<ILogger<DirectComputeKernelCompiler>>();
        _mockOpenCLLogger = new Mock<ILogger<OpenCLKernelCompiler>>();
        _directComputeCompiler = new DirectComputeKernelCompiler(_mockDirectComputeLogger.Object);
        _openCLCompiler = new OpenCLKernelCompiler(_mockOpenCLLogger.Object);
    }

    #region DirectCompute Compiler Tests

    [Fact]
    public void DirectComputeCompiler_Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_directComputeCompiler);
        _directComputeCompiler.Name.Should().Be("DirectCompute Kernel Compiler");
        _directComputeCompiler.Assert.Contains(KernelSourceType.HLSL, SupportedSourceTypes);
        _directComputeCompiler.Assert.Contains(KernelSourceType.Binary, SupportedSourceTypes);
    }

    [Fact]
    public void DirectComputeCompiler_Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new DirectComputeKernelCompiler(null!);
        Assert.Throws<ArgumentNullException>(() => act());
    }

    [Fact]
    public async Task DirectComputeCompiler_CompileAsync_WithValidKernel_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = CreateDirectComputeKernelDefinition("TestKernel");

        // Act
        var result = await _directComputeCompiler.CompileAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task DirectComputeCompiler_CompileAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _directComputeCompiler.MethodCall().AsTask());
    }

    [Fact]
    public async Task DirectComputeCompiler_CompileAsync_WithNonHLSLKernel_ShouldThrowArgumentException()
    {
        // Arrange
        var definition = CreateOpenCLKernelDefinition("TestKernel");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _directComputeCompiler.MethodCall().AsTask());
    }

    [Fact]
    public void DirectComputeCompiler_Validate_WithValidHLSLKernel_ShouldReturnSuccess()
    {
        // Arrange
        var definition = CreateDirectComputeKernelDefinition("TestKernel");

        // Act
        var result = _directComputeCompiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DirectComputeCompiler_Validate_WithMissingNumThreads_ShouldReturnFailure()
    {
        // Arrange
        var hlslCode = "void CSMain() { /* missing numthreads attribute */ }";
        var definition = CreateKernelDefinitionWithCode("TestKernel", hlslCode, KernelLanguage.HLSL);

        // Act
        var result = _directComputeCompiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("No [numthreads] attribute found", Message);
    }

    #endregion

    #region OpenCL Compiler Tests

    [Fact]
    public void OpenCLCompiler_Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_openCLCompiler);
        _openCLCompiler.Name.Should().Be("OpenCL Kernel Compiler");
        _openCLCompiler.Assert.Contains(KernelSourceType.OpenCL, SupportedSourceTypes);
        _openCLCompiler.Assert.Contains(KernelSourceType.Binary, SupportedSourceTypes);
    }

    [Fact]
    public void OpenCLCompiler_Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new OpenCLKernelCompiler(null!);
        Assert.Throws<ArgumentNullException>(() => act());
    }

    [Fact]
    public async Task OpenCLCompiler_CompileAsync_WithValidKernel_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = CreateOpenCLKernelDefinition("TestKernel");

        // Act
        var result = await _openCLCompiler.CompileAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task OpenCLCompiler_CompileAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _openCLCompiler.MethodCall().AsTask());
    }

    [Fact]
    public async Task OpenCLCompiler_CompileAsync_WithNonOpenCLKernel_ShouldThrowArgumentException()
    {
        // Arrange
        var definition = CreateDirectComputeKernelDefinition("TestKernel");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _openCLCompiler.MethodCall().AsTask());
    }

    [Fact]
    public void OpenCLCompiler_Validate_WithValidOpenCLKernel_ShouldReturnSuccess()
    {
        // Arrange
        var definition = CreateOpenCLKernelDefinition("TestKernel");

        // Act
        var result = _openCLCompiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OpenCLCompiler_Validate_WithMissingKernelFunction_ShouldReturnFailure()
    {
        // Arrange
        var openclCode = "void regularFunction() { /* no __kernel attribute */ }";
        var definition = CreateKernelDefinitionWithCode("TestKernel", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _openCLCompiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("No __kernel function found", Message);
    }

    [Theory]
    [InlineData("__kernel void test() { { { }")]
    [InlineData("__kernel void test() { (( )")]
    public void OpenCLCompiler_Validate_WithUnbalancedBrackets_ShouldReturnFailure(string openclCode)
    {
        // Arrange
        var definition = CreateKernelDefinitionWithCode("UnbalancedTest", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _openCLCompiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Message.ContainAny("Unbalanced braces", "Unbalanced parentheses", "Unbalanced brackets");
    }

    [Theory]
    [InlineData("__kernel void test() { malloc(100); }")]
    [InlineData("__kernel void test() { free(ptr); }")]
    public void OpenCLCompiler_Validate_WithDynamicMemoryAllocation_ShouldReturnFailure(string openclCode)
    {
        // Arrange
        var definition = CreateKernelDefinitionWithCode("MemoryTest", openclCode, KernelLanguage.OpenCL);

        // Act
        var result = _openCLCompiler.Validate(definition);

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("Dynamic memory allocation not supported", Message);
    }

    #endregion

    #region Common Tests

    [Fact]
    public async Task Compilers_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var directComputeDefinition = CreateDirectComputeKernelDefinition("CancelledDC");
        var openCLDefinition = CreateOpenCLKernelDefinition("CancelledOCL");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _directComputeCompiler.MethodCall().AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _openCLCompiler.MethodCall().AsTask());
    }

    [Theory]
    [InlineData(DotCompute.Abstractions.OptimizationLevel.Debug)]
    [InlineData(DotCompute.Abstractions.OptimizationLevel.Release)]
    public async Task Compilers_WithDifferentOptimizationLevels_ShouldCompile(DotCompute.Abstractions.OptimizationLevel level)
    {
        // Arrange
        var options = new CompilationOptions
        {
            OptimizationLevel = level,
            EnableDebugInfo = level == DotCompute.Abstractions.OptimizationLevel.Debug
        };

        var directComputeDefinition = CreateDirectComputeKernelDefinition("OptimizedDC");
        var openCLDefinition = CreateOpenCLKernelDefinition("OptimizedOCL");

        // Act
        var dcResult = await _directComputeCompiler.CompileAsync(directComputeDefinition, options);
        var oclResult = await _openCLCompiler.CompileAsync(openCLDefinition, options);

        // Assert
        Assert.NotNull(dcResult);
        Assert.NotNull(oclResult);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Compilers_Validate_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        _directComputeCompiler.Invoking(c => c.Validate(null!))
            .Throw<ArgumentNullException>();

        _openCLCompiler.Invoking(c => c.Validate(null!))
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Compilers_CompileAsync_ShouldReturnManagedCompiledKernel()
    {
        // Arrange
        var directComputeDefinition = CreateDirectComputeKernelDefinition("ManagedDC");
        var openCLDefinition = CreateOpenCLKernelDefinition("ManagedOCL");

        // Act
        var dcResult = await _directComputeCompiler.CompileAsync(directComputeDefinition);
        var oclResult = await _openCLCompiler.CompileAsync(openCLDefinition);

        // Assert
        Assert.IsType<ManagedCompiledKernel>(dcResult);
        Assert.IsType<ManagedCompiledKernel>(oclResult);

        var dcManagedKernel = dcResult as ManagedCompiledKernel;
        var oclManagedKernel = oclResult as ManagedCompiledKernel;

        dcManagedKernel!.Binary.Should().NotBeNull();
        dcManagedKernel.CompilationLog.Should().NotBeNullOrEmpty();
        dcManagedKernel.PerformanceMetadata.Should().ContainKey("CompilationTime");

        oclManagedKernel!.Binary.Should().NotBeNull();
        oclManagedKernel.CompilationLog.Should().NotBeNullOrEmpty();
        oclManagedKernel.PerformanceMetadata.Should().ContainKey("CompilationTime");
    }

    #endregion

    #region Helper Methods

    private KernelDefinition CreateDirectComputeKernelDefinition(string name)
    {
        var hlslCode = @"
[numthreads(8, 8, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    // Simple compute shader
}";
        return CreateKernelDefinitionWithCode(name, hlslCode, DotCompute.Abstractions.KernelLanguage.HLSL);
    }

    private KernelDefinition CreateOpenCLKernelDefinition(string name)
    {
        var openclCode = @"
__kernel void vectorAdd(__global const float* a, __global const float* b, __global float* c) {
    int id = get_global_id(0);
    c[id] = a[id] + b[id];
}";
        return CreateKernelDefinitionWithCode(name, openclCode, DotCompute.Abstractions.KernelLanguage.OpenCL);
    }

    private KernelDefinition CreateKernelDefinitionWithCode(string name, string code, DotCompute.Abstractions.KernelLanguage language)
    {
        var source = new TextKernelSource(code, name, language, "main");
        var options = new CompilationOptions();
        return new KernelDefinition(name, source, options);
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Tests for memory interface contracts and usage patterns.
/// </summary>
public class CoreMemoryInterfaceTests
{
    #region Interface Contract Tests

    [Fact]
    public void IMemoryManager_ShouldDefineRequiredMethods()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryManager);

        // Assert
        Assert.NotNull(interfaceType);
        interfaceType.GetMethod("CreateBufferAsync").NotBeNull();
        interfaceType.GetMethod("CopyAsync").NotBeNull();
        interfaceType.GetMethod("GetStatistics").NotBeNull();
        interfaceType.GetProperty("AvailableLocations").NotBeNull();
    }

    [Fact]
    public void IMemoryManager_ShouldInheritFromIAsyncDisposable()
    {
        // Arrange & Act
        var interfaceType = typeof(IMemoryManager);

        // Assert
        interfaceType.GetInterfaces().Contain(typeof(IAsyncDisposable));
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void MemoryLocation_ShouldHaveExpectedValues()
    {
        // Act & Assert
        Enum.GetValues<MemoryLocation>().Contain(new[]
        {
            MemoryLocation.Host,
            MemoryLocation.Device,
            MemoryLocation.HostPinned,
            MemoryLocation.Unified,
            MemoryLocation.Managed
        });
    }

    [Fact]
    public void MemoryAccess_ShouldBeFlagsEnum()
    {
        // Arrange & Act
        var enumType = typeof(MemoryAccess);

        // Assert
        enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Should().NotBeEmpty();
    }

    [Fact]
    public void MemoryAccess_ShouldHaveCorrectValues()
    {
        // Act & Assert
        DotCompute.Abstractions.MemoryAccess.ReadOnly.Should().Be((DotCompute.Abstractions.MemoryAccess)1);
        DotCompute.Abstractions.MemoryAccess.WriteOnly.Should().Be((DotCompute.Abstractions.MemoryAccess)2);
        DotCompute.Abstractions.MemoryAccess.ReadWrite.Should().Be(DotCompute.Abstractions.MemoryAccess.ReadOnly | DotCompute.Abstractions.MemoryAccess.WriteOnly);
        DotCompute.Abstractions.MemoryAccess.HostAccess.Should().Be((DotCompute.Abstractions.MemoryAccess)4);
    }

    #endregion

    #region Memory Access Pattern Tests

    [Theory]
    [InlineData(DotCompute.Abstractions.MemoryAccess.ReadOnly, DotCompute.Abstractions.MemoryAccess.ReadOnly, true)]
    [InlineData(DotCompute.Abstractions.MemoryAccess.WriteOnly, DotCompute.Abstractions.MemoryAccess.WriteOnly, true)]
    [InlineData(DotCompute.Abstractions.MemoryAccess.ReadWrite, DotCompute.Abstractions.MemoryAccess.ReadOnly, true)]
    [InlineData(DotCompute.Abstractions.MemoryAccess.ReadWrite, DotCompute.Abstractions.MemoryAccess.WriteOnly, true)]
    [InlineData(DotCompute.Abstractions.MemoryAccess.ReadOnly, DotCompute.Abstractions.MemoryAccess.WriteOnly, false)]
    [InlineData(DotCompute.Abstractions.MemoryAccess.WriteOnly, DotCompute.Abstractions.MemoryAccess.ReadOnly, false)]
    public void MemoryAccess_FlagsOperations_ShouldWorkCorrectly(DotCompute.Abstractions.MemoryAccess combined, DotCompute.Abstractions.MemoryAccess flag, bool hasFlag)
    {
        // Act
        var result = combined.HasFlag(flag);

        // Assert
        Assert.Equal(hasFlag, result);
    }

    [Fact]
    public void MemoryAccess_ReadWrite_ShouldCombineReadAndWrite()
    {
        // Act
        var readWrite = DotCompute.Abstractions.MemoryAccess.ReadWrite;

        // Assert
        readWrite.HasFlag(DotCompute.Abstractions.MemoryAccess.ReadOnly).Should().BeTrue();
        readWrite.HasFlag(DotCompute.Abstractions.MemoryAccess.WriteOnly).Should().BeTrue();
        Assert.Equal(DotCompute.Abstractions.MemoryAccess.ReadOnly | DotCompute.Abstractions.MemoryAccess.WriteOnly, readWrite);
    }

    #endregion
}
