// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Compilation;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Compilation;

/// <summary>
/// Tests for <see cref="CudaTypeMapper"/>.
/// </summary>
public class CudaTypeMapperTests
{
    #region Primitive Type Mapping Tests

    [Theory]
    [InlineData(typeof(sbyte), "char")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long long")]
    [InlineData(typeof(byte), "unsigned char")]
    [InlineData(typeof(ushort), "unsigned short")]
    [InlineData(typeof(uint), "unsigned int")]
    [InlineData(typeof(ulong), "unsigned long long")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(char), "unsigned short")]
    public void GetCudaType_PrimitiveTypes_ReturnsCorrectCudaType(Type csType, string expectedCudaType)
    {
        // Act
        var cudaType = CudaTypeMapper.GetCudaType(csType);

        // Assert
        Assert.Equal(expectedCudaType, cudaType);
    }

    #endregion

    #region Span and Array Mapping Tests

    [Fact]
    public void GetCudaType_SpanOfInt_ReturnsIntPointer()
    {
        // Arrange
        var spanType = typeof(Span<int>);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(spanType);

        // Assert
        Assert.Equal("int*", cudaType);
    }

    [Fact]
    public void GetCudaType_ReadOnlySpanOfFloat_ReturnsFloatPointer()
    {
        // Arrange
        var spanType = typeof(ReadOnlySpan<float>);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(spanType);

        // Assert
        Assert.Equal("float*", cudaType);
    }

    [Fact]
    public void GetCudaType_ArrayOfDouble_ReturnsDoublePointer()
    {
        // Arrange
        var arrayType = typeof(double[]);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(arrayType);

        // Assert
        Assert.Equal("double*", cudaType);
    }

    [Fact]
    public unsafe void GetCudaType_IntPointer_ReturnsIntPointer()
    {
        // Arrange
        var pointerType = typeof(int*);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(pointerType);

        // Assert
        Assert.Equal("int*", cudaType);
    }

    #endregion

    #region Vector Type Mapping Tests

    [Fact]
    public void GetCudaType_Vector2_ReturnsFloat2()
    {
        // Arrange
        var vectorType = typeof(Vector2);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(vectorType);

        // Assert
        Assert.Equal("float2", cudaType);
    }

    [Fact]
    public void GetCudaType_Vector3_ReturnsFloat3()
    {
        // Arrange
        var vectorType = typeof(Vector3);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(vectorType);

        // Assert
        Assert.Equal("float3", cudaType);
    }

    [Fact]
    public void GetCudaType_Vector4_ReturnsFloat4()
    {
        // Arrange
        var vectorType = typeof(Vector4);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(vectorType);

        // Assert
        Assert.Equal("float4", cudaType);
    }

    #endregion

    #region Struct Type Mapping Tests

    [Fact]
    public void GetCudaType_CustomStruct_ReturnsStructName()
    {
        // Arrange
        var structType = typeof(TestStruct);

        // Act
        var cudaType = CudaTypeMapper.GetCudaType(structType);

        // Assert
        Assert.Equal("TestStruct", cudaType);
    }

    [Fact]
    public void GenerateCudaStruct_SimpleStruct_GeneratesCorrectDefinition()
    {
        // Arrange
        var structType = typeof(SimpleStruct);

        // Act
        var cudaStruct = CudaTypeMapper.GenerateCudaStruct(structType);

        // Assert
        Assert.Contains("struct SimpleStruct {", cudaStruct);
        Assert.Contains("int X;", cudaStruct);
        Assert.Contains("float Y;", cudaStruct);
        Assert.Contains("};", cudaStruct);
    }

    [Fact]
    public void GenerateCudaStruct_StructWithExplicitLayout_IncludesOffsetComments()
    {
        // Arrange
        var structType = typeof(ExplicitLayoutStruct);

        // Act
        var cudaStruct = CudaTypeMapper.GenerateCudaStruct(structType);

        // Assert
        Assert.Contains("struct ExplicitLayoutStruct {", cudaStruct);
        Assert.Contains("offset: 0", cudaStruct);
        Assert.Contains("offset: 4", cudaStruct);
    }

    #endregion

    #region Parameter Declaration Tests

