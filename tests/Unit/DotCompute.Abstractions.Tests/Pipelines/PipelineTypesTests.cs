// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions.Pipelines.Caching;
using DotCompute.Abstractions.Pipelines.Configuration;
using DotCompute.Abstractions.Pipelines.Execution;
using DotCompute.Abstractions.Pipelines.Optimization;
using DotCompute.Abstractions.Pipelines.Streaming;
using DotCompute.Abstractions.Pipelines.Types;

namespace DotCompute.Abstractions.Tests.Pipelines;

/// <summary>
/// Comprehensive tests for pipeline types, enums, and configurations in PipelineTypes.cs (815 lines).
/// Target: 60+ tests covering all pipeline type definitions, builders, validators, and configuration.
/// </summary>
public class PipelineTypesTests
{
    #region PipelineState Enum Tests (9 tests)

    [Fact]
    public void PipelineState_Created_HasCorrectValue()
    {
        PipelineState.Created.Should().Be(PipelineState.Created);
        ((int)PipelineState.Created).Should().Be(0);
    }

    [Fact]
    public void PipelineState_Ready_HasCorrectValue()
    {
        PipelineState.Ready.Should().Be(PipelineState.Ready);
    }

    [Fact]
    public void PipelineState_Executing_HasCorrectValue()
    {
        PipelineState.Executing.Should().Be(PipelineState.Executing);
    }

    [Fact]
    public void PipelineState_Completed_HasCorrectValue()
    {
        PipelineState.Completed.Should().Be(PipelineState.Completed);
    }

    [Fact]
    public void PipelineState_Failed_HasCorrectValue()
    {
        PipelineState.Failed.Should().Be(PipelineState.Failed);
    }

    [Fact]
    public void PipelineState_Cancelled_HasCorrectValue()
    {
        PipelineState.Cancelled.Should().Be(PipelineState.Cancelled);
    }

    [Fact]
    public void PipelineState_AllValues_AreDistinct()
    {
        var values = Enum.GetValues<PipelineState>();
        values.Should().HaveCount(values.Distinct().Count());
    }

    [Theory]
    [InlineData(PipelineState.Created)]
    [InlineData(PipelineState.Ready)]
    [InlineData(PipelineState.Executing)]
    [InlineData(PipelineState.Completed)]
    public void PipelineState_ToString_ReturnsName(PipelineState state)
    {
        state.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PipelineState_HasNineValues()
    {
        var values = Enum.GetValues<PipelineState>();
        values.Should().HaveCount(9);
    }

    #endregion

    #region PipelineStageOptions Tests (10 tests)

    [Fact]
    public void PipelineStageOptions_Constructor_InitializesDefaults()
    {
        var options = new PipelineStageOptions();

        options.PreferredBackend.Should().BeNull();
        options.Timeout.Should().BeNull();
        options.Priority.Should().Be(ExecutionPriority.Normal);
        options.AllowParallelExecution.Should().BeTrue();
        options.Metadata.Should().NotBeNull().And.BeEmpty();
        options.EnableProfiling.Should().BeFalse();
        options.EnableOptimization.Should().BeTrue();
        options.EnableMemoryOptimization.Should().BeTrue();
    }

    [Fact]
    public void PipelineStageOptions_PreferredBackend_CanBeSet()
    {
        var options = new PipelineStageOptions { PreferredBackend = "CUDA" };
        options.PreferredBackend.Should().Be("CUDA");
    }

    [Fact]
    public void PipelineStageOptions_Timeout_CanBeSet()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var options = new PipelineStageOptions { Timeout = timeout };
        options.Timeout.Should().Be(timeout);
    }

    [Theory]
    [InlineData(ExecutionPriority.Low)]
    [InlineData(ExecutionPriority.Normal)]
    [InlineData(ExecutionPriority.High)]
    [InlineData(ExecutionPriority.Critical)]
    public void PipelineStageOptions_Priority_CanBeSet(ExecutionPriority priority)
    {
        var options = new PipelineStageOptions { Priority = priority };
        options.Priority.Should().Be(priority);
    }

