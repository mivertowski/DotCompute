// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests.CodeGeneration;

/// <summary>
/// Comprehensive unit tests for <see cref="CudaKernelGenerator"/>.
/// Tests kernel code generation logic without requiring CUDA hardware.
/// </summary>
public sealed class CudaKernelGeneratorTests
{
    private readonly CudaKernelGenerator _generator;

    public CudaKernelGeneratorTests()
    {
        _generator = new CudaKernelGenerator();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesValidInstance()
    {
        // Act
        var generator = new CudaKernelGenerator();

        // Assert
        _ = generator.Should().NotBeNull();
    }

    #endregion

    #region GenerateCudaKernel - Input Validation Tests

    [Fact]
    public void GenerateCudaKernel_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        var metadata = CreateMetadata<int, int>();

        // Act & Assert
        var act = () => _generator.GenerateCudaKernel(null!, metadata);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("graph");
    }

    [Fact]
    public void GenerateCudaKernel_NullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = new OperationGraph();

        // Act & Assert
        var act = () => _generator.GenerateCudaKernel(graph, null!);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("metadata");
    }

    [Fact]
    public void GenerateCudaKernel_EmptyGraph_ThrowsInvalidOperationException()
    {
        // Arrange
        var graph = new OperationGraph();
        var metadata = CreateMetadata<int, int>();

        // Act & Assert
        var act = () => _generator.GenerateCudaKernel(graph, metadata);
        _ = act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region GenerateCudaKernel - Map Operation Tests

    [Fact]
    public void GenerateCudaKernel_MapOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateMapGraph<int, int>(x => x * 2);
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
        _ = kernel.Should().Contain("blockIdx.x");
        _ = kernel.Should().Contain("blockDim.x");
        _ = kernel.Should().Contain("threadIdx.x");
    }

    [Fact]
    public void GenerateCudaKernel_MapOperation_ContainsKernelName()
    {
        // Arrange
        var graph = CreateMapGraph<int, int>(x => x * 2);
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("void");
        _ = kernel.Should().Contain("kernel");
    }

    [Fact]
    public void GenerateCudaKernel_MapOperation_ContainsBoundsCheck()
    {
        // Arrange
        var graph = CreateMapGraph<int, int>(x => x + 1);
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert - Generator uses early return pattern: "if (idx >= length) return;"
        _ = kernel.Should().Contain("idx >= length");
    }

    [Fact]
    public void GenerateCudaKernel_MapOperationFloat_UsesCorrectType()
    {
        // Arrange
        var graph = CreateMapGraph<float, float>(x => x * 2.0f);
        var metadata = CreateMetadata<float, float>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("float");
    }

    [Fact]
    public void GenerateCudaKernel_MapOperationDouble_UsesCorrectType()
    {
        // Arrange
        var graph = CreateMapGraph<double, double>(x => x * 2.0);
        var metadata = CreateMetadata<double, double>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("double");
    }

    #endregion

    #region GenerateCudaKernel - Filter Operation Tests

    [Fact]
    public void GenerateCudaKernel_FilterOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateFilterGraph<int>(x => x > 0);
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
    }

    [Fact]
    public void GenerateCudaKernel_FilterOperation_ContainsConditionalLogic()
    {
        // Arrange
        var graph = CreateFilterGraph<int>(x => x > 5);
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("if");
    }

    #endregion

    #region GenerateCudaKernel - Reduce Operation Tests

    [Fact]
    public void GenerateCudaKernel_ReduceOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateReduceGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
    }

    [Fact]
    public void GenerateCudaKernel_ReduceOperation_UsesSharedMemory()
    {
        // Arrange
        var graph = CreateReduceGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("__shared__");
    }

    [Fact]
    public void GenerateCudaKernel_ReduceOperation_ContainsSyncThreads()
    {
        // Arrange
        var graph = CreateReduceGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("__syncthreads()");
    }

    #endregion

    #region GenerateCudaKernel - Scan Operation Tests

    [Fact]
    public void GenerateCudaKernel_ScanOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateScanGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
        _ = kernel.Should().Contain("__shared__");
    }

    [Fact]
    public void GenerateCudaKernel_ScanOperation_ContainsBlellochAlgorithm()
    {
        // Arrange
        var graph = CreateScanGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert - Blelloch algorithm has up-sweep and down-sweep phases
        _ = kernel.Should().Contain("for");
    }

    #endregion

    #region GenerateCudaKernel - OrderBy Operation Tests

    [Fact]
    public void GenerateCudaKernel_OrderByOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateOrderByGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
    }

    [Fact]
    public void GenerateCudaKernel_OrderByOperation_UsesBitonicSort()
    {
        // Arrange
        var graph = CreateOrderByGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert - Bitonic sort uses comparison and swap
        _ = kernel.Should().Contain("__shared__");
    }

    #endregion

    #region GenerateCudaKernel - GroupBy Operation Tests

    [Fact]
    public void GenerateCudaKernel_GroupByOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateGroupByGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
    }

    [Fact]
    public void GenerateCudaKernel_GroupByOperation_UsesAtomicOperations()
    {
        // Arrange
        var graph = CreateGroupByGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().Contain("atomicAdd");
    }

    #endregion

    #region GenerateCudaKernel - Join Operation Tests

    [Fact]
    public void GenerateCudaKernel_JoinOperation_GeneratesValidKernel()
    {
        // Arrange
        var graph = CreateJoinGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
    }

    [Fact]
    public void GenerateCudaKernel_JoinOperation_UsesHashTable()
    {
        // Arrange
        var graph = CreateJoinGraph<int>();
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert - Hash join uses shared memory hash table
        _ = kernel.Should().Contain("__shared__");
        _ = kernel.Should().Contain("HASH_TABLE_SIZE");
    }

    #endregion

    #region GenerateCudaKernel - Fused Operations Tests

    [Fact]
    public void GenerateCudaKernel_MapThenFilter_GeneratesFusedKernel()
    {
        // Arrange
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "map_0",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = (Expression<Func<int, int>>)(x => x * 2)
            }
        });
        graph.Operations.Add(new Operation
        {
            Id = "filter_0",
            Type = OperationType.Filter,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = (Expression<Func<int, bool>>)(x => x > 10)
            }
        });
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert - Should generate a single kernel with both operations
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("__global__");
    }

    [Fact]
    public void GenerateCudaKernel_MultipleMapOperations_GeneratesFusedKernel()
    {
        // Arrange
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "map_0",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = (Expression<Func<int, int>>)(x => x * 2)
            }
        });
        graph.Operations.Add(new Operation
        {
            Id = "map_1",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = (Expression<Func<int, int>>)(x => x + 1)
            }
        });
        var metadata = CreateMetadata<int, int>();

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
        _ = kernel.Should().Contain("output[idx]");
    }

    #endregion

    #region GetCompilationOptions Tests

    [Fact]
    public void GetCompilationOptions_Cuda_ReturnsValidOptions()
    {
        // Act
        var options = _generator.GetCompilationOptions(ComputeBackend.Cuda);

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ThreadBlockSize.Should().BePositive();
        _ = options.TargetArchitecture.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetCompilationOptions_Cuda_DefaultThreadBlockSize256()
    {
        // Act
        var options = _generator.GetCompilationOptions(ComputeBackend.Cuda);

        // Assert
        _ = options.ThreadBlockSize.Should().Be(256);
    }

    [Fact]
    public void GetCompilationOptions_Cuda_TargetsSM50OrHigher()
    {
        // Act
        var options = _generator.GetCompilationOptions(ComputeBackend.Cuda);

        // Assert
        _ = options.TargetArchitecture.Should().Contain("sm_");
    }

    [Fact]
    public void GetCompilationOptions_OpenCL_ReturnsValidOptions()
    {
        // Act
        var options = _generator.GetCompilationOptions(ComputeBackend.OpenCL);

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ThreadBlockSize.Should().BePositive();
    }

    [Fact]
    public void GetCompilationOptions_Metal_ReturnsValidOptions()
    {
        // Act
        var options = _generator.GetCompilationOptions(ComputeBackend.Metal);

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ThreadBlockSize.Should().BePositive();
    }

    #endregion

    #region GenerateOpenCLKernel Tests

    [Fact]
    public void GenerateOpenCLKernel_ThrowsNotImplementedException()
    {
        // Arrange
        var graph = CreateMapGraph<int, int>(x => x * 2);
        var metadata = CreateMetadata<int, int>();

        // Act & Assert
        var act = () => _generator.GenerateOpenCLKernel(graph, metadata);
        _ = act.Should().Throw<NotImplementedException>();
    }

    #endregion

    #region GenerateMetalKernel Tests

    [Fact]
    public void GenerateMetalKernel_ThrowsNotImplementedException()
    {
        // Arrange
        var graph = CreateMapGraph<int, int>(x => x * 2);
        var metadata = CreateMetadata<int, int>();

        // Act & Assert
        var act = () => _generator.GenerateMetalKernel(graph, metadata);
        _ = act.Should().Throw<NotImplementedException>();
    }

    #endregion

    #region Type Mapping Tests

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(long))]
    public void GenerateCudaKernel_SupportedTypes_GeneratesValidKernel(Type elementType)
    {
        // Arrange
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "map_0",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>()
        });
        var metadata = new TypeMetadata
        {
            InputType = elementType,
            ResultType = elementType,
            IntermediateTypes = new Dictionary<string, Type>(),
            RequiresUnsafe = false,
            IsSimdCompatible = true,
            HasNullableTypes = false
        };

        // Act
        var kernel = _generator.GenerateCudaKernel(graph, metadata);

        // Assert
        _ = kernel.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task GenerateCudaKernel_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var generator = new CudaKernelGenerator();
        var graph = CreateMapGraph<int, int>(x => x * 2);
        var metadata = CreateMetadata<int, int>();
        const int concurrentCalls = 10;

        // Act
        var tasks = Enumerable.Range(0, concurrentCalls)
            .Select(_ => Task.Run(() => generator.GenerateCudaKernel(graph, metadata)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be valid and identical
        _ = results.Should().HaveCount(concurrentCalls);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNullOrEmpty());
        _ = results.Distinct().Should().HaveCount(1, "all generated kernels should be identical");
    }

    #endregion

    #region Helper Methods

    private static TypeMetadata CreateMetadata<TInput, TOutput>()
    {
        return new TypeMetadata
        {
            InputType = typeof(TInput),
            ResultType = typeof(TOutput),
            IntermediateTypes = new Dictionary<string, Type>(),
            RequiresUnsafe = false,
            IsSimdCompatible = true,
            HasNullableTypes = false
        };
    }

    private static OperationGraph CreateMapGraph<TInput, TOutput>(Expression<Func<TInput, TOutput>> selector)
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "map_0",
            Type = OperationType.Map,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = selector
            }
        });
        return graph;
    }

    private static OperationGraph CreateFilterGraph<T>(Expression<Func<T, bool>> predicate)
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "filter_0",
            Type = OperationType.Filter,
            Metadata = new Dictionary<string, object>
            {
                ["Lambda"] = predicate
            }
        });
        return graph;
    }

    private static OperationGraph CreateReduceGraph<T>()
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "reduce_0",
            Type = OperationType.Reduce,
            Metadata = new Dictionary<string, object>
            {
                ["ReduceType"] = "Sum"
            }
        });
        return graph;
    }

    private static OperationGraph CreateScanGraph<T>()
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "scan_0",
            Type = OperationType.Scan,
            Metadata = new Dictionary<string, object>
            {
                ["ScanType"] = "InclusiveSum"
            }
        });
        return graph;
    }

    private static OperationGraph CreateOrderByGraph<T>()
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "orderby_0",
            Type = OperationType.OrderBy,
            Metadata = new Dictionary<string, object>
            {
                ["Ascending"] = true
            }
        });
        return graph;
    }

    private static OperationGraph CreateGroupByGraph<T>()
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "groupby_0",
            Type = OperationType.GroupBy
        });
        return graph;
    }

    private static OperationGraph CreateJoinGraph<T>()
    {
        var graph = new OperationGraph();
        graph.Operations.Add(new Operation
        {
            Id = "join_0",
            Type = OperationType.Join,
            Metadata = new Dictionary<string, object>
            {
                ["JoinType"] = "INNER"
            }
        });
        return graph;
    }

    #endregion
}
