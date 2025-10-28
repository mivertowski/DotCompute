// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotCompute.Generators.Kernel;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.Integration;

/// <summary>
/// Advanced integration tests for end-to-end scenarios.
/// </summary>
public sealed class AdvancedIntegrationTests
{
    [Fact]
    public void EndToEnd_SimpleKernel_CompilesAndGenerates()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class IntegrationKernels
    {
        [Kernel]
        public static void SimpleKernel(Span<float> data)
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

        var (compilation, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void EndToEnd_MultipleFilesWithKernels_AllGenerated()
    {
        var file1 = @"
using System;
namespace App.Math
{
    public class MathKernels
    {
        [Kernel]
        public static void Add(Span<float> data) { }
    }
}";

        var file2 = @"
using System;
namespace App.Physics
{
    public class PhysicsKernels
    {
        [Kernel]
        public static void Calculate(Span<float> data) { }
    }
}";

        var (compilation, generatedSources) = CompileWithGenerator(file1, file2);

        Assert.NotEmpty(generatedSources);
        var registry = generatedSources.FirstOrDefault(s => s.HintName.Contains("KernelRegistry"));
        Assert.NotNull(registry);

        var content = registry.SourceText.ToString();
        Assert.Contains("Add", content);
        Assert.Contains("Calculate", content);
    }

    [Fact]
    public void EndToEnd_KernelWithDependencies_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public static class MathHelpers
    {
        public static float Square(float x) => x * x;
    }

    public class DependentKernels
    {
        [Kernel]
        public static void UseHelper(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length)
                data[idx] = MathHelpers.Square(data[idx]);
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_GenericHelperTypes_HandlesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public readonly struct Point2D
    {
        public readonly float X;
        public readonly float Y;

        public Point2D(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public class StructKernels
    {
        [Kernel]
        public static void ProcessPoints(Span<float> xCoords, Span<float> yCoords, Span<float> distances)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < distances.Length)
            {
                float x = xCoords[idx];
                float y = yCoords[idx];
                distances[idx] = MathF.Sqrt(x * x + y * y);
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_PartialClasses_GeneratesForAll()
    {
        const string code = @"
using System;
namespace TestApp
{
    public partial class PartialKernels
    {
        [Kernel]
        public static void Part1(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0f;
        }
    }

    public partial class PartialKernels
    {
        [Kernel]
        public static void Part2(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] += 1.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("Part1", content);
        Assert.Contains("Part2", content);
    }

    [Fact]
    public void EndToEnd_InternalClasses_StillGenerates()
    {
        const string code = @"
using System;
namespace TestApp
{
    internal class InternalKernels
    {
        [Kernel]
        public static void InternalKernel(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_StaticClasses_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public static class StaticKernels
    {
        [Kernel]
        public static void StaticKernel(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_SealedClasses_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public sealed class SealedKernels
    {
        [Kernel]
        public static void SealedKernel(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_ComplexNamespaces_GeneratesWithFullyQualifiedNames()
    {
        const string code = @"
using System;
namespace Company.Product.Module.SubModule
{
    public class DeepKernels
    {
        [Kernel]
        public static void DeepNamespaceKernel(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("Company.Product.Module.SubModule", content);
    }

    [Fact]
    public void EndToEnd_FileScopedNamespace_GeneratesCorrectly()
    {
        const string code = @"
using System;

namespace TestApp;

public class FileScopedKernels
{
    [Kernel]
    public static void FileScopedKernel(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] *= 2.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
class KernelAttribute : System.Attribute { }
static class Kernel { public static ThreadId ThreadId => new(); }
struct ThreadId { public int X => 0; }";

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_NullableEnabled_GeneratesCorrectly()
    {
        const string code = @"
#nullable enable
using System;
namespace TestApp
{
    public class NullableKernels
    {
        [Kernel]
        public static void NullableKernel(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_WithUsings_GeneratesCorrectly()
    {
        const string code = @"
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TestApp
{
    public class WithUsingsKernels
    {
        [Kernel]
        public static void WithUsings(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_RecordTypes_NotAffectedByGenerator()
    {
        const string code = @"
using System;
namespace TestApp
{
    public record DataRecord(float X, float Y);

    public class RecordKernels
    {
        [Kernel]
        public static void RecordKernel(Span<float> data)
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

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_WithPreprocessorDirectives_HandlesCorrectly()
    {
        const string code = @"
#define DEBUG_MODE
using System;
namespace TestApp
{
    public class PreprocessorKernels
    {
        [Kernel]
        public static void PreprocessorKernel(Span<float> data)
        {
#if DEBUG_MODE
            int idx = Kernel.ThreadId.X;
#else
            int idx = 0;
#endif
            if (idx < data.Length) data[idx] *= 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var (_, generatedSources) = CompileWithGenerator(code);

        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EndToEnd_WithGlobalUsings_GeneratesCorrectly()
    {
        var globalUsings = @"
global using System;
global using System.Runtime.InteropServices;
";

        var mainCode = @"
namespace TestApp
{
    public class GlobalKernels
    {
        [Kernel]
        public static void GlobalKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0f;
        }
    }
}
[AttributeUsage(AttributeTargets.Method)]
public class KernelAttribute : Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var (_, generatedSources) = CompileWithGenerator(globalUsings, mainCode);

        Assert.NotEmpty(generatedSources);
    }

    private static (Compilation, ImmutableArray<GeneratedSourceResult>) CompileWithGenerator(params string[] sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: trees,
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.InteropServices.MemoryMarshal).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.Unsafe).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new KernelSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results.Length > 0
            ? runResult.Results[0].GeneratedSources
            : ImmutableArray<GeneratedSourceResult>.Empty;

        return (outputCompilation, generatedSources);
    }
}