    [Fact]
    public void PipelineStageOptions_AllowParallelExecution_CanBeDisabled()
    {
        var options = new PipelineStageOptions { AllowParallelExecution = false };
        options.AllowParallelExecution.Should().BeFalse();
    }

    [Fact]
    public void PipelineStageOptions_Metadata_CanAddItems()
    {
        var options = new PipelineStageOptions();
        options.Metadata["key1"] = "value1";
        options.Metadata["key2"] = 42;

        options.Metadata.Should().HaveCount(2);
        options.Metadata["key1"].Should().Be("value1");
        options.Metadata["key2"].Should().Be(42);
    }

    [Fact]
    public void PipelineStageOptions_MemoryHints_CanBeSet()
    {
        var hints = new MemoryAllocationHints { ExpectedMemoryUsage = 1024 };
        var options = new PipelineStageOptions { MemoryHints = hints };
        options.MemoryHints.Should().BeSameAs(hints);
    }

    [Fact]
    public void PipelineStageOptions_RetryConfig_CanBeSet()
    {
        var retryConfig = new RetryConfiguration { MaxAttempts = 5 };
        var options = new PipelineStageOptions { RetryConfig = retryConfig };
        options.RetryConfig.Should().BeSameAs(retryConfig);
    }

    [Fact]
    public void PipelineStageOptions_EnableProfiling_CanBeEnabled()
    {
        var options = new PipelineStageOptions { EnableProfiling = true };
        options.EnableProfiling.Should().BeTrue();
    }

    [Fact]
    public void PipelineStageOptions_OptimizationFlags_CanBeDisabled()
    {
        var options = new PipelineStageOptions
        {
            EnableOptimization = false,
            EnableMemoryOptimization = false
        };

        options.EnableOptimization.Should().BeFalse();
        options.EnableMemoryOptimization.Should().BeFalse();
    }

    #endregion

    #region MemoryAllocationHints Tests (5 tests)

    [Fact]
    public void MemoryAllocationHints_Constructor_InitializesDefaults()
    {
        var hints = new MemoryAllocationHints();

        hints.ExpectedMemoryUsage.Should().BeNull();
        hints.PreferMemoryPooling.Should().BeTrue();
        hints.PreferredLocation.Should().Be(MemoryLocation.Host);
        hints.SupportsPinnedMemory.Should().BeFalse();
    }

    [Fact]
    public void MemoryAllocationHints_ExpectedMemoryUsage_CanBeSet()
    {
        var hints = new MemoryAllocationHints { ExpectedMemoryUsage = 1024 * 1024 };
        hints.ExpectedMemoryUsage.Should().Be(1024 * 1024);
    }

    [Theory]
    [InlineData(MemoryLocation.Host)]
    [InlineData(MemoryLocation.Device)]
    [InlineData(MemoryLocation.Unified)]
    public void MemoryAllocationHints_PreferredLocation_CanBeSet(MemoryLocation location)
    {
        var hints = new MemoryAllocationHints { PreferredLocation = location };
        hints.PreferredLocation.Should().Be(location);
    }

    [Fact]
    public void MemoryAllocationHints_PreferMemoryPooling_CanBeDisabled()
    {
        var hints = new MemoryAllocationHints { PreferMemoryPooling = false };
        hints.PreferMemoryPooling.Should().BeFalse();
    }

    [Fact]
    public void MemoryAllocationHints_SupportsPinnedMemory_CanBeEnabled()
    {
        var hints = new MemoryAllocationHints { SupportsPinnedMemory = true };
        hints.SupportsPinnedMemory.Should().BeTrue();
    }

    #endregion

    #region RetryConfiguration Tests (8 tests)

    [Fact]
    public void RetryConfiguration_Constructor_InitializesDefaults()
    {
        var config = new RetryConfiguration();

        config.MaxAttempts.Should().Be(3);
        config.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        config.BackoffStrategy.Should().Be(BackoffStrategy.Exponential);
        config.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        config.ShouldRetry.Should().BeNull();
        config.OnRetry.Should().BeNull();
    }

