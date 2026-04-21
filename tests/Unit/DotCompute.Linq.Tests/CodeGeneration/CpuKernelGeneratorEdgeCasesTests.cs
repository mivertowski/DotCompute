// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests.CodeGeneration;

/// <summary>
/// v1.0.0 Phase 9 — Edge-case and structural unit tests for
/// <see cref="CpuKernelGenerator"/>. Covers gaps in existing baseline:
/// the Scan operator, type permutations beyond happy-path, error/diagnostic
/// paths, and structural code-emission assertions that pin down behavior
/// the optimizer cares about (vector shape, accumulator type, remainder loop).
/// </summary>
/// <remarks>
/// These tests purposefully do not re-assert what
/// <c>CpuKernelGeneratorTests.cs</c> already covers — they extend it. Each
/// test asserts a concrete structural property of the emitted source so a
/// future refactor that regresses the property fails loudly here rather than
/// in the integration layer.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Category", "CodeGeneration")]
[Trait("Category", "CpuKernelGeneratorEdgeCases")]
public sealed class CpuKernelGeneratorEdgeCasesTests
{
    private readonly CpuKernelGenerator _generator = new();

    #region Scan (prefix sum) operator — zero coverage in baseline

    [Fact]
    public void Scan_WithoutLambda_EmitsCumulativeLoop()
    {
        var graph = CreateGraph(OperationType.Scan);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Scan operation");
        _ = code.Should().Contain("accumulator");
        _ = code.Should().Contain("output[i] = accumulator;");
        _ = code.Should().Contain("accumulator += input[i];");
    }

    [Fact]
    public void Scan_WithLambda_InlinesTransformBeforeAccumulate()
    {
        Expression<Func<int, int>> transform = x => x * 2;
        var graph = CreateGraph(OperationType.Scan, transform);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Scan operation");
        _ = code.Should().Contain("* 2");
        _ = code.Should().Contain("accumulator += ");
    }

