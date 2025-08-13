// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive unit tests for the IKernelCompiler interface and related types.
/// </summary>
public class IKernelCompilerTests
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
        _testKernelDefinition = new KernelDefinition("TestKernel", source, new CompilationOptions());
        _testCompilationOptions = new CompilationOptions();
    }

    #region Property Tests

    [Fact]
    public void Name_ShouldReturnCompilerName()
    {
        // Arrange
        const string expectedName = "TestCompiler";
        _mockCompiler.Setup(c => c.Name).Returns(expectedName);

        // Act
        var actualName = _mockCompiler.Object.Name;

        // Assert
        actualName.Should().Be(expectedName);
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
        _mockCompiler.Setup(c => c.Name).Returns(compilerName);

        // Act
        var actualName = _mockCompiler.Object.Name;

        // Assert
        actualName.Should().Be(compilerName);
    }

    [Fact]
    public void SupportedSourceTypes_ShouldReturnArrayOfSourceTypes()
    {
        // Arrange
        var expectedTypes = new[] { KernelSourceType.CUDA, KernelSourceType.OpenCL, KernelSourceType.SPIRV };
        _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(expectedTypes);

        // Act
        var actualTypes = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        actualTypes.Should().BeEquivalentTo(expectedTypes);
        _mockCompiler.Verify(c => c.SupportedSourceTypes, Times.Once);
    }

    [Fact]
    public void SupportedSourceTypes_ShouldNotReturnNull()
    {
        // Arrange
        _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(Array.Empty<KernelSourceType>());

        // Act
        var actualTypes = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        actualTypes.Should().NotBeNull();
        actualTypes.Should().BeEmpty();
    }

    [Fact]
    public void SupportedSourceTypes_ShouldHandleAllKnownSourceTypes()
    {
        // Arrange
        var allSourceTypes = Enum.GetValues<KernelSourceType>();
        _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(allSourceTypes);

        // Act
        var actualTypes = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        actualTypes.Should().HaveCount(allSourceTypes.Length);
        actualTypes.Should().BeEquivalentTo(allSourceTypes);
    }

    #endregion

    #region CompileAsync Tests

    [Fact]
    public async Task CompileAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, default))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var result = await _mockCompiler.Object.CompileAsync(_testKernelDefinition);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(_mockCompiledKernel.Object);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, null, default), Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithCompilationOptions_ShouldPassOptionsToCompiler()
    {
        // Arrange
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, _testCompilationOptions, default))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var result = await _mockCompiler.Object.CompileAsync(_testKernelDefinition, _testCompilationOptions);

        // Assert
        result.Should().BeSameAs(_mockCompiledKernel.Object);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, _testCompilationOptions, default), Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithCancellationToken_ShouldPassTokenToCompiler()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, cancellationToken))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var result = await _mockCompiler.Object.CompileAsync(_testKernelDefinition, null, cancellationToken);

        // Assert
        result.Should().BeSameAs(_mockCompiledKernel.Object);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, null, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithCancellationRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, cts.Token))
                    .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(_testKernelDefinition, null, cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CompileAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        _mockCompiler.Setup(c => c.CompileAsync(null!, null, default))
                    .ThrowsAsync(new ArgumentNullException("definition"));

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(null!);
        await action.Should().ThrowAsync<ArgumentNullException>().And.ParamName.Should().Be("definition");
    }

    [Fact]
    public async Task CompileAsync_WithCompilationError_ShouldThrowAcceleratorException()
    {
        // Arrange
        var compilationError = new AcceleratorException("Compilation failed: syntax error");
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, default))
                    .ThrowsAsync(compilationError);

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        await action.Should().ThrowAsync<AcceleratorException>().WithMessage("Compilation failed: syntax error");
    }

    [Fact]
    public async Task CompileAsync_WithTimeout_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, cts.Token))
                    .Returns(async (KernelDefinition def, CompilationOptions opts, CancellationToken ct) =>
                    {
                        await Task.Delay(200, ct); // Simulate long compilation
                        return _mockCompiledKernel.Object;
                    });

        // Act & Assert
        var action = () => _mockCompiler.Object.CompileAsync(_testKernelDefinition, null, cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidDefinition_ShouldReturnSuccessResult()
    {
        // Arrange
        var successResult = ValidationResult.Success();
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(successResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        result.Should().BeSameAs(successResult);
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockCompiler.Verify(c => c.Validate(_testKernelDefinition), Times.Once);
    }

    [Fact]
    public void Validate_WithValidDefinitionAndWarnings_ShouldReturnSuccessWithWarnings()
    {
        // Arrange
        var warnings = new[] { "Unused variable 'x'", "Performance hint: consider loop unrolling" };
        var successResult = ValidationResult.SuccessWithWarnings(warnings);
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(successResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void Validate_WithInvalidDefinition_ShouldReturnFailureResult()
    {
        // Arrange
        const string errorMessage = "Syntax error: missing semicolon on line 5";
        var failureResult = ValidationResult.Failure(errorMessage);
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(failureResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidDefinitionAndWarnings_ShouldReturnFailureWithWarnings()
    {
        // Arrange
        const string errorMessage = "Critical error: undefined function 'invalidCall'";
        var warnings = new[] { "Warning: potential performance issue" };
        var failureResult = ValidationResult.FailureWithWarnings(errorMessage, warnings);
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(failureResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void Validate_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        _mockCompiler.Setup(c => c.Validate(null!)).Throws(new ArgumentNullException("definition"));

        // Act & Assert
        var action = () => _mockCompiler.Object.Validate(null!);
        action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("definition");
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
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(failureResult);

        // Act
        var result = _mockCompiler.Object.Validate(_testKernelDefinition);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
    }

    #endregion

    #region Interface Tests

    [Fact]
    public void IKernelCompiler_ShouldBeInterface()
    {
        // Arrange & Act
        var compilerType = typeof(IKernelCompiler);

        // Assert
        compilerType.IsInterface.Should().BeTrue();
        compilerType.IsPublic.Should().BeTrue();
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
        properties.Should().Contain(p => p.Name == "Name");
        properties.Should().Contain(p => p.Name == "SupportedSourceTypes");
        
        methods.Should().Contain(m => m.Name == "CompileAsync");
        methods.Should().Contain(m => m.Name == "Validate");
    }

    #endregion

    #region CompilerSpecificOptions Tests

    [Fact]
    public void CompilerSpecificOptions_ShouldBeAbstractClass()
    {
        // Arrange & Act
        var optionsType = typeof(CompilerSpecificOptions);

        // Assert
        optionsType.IsAbstract.Should().BeTrue();
        optionsType.IsClass.Should().BeTrue();
        optionsType.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void CompilerSpecificOptions_ShouldHaveCompilerNameProperty()
    {
        // Arrange
        var optionsType = typeof(CompilerSpecificOptions);

        // Act
        var compilerNameProperty = optionsType.GetProperty("CompilerName");

        // Assert
        compilerNameProperty.Should().NotBeNull();
        compilerNameProperty!.PropertyType.Should().Be<string>();
        compilerNameProperty.GetMethod!.IsAbstract.Should().BeTrue();
    }

    /// <summary>
    /// Test implementation of CompilerSpecificOptions for testing.
    /// </summary>
    private class TestCompilerOptions : CompilerSpecificOptions
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
        options.CompilerName.Should().Be("TestCompiler");
        options.TestOption.Should().Be("CustomValue");
        options.Should().BeAssignableTo<CompilerSpecificOptions>();
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
        allValues.Should().Contain(sourceType);
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
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public async Task CompileAsync_MultipleParallelCalls_ShouldHandleConcurrency()
    {
        // Arrange
        _mockCompiler.Setup(c => c.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        var tasks = new Task<ICompiledKernel>[10];
        
        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(r => r.Should().BeSameAs(_mockCompiledKernel.Object));
        _mockCompiler.Verify(c => c.CompileAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public void Validate_MultipleParallelCalls_ShouldHandleConcurrency()
    {
        // Arrange
        var successResult = ValidationResult.Success();
        _mockCompiler.Setup(c => c.Validate(It.IsAny<KernelDefinition>())).Returns(successResult);

        var tasks = new Task<ValidationResult>[10];
        
        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _mockCompiler.Object.Validate(_testKernelDefinition));
        }

        Task.WaitAll(tasks);
        var results = tasks.Select(t => t.Result).ToArray();

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(r => r.IsValid.Should().BeTrue());
        _mockCompiler.Verify(c => c.Validate(It.IsAny<KernelDefinition>()), Times.Exactly(10));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IKernelCompiler_CompileAndValidateWorkflow_ShouldWorkTogether()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(validationResult);
        _mockCompiler.Setup(c => c.CompileAsync(_testKernelDefinition, null, default))
                    .ReturnsAsync(_mockCompiledKernel.Object);

        // Act
        var validation = _mockCompiler.Object.Validate(_testKernelDefinition);
        ICompiledKernel? compiledKernel = null;
        
        if (validation.IsValid)
        {
            compiledKernel = await _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        }

        // Assert
        validation.IsValid.Should().BeTrue();
        compiledKernel.Should().NotBeNull();
        compiledKernel.Should().BeSameAs(_mockCompiledKernel.Object);
        
        _mockCompiler.Verify(c => c.Validate(_testKernelDefinition), Times.Once);
        _mockCompiler.Verify(c => c.CompileAsync(_testKernelDefinition, null, default), Times.Once);
    }

    [Fact]
    public async Task IKernelCompiler_ValidationFailureShouldPreventCompilation_Workflow()
    {
        // Arrange
        var validationResult = ValidationResult.Failure("Invalid kernel source");
        _mockCompiler.Setup(c => c.Validate(_testKernelDefinition)).Returns(validationResult);

        // Act
        var validation = _mockCompiler.Object.Validate(_testKernelDefinition);
        ICompiledKernel? compiledKernel = null;
        
        if (validation.IsValid)
        {
            compiledKernel = await _mockCompiler.Object.CompileAsync(_testKernelDefinition);
        }

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.ErrorMessage.Should().Be("Invalid kernel source");
        compiledKernel.Should().BeNull();
        
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
        _mockCompiler.Setup(c => c.Name).Returns(compilerName);

        // Act
        var name1 = _mockCompiler.Object.Name;
        var name2 = _mockCompiler.Object.Name;
        var name3 = _mockCompiler.Object.Name;

        // Assert
        name1.Should().Be(compilerName);
        name2.Should().Be(compilerName);
        name3.Should().Be(compilerName);
        name1.Should().Be(name2).And.Be(name3);
    }

    [Fact]
    public void SupportedSourceTypes_WhenAccessedMultipleTimes_ShouldReturnConsistentValue()
    {
        // Arrange
        var sourceTypes = new[] { KernelSourceType.CUDA, KernelSourceType.OpenCL };
        _mockCompiler.Setup(c => c.SupportedSourceTypes).Returns(sourceTypes);

        // Act
        var types1 = _mockCompiler.Object.SupportedSourceTypes;
        var types2 = _mockCompiler.Object.SupportedSourceTypes;

        // Assert
        types1.Should().BeEquivalentTo(sourceTypes);
        types2.Should().BeEquivalentTo(sourceTypes);
        types1.Should().BeEquivalentTo(types2);
    }

    #endregion
}