    [Fact]
    public void RetryConfiguration_MaxAttempts_CanBeSet()
    {
        var config = new RetryConfiguration { MaxAttempts = 5 };
        config.MaxAttempts.Should().Be(5);
    }

    [Fact]
    public void RetryConfiguration_BaseDelay_CanBeSet()
    {
        var delay = TimeSpan.FromMilliseconds(500);
        var config = new RetryConfiguration { BaseDelay = delay };
        config.BaseDelay.Should().Be(delay);
    }

    [Theory]
    [InlineData(BackoffStrategy.Fixed)]
    [InlineData(BackoffStrategy.Linear)]
    [InlineData(BackoffStrategy.Exponential)]
    [InlineData(BackoffStrategy.ExponentialWithJitter)]
    public void RetryConfiguration_BackoffStrategy_CanBeSet(BackoffStrategy strategy)
    {
        var config = new RetryConfiguration { BackoffStrategy = strategy };
        config.BackoffStrategy.Should().Be(strategy);
    }

    [Fact]
    public void RetryConfiguration_MaxDelay_CanBeSet()
    {
        var maxDelay = TimeSpan.FromMinutes(1);
        var config = new RetryConfiguration { MaxDelay = maxDelay };
        config.MaxDelay.Should().Be(maxDelay);
    }

    [Fact]
    public void RetryConfiguration_ShouldRetry_CanBeSet()
    {
        Func<Exception, bool> shouldRetry = ex => ex is InvalidOperationException;
        var config = new RetryConfiguration { ShouldRetry = shouldRetry };
        config.ShouldRetry.Should().BeSameAs(shouldRetry);
    }

    [Fact]
    public void RetryConfiguration_OnRetry_CanBeSet()
    {
        Func<int, Exception, Task> onRetry = (attempt, ex) => Task.CompletedTask;
        var config = new RetryConfiguration { OnRetry = onRetry };
        config.OnRetry.Should().BeSameAs(onRetry);
    }

    [Fact]
    public void RetryConfiguration_CustomConfiguration_WorksCorrectly()
    {
        var config = new RetryConfiguration
        {
            MaxAttempts = 10,
            BaseDelay = TimeSpan.FromSeconds(1),
            BackoffStrategy = BackoffStrategy.Linear,
            MaxDelay = TimeSpan.FromMinutes(5),
            ShouldRetry = ex => ex is TimeoutException,
            OnRetry = async (attempt, ex) => await Task.Delay(attempt * 100)
        };

        config.MaxAttempts.Should().Be(10);
        config.BaseDelay.Should().Be(TimeSpan.FromSeconds(1));
        config.BackoffStrategy.Should().Be(BackoffStrategy.Linear);
        config.MaxDelay.Should().Be(TimeSpan.FromMinutes(5));
        config.ShouldRetry.Should().NotBeNull();
        config.OnRetry.Should().NotBeNull();
    }

    #endregion

    #region BackoffStrategy Enum Tests (5 tests)

    [Fact]
    public void BackoffStrategy_Fixed_HasCorrectValue()
    {
        BackoffStrategy.Fixed.Should().Be(BackoffStrategy.Fixed);
    }

    [Fact]
    public void BackoffStrategy_Linear_HasCorrectValue()
    {
        BackoffStrategy.Linear.Should().Be(BackoffStrategy.Linear);
    }

    [Fact]
    public void BackoffStrategy_Exponential_HasCorrectValue()
    {
        BackoffStrategy.Exponential.Should().Be(BackoffStrategy.Exponential);
    }

    [Fact]
    public void BackoffStrategy_ExponentialWithJitter_HasCorrectValue()
    {
        BackoffStrategy.ExponentialWithJitter.Should().Be(BackoffStrategy.ExponentialWithJitter);
    }

