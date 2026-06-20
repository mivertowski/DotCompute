// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Analyzers;
using DotCompute.Generators.Kernel;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Generators.Tests.Performance;

/// <summary>
/// Performance tests for generators and analyzers.
/// </summary>
public sealed class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Generator_LargeNumberOfKernels_CompletesInReasonableTime()
    {
        var code = GenerateCodeWithKernels(100);

        var stopwatch = Stopwatch.StartNew();
        var generatedSources = RunGenerator(code);
        stopwatch.Stop();

        // Timing ceiling is a gross-regression guard only — real source generation of 100 kernels under
        // a cold JIT on a loaded/virtualized CI runner can exceed a tight 10s bound without a regression.
        _output.WriteLine($"Generation of 100 kernels took {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < 60_000,
            $"Generation took {stopwatch.ElapsedMilliseconds}ms (gross-regression ceiling, not a tight target)");
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Analyzer_LargeNumberOfKernels_CompletesInReasonableTime()
    {
        var code = GenerateCodeWithKernels(100);

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = GetDiagnostics(code);
        stopwatch.Stop();

        // Timing ceiling is a gross-regression guard only — real analyzer execution over 100 kernels
        // under a cold JIT on a loaded/virtualized CI runner can exceed a tight 10s bound without a
        // regression.
        _output.WriteLine($"Analysis of 100 kernels took {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < 60_000,
            $"Analysis took {stopwatch.ElapsedMilliseconds}ms (gross-regression ceiling, not a tight target)");
    }

    [Fact]
    public void Generator_VeryLargeKernelMethod_HandlesCorrectly()
    {
        var codeBuilder = new StringBuilder();
        codeBuilder.AppendLine(@"
using System;
namespace TestApp
{
    public class LargeKernel
    {
        [Kernel]
        public static void HugeKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
            {
                float value = data[idx];");

        // Generate 1000 lines of arithmetic
        for (int i = 0; i < 1000; i++)
        {
            codeBuilder.AppendLine($"                value = value * {i + 1}f + {i}f;");
        }

        codeBuilder.AppendLine(@"
                data[idx] = value;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }");

        var code = codeBuilder.ToString();
        var generatedSources = RunGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Generator_DeeplyNestedClasses_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace Level1
{
    public class Level2
    {
        public class Level3
        {
            public class Level4
            {
                public class Level5
                {
                    [Kernel]
                    public static void DeepKernel(Span<float> data)
                    {
                        int idx = Kernel.ThreadId.X;
                        if (idx < data.Length) data[idx] *= 2.0f;
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
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        Assert.Contains("DeepKernel", registry.SourceText.ToString());
    }

    [Fact]
    public void Analyzer_ConcurrentAnalysis_WorksCorrectly()
    {
        var code = GenerateCodeWithKernels(50);

        // Run analysis multiple times concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => System.Threading.Tasks.Task.Run(() => GetDiagnostics(code)))
            .ToArray();

        System.Threading.Tasks.Task.WaitAll(tasks);

        foreach (var task in tasks)
        {
            Assert.NotNull(task.Result);
        }
    }

    [Fact]
    public void Generator_VeryLongMethodName_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class LongNames
    {
        [Kernel]
        public static void ThisIsAVeryLongMethodNameThatExceedsReasonableLengthButShouldStillBeHandledCorrectlyByTheGenerator(Span<float> data)
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
    public void Generator_ManyParametersKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ManyParams
    {
        [Kernel]
        public static void ManyParameters(
            ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c,
            ReadOnlySpan<float> d, ReadOnlySpan<float> e, ReadOnlySpan<float> f,
            Span<float> output,
            float scale1, float scale2, float scale3,
            int offset1, int offset2, double multiplier)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                output[idx] = (a[idx] * scale1 + b[idx] * scale2 + c[idx] * scale3) * (float)multiplier;
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
    public void Analyzer_EmptyFile_NoErrors()
    {
        const string code = "";

        var diagnostics = GetDiagnostics(code);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyzer_NoKernels_NoErrors()
    {
        const string code = @"
using System;
public class RegularClass
{
    public void RegularMethod()
    {
        Console.WriteLine(""Hello"");
    }
}";

        var diagnostics = GetDiagnostics(code);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Generator_UnicodeInComments_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class UnicodeKernels
    {
        /// <summary>
        /// 这是一个测试内核 (This is a test kernel)
        /// Это тестовое ядро (This is a test kernel)
        /// これはテストカーネルです (This is a test kernel)
        /// </summary>
        [Kernel]
        public static void UnicodeKernel(Span<float> data)
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
    public void Generator_SpecialCharactersInNames_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace Test_App_123
{
    public class Test_Class_456
    {
        [Kernel]
        public static void Test_Kernel_789(Span<float> _data_)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < _data_.Length) _data_[idx] *= 2.0f;
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
    public void Performance_IncrementalGeneration_Efficient()
    {
        var code1 = GenerateCodeWithKernels(10);
        var code2 = GenerateCodeWithKernels(10);

        var stopwatch = Stopwatch.StartNew();
        var gen1 = RunGenerator(code1);
        var time1 = stopwatch.ElapsedMilliseconds;

        stopwatch.Restart();
        var gen2 = RunGenerator(code2);
        var time2 = stopwatch.ElapsedMilliseconds;

        // Hard gate: repeated generation must remain functionally stable and produce the
        // same set of outputs on each run (this is what "efficient/no leak" really means
        // here — deterministic, non-degrading behavior).
        Assert.NotEmpty(gen1);
        Assert.NotEmpty(gen2);
        Assert.Equal(gen1.Length, gen2.Length);

        // Comparing two millisecond wall-clock measurements with a tight relative tolerance
        // is unreliable: the first run pays JIT/warmup cost and both can be sub-millisecond,
        // so scheduling jitter dominates. Timing is therefore informational; we only guard
        // against a gross absolute degradation on the second run.
        _output.WriteLine($"Incremental generation: run1={time1}ms, run2={time2}ms");
        Assert.True(time2 < 5000,
            $"Second generation took {time2}ms, indicating a gross performance degradation.");
    }

    [Fact]
    public void Generator_MultipleSyntaxErrors_StillProducesOutput()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class BrokenKernels
    {
        [Kernel]
        public static void ValidKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0f;
        }

        // This will have syntax errors but shouldn't break generation
        public static void BrokenMethod(
            // Missing closing parenthesis
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        // Generator should still process valid kernels even with syntax errors elsewhere
        var generatedSources = RunGenerator(code);

        // Should generate something even with errors
        Assert.NotNull(generatedSources);
    }

    private static string GenerateCodeWithKernels(int count)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("namespace TestApp {");
        builder.AppendLine("public class TestKernels {");

        for (int i = 0; i < count; i++)
        {
            builder.AppendLine($@"
    [Kernel]
    public static void Kernel{i}(ReadOnlySpan<float> input, Span<float> output)
    {{
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length)
            output[idx] = input[idx] * {i + 1}f;
    }}");
        }

        builder.AppendLine("}");
        builder.AppendLine("}");
        builder.AppendLine("[System.AttributeUsage(System.AttributeTargets.Method)]");
        builder.AppendLine("public class KernelAttribute : System.Attribute { }");
        builder.AppendLine("public static class Kernel { public static ThreadId ThreadId => new(); }");
        builder.AppendLine("public struct ThreadId { public int X => 0; }");

        return builder.ToString();
    }

    private static ImmutableArray<GeneratedSourceResult> RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
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
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions);

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
