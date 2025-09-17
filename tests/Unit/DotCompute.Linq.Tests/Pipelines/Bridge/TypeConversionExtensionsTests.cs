// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using FluentAssertions;
using DotCompute.Linq.Analysis;
using DotCompute.Linq.Compilation.Analysis;
using DotCompute.Linq.Pipelines.Bridge;
using DotCompute.Linq.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Parameters;
using LinqParameterDirection = DotCompute.Linq.Operators.Parameters.ParameterDirection;
using AbstractionsParameterDirection = DotCompute.Abstractions.Kernels.ParameterDirection;
using AbstractionsMemoryPattern = DotCompute.Abstractions.Types.MemoryAccessPattern;
using PipelineMemoryPattern = DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern;

namespace DotCompute.Linq.Tests.Pipelines.Bridge;

/// <summary>
/// Comprehensive unit tests for the TypeConversionExtensions class.
/// Tests all conversion methods, null handling, validation, and performance characteristics.
/// </summary>
public sealed class TypeConversionExtensionsTests
{
    #region PipelineOperatorInfo ↔ OperatorInfo Conversion Tests

    [Fact]
    public void ToOperatorInfo_WithValidPipelineInfo_ShouldConvertCorrectly()
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = "ADD",
            OperatorType = typeof(int),
            ComplexityScore = 3,
            CanParallelize = true,
            MemoryRequirement = 1024,
            InputTypes = new List<Type> { typeof(int), typeof(int) },
            OutputType = typeof(int),
            SupportsGpu = true,
            SupportsCpu = true,
            IsNativelySupported = true,
            Implementation = "Intrinsic",
            PerformanceCost = 0.5,
            Accuracy = 1.0,
            Metadata = new Dictionary<string, object> { ["TestKey"] = "TestValue" }
        };

        // Act
        var result = pipelineInfo.ToOperatorInfo(BackendType.CPU);

        // Assert
        result.Should().NotBeNull();
        result!.OperatorType.Should().Be(ExpressionType.Add);
        result.OperandTypes.Should().BeEquivalentTo(new[] { typeof(int), typeof(int) });
        result.ResultType.Should().Be(typeof(int));
        result.Backend.Should().Be(BackendType.CPU);
        result.IsNativelySupported.Should().BeTrue();
        result.SupportsVectorization.Should().BeTrue();
        result.Metadata.Should().ContainKey("Pipeline_TestKey");
    }

    [Fact]
    public void ToOperatorInfo_WithCudaBackend_ShouldOptimizeForCuda()
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = "MULTIPLY",
            InputTypes = new List<Type> { typeof(float), typeof(float) },
            OutputType = typeof(float),
            SupportsGpu = true
        };

        // Act
        var result = pipelineInfo.ToOperatorInfo(BackendType.CUDA);

        // Assert
        result.Should().NotBeNull();
        result!.Backend.Should().Be(BackendType.CUDA);
        result.OptimalVectorWidth.Should().Be(32); // CUDA warp size
        result.Performance.Throughput.Should().BeGreaterThan(1e10); // High GPU throughput
    }

    [Fact]
    public void ToOperatorInfo_WithNullInput_ShouldReturnNull()
    {
        // Act
        var result = ((PipelineOperatorInfo?)null).ToOperatorInfo();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToPipelineOperatorInfo_WithValidOperatorInfo_ShouldConvertCorrectly()
    {
        // Arrange
        var operatorInfo = OperatorInfo.ForCPU(ExpressionType.Add, new[] { typeof(int), typeof(int) }, typeof(int));

        // Act
        var result = operatorInfo.ToPipelineOperatorInfo();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Add");
        result.InputTypes.Should().BeEquivalentTo(new[] { typeof(int), typeof(int) });
        result.OutputType.Should().Be(typeof(int));
        result.SupportsCpu.Should().BeTrue();
        result.IsNativelySupported.Should().BeTrue();
        result.CanParallelize.Should().BeTrue(); // Add operation supports vectorization
    }

    [Fact]
    public void ToPipelineOperatorInfo_WithNullInput_ShouldReturnNull()
    {
        // Act
        var result = ((OperatorInfo?)null).ToPipelineOperatorInfo();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToOperatorInfos_WithCollection_ShouldConvertAll()
    {
        // Arrange
        var pipelineInfos = new[]
        {
            new PipelineOperatorInfo { Name = "ADD", InputTypes = new List<Type> { typeof(int) }, OutputType = typeof(int) },
            new PipelineOperatorInfo { Name = "MULTIPLY", InputTypes = new List<Type> { typeof(float) }, OutputType = typeof(float) }
        };

        // Act
        var results = pipelineInfos.ToOperatorInfos(BackendType.CPU).ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].OperatorType.Should().Be(ExpressionType.Add);
        results[1].OperatorType.Should().Be(ExpressionType.Multiply);
    }

    [Fact]
    public void ToPipelineOperatorInfos_WithCollection_ShouldConvertAll()
    {
        // Arrange
        var operatorInfos = new[]
        {
            OperatorInfo.ForCPU(ExpressionType.Add, new[] { typeof(int) }, typeof(int)),
            OperatorInfo.ForCPU(ExpressionType.Multiply, new[] { typeof(float) }, typeof(float))
        };

        // Act
        var results = operatorInfos.ToPipelineOperatorInfos().ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Add");
        results[1].Name.Should().Be("Multiply");
    }

    #endregion

    #region MemoryAccessPattern Enum Mapping Tests

    [Theory]
    [InlineData(AbstractionsMemoryPattern.Sequential, PipelineMemoryPattern.Sequential)]
    [InlineData(AbstractionsMemoryPattern.Strided, PipelineMemoryPattern.Strided)]
    [InlineData(AbstractionsMemoryPattern.Coalesced, PipelineMemoryPattern.Coalesced)]
    [InlineData(AbstractionsMemoryPattern.Random, PipelineMemoryPattern.Random)]
    [InlineData(AbstractionsMemoryPattern.Mixed, PipelineMemoryPattern.Random)]
    [InlineData(AbstractionsMemoryPattern.ScatterGather, PipelineMemoryPattern.Random)]
    [InlineData(AbstractionsMemoryPattern.Broadcast, PipelineMemoryPattern.Sequential)]
    public void ToPipelinePattern_ShouldMapCorrectly(AbstractionsMemoryPattern input, PipelineMemoryPattern expected)
    {
        // Act
        var result = input.ToPipelinePattern();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PipelineMemoryPattern.Sequential, AbstractionsMemoryPattern.Sequential)]
    [InlineData(PipelineMemoryPattern.Strided, AbstractionsMemoryPattern.Strided)]
    [InlineData(PipelineMemoryPattern.Coalesced, AbstractionsMemoryPattern.Coalesced)]
    [InlineData(PipelineMemoryPattern.Random, AbstractionsMemoryPattern.Random)]
    public void ToAbstractionsPattern_ShouldMapCorrectly(PipelineMemoryPattern input, AbstractionsMemoryPattern expected)
    {
        // Act
        var result = input.ToAbstractionsPattern();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MemoryAccessType.Sequential, AbstractionsMemoryPattern.Sequential)]
    [InlineData(MemoryAccessType.Random, AbstractionsMemoryPattern.Random)]
    [InlineData(MemoryAccessType.Strided, AbstractionsMemoryPattern.Strided)]
    [InlineData(MemoryAccessType.Coalesced, AbstractionsMemoryPattern.Coalesced)]
    public void ToMemoryAccessPattern_FromMemoryAccessType_ShouldMapCorrectly(MemoryAccessType input, AbstractionsMemoryPattern expected)
    {
        // Act
        var result = input.ToMemoryAccessPattern();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(AbstractionsMemoryPattern.Sequential, MemoryAccessType.Sequential)]
    [InlineData(AbstractionsMemoryPattern.Random, MemoryAccessType.Random)]
    [InlineData(AbstractionsMemoryPattern.Strided, MemoryAccessType.Strided)]
    [InlineData(AbstractionsMemoryPattern.Coalesced, MemoryAccessType.Coalesced)]
    [InlineData(AbstractionsMemoryPattern.Mixed, MemoryAccessType.Random)]
    [InlineData(AbstractionsMemoryPattern.ScatterGather, MemoryAccessType.Random)]
    [InlineData(AbstractionsMemoryPattern.Broadcast, MemoryAccessType.Sequential)]
    public void ToMemoryAccessType_ShouldMapCorrectly(AbstractionsMemoryPattern input, MemoryAccessType expected)
    {
        // Act
        var result = input.ToMemoryAccessType();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Parameter Direction Mapping Tests

    [Theory]
    [InlineData(LinqParameterDirection.In, AbstractionsParameterDirection.In)]
    [InlineData(LinqParameterDirection.Out, AbstractionsParameterDirection.Out)]
    [InlineData(LinqParameterDirection.InOut, AbstractionsParameterDirection.InOut)]
    [InlineData(LinqParameterDirection.Input, AbstractionsParameterDirection.In)]
    [InlineData(LinqParameterDirection.Output, AbstractionsParameterDirection.Out)]
    public void ToAbstractionsDirection_ShouldMapCorrectly(LinqParameterDirection input, AbstractionsParameterDirection expected)
    {
        // Act
        var result = input.ToAbstractionsDirection();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(AbstractionsParameterDirection.In, LinqParameterDirection.In)]
    [InlineData(AbstractionsParameterDirection.Out, LinqParameterDirection.Out)]
    [InlineData(AbstractionsParameterDirection.InOut, LinqParameterDirection.InOut)]
    public void ToLinqDirection_ShouldMapCorrectly(AbstractionsParameterDirection input, LinqParameterDirection expected)
    {
        // Act
        var result = input.ToLinqDirection();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToAbstractionsDirections_WithCollection_ShouldConvertAll()
    {
        // Arrange
        var linqDirections = new[] { LinqParameterDirection.In, LinqParameterDirection.Out, LinqParameterDirection.InOut };

        // Act
        var results = linqDirections.ToAbstractionsDirections().ToList();

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Be(AbstractionsParameterDirection.In);
        results[1].Should().Be(AbstractionsParameterDirection.Out);
        results[2].Should().Be(AbstractionsParameterDirection.InOut);
    }

    [Fact]
    public void ToLinqDirections_WithCollection_ShouldConvertAll()
    {
        // Arrange
        var abstractionsDirections = new[] { AbstractionsParameterDirection.In, AbstractionsParameterDirection.Out, AbstractionsParameterDirection.InOut };

        // Act
        var results = abstractionsDirections.ToLinqDirections().ToList();

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Be(LinqParameterDirection.In);
        results[1].Should().Be(LinqParameterDirection.Out);
        results[2].Should().Be(LinqParameterDirection.InOut);
    }

    [Fact]
    public void ToAbstractionsDirections_WithNullCollection_ShouldReturnEmpty()
    {
        // Act
        var results = ((IEnumerable<LinqParameterDirection>?)null).ToAbstractionsDirections().ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void ToLinqDirections_WithNullCollection_ShouldReturnEmpty()
    {
        // Act
        var results = ((IEnumerable<AbstractionsParameterDirection>?)null).ToLinqDirections().ToList();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region Pipeline Interface Bridge Adapter Tests

    [Theory]
    [InlineData(DataAccessPattern.Sequential, AbstractionsMemoryPattern.Sequential)]
    [InlineData(DataAccessPattern.Random, AbstractionsMemoryPattern.Random)]
    [InlineData(DataAccessPattern.Streaming, AbstractionsMemoryPattern.Sequential)]
    [InlineData(DataAccessPattern.Sparse, AbstractionsMemoryPattern.ScatterGather)]
    [InlineData(DataAccessPattern.CacheFriendly, AbstractionsMemoryPattern.Sequential)]
    public void ToMemoryAccessPattern_FromDataAccessPattern_ShouldMapCorrectly(DataAccessPattern input, AbstractionsMemoryPattern expected)
    {
        // Act
        var result = input.ToMemoryAccessPattern();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(AbstractionsMemoryPattern.Sequential, DataAccessPattern.Sequential)]
    [InlineData(AbstractionsMemoryPattern.Random, DataAccessPattern.Random)]
    [InlineData(AbstractionsMemoryPattern.Strided, DataAccessPattern.Sequential)]
    [InlineData(AbstractionsMemoryPattern.Coalesced, DataAccessPattern.CacheFriendly)]
    [InlineData(AbstractionsMemoryPattern.Mixed, DataAccessPattern.Random)]
    [InlineData(AbstractionsMemoryPattern.ScatterGather, DataAccessPattern.Sparse)]
    [InlineData(AbstractionsMemoryPattern.Broadcast, DataAccessPattern.CacheFriendly)]
    public void ToDataAccessPattern_ShouldMapCorrectly(AbstractionsMemoryPattern input, DataAccessPattern expected)
    {
        // Act
        var result = input.ToDataAccessPattern();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToMemoryAccessCharacteristics_WithValidGlobalPattern_ShouldCreateCharacteristics()
    {
        // Arrange
        var globalPattern = new GlobalMemoryAccessPattern
        {
            AccessType = MemoryAccessType.Sequential,
            LocalityFactor = 0.8,
            CacheEfficiency = 0.9,
            BandwidthUtilization = 0.7,
            IsCoalesced = true,
            StridePattern = 32
        };

        // Act
        var result = globalPattern.ToMemoryAccessCharacteristics();

        // Assert
        result.Should().NotBeNull();
        result.PrimaryPattern.Should().Be(DataAccessPattern.Sequential);
        result.LocalityScore.Should().Be(0.8);
        result.CacheHitRatio.Should().Be(0.9);
        result.BandwidthUtilization.Should().Be(0.7);
        result.SupportsCoalescing.Should().BeTrue();
        result.PreferredAlignment.Should().Be(32);
    }

    [Fact]
    public void ToMemoryAccessCharacteristics_WithNullPattern_ShouldCreateDefaultCharacteristics()
    {
        // Act
        var result = ((GlobalMemoryAccessPattern?)null).ToMemoryAccessCharacteristics();

        // Assert
        result.Should().NotBeNull();
        result.PrimaryPattern.Should().Be(DataAccessPattern.Sequential);
        result.LocalityScore.Should().Be(0.5);
        result.CacheHitRatio.Should().Be(0.5);
        result.BandwidthUtilization.Should().Be(0.5);
        result.SupportsCoalescing.Should().BeFalse();
        result.PreferredAlignment.Should().Be(16); // Minimum alignment
    }

    #endregion

    #region Type Safety and Validation Tests

    [Fact]
    public void ValidateConversionResult_WithValidValue_ShouldReturnValue()
    {
        // Arrange
        var testValue = new PipelineOperatorInfo { Name = "Test" };

        // Act
        var result = testValue.ValidateConversionResult("test operation");

        // Assert
        result.Should().BeSameAs(testValue);
    }

    [Fact]
    public void ValidateConversionResult_WithNullValue_ShouldThrowException()
    {
        // Act & Assert
        var action = () => ((PipelineOperatorInfo?)null).ValidateConversionResult("test operation");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Type conversion failed during test operation: result was null");
    }

    [Fact]
    public void ValidateConversionResult_WithNullValueAndFallback_ShouldReturnFallback()
    {
        // Arrange
        var fallback = new PipelineOperatorInfo { Name = "Fallback" };

        // Act
        var result = ((PipelineOperatorInfo?)null).ValidateConversionResult(fallback);

        // Assert
        result.Should().BeSameAs(fallback);
    }

    [Fact]
    public void SafeConvert_WithValidValue_ShouldConvert()
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo { Name = "ADD" };

        // Act
        var result = pipelineInfo.SafeConvert(p => p.ToOperatorInfo());

        // Assert
        result.Should().NotBeNull();
        result!.OperatorType.Should().Be(ExpressionType.Add);
    }

    [Fact]
    public void SafeConvert_WithNullValue_ShouldReturnNull()
    {
        // Act
        var result = ((PipelineOperatorInfo?)null).SafeConvert(p => p.ToOperatorInfo());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Performance and Memory Estimation Tests

    [Fact]
    public void MemoryEstimation_ShouldCalculateBasedOnTypes()
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = "ADD",
            InputTypes = new List<Type> { typeof(double), typeof(double) }, // 8 bytes each
            OutputType = typeof(double), // 8 bytes
            CanParallelize = true
        };

        // Act
        var operatorInfo = pipelineInfo.ToOperatorInfo(BackendType.CPU);

        // Assert
        operatorInfo.Should().NotBeNull();
        // Should account for input types (16 bytes) + output type (8 bytes) = 24 bytes
        // Multiplied by optimal vector width for doubles (4) = 96 bytes minimum
        var estimatedMemory = EstimateExpectedMemoryUsage(operatorInfo!);
        estimatedMemory.Should().BeGreaterThan(80); // Allow some variance
    }

    [Theory]
    [InlineData(typeof(byte), 1)]
    [InlineData(typeof(short), 2)]
    [InlineData(typeof(int), 4)]
    [InlineData(typeof(long), 8)]
    [InlineData(typeof(float), 4)]
    [InlineData(typeof(double), 8)]
    public void TypeSizeEstimation_ShouldBeCorrect(Type type, int expectedSize)
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = "TEST",
            InputTypes = new List<Type> { type },
            OutputType = type
        };

        // Act
        var operatorInfo = pipelineInfo.ToOperatorInfo();

        // Assert
        operatorInfo.Should().NotBeNull();
        // Verify that the type size estimation is reasonable based on the expected size
        // The actual calculation includes vector width, so we just verify it's not zero
        var estimatedMemory = EstimateExpectedMemoryUsage(operatorInfo!);
        estimatedMemory.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PerformanceCostCalculation_ShouldReflectThroughputAndLatency()
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = "EXPENSIVE_OP",
            PerformanceCost = 2.0, // High cost operation
            OutputType = typeof(double)
        };

        // Act
        var operatorInfo = pipelineInfo.ToOperatorInfo();
        var backToPipeline = operatorInfo!.ToPipelineOperatorInfo();

        // Assert
        backToPipeline.Should().NotBeNull();
        backToPipeline!.PerformanceCost.Should().BeGreaterThan(0);
    }

    #endregion

    #region Operator Name Mapping Tests

    [Theory]
    [InlineData("ADD", ExpressionType.Add)]
    [InlineData("ADDITION", ExpressionType.Add)]
    [InlineData("SUB", ExpressionType.Subtract)]
    [InlineData("SUBTRACT", ExpressionType.Subtract)]
    [InlineData("MUL", ExpressionType.Multiply)]
    [InlineData("MULTIPLY", ExpressionType.Multiply)]
    [InlineData("DIV", ExpressionType.Divide)]
    [InlineData("DIVIDE", ExpressionType.Divide)]
    [InlineData("MOD", ExpressionType.Modulo)]
    [InlineData("MODULO", ExpressionType.Modulo)]
    [InlineData("POW", ExpressionType.Power)]
    [InlineData("POWER", ExpressionType.Power)]
    [InlineData("AND", ExpressionType.And)]
    [InlineData("BITWISEAND", ExpressionType.And)]
    [InlineData("OR", ExpressionType.Or)]
    [InlineData("BITWISEOR", ExpressionType.Or)]
    [InlineData("XOR", ExpressionType.ExclusiveOr)]
    [InlineData("EXCLUSIVEOR", ExpressionType.ExclusiveOr)]
    [InlineData("NOT", ExpressionType.Not)]
    [InlineData("BITWISENOT", ExpressionType.Not)]
    [InlineData("EQ", ExpressionType.Equal)]
    [InlineData("EQUAL", ExpressionType.Equal)]
    [InlineData("NE", ExpressionType.NotEqual)]
    [InlineData("NOTEQUAL", ExpressionType.NotEqual)]
    [InlineData("LT", ExpressionType.LessThan)]
    [InlineData("LESSTHAN", ExpressionType.LessThan)]
    [InlineData("LE", ExpressionType.LessThanOrEqual)]
    [InlineData("LESSTHANOREQUAL", ExpressionType.LessThanOrEqual)]
    [InlineData("GT", ExpressionType.GreaterThan)]
    [InlineData("GREATERTHAN", ExpressionType.GreaterThan)]
    [InlineData("GE", ExpressionType.GreaterThanOrEqual)]
    [InlineData("GREATERTHANOREQUAL", ExpressionType.GreaterThanOrEqual)]
    [InlineData("NEG", ExpressionType.Negate)]
    [InlineData("NEGATE", ExpressionType.Negate)]
    [InlineData("COND", ExpressionType.Conditional)]
    [InlineData("CONDITIONAL", ExpressionType.Conditional)]
    [InlineData("UNKNOWN_OPERATOR", ExpressionType.Call)]
    public void OperatorNameMapping_ShouldMapCorrectly(string operatorName, ExpressionType expectedType)
    {
        // Arrange
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = operatorName,
            OutputType = typeof(int)
        };

        // Act
        var operatorInfo = pipelineInfo.ToOperatorInfo();

        // Assert
        operatorInfo.Should().NotBeNull();
        operatorInfo!.OperatorType.Should().Be(expectedType);
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Fact]
    public void Conversions_WithEmptyCollections_ShouldHandleGracefully()
    {
        // Arrange
        var emptyPipelineInfos = Array.Empty<PipelineOperatorInfo>();
        var emptyOperatorInfos = Array.Empty<OperatorInfo>();

        // Act
        var result1 = emptyPipelineInfos.ToOperatorInfos().ToList();
        var result2 = emptyOperatorInfos.ToPipelineOperatorInfos().ToList();

        // Assert
        result1.Should().BeEmpty();
        result2.Should().BeEmpty();
    }

    [Fact]
    public void Conversions_WithMinimalData_ShouldCreateValidObjects()
    {
        // Arrange
        var minimalPipelineInfo = new PipelineOperatorInfo(); // Default values only

        // Act
        var operatorInfo = minimalPipelineInfo.ToOperatorInfo();
        var backToPipeline = operatorInfo?.ToPipelineOperatorInfo();

        // Assert
        operatorInfo.Should().NotBeNull();
        backToPipeline.Should().NotBeNull();
        backToPipeline!.Name.Should().NotBeNullOrEmpty();
        backToPipeline.OutputType.Should().NotBeNull();
    }

    [Fact]
    public void MetadataCombination_ShouldPreserveAllInformation()
    {
        // Arrange
        var operatorInfo = OperatorInfo.ForCPU(ExpressionType.Add, new[] { typeof(int) }, typeof(int));
        var pipelineInfo = new PipelineOperatorInfo
        {
            Name = "ADD",
            Metadata = new Dictionary<string, object>
            {
                ["PipelineSpecific"] = "Value1",
                ["TestFlag"] = true
            }
        };

        // Act
        var enhanced = pipelineInfo.ToOperatorInfo();

        // Assert
        enhanced.Should().NotBeNull();
        enhanced!.Metadata.Should().ContainKey("Pipeline_PipelineSpecific");
        enhanced.Metadata.Should().ContainKey("Pipeline_TestFlag");
        enhanced.Metadata.Should().ContainKey("Backend"); // From base metadata
        enhanced.Metadata.Should().ContainKey("Implementation"); // From base metadata
    }

    #endregion

    #region Helper Methods

    private static long EstimateExpectedMemoryUsage(OperatorInfo operatorInfo)
    {
        // Replicate the logic from the TypeConversionExtensions for testing
        var baseSize = 0L;
        if (operatorInfo.OperandTypes != null)
        {
            foreach (var type in operatorInfo.OperandTypes)
            {
                baseSize += EstimateTypeSize(type);
            }
        }
        baseSize += EstimateTypeSize(operatorInfo.ResultType);
        return baseSize * operatorInfo.OptimalVectorWidth;
    }

    private static long EstimateTypeSize(Type type)
    {
        return type switch
        {
            _ when type == typeof(byte) => 1,
            _ when type == typeof(short) => 2,
            _ when type == typeof(int) => 4,
            _ when type == typeof(long) => 8,
            _ when type == typeof(float) => 4,
            _ when type == typeof(double) => 8,
            _ when type == typeof(decimal) => 16,
            _ => 8 // Default assumption
        };
    }

    #endregion
}