// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Types;
using TrendDirection = DotCompute.Abstractions.Types.TrendDirection;

namespace DotCompute.Abstractions.Tests.Debugging;

/// <summary>
/// Comprehensive tests for ProfilingTypes.cs (647 lines).
/// Target: 40+ tests for profiling infrastructure, counters, and timers.
/// </summary>
public class ProfilingTypesTests
{
    #region ProfilingSession Tests (10 tests)

    [Fact]
    public void ProfilingSession_DefaultConstructor_InitializesDefaults()
    {
        var session = new ProfilingSession();

        session.SessionId.Should().BeEmpty();
        session.KernelName.Should().BeEmpty();
        session.AcceleratorType.Should().Be(default);
        session.StartTime.Should().Be(default);
        session.IsActive.Should().BeTrue();
        session.Inputs.Should().NotBeNull().And.BeEmpty();
        session.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProfilingSession_AllProperties_CanBeSet()
    {
        var startTime = DateTime.UtcNow;
        var session = new ProfilingSession
        {
            SessionId = "session-123",
            KernelName = "VectorAdd",
            AcceleratorType = AcceleratorType.GPU,
            StartTime = startTime,
            IsActive = true,
            Inputs = new List<object> { 1, 2, 3 },
            Data = new List<ProfilingData>(),
            StartMemory = 1024,
            StartCpuTime = 10.5
        };

        session.SessionId.Should().Be("session-123");
        session.KernelName.Should().Be("VectorAdd");
        session.AcceleratorType.Should().Be(AcceleratorType.GPU);
        session.StartTime.Should().Be(startTime);
        session.IsActive.Should().BeTrue();
        session.Inputs.Should().HaveCount(3);
        session.StartMemory.Should().Be(1024);
        session.StartCpuTime.Should().Be(10.5);
    }

    [Fact]
    public void ProfilingSession_ElapsedTime_CalculatedCorrectly()
    {
        var session = new ProfilingSession
        {
            StartTime = DateTime.UtcNow.AddSeconds(-5)
        };

        session.ElapsedTime.Should().BeCloseTo(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ProfilingSession_Data_CanAddProfilingData()
    {
        var profilingData = new ProfilingData
        {
            Timestamp = DateTime.UtcNow,
            ExecutionTimeMs = 10.5,
            Success = true
        };

        var session = new ProfilingSession
        {
            Data = new List<ProfilingData> { profilingData }
        };

        session.Data.Should().HaveCount(1);
        session.Data[0].Should().BeSameAs(profilingData);
    }

    [Fact]
    public void ProfilingSession_IsActive_CanBeToggled()
    {
        var session = new ProfilingSession { IsActive = true };
        session.IsActive.Should().BeTrue();

        session.IsActive = false;
        session.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.GPU)]
    [InlineData(AcceleratorType.Auto)]
    public void ProfilingSession_AcceleratorType_SupportsAllTypes(AcceleratorType type)
    {
        var session = new ProfilingSession { AcceleratorType = type };
        session.AcceleratorType.Should().Be(type);
    }

    [Fact]
    public void ProfilingSession_Inputs_SupportsMultipleTypes()
    {
        var session = new ProfilingSession
        {
            Inputs = new List<object> { 42, "test", 3.14, true }
        };

        session.Inputs.Should().HaveCount(4);
        session.Inputs[0].Should().Be(42);
        session.Inputs[1].Should().Be("test");
        session.Inputs[2].Should().Be(3.14);
        session.Inputs[3].Should().Be(true);
    }

    [Fact]
    public void ProfilingSession_EmptySession_IsValid()
    {
        var session = new ProfilingSession
        {
            SessionId = "empty-session",
            KernelName = "EmptyKernel",
            StartTime = DateTime.UtcNow
        };

        session.Data.Should().BeEmpty();
        session.Inputs.Should().BeEmpty();
    }

    [Fact]
    public void ProfilingSession_LongRunningSession_TracksElapsedTime()
    {
        var session = new ProfilingSession
        {
            StartTime = DateTime.UtcNow.AddMinutes(-10)
        };

        session.ElapsedTime.TotalMinutes.Should().BeGreaterThan(9);
    }

    [Fact]
    public void ProfilingSession_WithMetrics_StoresMemoryAndCpu()
    {
        var session = new ProfilingSession
        {
            StartMemory = 2048 * 1024,
            StartCpuTime = 25.75
        };

        session.StartMemory.Should().Be(2048 * 1024);
        session.StartCpuTime.Should().Be(25.75);
    }

    #endregion

    #region ProfilingData Tests (12 tests)

    [Fact]
    public void ProfilingData_DefaultConstructor_InitializesDefaults()
    {
        var data = new ProfilingData();

        data.Timestamp.Should().Be(default);
        data.ExecutionTimeMs.Should().Be(0);
        data.MemoryUsageBytes.Should().Be(0);
        data.CpuUtilization.Should().Be(0);
        data.GpuUtilization.Should().Be(0);
        data.ThroughputOpsPerSecond.Should().Be(0);
        data.Success.Should().BeFalse();
        data.ErrorMessage.Should().BeNull();
        data.Metadata.Should().NotBeNull().And.BeEmpty();
        data.PerformanceMetrics.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProfilingData_AllProperties_CanBeSet()
    {
        var timestamp = DateTime.UtcNow;
        var data = new ProfilingData
        {
            Timestamp = timestamp,
            ExecutionTimeMs = 15.5,
            MemoryUsageBytes = 1024 * 1024,
            CpuUtilization = 75.0,
            GpuUtilization = 85.0,
            ThroughputOpsPerSecond = 10000.0,
            AcceleratorType = AcceleratorType.GPU,
            Success = true,
            SessionId = "session-1",
            KernelName = "TestKernel"
        };

        data.Timestamp.Should().Be(timestamp);
        data.ExecutionTimeMs.Should().Be(15.5);
        data.MemoryUsageBytes.Should().Be(1024 * 1024);
        data.CpuUtilization.Should().Be(75.0);
        data.GpuUtilization.Should().Be(85.0);
        data.ThroughputOpsPerSecond.Should().Be(10000.0);
        data.Success.Should().BeTrue();
    }

    [Fact]
    public void ProfilingData_ExecutionTime_CanBeDerived()
    {
        var start = DateTime.UtcNow;
        var end = start.AddMilliseconds(250);

        var data = new ProfilingData
        {
            StartTime = start,
            EndTime = end,
            ExecutionTime = end - start
        };

        data.ExecutionTime.Should().BeCloseTo(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void ProfilingData_MemoryUsage_CanBeDetailed()
    {
        var memoryData = new MemoryProfilingData
        {
            MemoryBefore = 1024,
            MemoryAfter = 2048,
            PeakMemory = 3072,
            AllocatedMemory = 1024
        };

        var data = new ProfilingData
        {
            MemoryUsageBytes = memoryData.PeakMemory,
            MemoryUsage = memoryData
        };

        data.MemoryUsageBytes.Should().Be(3072);
        data.MemoryUsage.Should().BeSameAs(memoryData);
    }

    [Fact]
    public void ProfilingData_CpuUsage_CanBeDetailed()
    {
        var cpuData = new CpuProfilingData
        {
            CpuUsagePercent = 80.0,
            UserTimeMs = 50.0,
            SystemTimeMs = 10.0,
            TotalTimeMs = 60.0
        };

        var data = new ProfilingData
        {
            CpuUtilization = cpuData.CpuUsagePercent,
            CpuUsage = cpuData
        };

        data.CpuUtilization.Should().Be(80.0);
        data.CpuUsage.Should().BeSameAs(cpuData);
    }

    [Fact]
    public void ProfilingData_Metadata_CanStoreCustomData()
    {
        var data = new ProfilingData();
        data.Metadata["customKey"] = "customValue";
        data.Metadata["iteration"] = 42;

        data.Metadata.Should().HaveCount(2);
        data.Metadata["customKey"].Should().Be("customValue");
        data.Metadata["iteration"].Should().Be(42);
    }

    [Fact]
    public void ProfilingData_PerformanceMetrics_CanStoreMetrics()
    {
        var data = new ProfilingData();
        data.PerformanceMetrics["latency"] = 5.5;
        data.PerformanceMetrics["bandwidth"] = 1024.0;

        data.PerformanceMetrics.Should().HaveCount(2);
        data.PerformanceMetrics["latency"].Should().Be(5.5);
    }

    [Fact]
    public void ProfilingData_WithError_StoresErrorInfo()
    {
        var exception = new InvalidOperationException("Test error");
        var data = new ProfilingData
        {
            Success = false,
            ErrorMessage = "Test error",
            Error = exception
        };

        data.Success.Should().BeFalse();
        data.ErrorMessage.Should().Be("Test error");
        data.Error.Should().BeSameAs(exception);
    }

    [Fact]
    public void ProfilingData_WithResult_StoresResult()
    {
        var result = new { Value = 42 };
        var data = new ProfilingData
        {
            Success = true,
            Result = result
        };

        data.Result.Should().BeSameAs(result);
    }

    [Fact]
    public void ProfilingData_UtilizationMetrics_InValidRange()
    {
        var data = new ProfilingData
        {
            CpuUtilization = 95.0,
            GpuUtilization = 100.0
        };

        data.CpuUtilization.Should().BeInRange(0, 100);
        data.GpuUtilization.Should().BeInRange(0, 100);
    }

    [Fact]
    public void ProfilingData_ThroughputMetrics_ArePositive()
    {
        var data = new ProfilingData
        {
            ThroughputOpsPerSecond = 50000.0
        };

        data.ThroughputOpsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProfilingData_CompleteExecutionRecord()
    {
        var start = DateTime.UtcNow;
        var data = new ProfilingData
        {
            SessionId = "session-123",
            KernelName = "VectorAdd",
            StartTime = start,
            EndTime = start.AddMilliseconds(100),
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            ExecutionTimeMs = 100.0,
            MemoryUsageBytes = 1024,
            CpuUtilization = 60.0,
            GpuUtilization = 80.0,
            ThroughputOpsPerSecond = 10000.0,
            AcceleratorType = AcceleratorType.GPU,
            Success = true
        };

        data.ExecutionTimeMs.Should().Be(100.0);
        data.Success.Should().BeTrue();
    }

    #endregion

    #region MemoryProfilingData Tests (6 tests)

    [Fact]
    public void MemoryProfilingData_AllProperties_CanBeSet()
    {
        var data = new MemoryProfilingData
        {
            MemoryBefore = 1024,
            MemoryAfter = 2048,
            PeakMemory = 3072,
            AllocatedMemory = 1024,
            StartMemory = 1024,
            EndMemory = 2048,
            GCCollections = 2
        };

        data.MemoryBefore.Should().Be(1024);
        data.MemoryAfter.Should().Be(2048);
        data.PeakMemory.Should().Be(3072);
        data.AllocatedMemory.Should().Be(1024);
        data.GCCollections.Should().Be(2);
    }

    [Fact]
    public void MemoryProfilingData_MemoryGrowth_CanBeCalculated()
    {
        var data = new MemoryProfilingData
        {
            MemoryBefore = 1024,
            MemoryAfter = 2048
        };

        var growth = data.MemoryAfter - data.MemoryBefore;
        growth.Should().Be(1024);
    }

    [Fact]
    public void MemoryProfilingData_PeakMemory_GreaterThanOrEqualToAfter()
    {
        var data = new MemoryProfilingData
        {
            MemoryAfter = 2048,
            PeakMemory = 3072
        };

        data.PeakMemory.Should().BeGreaterThanOrEqualTo(data.MemoryAfter);
    }

    [Fact]
    public void MemoryProfilingData_GCCollections_TracksGarbageCollection()
    {
        var data = new MemoryProfilingData { GCCollections = 5 };
        data.GCCollections.Should().Be(5);
    }

    [Fact]
    public void MemoryProfilingData_LargeAllocations()
    {
        var data = new MemoryProfilingData
        {
            AllocatedMemory = 1024L * 1024 * 1024, // 1 GB
            PeakMemory = 2048L * 1024 * 1024 // 2 GB
        };

        data.AllocatedMemory.Should().Be(1024L * 1024 * 1024);
        data.PeakMemory.Should().Be(2048L * 1024 * 1024);
    }

    [Fact]
    public void MemoryProfilingData_StartEndMemory_ConsistentWithBeforeAfter()
    {
        var data = new MemoryProfilingData
        {
            MemoryBefore = 1000,
            StartMemory = 1000,
            MemoryAfter = 1500,
            EndMemory = 1500
        };

        data.MemoryBefore.Should().Be(data.StartMemory);
        data.MemoryAfter.Should().Be(data.EndMemory);
    }

    #endregion

    #region CpuProfilingData Tests (6 tests)

    [Fact]
    public void CpuProfilingData_AllProperties_CanBeSet()
    {
        var data = new CpuProfilingData
        {
            CpuUsagePercent = 75.0,
            StartCpuTime = 10.0,
            EndCpuTime = 20.0,
            CpuTime = TimeSpan.FromMilliseconds(10),
            CpuUtilization = TimeSpan.FromMilliseconds(10),
            UserTimeMs = 8.0,
            SystemTimeMs = 2.0,
            TotalTimeMs = 10.0,
            ContextSwitches = 100,
            PageFaults = 5
        };

        data.CpuUsagePercent.Should().Be(75.0);
        data.UserTimeMs.Should().Be(8.0);
        data.SystemTimeMs.Should().Be(2.0);
        data.TotalTimeMs.Should().Be(10.0);
        data.ContextSwitches.Should().Be(100);
        data.PageFaults.Should().Be(5);
    }

    [Fact]
    public void CpuProfilingData_TotalTime_SumOfUserAndSystem()
    {
        var data = new CpuProfilingData
        {
            UserTimeMs = 8.0,
            SystemTimeMs = 2.0,
            TotalTimeMs = 10.0
        };

        data.TotalTimeMs.Should().BeApproximately(data.UserTimeMs + data.SystemTimeMs, 0.01);
    }

    [Fact]
    public void CpuProfilingData_CpuUtilization_InTimeSpan()
    {
        var data = new CpuProfilingData
        {
            CpuUtilization = TimeSpan.FromSeconds(5)
        };

        data.CpuUtilization.TotalSeconds.Should().Be(5);
    }

    [Fact]
    public void CpuProfilingData_ContextSwitches_TracksThreading()
    {
        var data = new CpuProfilingData
        {
            ContextSwitches = 1000
        };

        data.ContextSwitches.Should().Be(1000);
    }

    [Fact]
    public void CpuProfilingData_PageFaults_TracksMemoryAccess()
    {
        var data = new CpuProfilingData
        {
            PageFaults = 25
        };

        data.PageFaults.Should().Be(25);
    }

    [Fact]
    public void CpuProfilingData_CpuUsagePercent_InValidRange()
    {
        var data = new CpuProfilingData
        {
            CpuUsagePercent = 85.5
        };

        data.CpuUsagePercent.Should().BeInRange(0, 100);
    }

    #endregion

    #region PerformanceAnalysis Tests (6 tests)

    [Fact]
    public void PerformanceAnalysis_AllProperties_CanBeSet()
    {
        var analysis = new PerformanceAnalysis
        {
            KernelName = "VectorAdd",
            AverageExecutionTimeMs = 10.5,
            MinExecutionTimeMs = 8.0,
            MaxExecutionTimeMs = 15.0,
            ExecutionTimeStdDev = 2.0,
            AverageMemoryUsage = 1024 * 1024,
            PeakMemoryUsage = 2048 * 1024,
            AverageThroughput = 10000.0,
            DataPointCount = 100,
            AnalysisTimeRange = TimeSpan.FromHours(1),
            AcceleratorType = AcceleratorType.GPU,
            DataPoints = 100,
            AnalysisTime = DateTime.UtcNow
        };

        analysis.KernelName.Should().Be("VectorAdd");
        analysis.AverageExecutionTimeMs.Should().Be(10.5);
        analysis.MinExecutionTimeMs.Should().Be(8.0);
        analysis.MaxExecutionTimeMs.Should().Be(15.0);
        analysis.DataPointCount.Should().Be(100);
    }

    [Fact]
    public void PerformanceAnalysis_Trends_CanStoreMultipleTrends()
    {
        var trends = new List<PerformanceTrend>
        {
            new() { TrendDirection = TrendDirection.Improving, MetricName = "ExecutionTime" },
            new() { TrendDirection = TrendDirection.Stable, MetricName = "MemoryUsage" }
        };

        var analysis = new PerformanceAnalysis
        {
            Trends = trends
        };

        analysis.Trends.Should().HaveCount(2);
        analysis.Trends[0].Direction.Should().Be(TrendDirection.Improving);
    }

    [Fact]
    public void PerformanceAnalysis_Anomalies_CanDetectIssues()
    {
        var anomalies = new List<PerformanceAnomaly>
        {
            new() { Type = AnomalyType.PerformanceSpike, Severity = AnomalySeverity.High }
        };

        var analysis = new PerformanceAnalysis
        {
            Anomalies = anomalies
        };

        analysis.Anomalies.Should().HaveCount(1);
        analysis.Anomalies[0].Type.Should().Be(AnomalyType.PerformanceSpike);
    }

    [Fact]
    public void PerformanceAnalysis_StatisticalMetrics_AreConsistent()
    {
        var analysis = new PerformanceAnalysis
        {
            MinExecutionTimeMs = 5.0,
            AverageExecutionTimeMs = 10.0,
            MaxExecutionTimeMs = 15.0
        };

        analysis.MinExecutionTimeMs.Should().BeLessThanOrEqualTo(analysis.AverageExecutionTimeMs);
        analysis.AverageExecutionTimeMs.Should().BeLessThanOrEqualTo(analysis.MaxExecutionTimeMs);
    }

    [Fact]
    public void PerformanceAnalysis_AnalysisTime_RecordsWhenAnalyzed()
    {
        var analysis = new PerformanceAnalysis
        {
            AnalysisTime = DateTime.UtcNow
        };

        analysis.AnalysisTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PerformanceAnalysis_DataPointCount_MatchesDataPoints()
    {
        var analysis = new PerformanceAnalysis
        {
            DataPointCount = 50,
            DataPoints = 50
        };

        analysis.DataPointCount.Should().Be(analysis.DataPoints);
    }

    #endregion
}
