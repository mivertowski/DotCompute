// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Kernel;
using DotCompute.Generators.Analyzers;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Integration tests for the complete Kernel attribute → Generator → Runtime workflow.
/// </summary>
public class IntegrationWorkflowTests
{
    [Fact]
    public void Integration_CompleteWorkflow_KernelToGeneration()
    {
        const string kernelCode = @"
using System;

namespace TestApp.Kernels
{
    public class MathKernels
    {
        [Kernel]
        public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int index = Kernel.ThreadId.X;
            if (index < result.Length)
                result[index] = a[index] + b[index];
        }
        
        [Kernel]
        public static void VectorMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int index = Kernel.ThreadId.X;
            if (index < result.Length)
                result[index] = a[index] * b[index];
        }
        
        [Kernel]
        public static void ScalarMultiply(ReadOnlySpan<float> input, Span<float> output, float scalar)
        {
            int index = Kernel.ThreadId.X;
            if (index < output.Length)
                output[index] = input[index] * scalar;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        // Step 1: Analyze kernels for issues
        var diagnostics = GetDiagnostics(kernelCode);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        
        Assert.Empty(errorDiagnostics); // Should have no errors
        
        // Step 2: Generate code from kernels
        var generatedSources = RunGenerator(kernelCode);
        Assert.NotEmpty(generatedSources);
        
        // Step 3: Verify generated registry
        var registry = generatedSources.FirstOrDefault(source => source.HintName == "KernelRegistry.g.cs");
        Assert.NotNull(registry);
        
        var registryContent = registry.SourceText.ToString();
        
        // Should contain all three kernels
        Assert.Contains("VectorAdd", registryContent);
        Assert.Contains("VectorMultiply", registryContent);
        Assert.Contains("ScalarMultiply", registryContent);
        Assert.Contains("TestApp.Kernels.MathKernels", registryContent);
        
        // Step 4: Verify generated kernel implementations
        var implementations = generatedSources.Where(source => source.HintName.EndsWith(".Implementation.g.cs"));
        Assert.Equal(3, implementations.Count()); // One per kernel
        
        foreach (var impl in implementations)
        {
            var implContent = impl.SourceText.ToString();
            
            // Should contain CPU and GPU implementations
            Assert.Contains("CpuImplementation", implContent);
            Assert.Contains("GpuImplementation", implContent);
            
            // Should have proper parameter handling
            Assert.Contains("ReadOnlySpan<float>", implContent);
            Assert.Contains("Span<float>", implContent);
        }
    }

    [Fact]
    public void Integration_RuntimeDiscovery_GeneratedKernelsFound()
    {
        const string kernelCode = @"
using System;

namespace MyApp.Compute
{
    public class ComputeKernels
    {
        [Kernel]
        public static void ProcessData(ReadOnlySpan<int> input, Span<int> output)
        {
            int index = Kernel.ThreadId.X;
            if (index < output.Length)
                output[index] = input[index] * 2 + 1;
        }
        
        [Kernel]  
        public static void InitializeArray(Span<float> array, float value)
        {
            int index = Kernel.ThreadId.X;
            if (index < array.Length)
                array[index] = value;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(kernelCode);
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        // Should generate runtime discovery method
        Assert.Contains("GetAvailableKernels", registryContent);
        Assert.Contains("KernelRegistration", registryContent);
        
        // Should include kernel metadata
        Assert.Contains("ProcessData", registryContent);
        Assert.Contains("InitializeArray", registryContent);
        Assert.Contains("MyApp.Compute.ComputeKernels", registryContent);
        
        // Should have parameter type information
        Assert.Contains("ReadOnlySpan<int>", registryContent);
        Assert.Contains("Span<int>", registryContent);
        Assert.Contains("Span<float>", registryContent);
        Assert.Contains("float", registryContent);
    }

    [Fact]
    public void Integration_ErrorHandling_BadKernelsPropagatedCorrectly()
    {
        const string badKernelCode = @"
using System;

public class BadKernels
{
    [Kernel]
    public int BadReturnType(Span<float> data) // DC003: Invalid return type
    {
        return 42;
    }
    
    [Kernel]
    public static void RecursiveKernel(Span<float> data) // DC004: Recursive call
    {
        RecursiveKernel(data);
    }
    
    [Kernel]
    public void NonStaticKernel(object badParam) // DC001: Not static, DC002: Bad parameter
    {
        // Bad kernel
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        // Step 1: Should detect multiple errors
        var diagnostics = GetDiagnostics(badKernelCode);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        
        Assert.NotEmpty(errorDiagnostics);
        Assert.Contains(errorDiagnostics, d => d.Id == "DC001"); // Non-static
        Assert.Contains(errorDiagnostics, d => d.Id == "DC002"); // Bad parameter
        Assert.Contains(errorDiagnostics, d => d.Id == "DC003"); // Bad return type
        
        // Step 2: Generator should handle bad kernels gracefully
        var generatedSources = RunGenerator(badKernelCode);
        
        // Should still generate registry but with warnings or exclusions
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Integration_MultipleFiles_CrossFileReferences()
    {
        const string coreCode = @"
using System;

namespace Shared
{
    public static class Constants
    {
        public const float PI = 3.14159f;
        public const int BLOCK_SIZE = 256;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        const string kernelCode = @"
using System;
using Shared;

namespace App.Kernels
{
    public class GeometryKernels
    {
        [Kernel]
        public static void CalculateCircleArea(ReadOnlySpan<float> radii, Span<float> areas)
        {
            int index = Kernel.ThreadId.X;
            if (index < areas.Length)
                areas[index] = Constants.PI * radii[index] * radii[index]; // Cross-file reference
        }
    }
}";

        // Combine both files for compilation
        var combinedCode = coreCode + "\n" + kernelCode;
        
        var diagnostics = GetDiagnostics(combinedCode);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        
        Assert.Empty(errorDiagnostics); // Should handle cross-file references
        
        var generatedSources = RunGenerator(combinedCode);
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        Assert.Contains("CalculateCircleArea", registryContent);
        Assert.Contains("App.Kernels.GeometryKernels", registryContent);
    }

    [Fact]
    public void Integration_GenericTypes_HandledCorrectly()
    {
        const string genericKernelCode = @"
using System;

namespace GenericTest
{
    public class GenericKernels<T> where T : unmanaged
    {
        // Generic kernel methods are typically not supported, 
        // but we test how the system handles them
        
        [Kernel]
        public static void ProcessGeneric(ReadOnlySpan<T> input, Span<T> output)
        {
            int index = Kernel.ThreadId.X;
            if (index < output.Length)
                output[index] = input[index]; // Simple copy
        }
    }
    
    public class ConcreteKernels
    {
        [Kernel]
        public static void ProcessFloat(ReadOnlySpan<float> input, Span<float> output)
        {
            int index = Kernel.ThreadId.X;
            if (index < output.Length)
                output[index] = input[index] * 2.0f;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(genericKernelCode);
        var generatedSources = RunGenerator(genericKernelCode);
        
        // Should handle the concrete kernel
        Assert.NotEmpty(generatedSources);
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        Assert.Contains("ProcessFloat", registryContent); // Concrete method should be included
        
        // Generic method handling depends on implementation
        // (might be excluded or cause warnings)
    }

    [Fact]
    public void Integration_RealWorldScenario_ImageProcessingKernels()
    {
        const string imageProcessingCode = @"
using System;

namespace ImageProcessing.GPU
{
    public class ImageKernels
    {
        [Kernel]
        public static void GrayscaleConvert(ReadOnlySpan<float> red, ReadOnlySpan<float> green, 
                                          ReadOnlySpan<float> blue, Span<float> grayscale)
        {
            int index = Kernel.ThreadId.X;
            if (index < grayscale.Length && index < red.Length && 
                index < green.Length && index < blue.Length)
            {
                grayscale[index] = 0.299f * red[index] + 0.587f * green[index] + 0.114f * blue[index];
            }
        }
        
        [Kernel]
        public static void GaussianBlur(ReadOnlySpan<float> input, Span<float> output, 
                                      int width, int height, float sigma)
        {
            int index = Kernel.ThreadId.X;
            if (index >= output.Length) return;
            
            int x = index % width;
            int y = index / width;
            
            if (x >= width || y >= height) return;
            
            float sum = 0.0f;
            float weightSum = 0.0f;
            int kernelSize = (int)(sigma * 3) * 2 + 1;
            int radius = kernelSize / 2;
            
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = x + dx;
                    int py = y + dy;
                    
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        int inputIndex = py * width + px;
                        if (inputIndex < input.Length)
                        {
                            float weight = (float)Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                            sum += input[inputIndex] * weight;
                            weightSum += weight;
                        }
                    }
                }
            }
            
            output[index] = sum / weightSum;
        }
        
        [Kernel]
        public static void EdgeDetection(ReadOnlySpan<float> input, Span<float> output, int width, int height)
        {
            int index = Kernel.ThreadId.X;
            if (index >= output.Length) return;
            
            int x = index % width;
            int y = index / width;
            
            if (x >= width - 1 || y >= height - 1 || x <= 0 || y <= 0)
            {
                output[index] = 0.0f;
                return;
            }
            
            // Sobel operator
            float gx = input[(y - 1) * width + (x - 1)] * -1 + input[(y - 1) * width + (x + 1)] * 1 +
                       input[y * width + (x - 1)] * -2 + input[y * width + (x + 1)] * 2 +
                       input[(y + 1) * width + (x - 1)] * -1 + input[(y + 1) * width + (x + 1)] * 1;
                       
            float gy = input[(y - 1) * width + (x - 1)] * -1 + input[(y - 1) * width + x] * -2 + input[(y - 1) * width + (x + 1)] * -1 +
                       input[(y + 1) * width + (x - 1)] * 1 + input[(y + 1) * width + x] * 2 + input[(y + 1) * width + (x + 1)] * 1;
            
            output[index] = (float)Math.Sqrt(gx * gx + gy * gy);
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        // Test complete workflow with realistic kernels
        var diagnostics = GetDiagnostics(imageProcessingCode);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        
        Assert.Empty(errorDiagnostics); // Should be well-formed
        
        var generatedSources = RunGenerator(imageProcessingCode);
        Assert.NotEmpty(generatedSources);
        
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        // Should contain all image processing kernels
        Assert.Contains("GrayscaleConvert", registryContent);
        Assert.Contains("GaussianBlur", registryContent);
        Assert.Contains("EdgeDetection", registryContent);
        
        // Should handle complex parameter lists
        Assert.Contains("int width", registryContent);
        Assert.Contains("int height", registryContent);
        Assert.Contains("float sigma", registryContent);
        
        // Verify individual kernel implementations are generated
        var implementations = generatedSources.Where(source => source.HintName.EndsWith(".Implementation.g.cs"));
        Assert.Equal(3, implementations.Count());
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
                MetadataReference.CreateFromFile(typeof(Math).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new KernelSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        
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
                MetadataReference.CreateFromFile(typeof(Math).Assembly.Location),
            }
        );

        var analyzer = new DotComputeKernelAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        
        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
    }
}