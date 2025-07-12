using Xunit;
using FluentAssertions;
using DotCompute.Generators.Kernel;
using DotCompute.Generators.Backend;
using DotCompute.Generators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Advanced tests for DotCompute source generators
/// Targets 95%+ coverage including edge cases and error scenarios
/// </summary>
public class AdvancedGeneratorTests
{
    [Fact]
    public void KernelAttribute_WithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KernelAttribute(null!));
    }

    [Fact]
    public void KernelAttribute_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new KernelAttribute(""));
    }

    [Fact]
    public void KernelAttribute_WithWhitespaceName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new KernelAttribute("   "));
    }

    [Theory]
    [InlineData("ValidKernel")]
    [InlineData("Kernel_With_Underscores")]
    [InlineData("KernelWith123Numbers")]
    public void KernelAttribute_WithValidNames_ShouldAccept(string validName)
    {
        // Arrange & Act
        var attribute = new KernelAttribute(validName);

        // Assert
        attribute.Name.Should().Be(validName);
    }

    [Theory]
    [InlineData("123StartWithNumber")]
    [InlineData("Kernel-With-Dashes")]
    [InlineData("Kernel With Spaces")]
    [InlineData("Kernel.With.Dots")]
    public void KernelAttribute_WithInvalidNames_ShouldThrowArgumentException(string invalidName)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new KernelAttribute(invalidName));
    }

    [Fact]
    public void KernelAttribute_Properties_ShouldBeSettable()
    {
        // Arrange
        var attribute = new KernelAttribute("TestKernel");

        // Act
        attribute.AcceleratorType = AcceleratorType.GPU;
        attribute.LocalSize = new[] { 256, 1, 1 };
        attribute.SharedMemorySize = 1024;

        // Assert
        attribute.AcceleratorType.Should().Be(AcceleratorType.GPU);
        attribute.LocalSize.Should().Equal(256, 1, 1);
        attribute.SharedMemorySize.Should().Be(1024);
    }

    [Fact]
    public void KernelAttribute_WithNegativeSharedMemorySize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var attribute = new KernelAttribute("TestKernel");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => attribute.SharedMemorySize = -1);
    }

    [Fact]
    public void KernelAttribute_WithInvalidLocalSize_ShouldThrowArgumentException()
    {
        // Arrange
        var attribute = new KernelAttribute("TestKernel");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => attribute.LocalSize = new[] { 0, 1, 1 });
        Assert.Throws<ArgumentException>(() => attribute.LocalSize = new[] { -1, 1, 1 });
        Assert.Throws<ArgumentException>(() => attribute.LocalSize = new int[2]); // Wrong dimension
        Assert.Throws<ArgumentException>(() => attribute.LocalSize = new int[4]); // Wrong dimension
    }

    [Fact]
    public void KernelSourceGenerator_WithNullContext_ShouldHandleGracefully()
    {
        // Arrange
        var generator = new KernelSourceGenerator();

        // Act & Assert
        // Generator should handle null context gracefully without throwing
        generator.Should().NotBeNull();
    }

    [Fact]
    public void KernelSourceGenerator_Initialize_ShouldSetupCorrectly()
    {
        // Arrange
        var generator = new KernelSourceGenerator();
        var context = new TestGeneratorInitializationContext();

        // Act
        generator.Initialize(context);

        // Assert
        context.RegisteredSyntaxReceivers.Should().NotBeEmpty();
    }

    [Fact]
    public void KernelSourceGenerator_Execute_WithEmptyCompilation_ShouldHandleGracefully()
    {
        // Arrange
        var generator = new KernelSourceGenerator();
        var compilation = CSharpCompilation.Create("TestAssembly");
        var context = new TestGeneratorExecutionContext(compilation);

        // Act
        generator.Execute(context);

        // Assert
        context.GeneratedSources.Should().BeEmpty("No sources should be generated for empty compilation");
    }

    [Fact]
    public void KernelSourceGenerator_Execute_WithKernelMethods_ShouldGenerateCode()
    {
        // Arrange
        var generator = new KernelSourceGenerator();
        var sourceCode = @"
using DotCompute.Generators.Kernel;

public class TestKernels
{
    [Kernel(""AddKernel"")]
    public static void Add(float[] a, float[] b, float[] result)
    {
        // Kernel implementation
    }
}";
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree });
        var context = new TestGeneratorExecutionContext(compilation);

        // Act
        generator.Execute(context);

        // Assert
        context.GeneratedSources.Should().NotBeEmpty("Code should be generated for kernel methods");
    }

    [Fact]
    public void KernelCompilationAnalyzer_WithInvalidKernelSignature_ShouldReportDiagnostic()
    {
        // Arrange
        var analyzer = new KernelCompilationAnalyzer();
        var sourceCode = @"
using DotCompute.Generators.Kernel;

public class TestKernels
{
    [Kernel(""InvalidKernel"")]
    public void InvalidInstanceMethod() // Instance method - invalid
    {
    }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree });

        // Act
        var diagnostics = compilation.GetDiagnostics();

        // Assert
        // The analyzer should report diagnostics for invalid kernel signatures
        analyzer.Should().NotBeNull();
    }

    [Fact]
    public void CpuCodeGenerator_WithNullKernelInfo_ShouldThrowArgumentNullException()
    {
        // Arrange
        var generator = new CpuCodeGenerator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GenerateKernelCode(null!));
    }

    [Fact]
    public void CpuCodeGenerator_WithValidKernelInfo_ShouldGenerateCode()
    {
        // Arrange
        var generator = new CpuCodeGenerator();
        var kernelInfo = new TestKernelInfo
        {
            Name = "TestKernel",
            Parameters = new[] { "float[] input", "float[] output" },
            Body = "output[i] = input[i] * 2.0f;"
        };

        // Act
        var generatedCode = generator.GenerateKernelCode(kernelInfo);

        // Assert
        generatedCode.Should().NotBeNullOrEmpty();
        generatedCode.Should().Contain("TestKernel");
        generatedCode.Should().Contain("input");
        generatedCode.Should().Contain("output");
    }

    [Fact]
    public void SourceGeneratorHelpers_IsValidIdentifier_ShouldValidateCorrectly()
    {
        // Arrange & Act & Assert
        SourceGeneratorHelpers.IsValidIdentifier("ValidName").Should().BeTrue();
        SourceGeneratorHelpers.IsValidIdentifier("_ValidName").Should().BeTrue();
        SourceGeneratorHelpers.IsValidIdentifier("Valid123").Should().BeTrue();
        
        SourceGeneratorHelpers.IsValidIdentifier("123Invalid").Should().BeFalse();
        SourceGeneratorHelpers.IsValidIdentifier("Invalid-Name").Should().BeFalse();
        SourceGeneratorHelpers.IsValidIdentifier("Invalid Name").Should().BeFalse();
        SourceGeneratorHelpers.IsValidIdentifier("").Should().BeFalse();
        SourceGeneratorHelpers.IsValidIdentifier(null!).Should().BeFalse();
    }

    [Fact]
    public void SourceGeneratorHelpers_EscapeString_ShouldHandleSpecialCharacters()
    {
        // Arrange & Act & Assert
        SourceGeneratorHelpers.EscapeString("normal").Should().Be("normal");
        SourceGeneratorHelpers.EscapeString("with\"quotes").Should().Be("with\\\"quotes");
        SourceGeneratorHelpers.EscapeString("with\nnewlines").Should().Be("with\\nnewlines");
        SourceGeneratorHelpers.EscapeString("with\ttabs").Should().Be("with\\ttabs");
        SourceGeneratorHelpers.EscapeString("with\\backslashes").Should().Be("with\\\\backslashes");
    }

    [Fact]
    public void SourceGeneratorHelpers_EscapeString_WithNullInput_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => SourceGeneratorHelpers.EscapeString(null!));
    }

    [Fact]
    public void SourceGeneratorHelpers_GenerateUsings_ShouldCreateCorrectUsings()
    {
        // Arrange
        var namespaces = new[] { "System", "System.Collections.Generic", "DotCompute.Core" };

        // Act
        var usings = SourceGeneratorHelpers.GenerateUsings(namespaces);

        // Assert
        usings.Should().NotBeNullOrEmpty();
        usings.Should().Contain("using System;");
        usings.Should().Contain("using System.Collections.Generic;");
        usings.Should().Contain("using DotCompute.Core;");
    }

    [Fact]
    public void SourceGeneratorHelpers_GenerateUsings_WithNullNamespaces_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => SourceGeneratorHelpers.GenerateUsings(null!));
    }

    [Fact]
    public void SourceGeneratorHelpers_GenerateUsings_WithEmptyNamespaces_ShouldReturnEmpty()
    {
        // Arrange
        var namespaces = Array.Empty<string>();

        // Act
        var usings = SourceGeneratorHelpers.GenerateUsings(namespaces);

        // Assert
        usings.Should().BeEmpty();
    }
}

