// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Moq;
using Xunit;
using FluentAssertions;
using System.Globalization;

using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for the IKernelCompiler interface and related types.
/// </summary>
public sealed class IKernelCompilerTests
{
    private readonly Mock<IKernelCompiler> _mockCompiler;
    private readonly Mock<ICompiledKernel> _mockCompiledKernel;
    private readonly KernelDefinition _testKernelDefinition;
    private readonly CompilationOptions _testCompilationOptions;

    public IKernelCompilerTests()
    {
        _mockCompiler = new Mock<IKernelCompiler>();
        _mockCompiledKernel = new Mock<ICompiledKernel>();

        var source = new TextKernelSource("__global__ void test() {}", "test", KernelLanguage.Cuda);
        _testKernelDefinition = new KernelDefinition { Name = "TestKernel", Source = source.Code };
        _testCompilationOptions = new CompilationOptions();
    }

    #region Property Tests

    [Fact]
    public void Name_ShouldReturnCompilerName()
    {
        // Arrange
        const string expectedName = "TestCompiler";
        _ = _mockCompiler.Setup(c => c.Name).Returns(expectedName);

        // Act
        var actualName = _mockCompiler.Object.Name;

        // Assert
        Assert.Equal(expectedName, actualName);
        _mockCompiler.Verify(c => c.Name, Times.Once);
    }

    [Theory]
    [InlineData("NVIDIA CUDA Compiler")]
    [InlineData("AMD ROCm Compiler")]
    [InlineData("Intel OneAPI Compiler")]
    [InlineData("OpenCL Compiler")]
    [InlineData("")]
    public void Name_ShouldHandleDifferentCompilerNames(string compilerName)
    {
        // Arrange
        _ = _mockCompiler.Setup(c => c.Name).Returns(compilerName);

        // Act
        var actualName = _mockCompiler.Object.Name;

        // Assert
        Assert.Equal(compilerName, actualName);
    }

    [Fact]
    public void SupportedSourceTypes_ShouldReturnArrayOfSourceTypes()
    {
        // Arrange
        var expectedTypes = new[] { KernelSourceType.CUDA, KernelSourceType.OpenCL, KernelSourceType.SPIRV };
        _ = _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(expectedTypes);

        // Act
        var actualTypes = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        _ = actualTypes.Should().BeEquivalentTo(expectedTypes);
        _mockCompiler.Verify(c => c.SupportedSourceTypes, Times.Once);
    }

    [Fact]
    public void SupportedSourceTypes_ShouldNotReturnNull()
    {
        []
        // Arrange
        _ = _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(Array.Empty<KernelSourceType>());

        // Act
        var actualTypes = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        Assert.NotNull(actualTypes);
        Assert.Empty(actualTypes);
    }

    [Fact]
    public void SupportedSourceTypes_ShouldHandleAllKnownSourceTypes()
    {
        // Arrange
        var allSourceTypes = Enum.GetValues<KernelSourceType>();
        _ = _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(allSourceTypes);

        // Act
        var actualTypes = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        Assert.Equal(allSourceTypes.Length, actualTypes.Length);
        _ = actualTypes.Should().BeEquivalentTo(allSourceTypes);
    }

    #endregion

    #region CompileAsync Tests

