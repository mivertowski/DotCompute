// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Generators.Backend;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

public class CpuCodeGeneratorTests
{
    [Fact]
    public void Generate_SimpleKernel_ShouldProduceValidCode()
    {
        // Arrange
        var methodName = "AddArrays";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
           ("input1", "float[]", true),
           ("input2", "float[]", true),
           ("output", "float[]", true),
           ("length", "int", false)
        };
        var methodSyntax = CreateSimpleMethodSyntax();
        var generator = new CpuCodeGenerator(methodName, parameters, methodSyntax);

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Assert.Contains("namespace DotCompute.Generated.Cpu", result);
        Assert.Contains($"public static unsafe class {methodName}Cpu", result);
        Assert.Contains("ExecuteScalar", result);
        Assert.Contains("ExecuteSimd", result);
        Assert.Contains("ExecuteAvx2", result);
        Assert.Contains("ExecuteAvx512", result);
        Assert.Contains("ExecuteParallel", result);
        Assert.Contains("Execute", result);
    }

    [Fact]
    public void Generate_WithDifferentParameterTypes_ShouldHandleAllTypes()
    {
        // Arrange
        var methodName = "ProcessData";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
           ("intArray", "int[]", true),
           ("doubleArray", "double[]", true),
           ("floatPtr", "float*", true),
           ("count", "int", false),
           ("scale", "double", false)
        };
        var methodSyntax = CreateSimpleMethodSyntax();
        var generator = new CpuCodeGenerator(methodName, parameters, methodSyntax);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("int[] intArray", result);
        Assert.Contains("double[] doubleArray", result);
        Assert.Contains("float* floatPtr", result);
        Assert.Contains("int count", result);
        Assert.Contains("double scale", result);
    }

    [Fact]
    public void Generate_ShouldIncludeRequiredUsings()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("using System;", result);
        Assert.Contains("using System.Runtime.CompilerServices;", result);
        Assert.Contains("using System.Runtime.Intrinsics;", result);
        Assert.Contains("using System.Runtime.Intrinsics.X86;", result);
        Assert.Contains("using System.Runtime.Intrinsics.Arm;", result);
        Assert.Contains("using System.Threading.Tasks;", result);
        Assert.Contains("using System.Numerics;", result);
    }

    [Fact]
    public void Generate_ScalarImplementation_ShouldIncludeParameterValidation()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("ExecuteScalar", result);
        Assert.Contains("ArgumentNullException.ThrowIfNull", result);
        Assert.Contains("/// Scalar implementation for compatibility", result);
    }

    [Fact]
    public void Generate_SimdImplementation_ShouldIncludeVectorOperations()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("ExecuteSimd", result);
        Assert.Contains("Vector<float>.Count", result);
        Assert.Contains("alignedEnd", result);
        Assert.Contains("vectorSize", result);
        Assert.Contains("/// SIMD implementation using platform-agnostic vectors", result);
    }

    [Fact]
    public void Generate_Avx2Implementation_ShouldIncludeAvxIntrinsics()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("ExecuteAvx2", result);
        Assert.Contains("const int vectorSize = 8", result); // 256-bit / 32-bit
        Assert.Contains("/// AVX2 optimized implementation", result);
    }

    [Fact]
    public void Generate_Avx512Implementation_ShouldIncludeAvx512Intrinsics()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("ExecuteAvx512", result);
        Assert.Contains("const int vectorSize = 16", result); // 512-bit / 32-bit
        Assert.Contains("/// AVX-512 optimized implementation", result);
    }

    [Fact]
    public void Generate_ParallelImplementation_ShouldIncludeTaskParallelism()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("ExecuteParallel", result);
        Assert.Contains("Environment.ProcessorCount", result);
        Assert.Contains("Parallel.ForEach", result);
        Assert.Contains("Partitioner.Create", result);
        Assert.Contains("/// Parallel implementation using task parallelism", result);
    }

    [Fact]
    public void Generate_MainExecuteMethod_ShouldIncludeHardwareDetection()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("Avx512F.IsSupported", result);
        Assert.Contains("Avx2.IsSupported", result);
        Assert.Contains("Vector.IsHardwareAccelerated", result);
        Assert.Contains("/// Main execution method that selects the best implementation", result);
    }

    [Fact]
    public void Generate_ShouldIncludeConvenienceOverload()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("/// Convenience overload for full array processing", result);
        result.Should().MatchRegex(@"public static void Execute\([^,]+,\s*int length\)");
    }

    [Fact]
    public void Generate_WithComplexMethodBody_ShouldHandleTransformation()
    {
        // Arrange
        var methodName = "ComplexKernel";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
           ("input", "float[]", true),
           ("output", "float[]", true)
        };
        var complexMethod = CreateComplexMethodSyntax();
        var generator = new CpuCodeGenerator(methodName, parameters, complexMethod);

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Assert.Contains("ComplexKernelCpu", result);
        Assert.Contains("ExecuteScalar", result);
    }

    [Fact]
    public void Generate_WithArithmeticOperations_ShouldGenerateOptimizedVersions()
    {
        // Arrange
        var methodSyntax = CreateArithmeticMethodSyntax();
        var generator = CreateGeneratorWithMethod(methodSyntax);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("Vector.Add", result);
        Assert.Contains("Avx.Add", result);
        Assert.Contains("Avx512F.Add", result);
        Assert.Contains("fixedfloat*", result);
    }

    [Fact]
    public void Generate_WithMemoryOperations_ShouldOptimizeMemoryAccess()
    {
        // Arrange
        var methodSyntax = CreateMemoryMethodSyntax();
        var generator = CreateGeneratorWithMethod(methodSyntax);

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("LoadVector256", result);
        Assert.Contains("Store", result);
        Assert.Contains("CopyTo", result);
    }

    [Fact]
    public void Generate_ShouldIncludeAggressiveInliningAttribute()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Contain("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
    }

    [Fact]
    public void Generate_ShouldIncludeUnsafeContexts()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("public static unsafe class", result);
        Assert.Contains("unsafe", result);
        Assert.Contains("fixedfloat*", result);
    }

    [Fact]
    public void Generate_ShouldHandleRemainder_InVectorizedLoops()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("// Process remainder", result);
        result.Contain("ifalignedEnd < end)");
        Assert.Contains("ExecuteScalar(", result);
    }

    [Fact]
    public void Generate_ShouldIncludeDocumentationComments()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        Assert.Contains("/// <summary>", result);
        Assert.Contains("/// CPU implementation for", result);
        Assert.Contains("/// Scalar implementation", result);
        Assert.Contains("/// SIMD implementation", result);
        Assert.Contains("/// AVX2 optimized", result);
        Assert.Contains("/// AVX-512 optimized", result);
        Assert.Contains("/// Parallel implementation", result);
    }

    [Fact]
    public void Generate_WithNoBufferParameters_ShouldStillWork()
    {
        // Arrange
        var methodName = "SimpleCalculation";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
           ("value", "float", false),
           ("multiplier", "float", false)
        };
        var methodSyntax = CreateSimpleMethodSyntax();
        var generator = new CpuCodeGenerator(methodName, parameters, methodSyntax);

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Assert.Contains("SimpleCalculationCpu", result);
        Assert.Contains("float value", result);
        Assert.Contains("float multiplier", result);
    }

    private CpuCodeGenerator CreateBasicGenerator()
    {
        var methodName = "TestKernel";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
           ("input", "float[]", true),
           ("output", "float[]", true),
           ("length", "int", false)
        };
        var methodSyntax = CreateSimpleMethodSyntax();
        return new CpuCodeGenerator(methodName, parameters, methodSyntax);
    }

    private CpuCodeGenerator CreateGeneratorWithMethod(MethodDeclarationSyntax methodSyntax)
    {
        var methodName = "TestKernel";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
           ("input1", "float[]", true),
           ("input2", "float[]", true),
           ("output", "float[]", true),
           ("length", "int", false)
        };
        return new CpuCodeGenerator(methodName, parameters, methodSyntax);
    }

    private static MethodDeclarationSyntax CreateSimpleMethodSyntax()
    {
        var source = @"
public static void TestMethod(float[] input, float[] output, int length)
{
    for(int i = 0; i < length; i++)
    {
        output[i] = input[i] * 2.0f;
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    private static MethodDeclarationSyntax CreateComplexMethodSyntax()
    {
        var source = @"
public static void ComplexMethod(float[] input, float[] output, int length)
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
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    private static MethodDeclarationSyntax CreateArithmeticMethodSyntax()
    {
        var source = @"
public static void ArithmeticMethod(float[] a, float[] b, float[] result, int length)
{
    for(int i = 0; i < length; i++)
    {
        result[i] = a[i] + b[i] * 2.0f - 1.0f;
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    private static MethodDeclarationSyntax CreateMemoryMethodSyntax()
    {
        var source = @"
public static void MemoryMethod(float[] input, float[] output, int length)
{
    for(int i = 0; i < length; i++)
    {
        output[i] = input[i];
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }
}
}
