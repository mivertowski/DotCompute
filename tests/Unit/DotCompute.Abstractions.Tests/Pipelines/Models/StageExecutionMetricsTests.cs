// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Abstractions.Types;
using DataTransferType = DotCompute.Abstractions.Pipelines.Models.DataTransferType;

namespace DotCompute.Abstractions.Tests.Pipelines.Models;

/// <summary>
/// Comprehensive tests for StageExecutionMetrics (482 lines).
/// Target: 25+ tests for stage-level performance tracking.
/// </summary>
public class StageExecutionMetricsTests
{
    [Fact]
    public void StageExecutionMetrics_RequiredProperties_MustBeSet()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.StageId.Should().Be("stage-1");
        metrics.StageName.Should().Be("Stage1");
        metrics.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StageExecutionMetrics_Duration_CalculatedCorrectly()
    {
        var start = DateTime.UtcNow;
        var end = start.AddSeconds(3);

        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = start,
            EndTime = end
        };

        metrics.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void StageExecutionMetrics_Duration_WithoutEndTime_ReturnsZero()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void StageExecutionMetrics_ProcessingRate_CalculatedCorrectly()
    {
        var start = DateTime.UtcNow;
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = start,
            EndTime = start.AddSeconds(2),
            InputDataSize = 2000
        };

