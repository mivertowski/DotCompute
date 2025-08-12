// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Generators.Backend;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotCompute.Tests.Unit;

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
        result.Should().Contain("namespace DotCompute.Generated.Cpu");
        result.Should().Contain($"public static unsafe class {methodName}Cpu");
        result.Should().Contain("ExecuteScalar");
        result.Should().Contain("ExecuteSimd");
        result.Should().Contain("ExecuteAvx2");
        result.Should().Contain("ExecuteAvx512");
        result.Should().Contain("ExecuteParallel");
        result.Should().Contain("Execute");
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
        result.Should().Contain("int[] intArray");
        result.Should().Contain("double[] doubleArray");
        result.Should().Contain("float* floatPtr");
        result.Should().Contain("int count");
        result.Should().Contain("double scale");
    }

    [Fact]
    public void Generate_ShouldIncludeRequiredUsings()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("using System;");
        result.Should().Contain("using System.Runtime.CompilerServices;");
        result.Should().Contain("using System.Runtime.Intrinsics;");
        result.Should().Contain("using System.Runtime.Intrinsics.X86;");
        result.Should().Contain("using System.Runtime.Intrinsics.Arm;");
        result.Should().Contain("using System.Threading.Tasks;");
        result.Should().Contain("using System.Numerics;");
    }

    [Fact]
    public void Generate_ScalarImplementation_ShouldIncludeParameterValidation()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("ExecuteScalar");
        result.Should().Contain("ArgumentNullException.ThrowIfNull");
        result.Should().Contain("/// Scalar implementation for compatibility");
    }

    [Fact]
    public void Generate_SimdImplementation_ShouldIncludeVectorOperations()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("ExecuteSimd");
        result.Should().Contain("Vector<float>.Count");
        result.Should().Contain("alignedEnd");
        result.Should().Contain("vectorSize");
        result.Should().Contain("/// SIMD implementation using platform-agnostic vectors");
    }

    [Fact]
    public void Generate_Avx2Implementation_ShouldIncludeAvxIntrinsics()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("ExecuteAvx2");
        result.Should().Contain("const int vectorSize = 8"); // 256-bit / 32-bit
        result.Should().Contain("/// AVX2 optimized implementation");
    }

    [Fact]
    public void Generate_Avx512Implementation_ShouldIncludeAvx512Intrinsics()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("ExecuteAvx512");
        result.Should().Contain("const int vectorSize = 16"); // 512-bit / 32-bit
        result.Should().Contain("/// AVX-512 optimized implementation");
    }

    [Fact]
    public void Generate_ParallelImplementation_ShouldIncludeTaskParallelism()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("ExecuteParallel");
        result.Should().Contain("Environment.ProcessorCount");
        result.Should().Contain("Parallel.ForEach");
        result.Should().Contain("Partitioner.Create");
        result.Should().Contain("/// Parallel implementation using task parallelism");
    }

    [Fact]
    public void Generate_MainExecuteMethod_ShouldIncludeHardwareDetection()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("Avx512F.IsSupported");
        result.Should().Contain("Avx2.IsSupported");
        result.Should().Contain("Vector.IsHardwareAccelerated");
        result.Should().Contain("/// Main execution method that selects the best implementation");
    }

    [Fact]
    public void Generate_ShouldIncludeConvenienceOverload()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("/// Convenience overload for full array processing");
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
        result.Should().Contain("ComplexKernelCpu");
        result.Should().Contain("ExecuteScalar");
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
        result.Should().Contain("Vector.Add");
        result.Should().Contain("Avx.Add");
        result.Should().Contain("Avx512F.Add");
        result.Should().Contain("fixed (float*");
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
        result.Should().Contain("LoadVector256");
        result.Should().Contain("Store");
        result.Should().Contain("CopyTo");
    }

    [Fact]
    public void Generate_ShouldIncludeAggressiveInliningAttribute()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
    }

    [Fact]
    public void Generate_ShouldIncludeUnsafeContexts()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("public static unsafe class");
        result.Should().Contain("unsafe");
        result.Should().Contain("fixed (float*");
    }

    [Fact]
    public void Generate_ShouldHandleRemainder_InVectorizedLoops()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("// Process remainder");
        result.Should().Contain("if (alignedEnd < end)");
        result.Should().Contain("ExecuteScalar(");
    }

    [Fact]
    public void Generate_ShouldIncludeDocumentationComments()
    {
        // Arrange
        var generator = CreateBasicGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// CPU implementation for");
        result.Should().Contain("/// Scalar implementation");
        result.Should().Contain("/// SIMD implementation");
        result.Should().Contain("/// AVX2 optimized");
        result.Should().Contain("/// AVX-512 optimized");
        result.Should().Contain("/// Parallel implementation");
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
        result.Should().Contain("SimpleCalculationCpu");
        result.Should().Contain("float value");
        result.Should().Contain("float multiplier");
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
    for (int i = 0; i < length; i++)
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
    for (int i = 0; i < length; i++)
    {
        var value = input[i];
        if (value > 0)
        {
            output[i] = (float)Math.Sqrt(value * 2.0f + 1.0f);
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
    for (int i = 0; i < length; i++)
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
    for (int i = 0; i < length; i++)
    {
        output[i] = input[i];
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }
}