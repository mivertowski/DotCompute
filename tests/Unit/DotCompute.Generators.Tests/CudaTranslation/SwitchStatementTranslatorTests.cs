// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.CudaTranslation;

/// <summary>
/// Tests for switch statement translation in CSharpToCudaTranslator.
/// Validates that C# switch statements are correctly translated to CUDA C switch syntax.
/// </summary>
public sealed class SwitchStatementTranslatorTests
{
    [Fact(DisplayName = "Switch: Simple integer switch with 3 cases translates correctly")]
    public void TranslateSwitch_SimpleIntegerSwitch_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ProcessMessage(int messageType)
    {
        int result = 0;
        switch (messageType)
        {
            case 1:
                result = 10;
                break;
            case 2:
                result = 20;
                break;
            case 3:
                result = 30;
                break;
        }
        return result;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ProcessMessage");

        // Assert
        Assert.Contains("switch (messageType) {", cudaCode);
        Assert.Contains("case 1:", cudaCode);
        Assert.Contains("result = 10;", cudaCode);
        Assert.Contains("break;", cudaCode);
        Assert.Contains("case 2:", cudaCode);
        Assert.Contains("result = 20;", cudaCode);
        Assert.Contains("case 3:", cudaCode);
        Assert.Contains("result = 30;", cudaCode);
        Assert.Contains("return result;", cudaCode);
    }

    [Fact(DisplayName = "Switch: Switch with default case translates correctly")]
    public void TranslateSwitch_WithDefaultCase_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ProcessMessage(int messageType)
    {
        int result = 0;
        switch (messageType)
        {
            case 1:
                result = 10;
                break;
            case 2:
                result = 20;
                break;
            default:
                result = -1;
                break;
        }
        return result;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ProcessMessage");

        // Assert
        Assert.Contains("switch (messageType) {", cudaCode);
        Assert.Contains("case 1:", cudaCode);
        Assert.Contains("case 2:", cudaCode);
        Assert.Contains("default:", cudaCode);
        Assert.Contains("result = -1;", cudaCode);
    }

    [Fact(DisplayName = "Switch: Switch with fall-through (no break) preserves semantics")]
    public void TranslateSwitch_FallThrough_PreservesCudaSemantics()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ProcessFlags(int flags)
    {
        int count = 0;
        switch (flags)
        {
            case 1:
                count++;
                break;
            case 2:
                count++;
                // Fall through to case 3
            case 3:
                count += 2;
                break;
        }
        return count;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ProcessFlags");

        // Assert
        Assert.Contains("switch (flags) {", cudaCode);
        Assert.Contains("case 1:", cudaCode);
        Assert.Contains("count++;", cudaCode);
        Assert.Contains("break;", cudaCode);
        Assert.Contains("case 2:", cudaCode);
        Assert.Contains("case 3:", cudaCode);
        Assert.Contains("count += 2;", cudaCode);
    }

    [Fact(DisplayName = "Switch: Nested switch statements translate correctly")]
    public void TranslateSwitch_NestedSwitch_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ProcessNested(int outer, int inner)
    {
        int result = 0;
        switch (outer)
        {
            case 1:
                switch (inner)
                {
                    case 10:
                        result = 100;
                        break;
                    case 20:
                        result = 200;
                        break;
                }
                break;
            case 2:
                result = 42;
                break;
        }
        return result;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ProcessNested");

        // Assert
        Assert.Contains("switch (outer) {", cudaCode);
        Assert.Contains("case 1:", cudaCode);
        Assert.Contains("switch (inner) {", cudaCode);
        Assert.Contains("case 10:", cudaCode);
        Assert.Contains("result = 100;", cudaCode);
        Assert.Contains("case 20:", cudaCode);
        Assert.Contains("result = 200;", cudaCode);
        Assert.Contains("case 2:", cudaCode);
        Assert.Contains("result = 42;", cudaCode);
    }

    [Fact(DisplayName = "Switch: Switch with multiple statements per case")]
    public void TranslateSwitch_MultipleStatementsPerCase_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ProcessComplex(int mode)
    {
        int a = 0;
        int b = 0;
        switch (mode)
        {
            case 1:
                a = 10;
                b = 20;
                a += b;
                break;
            case 2:
                a = 5;
                b = a * 2;
                break;
        }
        return a + b;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ProcessComplex");

        // Assert
        Assert.Contains("switch (mode) {", cudaCode);
        Assert.Contains("case 1:", cudaCode);
        Assert.Contains("a = 10;", cudaCode);
        Assert.Contains("b = 20;", cudaCode);
        Assert.Contains("a += b;", cudaCode);
        Assert.Contains("case 2:", cudaCode);
        Assert.Contains("a = 5;", cudaCode);
        Assert.Contains("b = a * 2;", cudaCode);
    }

    // Helper method to translate a C# method to CUDA
    private static string TranslateMethod(string code, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
        Assert.NotNull(methodSymbol);

        var kernelInfo = new KernelMethodInfo
        {
            Name = methodName,
            MethodDeclaration = method,
            ReturnType = methodSymbol.ReturnType.ToDisplayString(),
            ContainingType = methodSymbol.ContainingType.ToDisplayString(),
            Namespace = methodSymbol.ContainingNamespace.ToDisplayString()
        };

        var translator = new CSharpToCudaTranslator(semanticModel, kernelInfo);
        return translator.TranslateMethodBody();
    }
}