        metrics.ProcessingRate.Should().BeApproximately(1000.0, 50.0); // ~1000 bytes/sec
    }

    [Fact]
    public void StageExecutionMetrics_ProcessingRate_WithZeroDuration_ReturnsZero()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow,
            InputDataSize = 1000
        };

        metrics.ProcessingRate.Should().Be(0.0);
    }

    [Fact]
    public void StageExecutionMetrics_AddCustomMetric_StoresValue()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.AddCustomMetric("CustomMetric", 42);
        metrics.CustomMetrics["CustomMetric"].Should().Be(42);
    }

    [Fact]
    public void StageExecutionMetrics_GetCustomMetric_ReturnsCorrectValue()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.AddCustomMetric("TestMetric", "test value");
        metrics.GetCustomMetric<string>("TestMetric").Should().Be("test value");
    }

    [Fact]
    public void StageExecutionMetrics_GetCustomMetric_WithNonExistentKey_ReturnsDefault()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.GetCustomMetric<int>("NonExistent").Should().Be(0);
    }

    [Fact]
    public void StageExecutionMetrics_AddWarning_FormatsWithTimestamp()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.AddWarning("Test warning");

        metrics.Warnings.Should().HaveCount(1);
        metrics.Warnings[0].Should().Contain("Test warning");
        metrics.Warnings[0].Should().ContainAll("[", "]"); // Timestamp format
    }

    [Fact]
    public void StageExecutionMetrics_AddDebugInfo_StoresKeyValue()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.AddDebugInfo("debugKey", "debugValue");
        metrics.DebugInfo["debugKey"].Should().Be("debugValue");
    }

    [Fact]
    public void StageExecutionMetrics_GetEfficiencyRatio_WithSkippedStage_ReturnsZero()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow,
            WasSkipped = true
        };

        metrics.GetEfficiencyRatio().Should().Be(0.0);
    }

    [Fact]
    public void StageExecutionMetrics_GetEfficiencyRatio_WithFailedStage_ReturnsZero()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow,
            Success = false
        };

        metrics.GetEfficiencyRatio().Should().Be(0.0);
    }

    [Fact]
    public void StageExecutionMetrics_GetEfficiencyRatio_WithCachedResult_ReturnsOne()
    {
        var metrics = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow,
            Success = true,
            WasCached = true
        };

        metrics.GetEfficiencyRatio().Should().Be(1.0);
    }

    [Fact]
    public void StageMemoryMetrics_AllProperties_CanBeSet()
    {
        var metrics = new StageMemoryMetrics
        {
            PeakMemoryUsage = 1024 * 1024,
            AverageMemoryUsage = 512 * 1024,
            AllocatedMemory = 2048 * 1024,
            AllocationCount = 50,
            DeallocationCount = 45,
            UsedMemoryPool = true,
            MemoryPoolHitRate = 0.90,
            FragmentationLevel = 0.15
        };

        metrics.PeakMemoryUsage.Should().Be(1024 * 1024);
        metrics.UsedMemoryPool.Should().BeTrue();
        metrics.MemoryPoolHitRate.Should().Be(0.90);
        metrics.FragmentationLevel.Should().Be(0.15);
    }

    [Fact]
    public void StageComputeMetrics_ComputeEfficiency_CalculatedCorrectly()
    {
        var metrics = new StageComputeMetrics
        {
            TheoreticalMaxThroughput = 1000.0,
            AchievedThroughput = 850.0
        };

        metrics.ComputeEfficiency.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public void StageComputeMetrics_ComputeEfficiency_WithZeroTheoretical_ReturnsZero()
    {
        var metrics = new StageComputeMetrics
        {
            TheoreticalMaxThroughput = 0.0,
            AchievedThroughput = 100.0
        };

        metrics.ComputeEfficiency.Should().Be(0.0);
    }

    [Fact]
    public void StageDataTransferMetrics_TransfersByType_InitializesEmpty()
    {
        var metrics = new StageDataTransferMetrics();
        metrics.TransfersByType.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void StageDataTransferMetrics_TransfersByType_CanAddEntries()
    {
        var metrics = new StageDataTransferMetrics();
        metrics.TransfersByType[DataTransferType.HostToDevice] = 1024;
        metrics.TransfersByType[DataTransferType.DeviceToHost] = 512;

        metrics.TransfersByType.Should().HaveCount(2);
        metrics.TransfersByType[DataTransferType.HostToDevice].Should().Be(1024);
    }

    [Fact]
    public void StagePerformanceMetrics_AllProperties_CanBeSet()
    {
        var metrics = new StagePerformanceMetrics
        {
            OperationsPerSecond = 10000.0,
            LatencyMs = 5.0,
            CacheHitRate = 0.85,
            InstructionCount = 1000000,
            InstructionsPerCycle = 2.5,
            BranchPredictionAccuracy = 0.95,
            EnergyConsumptionJ = 10.5
        };

        metrics.OperationsPerSecond.Should().Be(10000.0);
        metrics.LatencyMs.Should().Be(5.0);
        metrics.CacheHitRate.Should().Be(0.85);
        metrics.EnergyConsumptionJ.Should().Be(10.5);
    }

    [Fact]
    public void StageSynchronizationMetrics_AllProperties_CanBeSet()
    {
        var metrics = new StageSynchronizationMetrics
        {
            SynchronizationPointCount = 10,
            TotalSynchronizationTime = TimeSpan.FromMilliseconds(50),
            AverageSynchronizationWaitTime = TimeSpan.FromMilliseconds(5),
            MaxSynchronizationWaitTime = TimeSpan.FromMilliseconds(15),
            LoadBalancingEfficiency = 0.88,
            ParallelEfficiency = 0.92
        };

        metrics.SynchronizationPointCount.Should().Be(10);
        metrics.LoadBalancingEfficiency.Should().Be(0.88);
        metrics.ParallelEfficiency.Should().Be(0.92);
    }

    [Fact]
    public void StageOptimizationMetrics_AllProperties_CanBeSet()
    {
        var metrics = new StageOptimizationMetrics
        {
            AppliedOptimizations = OptimizationType.LoopOptimization | OptimizationType.Vectorization,
            OptimizationTime = TimeSpan.FromMilliseconds(20),
            PerformanceImprovementFactor = 1.5,
            MemoryImprovementFactor = 0.75,
            OptimizationValidationPassed = true
        };

        metrics.AppliedOptimizations.Should().HaveFlag(OptimizationType.LoopOptimization);
        metrics.PerformanceImprovementFactor.Should().Be(1.5);
        metrics.OptimizationValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void StageQualityMetrics_AllProperties_CanBeSet()
    {
        var metrics = new StageQualityMetrics
        {
            NumericalAccuracy = 0.9999,
            DeterminismScore = 1.0,
            CorrectnessValidationPassed = true,
            ConfidenceLevel = 0.95
        };

        metrics.QualityIssues.Add("Minor rounding detected");

        metrics.NumericalAccuracy.Should().Be(0.9999);
        metrics.DeterminismScore.Should().Be(1.0);
        metrics.CorrectnessValidationPassed.Should().BeTrue();
        metrics.ConfidenceLevel.Should().Be(0.95);
        metrics.QualityIssues.Should().HaveCount(1);
    }

    [Fact]
    public void StageExecutionMetrics_ComplexScenario_AllMetricsTogether()
    {
        var start = DateTime.UtcNow;
        var metrics = new StageExecutionMetrics
        {
            StageId = "complex-stage",
            StageName = "ComplexStage",
            StageType = PipelineStageType.Computation,
            StageIndex = 2,
            StartTime = start,
            EndTime = start.AddSeconds(5),
            Success = true,
            Backend = "CUDA",
            DeviceType = ComputeDeviceType.CUDA,
            RetryCount = 0,
            WasCached = false,
            InputDataSize = 1024 * 1024,
            OutputDataSize = 1024 * 1024,
            MemoryMetrics = new StageMemoryMetrics { PeakMemoryUsage = 2048 * 1024 },
            ComputeMetrics = new StageComputeMetrics { AverageGpuUtilization = 85.0 },
            DataTransferMetrics = new StageDataTransferMetrics { TotalBytesTransferred = 2 * 1024 * 1024 },
            PerformanceMetrics = new StagePerformanceMetrics { OperationsPerSecond = 50000 },
            QualityMetrics = new StageQualityMetrics { CorrectnessValidationPassed = true }
        };

        metrics.Duration.TotalSeconds.Should().BeGreaterThan(4);
        metrics.ProcessingRate.Should().BeGreaterThan(0);
        metrics.GetEfficiencyRatio().Should().BeLessThanOrEqualTo(1.0);
    }
}
