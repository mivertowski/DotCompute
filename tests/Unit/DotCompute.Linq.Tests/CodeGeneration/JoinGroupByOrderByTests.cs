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
/// Phase 8 (v1.0.0): Unit tests for Join, GroupBy, and OrderBy operators across
/// the CPU SIMD, CUDA, and Metal code generators. Verifies that the emitted source
/// is well-formed and contains the expected structural elements for each operator
/// shape - it does not execute the kernels (hardware tests cover that).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "CodeGeneration")]
[Trait("Category", "JoinGroupByOrderBy")]
public sealed class JoinGroupByOrderByTests
{
    private readonly CpuKernelGenerator _cpuGenerator = new();
    private readonly CudaKernelGenerator _cudaGenerator = new();
    private readonly MetalKernelGenerator _metalGenerator = new();
    private readonly JoinKernelGenerator _joinGenerator = new();
    private readonly GroupByKernelGenerator _groupByGenerator = new();
    private readonly OrderByKernelGenerator _orderByGenerator = new();

    #region CPU - OrderBy Tests

    [Fact]
    public void Cpu_OrderBy_WithoutKeySelector_GeneratesNaturalSort()
    {
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("OrderBy operation (ascending)");
        _ = code.Should().Contain("Array.Sort");
        _ = code.Should().NotContain("passing through");
    }

    [Fact]
    public void Cpu_OrderBy_Descending_CallsArrayReverse()
    {
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object> { ["Descending"] = true }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<float, float>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("OrderBy operation (descending)");
        _ = code.Should().Contain("Array.Reverse");
    }

    [Fact]
    public void Cpu_OrderBy_WithKeySelector_BuildsParallelKeyArray()
    {
        Expression<Func<int, int>> keySelector = x => x * -1;
        var graph = CreateGraph(OperationType.OrderBy, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("sortKeys");
        _ = code.Should().Contain("Array.Sort(sortKeys, sortValues)");
    }

    #endregion

    #region CPU - GroupBy Tests

    [Fact]
    public void Cpu_GroupBy_DefaultCountAggregation_UsesDictionary()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("GroupBy operation with Count aggregation");
        _ = code.Should().Contain("Dictionary<");
        _ = code.Should().NotContain("passing through");
    }

