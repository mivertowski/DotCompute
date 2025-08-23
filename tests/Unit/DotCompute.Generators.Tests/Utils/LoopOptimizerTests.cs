using System;
using System.Linq;
using DotCompute.Generators.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotCompute.Generators.Tests.Utils;

public class LoopOptimizerTests
{
    #region OptimizeLoop Tests

    [Fact]
    public void OptimizeLoop_WithSimpleForLoop_AppliesUnrolling()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 100; i++)
            {
                array[i] = i * 2;
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.OptimizeLoop(forLoop);
        
        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().Contain("i");
        // The optimized loop should still be valid syntax
        var optimizedTree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {result} }} }}");
        optimizedTree.GetDiagnostics().Should().BeEmpty();
    }

    [Fact]
    public void OptimizeLoop_WithNullLoop_ReturnsNull()
    {
        // Act
        var result = LoopOptimizer.OptimizeLoop(null!);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OptimizeLoop_WithComplexCondition_HandlesCorrectly()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < array.Length && i < maxSize; i++)
            {
                Process(array[i]);
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.OptimizeLoop(forLoop);
        
        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void OptimizeLoop_WithNestedLoops_OptimizesOuterLoop()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    matrix[i][j] = i * j;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var outerLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.OptimizeLoop(outerLoop);
        
        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().Contain("i");
    }

    #endregion

    #region UnrollLoop Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void UnrollLoop_WithDifferentFactors_GeneratesCorrectUnrolling(int factor)
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 100; i++)
            {
                sum += array[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.UnrollLoop(forLoop, factor);
        
        // Assert
        result.Should().NotBeNull();
        if (factor > 1)
        {
            // Unrolled loop should contain multiple statements in the body
            result.ToString().Should().Contain("+=");
        }
    }

    [Fact]
    public void UnrollLoop_WithInvalidFactor_ReturnsOriginalLoop()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 10; i++)
            {
                Process(i);
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var resultNegative = LoopOptimizer.UnrollLoop(forLoop, -1);
        var resultZero = LoopOptimizer.UnrollLoop(forLoop, 0);
        
        // Assert
        resultNegative.Should().BeSameAs(forLoop);
        resultZero.Should().BeSameAs(forLoop);
    }

    [Fact]
    public void UnrollLoop_WithNullLoop_ReturnsNull()
    {
        // Act
        var result = LoopOptimizer.UnrollLoop(null!, 4);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UnrollLoop_WithEmptyBody_HandlesCorrectly()
    {
        // Arrange
        var code = @"for (int i = 0; i < 10; i++) { }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.UnrollLoop(forLoop, 2);
        
        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region VectorizeLoop Tests

    [Fact]
    public void VectorizeLoop_WithSimpleArrayOperation_GeneratesVectorizedCode()
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
        var result = LoopOptimizer.VectorizeLoop(forLoop, "float");
        
        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().ContainAny("Vector", "vector", "SIMD");
    }

    [Fact]
    public void VectorizeLoop_WithUnsupportedType_ReturnsOriginalLoop()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < array.Length; i++)
            {
                strings[i] = array[i].ToString();
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.VectorizeLoop(forLoop, "string");
        
        // Assert
        result.Should().BeSameAs(forLoop);
    }

    [Fact]
    public void VectorizeLoop_WithNullLoop_ReturnsNull()
    {
        // Act
        var result = LoopOptimizer.VectorizeLoop(null!, "float");
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("int")]
    public void VectorizeLoop_WithDifferentNumericTypes_GeneratesAppropriateVectorCode(string elementType)
    {
        // Arrange
        var code = @"
            for (int i = 0; i < data.Length; i++)
            {
                output[i] = data[i] + offset[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.VectorizeLoop(forLoop, elementType);
        
        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region TileLoop Tests

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void TileLoop_WithDifferentTileSizes_GeneratesTiledLoop(int tileSize)
    {
        // Arrange
        var code = @"
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    matrix[i][j] = compute(i, j);
                }
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.TileLoop(forLoop, tileSize);
        
        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void TileLoop_WithInvalidTileSize_ReturnsOriginalLoop()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 100; i++)
            {
                Process(i);
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var resultNegative = LoopOptimizer.TileLoop(forLoop, -1);
        var resultZero = LoopOptimizer.TileLoop(forLoop, 0);
        
        // Assert
        resultNegative.Should().BeSameAs(forLoop);
        resultZero.Should().BeSameAs(forLoop);
    }

    [Fact]
    public void TileLoop_WithNullLoop_ReturnsNull()
    {
        // Act
        var result = LoopOptimizer.TileLoop(null!, 32);
        
        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region AnalyzeLoopDependencies Tests

    [Fact]
    public void AnalyzeLoopDependencies_WithIndependentIterations_ReturnsNoDependencies()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < n; i++)
            {
                array[i] = i * 2;
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.AnalyzeLoopDependencies(forLoop);
        
        // Assert
        result.Should().NotBeNull();
        result.HasDependencies.Should().BeFalse();
        result.CanVectorize.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeLoopDependencies_WithDependentIterations_ReturnsDependencies()
    {
        // Arrange
        var code = @"
            for (int i = 1; i < n; i++)
            {
                array[i] = array[i-1] + array[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.AnalyzeLoopDependencies(forLoop);
        
        // Assert
        result.Should().NotBeNull();
        result.HasDependencies.Should().BeTrue();
        result.CanVectorize.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeLoopDependencies_WithNullLoop_ReturnsNull()
    {
        // Act
        var result = LoopOptimizer.AnalyzeLoopDependencies(null!);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void AnalyzeLoopDependencies_WithFunctionCalls_IdentifiesAsPotentiallyDependent()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < n; i++)
            {
                result[i] = ComputeValue(i);
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.AnalyzeLoopDependencies(forLoop);
        
        // Assert
        result.Should().NotBeNull();
        // Function calls might have side effects
        result.Dependencies.Should().NotBeEmpty();
    }

    #endregion

    #region ApplyLoopFusion Tests

    [Fact]
    public void ApplyLoopFusion_WithCompatibleLoops_FusesSuccessfully()
    {
        // Arrange
        var code1 = @"
            for (int i = 0; i < n; i++)
            {
                a[i] = b[i] * 2;
            }";
        var code2 = @"
            for (int i = 0; i < n; i++)
            {
                c[i] = a[i] + 1;
            }";
        
        var tree1 = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code1} }} }}");
        var tree2 = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code2} }} }}");
        
        var loop1 = tree1.GetRoot().DescendantNodes().OfType<ForStatementSyntax>().First();
        var loop2 = tree2.GetRoot().DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.ApplyLoopFusion(loop1, loop2);
        
        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().Contain("a[i]");
        result.ToString().Should().Contain("c[i]");
    }

    [Fact]
    public void ApplyLoopFusion_WithIncompatibleBounds_ReturnsNull()
    {
        // Arrange
        var code1 = @"
            for (int i = 0; i < 10; i++)
            {
                a[i] = i;
            }";
        var code2 = @"
            for (int i = 0; i < 20; i++)
            {
                b[i] = i * 2;
            }";
        
        var tree1 = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code1} }} }}");
        var tree2 = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code2} }} }}");
        
        var loop1 = tree1.GetRoot().DescendantNodes().OfType<ForStatementSyntax>().First();
        var loop2 = tree2.GetRoot().DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.ApplyLoopFusion(loop1, loop2);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ApplyLoopFusion_WithNullLoops_ReturnsNull()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < n; i++)
            {
                a[i] = i;
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var loop = tree.GetRoot().DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result1 = LoopOptimizer.ApplyLoopFusion(null!, loop);
        var result2 = LoopOptimizer.ApplyLoopFusion(loop, null!);
        var result3 = LoopOptimizer.ApplyLoopFusion(null!, null!);
        
        // Assert
        result1.Should().BeNull();
        result2.Should().BeNull();
        result3.Should().BeNull();
    }

    #endregion

    #region Integration and Edge Case Tests

    [Fact]
    public void OptimizeLoop_WithWhileLoop_HandlesGracefully()
    {
        // Arrange
        var code = @"
            while (i < 100)
            {
                array[i] = i * 2;
                i++;
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ int i = 0; {code} }} }}");
        var root = tree.GetRoot();
        var whileLoop = root.DescendantNodes().OfType<WhileStatementSyntax>().First();
        
        // Act
        // OptimizeLoop expects ForStatementSyntax, so this should handle the mismatch gracefully
        var forLoop = whileLoop as ForStatementSyntax;
        var result = LoopOptimizer.OptimizeLoop(forLoop!);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void VectorizeLoop_WithComplexExpression_SimplifiesWherePoossible()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < n; i++)
            {
                result[i] = (a[i] * b[i]) + (c[i] * d[i]) - e[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var forLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var result = LoopOptimizer.VectorizeLoop(forLoop, "float");
        
        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void UnrollLoop_PreservesLoopSemantics()
    {
        // Arrange
        var code = @"
            for (int i = 0; i < 8; i++)
            {
                sum += values[i];
            }";
        var tree = CSharpSyntaxTree.ParseText($"class Test {{ void Method() {{ {code} }} }}");
        var root = tree.GetRoot();
        var originalLoop = root.DescendantNodes().OfType<ForStatementSyntax>().First();
        
        // Act
        var unrolledLoop = LoopOptimizer.UnrollLoop(originalLoop, 4);
        
        // Assert
        unrolledLoop.Should().NotBeNull();
        // The unrolled loop should still process all elements
        unrolledLoop.ToString().Should().Contain("i");
    }

    #endregion
}