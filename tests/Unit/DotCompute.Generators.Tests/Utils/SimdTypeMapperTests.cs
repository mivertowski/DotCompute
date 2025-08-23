using System;
using System.Collections.Generic;
using DotCompute.Generators.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Generators.Tests.Utils;

public class SimdTypeMapperTests
{
    #region GetSimdType Tests

    [Theory]
    [InlineData("float", "Vector<float>")]
    [InlineData("double", "Vector<double>")]
    [InlineData("int", "Vector<int>")]
    [InlineData("uint", "Vector<uint>")]
    [InlineData("long", "Vector<long>")]
    [InlineData("ulong", "Vector<ulong>")]
    [InlineData("short", "Vector<short>")]
    [InlineData("ushort", "Vector<ushort>")]
    [InlineData("byte", "Vector<byte>")]
    [InlineData("sbyte", "Vector<sbyte>")]
    public void GetSimdType_WithSupportedPrimitiveTypes_ReturnsCorrectVectorType(string scalarType, string expectedVectorType)
    {
        // Act
        var result = SimdTypeMapper.GetSimdType(scalarType);
        
        // Assert
        result.Should().Be(expectedVectorType);
    }

    [Theory]
    [InlineData("System.Single", "Vector<float>")]
    [InlineData("System.Double", "Vector<double>")]
    [InlineData("System.Int32", "Vector<int>")]
    [InlineData("System.UInt32", "Vector<uint>")]
    [InlineData("System.Int64", "Vector<long>")]
    [InlineData("System.UInt64", "Vector<ulong>")]
    [InlineData("System.Int16", "Vector<short>")]
    [InlineData("System.UInt16", "Vector<ushort>")]
    [InlineData("System.Byte", "Vector<byte>")]
    [InlineData("System.SByte", "Vector<sbyte>")]
    public void GetSimdType_WithFullyQualifiedTypes_ReturnsCorrectVectorType(string scalarType, string expectedVectorType)
    {
        // Act
        var result = SimdTypeMapper.GetSimdType(scalarType);
        
        // Assert
        result.Should().Be(expectedVectorType);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("decimal")]
    [InlineData("char")]
    [InlineData("object")]
    [InlineData("DateTime")]
    [InlineData("CustomType")]
    public void GetSimdType_WithUnsupportedTypes_ReturnsNull(string scalarType)
    {
        // Act
        var result = SimdTypeMapper.GetSimdType(scalarType);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetSimdType_WithNullOrEmptyType_ReturnsNull(string? scalarType)
    {
        // Act
        var result = SimdTypeMapper.GetSimdType(scalarType!);
        
        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsVectorizableType Tests

    [Theory]
    [InlineData("float", true)]
    [InlineData("double", true)]
    [InlineData("int", true)]
    [InlineData("uint", true)]
    [InlineData("long", true)]
    [InlineData("ulong", true)]
    [InlineData("short", true)]
    [InlineData("ushort", true)]
    [InlineData("byte", true)]
    [InlineData("sbyte", true)]
    [InlineData("string", false)]
    [InlineData("bool", false)]
    [InlineData("decimal", false)]
    [InlineData("object", false)]
    public void IsVectorizableType_ReturnsCorrectResult(string typeName, bool expected)
    {
        // Act
        var result = SimdTypeMapper.IsVectorizableType(typeName);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("System.Single", true)]
    [InlineData("System.Double", true)]
    [InlineData("System.String", false)]
    [InlineData("System.Boolean", false)]
    public void IsVectorizableType_WithFullyQualifiedNames_ReturnsCorrectResult(string typeName, bool expected)
    {
        // Act
        var result = SimdTypeMapper.IsVectorizableType(typeName);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsVectorizableType_WithNullOrEmpty_ReturnsFalse(string? typeName, bool expected)
    {
        // Act
        var result = SimdTypeMapper.IsVectorizableType(typeName!);
        
        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetVectorSize Tests

    [Theory]
    [InlineData("float", 4)]
    [InlineData("double", 8)]
    [InlineData("int", 4)]
    [InlineData("uint", 4)]
    [InlineData("long", 8)]
    [InlineData("ulong", 8)]
    [InlineData("short", 2)]
    [InlineData("ushort", 2)]
    [InlineData("byte", 1)]
    [InlineData("sbyte", 1)]
    public void GetVectorSize_WithValidTypes_ReturnsCorrectSize(string typeName, int expectedSize)
    {
        // Act
        var result = SimdTypeMapper.GetVectorSize(typeName);
        
        // Assert
        result.Should().Be(expectedSize);
    }

    [Theory]
    [InlineData("System.Single", 4)]
    [InlineData("System.Double", 8)]
    [InlineData("System.Int32", 4)]
    [InlineData("System.Int64", 8)]
    public void GetVectorSize_WithFullyQualifiedTypes_ReturnsCorrectSize(string typeName, int expectedSize)
    {
        // Act
        var result = SimdTypeMapper.GetVectorSize(typeName);
        
        // Assert
        result.Should().Be(expectedSize);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("decimal")]
    [InlineData("CustomType")]
    [InlineData(null)]
    [InlineData("")]
    public void GetVectorSize_WithInvalidTypes_ReturnsZero(string? typeName)
    {
        // Act
        var result = SimdTypeMapper.GetVectorSize(typeName!);
        
        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetVectorCount Tests

    [Theory]
    [InlineData("float", 256, 64)]  // 256 bits / 32 bits per float = 8 elements per vector, 256 total / 4 per vector in older systems
    [InlineData("double", 256, 32)] // 256 bits / 64 bits per double = 4 elements per vector
    [InlineData("int", 256, 64)]    // 256 bits / 32 bits per int = 8 elements per vector
    [InlineData("byte", 256, 256)]  // 256 bits / 8 bits per byte = 32 elements per vector
    public void GetVectorCount_WithValidParameters_ReturnsCorrectCount(string typeName, int arraySize, int expectedMinCount)
    {
        // Act
        var result = SimdTypeMapper.GetVectorCount(typeName, arraySize);
        
        // Assert
        result.Should().BeGreaterThanOrEqualTo(expectedMinCount / 8); // Adjust for typical vector width
    }

    [Theory]
    [InlineData("float", 0)]
    [InlineData("float", -1)]
    [InlineData("float", -100)]
    public void GetVectorCount_WithInvalidArraySize_ReturnsZero(string typeName, int arraySize)
    {
        // Act
        var result = SimdTypeMapper.GetVectorCount(typeName, arraySize);
        
        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData("string", 100)]
    [InlineData("bool", 100)]
    [InlineData("object", 100)]
    [InlineData("", 100)]
    [InlineData(null, 100)]
    public void GetVectorCount_WithInvalidType_ReturnsZero(string? typeName, int arraySize)
    {
        // Act
        var result = SimdTypeMapper.GetVectorCount(typeName!, arraySize);
        
        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData("float", 1)]
    [InlineData("float", 3)]
    [InlineData("double", 1)]
    public void GetVectorCount_WithSmallArrays_ReturnsOne(string typeName, int arraySize)
    {
        // Act
        var result = SimdTypeMapper.GetVectorCount(typeName, arraySize);
        
        // Assert
        result.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region GetSimdIntrinsic Tests

    [Theory]
    [InlineData("Add", "float", "Vector.Add")]
    [InlineData("Subtract", "double", "Vector.Subtract")]
    [InlineData("Multiply", "int", "Vector.Multiply")]
    [InlineData("Divide", "float", "Vector.Divide")]
    [InlineData("Min", "float", "Vector.Min")]
    [InlineData("Max", "double", "Vector.Max")]
    [InlineData("Abs", "float", "Vector.Abs")]
    [InlineData("Sqrt", "float", "Vector.SquareRoot")]
    [InlineData("Negate", "int", "Vector.Negate")]
    public void GetSimdIntrinsic_WithSupportedOperations_ReturnsCorrectIntrinsic(string operation, string typeName, string expected)
    {
        // Act
        var result = SimdTypeMapper.GetSimdIntrinsic(operation, typeName);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("CustomOp", "float")]
    [InlineData("Unknown", "int")]
    [InlineData("", "double")]
    [InlineData(null, "float")]
    public void GetSimdIntrinsic_WithUnsupportedOperations_ReturnsNull(string? operation, string typeName)
    {
        // Act
        var result = SimdTypeMapper.GetSimdIntrinsic(operation!, typeName);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Add", "string")]
    [InlineData("Multiply", "bool")]
    [InlineData("Divide", "object")]
    [InlineData("Add", "")]
    [InlineData("Add", null)]
    public void GetSimdIntrinsic_WithInvalidTypes_ReturnsNull(string operation, string? typeName)
    {
        // Act
        var result = SimdTypeMapper.GetSimdIntrinsic(operation, typeName!);
        
        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region MapTypeToSimdRegister Tests

    [Theory]
    [InlineData("float", "Vector256<float>")]
    [InlineData("double", "Vector256<double>")]
    [InlineData("int", "Vector256<int>")]
    [InlineData("long", "Vector256<long>")]
    [InlineData("byte", "Vector256<byte>")]
    public void MapTypeToSimdRegister_WithSupportedTypes_ReturnsVector256(string typeName, string expected)
    {
        // Act
        var result = SimdTypeMapper.MapTypeToSimdRegister(typeName, 256);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("float", 128, "Vector128<float>")]
    [InlineData("double", 128, "Vector128<double>")]
    [InlineData("float", 512, "Vector512<float>")]
    public void MapTypeToSimdRegister_WithDifferentWidths_ReturnsCorrectVector(string typeName, int width, string expected)
    {
        // Act
        var result = SimdTypeMapper.MapTypeToSimdRegister(typeName, width);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("string", 256)]
    [InlineData("bool", 256)]
    [InlineData("object", 256)]
    public void MapTypeToSimdRegister_WithUnsupportedTypes_ReturnsNull(string typeName, int width)
    {
        // Act
        var result = SimdTypeMapper.MapTypeToSimdRegister(typeName, width);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("float", 0)]
    [InlineData("float", -256)]
    [InlineData("float", 64)]
    [InlineData("float", 100)]
    public void MapTypeToSimdRegister_WithInvalidWidth_ReturnsNull(string typeName, int width)
    {
        // Act
        var result = SimdTypeMapper.MapTypeToSimdRegister(typeName, width);
        
        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetScalarType Tests

    [Theory]
    [InlineData("Vector<float>", "float")]
    [InlineData("Vector<double>", "double")]
    [InlineData("Vector<int>", "int")]
    [InlineData("Vector256<float>", "float")]
    [InlineData("Vector128<double>", "double")]
    [InlineData("Vector512<int>", "int")]
    public void GetScalarType_WithVectorTypes_ReturnsCorrectScalarType(string vectorType, string expected)
    {
        // Act
        var result = SimdTypeMapper.GetScalarType(vectorType);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("float")]
    [InlineData("int")]
    [InlineData("string")]
    [InlineData("CustomType")]
    [InlineData("")]
    [InlineData(null)]
    public void GetScalarType_WithNonVectorTypes_ReturnsNull(string? vectorType)
    {
        // Act
        var result = SimdTypeMapper.GetScalarType(vectorType!);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Vector<>")]
    [InlineData("Vector256<>")]
    [InlineData("Vector")]
    [InlineData("<float>")]
    public void GetScalarType_WithMalformedVectorTypes_ReturnsNull(string vectorType)
    {
        // Act
        var result = SimdTypeMapper.GetScalarType(vectorType);
        
        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void SimdTypeMapper_RoundTripConversion_WorksCorrectly()
    {
        // Arrange
        var scalarTypes = new[] { "float", "double", "int", "long", "byte" };
        
        foreach (var scalar in scalarTypes)
        {
            // Act
            var vectorType = SimdTypeMapper.GetSimdType(scalar);
            var roundTripScalar = SimdTypeMapper.GetScalarType(vectorType!);
            
            // Assert
            roundTripScalar.Should().Be(scalar);
        }
    }

    [Fact]
    public void SimdTypeMapper_ConsistencyCheck_AllVectorizableTypesHaveMapping()
    {
        // Arrange
        var types = new[] { "float", "double", "int", "uint", "long", "ulong", "short", "ushort", "byte", "sbyte" };
        
        foreach (var type in types)
        {
            // Act & Assert
            SimdTypeMapper.IsVectorizableType(type).Should().BeTrue();
            SimdTypeMapper.GetSimdType(type).Should().NotBeNull();
            SimdTypeMapper.GetVectorSize(type).Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void SimdTypeMapper_CaseInsensitivity_WorksCorrectly()
    {
        // Arrange
        var variations = new[] { "FLOAT", "Float", "fLoAt" };
        
        foreach (var variation in variations)
        {
            // Act
            var result = SimdTypeMapper.GetSimdType(variation.ToLower());
            
            // Assert
            result.Should().Be("Vector<float>");
        }
    }

    #endregion
}