    [Fact]
    public void Scan_IsSequential_DoesNotEmitVectorLoop()
    {
        var graph = CreateGraph(OperationType.Scan);
        var metadata = CreateMetadata<double, double>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Scan is inherently sequential — no vectorization should be emitted.
        _ = code.Should().Contain("sequential by nature");
        _ = code.Should().NotContain("Vector<double>.Count");
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(short), "short")]
    public void Scan_AccumulatorDeclaredWithElementType(Type elementType, string expectedTypeName)
    {
        var graph = CreateGraph(OperationType.Scan);
        var metadata = new TypeMetadata
        {
            InputType = elementType,
            ResultType = elementType,
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = true,
            RequiresUnsafe = false,
            HasNullableTypes = false
        };

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain($"{expectedTypeName} accumulator = default;");
    }

    [Fact]
    public void Scan_LoopIteratesEveryInputElement()
    {
        var graph = CreateGraph(OperationType.Scan);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("for (int i = 0; i < input.Length; i++)");
    }

    [Fact]
    public void Scan_DefaultAccumulatorInitializedToZero()
    {
        var graph = CreateGraph(OperationType.Scan);
        var metadata = CreateMetadata<float, float>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("float accumulator = default;");
    }

    [Fact]
    public void Scan_UnsupportedTypeStillEmitsScalarLoop()
    {
        // Scan ignores SIMD capability — should work for any type.
        var graph = CreateGraph(OperationType.Scan);
        var metadata = CreateMetadata<decimal, decimal>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("accumulator");
        _ = code.Should().Contain("output[i] = accumulator");
    }

    #endregion

    #region Type permutation coverage — Map across every supported primitive

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(ushort), "ushort")]
    [InlineData(typeof(byte), "byte")]
    [InlineData(typeof(sbyte), "sbyte")]
    public void Map_EmitsSignatureForEverySupportedPrimitive(Type elementType, string expectedTypeName)
    {
        // Using a no-op expression (param => param) so every element type works.
        var param = Expression.Parameter(elementType, "x");
        var lambda = Expression.Lambda(param, param);

        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = new TypeMetadata
        {
            InputType = elementType,
            ResultType = elementType,
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = true,
            RequiresUnsafe = false,
            HasNullableTypes = false
        };

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain($"ReadOnlySpan<{expectedTypeName}> input");
        _ = code.Should().Contain($"Span<{expectedTypeName}> output");
        // Array-overload must also be materialized.
        _ = code.Should().Contain($"{expectedTypeName}[] Execute({expectedTypeName}[] input)");
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    public void Reduce_EmitsVectorAccumulatorForPrimitives(Type elementType)
    {
        var param = Expression.Parameter(elementType, "x");
        var lambda = Expression.Lambda(param, param);
        var graph = CreateGraph(OperationType.Reduce, lambda);
        var metadata = new TypeMetadata
        {
            InputType = elementType,
            ResultType = elementType,
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = true,
            RequiresUnsafe = false,
            HasNullableTypes = false
        };

        var code = _generator.GenerateKernel(graph, metadata);

        // Must emit a zero-initialized vector accumulator of element type.
        var typeName = GetName(elementType);
        _ = code.Should().Contain($"Vector<{typeName}>.Zero");
        _ = code.Should().Contain($"Vector<{typeName}>.Count");
    }

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    public void Reduce_SmallIntegerTypes_EmitSimdPath(Type elementType)
    {
        var param = Expression.Parameter(elementType, "x");
        var lambda = Expression.Lambda(param, param);
        var graph = CreateGraph(OperationType.Reduce, lambda);
        var metadata = new TypeMetadata
        {
            InputType = elementType,
            ResultType = elementType,
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = true,
            RequiresUnsafe = false,
            HasNullableTypes = false
        };

        var code = _generator.GenerateKernel(graph, metadata);

        // Reduce explicitly supports all primitives — vectorized path expected.
        var typeName = GetName(elementType);
        _ = code.Should().Contain($"new Vector<{typeName}>(input.Slice(i))");
    }

    #endregion

    #region Unsupported / complex-type behavior

    [Fact]
    public void Map_ComplexStructType_FallsBackToScalarLoop()
    {
        // DateTime is not in the SIMD-supported primitive set — generator should emit scalar only.
        Expression<Func<DateTime, DateTime>> lambda = x => x;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<DateTime, DateTime>();

        var code = _generator.GenerateKernel(graph, metadata);

        // No Vector<DateTime>-qualified code should appear.
        _ = code.Should().NotContain("Vector<DateTime>");
        // Scalar loop shape.
        _ = code.Should().Contain("for (int i = 0; i < input.Length; i++)");
    }

    [Fact]
    public void Map_StringType_FallsBackToScalarLoop()
    {
        Expression<Func<string, string>> lambda = s => s;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<string, string>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().NotContain("Vector<string>");
        _ = code.Should().Contain("ReadOnlySpan<string> input");
    }

    [Fact]
    public void Reduce_NonPrimitiveType_UsesScalarReducePath()
    {
        Expression<Func<decimal, decimal>> lambda = x => x;
        var graph = CreateGraph(OperationType.Reduce, lambda);
        var metadata = CreateMetadata<decimal, decimal>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Non-primitive types fall out of the C#-keyword map in GetTypeName and
        // render via Type.Name ("Decimal"). Both forms are valid C#; the important
        // property is that no Vector<T> accumulator is emitted.
        _ = code.Should().Contain("Decimal result = default;");
        _ = code.Should().NotContain("Vector<");
    }

    #endregion

    #region Edge-case input shapes

    [Fact]
    public void Map_WithoutLambda_FallsBackToIdentityCopy()
    {
        var op = new Operation
        {
            Id = "map-no-lambda",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>()
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<decimal, decimal>(); // non-SIMD to force scalar path

        var code = _generator.GenerateKernel(graph, metadata);

        // Scalar map path with no lambda copies input directly.
        _ = code.Should().Contain("output[i] = input[i];");
    }

    [Fact]
    public void Reduce_WithoutLambda_AccumulatesInputDirectly()
    {
        var op = new Operation
        {
            Id = "reduce-no-lambda",
            Type = OperationType.Reduce,
            Metadata = new Dictionary<string, object>()
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("accumulator += vec;");
        _ = code.Should().Contain("result += input[i];");
    }

    [Fact]
    public void Aggregate_DispatchedToReducePath()
    {
        // OperationType.Aggregate and OperationType.Reduce must emit identical structure.
        Expression<Func<float, float>> lambda = x => x;
        var graph = CreateGraph(OperationType.Aggregate, lambda);
        var metadata = CreateMetadata<float, float>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Reduce operation");
        _ = code.Should().Contain("Vector<float>.Zero");
    }

    [Fact]
    public void Filter_EmitsOutputIndexWriteAndPredicate()
    {
        Expression<Func<int, bool>> predicate = x => x > 10;
        var graph = CreateGraph(OperationType.Filter, predicate);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("int outputIndex = 0;");
        _ = code.Should().Contain("output[outputIndex++] = input[i];");
        _ = code.Should().Contain("> 10");
    }

    [Fact]
    public void Filter_WithoutLambda_ThrowsInvalidOperation()
    {
        var op = new Operation
        {
            Id = "filter-no-lambda",
            Type = OperationType.Filter,
            Metadata = new Dictionary<string, object>()
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var act = () => _generator.GenerateKernel(graph, metadata);

        _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Filter*does not have a Lambda*");
    }

    #endregion

    #region Structural / emission guarantees

    [Fact]
    public void Map_SimdPath_EmitsVectorizedThenScalarRemainder()
    {
        Expression<Func<int, int>> lambda = x => x + 1;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Vectorized main loop");
        _ = code.Should().Contain("Scalar remainder loop");
        // Remainder must run only on tail.
        _ = code.Should().Contain("for (; i < input.Length; i++)");
    }

    [Fact]
    public void Map_SimdPath_EmitsVectorConstruction()
    {
        Expression<Func<float, float>> lambda = x => x * 1.5f;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<float, float>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("new Vector<float>(input.Slice(i))");
        _ = code.Should().Contain("result.CopyTo(output.Slice(i));");
    }

    [Fact]
    public void Map_SimdPath_UsesVectorConstantForScalarOperand()
    {
        // Vector constant-wrapping is a load-bearing codegen contract —
        // if this breaks, vectorized kernels silently compile to scalar fallbacks.
        Expression<Func<int, int>> lambda = x => x * 7;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("new Vector<int>(");
        _ = code.Should().Contain("* new Vector<int>(7)");
    }

    [Fact]
    public void Reduce_HorizontalSum_IteratesAllLanes()
    {
        Expression<Func<int, int>> lambda = x => x;
        var graph = CreateGraph(OperationType.Reduce, lambda);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("for (int lane = 0; lane < vectorSize; lane++)");
        _ = code.Should().Contain("result += accumulator[lane];");
    }

    [Fact]
    public void Reduce_WritesResultToFirstOutputSlot()
    {
        Expression<Func<int, int>> lambda = x => x;
        var graph = CreateGraph(OperationType.Reduce, lambda);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("output[0] = result;");
    }

    [Fact]
    public void Reduce_WithMapLambda_AppliesTransformInsideVectorLoop()
    {
        Expression<Func<int, int>> lambda = x => x * x;
        var graph = CreateGraph(OperationType.Reduce, lambda);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        // vec reassigned with the transform before accumulation.
        _ = code.Should().Contain("vec = (vec * vec);");
        _ = code.Should().Contain("accumulator += vec;");
    }

    [Fact]
    public void Map_ConstantSuffix_IsTypeCorrect_ForFloat()
    {
        Expression<Func<float, float>> lambda = x => x + 2.5f;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<float, float>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Float constants must carry the 'f' suffix to prevent double-promotion.
        _ = code.Should().Contain("2.5f");
    }

    [Fact]
    public void Map_ConstantSuffix_IsTypeCorrect_ForLong()
    {
        Expression<Func<long, long>> lambda = x => x + 100L;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<long, long>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Long constants must carry the 'L' suffix.
        _ = code.Should().Contain("100L");
    }

    [Fact]
    public void Map_ConstantSuffix_IsTypeCorrect_ForDouble()
    {
        Expression<Func<double, double>> lambda = x => x * 3.14;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<double, double>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("3.14d");
    }

    #endregion

    #region Generated output envelope

    [Fact]
    public void AnyOperator_EmitsGeneratedKernelClass()
    {
        var graph = CreateGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("namespace DotCompute.Linq.Generated;");
        _ = code.Should().Contain("public static class GeneratedKernel");
    }

    [Fact]
    public void AnyOperator_EmitsArrayOverload_WithMatchingOutputAllocation()
    {
        var graph = CreateGraph(OperationType.Map, (Expression<Func<int, double>>)(x => x * 1.5));
        var metadata = CreateMetadata<int, double>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Array-based overload delegates to Span overload.
        _ = code.Should().Contain("public static double[] Execute(int[] input)");
        _ = code.Should().Contain("var output = new double[input.Length];");
        _ = code.Should().Contain("Execute(input.AsSpan(), output.AsSpan());");
        _ = code.Should().Contain("return output;");
    }

    [Fact]
    public void AnyOperator_EmitsAllRequiredUsings()
    {
        var graph = CreateGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("using System;");
        _ = code.Should().Contain("using System.Numerics;");
        _ = code.Should().Contain("using System.Runtime.Intrinsics;");
        _ = code.Should().Contain("using System.Threading.Tasks;");
        _ = code.Should().Contain("using System.Collections.Concurrent;");
    }

    [Fact]
    public void Generator_IsReusable_ProducesConsistentOutputOnRepeatedCalls()
    {
        // Generator keeps internal state (_builder, _indentLevel, _tempVarCounter)
        // but clears it per call. Must be safe to reuse.
        var graph = CreateGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x + 1));
        var metadata = CreateMetadata<int, int>();

        var first = _generator.GenerateKernel(graph, metadata);
        var second = _generator.GenerateKernel(graph, metadata);

        _ = second.Should().Be(first);
    }

    [Fact]
    public void Generator_HandlesDifferentInputAndOutputTypes()
    {
        // int → double projection.
        Expression<Func<int, double>> lambda = x => (double)x;
        var graph = CreateGraph(OperationType.Map, lambda);
        var metadata = CreateMetadata<int, double>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("ReadOnlySpan<int> input");
        _ = code.Should().Contain("Span<double> output");
    }

    #endregion

    #region Invalid graphs / argument validation

    [Fact]
    public void GenerateKernel_NullGraph_Throws()
    {
        var metadata = CreateMetadata<int, int>();

        var act = () => _generator.GenerateKernel(null!, metadata);

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateKernel_NullMetadata_Throws()
    {
        var graph = CreateGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x));

        var act = () => _generator.GenerateKernel(graph, null!);

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateKernel_EmptyGraph_ThrowsWithMessage()
    {
        var graph = new OperationGraph { Operations = new Collection<Operation>() };
        var metadata = CreateMetadata<int, int>();

        var act = () => _generator.GenerateKernel(graph, metadata);

        _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*no operations*");
    }

    [Fact]
    public void GenerateKernel_InvalidOperationTypeInGraph_ThrowsWithMessage()
    {
        // An undefined enum value surfaces through ValidateGraph.
        var op = new Operation
        {
            Id = "bad",
            Type = (OperationType)9999,
            Metadata = new Dictionary<string, object>()
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var act = () => _generator.GenerateKernel(graph, metadata);

        _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*invalid operation type*");
    }

    #endregion

    #region Helpers

    private static OperationGraph CreateGraph(OperationType type, LambdaExpression? lambda = null)
    {
        var metadata = new Dictionary<string, object>();
        if (lambda != null)
        {
            metadata["Lambda"] = lambda;
        }

        var op = new Operation
        {
            Id = "op",
            Type = type,
            Metadata = metadata
        };

        return new OperationGraph { Operations = new Collection<Operation> { op } };
    }

    private static TypeMetadata CreateMetadata<TInput, TOutput>()
    {
        return new TypeMetadata
        {
            InputType = typeof(TInput),
            ResultType = typeof(TOutput),
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = true,
            RequiresUnsafe = false,
            HasNullableTypes = false
        };
    }

    private static string GetName(Type type) => type switch
    {
        Type t when t == typeof(int) => "int",
        Type t when t == typeof(long) => "long",
        Type t when t == typeof(float) => "float",
        Type t when t == typeof(double) => "double",
        Type t when t == typeof(uint) => "uint",
        Type t when t == typeof(ulong) => "ulong",
        Type t when t == typeof(short) => "short",
        Type t when t == typeof(ushort) => "ushort",
        Type t when t == typeof(byte) => "byte",
        Type t when t == typeof(sbyte) => "sbyte",
        _ => type.Name
    };

    #endregion
}
