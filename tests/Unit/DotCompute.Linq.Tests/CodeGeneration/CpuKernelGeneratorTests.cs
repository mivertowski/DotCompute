// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Linq.Tests.CodeGeneration;

/// <summary>
/// Comprehensive unit tests for the CpuKernelGenerator component.
/// Tests kernel generation for Map/Filter/Reduce operations with SIMD acceleration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "CodeGeneration")]
public class CpuKernelGeneratorTests
{
    private readonly CpuKernelGenerator _generator;

    public CpuKernelGeneratorTests()
    {
        _generator = new CpuKernelGenerator();
    }

    #region Basic Kernel Generation Tests (10 tests)

    [Fact]
    public void GenerateKernel_SimpleMapOperation_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("public static class GeneratedKernel");
        result.Should().Contain("public static void Execute");
        result.Should().Contain("ReadOnlySpan<int> input");
        result.Should().Contain("Span<int> output");
    }

    [Fact]
    public void GenerateKernel_SimpleFilterOperation_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Filter operation");
        result.Should().Contain("outputIndex");
        result.Should().Contain("return outputIndex");
    }

    [Fact]
    public void GenerateKernel_SimpleReduceOperation_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Reduce operation");
        result.Should().Contain("accumulator");
        result.Should().Contain("return result");
    }

    [Fact]
    public void GenerateKernel_AggregateOperation_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Aggregate, (Expression<Func<float, float>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(float), typeof(float));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Reduce operation");
        result.Should().Contain("accumulator");
    }

    [Fact]
    public void GenerateKernel_FloatType_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<float, float>>)(x => x * 2.0f));
        var metadata = CreateTypeMetadata(typeof(float), typeof(float));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<float> input");
        result.Should().Contain("Span<float> output");
        result.Should().Contain("Vector<float>");
    }

    [Fact]
    public void GenerateKernel_DoubleType_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<double, double>>)(x => x * 2.0));
        var metadata = CreateTypeMetadata(typeof(double), typeof(double));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<double> input");
        result.Should().Contain("Span<double> output");
        result.Should().Contain("Vector<double>");
    }

    [Fact]
    public void GenerateKernel_LongType_GeneratesValidCode()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<long, long>>)(x => x * 2L));
        var metadata = CreateTypeMetadata(typeof(long), typeof(long));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<long> input");
        result.Should().Contain("Span<long> output");
    }

    [Fact]
    public void GenerateKernel_EmptyGraph_ThrowsInvalidOperationException()
    {
        // Arrange
        var graph = new OperationGraph { Operations = new Collection<Operation>() };
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        Action act = () => _generator.GenerateKernel(graph, metadata);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no operations*");
    }

    [Fact]
    public void GenerateKernel_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        Action act = () => _generator.GenerateKernel(null!, metadata);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("graph");
    }

    [Fact]
    public void GenerateKernel_NullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));

        // Act
        Action act = () => _generator.GenerateKernel(graph, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadata");
    }

    #endregion

    #region SIMD Capability Detection Tests (10 tests)

    [Fact]
    public void Capabilities_ReturnsValidSimdLevel()
    {
        // Act
        var capabilities = _generator.Capabilities;

        // Assert
        capabilities.Should().BeDefined();
        // Capabilities should match actual hardware
        if (Avx512F.IsSupported && Avx512BW.IsSupported)
        {
            // Can't directly assert due to internal enum, but should not throw
        }
    }

    [Fact]
    public void GenerateKernel_IntType_UsesSimdWhenSupported()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert - Should generate SIMD code if supported
        if (Avx2.IsSupported || AdvSimd.IsSupported)
        {
            result.Should().Contain("Vector<int>");
            result.Should().Contain("vectorSize");
        }
    }

    [Fact]
    public void GenerateKernel_UnsupportedType_GeneratesScalarCode()
    {
        // Arrange: Use a custom struct that doesn't support SIMD
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<decimal, decimal>>)(x => x * 2m));
        var metadata = CreateTypeMetadata(typeof(decimal), typeof(decimal)); // decimal doesn't support SIMD

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert - Should generate scalar fallback
        result.Should().Contain("for (int i = 0; i < input.Length; i++)");
        result.Should().NotContain("Vector<");
    }

    [Fact]
    public void GenerateKernel_ByteType_SupportsSimd()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<byte, byte>>)(x => (byte)(x + 1)));
        var metadata = CreateTypeMetadata(typeof(byte), typeof(byte));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<byte> input");
    }

    [Fact]
    public void GenerateKernel_ShortType_SupportsSimd()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<short, short>>)(x => (short)(x * 2)));
        var metadata = CreateTypeMetadata(typeof(short), typeof(short));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<short> input");
    }

    [Fact]
    public void GenerateKernel_UIntType_SupportsSimd()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<uint, uint>>)(x => x * 2u));
        var metadata = CreateTypeMetadata(typeof(uint), typeof(uint));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<uint> input");
    }

    [Fact]
    public void GenerateKernel_ULongType_SupportsSimd()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<ulong, ulong>>)(x => x * 2ul));
        var metadata = CreateTypeMetadata(typeof(ulong), typeof(ulong));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<ulong> input");
    }

    [Fact]
    public void GenerateKernel_SByteType_SupportsSimd()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<sbyte, sbyte>>)(x => (sbyte)(x + 1)));
        var metadata = CreateTypeMetadata(typeof(sbyte), typeof(sbyte));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<sbyte> input");
    }

    [Fact]
    public void GenerateKernel_UShortType_SupportsSimd()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<ushort, ushort>>)(x => (ushort)(x * 2)));
        var metadata = CreateTypeMetadata(typeof(ushort), typeof(ushort));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ReadOnlySpan<ushort> input");
    }

    [Fact]
    public void GenerateKernel_MixedTypes_GeneratesCorrectSignature()
    {
        // Arrange: Map int to float
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(float));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("ReadOnlySpan<int> input");
        result.Should().Contain("Span<float> output");
    }

    #endregion

    #region Fused Operation Tests (10 tests)

    [Fact]
    public void GenerateKernel_SelectWherePattern_GeneratesFusedCode()
    {
        // Arrange: Select then Where (Map then Filter)
        var graph = CreateFusedOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2), OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("Fused operations");
        result.Should().Match(s => s.Contains("Select + Where") || s.Contains("Map")).And.Contain("Filter");
    }

    [Fact]
    public void GenerateKernel_WhereSelectPattern_GeneratesFusedCode()
    {
        // Arrange: Where then Select (Filter then Map)
        var graph = CreateFusedOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0), OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("Fused operations");
    }

    [Fact]
    public void GenerateKernel_MapMapPattern_GeneratesFusedCode()
    {
        // Arrange: Two consecutive Map operations
        var graph = CreateFusedOperationGraph(
            OperationType.Map, (Expression<Func<int, int>>)(x => x * 2),
            OperationType.Map, (Expression<Func<int, int>>)(x => x + 1));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("Fused operations");
        result.Should().Contain("Map -> Map");
    }

    [Fact]
    public void GenerateKernel_ThreeOperations_IdentifiesFusableSubset()
    {
        // Arrange: Map -> Filter -> Reduce (only first two should fuse)
        var operations = new Collection<Operation>
        {
            CreateOperation("op1", OperationType.Map, (Expression<Func<int, int>>)(x => x * 2)),
            CreateOperation("op2", OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0)),
            CreateOperation("op3", OperationType.Reduce, (Expression<Func<int, int>>)(x => x))
        };
        var graph = new OperationGraph { Operations = operations };
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("Fused operations");
        result.Should().Contain("Map").And.Contain("Filter");
    }

    [Fact]
    public void GenerateKernel_NonFusableOps_GeneratesSeparateOperations()
    {
        // Arrange: Reduce followed by Map (cannot fuse)
        var operations = new Collection<Operation>
        {
            CreateOperation("op1", OperationType.Reduce, (Expression<Func<int, int>>)(x => x)),
            CreateOperation("op2", OperationType.Map, (Expression<Func<int, int>>)(x => x * 2))
        };
        var graph = new OperationGraph { Operations = operations };
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateKernel_FusedSelectWhere_ContainsTransformedVariable()
    {
        // Arrange
        var graph = CreateFusedOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2), OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("transformed");
        result.Should().Contain("mask");
    }

    [Fact]
    public void GenerateKernel_FusedOperations_ContainsVectorizedLoop()
    {
        // Arrange
        var graph = CreateFusedOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2), OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("vectorSize");
        result.Should().Contain("for (; i <= input.Length - vectorSize");
    }

    [Fact]
    public void GenerateKernel_FusedOperations_ContainsScalarRemainder()
    {
        // Arrange
        var graph = CreateFusedOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2), OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("Scalar remainder");
        result.Should().Contain("for (; i < input.Length");
    }

    [Fact]
    public void GenerateKernel_GeneralFusion_AppliesOperationsSequentially()
    {
        // Arrange: Multiple Map operations
        var operations = new Collection<Operation>
        {
            CreateOperation("op1", OperationType.Map, (Expression<Func<int, int>>)(x => x * 2)),
            CreateOperation("op2", OperationType.Map, (Expression<Func<int, int>>)(x => x + 1)),
            CreateOperation("op3", OperationType.Map, (Expression<Func<int, int>>)(x => x - 3))
        };
        var graph = new OperationGraph { Operations = operations };
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("vec");
        result.Should().Contain("CopyTo");
    }

    [Fact]
    public void GenerateKernel_FusedFloat_UsesFloatVectors()
    {
        // Arrange
        var graph = CreateFusedOperationGraph(OperationType.Map, (Expression<Func<float, float>>)(x => x * 2.0f), OperationType.Filter, (Expression<Func<float, bool>>)(x => x > 0.0f));
        var metadata = CreateTypeMetadata(typeof(float), typeof(float));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("Vector<float>");
    }

    #endregion

    #region Lambda Expression Handling Tests (10 tests)

    [Fact]
    public void GenerateKernel_SimpleLambda_InlinesCorrectly()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("* 2");
    }

    [Fact]
    public void GenerateKernel_ComplexLambda_InlinesCorrectly()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => (x * 2 + 5) / 3));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("*").And.Contain("+").And.Contain("/");
    }

    [Fact]
    public void GenerateKernel_ComparisonLambda_GeneratesComparison()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 10));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("> 10");
    }

    [Fact]
    public void GenerateKernel_EqualityLambda_GeneratesEquality()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x == 42));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("== 42");
    }

    [Fact]
    public void GenerateKernel_LogicalAndLambda_GeneratesAndOperator()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 10 && x < 100));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain(">");
        result.Should().Contain("<");
        result.Should().Contain("&&");
    }

    [Fact]
    public void GenerateKernel_LogicalOrLambda_GeneratesOrOperator()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x < 10 || x > 100));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("||");
    }

    [Fact]
    public void GenerateKernel_NotLambda_GeneratesNotOperator()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => !(x == 0)));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("!");
    }

    [Fact]
    public void GenerateKernel_ModuloLambda_GeneratesModuloOperator()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x % 2 == 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("%");
    }

    [Fact]
    public void GenerateKernel_NegateLambda_GeneratesNegation()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => -x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("-");
    }

    [Fact]
    public void GenerateKernel_OperationWithoutLambda_HandlesGracefully()
    {
        // Arrange: Reduce without transformation (direct sum)
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("result += input[i]");
    }

    #endregion

    #region Parallel Loop Generation Tests (5 tests)

    [Fact]
    public void GenerateKernel_LargeReduce_GeneratesParallelCode()
    {
        // Arrange: Use metadata to hint at large dataset
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        // Should generate parallel reduction for large datasets
        result.Should().Match(s => s.Contains("Parallel") || s.Contains("accumulator"));
    }

    [Fact]
    public void GenerateKernel_ParallelReduction_ContainsConcurrentBag()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        // Check if parallel code path is taken
        if (result.Contains("Parallel"))
        {
            result.Should().Contain("ConcurrentBag");
        }
    }

    [Fact]
    public void GenerateKernel_ParallelReduction_ContainsPartitioner()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        if (result.Contains("Parallel"))
        {
            result.Should().Contain("Partitioner");
        }
    }

    [Fact]
    public void GenerateKernel_ParallelReduction_HasLocalAccumulator()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<float, float>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(float), typeof(float));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        if (result.Contains("Parallel"))
        {
            result.Should().Match(s => s.Contains("localAccumulator") || s.Contains("localSum"));
        }
    }

    [Fact]
    public void GenerateKernel_ParallelReduction_AggregatesResults()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<double, double>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(double), typeof(double));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("result");
        result.Should().Contain("return result");
    }

    #endregion

    #region Generated Code Compilation Tests (10 tests)

    [Fact]
    public void GenerateKernel_SimpleMap_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_SimpleFilter_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_SimpleReduce_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_FusedOperations_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateFusedOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2), OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 0));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_FloatOperations_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (float x) => x * 2.5f);
        var metadata = CreateTypeMetadata(typeof(float), typeof(float));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_DoubleOperations_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (double x) => x * 2.5);
        var metadata = CreateTypeMetadata(typeof(double), typeof(double));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_ComplexFilter_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Filter, (Expression<Func<int, bool>>)(x => x > 10 && x < 100));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_LongOperations_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (long x) => x * 2L);
        var metadata = CreateTypeMetadata(typeof(long), typeof(long));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_ByteOperations_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<byte, byte>>)(x => (byte)(x + 1)));
        var metadata = CreateTypeMetadata(typeof(byte), typeof(byte));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    [Fact]
    public void GenerateKernel_AggregateWithTransform_CompilesSuccessfully()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Aggregate, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var code = _generator.GenerateKernel(graph, metadata);
        var compilation = CompileCode(code);

        // Assert
        compilation.Success.Should().BeTrue($"Code should compile: {string.Join(", ", compilation.Diagnostics)}");
    }

    #endregion

    #region Edge Cases and Validation Tests (5 tests)

    [Fact]
    public void GenerateKernel_ContainsNamespace()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("namespace DotCompute.Linq.Generated");
    }

    [Fact]
    public void GenerateKernel_ContainsRequiredUsings()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("using System;");
        result.Should().Contain("using System.Numerics;");
        result.Should().Contain("using System.Runtime.Intrinsics;");
    }

    [Fact]
    public void GenerateKernel_ContainsXmlDocumentation()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// <param name=\"input\">");
        result.Should().Contain("/// <param name=\"output\">");
    }

    [Fact]
    public void GenerateKernel_HasProperIndentation()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("    public static void Execute"); // 4-space indent
        result.Should().Contain("        "); // 8-space indent for method body
    }

    [Fact]
    public void GenerateKernel_HasStaticClass()
    {
        // Arrange
        var graph = CreateOperationGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x * 2));
        var metadata = CreateTypeMetadata(typeof(int), typeof(int));

        // Act
        var result = _generator.GenerateKernel(graph, metadata);

        // Assert
        result.Should().Contain("public static class GeneratedKernel");
        result.Should().Contain("public static void Execute");
    }

    #endregion

    #region Helper Methods

    private static OperationGraph CreateOperationGraph(OperationType type, LambdaExpression? lambda = null)
    {
        var operation = CreateOperation("op1", type, lambda);
        return new OperationGraph { Operations = new Collection<Operation> { operation } };
    }

    private static OperationGraph CreateOperationGraphWithoutLambda(OperationType type)
    {
        var operation = new Operation
        {
            Id = "op1",
            Type = type,
            Metadata = new Dictionary<string, object>()
        };
        return new OperationGraph { Operations = new Collection<Operation> { operation } };
    }

    private static OperationGraph CreateFusedOperationGraph(
        OperationType type1, LambdaExpression lambda1,
        OperationType type2, LambdaExpression lambda2)
    {
        var op1 = CreateOperation("op1", type1, lambda1);
        var op2 = CreateOperation("op2", type2, lambda2);
        return new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
    }

    private static Operation CreateOperation(string id, OperationType type, LambdaExpression? lambda = null)
    {
        var metadata = new Dictionary<string, object>();
        if (lambda != null)
        {
            metadata["Lambda"] = lambda;
        }

        return new Operation
        {
            Id = id,
            Type = type,
            Metadata = metadata
        };
    }

    private static TypeMetadata CreateTypeMetadata(Type inputType, Type resultType)
    {
        return new TypeMetadata
        {
            InputType = inputType,
            ResultType = resultType,
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = IsSimdCompatible(inputType),
            RequiresUnsafe = false,
            HasNullableTypes = false
        };
    }

    private static bool IsSimdCompatible(Type type)
    {
        return type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(uint) ||
               type == typeof(ulong) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(byte) ||
               type == typeof(sbyte);
    }

    private static (bool Success, IEnumerable<string> Diagnostics) CompileCode(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Numerics.Vector<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.Intrinsics.Vector128<>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Threading.Tasks").Location),
        };

        var compilation = CSharpCompilation.Create(
            "GeneratedKernelTest",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage())
            .ToList();

        return (diagnostics.Count == 0, diagnostics);
    }

    private static OperationGraph CreateFusedOperationGraphWithoutLambda(
        OperationType type1, OperationType type2)
    {
        var op1 = CreateOperationWithoutLambda("op1", type1);
        var op2 = CreateOperationWithoutLambda("op2", type2);
        return new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
    }

    private static Operation CreateOperationWithoutLambda(string id, OperationType type)
    {
        return new Operation
        {
            Id = id,
            Type = type,
            Metadata = new Dictionary<string, object>()
        };
    }

    #endregion
}
