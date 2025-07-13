// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using DotCompute.Generators.Backend;
using System.Linq;
using System.Collections.Generic;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Tests for vector operations code generation
/// Note: VectorOperationsGenerator is not yet implemented
/// </summary>
public class VectorOperationsGeneratorTests
{
    [Fact]
    public void CpuCodeGenerator_WithValidParameters_ShouldGenerate()
    {
        // Arrange
        var methodName = "AddVectors";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
            ("input1", "float[]", true),
            ("input2", "float[]", true),
            ("output", "float[]", true)
        };
        
        // Parse a simple method
        var sourceCode = @"
        public static void AddVectors(float[] input1, float[] input2, float[] output)
        {
            for (int i = 0; i < input1.Length; i++)
            {
                output[i] = input1[i] + input2[i];
            }
        }";
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var methodDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        var generator = new CpuCodeGenerator(methodName, parameters, methodDeclaration);

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("AddVectorsCpu");
        result.Should().Contain("ExecuteScalar");
        result.Should().Contain("ExecuteSimd");
        result.Should().Contain("ExecuteAvx2");
    }

    [Fact]
    public void CpuCodeGenerator_WithNullParameters_ShouldThrowArgumentNullException()
    {
        // Arrange
        var methodName = "TestMethod";
        var parameters = new List<(string name, string type, bool isBuffer)>();
        
        var sourceCode = @"public static void TestMethod() { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var methodDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();

        // Act & Assert
        var generator = new CpuCodeGenerator(methodName, parameters, methodDeclaration);
        generator.Should().NotBeNull();
    }

    [Fact]
    public void CpuCodeGenerator_WithSimpleArithmetic_ShouldGenerateOptimizedCode()
    {
        // Arrange
        var methodName = "MultiplyArrays";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
            ("input", "float[]", true),
            ("output", "float[]", true)
        };
        
        var sourceCode = @"
        public static void MultiplyArrays(float[] input, float[] output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = input[i] * 2.0f;
            }
        }";
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var methodDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();

        var generator = new CpuCodeGenerator(methodName, parameters, methodDeclaration);

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("MultiplyArraysCpu");
        result.Should().Contain("ExecuteParallel");
        result.Should().Contain("Vector.IsHardwareAccelerated");
    }

    [Fact]
    public void CpuCodeGenerator_WithComplexParameters_ShouldHandleCorrectly()
    {
        // Arrange
        var methodName = "ProcessData";
        var parameters = new List<(string name, string type, bool isBuffer)>
        {
            ("data1", "double[]", true),
            ("data2", "double[]", true),
            ("result", "double[]", true),
            ("scalar", "double", false)
        };
        
        var sourceCode = @"
        public static void ProcessData(double[] data1, double[] data2, double[] result, double scalar)
        {
            for (int i = 0; i < data1.Length; i++)
            {
                result[i] = data1[i] + data2[i] * scalar;
            }
        }";
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var methodDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();

        var generator = new CpuCodeGenerator(methodName, parameters, methodDeclaration);

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ProcessDataCpu");
        result.Should().Contain("Avx512F.IsSupported");
        result.Should().Contain("Avx2.IsSupported");
    }
}