    [Fact]
    public void Cpu_GroupBy_SumAggregation_EmitsAccumulatorMath()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Sum" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("GroupBy operation with Sum aggregation");
        _ = code.Should().Contain("current.Acc + value");
    }

    [Fact]
    public void Cpu_GroupBy_MinMaxAggregation_UsesComparisonExpression()
    {
        foreach (var aggregation in new[] { "Min", "Max" })
        {
            var op = new Operation
            {
                Id = "groupBy",
                Type = OperationType.GroupBy,
                Metadata = new Dictionary<string, object> { ["Aggregation"] = aggregation }
            };
            var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
            var metadata = CreateMetadata<float, float>();

            var code = _cpuGenerator.GenerateKernel(graph, metadata);

            _ = code.Should().Contain($"GroupBy operation with {aggregation} aggregation");
            var containsMinOrMax = code.Contains("value < current.Acc", StringComparison.Ordinal)
                                || code.Contains("value > current.Acc", StringComparison.Ordinal);
            _ = containsMinOrMax.Should().BeTrue();
        }
    }

    [Fact]
    public void Cpu_GroupBy_AverageAggregation_DividesSumByCount()
    {
        var op = new Operation
        {
            Id = "groupBy",
            Type = OperationType.GroupBy,
            Metadata = new Dictionary<string, object> { ["Aggregation"] = "Average" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("kv.Value.Acc / (double)kv.Value.Count");
    }

    [Fact]
    public void Cpu_GroupBy_WithKeySelector_InvokesLambdaExpression()
    {
        Expression<Func<int, int>> keySelector = x => x % 5;
        var graph = CreateGraph(OperationType.GroupBy, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        var containsModulo = code.Contains("input[i] % 5", StringComparison.Ordinal)
                          || code.Contains("(input[i] % 5)", StringComparison.Ordinal);
        _ = containsModulo.Should().BeTrue();
    }

    #endregion

    #region CPU - Join Tests

    [Fact]
    public void Cpu_Join_DefaultPath_UsesHashSetDedup()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("Join operation");
        _ = code.Should().Contain("HashSet<");
        _ = code.Should().Contain("seen.Add(key)");
        _ = code.Should().NotContain("passing through");
    }

    [Fact]
    public void Cpu_Join_EmptyInput_GeneratesSafeCode()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _cpuGenerator.GenerateKernel(graph, metadata);

        _ = code.Should().Contain("writeIdx < output.Length");
    }

    #endregion

    #region CUDA - OrderBy Tests

    [Fact]
    public void Cuda_OrderBy_GeneratesBitonicSort()
    {
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("Bitonic Sort");
        _ = code.Should().Contain("__shared__");
        _ = code.Should().Contain("__syncthreads");
    }

    [Fact]
    public void Cuda_OrderBy_Ascending_DefaultDirection()
    {
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<float, float>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("ascending order");
        var containsMaxValue = code.Contains("FLT_MAX", StringComparison.Ordinal)
                            || code.Contains("3.402823466e+38f", StringComparison.Ordinal);
        _ = containsMaxValue.Should().BeTrue();
    }

    [Fact]
    public void Cuda_OrderBy_Descending_UsesMinValuePadding()
    {
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object> { ["Descending"] = true }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("descending order");
        _ = code.Should().Contain("-2147483648"); // INT_MIN
    }

    #endregion

    #region CUDA - GroupBy Tests

    [Fact]
    public void Cuda_GroupBy_UsesAtomicAdd()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("GroupBy Operation");
        _ = code.Should().Contain("atomicAdd(&output[key], 1)");
    }

    [Fact]
    public void Cuda_GroupBy_GuardsKeyRange()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("key >= 0 && key < length");
    }

    [Fact]
    public void Cuda_GroupBy_WithKeySelector_EmitsLambda()
    {
        Expression<Func<int, int>> keySelector = x => x / 10;
        var graph = CreateGraph(OperationType.GroupBy, keySelector);
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("/ 10");
    }

    #endregion

    #region CUDA - Join Tests

    [Fact]
    public void Cuda_Join_GeneratesHashTable()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("Hash Join Operation");
        _ = code.Should().Contain("HASH_TABLE_SIZE");
        _ = code.Should().Contain("atomicCAS");
    }

    [Fact]
    public void Cuda_Join_DefaultInnerJoin()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("INNER join");
    }

    [Fact]
    public void Cuda_Join_SemiType_EmitsExistenceCheck()
    {
        var op = new Operation
        {
            Id = "join",
            Type = OperationType.Join,
            Metadata = new Dictionary<string, object> { ["JoinType"] = "semi" }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<int, int>();

        var code = _cudaGenerator.GenerateCudaKernel(graph, metadata);

        _ = code.Should().Contain("SEMI join");
        _ = code.Should().Contain("Semi-join output: 1 if match exists");
    }

    #endregion

    #region Metal - OrderBy Tests

    [Fact]
    public void Metal_OrderBy_GeneratesBitonicSort()
    {
        var graph = CreateGraph(OperationType.OrderBy);
        var metadata = CreateMetadata<int, int>();

        var code = _metalGenerator.GenerateMetalKernel(graph, metadata);

        _ = code.Should().Contain("Bitonic Sort");
        _ = code.Should().Contain("threadgroup");
        _ = code.Should().Contain("threadgroup_barrier");
        _ = code.Should().NotContain("not yet implemented");
    }

    [Fact]
    public void Metal_OrderBy_Descending_InvertsCompare()
    {
        var op = new Operation
        {
            Id = "sort",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object> { ["Descending"] = true }
        };
        var graph = new OperationGraph { Operations = new Collection<Operation> { op } };
        var metadata = CreateMetadata<float, float>();

        var code = _metalGenerator.GenerateMetalKernel(graph, metadata);

        _ = code.Should().Contain("descending");
        _ = code.Should().Contain("a < b");
    }

    #endregion

    #region Metal - GroupBy Tests

    [Fact]
    public void Metal_GroupBy_IncrementsOutputBucket()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _metalGenerator.GenerateMetalKernel(graph, metadata);

        _ = code.Should().Contain("GroupBy Operation");
        _ = code.Should().Contain("output[key]");
    }

    [Fact]
    public void Metal_GroupBy_GuardsKeyRange()
    {
        var graph = CreateGraph(OperationType.GroupBy);
        var metadata = CreateMetadata<int, int>();

        var code = _metalGenerator.GenerateMetalKernel(graph, metadata);

        _ = code.Should().Contain("key >= 0 && key < length");
    }

    #endregion

    #region Metal - Join Tests

    [Fact]
    public void Metal_Join_UsesSelfSemiJoinPattern()
    {
        var graph = CreateGraph(OperationType.Join);
        var metadata = CreateMetadata<int, int>();

        var code = _metalGenerator.GenerateMetalKernel(graph, metadata);

        _ = code.Should().Contain("Join Operation");
        _ = code.Should().Contain("matchCount");
        _ = code.Should().NotContain("not yet implemented");
    }

    #endregion

    #region JoinKernelGenerator - Specialized Generator Tests

    [Fact]
    public void JoinKernelGenerator_Cuda_GeneratesBuildAndProbePhases()
    {
        var code = _joinGenerator.GenerateCudaJoinKernel("int", "int", "int");

        _ = code.Should().Contain("HashJoinBuild");
        _ = code.Should().Contain("HashJoinProbe");
        _ = code.Should().Contain("HashJoinGather");
        _ = code.Should().Contain("atomicCAS");
        _ = code.Should().Contain("atomicAdd(matchCount, 1)");
    }

    [Fact]
    public void JoinKernelGenerator_Cuda_LeftOuterJoin_IncludesAllLeftRows()
    {
        var config = new JoinKernelGenerator.JoinConfiguration
        {
            JoinType = JoinKernelGenerator.JoinType.LeftOuter
        };

        var code = _joinGenerator.GenerateCudaJoinKernel("int", "int", "int", config);

        _ = code.Should().Contain("Left Outer Join");
        _ = code.Should().Contain("LeftOuter");
    }

    [Fact]
    public void JoinKernelGenerator_Cuda_SemiJoin_OutputsIndicesOnly()
    {
        var config = new JoinKernelGenerator.JoinConfiguration
        {
            JoinType = JoinKernelGenerator.JoinType.Semi
        };

        var code = _joinGenerator.GenerateCudaJoinKernel("int", "int", "int", config);

        _ = code.Should().Contain("Semi Join");
        _ = code.Should().Contain("leftIndices[outIdx] = idx");
    }

    [Fact]
    public void JoinKernelGenerator_Cuda_AntiJoin_OutputsNonMatches()
    {
        var config = new JoinKernelGenerator.JoinConfiguration
        {
            JoinType = JoinKernelGenerator.JoinType.Anti
        };

        var code = _joinGenerator.GenerateCudaJoinKernel("int", "int", "int", config);

        _ = code.Should().Contain("Anti Join");
        _ = code.Should().Contain("matchedIdx < 0");
    }

    [Fact]
    public void JoinKernelGenerator_Metal_GeneratesBuildAndProbeKernels()
    {
        var code = _joinGenerator.GenerateMetalJoinKernel("int", "int", "int");

        _ = code.Should().Contain("kernel void HashJoinBuild");
        _ = code.Should().Contain("kernel void HashJoinProbe");
        _ = code.Should().Contain("atomic_compare_exchange_weak_explicit");
    }

    [Fact]
    public void JoinKernelGenerator_BuildPhase_UsesLinearProbing()
    {
        var code = _joinGenerator.GenerateCudaBuildPhaseKernel("int", "key");

        _ = code.Should().Contain("Linear probing insertion");
        _ = code.Should().Contain("atomicCAS");
    }

    [Fact]
    public void JoinKernelGenerator_ProbePhase_TerminatesOnEmptySlot()
    {
        var code = _joinGenerator.GenerateCudaProbePhaseKernel("int", "int", "key");

        _ = code.Should().Contain("Linear probing search");
        _ = code.Should().Contain("Key not found");
    }

    [Fact]
    public void JoinKernelGenerator_WithOuterKeySelector_InlinesLambda()
    {
        Expression<Func<int, int>> keySelector = x => x + 100;
        var config = new JoinKernelGenerator.JoinConfiguration
        {
            OuterKeySelector = keySelector
        };

        var code = _joinGenerator.GenerateCudaJoinKernel("int", "int", "int", config);

        _ = code.Should().Contain("+ 100");
    }

    #endregion

    #region GroupByKernelGenerator - Specialized Generator Tests

    [Fact]
    public void GroupByKernelGenerator_Cuda_Count_Only()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Count
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "int", config);

        _ = code.Should().Contain("GroupByInit");
        _ = code.Should().Contain("GroupByAggregate");
        _ = code.Should().Contain("atomicAdd(&counts[slot], 1)");
    }

    [Fact]
    public void GroupByKernelGenerator_Cuda_Sum_GeneratesSumAtomic()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Sum
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "float", config);

        _ = code.Should().Contain("atomicAdd(&sums[slot], value)");
    }

    [Fact]
    public void GroupByKernelGenerator_Cuda_Min_GeneratesAtomicCasLoop()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Min
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "float", config);

        _ = code.Should().Contain("Atomic min update using CAS loop");
        _ = code.Should().Contain("atomicCAS((int*)&minVals[slot]");
    }

    [Fact]
    public void GroupByKernelGenerator_Cuda_Max_GeneratesAtomicCasLoop()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Max
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "float", config);

        _ = code.Should().Contain("Atomic max update using CAS loop");
        _ = code.Should().Contain("atomicCAS((int*)&maxVals[slot]");
    }

    [Fact]
    public void GroupByKernelGenerator_Cuda_Average_GeneratesFinalizationKernel()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Average
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "float", config);

        _ = code.Should().Contain("GroupByFinalize");
        _ = code.Should().Contain("sums[idx] / (float)counts[idx]");
    }

    [Fact]
    public void GroupByKernelGenerator_Cuda_AllAggregations_ProducesStructuredOutput()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.All
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "float", config);

        _ = code.Should().Contain("int count;");
        _ = code.Should().Contain("float sum;");
        _ = code.Should().Contain("float minVal;");
        _ = code.Should().Contain("float maxVal;");
    }

    [Fact]
    public void GroupByKernelGenerator_Metal_Count_GeneratesAtomicBucketUpdate()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Count
        };

        var code = _groupByGenerator.GenerateMetalGroupByKernel("int", "int", "float", config);

        _ = code.Should().Contain("kernel void GroupByInit");
        _ = code.Should().Contain("kernel void GroupByAggregate");
        _ = code.Should().Contain("atomic_fetch_add_explicit(&counts[slot], 1");
    }

    [Fact]
    public void GroupByKernelGenerator_LinearProbeCollisionResolution_Present()
    {
        var config = new GroupByKernelGenerator.GroupByConfiguration
        {
            Aggregations = GroupByKernelGenerator.AggregationFunction.Count
        };

        var code = _groupByGenerator.GenerateCudaGroupByKernel("int", "int", "int", config);

        _ = code.Should().Contain("Linear probing");
        _ = code.Should().Contain("MAX_PROBE_DISTANCE");
    }

    #endregion

    #region OrderByKernelGenerator - Specialized Generator Tests

    [Fact]
    public void OrderByKernelGenerator_Cuda_ThreeKernels_Emitted()
    {
        var code = _orderByGenerator.GenerateCudaOrderByKernel("int", "int");

        _ = code.Should().Contain("BitonicSortLocal");
        _ = code.Should().Contain("BitonicMergeGlobal");
        _ = code.Should().Contain("BitonicMergeShared");
    }

    [Fact]
    public void OrderByKernelGenerator_Cuda_UsesSharedMemory()
    {
        var code = _orderByGenerator.GenerateCudaOrderByKernel("int", "int");

        _ = code.Should().Contain("__shared__");
        _ = code.Should().Contain("BLOCK_SIZE");
    }

    [Fact]
    public void OrderByKernelGenerator_Cuda_KeySelectorExtractor_Emitted()
    {
        Expression<Func<int, int>> keySelector = x => x * 2;
        var config = new OrderByKernelGenerator.OrderByConfiguration
        {
            KeySelector = keySelector
        };

        var code = _orderByGenerator.GenerateCudaOrderByKernel("int", "int", config);

        _ = code.Should().Contain("extractKey");
    }

    [Fact]
    public void OrderByKernelGenerator_Descending_SwapConditionInverted()
    {
        var config = new OrderByKernelGenerator.OrderByConfiguration
        {
            Descending = true
        };

        var code = _orderByGenerator.GenerateCudaOrderByKernel("int", "int", config);

        _ = code.Should().Contain("descending");
    }

    [Fact]
    public void OrderByKernelGenerator_Metal_TwoKernels_Emitted()
    {
        var code = _orderByGenerator.GenerateMetalOrderByKernel("int", "int");

        _ = code.Should().Contain("kernel void BitonicSortLocal");
        _ = code.Should().Contain("kernel void BitonicMergeGlobal");
    }

    [Fact]
    public void OrderByKernelGenerator_CustomBlockSize_Applied()
    {
        var config = new OrderByKernelGenerator.OrderByConfiguration
        {
            BlockSize = 512
        };

        var code = _orderByGenerator.GenerateCudaOrderByKernel("int", "int", config);

        _ = code.Should().Contain("BLOCK_SIZE 512");
    }

    [Fact]
    public void OrderByKernelGenerator_Cuda_XorPartnerIndex_Used()
    {
        var code = _orderByGenerator.GenerateCudaOrderByKernel("int", "int");

        _ = code.Should().Contain("tid ^ j");
    }

    #endregion

    #region Edge Case Tests - Empty Graphs / Null Inputs

    [Fact]
    public void Cpu_OrderBy_EmptyGraph_Throws()
    {
        var graph = new OperationGraph { Operations = new Collection<Operation>() };
        var metadata = CreateMetadata<int, int>();

        var act = () => _cpuGenerator.GenerateKernel(graph, metadata);
        _ = act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cuda_GroupBy_EmptyGraph_Throws()
    {
        var graph = new OperationGraph { Operations = new Collection<Operation>() };
        var metadata = CreateMetadata<int, int>();

        var act = () => _cudaGenerator.GenerateCudaKernel(graph, metadata);
        _ = act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Metal_Join_EmptyGraph_Throws()
    {
        var graph = new OperationGraph { Operations = new Collection<Operation>() };
        var metadata = CreateMetadata<int, int>();

        var act = () => _metalGenerator.GenerateMetalKernel(graph, metadata);
        _ = act.Should().Throw<InvalidOperationException>();
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