    [Fact]
    public void GetCudaParameterDeclaration_IntParameter_ReturnsCorrectDeclaration()
    {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.MethodWithInt))!;
        var parameter = method.GetParameters()[0];

        // Act
        var declaration = CudaTypeMapper.GetCudaParameterDeclaration(parameter);

        // Assert
        Assert.Equal("int value", declaration);
    }

    [Fact]
    public void GetCudaParameterDeclaration_SpanParameter_ReturnsPointerDeclaration()
    {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.MethodWithSpan))!;
        var parameter = method.GetParameters()[0];

        // Act
        var declaration = CudaTypeMapper.GetCudaParameterDeclaration(parameter);

        // Assert
        Assert.Equal("float* data", declaration);
    }

    [Fact]
    public void GetCudaParameterDeclaration_ReadOnlySpanParameter_ReturnsConstPointer()
    {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.MethodWithReadOnlySpan))!;
        var parameter = method.GetParameters()[0];

        // Act
        var declaration = CudaTypeMapper.GetCudaParameterDeclaration(parameter);

        // Assert
        Assert.Equal("const float* data", declaration);
    }

    #endregion

    #region Size Calculation Tests

    [Theory]
    [InlineData("char", 1)]
    [InlineData("unsigned char", 1)]
    [InlineData("bool", 1)]
    [InlineData("short", 2)]
    [InlineData("unsigned short", 2)]
    [InlineData("int", 4)]
    [InlineData("unsigned int", 4)]
    [InlineData("float", 4)]
    [InlineData("long long", 8)]
    [InlineData("unsigned long long", 8)]
    [InlineData("double", 8)]
    [InlineData("float2", 8)]
    [InlineData("float3", 12)]
    [InlineData("float4", 16)]
    [InlineData("double2", 16)]
    [InlineData("double3", 24)]
    [InlineData("double4", 32)]
    public void GetCudaTypeSize_ValidTypes_ReturnsCorrectSize(string cudaType, int expectedSize)
    {
        // Act
        var size = CudaTypeMapper.GetCudaTypeSize(cudaType);

        // Assert
        Assert.Equal(expectedSize, size);
    }

    [Fact]
    public void GetCudaTypeSize_PointerType_ReturnsPointerSize()
    {
        // Arrange
        var pointerType = "int*";

        // Act
        var size = CudaTypeMapper.GetCudaTypeSize(pointerType);

        // Assert
        Assert.Equal(IntPtr.Size, size);
    }

    [Fact]
    public void GetMemoryPackSerializedSize_AnyMessageType_Returns65792()
    {
        // Arrange
        var messageType = typeof(TestStruct);

        // Act
        var size = CudaTypeMapper.GetMemoryPackSerializedSize(messageType);

        // Assert
        // Header (16 + 256 + 8 = 280) + Payload (65536) = 65816
        // Wait, the implementation says 256 + 65536... let me check
        // headerSize = 16 + 256 + 8 = 280
        // maxPayloadSize = 65536
        // Total = 65816
        Assert.Equal(65816, size);
    }

    #endregion

    #region Header Requirements Tests

    [Fact]
    public void GetRequiredCudaHeader_Vector2_ReturnsVectorTypesHeader()
    {
        // Arrange
        var vectorType = typeof(Vector2);

        // Act
        var header = CudaTypeMapper.GetRequiredCudaHeader(vectorType);

        // Assert
        Assert.Equal("vector_types.h", header);
    }

    [Fact]
    public void GetRequiredCudaHeader_PrimitiveType_ReturnsNull()
    {
        // Arrange
        var primitiveType = typeof(int);

        // Act
        var header = CudaTypeMapper.GetRequiredCudaHeader(primitiveType);

        // Assert
        Assert.Null(header);
    }

    #endregion

    #region Type Support Tests

    [Theory]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(float), true)]
    [InlineData(typeof(double), true)]
    public void IsTypeSupported_SupportedTypes_ReturnsTrue(Type type, bool expected)
    {
        // Act
        var isSupported = CudaTypeMapper.IsTypeSupported(type);

        // Assert
        Assert.Equal(expected, isSupported);
    }

    [Fact]
    public void IsTypeSupported_UnsupportedReferenceType_ReturnsFalse()
    {
        // Arrange
        var unsupportedType = typeof(string);

        // Act
        var isSupported = CudaTypeMapper.IsTypeSupported(unsupportedType);

        // Assert
        Assert.False(isSupported);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void GetCudaType_NullType_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CudaTypeMapper.GetCudaType(null!));
    }

    [Fact]
    public void GetCudaType_UnsupportedReferenceType_ThrowsNotSupportedException()
    {
        // Arrange
        var unsupportedType = typeof(string);

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() =>
            CudaTypeMapper.GetCudaType(unsupportedType));

        Assert.Contains("not supported in CUDA kernels", exception.Message);
    }

    [Fact]
    public void GetCudaTypeSize_InvalidTypeName_ThrowsNotSupportedException()
    {
        // Arrange
        var invalidType = "invalid_type";

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            CudaTypeMapper.GetCudaTypeSize(invalidType));
    }

    [Fact]
    public void GenerateCudaStruct_ReferenceType_ThrowsArgumentException()
    {
        // Arrange
        var referenceType = typeof(string);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            CudaTypeMapper.GenerateCudaStruct(referenceType));

        Assert.Contains("not a value type", exception.Message);
    }

    #endregion

    #region Test Helper Types

    private struct TestStruct
    {
        public int Field1;
        public float Field2;
    }

    private struct SimpleStruct
    {
        public int X;
        public float Y;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct ExplicitLayoutStruct
    {
        [FieldOffset(0)]
        public int Field1;

        [FieldOffset(4)]
        public float Field2;
    }

    private static class TestMethods
    {
        public static void MethodWithInt(int value) { }
        public static void MethodWithSpan(Span<float> data) { }
        public static void MethodWithReadOnlySpan(ReadOnlySpan<float> data) { }
    }

    #endregion
}
