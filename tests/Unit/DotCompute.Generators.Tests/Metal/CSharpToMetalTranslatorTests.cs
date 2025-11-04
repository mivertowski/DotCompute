// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotCompute.Generators.Kernel;
using DotCompute.Generators.Models.Kernel;
using FluentAssertions;

namespace DotCompute.Generators.Tests.Metal;

/// <summary>
/// Production-quality tests for CSharpToMetalTranslator with zero compilation errors.
/// Tests Metal Shading Language translation from C# kernel code.
/// </summary>
public sealed class CSharpToMetalTranslatorTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var (semanticModel, kernelInfo) = CreateTestKernel("test", "int idx = 0;");

        // Act
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Assert
        translator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullSemanticModel_ShouldThrow()
    {
        // Arrange
        var kernelInfo = CreateEmptyKernelInfo();

        // Act & Assert
        var act = () => new CSharpToMetalTranslator(null!, kernelInfo);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullKernelInfo_ShouldThrow()
    {
        // Arrange
        var (semanticModel, _) = CreateTestKernel("test", "int idx = 0;");

        // Act & Assert
        var act = () => new CSharpToMetalTranslator(semanticModel, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateCompleteKernel_SimpleKernel_ShouldIncludeMetalHeaders()
    {
        // Arrange
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", "int idx = 0;");
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("#include <metal_stdlib>");
        result.Should().Contain("#include <metal_atomic>");
        result.Should().Contain("using namespace metal;");
    }

    [Fact]
    public void GenerateCompleteKernel_SimpleKernel_ShouldIncludeKernelSignature()
    {
        // Arrange
        var (semanticModel, kernelInfo) = CreateTestKernel("VectorAdd", "int idx = 0;");
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().Contain("kernel void VectorAdd");
    }

    [Fact]
    public void GenerateCompleteKernel_WithReadOnlySpanParameter_ShouldTranslateToDeviceConst()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "input", Type = "ReadOnlySpan<float>", IsBuffer = true, IsReadOnly = true }
        };
        var (semanticModel, kernelInfo) = CreateTestKernelWithParameters("TestKernel", parameters, "int idx = 0;");
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().Contain("device const float* input");
    }

    [Fact]
    public void GenerateCompleteKernel_WithSpanParameter_ShouldTranslateToDevicePointer()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "output", Type = "Span<float>", IsBuffer = true, IsReadOnly = false }
        };
        var (semanticModel, kernelInfo) = CreateTestKernelWithParameters("TestKernel", parameters, "int idx = 0;");
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().Contain("device float* output");
    }

    [Fact]
    public void GenerateCompleteKernel_With1DKernel_ShouldUseScalarThreadId()
    {
        // Arrange
        var code = "int idx = Kernel.ThreadId.X;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().Contain("uint gid [[thread_position_in_grid]]");
    }

    [Fact]
    public void GenerateCompleteKernel_With2DKernel_ShouldUseUint2ThreadId()
    {
        // Arrange
        var code = "int x = Kernel.ThreadId.X; int y = Kernel.ThreadId.Y;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().Contain("uint2 gid [[thread_position_in_grid]]");
    }

    [Fact]
    public void GenerateCompleteKernel_With3DKernel_ShouldUseUint3ThreadId()
    {
        // Arrange
        var code = "int x = Kernel.ThreadId.X; int y = Kernel.ThreadId.Y; int z = Kernel.ThreadId.Z;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.GenerateCompleteKernel();

        // Assert
        result.Should().Contain("uint3 gid [[thread_position_in_grid]]");
    }

    [Fact]
    public void TranslateMethodBody_WithThreadIdX_ShouldTranslateToGid()
    {
        // Arrange
        var code = "int idx = Kernel.ThreadId.X;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("gid");
    }

    [Fact]
    public void TranslateMethodBody_WithBinaryExpression_ShouldTranslateOperators()
    {
        // Arrange
        var code = "int result = 5 + 3;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("int result");
        result.Should().Contain("=");
        result.Should().Contain("5 + 3");
    }

    [Fact]
    public void TranslateMethodBody_WithIfStatement_ShouldTranslateCorrectly()
    {
        // Arrange
        var code = "if (Kernel.ThreadId.X < 10) { int x = 0; }";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("if");
        result.Should().Contain("gid");
        result.Should().Contain("<");
        result.Should().Contain("10");
    }

    [Fact]
    public void TranslateMethodBody_WithForLoop_ShouldTranslateCorrectly()
    {
        // Arrange
        var code = "for (int i = 0; i < 10; i++) { int x = i; }";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("for");
        result.Should().Contain("int i");
        result.Should().Contain("i < 10");
        result.Should().Contain("i++");
    }

    [Fact]
    public void TranslateMethodBody_WithWhileLoop_ShouldTranslateCorrectly()
    {
        // Arrange
        var code = "int i = 0; while (i < 10) { i++; }";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("while");
        result.Should().Contain("i < 10");
    }

    [Fact]
    public void TranslateMethodBody_WithMathSin_ShouldTranslateToMetalSin()
    {
        // Arrange
        var code = "float x = MathF.Sin(1.0f);";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("sin");
        result.Should().Contain("1.0");
    }

    [Fact]
    public void TranslateMethodBody_WithMathCos_ShouldTranslateToMetalCos()
    {
        // Arrange
        var code = "float x = MathF.Cos(2.0f);";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("cos");
        result.Should().Contain("2.0");
    }

    [Fact]
    public void TranslateMethodBody_WithMathSqrt_ShouldTranslateToMetalSqrt()
    {
        // Arrange
        var code = "float x = MathF.Sqrt(4.0f);";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("sqrt");
    }

    [Fact]
    public void TranslateMethodBody_WithArrayAccess_ShouldTranslateCorrectly()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "data", Type = "Span<float>", IsBuffer = true, IsReadOnly = false }
        };
        var code = "data[0] = 1.0f;";
        var (semanticModel, kernelInfo) = CreateTestKernelWithParameters("TestKernel", parameters, code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("data[0]");
        result.Should().Contain("1.0");
    }

    [Fact]
    public void TranslateMethodBody_WithNullMethodDeclaration_ShouldGenerateDefaultBody()
    {
        // Arrange
        var kernelInfo = CreateEmptyKernelInfo();
        var (semanticModel, _) = CreateTestKernel("TestKernel", "int idx = 0;");
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void TranslateMethodBody_WithCastExpression_ShouldTranslateTypeCorrectly()
    {
        // Arrange
        var code = "int x = (int)5.5f;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("(int)");
        result.Should().Contain("5.5");
    }

    [Fact]
    public void TranslateMethodBody_WithReturnStatement_ShouldTranslateCorrectly()
    {
        // Arrange
        var code = "return;";
        var (semanticModel, kernelInfo) = CreateTestKernel("TestKernel", code);
        var translator = new CSharpToMetalTranslator(semanticModel, kernelInfo);

        // Act
        var result = translator.TranslateMethodBody();

        // Assert
        result.Should().Contain("return");
    }

    // Helper methods

    private static (SemanticModel, KernelMethodInfo) CreateTestKernel(string kernelName, string bodyCode)
    {
        return CreateTestKernelWithParameters(kernelName, new List<ParameterInfo>(), bodyCode);
    }

    private static (SemanticModel, KernelMethodInfo) CreateTestKernelWithParameters(
        string kernelName,
        List<ParameterInfo> parameters,
        string bodyCode)
    {
        var code = $@"
using System;

public class TestClass
{{
    [Kernel]
    public static void {kernelName}()
    {{
        {bodyCode}
    }}
}}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute {{ }}
public static class Kernel {{ public static ThreadId ThreadId => new(); }}
public struct ThreadId {{ public int X => 0; public int Y => 0; public int Z => 0; }}
";

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
            });

        var semanticModel = compilation.GetSemanticModel(tree);
        var methodDeclaration = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == kernelName);

        var kernelInfo = new KernelMethodInfo
        {
            Name = kernelName,
            MethodDeclaration = methodDeclaration
        };

        // Add parameters to the collection
        foreach (var param in parameters)
        {
            kernelInfo.Parameters.Add(param);
        }

        return (semanticModel, kernelInfo);
    }

    private static KernelMethodInfo CreateEmptyKernelInfo()
    {
        return new KernelMethodInfo
        {
            Name = "EmptyKernel",
            MethodDeclaration = null
        };
    }
}