    [Fact]
    public async Task CompileAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, default))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var result = await _mockCompiler.Object.CompileAsync(_testKernelDefinition);

        // Assert
        Assert.NotNull(result);
        _ = result.Should().BeSameAs(_mockCompiledKernel.Object);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, null, default), Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithCompilationOptions_ShouldPassOptionsToCompiler()
    {
        // Arrange
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, _testCompilationOptions, default))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var result = await _mockCompiler.Object.CompileAsync(_testKernelDefinition, _testCompilationOptions);

        // Assert
        _ = result.Should().BeSameAs(_mockCompiledKernel.Object);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, _testCompilationOptions, default), Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithCancellationToken_ShouldPassTokenToCompiler()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, cancellationToken))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var result = await _mockCompiler.Object.CompileAsync(_testKernelDefinition, null, cancellationToken);

        // Assert
        _ = result.Should().BeSameAs(_mockCompiledKernel.Object);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, null, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithCancellationRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, cts.Token))
                    .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(_testKernelDefinition, null, cts.Token);
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => action().AsTask());
    }

    [Fact]
    public async Task CompileAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        _ = _mockCompiler.Setup(c => c.CompileAsync(null!, null, default))
                    .ThrowsAsync(new ArgumentNullException("definition"));

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(null!);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => action().AsTask());
        Assert.Equal("definition", ex.ParamName);
    }

    [Fact]
    public async Task CompileAsync_WithCompilationError_ShouldThrowAcceleratorException()
    {
        // Arrange
        var compilationError = new AcceleratorException("Compilation failed: syntax error");
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, default))
                    .ThrowsAsync(compilationError);

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        var ex = await Assert.ThrowsAsync<AcceleratorException>(() => action().AsTask());
        Assert.Contains("Compilation failed: syntax error", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompileAsync_WithTimeout_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, cts.Token))
                    .Returns(async (KernelDefinition def, CompilationOptions opts, CancellationToken ct) =>
                    {
                        await Task.Delay(200, ct); // Simulate long compilation
                        return _mockCompiledKernel.Object;
                    });

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(_testKernelDefinition, null, cts.Token);
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => action().AsTask());
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidDefinition_ShouldReturnSuccessResult()
    {
        // Arrange
        var successResult = ValidationResult.Success();
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(successResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        _ = result.Should().BeSameAs(successResult);
        _ = result.IsValid.Should().BeTrue();
        _ = result.ErrorMessage.Should().BeNull();
        _mockCompiler.Verify(c => c.Validate(_testKernelDefinition), Times.Once);
    }

    [Fact]
    public void Validate_WithValidDefinitionAndWarnings_ShouldReturnSuccessWithWarnings()
    {
        // Arrange
        var warnings = new[] { "Unused variable 'x'", "Performance hint: consider loop unrolling" };
        var successResult = ValidationResult.SuccessWithWarnings(warnings);
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(successResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.ErrorMessage.Should().BeNull();
        _ = result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void Validate_WithInvalidDefinition_ShouldReturnFailureResult()
    {
        // Arrange
        const string errorMessage = "Syntax error: missing semicolon on line 5";
        var failureResult = ValidationResult.Failure(errorMessage);
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(failureResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(errorMessage);
        _ = result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidDefinitionAndWarnings_ShouldReturnFailureWithWarnings()
    {
        // Arrange
        const string errorMessage = "Critical error: undefined function 'invalidCall'";
        var warnings = new[] { "Warning: potential performance issue" };
        var failureResult = ValidationResult.FailureWithWarnings(errorMessage, warnings);
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(failureResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(errorMessage);
        _ = result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void Validate_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        _ = _mockCompiler.Setup(c => c.Validate(null!)).Throws(new ArgumentNullException("definition"));

        // Act & Assert
        var action = () => _mockCompiler.Object.Validate(null!);
        var ex = Assert.Throws<ArgumentNullException>(() => action());
        Assert.Equal("definition", ex.ParamName);
    }

    [Theory]
    [InlineData("Syntax error")]
    [InlineData("Type mismatch")]
    [InlineData("Undefined symbol 'foo'")]
    [InlineData("Memory access violation")]
    public void Validate_WithSpecificErrors_ShouldReturnAppropriateFailureResult(string errorMessage)
    {
        // Arrange
        var failureResult = ValidationResult.Failure(errorMessage);
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(failureResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(errorMessage);
    }

    #endregion

    #region Interface Tests

    [Fact]
    public void IKernelCompiler_ShouldBeInterface()
    {
        // Arrange & Act
        var compilerType = typeof(IKernelCompiler);

        // Assert
        _ = compilerType.IsInterface.Should().BeTrue();
        _ = compilerType.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void IKernelCompiler_ShouldHaveExpectedMembers()
    {
        // Arrange
        var compilerType = typeof(IKernelCompiler);

        // Act
        var properties = compilerType.GetProperties();
        var methods = compilerType.GetMethods();

        // Assert
        Assert.Contains(properties, p => p.Name == "Name");
        Assert.Contains(properties, p => p.Name == "SupportedSourceTypes");

        Assert.Contains(methods, m => m.Name == "CompileAsync");
        Assert.Contains(methods, m => m.Name == "Validate");
    }

    #endregion

    #region CompilerSpecificOptions Tests

    [Fact]
    public void CompilerSpecificOptions_ShouldBeAbstractClass()
    {
        // Arrange & Act
        var optionsType = typeof(CompilerSpecificOptions);

        // Assert
        _ = optionsType.IsAbstract.Should().BeTrue();
        _ = optionsType.IsClass.Should().BeTrue();
        _ = optionsType.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void CompilerSpecificOptions_ShouldHaveCompilerNameProperty()
    {
        // Arrange
        var optionsType = typeof(CompilerSpecificOptions);

        // Act
        var compilerNameProperty = optionsType.GetProperty("CompilerName");

        // Assert
        Assert.NotNull(compilerNameProperty);
        _ = compilerNameProperty!.PropertyType.Should().Be<string>();
        _ = compilerNameProperty.GetMethod!.IsAbstract.Should().BeTrue();
    }

    /// <summary>
    /// Test implementation of CompilerSpecificOptions for testing.
    /// </summary>
    private sealed class TestCompilerOptions : CompilerSpecificOptions
    {
        public override string CompilerName => "TestCompiler";

        public string TestOption { get; set; } = "DefaultValue";
    }

    [Fact]
    public void CompilerSpecificOptions_ConcreteImplementation_ShouldWork()
    {
        // Arrange & Act
        var options = new TestCompilerOptions { TestOption = "CustomValue" };

        // Assert
        _ = options.CompilerName.Should().Be("TestCompiler");
        _ = options.TestOption.Should().Be("CustomValue");
        _ = Assert.IsAssignableFrom<CompilerSpecificOptions>(options);
    }

    #endregion

    #region KernelSourceType Enum Tests

    [Theory]
    [InlineData(KernelSourceType.ExpressionTree)]
    [InlineData(KernelSourceType.CUDA)]
    [InlineData(KernelSourceType.OpenCL)]
    [InlineData(KernelSourceType.HLSL)]
    [InlineData(KernelSourceType.SPIRV)]
    [InlineData(KernelSourceType.Metal)]
    [InlineData(KernelSourceType.HIP)]
    [InlineData(KernelSourceType.SYCL)]
    [InlineData(KernelSourceType.Binary)]
    public void KernelSourceType_ShouldHaveExpectedValues(KernelSourceType sourceType)
    {
        // Act
        var allValues = Enum.GetValues<KernelSourceType>();

        // Assert
        Assert.Contains(sourceType, allValues);
    }

    [Fact]
    public void KernelSourceType_ShouldHaveConsistentValues()
    {
        // Arrange
        var expectedValues = new[]
        {
        KernelSourceType.ExpressionTree,
        KernelSourceType.CUDA,
        KernelSourceType.OpenCL,
        KernelSourceType.HLSL,
        KernelSourceType.SPIRV,
        KernelSourceType.Metal,
        KernelSourceType.HIP,
        KernelSourceType.SYCL,
        KernelSourceType.Binary
    };

        // Act
        var actualValues = Enum.GetValues<KernelSourceType>();

        // Assert
        _ = actualValues.Should().BeEquivalentTo(expectedValues);
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public async Task CompileAsync_MultipleParallelCalls_ShouldHandleConcurrency()
    {
        // Arrange
        _ = _mockCompiler.Setup(c => c.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        var tasks = new Task<ICompiledKernel>[10];

        // Act
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _mockCompiler.Object.CompileAsync(_testKernelDefinition).AsTask();
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        _ = results.Should().AllSatisfy(r => r.Should().BeSameAs(_mockCompiledKernel.Object));
        _mockCompiler.Verify(c => c.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public void Validate_MultipleParallelCalls_ShouldHandleConcurrency()
    {
        // Arrange
        var successResult = ValidationResult.Success();
        _ = _mockCompiler.Setup(c => c.Validate(It.IsAny<KernelDefinition>())).Returns(successResult);

        var tasks = new Task<ValidationResult>[10];

        // Act
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _mockCompiler.Object.Validate(_testKernelDefinition));
        }

        Task.WaitAll(tasks);
        var results = tasks.Select(t => t.Result).ToArray();

        // Assert
        Assert.Equal(10, results.Length);
        _ = results.Should().AllSatisfy(r => r.IsValid.Should().BeTrue());
        _mockCompiler.Verify(c => c.Validate(It.IsAny<KernelDefinition>()), Times.Exactly(10));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IKernelCompiler_CompileAndValidateWorkflow_ShouldWorkTogether()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(validationResult);
        _ = _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, default))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var validation = _mockCompiler.Object.Validate(_testKernelDefinition);
        ICompiledKernel? compiledKernel = null;

        if (validation.IsValid)
        {
            compiledKernel = await _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        }

        // Assert
        _ = validation.IsValid.Should().BeTrue();
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Should().BeSameAs(_mockCompiledKernel.Object);

        _mockCompiler.Verify(c => c.Validate(_testKernelDefinition), Times.Once);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, null, default), Times.Once);
    }

    [Fact]
    public async Task IKernelCompiler_ValidationFailureShouldPreventCompilation_Workflow()
    {
        // Arrange
        var validationResult = ValidationResult.Failure("Invalid kernel source");
        _ = _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(validationResult);

        // Act
        var validation = _mockCompiler.Object.Validate(_testKernelDefinition);
        ICompiledKernel? compiledKernel = null;

        if (validation.IsValid)
        {
            compiledKernel = await _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        }

        // Assert
        _ = validation.IsValid.Should().BeFalse();
        _ = validation.ErrorMessage.Should().Be("Invalid kernel source");
        Assert.Null(compiledKernel);

        _mockCompiler.Verify(c => c.Validate(_testKernelDefinition), Times.Once);
        _mockCompiler.Verify(c => c.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Name_WhenAccessedMultipleTimes_ShouldReturnConsistentValue()
    {
        // Arrange
        const string compilerName = "ConsistentCompiler";
        _ = _mockCompiler.Setup(c => c.Name).Returns(compilerName);

        // Act
        var name1 = _mockCompiler.Object.Name;
        var name2 = _mockCompiler.Object.Name;
        var name3 = _mockCompiler.Object.Name;

        // Assert
        Assert.Equal(compilerName, name1);
        Assert.Equal(compilerName, name2);
        Assert.Equal(compilerName, name3);
    }

    [Fact]
    public void SupportedSourceTypes_WhenAccessedMultipleTimes_ShouldReturnConsistentValue()
    {
        // Arrange
        var sourceTypes = new[] { KernelSourceType.CUDA, KernelSourceType.OpenCL };
        _ = _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(sourceTypes);

        // Act
        var types1 = _mockCompiler.Object.SupportedSourceTypes;
        var types2 = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        _ = types1.Should().BeEquivalentTo(sourceTypes);
        _ = types2.Should().BeEquivalentTo(sourceTypes);
        _ = types1.Should().BeEquivalentTo(types2);
    }

    #endregion
}
