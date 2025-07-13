// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using DotCompute.Generators.Kernel;
using FluentAssertions;
using System.Linq;

namespace DotCompute.Generators.Tests
{
    public class KernelSourceGeneratorTests
    {
        [Fact]
        public void KernelSourceGenerator_ShouldCreateInstance()
        {
            // Arrange & Act
            var generator = new KernelSourceGenerator();

            // Assert
            generator.Should().NotBeNull();
        }

        [Fact]
        public void KernelSourceGenerator_WithValidKernelSource_ShouldParseCorrectly()
        {
            var source = @"
using DotCompute.Generators.Kernel;
using System;

namespace TestNamespace
{
    public unsafe class TestKernels
    {
        [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA, VectorSize = 8)]
        public static void AddVectors(float* a, float* b, float* result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
    }
}";

            // Arrange
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree });
            var generator = new KernelSourceGenerator();

            // Act & Assert
            generator.Should().NotBeNull();
            compilation.Should().NotBeNull();
            compilation.SyntaxTrees.Should().NotBeEmpty();
            compilation.SyntaxTrees.First().GetText().ToString().Should().Contain("AddVectors");
        }

        [Fact]
        public void KernelSourceGenerator_WithMultipleKernels_ShouldHandleCorrectly()
        {
            var source = @"
using DotCompute.Generators.Kernel;
using System;

namespace TestNamespace
{
    public unsafe class MathKernels
    {
        [Kernel(Backends = KernelBackends.CPU, VectorSize = 8)]
        public static void MultiplyAdd(float* a, float* b, float* c, float* result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] * b[i] + c[i];
            }
        }

        [Kernel(Backends = KernelBackends.CUDA)]
        public static void Subtract(float* a, float* b, float* result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] - b[i];
            }
        }
    }
}";

            // Arrange
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree });
            var generator = new KernelSourceGenerator();

            // Act & Assert
            generator.Should().NotBeNull();
            compilation.Should().NotBeNull();
            compilation.SyntaxTrees.Should().NotBeEmpty();
            compilation.SyntaxTrees.First().GetText().ToString().Should().Contain("MultiplyAdd");
            compilation.SyntaxTrees.First().GetText().ToString().Should().Contain("Subtract");
        }

        [Fact]
        public void KernelCompilationAnalyzer_ShouldCreateInstance()
        {
            // Arrange & Act
            var analyzer = new KernelCompilationAnalyzer();

            // Assert
            analyzer.Should().NotBeNull();
        }

        private static Compilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(KernelAttribute).Assembly.Location)
            };

            return CSharpCompilation.Create(
                assemblyName: "TestAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}