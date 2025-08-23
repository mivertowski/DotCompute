using System;
using System.Collections.Generic;
using DotCompute.Generators.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotCompute.Generators.Tests.Utils;

public class ParameterValidatorTests
{
    #region ValidateNotNull Tests

    [Fact]
    public void ValidateNotNull_WithNullParameter_ThrowsArgumentNullException()
    {
        // Arrange
        object? parameter = null;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotNull(parameter, "testParam");
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*testParam*")
            .And.ParamName.Should().Be("testParam");
    }

    [Fact]
    public void ValidateNotNull_WithNonNullParameter_DoesNotThrow()
    {
        // Arrange
        var parameter = new object();
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotNull(parameter, "testParam");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("test")]
    [InlineData("   ")]
    public void ValidateNotNull_WithNonNullString_DoesNotThrow(string value)
    {
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotNull(value, "stringParam");
        act.Should().NotThrow();
    }

    #endregion

    #region ValidateNotEmpty Tests

    [Fact]
    public void ValidateNotEmpty_WithNullString_ThrowsArgumentNullException()
    {
        // Arrange
        string? value = null;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(value!, "testParam");
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*testParam*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t \n ")]
    public void ValidateNotEmpty_WithEmptyOrWhitespaceString_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(value, "testParam");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*testParam*cannot be empty*");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("test")]
    [InlineData(" test ")]
    [InlineData("multi\nline")]
    public void ValidateNotEmpty_WithNonEmptyString_DoesNotThrow(string value)
    {
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(value, "testParam");
        act.Should().NotThrow();
    }

    #endregion

    #region ValidateNotEmpty Collection Tests

    [Fact]
    public void ValidateNotEmpty_WithNullCollection_ThrowsArgumentNullException()
    {
        // Arrange
        List<int>? collection = null;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(collection!, "testParam");
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*testParam*");
    }

    [Fact]
    public void ValidateNotEmpty_WithEmptyCollection_ThrowsArgumentException()
    {
        // Arrange
        var collection = new List<int>();
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(collection, "testParam");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*testParam*cannot be empty*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1, 2, 3)]
    [InlineData(1, 2, 3, 4, 5)]
    public void ValidateNotEmpty_WithNonEmptyCollection_DoesNotThrow(params int[] values)
    {
        // Arrange
        var collection = new List<int>(values);
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(collection, "testParam");
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateNotEmpty_WithDifferentCollectionTypes_WorksCorrectly()
    {
        // Arrange
        var array = new[] { 1, 2, 3 };
        var hashSet = new HashSet<string> { "a", "b" };
        var dictionary = new Dictionary<int, string> { { 1, "one" } };
        
        // Act & Assert
        Action actArray = () => ParameterValidator.ValidateNotEmpty(array, "array");
        Action actHashSet = () => ParameterValidator.ValidateNotEmpty(hashSet, "hashSet");
        Action actDictionary = () => ParameterValidator.ValidateNotEmpty(dictionary, "dictionary");
        
        actArray.Should().NotThrow();
        actHashSet.Should().NotThrow();
        actDictionary.Should().NotThrow();
    }

    #endregion

    #region ValidateType Tests

    [Fact]
    public void ValidateType_WithNullObject_ThrowsArgumentNullException()
    {
        // Arrange
        object? obj = null;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateType<string>(obj!, "testParam");
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*testParam*");
    }

    [Fact]
    public void ValidateType_WithIncorrectType_ThrowsArgumentException()
    {
        // Arrange
        object obj = 123;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateType<string>(obj, "testParam");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*testParam*must be of type*String*");
    }

    [Fact]
    public void ValidateType_WithCorrectType_ReturnsTypedObject()
    {
        // Arrange
        object obj = "test string";
        
        // Act
        var result = ParameterValidator.ValidateType<string>(obj, "testParam");
        
        // Assert
        result.Should().Be("test string");
    }

    [Fact]
    public void ValidateType_WithDerivedType_AcceptsIt()
    {
        // Arrange
        object obj = new ArgumentNullException("test");
        
        // Act
        var result = ParameterValidator.ValidateType<Exception>(obj, "testParam");
        
        // Assert
        result.Should().BeOfType<ArgumentNullException>();
    }

    #endregion

    #region ValidateSyntaxNode Tests

    [Fact]
    public void ValidateSyntaxNode_WithNullNode_ThrowsArgumentNullException()
    {
        // Arrange
        SyntaxNode? node = null;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateSyntaxNode<MethodDeclarationSyntax>(node!, "testNode");
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*testNode*");
    }

    [Fact]
    public void ValidateSyntaxNode_WithIncorrectNodeType_ThrowsArgumentException()
    {
        // Arrange
        var tree = CSharpSyntaxTree.ParseText("class Test { }");
        var root = tree.GetRoot();
        var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateSyntaxNode<MethodDeclarationSyntax>(classNode, "testNode");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*testNode*must be of type*MethodDeclarationSyntax*");
    }

    [Fact]
    public void ValidateSyntaxNode_WithCorrectNodeType_ReturnsTypedNode()
    {
        // Arrange
        var tree = CSharpSyntaxTree.ParseText("class Test { void Method() { } }");
        var root = tree.GetRoot();
        var methodNode = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        
        // Act
        var result = ParameterValidator.ValidateSyntaxNode<MethodDeclarationSyntax>(methodNode, "testNode");
        
        // Assert
        result.Should().BeSameAs(methodNode);
        result.Identifier.Text.Should().Be("Method");
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ValidateNotNull_WithEmptyParameterName_StillIncludesItInException()
    {
        // Arrange
        object? parameter = null;
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotNull(parameter, "");
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("");
    }

    [Fact]
    public void ValidateNotEmpty_WithCollectionContainingNulls_DoesNotThrow()
    {
        // Arrange
        var collection = new List<string?> { null, null, null };
        
        // Act & Assert
        Action act = () => ParameterValidator.ValidateNotEmpty(collection, "testParam");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(List<int>))]
    public void ValidateType_WithValueTypes_WorksCorrectly(Type expectedType)
    {
        // Arrange
        object obj = expectedType == typeof(int) ? 42 :
                    expectedType == typeof(string) ? "test" :
                    new List<int> { 1, 2, 3 };
        
        // Act & Assert
        Action act = () =>
        {
            var method = typeof(ParameterValidator)
                .GetMethod(nameof(ParameterValidator.ValidateType))!
                .MakeGenericMethod(expectedType);
            method.Invoke(null, new[] { obj, "testParam" });
        };
        
        act.Should().NotThrow();
    }

    #endregion
}