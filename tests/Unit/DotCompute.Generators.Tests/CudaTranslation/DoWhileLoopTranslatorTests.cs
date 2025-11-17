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
/// Tests for do-while loop translation in CSharpToCudaTranslator.
/// Validates that C# do-while loops are correctly translated to CUDA C do-while syntax.
/// </summary>
public sealed class DoWhileLoopTranslatorTests
{
    [Fact(DisplayName = "DoWhile: Simple do-while with counter translates correctly")]
    public void TranslateDoWhile_SimpleCounter_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int CountDown(int start)
    {
        int count = start;
        do
        {
            count--;
        } while (count > 0);
        return count;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "CountDown");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("count--;", cudaCode);
        Assert.Contains("} while (count > 0);", cudaCode);
        Assert.Contains("return count;", cudaCode);
    }

    [Fact(DisplayName = "DoWhile: Do-while with complex condition")]
    public void TranslateDoWhile_ComplexCondition_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ProcessLoop(int a, int b)
    {
        int result = 0;
        do
        {
            result += a;
            a++;
        } while (a < b && result < 100);
        return result;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ProcessLoop");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("result += a;", cudaCode);
        Assert.Contains("a++;", cudaCode);
        Assert.Contains("} while (a < b && result < 100);", cudaCode);
    }

    [Fact(DisplayName = "DoWhile: Nested do-while loops translate correctly")]
    public void TranslateDoWhile_NestedLoops_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int NestedDoWhile(int n)
    {
        int total = 0;
        int i = 0;
        do
        {
            int j = 0;
            do
            {
                total += i * j;
                j++;
            } while (j < 3);
            i++;
        } while (i < n);
        return total;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "NestedDoWhile");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("total += i * j;", cudaCode);
        Assert.Contains("j++;", cudaCode);
        Assert.Contains("} while (j < 3);", cudaCode);
        Assert.Contains("i++;", cudaCode);
        Assert.Contains("} while (i < n);", cudaCode);
    }

    [Fact(DisplayName = "DoWhile: Do-while with break statement")]
    public void TranslateDoWhile_WithBreak_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int DoWhileWithBreak(int threshold)
    {
        int value = 1;
        do
        {
            value *= 2;
            if (value > threshold)
            {
                break;
            }
        } while (value < 1000);
        return value;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "DoWhileWithBreak");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("value *= 2;", cudaCode);
        Assert.Contains("if (value > threshold) {", cudaCode);
        Assert.Contains("break;", cudaCode);
        Assert.Contains("} while (value < 1000);", cudaCode);
    }

    [Fact(DisplayName = "DoWhile: Do-while with continue statement")]
    public void TranslateDoWhile_WithContinue_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int DoWhileWithContinue(int max)
    {
        int sum = 0;
        int i = 0;
        do
        {
            i++;
            if (i % 2 == 0)
            {
                continue;
            }
            sum += i;
        } while (i < max);
        return sum;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "DoWhileWithContinue");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("i++;", cudaCode);
        Assert.Contains("if (i % 2 == 0) {", cudaCode);
        Assert.Contains("continue;", cudaCode);
        Assert.Contains("sum += i;", cudaCode);
        Assert.Contains("} while (i < max);", cudaCode);
    }

    [Fact(DisplayName = "DoWhile: Do-while with single statement (no braces)")]
    public void TranslateDoWhile_SingleStatement_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int SimpleDoWhile(int n)
    {
        int result = n;
        do
            result--;
        while (result > 0);
        return result;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "SimpleDoWhile");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("result--;", cudaCode);
        Assert.Contains("} while (result > 0);", cudaCode);
    }

    [Fact(DisplayName = "DoWhile: Do-while with multiple statements")]
    public void TranslateDoWhile_MultipleStatements_GeneratesValidCuda()
    {
        // Arrange
        const string code = @"
using System;
public class TestClass
{
    public static int ComplexDoWhile(int initial)
    {
        int a = initial;
        int b = 1;
        int sum = 0;
        do
        {
            sum += a;
            a /= 2;
            b++;
        } while (a > 0 && b < 10);
        return sum;
    }
}";

        // Act
        var cudaCode = TranslateMethod(code, "ComplexDoWhile");

        // Assert
        Assert.Contains("do {", cudaCode);
        Assert.Contains("sum += a;", cudaCode);
        Assert.Contains("a /= 2;", cudaCode);
        Assert.Contains("b++;", cudaCode);
        Assert.Contains("} while (a > 0 && b < 10);", cudaCode);
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
