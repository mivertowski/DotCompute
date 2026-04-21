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
/// v1.0.0 Phase 9 — Structural unit tests for <see cref="CpuKernelGenerator"/>
/// focused on the OrderBy / GroupBy / Join operators. Extends the Phase 8 tests
/// in <c>JoinGroupByOrderByTests.cs</c> with deeper coverage of metadata-driven
/// behavior (Descending, Aggregation kind), lambda key-selectors, type
/// permutations, and default-branch fallback.
/// </summary>
/// <remarks>
/// The CPU generator chose a different shape per operator (Array.Sort for
/// OrderBy, Dictionary for GroupBy, HashSet for Join). These tests pin the
/// shape so a refactor that swaps data structures surfaces here loudly.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Category", "CodeGeneration")]
[Trait("Category", "CpuKernelGeneratorOperators")]
public sealed class CpuKernelGeneratorOperatorTests
{
    private readonly CpuKernelGenerator _generator = new();

    #region OrderBy — metadata and key-selector paths

    [Fact]
    public void OrderBy_DefaultMetadata_AscendingLabelEmitted()
    {
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("OrderBy operation (ascending)");
        _ = code.Should().NotContain("Array.Reverse");
    }

    [Fact]
    public void OrderBy_DescendingMetadataFalse_DefaultsToAscending()
    {
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object> { ["Descending"] = false }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("OrderBy operation (ascending)");
        _ = code.Should().NotContain("Array.Reverse");
    }

