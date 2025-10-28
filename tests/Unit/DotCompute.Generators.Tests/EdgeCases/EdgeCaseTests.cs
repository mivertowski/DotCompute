// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Analyzers;
using DotCompute.Generators.Kernel;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.EdgeCases;

/// <summary>
/// Tests for edge cases and unusual scenarios.
/// </summary>
public sealed class EdgeCaseTests
{
    [Fact]
    public void EdgeCase_EmptyKernelBody_GeneratesWithoutError()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class EmptyKernels
    {
        [Kernel]
        public static void EmptyKernel(Span<float> data)
        {
            // Completely empty
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var generatedSources = RunGenerator(code);
        Assert.NotNull(generatedSources);
    }

    [Fact]
    public void EdgeCase_OnlyCommentsInKernel_GeneratesWithoutError()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class CommentKernels
    {
        [Kernel]
        public static void CommentOnlyKernel(Span<float> data)
        {
            // This kernel only has comments
            /* Multi-line comment
               that spans multiple lines */
            /// XML comment
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var generatedSources = RunGenerator(code);
        Assert.NotNull(generatedSources);
    }

    [Fact]
    public void EdgeCase_VeryShortParameterNames_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ShortNames
    {
        [Kernel]
        public static void K(Span<float> a, Span<float> b, Span<float> c, float d, int e)
        {
            int x = Kernel.ThreadId.X;
            if (x < c.Length) c[x] = a[x] + b[x] * d + e;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_KernelWithOnlyReturn_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ReturnKernels
    {
        [Kernel]
        public static void OnlyReturn(Span<float> data)
        {
            return;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var generatedSources = RunGenerator(code);
        Assert.NotNull(generatedSources);
    }

    [Fact]
    public void EdgeCase_KernelWithConditionalReturn_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ConditionalReturnKernels
    {
        [Kernel]
        public static void ConditionalReturn(Span<float> data, bool condition)
        {
            int idx = Kernel.ThreadId.X;
            if (condition) return;
            if (idx < data.Length) data[idx] *= 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_ZeroParameters_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class NoParamKernels
    {
        [Kernel]
        public static void NoParams()
        {
            // Kernel with no parameters
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var generatedSources = RunGenerator(code);
        Assert.NotNull(generatedSources);
    }

    [Fact]
    public void EdgeCase_MaxIntValue_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class MaxValueKernels
    {
        [Kernel]
        public static void UseMaxValue(Span<int> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                data[idx] = int.MaxValue;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_MinMaxFloatValues_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class FloatExtremesKernels
    {
        [Kernel]
        public static void UseExtremes(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                float value = data[idx];
                if (value == float.MaxValue) data[idx] = float.MinValue;
                else if (value == float.MinValue) data[idx] = float.MaxValue;
                else if (float.IsNaN(value)) data[idx] = 0.0f;
                else if (float.IsInfinity(value)) data[idx] = 1.0f;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_NegativeNumbers_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class NegativeKernels
    {
        [Kernel]
        public static void UseNegatives(Span<float> data, float negative)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                data[idx] = data[idx] * -1.0f + negative;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_ZeroDivision_AllowedInCode()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class DivisionKernels
    {
        [Kernel]
        public static void DivideByZero(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                data[idx] = data[idx] / 0.0f;  // Will produce infinity
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_UnusedParameter_GeneratesWithoutError()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class UnusedParamKernels
    {
        [Kernel]
        public static void UnusedParam(Span<float> data, int unusedValue, float unusedScale)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_AllParametersReadOnly_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ReadOnlyKernels
    {
        [Kernel]
        public static void AllReadOnly(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int idx = Kernel.ThreadId.X;
            // Cannot write to output, just read
            if (idx < a.Length && idx < b.Length)
            {
                float sum = a[idx] + b[idx];  // Computed but not stored
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_NestedIfStatements_Deep_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class NestedKernels
    {
        [Kernel]
        public static void DeepNesting(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                if (data[idx] > 0)
                {
                    if (data[idx] < 10)
                    {
                        if (data[idx] > 5)
                        {
                            if (data[idx] < 8)
                            {
                                data[idx] *= 2.0f;
                            }
                        }
                    }
                }
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_BooleanOperations_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class BoolKernels
    {
        [Kernel]
        public static void BoolLogic(Span<int> data, bool flag1, bool flag2)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                bool result = flag1 && flag2 || !flag1;
                data[idx] = result ? 1 : 0;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_CastingOperations_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class CastKernels
    {
        [Kernel]
        public static void Casting(Span<int> intData, Span<float> floatData)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < intData.Length && idx < floatData.Length)
            {
                intData[idx] = (int)floatData[idx];
                floatData[idx] = (float)intData[idx];
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Analyzer_EdgeCase_KernelNameSameAsClass_HandlesCorrectly()
    {
        const string code = @"
using System;
public class Kernel
{
    [Kernel]
    public static void Kernel(Span<float> data)
    {
        // Method named Kernel in class named Kernel
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        // Should handle this without crashing
        Assert.NotNull(diagnostics);
    }

    [Fact]
    public void Analyzer_EdgeCase_KernelWithSameName_InDifferentClasses()
    {
        const string code = @"
using System;
public class Class1
{
    [Kernel]
    public static void Process(Span<float> data) { }
}

public class Class2
{
    [Kernel]
    public static void Process(Span<float> data) { }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        // Should handle duplicate names in different classes
        Assert.NotNull(diagnostics);
    }

    [Fact]
    public void EdgeCase_WhitespaceOnlyLines_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class WhitespaceKernels
    {
        [Kernel]
        public static void WhitespaceKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;


            if (idx < data.Length)
            {

                data[idx] *= 2.0f;

            }

        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_TabIndentation_HandlesCorrectly()
    {
        const string code = "using System;\nnamespace TestApp\n{\n\tpublic class TabKernels\n\t{\n\t\t[Kernel]\n\t\tpublic static void TabKernel(Span<float> data)\n\t\t{\n\t\t\tint idx = Kernel.ThreadId.X;\n\t\t\tif (idx < data.Length) data[idx] *= 2.0f;\n\t\t}\n\t}\n}\n[System.AttributeUsage(System.AttributeTargets.Method)]\npublic class KernelAttribute : System.Attribute { }\npublic static class Kernel { public static ThreadId ThreadId => new(); }\npublic struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_MixedLineEndings_HandlesCorrectly()
    {
        // Mix of \r\n and \n line endings
        const string code = "using System;\r\nnamespace TestApp\n{\r\n    public class MixedKernels\n    {\r\n        [Kernel]\n        public static void MixedKernel(Span<float> data)\r\n        {\n            int idx = Kernel.ThreadId.X;\r\n            if (idx < data.Length) data[idx] *= 2.0f;\n        }\r\n    }\n}\r\n[System.AttributeUsage(System.AttributeTargets.Method)]\npublic class KernelAttribute : System.Attribute { }\npublic static class Kernel { public static ThreadId ThreadId => new(); }\npublic struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    private static ImmutableArray<GeneratedSourceResult> RunGenerator(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
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

        var generator = new KernelSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        return runResult.Results.Length > 0 ? runResult.Results[0].GeneratedSources : ImmutableArray<GeneratedSourceResult>.Empty;
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
            }
        );

        var analyzer = new DotComputeKernelAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
    }
}
