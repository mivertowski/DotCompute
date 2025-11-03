// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests.Compilation;

/// <summary>
/// Comprehensive unit tests for TypeInferenceEngine.
/// Tests type inference, SIMD capability detection, compatibility checking, and validation.
/// ~30 tests covering all major code paths.
/// </summary>
public sealed class TypeInferenceEngineTests
{
    private readonly TypeInferenceEngine _engine = new();

    #region Type Inference Tests (8 tests)

    [Fact]
    public void InferType_IntExpression_ReturnsIntTypeInfo()
    {
        // Arrange
        Expression<Func<int>> expr = () => 42;

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Should().NotBeNull();
        typeInfo.Type.Should().Be(typeof(int));
        typeInfo.IsSimdCapable.Should().BeTrue();
        typeInfo.IsNullable.Should().BeFalse();
        typeInfo.RequiresUnsafe.Should().BeFalse();
        typeInfo.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void InferType_NullableIntExpression_ReturnsNullableIntTypeInfo()
    {
        // Arrange
        Expression<Func<int?>> expr = () => (int?)42;

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(int?));
        typeInfo.IsNullable.Should().BeTrue();
        typeInfo.IsSimdCapable.Should().BeTrue(); // Underlying type is SIMD-capable
    }

    [Fact]
    public void InferType_NullableDoubleExpression_ReturnsNullableDoubleTypeInfo()
    {
        // Arrange
        Expression<Func<double?>> expr = () => (double?)3.14;

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(double?));
        typeInfo.IsNullable.Should().BeTrue();
        typeInfo.IsSimdCapable.Should().BeTrue();
    }

    [Fact]
    public void InferType_ListOfIntExpression_ReturnsCollectionTypeInfo()
    {
        // Arrange
        Expression<Func<List<int>>> expr = () => new List<int>();

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(List<int>));
        typeInfo.IsCollection.Should().BeTrue();
        typeInfo.ElementType.Should().Be(typeof(int));
        typeInfo.GenericArguments.Should().ContainSingle().Which.Should().Be(typeof(int));
    }

    [Fact]
    public void InferType_IntArrayExpression_ReturnsArrayTypeInfo()
    {
        // Arrange
        Expression<Func<int[]>> expr = () => new int[] { 1, 2, 3 };

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(int[]));
        typeInfo.IsCollection.Should().BeTrue();
        typeInfo.ElementType.Should().Be(typeof(int));
    }

    [Fact]
    public void InferType_GenericFuncExpression_ReturnsGenericTypeInfo()
    {
        // Arrange
        Expression<Func<Func<int, string>>> expr = () => x => x.ToString();

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(Func<int, string>));
        typeInfo.GenericArguments.Should().HaveCount(2);
        typeInfo.GenericArguments[0].Should().Be(typeof(int));
        typeInfo.GenericArguments[1].Should().Be(typeof(string));
    }

    [Fact]
    public void InferType_CustomClassExpression_ReturnsClassTypeInfo()
    {
        // Arrange
        Expression<Func<CustomTestClass>> expr = () => new CustomTestClass();

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(CustomTestClass));
        typeInfo.IsSimdCapable.Should().BeFalse();
        typeInfo.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void InferType_StructExpression_ReturnsStructTypeInfo()
    {
        // Arrange
        Expression<Func<CustomTestStruct>> expr = () => new CustomTestStruct { Value = 42 };

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.Type.Should().Be(typeof(CustomTestStruct));
        typeInfo.Type.IsValueType.Should().BeTrue();
    }

    #endregion

    #region Element Type Inference Tests (5 tests)

    [Fact]
    public void InferElementType_IEnumerableOfInt_ReturnsInt()
    {
        // Act
        var elementType = _engine.InferElementType(typeof(IEnumerable<int>));

        // Assert
        elementType.Should().Be(typeof(int));
    }

    [Fact]
    public void InferElementType_ListOfString_ReturnsString()
    {
        // Act
        var elementType = _engine.InferElementType(typeof(List<string>));

        // Assert
        elementType.Should().Be(typeof(string));
    }

    [Fact]
    public void InferElementType_ArrayOfDouble_ReturnsDouble()
    {
        // Act
        var elementType = _engine.InferElementType(typeof(double[]));

        // Assert
        elementType.Should().Be(typeof(double));
    }

    [Fact]
    public void InferElementType_NestedCollection_ReturnsInnerCollection()
    {
        // Act
        var elementType = _engine.InferElementType(typeof(List<List<int>>));

        // Assert
        elementType.Should().Be(typeof(List<int>));
    }

    [Fact]
    public void InferElementType_NonCollectionType_ReturnsNull()
    {
        // Act
        var elementType = _engine.InferElementType(typeof(int));

        // Assert
        elementType.Should().BeNull();
    }

    #endregion

    #region SIMD Capability Detection Tests (5 tests)

    [Theory]
    [InlineData(typeof(byte), true)]
    [InlineData(typeof(sbyte), true)]
    [InlineData(typeof(short), true)]
    [InlineData(typeof(ushort), true)]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(uint), true)]
    [InlineData(typeof(long), true)]
    [InlineData(typeof(ulong), true)]
    [InlineData(typeof(float), true)]
    [InlineData(typeof(double), true)]
    public void InferType_SimdCapableTypes_CorrectlyDetectsSIMDCapability(Type type, bool expectedSimdCapable)
    {
        // Arrange
        var parameter = Expression.Parameter(type);
        var expr = Expression.Constant(Activator.CreateInstance(type), type);

        // Act
        var typeInfo = _engine.InferType(expr);

        // Assert
        typeInfo.IsSimdCapable.Should().Be(expectedSimdCapable);
    }

    [Theory]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(char), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(decimal), false)]
    [InlineData(typeof(DateTime), false)]
    public void InferType_NonSimdTypes_CorrectlyDetectsNoSIMDCapability(Type type, bool expectedSimdCapable)
    {
        // Arrange
        var expr = Expression.Default(type);

        // Act
        var typeInfo = _engine.InferType(expr);

        // Assert
        typeInfo.IsSimdCapable.Should().Be(expectedSimdCapable);
    }

    [Fact]
    public void InferType_NullableSimdType_DetectsSIMDCapability()
    {
        // Arrange
        Expression<Func<int?>> expr = () => (int?)42;

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.IsSimdCapable.Should().BeTrue();
        typeInfo.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void InferType_CollectionOfSimdType_DetectsCollection()
    {
        // Arrange
        Expression<Func<List<float>>> expr = () => new List<float>();

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.IsCollection.Should().BeTrue();
        typeInfo.ElementType.Should().Be(typeof(float));
    }

    [Fact]
    public void InferType_CustomClass_NotSimdCapable()
    {
        // Arrange
        Expression<Func<CustomTestClass>> expr = () => new CustomTestClass();

        // Act
        var typeInfo = _engine.InferType(expr.Body);

        // Assert
        typeInfo.IsSimdCapable.Should().BeFalse();
    }

    #endregion

    #region Type Compatibility Tests (8 tests)

    [Fact]
    public void AreCompatible_ExactTypeMatch_ReturnsTrue()
    {
        // Arrange
        var left = new TypeInfo(typeof(int)) { IsSimdCapable = true };
        var right = new TypeInfo(typeof(int)) { IsSimdCapable = true };

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreCompatible_NullableWithNonNullable_ReturnsTrue()
    {
        // Arrange
        var left = new TypeInfo(typeof(int?)) { IsNullable = true };
        var right = new TypeInfo(typeof(int));

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreCompatible_IntToLong_ReturnsTrue()
    {
        // Arrange (int can be safely converted to long)
        var left = new TypeInfo(typeof(long));
        var right = new TypeInfo(typeof(int));

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreCompatible_FloatToDouble_ReturnsTrue()
    {
        // Arrange (float can be safely converted to double)
        var left = new TypeInfo(typeof(double));
        var right = new TypeInfo(typeof(float));

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreCompatible_InheritanceCompatibility_ReturnsTrue()
    {
        // Arrange
        var left = new TypeInfo(typeof(object));
        var right = new TypeInfo(typeof(string));

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreCompatible_InterfaceCompatibility_ReturnsTrue()
    {
        // Arrange
        var left = new TypeInfo(typeof(IEnumerable<int>));
        var right = new TypeInfo(typeof(List<int>));

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreCompatible_IncompatibleTypes_ReturnsFalse()
    {
        // Arrange
        var left = new TypeInfo(typeof(int));
        var right = new TypeInfo(typeof(string));

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreCompatible_CollectionCompatibility_ReturnsTrue()
    {
        // Arrange
        var left = new TypeInfo(typeof(IEnumerable<int>)) { IsCollection = true, ElementType = typeof(int) };
        var right = new TypeInfo(typeof(IEnumerable<int>)) { IsCollection = true, ElementType = typeof(int) };

        // Act
        var result = _engine.AreCompatible(left, right);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Type Validation Tests (4 tests)

    [Fact]
    public void ValidateTypes_ValidGraph_ReturnsTrue()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "op1",
                    Type = OperationType.Map,
                    Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                    {
                        ["InputType"] = typeof(int),
                        ["OutputType"] = typeof(double)
                    })
                }
            }
        };

        // Act
        var result = _engine.ValidateTypes(graph, out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTypes_InvalidWherePredicateNonBool_ReturnsFalse()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "filter1",
                    Type = OperationType.Filter,
                    Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                    {
                        ["InputType"] = typeof(int),
                        ["OutputType"] = typeof(int) // Should be bool!
                    })
                }
            }
        };

        // Act
        var result = _engine.ValidateTypes(graph, out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().ContainSingle();
        errors[0].Should().Contain("Where predicate");
        errors[0].Should().Contain("bool");
    }

    [Fact]
    public void ValidateTypes_MapOperationWithValidTypes_Succeeds()
    {
        // Arrange - Map operation with compatible types
        var metadata = new Dictionary<string, object>
        {
            ["InputType"] = typeof(int),
            ["OutputType"] = typeof(string)
        };

        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "map1",
                    Type = OperationType.Map,
                    Metadata = new ReadOnlyDictionary<string, object>(metadata)
                }
            }
        };

        // Act
        var result = _engine.ValidateTypes(graph, out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTypes_IncompatibleAggregateTypes_ReturnsFalse()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "aggregate1",
                    Type = OperationType.Aggregate,
                    Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                    {
                        ["InputType"] = typeof(int),
                        ["OutputType"] = typeof(string) // Incompatible accumulator type
                    })
                }
            }
        };

        // Act
        var result = _engine.ValidateTypes(graph, out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().ContainSingle();
        errors[0].Should().Contain("Aggregate");
        errors[0].Should().Contain("compatible");
    }

    #endregion

    #region InferTypes Method Tests (TypeMetadata)

    [Fact]
    public void InferTypes_BasicGraph_ReturnsTypeMetadata()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "op1",
                    Type = OperationType.Map,
                    Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                    {
                        ["InputType"] = typeof(int),
                        ["OutputType"] = typeof(double)
                    })
                }
            }
        };

        // Act
        var metadata = _engine.InferTypes(graph, typeof(int), typeof(double));

        // Assert
        metadata.Should().NotBeNull();
        metadata.InputType.Should().Be(typeof(int));
        metadata.ResultType.Should().Be(typeof(double));
        metadata.IntermediateTypes.Should().ContainKey("op1_input");
        metadata.IntermediateTypes.Should().ContainKey("op1_output");
        metadata.IsSimdCompatible.Should().BeTrue();
        metadata.RequiresUnsafe.Should().BeFalse();
        metadata.HasNullableTypes.Should().BeFalse();
    }

    [Fact]
    public unsafe void InferTypes_WithUnsafeType_SetsRequiresUnsafe()
    {
        // Arrange
        var pointerType = typeof(int).MakePointerType();

        var operationMetadata = new Dictionary<string, object>
        {
            ["InputType"] = pointerType,
            ["OutputType"] = typeof(int)
        };

        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "op1",
                    Type = OperationType.Map,
                    Metadata = new ReadOnlyDictionary<string, object>(operationMetadata)
                }
            }
        };

        // Act
        var metadata = _engine.InferTypes(graph, typeof(int), typeof(int));

        // Assert
        metadata.RequiresUnsafe.Should().BeTrue();
    }

    [Fact]
    public void InferTypes_WithNullableType_SetsHasNullableTypes()
    {
        // Arrange
        var graph = new OperationGraph
        {
            Operations = new Collection<Operation>
            {
                new Operation
                {
                    Id = "op1",
                    Type = OperationType.Map,
                    Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                    {
                        ["InputType"] = typeof(int?),
                        ["OutputType"] = typeof(double)
                    })
                }
            }
        };

        // Act
        var metadata = _engine.InferTypes(graph, typeof(int), typeof(double));

        // Assert
        metadata.HasNullableTypes.Should().BeTrue();
    }

    #endregion

    #region ResolveLambdaTypes Tests

    [Fact]
    public void ResolveLambdaTypes_SimpleLambda_ReturnsTypeInfo()
    {
        // Arrange
        Expression<Func<int, int>> lambda = x => x * 2;

        // Act
        var typeInfo = _engine.ResolveLambdaTypes(lambda);

        // Assert
        typeInfo.Should().NotBeNull();
        typeInfo.Type.Should().Be(typeof(int));
    }

    [Fact]
    public void ResolveLambdaTypes_LambdaWithTransformation_ReturnsOutputTypeInfo()
    {
        // Arrange
        Expression<Func<int, string>> lambda = x => x.ToString();

        // Act
        var typeInfo = _engine.ResolveLambdaTypes(lambda);

        // Assert
        typeInfo.Type.Should().Be(typeof(string));
    }

    #endregion

    #region Helper Classes

    private class CustomTestClass
    {
        public int Value { get; set; }
    }

    private struct CustomTestStruct
    {
        public int Value { get; set; }
    }

    #endregion
}