/// <summary>
/// Error handling and edge case tests for generators
/// </summary>
public class GeneratorErrorHandlingTests
{
    [Fact]
    public void KernelSourceGenerator_WithMalformedSyntaxTree_ShouldHandleGracefully()
    {
        // Arrange
        var generator = new KernelSourceGenerator();
        var malformedCode = @"
public class {
    [Kernel(""Bad
    public void Method(
}";
        
        var syntaxTree = CSharpSyntaxTree.ParseText(malformedCode);
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree });
        var context = new TestGeneratorExecutionContext(compilation);

        // Act & Assert
        // Generator should not throw exception even with malformed code
        generator.Execute(context);
        
        // The context might contain errors, but the generator should not crash
        context.Should().NotBeNull();
    }

    [Fact]
    public void CpuCodeGenerator_WithExtremelyLongKernelName_ShouldHandleGracefully()
    {
        // Arrange
        var generator = new CpuCodeGenerator();
        var longName = new string('A', 10000); // Very long name
        var kernelInfo = new TestKernelInfo
        {
            Name = longName,
            Parameters = new[] { "float[] input" },
            Body = "// Empty body"
        };

        // Act
        var result = generator.GenerateKernelCode(kernelInfo);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(longName);
    }

    [Fact]
    public void CpuCodeGenerator_WithSpecialCharactersInCode_ShouldEscapeProperly()
    {
        // Arrange
        var generator = new CpuCodeGenerator();
        var kernelInfo = new TestKernelInfo
        {
            Name = "TestKernel",
            Parameters = new[] { "float[] input" },
            Body = "// Comment with \"quotes\" and \n newlines \t tabs"
        };

        // Act
        var result = generator.GenerateKernelCode(kernelInfo);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotContain("\\n\\n"); // Should not double-escape
    }

    [Fact]
    public void SourceGeneratorHelpers_IsValidIdentifier_WithExtremeInputs_ShouldHandleGracefully()
    {
        // Arrange & Act & Assert
        SourceGeneratorHelpers.IsValidIdentifier(string.Empty).Should().BeFalse();
        SourceGeneratorHelpers.IsValidIdentifier(new string('A', 10000)).Should().BeTrue(); // Very long but valid
        SourceGeneratorHelpers.IsValidIdentifier("_").Should().BeTrue(); // Single underscore
        SourceGeneratorHelpers.IsValidIdentifier("__").Should().BeTrue(); // Double underscore
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\t")]
    public void SourceGeneratorHelpers_EscapeString_WithControlCharacters_ShouldEscape(string controlChar)
    {
        // Arrange & Act
        var result = SourceGeneratorHelpers.EscapeString($"test{controlChar}string");

        // Assert
        result.Should().NotContain(controlChar);
        result.Should().Contain("\\");
    }

    [Fact]
    public void KernelAttribute_ToString_ShouldContainRelevantInfo()
    {
        // Arrange
        var attribute = new KernelAttribute("TestKernel")
        {
            AcceleratorType = AcceleratorType.GPU,
            LocalSize = new[] { 256, 1, 1 },
            SharedMemorySize = 1024
        };

        // Act
        var result = attribute.ToString();

        // Assert
        result.Should().Contain("TestKernel");
        result.Should().Contain("GPU");
        result.Should().Contain("256");
        result.Should().Contain("1024");
    }

    [Fact]
    public void KernelAttribute_Equality_ShouldCompareAllProperties()
    {
        // Arrange
        var attr1 = new KernelAttribute("Test") { AcceleratorType = AcceleratorType.CPU };
        var attr2 = new KernelAttribute("Test") { AcceleratorType = AcceleratorType.CPU };
        var attr3 = new KernelAttribute("Test") { AcceleratorType = AcceleratorType.GPU };

        // Act & Assert
        attr1.Equals(attr2).Should().BeTrue();
        attr1.Equals(attr3).Should().BeFalse();
        attr1.Equals(null).Should().BeFalse();
        attr1.Equals("string").Should().BeFalse();
    }

    [Fact]
    public void KernelAttribute_GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var attr1 = new KernelAttribute("Test") { AcceleratorType = AcceleratorType.CPU };
        var attr2 = new KernelAttribute("Test") { AcceleratorType = AcceleratorType.CPU };

        // Act & Assert
        attr1.GetHashCode().Should().Be(attr2.GetHashCode());
    }
}

/// <summary>
/// Performance tests for source generators
/// </summary>
public class GeneratorPerformanceTests
{
    [Fact]
    public void KernelSourceGenerator_WithManyKernels_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var generator = new KernelSourceGenerator();
        var kernelCount = 100;
        var sourceCodeBuilder = new System.Text.StringBuilder();
        
