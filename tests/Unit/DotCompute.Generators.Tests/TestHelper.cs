// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Helper class for testing source generators.
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Runs a source generator against the provided source code.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator(ISourceGenerator generator, string source)
    {
        return RunGenerator(generator, new[] { source });
    }

    /// <summary>
    /// Runs a source generator against multiple source files.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator(ISourceGenerator generator, string[] sources)
    {
        var syntaxTrees = sources.Select(source =>
            CSharpSyntaxTree.ParseText(source)).ToArray();

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(generator);
        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>
    /// Runs an incremental source generator against the provided source code.
    /// </summary>
    public static GeneratorDriverRunResult RunIncrementalGenerator(IIncrementalGenerator generator, string source)
    {
        return RunIncrementalGenerator(generator, new[] { source });
    }

    /// <summary>
    /// Runs an incremental source generator against multiple source files.
    /// </summary>
    public static GeneratorDriverRunResult RunIncrementalGenerator(IIncrementalGenerator generator, string[] sources)
    {
        var syntaxTrees = sources.Select(source =>
            CSharpSyntaxTree.ParseText(source)).ToArray();

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(generator);
        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>
    /// Creates a compilation with the provided source code.
    /// </summary>
    public static Compilation CreateCompilation(string source)
    {
        return CreateCompilation(new[] { source });
    }

    /// <summary>
    /// Creates a compilation with multiple source files.
    /// </summary>
    public static Compilation CreateCompilation(string[] sources)
    {
        var syntaxTrees = sources.Select(source =>
            CSharpSyntaxTree.ParseText(source)).ToArray();

        var references = GetMetadataReferences();
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a compilation with unsafe code enabled.
    /// </summary>
    public static Compilation CreateUnsafeCompilation(string source)
    {
        return CreateUnsafeCompilation(new[] { source });
    }

    /// <summary>
    /// Creates a compilation with unsafe code enabled for multiple sources.
    /// </summary>
    public static Compilation CreateUnsafeCompilation(string[] sources)
    {
        var syntaxTrees = sources.Select(source =>
            CSharpSyntaxTree.ParseText(source)).ToArray();

        var references = GetMetadataReferences();
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
    }

    /// <summary>
    /// Gets the standard metadata references for testing.
    /// </summary>
    public static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Add .NET runtime references
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Task).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Span<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Memory<>).Assembly.Location));

        // Add System.Runtime references
        var systemRuntimePath = Path.Combine(runtimePath, "System.Runtime.dll");
        if(File.Exists(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        // Add System.Runtime.CompilerServices.Unsafe
        var unsafePath = Path.Combine(runtimePath, "System.Runtime.CompilerServices.Unsafe.dll");
        if(File.Exists(unsafePath))
        {
            references.Add(MetadataReference.CreateFromFile(unsafePath));
        }

        // Add System.Numerics.Vectors
        var vectorsPath = Path.Combine(runtimePath, "System.Numerics.Vectors.dll");
        if(File.Exists(vectorsPath))
        {
            references.Add(MetadataReference.CreateFromFile(vectorsPath));
        }

        // Add System.Runtime.Intrinsics
        var intrinsicsPath = Path.Combine(runtimePath, "System.Runtime.Intrinsics.dll");
        if(File.Exists(intrinsicsPath))
        {
            references.Add(MetadataReference.CreateFromFile(intrinsicsPath));
        }

        // Try to add the generators assembly(for KernelAttribute)
        try
        {
            var generatorsAssembly = Assembly.GetAssembly(typeof(DotCompute.Generators.Kernel.KernelAttribute));
            if(generatorsAssembly != null)
            {
                references.Add(MetadataReference.CreateFromFile(generatorsAssembly.Location));
            }
        }
        catch
        {
            // Ignore if assembly not found in test context
        }

        return references.ToImmutableArray();
    }

    /// <summary>
    /// Extracts the generated source code for a specific file.
    /// </summary>
    public static string? GetGeneratedSource(GeneratorDriverRunResult result, string fileName)
    {
        return result.GeneratedSources.FirstOrDefault(s => s.HintName == fileName)?.SourceText.ToString();
    }

    /// <summary>
    /// Verifies that a generator produces the expected number of source files.
    /// </summary>
    public static void VerifyGeneratedSourceCount(GeneratorDriverRunResult result, int expectedCount)
    {
        if(result.GeneratedSources.Length != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} generated sources, but got {result.GeneratedSources.Length}");
        }
    }

    /// <summary>
    /// Verifies that a generator produces no diagnostics.
    /// </summary>
    public static void VerifyNoDiagnostics(GeneratorDriverRunResult result)
    {
        if(result.Diagnostics.Any())
        {
            var diagnosticMessages = string.Join("\n", result.Diagnostics.Select(d => d.ToString();
            throw new InvalidOperationException($"Expected no diagnostics, but got:\n{diagnosticMessages}");
        }
    }

    /// <summary>
    /// Verifies that a generator produces specific diagnostics.
    /// </summary>
    public static void VerifyDiagnostics(GeneratorDriverRunResult result, params DiagnosticDescriptor[] expectedDescriptors)
    {
        var expectedIds = expectedDescriptors.Select(d => d.Id).ToHashSet();
        var actualIds = result.Diagnostics.Select(d => d.Id).ToHashSet();

        if(!expectedIds.SetEquals(actualIds))
        {
            var missing = expectedIds.Except(actualIds);
            var unexpected = actualIds.Except(expectedIds);
            var message = $"Diagnostic mismatch:\nMissing: [{string.Join(", ", missing)}]\nUnexpected: [{string.Join(", ", unexpected)}]";
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Creates a simple test kernel source with specified parameters.
    /// </summary>
    public static string CreateKernelSource(string className, string methodName, string parameters, string body)
    {
        return $@"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class {className}
{{
    [Kernel]
    public static void {methodName}({parameters})
    {{
        {body}
    }}
}}";
    }

    /// <summary>
    /// Creates a kernel source with specific attributes.
    /// </summary>
    public static string CreateKernelWithAttributes(string className, string methodName, 
        string attributes, string parameters, string body)
    {
        return $@"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class {className}
{{
    [Kernel({attributes})]
    public static void {methodName}({parameters})
    {{
        {body}
    }}
}}";
    }

    /// <summary>
    /// Creates an unsafe kernel source.
    /// </summary>
    public static string CreateUnsafeKernelSource(string className, string methodName, string parameters, string body)
    {
        return $@"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public unsafe class {className}
{{
    [Kernel]
    public static void {methodName}({parameters})
    {{
        {body}
    }}
}}";
    }

    /// <summary>
    /// Common kernel bodies for testing.
    /// </summary>
    public static class KernelBodies
    {
        public const string SimpleAdd = @"
            for(int i = 0; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }";

        public const string SimpleMultiply = @"
            for(int i = 0; i < length; i++)
            {
                result[i] = a[i] * b[i];
            }";

        public const string ScalarOperation = @"
            for(int i = 0; i < length; i++)
            {
                result[i] = input[i] * scale;
            }";

        public const string ComplexOperation = @"
            for(int i = 0; i < length; i++)
            {
                var value = input[i];
                if(value > 0)
                {
                    result[i] =(float)Math.Sqrt(value * 2.0f + 1.0f);
                }
                else
                {
                    result[i] = Math.Abs(value);
                }
            }";

        public const string MemoryCopy = @"
            for(int i = 0; i < length; i++)
            {
                output[i] = input[i];
            }";

        public const string NestedLoop = @"
            for(int i = 0; i < height; i++)
            {
                for(int j = 0; j < width; j++)
                {
                    result[i * width + j] = input[i * width + j] * 2.0f;
                }
            }";
    }

    /// <summary>
    /// Common parameter lists for testing.
    /// </summary>
    public static class Parameters
    {
        public const string TwoArrays = "float[] a, float[] b, float[] result, int length";
        public const string SingleArray = "float[] input, float[] output, int length";
        public const string ScalarArray = "float[] input, float[] output, float scale, int length";
        public const string MatrixParams = "float[] input, float[] result, int width, int height";
        public const string PointerParams = "float* input, float* output, int length";
        public const string SpanParams = "Span<float> input, Span<float> output";
    }
}
}