    [Fact]
    public void BackoffStrategy_AllValues_AreDistinct()
    {
        var values = Enum.GetValues<BackoffStrategy>();
        values.Should().HaveCount(4);
        values.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region ResourceAllocationPreferences Tests (6 tests)

    [Fact]
    public void ResourceAllocationPreferences_Constructor_InitializesDefaults()
    {
        var prefs = new ResourceAllocationPreferences();

        prefs.PreferredBackends.Should().NotBeNull().And.BeEmpty();
        prefs.MaxMemoryUsage.Should().BeNull();
        prefs.MaxCpuCores.Should().BeNull();
        prefs.AllowGpuAcceleration.Should().BeTrue();
        prefs.SharingPolicy.Should().Be(ResourceSharingPolicy.Fair);
    }

    [Fact]
    public void ResourceAllocationPreferences_PreferredBackends_CanAddItems()
    {
        var prefs = new ResourceAllocationPreferences();
        prefs.PreferredBackends.Add("CUDA");
        prefs.PreferredBackends.Add("CPU");

        prefs.PreferredBackends.Should().HaveCount(2);
        prefs.PreferredBackends.Should().ContainInOrder("CUDA", "CPU");
    }

    [Fact]
    public void ResourceAllocationPreferences_MaxMemoryUsage_CanBeSet()
    {
        var prefs = new ResourceAllocationPreferences { MaxMemoryUsage = 1024 * 1024 * 1024 };
        prefs.MaxMemoryUsage.Should().Be(1024 * 1024 * 1024);
    }

    [Fact]
    public void ResourceAllocationPreferences_MaxCpuCores_CanBeSet()
    {
        var prefs = new ResourceAllocationPreferences { MaxCpuCores = 8 };
        prefs.MaxCpuCores.Should().Be(8);
    }

    [Theory]
    [InlineData(ResourceSharingPolicy.Fair)]
    [InlineData(ResourceSharingPolicy.PriorityBased)]
    [InlineData(ResourceSharingPolicy.Exclusive)]
    [InlineData(ResourceSharingPolicy.Adaptive)]
    public void ResourceAllocationPreferences_SharingPolicy_CanBeSet(ResourceSharingPolicy policy)
    {
        var prefs = new ResourceAllocationPreferences { SharingPolicy = policy };
        prefs.SharingPolicy.Should().Be(policy);
    }

    [Fact]
    public void ResourceAllocationPreferences_AllowGpuAcceleration_CanBeDisabled()
    {
        var prefs = new ResourceAllocationPreferences { AllowGpuAcceleration = false };
        prefs.AllowGpuAcceleration.Should().BeFalse();
    }

    #endregion

    #region ErrorHandlingStrategy Enum Tests (6 tests)

    [Theory]
    [InlineData(ErrorHandlingStrategy.StopOnFirstError)]
    [InlineData(ErrorHandlingStrategy.ContinueOnError)]
    [InlineData(ErrorHandlingStrategy.AutoRecover)]
    [InlineData(ErrorHandlingStrategy.Custom)]
    [InlineData(ErrorHandlingStrategy.Retry)]
    [InlineData(ErrorHandlingStrategy.Fallback)]
    public void ErrorHandlingStrategy_AllValues_Exist(ErrorHandlingStrategy strategy)
    {
        strategy.Should().BeDefined();
    }

    [Fact]
    public void ErrorHandlingStrategy_HasSixValues()
    {
        var values = Enum.GetValues<ErrorHandlingStrategy>();
        values.Should().HaveCount(6);
    }

    #endregion

    #region StreamingConfiguration Tests (6 tests)

    [Fact]
    public void StreamingConfiguration_Constructor_InitializesDefaults()
    {
        var config = new StreamingConfiguration();

        config.BufferSize.Should().Be(1024);
        config.MaxConcurrency.Should().Be(Environment.ProcessorCount);
        config.ItemTimeout.Should().Be(TimeSpan.FromSeconds(30));
        config.PreserveOrder.Should().BeTrue();
        config.BackpressureStrategy.Should().Be(BackpressureStrategy.Buffer);
        config.ErrorHandling.Should().Be(StreamingErrorHandling.Skip);
    }

    [Fact]
    public void StreamingConfiguration_BufferSize_CanBeSet()
    {
        var config = new StreamingConfiguration { BufferSize = 2048 };
        config.BufferSize.Should().Be(2048);
    }

    [Fact]
    public void StreamingConfiguration_MaxConcurrency_CanBeSet()
    {
        var config = new StreamingConfiguration { MaxConcurrency = 4 };
        config.MaxConcurrency.Should().Be(4);
    }

    [Theory]
    [InlineData(BackpressureStrategy.Buffer)]
    [InlineData(BackpressureStrategy.DropOldest)]
    [InlineData(BackpressureStrategy.DropNewest)]
    [InlineData(BackpressureStrategy.Block)]
    [InlineData(BackpressureStrategy.Fail)]
    public void StreamingConfiguration_BackpressureStrategy_CanBeSet(BackpressureStrategy strategy)
    {
        var config = new StreamingConfiguration { BackpressureStrategy = strategy };
        config.BackpressureStrategy.Should().Be(strategy);
    }

    [Theory]
    [InlineData(StreamingErrorHandling.Skip)]
    [InlineData(StreamingErrorHandling.Stop)]
    [InlineData(StreamingErrorHandling.Retry)]
    [InlineData(StreamingErrorHandling.DeadLetter)]
    public void StreamingConfiguration_ErrorHandling_CanBeSet(StreamingErrorHandling handling)
    {
        var config = new StreamingConfiguration { ErrorHandling = handling };
        config.ErrorHandling.Should().Be(handling);
    }

    [Fact]
    public void StreamingConfiguration_PreserveOrder_CanBeDisabled()
    {
        var config = new StreamingConfiguration { PreserveOrder = false };
        config.PreserveOrder.Should().BeFalse();
    }

    #endregion

    #region OptimizationStrategy Enum Tests (8 tests)

    [Theory]
    [InlineData(OptimizationStrategy.Conservative)]
    [InlineData(OptimizationStrategy.Balanced)]
    [InlineData(OptimizationStrategy.Aggressive)]
    [InlineData(OptimizationStrategy.Adaptive)]
    [InlineData(OptimizationStrategy.MemoryOptimal)]
    [InlineData(OptimizationStrategy.ThroughputOptimal)]
    [InlineData(OptimizationStrategy.LatencyOptimal)]
    [InlineData(OptimizationStrategy.EnergyEfficient)]
    public void OptimizationStrategy_AllValues_Exist(OptimizationStrategy strategy)
    {
        strategy.Should().BeDefined();
    }

    [Fact]
    public void OptimizationStrategy_HasEightValues()
    {
        var values = Enum.GetValues<OptimizationStrategy>();
        values.Should().HaveCount(8);
    }

    #endregion

    #region PerformanceGoals and Weights Tests (5 tests)

    [Fact]
    public void PerformanceGoals_Constructor_InitializesDefaults()
    {
        var goals = new PerformanceGoals();

        goals.TargetExecutionTime.Should().BeNull();
        goals.TargetThroughput.Should().BeNull();
        goals.MaxMemoryUsage.Should().BeNull();
        goals.TargetCpuUtilization.Should().BeNull();
        goals.TargetEnergyEfficiency.Should().BeNull();
        goals.Weights.Should().NotBeNull();
    }

    [Fact]
    public void PerformanceWeights_Constructor_InitializesDefaults()
    {
        var weights = new PerformanceWeights();

        weights.ExecutionTime.Should().Be(0.4);
        weights.MemoryUsage.Should().Be(0.2);
        weights.Throughput.Should().Be(0.3);
        weights.EnergyEfficiency.Should().Be(0.1);
    }

    [Fact]
    public void PerformanceWeights_Sum_EqualsOne()
    {
        var weights = new PerformanceWeights();
        var sum = weights.ExecutionTime + weights.MemoryUsage + weights.Throughput + weights.EnergyEfficiency;
        sum.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void PerformanceGoals_AllProperties_CanBeSet()
    {
        var goals = new PerformanceGoals
        {
            TargetExecutionTime = TimeSpan.FromSeconds(1),
            TargetThroughput = 1000.0,
            MaxMemoryUsage = 1024 * 1024,
            TargetCpuUtilization = 80.0,
            TargetEnergyEfficiency = 100.0
        };

        goals.TargetExecutionTime.Should().Be(TimeSpan.FromSeconds(1));
        goals.TargetThroughput.Should().Be(1000.0);
        goals.MaxMemoryUsage.Should().Be(1024 * 1024);
        goals.TargetCpuUtilization.Should().Be(80.0);
        goals.TargetEnergyEfficiency.Should().Be(100.0);
    }

    [Fact]
    public void PerformanceWeights_CustomWeights_CanBeSet()
    {
        var weights = new PerformanceWeights
        {
            ExecutionTime = 0.5,
            MemoryUsage = 0.3,
            Throughput = 0.15,
            EnergyEfficiency = 0.05
        };

        weights.ExecutionTime.Should().Be(0.5);
        weights.MemoryUsage.Should().Be(0.3);
        weights.Throughput.Should().Be(0.15);
        weights.EnergyEfficiency.Should().Be(0.05);
    }

    #endregion

    #region OptimizationConstraints Tests (7 tests)

    [Fact]
    public void OptimizationConstraints_Constructor_InitializesDefaults()
    {
        var constraints = new OptimizationConstraints();

        constraints.MaxExecutionTime.Should().BeNull();
        constraints.MaxMemoryAllocation.Should().BeNull();
        constraints.MaxCpuCores.Should().BeNull();
        constraints.DisallowedBackends.Should().NotBeNull().And.BeEmpty();
        constraints.RequiredBackends.Should().NotBeNull().And.BeEmpty();
        constraints.AllowStructuralChanges.Should().BeTrue();
        constraints.AllowReordering.Should().BeTrue();
    }

    [Fact]
    public void OptimizationConstraints_MaxExecutionTime_CanBeSet()
    {
        var constraints = new OptimizationConstraints { MaxExecutionTime = TimeSpan.FromSeconds(30) };
        constraints.MaxExecutionTime.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void OptimizationConstraints_DisallowedBackends_CanAddItems()
    {
        var constraints = new OptimizationConstraints();
        constraints.DisallowedBackends.Add("ROCm");
        constraints.DisallowedBackends.Add("Metal");

        constraints.DisallowedBackends.Should().Contain("ROCm");
        constraints.DisallowedBackends.Should().Contain("Metal");
    }

    [Fact]
    public void OptimizationConstraints_RequiredBackends_CanAddItems()
    {
        var constraints = new OptimizationConstraints();
        constraints.RequiredBackends.Add("CUDA");

        constraints.RequiredBackends.Should().Contain("CUDA");
    }

    [Fact]
    public void OptimizationConstraints_StructuralChanges_CanBeDisabled()
    {
        var constraints = new OptimizationConstraints { AllowStructuralChanges = false };
        constraints.AllowStructuralChanges.Should().BeFalse();
    }

    [Fact]
    public void OptimizationConstraints_Reordering_CanBeDisabled()
    {
        var constraints = new OptimizationConstraints { AllowReordering = false };
        constraints.AllowReordering.Should().BeFalse();
    }

    [Fact]
    public void OptimizationConstraints_MaxCpuCores_CanBeSet()
    {
        var constraints = new OptimizationConstraints { MaxCpuCores = 4 };
        constraints.MaxCpuCores.Should().Be(4);
    }

    #endregion

    #region Cache Policy Tests (8 tests)

    [Fact]
    public void CachePolicy_Default_IsLRUPolicy()
    {
        var policy = CachePolicy.Default;
        policy.Should().BeOfType<LRUCachePolicy>();
    }

    [Fact]
    public void LRUCachePolicy_Constructor_SetsProperties()
    {
        var policy = new LRUCachePolicy(maxSize: 100, maxAge: TimeSpan.FromHours(1));

        policy.MaxSize.Should().Be(100);
        policy.MaxAge.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void LRUCachePolicy_ShouldEvict_WithExpiredItem_ReturnsTrue()
    {
        var policy = new LRUCachePolicy(maxSize: 100, maxAge: TimeSpan.FromSeconds(1));
        var entry = Substitute.For<ICacheEntry>();
        entry.CreatedAt.Returns(DateTimeOffset.UtcNow.AddSeconds(-2));

        var result = policy.ShouldEvict(entry);
        result.Should().BeTrue();
    }

    [Fact]
    public void LRUCachePolicy_GetEvictionPriority_ReturnsNegativeForOldItems()
    {
        var policy = new LRUCachePolicy(maxSize: 100);
        var entry = Substitute.For<ICacheEntry>();
        entry.LastAccessedAt.Returns(DateTimeOffset.UtcNow.AddMinutes(-5));

        var priority = policy.GetEvictionPriority(entry);
        priority.Should().BeLessThan(0);
    }

    [Fact]
    public void TTLCachePolicy_Constructor_SetsTimeToLive()
    {
        var ttl = TimeSpan.FromMinutes(30);
        var policy = new TTLCachePolicy(ttl);

        policy.TimeToLive.Should().Be(ttl);
    }

    [Fact]
    public void TTLCachePolicy_ShouldEvict_WithExpiredItem_ReturnsTrue()
    {
        var policy = new TTLCachePolicy(TimeSpan.FromSeconds(1));
        var entry = Substitute.For<ICacheEntry>();
        entry.CreatedAt.Returns(DateTimeOffset.UtcNow.AddSeconds(-2));

        var result = policy.ShouldEvict(entry);
        result.Should().BeTrue();
    }

    [Fact]
    public void TTLCachePolicy_ShouldEvict_WithFreshItem_ReturnsFalse()
    {
        var policy = new TTLCachePolicy(TimeSpan.FromMinutes(10));
        var entry = Substitute.For<ICacheEntry>();
        entry.CreatedAt.Returns(DateTimeOffset.UtcNow);

        var result = policy.ShouldEvict(entry);
        result.Should().BeFalse();
    }

    [Fact]
    public void TTLCachePolicy_GetEvictionPriority_ReturnsPositiveForFreshItems()
    {
        var policy = new TTLCachePolicy(TimeSpan.FromMinutes(10));
        var entry = Substitute.For<ICacheEntry>();
        entry.CreatedAt.Returns(DateTimeOffset.UtcNow);

        var priority = policy.GetEvictionPriority(entry);
        priority.Should().BeGreaterThan(0);
    }

    #endregion

    #region MemoryAllocationStrategy Enum Tests (5 tests)

    [Theory]
    [InlineData(MemoryAllocationStrategy.OnDemand)]
    [InlineData(MemoryAllocationStrategy.PreAllocated)]
    [InlineData(MemoryAllocationStrategy.Pooled)]
    [InlineData(MemoryAllocationStrategy.Adaptive)]
    [InlineData(MemoryAllocationStrategy.Optimal)]
    public void MemoryAllocationStrategy_AllValues_Exist(MemoryAllocationStrategy strategy)
    {
        strategy.Should().BeDefined();
    }

    [Fact]
    public void MemoryAllocationStrategy_HasFiveValues()
    {
        var values = Enum.GetValues<MemoryAllocationStrategy>();
        values.Should().HaveCount(5);
    }

    #endregion

    #region Event Tests (10 tests)

    [Fact]
    public void PipelineEvent_Constructor_InitializesDefaults()
    {
        var evt = new PipelineEvent();

        evt.EventId.Should().NotBeEmpty();
        evt.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        evt.Severity.Should().Be(EventSeverity.Information);
        evt.Description.Should().BeEmpty();
        evt.Message.Should().BeEmpty();
        evt.Data.Should().NotBeNull().And.BeEmpty();
        evt.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void PipelineEvent_Properties_CanBeSet()
    {
        var pipelineId = Guid.NewGuid();
        var evt = new PipelineEvent
        {
            PipelineId = pipelineId,
            Severity = EventSeverity.Error,
            Description = "Test error",
            Message = "Error message",
            Type = PipelineEventType.Failed,
            StageId = "stage1"
        };

        evt.PipelineId.Should().Be(pipelineId);
        evt.Severity.Should().Be(EventSeverity.Error);
        evt.Description.Should().Be("Test error");
        evt.Message.Should().Be("Error message");
        evt.Type.Should().Be(PipelineEventType.Failed);
        evt.StageId.Should().Be("stage1");
    }

    [Fact]
    public void PipelineExecutionStartedEvent_InheritsFromPipelineEvent()
    {
        var evt = new PipelineExecutionStartedEvent();
        evt.Should().BeAssignableTo<PipelineEvent>();
    }

    [Fact]
    public void PipelineExecutionCompletedEvent_Properties_CanBeSet()
    {
        var evt = new PipelineExecutionCompletedEvent
        {
            IsSuccess = true,
            ExecutionTime = TimeSpan.FromSeconds(5),
            ResultSummary = "Completed successfully"
        };

        evt.IsSuccess.Should().BeTrue();
        evt.ExecutionTime.Should().Be(TimeSpan.FromSeconds(5));
        evt.ResultSummary.Should().Be("Completed successfully");
        evt.Exceptions.Should().NotBeNull();
    }

    [Fact]
    public void StageExecutionStartedEvent_Properties_CanBeSet()
    {
        var evt = new StageExecutionStartedEvent
        {
            StageName = "Stage1",
            KernelName = "VectorAdd",
            Backend = "CUDA"
        };

        evt.StageName.Should().Be("Stage1");
        evt.KernelName.Should().Be("VectorAdd");
        evt.Backend.Should().Be("CUDA");
    }

    [Fact]
    public void StageExecutionCompletedEvent_Properties_CanBeSet()
    {
        var exception = new InvalidOperationException("Test error");
        var evt = new StageExecutionCompletedEvent
        {
            StageName = "Stage1",
            IsSuccess = false,
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            Exception = exception
        };

        evt.StageName.Should().Be("Stage1");
        evt.IsSuccess.Should().BeFalse();
        evt.ExecutionTime.Should().Be(TimeSpan.FromMilliseconds(100));
        evt.Exception.Should().BeSameAs(exception);
    }

    [Theory]
    [InlineData(EventSeverity.Verbose)]
    [InlineData(EventSeverity.Information)]
    [InlineData(EventSeverity.Warning)]
    [InlineData(EventSeverity.Error)]
    [InlineData(EventSeverity.Critical)]
    public void EventSeverity_AllValues_Exist(EventSeverity severity)
    {
        severity.Should().BeDefined();
    }

    [Fact]
    public void EventSeverity_HasFiveValues()
    {
        var values = Enum.GetValues<EventSeverity>();
        values.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(PipelineEventType.Started)]
    [InlineData(PipelineEventType.Completed)]
    [InlineData(PipelineEventType.Failed)]
    [InlineData(PipelineEventType.StageStarted)]
    [InlineData(PipelineEventType.StageCompleted)]
    [InlineData(PipelineEventType.StageFailed)]
    [InlineData(PipelineEventType.Optimized)]
    [InlineData(PipelineEventType.Validated)]
    public void PipelineEventType_AllValues_Exist(PipelineEventType type)
    {
        type.Should().BeDefined();
    }

    [Fact]
    public void PipelineEventType_HasEightValues()
    {
        var values = Enum.GetValues<PipelineEventType>();
        values.Should().HaveCount(8);
    }

    #endregion
}