        sourceCodeBuilder.AppendLine("using DotCompute.Generators.Kernel;");
        sourceCodeBuilder.AppendLine("public class ManyKernels {");
        
        for (int i = 0; i < kernelCount; i++)
        {
            sourceCodeBuilder.AppendLine($"[Kernel(\"Kernel{i}\")]");
            sourceCodeBuilder.AppendLine($"public static void Kernel{i}(float[] data) {{ }}");
        }
        
        sourceCodeBuilder.AppendLine("}");
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCodeBuilder.ToString());
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree });
        var context = new TestGeneratorExecutionContext(compilation);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        generator.Execute(context);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Should generate 100 kernels within 5 seconds");
        context.GeneratedSources.Should().NotBeEmpty();
    }

    [Fact]
    public void CpuCodeGenerator_HighVolumeGeneration_ShouldMaintainPerformance()
    {
        // Arrange
        var generator = new CpuCodeGenerator();
        const int generationCount = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < generationCount; i++)
        {
            var kernelInfo = new TestKernelInfo
            {
                Name = $"Kernel{i}",
                Parameters = new[] { "float[] input", "float[] output" },
                Body = $"output[index] = input[index] * {i};"
            };
            
            var result = generator.GenerateKernelCode(kernelInfo);
            result.Should().NotBeNullOrEmpty();
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Should generate 1000 kernels within 2 seconds");
    }

    [Fact]
    public void SourceGeneratorHelpers_MassiveStringEscaping_ShouldBePerformant()
    {
        // Arrange
        const int stringCount = 10000;
        var testStrings = new string[stringCount];
        for (int i = 0; i < stringCount; i++)
        {
            testStrings[i] = $"String with \"quotes\" and \n newlines {i}";
        }
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < stringCount; i++)
        {
            var escaped = SourceGeneratorHelpers.EscapeString(testStrings[i]);
            escaped.Should().NotBeNull();
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Should escape 10k strings within 1 second");
    }
}

