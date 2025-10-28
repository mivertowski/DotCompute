// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DataTransferType = DotCompute.Abstractions.Pipelines.Models.DataTransferType;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Tests.Pipelines.Models;

/// <summary>
/// Comprehensive tests for PipelineExecutionMetrics (582 lines).
/// Target: 30+ tests for metrics creation, aggregation, and serialization.
/// </summary>
public class PipelineExecutionMetricsTests
{
    [Fact]
    public void PipelineExecutionMetrics_RequiredProperties_MustBeSet()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "TestPipeline",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.ExecutionId.Should().Be("exec-123");
        metrics.PipelineName.Should().Be("TestPipeline");
        metrics.StartTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PipelineExecutionMetrics_Duration_CalculatedCorrectly()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddSeconds(5);

        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = start,
            EndTime = end
        };

        metrics.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void PipelineExecutionMetrics_Duration_WithoutEndTime_ReturnsZero()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void PipelineExecutionMetrics_IsSuccessful_DefaultsFalse()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public void PipelineExecutionMetrics_Success_ObsoleteProperty_WorksCorrectly()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow,
            Success = true
        };

        metrics.Success.Should().BeTrue();
        metrics.IsSuccessful.Should().BeTrue();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void PipelineExecutionMetrics_StageMetrics_InitializesEmpty()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.StageMetrics.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void PipelineExecutionMetrics_StageMetrics_CanAddItems()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        var stageMetric = new StageExecutionMetrics
        {
            StageId = "stage-1",
            StageName = "Stage1",
            StartTime = DateTime.UtcNow
        };

        metrics.StageMetrics.Add(stageMetric);
        metrics.StageMetrics.Should().HaveCount(1);
        metrics.StageMetrics[0].Should().BeSameAs(stageMetric);
    }

    [Fact]
    public void PipelineExecutionMetrics_PeakMemoryUsage_WithMemoryMetrics_ReturnsCorrectValue()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow,
            MemoryMetrics = new MemoryUsageMetrics { PeakMemoryUsage = 1024 * 1024 }
        };

        metrics.PeakMemoryUsage.Should().Be(1024 * 1024);
    }

    [Fact]
    public void PipelineExecutionMetrics_PeakMemoryUsage_WithoutMemoryMetrics_ReturnsZero()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.PeakMemoryUsage.Should().Be(0);
    }

    [Fact]
    public void PipelineExecutionMetrics_AddCustomMetric_StoresValue()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.AddCustomMetric("TestMetric", 42);
        metrics.CustomMetrics["TestMetric"].Should().Be(42);
    }

    [Fact]
    public void PipelineExecutionMetrics_GetCustomMetric_ReturnsCorrectType()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.AddCustomMetric("IntMetric", 42);
        metrics.AddCustomMetric("StringMetric", "test");

        metrics.GetCustomMetric<int>("IntMetric").Should().Be(42);
        metrics.GetCustomMetric<string>("StringMetric").Should().Be("test");
    }

    [Fact]
    public void PipelineExecutionMetrics_GetCustomMetric_WithInvalidKey_ReturnsDefault()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.GetCustomMetric<int>("NonExistent").Should().Be(0);
        metrics.GetCustomMetric<string>("NonExistent").Should().BeNull();
    }

    [Fact]
    public void PipelineExecutionMetrics_GetAverageStageTime_WithNoStages_ReturnsZero()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.GetAverageStageTime().Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void PipelineExecutionMetrics_GetAverageStageTime_WithStages_CalculatesCorrectly()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        var now = DateTime.UtcNow;
        metrics.StageMetrics.Add(new StageExecutionMetrics
        {
            StageId = "1",
            StageName = "Stage1",
            StartTime = now,
            EndTime = now.AddSeconds(2)
        });
        metrics.StageMetrics.Add(new StageExecutionMetrics
        {
            StageId = "2",
            StageName = "Stage2",
            StartTime = now,
            EndTime = now.AddSeconds(4)
        });

        var avgTime = metrics.GetAverageStageTime();
        avgTime.Should().BeCloseTo(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void PipelineExecutionMetrics_GetLongestStage_WithNoStages_ReturnsNull()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        metrics.GetLongestStage().Should().BeNull();
    }

    [Fact]
    public void PipelineExecutionMetrics_GetLongestStage_ReturnsCorrectStage()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };

        var now = DateTime.UtcNow;
        var shortStage = new StageExecutionMetrics
        {
            StageId = "1",
            StageName = "ShortStage",
            StartTime = now,
            EndTime = now.AddSeconds(1)
        };
        var longStage = new StageExecutionMetrics
        {
            StageId = "2",
            StageName = "LongStage",
            StartTime = now,
            EndTime = now.AddSeconds(5)
        };

        metrics.StageMetrics.Add(shortStage);
        metrics.StageMetrics.Add(longStage);

        var longest = metrics.GetLongestStage();
        longest.Should().BeSameAs(longStage);
    }

    [Fact]
    public void PipelineExecutionMetrics_GetEfficiencyRatio_WithNoStages_ReturnsZero()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow,
            TotalStages = 0
        };

        metrics.GetEfficiencyRatio().Should().Be(0.0);
    }

    [Fact]
    public void PipelineExecutionMetrics_GetEfficiencyRatio_CalculatesCorrectly()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-123",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow,
            TotalStages = 10,
            CompletedStages = 8
        };

        metrics.GetEfficiencyRatio().Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void MemoryUsageMetrics_AllProperties_CanBeSet()
    {
        var metrics = new MemoryUsageMetrics
        {
            PeakMemoryUsage = 1024 * 1024,
            AverageMemoryUsage = 512 * 1024,
            TotalMemoryAllocated = 2048 * 1024,
            AllocationCount = 100,
            GarbageCollectionCount = 5,
            MemoryPoolingUsed = true,
            MemoryPoolHitRate = 0.85
        };

        metrics.PeakMemoryUsage.Should().Be(1024 * 1024);
        metrics.AverageMemoryUsage.Should().Be(512 * 1024);
        metrics.TotalMemoryAllocated.Should().Be(2048 * 1024);
        metrics.AllocationCount.Should().Be(100);
        metrics.GarbageCollectionCount.Should().Be(5);
        metrics.MemoryPoolingUsed.Should().BeTrue();
        metrics.MemoryPoolHitRate.Should().Be(0.85);
    }

    [Fact]
    public void ComputeResourceMetrics_AllProperties_CanBeSet()
    {
        var metrics = new ComputeResourceMetrics
        {
            AverageCpuUtilization = 75.5,
            PeakCpuUtilization = 95.0,
            AverageGpuUtilization = 60.0,
            PeakGpuUtilization = 80.0,
            ComputeUnitsUsed = 8,
            ThreadCount = 16,
            DegreeOfParallelism = 4
        };

        metrics.AverageCpuUtilization.Should().Be(75.5);
        metrics.PeakCpuUtilization.Should().Be(95.0);
        metrics.AverageGpuUtilization.Should().Be(60.0);
        metrics.PeakGpuUtilization.Should().Be(80.0);
        metrics.ComputeUnitsUsed.Should().Be(8);
        metrics.ThreadCount.Should().Be(16);
        metrics.DegreeOfParallelism.Should().Be(4);
    }

    [Fact]
    public void DataTransferMetrics_TransfersByType_InitializesEmpty()
    {
        var metrics = new DataTransferMetrics();
        metrics.TransfersByType.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void DataTransferMetrics_TransfersByType_CanAddItems()
    {
        var metrics = new DataTransferMetrics();
        var typeMetrics = new TransferTypeMetrics
        {
            Count = 10,
            TotalBytes = 1024,
            TotalTime = TimeSpan.FromMilliseconds(100),
            AverageRate = 1024 / 0.1
        };

        metrics.TransfersByType[DataTransferType.HostToDevice] = typeMetrics;
        metrics.TransfersByType.Should().HaveCount(1);
        metrics.TransfersByType[DataTransferType.HostToDevice].Should().BeSameAs(typeMetrics);
    }

    [Fact]
    public void CacheMetrics_HitRate_CalculatedCorrectly()
    {
        var metrics = new CacheMetrics
        {
            TotalAccesses = 100,
            CacheHits = 85,
            CacheMisses = 15
        };

        metrics.HitRate.Should().BeApproximately(85.0, 0.01);
    }

    [Fact]
    public void CacheMetrics_HitRate_WithZeroAccesses_ReturnsZero()
    {
        var metrics = new CacheMetrics
        {
            TotalAccesses = 0,
            CacheHits = 0,
            CacheMisses = 0
        };

        metrics.HitRate.Should().Be(0.0);
    }

    [Fact]
    public void OptimizationMetrics_AllProperties_CanBeSet()
    {
        var metrics = new OptimizationMetrics
        {
            OptimizationTime = TimeSpan.FromMilliseconds(50),
            PerformanceImprovement = 1.5,
            MemoryImprovement = 0.8,
            AppliedOptimizations = OptimizationType.KernelFusion | OptimizationType.MemoryAccess,
            ValidationPassed = true
        };

        metrics.OptimizationTime.Should().Be(TimeSpan.FromMilliseconds(50));
        metrics.PerformanceImprovement.Should().Be(1.5);
        metrics.MemoryImprovement.Should().Be(0.8);
        metrics.AppliedOptimizations.Should().HaveFlag(OptimizationType.KernelFusion);
        metrics.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void ParallelizationMetrics_AllProperties_CanBeSet()
    {
        var metrics = new ParallelizationMetrics
        {
            MaxDegreeOfParallelism = 8,
            AverageDegreeOfParallelism = 6.5,
            ParallelEfficiency = 0.85,
            LoadBalancingEfficiency = 0.90,
            SynchronizationPoints = 5,
            SynchronizationTime = TimeSpan.FromMilliseconds(20)
        };

        metrics.MaxDegreeOfParallelism.Should().Be(8);
        metrics.AverageDegreeOfParallelism.Should().Be(6.5);
        metrics.ParallelEfficiency.Should().Be(0.85);
        metrics.LoadBalancingEfficiency.Should().Be(0.90);
        metrics.SynchronizationPoints.Should().Be(5);
        metrics.SynchronizationTime.Should().Be(TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public void QualityMetrics_AllProperties_CanBeSet()
    {
        var metrics = new QualityMetrics
        {
            NumericalAccuracy = 0.9999,
            DeterminismScore = 1.0,
            CorrectnessVerified = true
        };

        metrics.QualityWarnings.Add("Minor precision loss detected");

        metrics.NumericalAccuracy.Should().Be(0.9999);
        metrics.DeterminismScore.Should().Be(1.0);
        metrics.CorrectnessVerified.Should().BeTrue();
        metrics.QualityWarnings.Should().HaveCount(1);
    }

    [Fact]
    public void PipelineExecutionMetrics_ComplexScenario_AllMetricsTogether()
    {
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-complex",
            PipelineName = "ComplexPipeline",
            StartTime = DateTimeOffset.UtcNow.AddSeconds(-10),
            EndTime = DateTimeOffset.UtcNow,
            IsSuccessful = true,
            TotalStages = 5,
            CompletedStages = 5,
            FailedStages = 0,
            Backend = "CUDA",
            DeviceType = ComputeDeviceType.CUDA,
            MemoryMetrics = new MemoryUsageMetrics { PeakMemoryUsage = 1024 * 1024 },
            ComputeMetrics = new ComputeResourceMetrics { AverageGpuUtilization = 85.0 },
            CacheMetrics = new CacheMetrics { TotalAccesses = 100, CacheHits = 90 },
            OptimizationMetrics = new OptimizationMetrics { PerformanceImprovement = 1.8 },
            ParallelizationMetrics = new ParallelizationMetrics { ParallelEfficiency = 0.92 },
            QualityMetrics = new QualityMetrics { CorrectnessVerified = true }
        };

        metrics.Duration.TotalSeconds.Should().BeGreaterThan(9);
        metrics.IsSuccessful.Should().BeTrue();
        metrics.GetEfficiencyRatio().Should().Be(1.0);
        metrics.PeakMemoryUsage.Should().Be(1024 * 1024);
    }

    [Theory]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.DataTransferType.HostToDevice)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.DataTransferType.DeviceToHost)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.DataTransferType.DeviceToDevice)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.DataTransferType.PeerToPeer)]
    public void DataTransferType_AllValues_Exist(DotCompute.Abstractions.Pipelines.Enums.DataTransferType type)
    {
        type.Should().BeDefined();
    }
}
