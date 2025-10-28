// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Core.Optimization.Models;

namespace DotCompute.Core.Tests.Optimization;

/// <summary>
/// Comprehensive tests for WorkloadCharacteristics.
/// Tests workload pattern analysis, data size calculations, and characteristic modeling.
/// </summary>
public class WorkloadCharacteristicsTests
{
    #region Constructor and Property Tests

    [Fact]
    public void Constructor_CreatesInstanceWithDefaults()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics();

        // Assert
        _ = workload.Should().NotBeNull();
        _ = workload.DataSize.Should().Be(0);
        _ = workload.ComputeIntensity.Should().Be(0);
        _ = workload.MemoryIntensity.Should().Be(0);
        _ = workload.ParallelismLevel.Should().Be(0);
        _ = workload.OperationCount.Should().Be(0);
        _ = workload.AccessPattern.Should().Be(MemoryAccessPattern.Sequential);
        _ = workload.CustomCharacteristics.Should().NotBeNull();
        _ = workload.CustomCharacteristics.Should().BeEmpty();
        _ = workload.OptimizationHints.Should().NotBeNull();
        _ = workload.OptimizationHints.Should().BeEmpty();
    }

    [Fact]
    public void DataSize_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const long expectedSize = 1024 * 1024 * 10; // 10 MB

        // Act
        workload.DataSize = expectedSize;

        // Assert
        _ = workload.DataSize.Should().Be(expectedSize);
    }

    [Fact]
    public void ComputeIntensity_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const double expectedIntensity = 0.85;

        // Act
        workload.ComputeIntensity = expectedIntensity;

        // Assert
        _ = workload.ComputeIntensity.Should().Be(expectedIntensity);
    }

    [Fact]
    public void MemoryIntensity_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const double expectedIntensity = 0.75;

        // Act
        workload.MemoryIntensity = expectedIntensity;

        // Assert
        _ = workload.MemoryIntensity.Should().Be(expectedIntensity);
    }

    [Fact]
    public void ParallelismLevel_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const double expectedLevel = 0.95;

        // Act
        workload.ParallelismLevel = expectedLevel;

        // Assert
        _ = workload.ParallelismLevel.Should().Be(expectedLevel);
    }

    [Fact]
    public void OperationCount_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const long expectedCount = 1_000_000;

        // Act
        workload.OperationCount = expectedCount;

        // Assert
        _ = workload.OperationCount.Should().Be(expectedCount);
    }

    [Fact]
    public void AccessPattern_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const MemoryAccessPattern expectedPattern = MemoryAccessPattern.Strided;

        // Act
        workload.AccessPattern = expectedPattern;

        // Assert
        _ = workload.AccessPattern.Should().Be(expectedPattern);
    }

    #endregion

    #region OperationCountInt Property Tests

    [Fact]
    public void OperationCountInt_WithSmallValue_ReturnsExactValue()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            OperationCount = 1000
        };

        // Act
        var result = workload.OperationCountInt;

        // Assert
        _ = result.Should().Be(1000);
    }

    [Fact]
    public void OperationCountInt_WithLargeValue_ReturnsIntMaxValue()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            OperationCount = (long)int.MaxValue + 1000
        };

        // Act
        var result = workload.OperationCountInt;

        // Assert
        _ = result.Should().Be(int.MaxValue);
    }

    [Fact]
    public void OperationCountInt_WithMaxLongValue_ReturnsIntMaxValue()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            OperationCount = long.MaxValue
        };

        // Act
        var result = workload.OperationCountInt;

        // Assert
        _ = result.Should().Be(int.MaxValue);
    }

    [Fact]
    public void OperationCountInt_WithIntMaxValue_ReturnsExactValue()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            OperationCount = int.MaxValue
        };

        // Act
        var result = workload.OperationCountInt;

        // Assert
        _ = result.Should().Be(int.MaxValue);
    }

    #endregion

    #region ParallelismPotential Property Tests

    [Fact]
    public void ParallelismPotential_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        const double expectedPotential = 0.88;

        // Act
        workload.ParallelismPotential = expectedPotential;

        // Assert
        _ = workload.ParallelismPotential.Should().Be(expectedPotential);
    }

    [Fact]
    public void ParallelismPotential_DefaultsToZero()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics();

        // Assert
        _ = workload.ParallelismPotential.Should().Be(0);
    }

    #endregion

    #region CustomCharacteristics Dictionary Tests

    [Fact]
    public void CustomCharacteristics_CanAddKeyValuePairs()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();

        // Act
        workload.CustomCharacteristics["CacheSize"] = 64 * 1024;
        workload.CustomCharacteristics["BranchPrediction"] = true;
        workload.CustomCharacteristics["VectorWidth"] = 256;

        // Assert
        _ = workload.CustomCharacteristics.Should().HaveCount(3);
        _ = workload.CustomCharacteristics["CacheSize"].Should().Be(64 * 1024);
        _ = workload.CustomCharacteristics["BranchPrediction"].Should().Be(true);
        _ = workload.CustomCharacteristics["VectorWidth"].Should().Be(256);
    }

    [Fact]
    public void CustomCharacteristics_CanStoreComplexObjects()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        var complexObject = new { Name = "FFT", Stages = 8, Radix = 2 };

        // Act
        workload.CustomCharacteristics["AlgorithmDetails"] = complexObject;

        // Assert
        _ = workload.CustomCharacteristics.Should().ContainKey("AlgorithmDetails");
        _ = workload.CustomCharacteristics["AlgorithmDetails"].Should().Be(complexObject);
    }

    [Fact]
    public void CustomCharacteristics_InitializedAsEmptyDictionary()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics();

        // Assert
        _ = workload.CustomCharacteristics.Should().NotBeNull();
        _ = workload.CustomCharacteristics.Should().BeEmpty();
    }

    [Fact]
    public void CustomCharacteristics_CanBeModifiedAfterConstruction()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();

        // Act
        workload.CustomCharacteristics.Add("Key1", "Value1");
        workload.CustomCharacteristics.Add("Key2", 42);
        _ = workload.CustomCharacteristics.Remove("Key1");

        // Assert
        _ = workload.CustomCharacteristics.Should().HaveCount(1);
        _ = workload.CustomCharacteristics.Should().ContainKey("Key2");
        _ = workload.CustomCharacteristics.Should().NotContainKey("Key1");
    }

    #endregion

    #region OptimizationHints List Tests

    [Fact]
    public void OptimizationHints_CanAddHints()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();

        // Act
        workload.OptimizationHints.Add("use_shared_memory");
        workload.OptimizationHints.Add("vectorize");
        workload.OptimizationHints.Add("unroll_loops");

        // Assert
        _ = workload.OptimizationHints.Should().HaveCount(3);
        _ = workload.OptimizationHints.Should().Contain("use_shared_memory");
        _ = workload.OptimizationHints.Should().Contain("vectorize");
        _ = workload.OptimizationHints.Should().Contain("unroll_loops");
    }

    [Fact]
    public void OptimizationHints_InitializedAsEmptyList()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics();

        // Assert
        _ = workload.OptimizationHints.Should().NotBeNull();
        _ = workload.OptimizationHints.Should().BeEmpty();
    }

    [Fact]
    public void OptimizationHints_CanBeModifiedAfterConstruction()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();

        // Act
        workload.OptimizationHints.Add("hint1");
        workload.OptimizationHints.Add("hint2");
        _ = workload.OptimizationHints.Remove("hint1");

        // Assert
        _ = workload.OptimizationHints.Should().HaveCount(1);
        _ = workload.OptimizationHints.Should().Contain("hint2");
    }

    #endregion

    #region Realistic Workload Scenarios

    [Fact]
    public void WorkloadCharacteristics_ComputeIntensiveScenario()
    {
        // Arrange & Act - FFT computation
        var workload = new WorkloadCharacteristics
        {
            DataSize = 4 * 1024 * 1024, // 4 MB
            ComputeIntensity = 0.95,
            MemoryIntensity = 0.3,
            ParallelismLevel = 0.9,
            OperationCount = 2_000_000,
            AccessPattern = MemoryAccessPattern.Sequential
        };
        workload.OptimizationHints.Add("gpu_preferred");
        workload.OptimizationHints.Add("high_parallelism");

        // Assert
        _ = workload.ComputeIntensity.Should().BeGreaterThan(0.8);
        _ = workload.ParallelismLevel.Should().BeGreaterThan(0.8);
        _ = workload.OptimizationHints.Should().Contain("gpu_preferred");
    }

    [Fact]
    public void WorkloadCharacteristics_MemoryIntensiveScenario()
    {
        // Arrange & Act - Memory copy/transpose
        var workload = new WorkloadCharacteristics
        {
            DataSize = 100 * 1024 * 1024, // 100 MB
            ComputeIntensity = 0.1,
            MemoryIntensity = 0.95,
            ParallelismLevel = 0.4,
            OperationCount = 100_000,
            AccessPattern = MemoryAccessPattern.Strided
        };
        workload.OptimizationHints.Add("cpu_preferred");
        workload.OptimizationHints.Add("cache_optimization");

        // Assert
        _ = workload.MemoryIntensity.Should().BeGreaterThan(0.8);
        _ = workload.ComputeIntensity.Should().BeLessThan(0.3);
        _ = workload.OptimizationHints.Should().Contain("cpu_preferred");
    }

    [Fact]
    public void WorkloadCharacteristics_BalancedScenario()
    {
        // Arrange & Act - General matrix operations
        var workload = new WorkloadCharacteristics
        {
            DataSize = 16 * 1024 * 1024, // 16 MB
            ComputeIntensity = 0.6,
            MemoryIntensity = 0.6,
            ParallelismLevel = 0.75,
            OperationCount = 1_000_000,
            AccessPattern = MemoryAccessPattern.Coalesced
        };
        workload.OptimizationHints.Add("adaptive_backend");

        // Assert
        _ = workload.ComputeIntensity.Should().BeInRange(0.4, 0.8);
        _ = workload.MemoryIntensity.Should().BeInRange(0.4, 0.8);
    }

    [Fact]
    public void WorkloadCharacteristics_HighlyParallelScenario()
    {
        // Arrange & Act - Element-wise operations
        var workload = new WorkloadCharacteristics
        {
            DataSize = 8 * 1024 * 1024, // 8 MB
            ComputeIntensity = 0.4,
            MemoryIntensity = 0.5,
            ParallelismLevel = 0.98,
            ParallelismPotential = 0.99,
            OperationCount = 2_000_000,
            AccessPattern = MemoryAccessPattern.Sequential
        };
        workload.OptimizationHints.Add("simd_vectorization");
        workload.OptimizationHints.Add("max_parallelism");

        // Assert
        _ = workload.ParallelismLevel.Should().BeGreaterThan(0.9);
        _ = workload.ParallelismPotential.Should().BeGreaterThan(0.9);
        _ = workload.OptimizationHints.Should().Contain("max_parallelism");
    }

    [Fact]
    public void WorkloadCharacteristics_SequentialScenario()
    {
        // Arrange & Act - Sequential algorithm (e.g., recursive)
        var workload = new WorkloadCharacteristics
        {
            DataSize = 1024 * 1024, // 1 MB
            ComputeIntensity = 0.7,
            MemoryIntensity = 0.3,
            ParallelismLevel = 0.05,
            ParallelismPotential = 0.1,
            OperationCount = 500_000,
            AccessPattern = MemoryAccessPattern.Random
        };
        workload.OptimizationHints.Add("cpu_only");
        workload.OptimizationHints.Add("single_thread");

        // Assert
        _ = workload.ParallelismLevel.Should().BeLessThan(0.2);
        _ = workload.OptimizationHints.Should().Contain("cpu_only");
    }

    #endregion

    #region Memory Access Pattern Tests

    [Fact]
    public void AccessPattern_Sequential_IsDefault()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics();

        // Assert
        _ = workload.AccessPattern.Should().Be(MemoryAccessPattern.Sequential);
    }

    [Fact]
    public void AccessPattern_AllPatternsCanBeSet()
    {
        // Arrange & Act & Assert
        var workload1 = new WorkloadCharacteristics { AccessPattern = MemoryAccessPattern.Sequential };
        _ = workload1.AccessPattern.Should().Be(MemoryAccessPattern.Sequential);

        var workload2 = new WorkloadCharacteristics { AccessPattern = MemoryAccessPattern.Strided };
        _ = workload2.AccessPattern.Should().Be(MemoryAccessPattern.Strided);

        var workload3 = new WorkloadCharacteristics { AccessPattern = MemoryAccessPattern.Random };
        _ = workload3.AccessPattern.Should().Be(MemoryAccessPattern.Random);

        var workload4 = new WorkloadCharacteristics { AccessPattern = MemoryAccessPattern.Coalesced };
        _ = workload4.AccessPattern.Should().Be(MemoryAccessPattern.Coalesced);

        var workload5 = new WorkloadCharacteristics { AccessPattern = MemoryAccessPattern.ScatterGather };
        _ = workload5.AccessPattern.Should().Be(MemoryAccessPattern.ScatterGather);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void WorkloadCharacteristics_WithZeroDataSize()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics
        {
            DataSize = 0,
            ComputeIntensity = 0.5,
            MemoryIntensity = 0.5
        };

        // Assert
        _ = workload.DataSize.Should().Be(0);
        _ = workload.ComputeIntensity.Should().Be(0.5);
    }

    [Fact]
    public void WorkloadCharacteristics_WithMaximumValues()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics
        {
            DataSize = long.MaxValue,
            ComputeIntensity = 1.0,
            MemoryIntensity = 1.0,
            ParallelismLevel = 1.0,
            OperationCount = long.MaxValue
        };

        // Assert
        _ = workload.DataSize.Should().Be(long.MaxValue);
        _ = workload.ComputeIntensity.Should().Be(1.0);
        _ = workload.MemoryIntensity.Should().Be(1.0);
        _ = workload.ParallelismLevel.Should().Be(1.0);
    }

    [Fact]
    public void WorkloadCharacteristics_WithMinimumValues()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics
        {
            DataSize = 0,
            ComputeIntensity = 0.0,
            MemoryIntensity = 0.0,
            ParallelismLevel = 0.0,
            OperationCount = 0
        };

        // Assert
        _ = workload.DataSize.Should().Be(0);
        _ = workload.ComputeIntensity.Should().Be(0.0);
        _ = workload.MemoryIntensity.Should().Be(0.0);
        _ = workload.ParallelismLevel.Should().Be(0.0);
    }

    #endregion

    #region Hardware Property Tests

    [Fact]
    public void Hardware_CanBeSetAndRetrieved()
    {
        // Arrange
        var workload = new WorkloadCharacteristics();
        var hardwareInfo = new { GPU = "CUDA", Cores = 8, Memory = 16384 };

        // Act
        workload.Hardware = hardwareInfo;

        // Assert
        _ = workload.Hardware.Should().Be(hardwareInfo);
    }

    [Fact]
    public void Hardware_DefaultsToNull()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics();

        // Assert
        _ = workload.Hardware.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void WorkloadCharacteristics_FullyPopulatedInstance()
    {
        // Arrange & Act
        var workload = new WorkloadCharacteristics
        {
            DataSize = 50 * 1024 * 1024,
            ComputeIntensity = 0.8,
            MemoryIntensity = 0.6,
            ParallelismLevel = 0.9,
            ParallelismPotential = 0.95,
            OperationCount = 5_000_000,
            AccessPattern = MemoryAccessPattern.Coalesced,
            Hardware = new { Type = "GPU", Model = "RTX 2000" }
        };
        workload.CustomCharacteristics["Algorithm"] = "MatrixMultiply";
        workload.CustomCharacteristics["Precision"] = "Float32";
        workload.OptimizationHints.Add("use_tensor_cores");
        workload.OptimizationHints.Add("fma_operations");

        // Assert
        _ = workload.DataSize.Should().Be(50 * 1024 * 1024);
        _ = workload.ComputeIntensity.Should().Be(0.8);
        _ = workload.MemoryIntensity.Should().Be(0.6);
        _ = workload.ParallelismLevel.Should().Be(0.9);
        _ = workload.ParallelismPotential.Should().Be(0.95);
        _ = workload.OperationCount.Should().Be(5_000_000);
        _ = workload.OperationCountInt.Should().Be(5_000_000);
        _ = workload.AccessPattern.Should().Be(MemoryAccessPattern.Coalesced);
        _ = workload.Hardware.Should().NotBeNull();
        _ = workload.CustomCharacteristics.Should().HaveCount(2);
        _ = workload.OptimizationHints.Should().HaveCount(2);
    }

    #endregion
}
