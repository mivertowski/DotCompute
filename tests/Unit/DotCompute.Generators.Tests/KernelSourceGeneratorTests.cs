// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using DotCompute.Generators.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

public class KernelSourceGeneratorTests
{
    private readonly KernelSourceGenerator _generator = new();

    [Fact]
    public void Initialize_ShouldRegisterSyntaxProviders()
    {
        // Arrange
        var context = new TestIncrementalGeneratorInitializationContext();
        
        // Act
        _generator.Initialize(context);
        
        // Assert
        context."Should register providers for kernel methods and classes", SyntaxProviders.Count.Should().Be(2));
    }

    [Fact]
    public void Generator_ShouldGenerateKernelRegistry_WhenKernelMethodsExist()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class TestKernels
{
    [Kernel]
    public static void AddArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.Diagnostics.Should().BeEmpty("Generator should not produce diagnostics for valid kernel");
        result.GeneratedSources.ContainSingle(s => s.HintName == "KernelRegistry.g.cs");
        
        var registrySource = result.GeneratedSources.First(s => s.HintName == "KernelRegistry.g.cs");
        _ = registrySource.SourceText.ToString().Contain("KernelRegistry");
        _ = registrySource.SourceText.ToString().Contain("TestKernels.AddArrays");
    }

    [Fact]
    public void Generator_ShouldGenerateCpuImplementation_ForKernelMethods()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class MathKernels
{
    [Kernel(Backends = KernelBackends.CPU, VectorSize = 8)]
    public static void MultiplyArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] * b[i];
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Contain(s => s.HintName.Contains("MathKernels_MultiplyArrays_CPU.g.cs"));
        
        var cpuImpl = result.GeneratedSources.First(s => s.HintName.Contains("CPU.g.cs"));
        _ = cpuImpl.SourceText.ToString().Contain("MultiplyArraysCpuKernel");
        _ = cpuImpl.SourceText.ToString().Contain("ExecuteSIMD");
        _ = cpuImpl.SourceText.ToString().Contain("ExecuteScalar");
    }

    [Fact]
    public void Generator_ShouldGenerateMultipleBackends_WhenSpecified()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class MultiBackendKernel
{
    [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA | KernelBackends.Metal)]
    public static void ProcessData(float[] input, float[] output, int length)
    {
        for(int i = 0; i < length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Contain(s => s.HintName.Contains("_CPU.g.cs"));
        result.GeneratedSources.Contain(s => s.HintName.Contains("_CUDA.g.cs"));
        result.GeneratedSources.Contain(s => s.HintName.Contains("_Metal.g.cs"));
    }

    [Fact]
    public void Generator_ShouldGenerateKernelInvoker_ForKernelClasses()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class ComplexKernels
{
    [Kernel]
    public static void Add(float[] a, float[] b, float[] result, int length) { }
    
    [Kernel]
    public static void Multiply(float[] a, float[] b, float[] result, int length) { }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.Assert.Contains(s => s.HintName == "ComplexKernelsInvoker.g.cs", GeneratedSources);
        
        var invoker = result.GeneratedSources.First(s => s.HintName == "ComplexKernelsInvoker.g.cs");
        _ = invoker.SourceText.ToString().Contain("InvokeAdd");
        _ = invoker.SourceText.ToString().Contain("InvokeMultiply");
        _ = invoker.SourceText.ToString().Contain("InvokeKernel");
    }

    [Fact]
    public void Generator_ShouldHandleParallelKernels()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class ParallelKernels
{
    [Kernel(IsParallel = true, VectorSize = 16)]
    public static void ParallelSum(float[] input, float[] output, int length)
    {
        for(int i = 0; i < length; i++)
        {
            output[i] = input[i] + 1.0f;
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        var cpuImpl = result.GeneratedSources.First(s => s.HintName.Contains("CPU.g.cs"));
        _ = cpuImpl.SourceText.ToString().Contain("ExecuteParallel");
        _ = cpuImpl.SourceText.ToString().Contain("Parallel.ForEach");
    }

    [Fact]
    public void Generator_ShouldHandleOptimizationHints()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class OptimizedKernels
{
    [Kernel(Optimizations = OptimizationHints.Vectorize | OptimizationHints.FastMath, 
            MemoryPattern = MemoryAccessPattern.Sequential)]
    public static void OptimizedAdd(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        resultDiagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().NotBeEmpty();
    }

    [Fact]
    public void Generator_ShouldHandleGridAndBlockDimensions()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class GpuKernels
{
    [Kernel(Backends = KernelBackends.CUDA, 
            GridDimensions = new[] { 256, 256 }, 
            BlockDimensions = new[] { 16, 16 })]
    public static void MatrixMultiply(float[] a, float[] b, float[] c, int size)
    {
        // Matrix multiplication implementation
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Contain(s => s.HintName.Contains("_CUDA.g.cs"));
        var cudaImpl = result.GeneratedSources.First(s => s.HintName.Contains("CUDA.g.cs"));
        _ = cudaImpl.SourceText.ToString().Contain("MatrixMultiply_cuda_kernel");
    }

    [Fact]
    public void Generator_ShouldGenerateTypeConversions_ForDifferentTypes()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class TypedKernels
{
    [Kernel]
    public static void ProcessInts(int[] input, int[] output, int length) { }
    
    [Kernel]
    public static void ProcessDoubles(double[] input, double[] output, int length) { }
    
    [Kernel]
    public static unsafe void ProcessPointers(float* input, float* output, int length) { }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.GeneratedSources.HaveCountGreaterThan(3);
        result.GeneratedSources.SelectMany(s => s.SourceText.ToString().Split('\n'))
            .ContainMatch("*int*");
        result.GeneratedSources.SelectMany(s => s.SourceText.ToString().Split('\n'))
            .ContainMatch("*double*");
        result.GeneratedSources.SelectMany(s => s.SourceText.ToString().Split('\n'))
            .ContainMatch("*float**");
    }

    [Fact]
    public void Generator_ShouldSkipGenerationForEmptyInput()
    {
        // Arrange
        var source = @"
public class NonKernelClass
{
    public void RegularMethod() { }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().BeEmpty("No kernel methods should produce no output");
    }

    [Fact]
    public void Generator_ShouldHandleComplexMethodBodies()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using System;
using FluentAssertions;

public class ComplexKernels
{
    [Kernel]
    public static void ComplexOperation(float[] input, float[] output, int length)
    {
        for(int i = 0; i < length; i++)
        {
            var value = input[i];
            if(value > 0)
            {
                output[i] =(float)Math.Sqrt(value * 2.0f + 1.0f);
            }
            else
            {
                output[i] = Math.Abs(value);
            }
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        resultDiagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().NotBeEmpty();
        var cpuImpl = result.GeneratedSources.First(s => s.HintName.Contains("CPU.g.cs"));
        _ = cpuImpl.SourceText.ToString().Contain("ComplexOperationCpuKernel");
    }

    [Fact]
    public void Generator_ShouldHandleGenericConstraints()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class GenericKernels<T> where T : unmanaged
{
    [Kernel]
    public static void ProcessGeneric(T[] input, T[] output, int length)
    {
        for(int i = 0; i < length; i++)
        {
            output[i] = input[i];
        }
    }
}";

        // Act
        var result = TestHelper.RunGenerator(_generator, source);

        // Assert
        // Note: Generators may skip or handle generics differently
        // This test ensures no crashes occur with generic types
        Assert.NotNull(result);
    }
}

/// <summary>
/// Test context for incremental generator initialization.
/// </summary>
public class TestIncrementalGeneratorInitializationContext : IncrementalGeneratorInitializationContext
{
    public List<IncrementalValueProvider<object>> SyntaxProviders { get; } = new();

    public IncrementalValueProvider<Compilation> CompilationProvider => throw new NotImplementedException();
    
    public IncrementalValueProvider<AnalyzerConfigOptionsProvider> AnalyzerConfigOptionsProvider => throw new NotImplementedException();
    
    public IncrementalValueProvider<AdditionalTextsProvider> AdditionalTextsProvider => throw new NotImplementedException();
    
    public IncrementalValueProvider<MetadataReferencesProvider> MetadataReferencesProvider => throw new NotImplementedException();
    
    public IncrementalValueProvider<ParseOptionsProvider> ParseOptionsProvider => throw new NotImplementedException();

    public void RegisterSourceOutput<TSource>(IncrementalValueProvider<TSource> source, Action<SourceProductionContext, TSource> action)
    {
        // Test implementation
    }

    public void RegisterSourceOutput<TSource>(IncrementalValuesProvider<TSource> source, Action<SourceProductionContext, TSource> action)
    {
        // Test implementation
    }

    public void RegisterImplementationSourceOutput<TSource>(IncrementalValueProvider<TSource> source, Action<SourceProductionContext, TSource> action)
    {
        // Test implementation
    }

    public void RegisterImplementationSourceOutput<TSource>(IncrementalValuesProvider<TSource> source, Action<SourceProductionContext, TSource> action)
    {
        // Test implementation
    }

    public void RegisterPostInitializationOutput(Action<IncrementalGeneratorPostInitializationContext> callback)
    {
        // Test implementation
    }

    public IncrementalValueProvider<TResult> CreateSyntaxProvider<TResult>(
        Func<SyntaxNode, CancellationToken, bool> predicate,
        Func<GeneratorSyntaxContext, CancellationToken, TResult> transform)
    {
        var provider = new TestIncrementalValueProvider<TResult>();
        SyntaxProviders.Add(provider);
        return provider;
    }
}

/// <summary>
/// Test implementation of IncrementalValueProvider.
/// </summary>
public class TestIncrementalValueProvider<T> : IncrementalValueProvider<T>
{
    public IncrementalValueProvider<TResult> Select<TResult>(Func<T, CancellationToken, TResult> selector)
    {
        return new TestIncrementalValueProvider<TResult>();
    }

    public IncrementalValueProvider<TResult> Where<TResult>(Func<T, bool> predicate)
    {
        return new TestIncrementalValueProvider<TResult>();
    }

    public IncrementalValueProvider<TResult> Combine<TOther, TResult>(
        IncrementalValueProvider<TOther> other,
        Func<T, TOther, TResult> selector)
    {
        return new TestIncrementalValueProvider<TResult>();
    }
}