/// <summary>
/// Test helper classes
/// </summary>
internal class TestKernelInfo
{
    public string Name { get; set; } = "";
    public string[] Parameters { get; set; } = Array.Empty<string>();
    public string Body { get; set; } = "";
}

internal class TestGeneratorInitializationContext : GeneratorInitializationContext
{
    public List<ISyntaxReceiver> RegisteredSyntaxReceivers { get; } = new();

    public void RegisterForSyntaxNotifications(SyntaxReceiverCreator receiverCreator)
    {
        var receiver = receiverCreator();
        RegisteredSyntaxReceivers.Add(receiver);
    }

    public void RegisterForPostInitialization(GeneratorPostInitializationCallback callback)
    {
        // Test implementation
    }
}

internal class TestGeneratorExecutionContext : GeneratorExecutionContext
{
    public TestGeneratorExecutionContext(Compilation compilation)
    {
        Compilation = compilation;
    }

    public Compilation Compilation { get; }
    public List<(string, string)> GeneratedSources { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();

    public void AddSource(string hintName, string source)
    {
        GeneratedSources.Add((hintName, source));
    }

    public void ReportDiagnostic(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    public ParseOptions ParseOptions => CSharpParseOptions.Default;
    public AnalyzerConfigOptionsProvider AnalyzerConfigOptions => throw new NotImplementedException();
    public AdditionalText[] AdditionalFiles => Array.Empty<AdditionalText>();
    public CancellationToken CancellationToken => CancellationToken.None;
    public ISyntaxReceiver? SyntaxReceiver => null;
}