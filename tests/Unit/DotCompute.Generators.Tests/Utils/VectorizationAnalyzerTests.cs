using System;
using System.Linq;
using DotCompute.Generators.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotCompute.Generators.Tests.Utils;

public class VectorizationAnalyzerTests
{
    #region CanVectorize Tests

    [Fact]
    public void CanVectorize_WithSimpleArrayAddition_ReturnsTrue()
    {
        // Arrange
        var code = @"
            class Test
            {
                void Method()
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        result[i] = a[i] + b[i];
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = VectorizationAnalyzer.CanVectorize(forLoop);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanVectorize_WithDataDependency_ReturnsFalse()
    {
        // Arrange
        var code = @"
            class Test
            {
                void Method()
                {
                    for (int i = 1; i < array.Length; i++)
                    {
                        array[i] = array[i-1] * 2;
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = VectorizationAnalyzer.CanVectorize(forLoop);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanVectorize_WithNullLoop_ReturnsFalse()
    {
        // Act
        var result = VectorizationAnalyzer.CanVectorize(null!);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanVectorize_WithBreakStatement_ReturnsFalse()
    {
        // Arrange
        var code = @"
            class Test
            {
                void Method()
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (array[i] == 0) break;
                        result[i] = array[i] * 2;
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = VectorizationAnalyzer.CanVectorize(forLoop);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanVectorize_WithContinueStatement_ReturnsFalse()
    {
        // Arrange
        var code = @"
            class Test
            {
                void Method()
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (array[i] < 0) continue;
                        result[i] = array[i] * 2;
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = VectorizationAnalyzer.CanVectorize(forLoop);
        
        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AnalyzeVectorizationOpportunities Tests

    [Fact]
    public void AnalyzeVectorizationOpportunities_WithVectorizableMethod_ReturnsOpportunities()
    {
        // Arrange
        var code = @"
            class Test
            {
                void ProcessArrays(float[] a, float[] b, float[] result)
                {
                    for (int i = 0; i < a.Length; i++)
                    {
                        result[i] = a[i] * b[i];
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var opportunities = VectorizationAnalyzer.AnalyzeVectorizationOpportunities(method);
        
        // Assert
        opportunities.Should().NotBeNull();
        opportunities.Should().HaveCountGreaterThan(0);
        opportunities.First().CanVectorize.Should().BeTrue();
        opportunities.First().Loop.Should().NotBeNull();
    }

    [Fact]
    public void AnalyzeVectorizationOpportunities_WithMultipleLoops_ReturnsAllOpportunities()
    {
        // Arrange
        var code = @"
            class Test
            {
                void ProcessData(float[] data)
                {
                    // First loop - vectorizable
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = data[i] * 2.0f;
                    }
                    
                    // Second loop - not vectorizable due to dependency
                    for (int i = 1; i < data.Length; i++)
                    {
                        data[i] = data[i-1] + data[i];
                    }
                    
                    // Third loop - vectorizable
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = Math.Abs(data[i]);
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var opportunities = VectorizationAnalyzer.AnalyzeVectorizationOpportunities(method);
        
        // Assert
        opportunities.Should().HaveCount(3);
        opportunities[0].CanVectorize.Should().BeTrue();
        opportunities[1].CanVectorize.Should().BeFalse();
        opportunities[2].CanVectorize.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeVectorizationOpportunities_WithNullMethod_ReturnsEmptyList()
    {
        // Act
        var opportunities = VectorizationAnalyzer.AnalyzeVectorizationOpportunities(null!);
        
        // Assert
        opportunities.Should().NotBeNull();
        opportunities.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeVectorizationOpportunities_WithNoLoops_ReturnsEmptyList()
    {
        // Arrange
        var code = @"
            class Test
            {
                int Add(int a, int b)
                {
                    return a + b;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var opportunities = VectorizationAnalyzer.AnalyzeVectorizationOpportunities(method);
        
        // Assert
        opportunities.Should().BeEmpty();
    }

    #endregion

    #region GetVectorizableExpressions Tests

    [Fact]
    public void GetVectorizableExpressions_WithArithmeticOperations_ReturnsExpressions()
    {
        // Arrange
        var code = @"
            void Method()
            {
                result = a + b;
                result = a - b;
                result = a * b;
                result = a / b;
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ {code} }}");
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var expressions = VectorizationAnalyzer.GetVectorizableExpressions(method);
        
        // Assert
        expressions.Should().HaveCount(4);
        expressions.Should().AllBeOfType<BinaryExpressionSyntax>();
    }

    [Fact]
    public void GetVectorizableExpressions_WithArrayAccess_ReturnsExpressions()
    {
        // Arrange
        var code = @"
            void Method()
            {
                for (int i = 0; i < n; i++)
                {
                    c[i] = a[i] + b[i];
                    d[i] = a[i] * 2.0f;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ {code} }}");
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var expressions = VectorizationAnalyzer.GetVectorizableExpressions(method);
        
        // Assert
        expressions.Should().HaveCountGreaterThan(0);
        expressions.Should().Contain(e => e.ToString().Contains("+"));
        expressions.Should().Contain(e => e.ToString().Contains("*"));
    }

    [Fact]
    public void GetVectorizableExpressions_WithNullMethod_ReturnsEmptyList()
    {
        // Act
        var expressions = VectorizationAnalyzer.GetVectorizableExpressions(null!);
        
        // Assert
        expressions.Should().NotBeNull();
        expressions.Should().BeEmpty();
    }

    [Fact]
    public void GetVectorizableExpressions_WithComplexExpressions_IdentifiesCorrectly()
    {
        // Arrange
        var code = @"
            void Method()
            {
                // Vectorizable
                result = (a + b) * (c - d);
                
                // Not vectorizable (string operations)
                text = str1 + str2;
                
                // Vectorizable
                value = Math.Sqrt(x * x + y * y);
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ {code} }}");
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var expressions = VectorizationAnalyzer.GetVectorizableExpressions(method);
        
        // Assert
        expressions.Should().HaveCountGreaterThanOrEqualTo(2);
        expressions.Should().NotContain(e => e.ToString().Contains("str1"));
    }

    #endregion

    #region EstimateSpeedup Tests

    [Theory]
    [InlineData(100, 1.0, 1.5)]    // Small loop
    [InlineData(1000, 2.0, 4.0)]   // Medium loop
    [InlineData(10000, 3.0, 8.0)]  // Large loop
    public void EstimateSpeedup_WithDifferentLoopSizes_ReturnsReasonableEstimates(int iterations, double minSpeedup, double maxSpeedup)
    {
        // Arrange
        var code = $@"
            for (int i = 0; i < {iterations}; i++)
            {{
                result[i] = a[i] + b[i];
            }}";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var speedup = VectorizationAnalyzer.EstimateSpeedup(forLoop, "float");
        
        // Assert
        speedup.Should().BeGreaterThanOrEqualTo(minSpeedup);
        speedup.Should().BeLessThanOrEqualTo(maxSpeedup);
    }

    [Fact]
    public void EstimateSpeedup_WithNullLoop_ReturnsOne()
    {
        // Act
        var speedup = VectorizationAnalyzer.EstimateSpeedup(null!, "float");
        
        // Assert
        speedup.Should().Be(1.0);
    }

    [Theory]
    [InlineData("float", 2.0, 8.0)]   // 32-bit, good vectorization
    [InlineData("double", 1.5, 4.0)]  // 64-bit, moderate vectorization
    [InlineData("byte", 4.0, 16.0)]   // 8-bit, excellent vectorization
    public void EstimateSpeedup_WithDifferentDataTypes_ReturnsAppropriateEstimates(string dataType, double minSpeedup, double maxSpeedup)
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 10000; i++)
            {
                result[i] = a[i] * b[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var speedup = VectorizationAnalyzer.EstimateSpeedup(forLoop, dataType);
        
        // Assert
        speedup.Should().BeGreaterThanOrEqualTo(minSpeedup);
        speedup.Should().BeLessThanOrEqualTo(maxSpeedup);
    }

    [Fact]
    public void EstimateSpeedup_WithUnsupportedType_ReturnsOne()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 1000; i++)
            {
                result[i] = data[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var speedup = VectorizationAnalyzer.EstimateSpeedup(forLoop, "string");
        
        // Assert
        speedup.Should().Be(1.0);
    }

    #endregion

    #region GenerateVectorizedCode Tests

    [Fact]
    public void GenerateVectorizedCode_WithSimpleLoop_GeneratesValidCode()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < array.Length; i++)
            {
                result[i] = array[i] * 2.0f;
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var vectorizedCode = VectorizationAnalyzer.GenerateVectorizedCode(forLoop, "float");
        
        // Assert
        vectorizedCode.Should().NotBeNull();
        vectorizedCode.Should().ContainAny("Vector", "Simd", "vector");
        
        // Verify generated code is valid syntax
        var testCode = $"class Test {{ void Method() {{ {vectorizedCode} }} }}";
        var vectorizedTree = CSharpSyntaxTree.ParseText(testCode);
        vectorizedTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void GenerateVectorizedCode_WithNullLoop_ReturnsEmptyString()
    {
        // Act
        var result = VectorizationAnalyzer.GenerateVectorizedCode(null!, "float");
        
        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateVectorizedCode_WithComplexExpression_GeneratesOptimizedCode()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < n; i++)
            {
                result[i] = (a[i] + b[i]) * c[i] - d[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var vectorizedCode = VectorizationAnalyzer.GenerateVectorizedCode(forLoop, "float");
        
        // Assert
        vectorizedCode.Should().NotBeNull();
        vectorizedCode.Should().NotBeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void VectorizationAnalyzer_EndToEnd_WorksCorrectly()
    {
        // Arrange
        var code = @"
            class Processor
            {
                public void ProcessData(float[] input, float[] output)
                {
                    // This loop should be vectorizable
                    for (int i = 0; i < input.Length; i++)
                    {
                        output[i] = input[i] * 2.0f + 1.0f;
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var opportunities = VectorizationAnalyzer.AnalyzeVectorizationOpportunities(method);
        var canVectorize = opportunities.Any(o => o.CanVectorize);
        
        if (canVectorize)
        {
            var loop = opportunities.First(o => o.CanVectorize).Loop;
            var speedup = VectorizationAnalyzer.EstimateSpeedup(loop, "float");
            var vectorizedCode = VectorizationAnalyzer.GenerateVectorizedCode(loop, "float");
            
            // Assert
            speedup.Should().BeGreaterThan(1.0);
            vectorizedCode.Should().NotBeEmpty();
        }
        
        // Assert
        canVectorize.Should().BeTrue();
    }

    [Fact]
    public void VectorizationAnalyzer_WithNestedLoops_AnalyzesCorrectly()
    {
        // Arrange
        var code = @"
            class MatrixProcessor
            {
                public void MultiplyMatrices(float[,] a, float[,] b, float[,] result)
                {
                    int n = a.GetLength(0);
                    
                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            float sum = 0;
                            // This inner loop might be vectorizable
                            for (int k = 0; k < n; k++)
                            {
                                sum += a[i, k] * b[k, j];
                            }
                            result[i, j] = sum;
                        }
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var opportunities = VectorizationAnalyzer.AnalyzeVectorizationOpportunities(method);
        
        // Assert
        opportunities.Should().HaveCount(3); // Three nested loops
        // Inner loop might be vectorizable depending on analysis
        opportunities.Should().Contain(o => o.Loop.ToString().Contains("sum"));
    }

    [Fact]
    public void VectorizationAnalyzer_WithConditionalLogic_IdentifiesAsNonVectorizable()
    {
        // Arrange
        var code = @"
            class ConditionalProcessor
            {
                public void ProcessWithConditions(float[] data)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i] > 0)
                        {
                            data[i] = Math.Sqrt(data[i]);
                        }
                        else
                        {
                            data[i] = -data[i];
                        }
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var loop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var canVectorize = VectorizationAnalyzer.CanVectorize(loop);
        
        // Assert
        canVectorize.Should().BeFalse(); // Conditional logic prevents vectorization
    }

    #endregion
}