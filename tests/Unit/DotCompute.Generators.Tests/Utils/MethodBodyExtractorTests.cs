using System;
using System.Linq;
using DotCompute.Generators.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotCompute.Generators.Tests.Utils;

public class MethodBodyExtractorTests
{
    #region ExtractMethodBody Tests

    [Fact]
    public void ExtractMethodBody_WithSimpleMethod_ExtractsBody()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var body = MethodBodyExtractor.ExtractMethodBody(method);
        
        // Assert
        body.Should().NotBeNull();
        body!.Statements.Should().HaveCount(1);
        body.Statements[0].Should().BeOfType<ReturnStatementSyntax>();
        body.ToString().Should().Contain("return a + b");
    }

    [Fact]
    public void ExtractMethodBody_WithExpressionBody_ReturnsNull()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Add(int a, int b) => a + b;
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var body = MethodBodyExtractor.ExtractMethodBody(method);
        
        // Assert
        body.Should().BeNull(); // Expression-bodied methods don't have block bodies
    }

    [Fact]
    public void ExtractMethodBody_WithNullMethod_ReturnsNull()
    {
        // Act
        var body = MethodBodyExtractor.ExtractMethodBody(null!);
        
        // Assert
        body.Should().BeNull();
    }

    [Fact]
    public void ExtractMethodBody_WithComplexBody_ExtractsAllStatements()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void ProcessData(int[] data)
                {
                    if (data == null) throw new ArgumentNullException(nameof(data));
                    
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] *= 2;
                    }
                    
                    Console.WriteLine(""Processing complete"");
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var body = MethodBodyExtractor.ExtractMethodBody(method);
        
        // Assert
        body.Should().NotBeNull();
        body!.Statements.Should().HaveCount(3);
        body.Statements[0].Should().BeOfType<IfStatementSyntax>();
        body.Statements[1].Should().BeOfType<ForStatementSyntax>();
        body.Statements[2].Should().BeOfType<ExpressionStatementSyntax>();
    }

    #endregion

    #region ExtractExpressionBody Tests

    [Fact]
    public void ExtractExpressionBody_WithExpressionBodiedMethod_ExtractsExpression()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Square(int x) => x * x;
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var expression = MethodBodyExtractor.ExtractExpressionBody(method);
        
        // Assert
        expression.Should().NotBeNull();
        expression!.ToString().Should().Be("x * x");
        expression.Should().BeOfType<BinaryExpressionSyntax>();
    }

    [Fact]
    public void ExtractExpressionBody_WithBlockBody_ReturnsNull()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var expression = MethodBodyExtractor.ExtractExpressionBody(method);
        
        // Assert
        expression.Should().BeNull();
    }

    [Fact]
    public void ExtractExpressionBody_WithNullMethod_ReturnsNull()
    {
        // Act
        var expression = MethodBodyExtractor.ExtractExpressionBody(null!);
        
        // Assert
        expression.Should().BeNull();
    }

    #endregion

    #region ExtractStatements Tests

    [Fact]
    public void ExtractStatements_WithMultipleStatements_ExtractsAll()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void Method()
                {
                    int x = 5;
                    x *= 2;
                    Console.WriteLine(x);
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var statements = MethodBodyExtractor.ExtractStatements(method);
        
        // Assert
        statements.Should().HaveCount(3);
        statements[0].Should().BeOfType<LocalDeclarationStatementSyntax>();
        statements[1].Should().BeOfType<ExpressionStatementSyntax>();
        statements[2].Should().BeOfType<ExpressionStatementSyntax>();
    }

    [Fact]
    public void ExtractStatements_WithExpressionBody_ReturnsEmptyList()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Double(int x) => x * 2;
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var statements = MethodBodyExtractor.ExtractStatements(method);
        
        // Assert
        statements.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStatements_WithNullMethod_ReturnsEmptyList()
    {
        // Act
        var statements = MethodBodyExtractor.ExtractStatements(null!);
        
        // Assert
        statements.Should().NotBeNull();
        statements.Should().BeEmpty();
    }

    #endregion

    #region ExtractLoops Tests

    [Fact]
    public void ExtractLoops_WithForLoops_ExtractsAll()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void ProcessArrays()
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Console.WriteLine(i);
                    }
                    
                    for (int j = 0; j < 20; j++)
                    {
                        Process(j);
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var loops = MethodBodyExtractor.ExtractLoops(method);
        
        // Assert
        loops.Should().HaveCount(2);
        loops.Should().AllBeOfType<ForStatementSyntax>();
        loops[0].ToString().Should().Contain("i < 10");
        loops[1].ToString().Should().Contain("j < 20");
    }

    [Fact]
    public void ExtractLoops_WithMixedLoopTypes_ExtractsAll()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void ProcessData(int[] data)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] *= 2;
                    }
                    
                    foreach (var item in data)
                    {
                        Console.WriteLine(item);
                    }
                    
                    int j = 0;
                    while (j < 10)
                    {
                        Process(j++);
                    }
                    
                    do
                    {
                        j--;
                    } while (j > 0);
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var loops = MethodBodyExtractor.ExtractLoops(method);
        
        // Assert
        loops.Should().HaveCount(4);
        loops.OfType<ForStatementSyntax>().Should().HaveCount(1);
        loops.OfType<ForEachStatementSyntax>().Should().HaveCount(1);
        loops.OfType<WhileStatementSyntax>().Should().HaveCount(1);
        loops.OfType<DoStatementSyntax>().Should().HaveCount(1);
    }

    [Fact]
    public void ExtractLoops_WithNestedLoops_ExtractsAll()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void ProcessMatrix(int[,] matrix)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            matrix[i, j] = i * j;
                        }
                    }
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var loops = MethodBodyExtractor.ExtractLoops(method);
        
        // Assert
        loops.Should().HaveCount(2);
        loops.Should().AllBeOfType<ForStatementSyntax>();
    }

    [Fact]
    public void ExtractLoops_WithNoLoops_ReturnsEmptyList()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var loops = MethodBodyExtractor.ExtractLoops(method);
        
        // Assert
        loops.Should().BeEmpty();
    }

    [Fact]
    public void ExtractLoops_WithNullMethod_ReturnsEmptyList()
    {
        // Act
        var loops = MethodBodyExtractor.ExtractLoops(null!);
        
        // Assert
        loops.Should().NotBeNull();
        loops.Should().BeEmpty();
    }

    #endregion

    #region ExtractParameters Tests

    [Fact]
    public void ExtractParameters_WithMultipleParameters_ExtractsAll()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void Method(int a, string b, float c, bool d)
                {
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var parameters = MethodBodyExtractor.ExtractParameters(method);
        
        // Assert
        parameters.Should().HaveCount(4);
        parameters[0].Identifier.Text.Should().Be("a");
        parameters[1].Identifier.Text.Should().Be("b");
        parameters[2].Identifier.Text.Should().Be("c");
        parameters[3].Identifier.Text.Should().Be("d");
    }

    [Fact]
    public void ExtractParameters_WithNoParameters_ReturnsEmptyList()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void Method()
                {
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var parameters = MethodBodyExtractor.ExtractParameters(method);
        
        // Assert
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void ExtractParameters_WithModifiers_ExtractsCorrectly()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void Method(ref int a, out string b, in float c, params int[] d)
                {
                    b = """";
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var parameters = MethodBodyExtractor.ExtractParameters(method);
        
        // Assert
        parameters.Should().HaveCount(4);
        parameters[0].Modifiers.Should().Contain(m => m.IsKind(SyntaxKind.RefKeyword));
        parameters[1].Modifiers.Should().Contain(m => m.IsKind(SyntaxKind.OutKeyword));
        parameters[2].Modifiers.Should().Contain(m => m.IsKind(SyntaxKind.InKeyword));
        parameters[3].Modifiers.Should().Contain(m => m.IsKind(SyntaxKind.ParamsKeyword));
    }

    [Fact]
    public void ExtractParameters_WithNullMethod_ReturnsEmptyList()
    {
        // Act
        var parameters = MethodBodyExtractor.ExtractParameters(null!);
        
        // Assert
        parameters.Should().NotBeNull();
        parameters.Should().BeEmpty();
    }

    #endregion

    #region ExtractLocalVariables Tests

    [Fact]
    public void ExtractLocalVariables_WithMultipleDeclarations_ExtractsAll()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void Method()
                {
                    int x = 5;
                    string message = ""Hello"";
                    var data = new float[10];
                    bool flag = true;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var locals = MethodBodyExtractor.ExtractLocalVariables(method);
        
        // Assert
        locals.Should().HaveCount(4);
        locals[0].Declaration.Variables[0].Identifier.Text.Should().Be("x");
        locals[1].Declaration.Variables[0].Identifier.Text.Should().Be("message");
        locals[2].Declaration.Variables[0].Identifier.Text.Should().Be("data");
        locals[3].Declaration.Variables[0].Identifier.Text.Should().Be("flag");
    }

    [Fact]
    public void ExtractLocalVariables_WithMultipleVariablesInOneDeclaration_ExtractsCorrectly()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void Method()
                {
                    int x = 5, y = 10, z = 15;
                    string a, b, c;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var locals = MethodBodyExtractor.ExtractLocalVariables(method);
        
        // Assert
        locals.Should().HaveCount(2);
        locals[0].Declaration.Variables.Should().HaveCount(3);
        locals[1].Declaration.Variables.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractLocalVariables_WithNoLocals_ReturnsEmptyList()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var locals = MethodBodyExtractor.ExtractLocalVariables(method);
        
        // Assert
        locals.Should().BeEmpty();
    }

    [Fact]
    public void ExtractLocalVariables_WithNullMethod_ReturnsEmptyList()
    {
        // Act
        var locals = MethodBodyExtractor.ExtractLocalVariables(null!);
        
        // Assert
        locals.Should().NotBeNull();
        locals.Should().BeEmpty();
    }

    #endregion

    #region ReplaceMethodBody Tests

    [Fact]
    public void ReplaceMethodBody_WithNewBody_ReplacesSuccessfully()
    {
        // Arrange
        var originalCode = @"
            class Test
            {
                public int Method()
                {
                    return 1;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(originalCode);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        var newBody = SyntaxFactory.Block(
            SyntaxFactory.ReturnStatement(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(42))));
        
        // Act
        var modifiedMethod = MethodBodyExtractor.ReplaceMethodBody(method, newBody);
        
        // Assert
        modifiedMethod.Should().NotBeNull();
        modifiedMethod.Body!.ToString().Should().Contain("42");
        modifiedMethod.Body.ToString().Should().NotContain("return 1");
    }

    [Fact]
    public void ReplaceMethodBody_WithNullMethod_ReturnsNull()
    {
        // Arrange
        var newBody = SyntaxFactory.Block();
        
        // Act
        var result = MethodBodyExtractor.ReplaceMethodBody(null!, newBody);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReplaceMethodBody_WithNullBody_RemovesBody()
    {
        // Arrange
        var code = @"
            class Test
            {
                public int Method()
                {
                    return 1;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var modifiedMethod = MethodBodyExtractor.ReplaceMethodBody(method, null!);
        
        // Assert
        modifiedMethod.Should().NotBeNull();
        modifiedMethod.Body.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MethodBodyExtractor_ComplexScenario_WorksCorrectly()
    {
        // Arrange
        var code = @"
            class ComplexClass
            {
                public int ComplexMethod(int[] data, int threshold)
                {
                    if (data == null) throw new ArgumentNullException(nameof(data));
                    
                    int sum = 0;
                    int count = 0;
                    
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i] > threshold)
                        {
                            sum += data[i];
                            count++;
                        }
                    }
                    
                    return count > 0 ? sum / count : 0;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var body = MethodBodyExtractor.ExtractMethodBody(method);
        var parameters = MethodBodyExtractor.ExtractParameters(method);
        var loops = MethodBodyExtractor.ExtractLoops(method);
        var locals = MethodBodyExtractor.ExtractLocalVariables(method);
        var statements = MethodBodyExtractor.ExtractStatements(method);
        
        // Assert
        body.Should().NotBeNull();
        parameters.Should().HaveCount(2);
        loops.Should().HaveCount(1);
        locals.Should().HaveCount(2);
        statements.Should().HaveCountGreaterThan(3);
    }

    [Fact]
    public void MethodBodyExtractor_ModifyAndReplace_WorksCorrectly()
    {
        // Arrange
        var code = @"
            class Test
            {
                public void PrintMessage()
                {
                    Console.WriteLine(""Original"");
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Create new body
        var newStatement = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Console"),
                    SyntaxFactory.IdentifierName("WriteLine")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal("Modified")))))));
        
        var newBody = SyntaxFactory.Block(newStatement);
        
        // Act
        var modifiedMethod = MethodBodyExtractor.ReplaceMethodBody(method, newBody);
        
        // Assert
        modifiedMethod.Should().NotBeNull();
        modifiedMethod.Body!.ToString().Should().Contain("Modified");
        modifiedMethod.Body.ToString().Should().NotContain("Original");
    }

    #endregion
}