    [Fact]
    public void OrderBy_WithKeySelector_DescendingReversesSortedValues()
    {
        Expression<Func<int, int>> keySelector = x => -x;
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = keySelector,
                ["Descending"] = true
            }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("sortKeys");
        _ = code.Should().Contain("Array.Sort(sortKeys, sortValues)");
        _ = code.Should().Contain("Array.Reverse(sortValues)");
    }

    [Fact]
    public void OrderBy_WithFloatKeySelector_DeclaresFloatKeyArray()
    {
        Expression<Func<int, float>> keySelector = x => (float)x * 0.5f;
        var graph = CreateGraph(OperationType.OrderBy, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("new float[output.Length]");
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(byte), "byte")]
    public void OrderBy_NaturalSort_EmitsTypedSortBuffer(Type elementType, string typeName)
    {
        var graph = CreateGraph(OperationType.OrderBy);
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

        _ = code.Should().Contain($"new {typeName}[output.Length]");
        _ = code.Should().Contain("Array.Sort(sortBuffer)");
    }

    [Fact]
    public void OrderBy_CopiesInputToOutputBeforeSorting()
    {
        // input.CopyTo(output) ensures the algorithm doesn't mutate the caller's buffer.
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<double, double>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("input.CopyTo(output);");
    }

    [Fact]
    public void OrderBy_DescendingNaturalSort_ReversesBuffer()
    {
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object> { ["Descending"] = true }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Array.Sort(sortBuffer)");
        _ = code.Should().Contain("Array.Reverse(sortBuffer)");
    }

    [Fact]
    public void OrderBy_DescendingMetadata_NonBoolValue_TreatedAsAscending()
    {
        // Generator uses `descObj is true` — any non-bool value (even truthy)
        // must not enable descending mode. This is a subtle failure mode.
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object> { ["Descending"] = "yes" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("ascending");
        _ = code.Should().NotContain("Array.Reverse");
    }

    #endregion

    #region GroupBy — aggregation kinds

    [Theory]
    [InlineData("Sum")]
    [InlineData("Min")]
    [InlineData("Max")]
    [InlineData("Average")]
    [InlineData("Avg")]
    public void GroupBy_NonCountAggregation_EmitsAccumulatorTuple(string aggregation)
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = aggregation }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Non-count aggregations produce (accumulator, count) tuples per key.
        _ = code.Should().Contain("(int Acc, int Count)");
        _ = code.Should().Contain("current.Count + 1");
    }

    [Fact]
    public void GroupBy_CountAggregation_EmitsIntegerCountDictionary()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Count" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Dictionary<int, int>");
        _ = code.Should().Contain("counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;");
    }

    [Fact]
    public void GroupBy_UnknownAggregation_FallsBackToCount()
    {
        // Any unrecognized aggregation key is treated as Count (default branch).
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "NonExistent" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("GroupBy operation with NonExistent aggregation");
        _ = code.Should().Contain("counts.TryGetValue");
    }

    [Fact]
    public void GroupBy_NoMetadata_DefaultsToCountAggregation()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Count aggregation");
        _ = code.Should().Contain("counts");
    }

    [Fact]
    public void GroupBy_SumAggregation_WritesAccumulatorToOutput()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Sum" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("output[groupIdx++] = (int)kv.Value.Acc;");
    }

    [Fact]
    public void GroupBy_AverageAggregation_CastsAccumulatorToDoubleForDivision()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Avg" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("(double)kv.Value.Count");
    }

    [Fact]
    public void GroupBy_MinAggregation_EmitsLessThanComparison()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Min" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<float, float>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("value < current.Acc ? value : current.Acc");
    }

    [Fact]
    public void GroupBy_MaxAggregation_EmitsGreaterThanComparison()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Max" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<float, float>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("value > current.Acc ? value : current.Acc");
    }

    [Fact]
    public void GroupBy_HonorsOutputCapacity_EmitsBoundsCheck()
    {
        // Output write must be guarded so a small output span doesn't overflow.
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("if (groupIdx >= output.Length) break;");
    }

    [Fact]
    public void GroupBy_NoKeySelector_UsesElementAsKey()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        // Without a key selector, input[i] is the key.
        _ = code.Should().Contain("var key = input[i];");
    }

    [Fact]
    public void GroupBy_WithDifferentKeyType_EmitsCorrectDictionaryGenericArg()
    {
        Expression<Func<int, long>> keySelector = x => (long)x;
        var graph = CreateGraph(OperationType.GroupBy, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Dictionary<long,");
    }

    [Theory]
    [InlineData(typeof(int), typeof(int), "int")]
    [InlineData(typeof(long), typeof(long), "long")]
    [InlineData(typeof(float), typeof(float), "float")]
    [InlineData(typeof(double), typeof(double), "double")]
    public void GroupBy_Sum_AggregatorTypeMatchesOutputType(
        Type inputType, Type outputType, string outputTypeName)
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Sum" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = new TypeMetadata
        {
            InputType = inputType,
            ResultType = outputType,
            IntermediateTypes = new Dictionary<string, Type>(),
            IsSimdCompatible = true,
            RequiresUnsafe = false,
            HasNullableTypes = false
        };

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain($"({outputTypeName} Acc, int Count)");
        _ = code.Should().Contain($"({outputTypeName})input[i]");
    }

    #endregion

    #region Join — hash-based dedup

    [Fact]
    public void Join_EmitsHashSetAndWriteIndex()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("new HashSet<int>()");
        _ = code.Should().Contain("int writeIdx = 0;");
        _ = code.Should().Contain("if (seen.Add(key))");
    }

    [Fact]
    public void Join_HonorsOutputCapacity()
    {
        // Prevents overflow when output is smaller than distinct-key cardinality.
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("if (writeIdx < output.Length)");
    }

    [Fact]
    public void Join_WithKeySelector_UsesLambdaKeyExtraction()
    {
        Expression<Func<int, int>> keySelector = x => x / 10;
        var graph = CreateGraph(OperationType.Join, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("/ 10");
    }

    [Fact]
    public void Join_WithStringKeySelector_EmitsStringHashSet()
    {
        Expression<Func<int, string>> keySelector = x => x.ToString();
        var graph = CreateGraph(OperationType.Join, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("new HashSet<string>()");
    }

    [Fact]
    public void Join_NoKeySelector_KeyIsInputElement()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("var key = input[i];");
    }

    [Fact]
    public void Join_WritesOriginalInputElementNotKey()
    {
        // The dedup semantic: key is used for Add(); original element is written.
        Expression<Func<int, long>> keySelector = x => (long)x;
        var graph = CreateGraph(OperationType.Join, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("output[writeIdx++] = input[i];");
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(short), "short")]
    public void Join_TypePermutations_EmitCorrectHashSetType(Type elementType, string typeName)
    {
        var graph = CreateGraph(OperationType.Join);
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

        _ = code.Should().Contain($"new HashSet<{typeName}>()");
    }

    #endregion

    #region Multi-op graphs and fusion paths

    [Fact]
    public void MapMapFusion_AppliesBothLambdasInSingleLoop()
    {
        Expression<Func<int, int>> first = x => x + 1;
        Expression<Func<int, int>> second = x => x * 2;
        var op1 = new Operation
        {
            Id = "op1",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object> { ["Lambda"] = first }
        };
        var op2 = new Operation
        {
            Id = "op2",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object> { ["Lambda"] = second }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Fused operations");
        _ = code.Should().Contain("+ 1");
        _ = code.Should().Contain("* 2");
    }

    [Fact]
    public void MapFilterFusion_ProducesFusedSelectWhereBlock()
    {
        Expression<Func<int, int>> map = x => x + 10;
        Expression<Func<int, bool>> filter = x => x > 15;
        var op1 = new Operation
        {
            Id = "op1",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object> { ["Lambda"] = map }
        };
        var op2 = new Operation
        {
            Id = "op2",
            Type = OperationType.Filter,
            Metadata = new Dictionary<string, object> { ["Lambda"] = filter }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Fused Select + Where");
        _ = code.Should().Contain("var transformed =");
        _ = code.Should().Contain("var mask =");
    }

    [Fact]
    public void FilterMapFusion_ProducesFusedWhereSelectBlock()
    {
        Expression<Func<int, bool>> filter = x => x > 0;
        Expression<Func<int, int>> map = x => x * 3;
        var op1 = new Operation
        {
            Id = "op1",
            Type = OperationType.Filter,
            Metadata = new Dictionary<string, object> { ["Lambda"] = filter }
        };
        var op2 = new Operation
        {
            Id = "op2",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object> { ["Lambda"] = map }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Fused Where + Select");
    }

    [Fact]
    public void NonFusableOps_SortThenMap_GeneratesSeparateBlocks()
    {
        Expression<Func<int, int>> map = x => x + 1;
        var op1 = new Operation
        {
            Id = "op1",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object>()
        };
        var op2 = new Operation
        {
            Id = "op2",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object> { ["Lambda"] = map }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        // OrderBy does not fuse — two distinct operation comments.
        _ = code.Should().Contain("OrderBy operation");
        _ = code.Should().Contain("Map operation");
    }

    [Fact]
    public void ScanThenMap_NonFusable_ProducesIndependentBlocks()
    {
        Expression<Func<int, int>> map = x => x + 2;
        var op1 = new Operation
        {
            Id = "op1",
            Type = OperationType.Scan,
            Metadata = new Dictionary<string, object>()
        };
        var op2 = new Operation
        {
            Id = "op2",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object> { ["Lambda"] = map }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op1, op2 } };
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Scan operation");
        _ = code.Should().Contain("Map operation");
    }

    #endregion

    #region Error paths specific to operator dispatch

    [Fact]
    public void JoinWithoutKeySelectorAndOutputCapacity_EmitsBoundsCheck()
    {
        // Regression: generator must always guard output writes in Join.
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("writeIdx < output.Length");
        _ = code.Should().NotContain("passing through"); // legacy placeholder string
    }

    [Fact]
    public void OrderBy_OutputContainsNoLegacyPassthrough()
    {
        // OrderBy was previously a placeholder — guard against regression.
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().NotContain("TODO");
        _ = code.Should().NotContain("passing through");
        _ = code.Should().NotContain("not yet implemented");
    }

    [Fact]
    public void GroupBy_OutputContainsNoLegacyPassthrough()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().NotContain("TODO");
        _ = code.Should().NotContain("passing through");
        _ = code.Should().NotContain("not yet implemented");
    }

    [Fact]
    public void Join_OutputContainsNoLegacyPassthrough()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _generator.GenerateKernel(graph, metadata);

        _ = code.Should().NotContain("TODO");
        _ = code.Should().NotContain("passing through");
        _ = code.Should().NotContain("not yet implemented");
    }

    #endregion

    #region Concurrency surface — repeated calls do not corrupt state

    [Fact]
    public void Generator_AlternatingOperatorCalls_KeepOutputsDistinct()
    {
        // Simulates a pipeline compiling many kernels through one generator.
        var mapGraph = CreateGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x + 1));
        var reduceGraph = CreateGraph(OperationType.Reduce, (Expression<Func<int, int>>)(x => x));
        var metadata = CreateMetadata<int, int>();

        var mapCode = _generator.GenerateKernel(mapGraph, metadata);
        var reduceCode = _generator.GenerateKernel(reduceGraph, metadata);
        var mapCodeAgain = _generator.GenerateKernel(mapGraph, metadata);

        _ = mapCode.Should().Contain("Map operation");
        _ = reduceCode.Should().Contain("Reduce operation");
        _ = mapCode.Should().NotContain("Reduce operation");
        _ = reduceCode.Should().NotContain("Map operation");
        _ = mapCodeAgain.Should().Be(mapCode);
    }

    [Fact]
    public void Generator_ProducesSameOutputAcrossTwoInstances()
    {
        var graph = CreateGraph(OperationType.Map, (Expression<Func<int, int>>)(x => x + 1));
        var metadata = CreateMetadata<int, int>();

        var first = new CpuKernelGenerator().GenerateKernel(graph, metadata);
        var second = new CpuKernelGenerator().GenerateKernel(graph, metadata);

        _ = second.Should().Be(first);
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

    #endregion
}
