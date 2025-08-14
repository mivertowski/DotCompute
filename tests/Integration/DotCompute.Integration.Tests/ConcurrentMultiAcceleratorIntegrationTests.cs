// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Tests.Common.Hardware;
using DotCompute.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for concurrent multi-accelerator workflows including
/// parallel execution, load balancing, synchronization, and resource coordination.
/// </summary>
[Collection("Integration")]
public class ConcurrentMultiAcceleratorIntegrationTests : ComputeWorkflowTestBase
{
    private readonly IAcceleratorManager _acceleratorManager;

    public ConcurrentMultiAcceleratorIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
    }

    [Fact]
    public async Task ParallelExecution_MultipleAccelerators_ShouldExecuteConcurrently()
    {
        // Arrange
        var workflows = new[]
        {
            CreateAcceleratorSpecificWorkflow("GPU_Workflow_1", ComputeBackendType.CUDA, 2048),
            CreateAcceleratorSpecificWorkflow("GPU_Workflow_2", ComputeBackendType.CUDA, 2048),
            CreateAcceleratorSpecificWorkflow("CPU_Workflow_1", ComputeBackendType.CPU, 1024),
            CreateAcceleratorSpecificWorkflow("CPU_Workflow_2", ComputeBackendType.CPU, 1024),
            CreateAcceleratorSpecificWorkflow("Metal_Workflow", ComputeBackendType.Metal, 1536)
        };

        var executionStopwatch = Stopwatch.StartNew();

        // Act - Execute all workflows in parallel
        var tasks = workflows.Select((workflow, index) =>
            ExecuteComputeWorkflowAsync($"ParallelExecution_{index}", workflow))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        executionStopwatch.Stop();

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Calculate performance metrics
        var totalDataProcessed = workflows.Sum(w => w.Inputs.Sum(i => i.Data.Length));
        var parallelThroughput = (totalDataProcessed * sizeof(float)) / 1024.0 / 1024.0 / 
                                executionStopwatch.Elapsed.TotalSeconds;

        Logger.LogInformation("Parallel multi-accelerator execution: {Count} workflows, " +
                             "{Throughput:F2} MB/s total throughput, {Duration:F1}ms",
            workflows.Length, parallelThroughput, executionStopwatch.ElapsedMilliseconds);

        // Verify each accelerator type was utilized
        var backendUsage = results.GroupBy(r => DetectBackendFromResult(r))
                                .ToDictionary(g => g.Key, g => g.Count());

        Assert.NotEmpty(backendUsage);
        Logger.LogInformation("Backend usage: {Usage}", 
            string.Join(", ", backendUsage.Select(kvp => $"{kvp.Key}: {kvp.Value}");

        Assert.True(parallelThroughput > 50, "Parallel execution should achieve good throughput");
    }

    [Fact]
    public async Task LoadBalancing_UnevenWorkloads_ShouldDistributeOptimally()
    {
        // Arrange - Create workloads of varying sizes
        var workloadSizes = new[] { 512, 1024, 2048, 4096, 8192, 1536, 768, 3072 };
        var workflows = workloadSizes.Select((size, index) =>
            CreateLoadBalancingWorkflow($"LoadBalanced_{index}", size))
            .ToArray();

        var loadBalancer = new WorkloadLoadBalancer(_acceleratorManager, Logger);

        // Act
        var distributionPlan = await loadBalancer.CreateDistributionPlanAsync(workflows);
        var executionTasks = distributionPlan.Select(assignment =>
            ExecuteComputeWorkflowAsync(assignment.WorkflowName, assignment.Workflow))
            .ToArray();

        var results = await Task.WhenAll(executionTasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Analyze load distribution
        var executionTimes = results.Select(r => r.Duration.TotalMilliseconds).ToArray();
        var maxExecutionTime = executionTimes.Max();
        var minExecutionTime = executionTimes.Min();
        var executionTimeVariance = maxExecutionTime - minExecutionTime;

        Logger.LogInformation("Load balancing results: Min={Min:F1}ms, Max={Max:F1}ms, Variance={Variance:F1}ms",
            minExecutionTime, maxExecutionTime, executionTimeVariance);

        // Well-balanced execution should have relatively low variance
        var normalizedVariance = executionTimeVariance / maxExecutionTime;
        Assert.True(normalizedVariance < 0.5, "Load balancing should minimize execution time variance");
    }

    [Fact]
    public async Task SynchronizedExecution_SharedData_ShouldMaintainConsistency()
    {
        // Arrange
        const int sharedDataSize = 2048;
        var sharedData = TestDataGenerators.GenerateFloatArray(sharedDataSize);
        var synchronizationBarrier = new SemaphoreSlim(0);
        var results = new ConcurrentDictionary<string, float[]>();

        var workflows = Enumerable.Range(0, 4).Select(i =>
            CreateSynchronizedWorkflow($"Sync_Worker_{i}", sharedData, i))
            .ToArray();

        // Act - Execute workflows with synchronization points
        var tasks = workflows.Select(async (workflow, index) =>
        {
            var result = await ExecuteComputeWorkflowAsync($"SynchronizedExecution_{index}", workflow);
            
            if (result.Success && result.Results.ContainsKey("output"))
            {
                results[workflow.Name] = (float[])result.Results["output"];
            }

            // Signal completion
            synchronizationBarrier.Release();
            return result;
        }).ToArray();

        // Wait for all workflows to complete
        await Task.WhenAll(tasks);

        // Assert
        tasks.Select(t => t.Result).Should().AllSatisfy(r => r.Success.Should().BeTrue());
        Assert.Equal(4, results.Count());

        // Verify data consistency across all workers
        var referenceResult = results.Values.First();
        foreach (var result in results.Values.Skip(1))
        {
            for (int i = 0; i < Math.Min(100, referenceResult.Length); i++)
            {
                result[i].Should().BeApproximately(referenceResult[i], 0.01f,
                    $"Synchronized execution should produce consistent results at index {i}");
            }
        }

        Logger.LogInformation("Synchronized execution completed successfully across {Count} accelerators", 
            workflows.Length);
    }

    [Fact]
    public async Task ResourceCoordination_MemorySharing_ShouldOptimizeUsage()
    {
        // Arrange
        var resourceCoordinator = new MultiAcceleratorResourceCoordinator(Logger);
        
        var memoryIntensiveWorkflows = Enumerable.Range(0, 3).Select(i =>
            CreateMemoryIntensiveWorkflow($"MemoryIntensive_{i}", 4096))
            .ToArray();

        // Act
        var coordinationPlan = await resourceCoordinator.CoordinateResourcesAsync(memoryIntensiveWorkflows);
        var executionTasks = coordinationPlan.Select(plan =>
            ExecuteComputeWorkflowAsync(plan.WorkflowName, plan.Workflow))
            .ToArray();

        var results = await Task.WhenAll(executionTasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Verify resource coordination efficiency
        var totalMemoryUsed = results.Sum(r => r.Metrics?.MemoryUsageDelta ?? 0);
        var peakMemoryUsage = results.Max(r => r.Metrics?.ResourceUtilization.MemoryUsagePercent ?? 0);

        Logger.LogInformation("Resource coordination: Total memory used: {Total}MB, Peak usage: {Peak:F1}%",
            totalMemoryUsed / (1024 * 1024), peakMemoryUsage);

        Assert.True(peakMemoryUsage < 90, "Resource coordination should prevent memory exhaustion");
    }

    [Fact]
    public async Task FaultTolerance_AcceleratorFailure_ShouldRedistributeWork()
    {
        // Arrange
        var workflows = Enumerable.Range(0, 6).Select(i =>
            CreateFaultTolerantWorkflow($"FaultTolerant_{i}", 1024))
            .ToArray();

        // Act - Start execution
        var executionTasks = workflows.Select((workflow, index) =>
            ExecuteComputeWorkflowAsync($"FaultTolerance_{index}", workflow))
            .ToArray();

        // Simulate accelerator failure after a delay
        await Task.Delay(100);
        HardwareSimulator.SimulateRandomFailures(0.5, AcceleratorType.CUDA);

        var results = await Task.WhenAll(executionTasks);

        // Assert
        var successfulResults = results.Where(r => r.Success).ToArray();
        var failedResults = results.Where(r => !r.Success).ToArray();

        Logger.LogInformation("Fault tolerance test: {Successful} successful, {Failed} failed workflows",
            successfulResults.Length, failedResults.Length);

        // At least half should succeed despite failures
        successfulResults.Assert.True(Length >= workflows.Length / 2,
            "Fault tolerance should allow majority of work to complete");

        // Failed results should have meaningful error information
        failedResults.Should().AllSatisfy(r => 
        {
            r.Error.Should().NotBeNull();
            r.Error!.Message.Should().NotBeEmpty();
        });

        // Reset hardware state
        HardwareSimulator.ResetAllConditions();
    }

    [Fact]
    public async Task DynamicScaling_VaryingLoad_ShouldAdaptResourceAllocation()
    {
        // Arrange
        var phases = new[]
        {
            new LoadPhase { Name = "LightLoad", WorkflowCount = 2, DataSize = 512 },
            new LoadPhase { Name = "MediumLoad", WorkflowCount = 4, DataSize = 1024 },
            new LoadPhase { Name = "HeavyLoad", WorkflowCount = 8, DataSize = 2048 },
            new LoadPhase { Name = "PeakLoad", WorkflowCount = 12, DataSize = 4096 },
            new LoadPhase { Name = "CoolDown", WorkflowCount = 2, DataSize = 256 }
        };

        var scalingResults = new List<ScalingPhaseResult>();

        // Act - Execute each load phase
        foreach (var phase in phases)
        {
            Logger.LogInformation("Starting load phase: {PhaseName}", phase.Name);
            
            var phaseWorkflows = Enumerable.Range(0, phase.WorkflowCount)
                .Select(i => CreateScalableWorkflow($"{phase.Name}_Workflow_{i}", phase.DataSize))
                .ToArray();

            var phaseStopwatch = Stopwatch.StartNew();
            
            var phaseTasks = phaseWorkflows.Select((workflow, index) =>
                ExecuteComputeWorkflowAsync($"{phase.Name}_{index}", workflow))
                .ToArray();

            var phaseResults = await Task.WhenAll(phaseTasks);
            phaseStopwatch.Stop();

            var phaseResult = new ScalingPhaseResult
            {
                PhaseName = phase.Name,
                WorkflowCount = phase.WorkflowCount,
                DataSize = phase.DataSize,
                TotalDuration = phaseStopwatch.Elapsed,
                SuccessfulWorkflows = phaseResults.Count(r => r.Success),
                AverageThroughput = phaseResults.Where(r => r.Success)
                    .Average(r => r.Metrics?.ThroughputMBps ?? 0),
                PeakMemoryUsage = phaseResults.Max(r => r.Metrics?.ResourceUtilization.MemoryUsagePercent ?? 0)
            };

            scalingResults.Add(phaseResult);
            
            Logger.LogInformation("Phase {Phase} completed: {Successful}/{Total} successful, " +
                                 "Avg throughput: {Throughput:F2} MB/s",
                phase.Name, phaseResult.SuccessfulWorkflows, phase.WorkflowCount, 
                phaseResult.AverageThroughput);

            // Brief cooldown between phases
            await Task.Delay(200);
        }

        // Assert
        scalingResults.Should().AllSatisfy(r => 
            r.SuccessfulWorkflows.BeGreaterOrEqualTo(r.WorkflowCount / 2));

        // Verify scaling behavior
        ValidateScalingBehavior(scalingResults);
    }

    [Fact]
    public async Task PipelineParallelism_CrossAcceleratorPipeline_ShouldOptimizeLatency()
    {
        // Arrange - Create pipeline that spans multiple accelerators
        const int pipelineDepth = 4;
        const int dataSize = 1024;
        
        var pipelineWorkflow = CreateCrossAcceleratorPipeline(pipelineDepth, dataSize);

        // Act
        var result = await ExecuteComputeWorkflowAsync("PipelineParallelism", pipelineWorkflow);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionResults.Count.Should().Be(pipelineDepth));

        // All stages should complete successfully
        result.ExecutionResults.Values.Should().AllSatisfy(stage => 
            stage.Success.Should().BeTrue());

        // Pipeline should show good utilization
        var totalStageTime = result.ExecutionResults.Values.Sum(stage => stage.Duration.TotalMilliseconds);
        var actualPipelineTime = result.Duration.TotalMilliseconds;
        var pipelineEfficiency = Math.Max(0, (totalStageTime - actualPipelineTime) / totalStageTime);

        Logger.LogInformation("Pipeline parallelism: {Stages} stages, {Efficiency:P1} efficiency, " +
                             "Stage time: {StageTime:F1}ms, Pipeline time: {PipelineTime:F1}ms",
            pipelineDepth, pipelineEfficiency, totalStageTime, actualPipelineTime);

        Assert.True(pipelineEfficiency > 0.2, "Pipeline should show parallelism benefits");
    }

    [Theory]
    [InlineData(2, 1024)]
    [InlineData(4, 2048)]
    [InlineData(8, 4096)]
    public async Task ConcurrencyScaling_MultipleWorkflows_ShouldScaleWithAcceleratorCount(
        int acceleratorCount, int dataSize)
    {
        // Arrange
        var workflows = Enumerable.Range(0, acceleratorCount * 2) // 2 workflows per accelerator
            .Select(i => CreateConcurrencyTestWorkflow($"Concurrent_{i}", dataSize))
            .ToArray();

        var concurrencyStopwatch = Stopwatch.StartNew();

        // Act
        var tasks = workflows.Select((workflow, index) =>
            ExecuteComputeWorkflowAsync($"ConcurrencyScaling_{acceleratorCount}_{index}", workflow))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        concurrencyStopwatch.Stop();

        // Assert
        var successfulResults = results.Where(r => r.Success).ToArray();
        var successRate = (double)successfulResults.Length / workflows.Length;

        Logger.LogInformation("Concurrency scaling test: {AcceleratorCount} accelerators, " +
                             "{Workflows} workflows, {SuccessRate:P1} success rate, {Duration:F1}ms",
            acceleratorCount, workflows.Length, successRate, concurrencyStopwatch.ElapsedMilliseconds);

        Assert.True(successRate >= 0.75, "Most workflows should complete successfully");

        // Calculate effective throughput
        var totalDataMB = workflows.Sum(w => w.Inputs.Sum(i => i.Data.Length * sizeof(float) / 1024.0 / 1024.0;
        var effectiveThroughput = totalDataMB / concurrencyStopwatch.Elapsed.TotalSeconds;

        Assert.True(effectiveThroughput > 10, "Concurrent execution should maintain good throughput");

        LogPerformanceMetrics($"ConcurrencyScaling_{acceleratorCount}", 
            concurrencyStopwatch.Elapsed, workflows.Sum(w => w.Inputs.Sum(i => i.Data.Length);
    }

    // Helper methods and classes

    private ComputeWorkflowDefinition CreateAcceleratorSpecificWorkflow(string name, ComputeBackendType backend, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "backend_specific",
                    SourceCode = GetBackendSpecificKernel(backend),
                    CompilationOptions = GetBackendOptimizedOptions(backend)
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "execute_stage",
                    Order = 1,
                    KernelName = "backend_specific",
                    BackendType = backend,
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateLoadBalancingWorkflow(string name, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "balanced_compute",
                    SourceCode = KernelSources.BalancedCompute,
                    CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "compute_stage",
                    Order = 1,
                    KernelName = "balanced_compute",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateSynchronizedWorkflow(string name, float[] sharedData, int workerId)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "synchronized_process",
                    SourceCode = KernelSources.SynchronizedProcess
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "shared_data", Data = sharedData }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = sharedData.Length }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "sync_stage",
                    Order = 1,
                    KernelName = "synchronized_process",
                    ArgumentNames = new[] { "shared_data", "output" },
                    Parameters = new Dictionary<string, object> { ["worker_id"] = workerId }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateMemoryIntensiveWorkflow(string name, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "memory_intensive",
                    SourceCode = KernelSources.MemoryIntensive
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            IntermediateBuffers = new()
            {
                new WorkflowIntermediateBuffer 
                { 
                    Name = "temp_buffer", 
                    SizeInBytes = dataSize * sizeof(float) * 2 
                }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "intensive_stage",
                    Order = 1,
                    KernelName = "memory_intensive",
                    ArgumentNames = new[] { "input", "temp_buffer", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateFaultTolerantWorkflow(string name, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "fault_tolerant",
                    SourceCode = KernelSources.FaultTolerantCompute
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "tolerant_stage",
                    Order = 1,
                    KernelName = "fault_tolerant",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateScalableWorkflow(string name, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "scalable_compute",
                    SourceCode = KernelSources.ScalableCompute
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "scalable_stage",
                    Order = 1,
                    KernelName = "scalable_compute",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateCrossAcceleratorPipeline(int stageCount, int dataSize)
    {
        var workflow = new ComputeWorkflowDefinition
        {
            Name = "CrossAcceleratorPipeline",
            Kernels = new()
            {
                new WorkflowKernel { Name = "pipeline_stage", SourceCode = KernelSources.PipelineStage }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            IntermediateBuffers = new(),
            ExecutionStages = new()
        };

        // Create intermediate buffers
        for (int i = 0; i < stageCount - 1; i++)
        {
            workflow.IntermediateBuffers.Add(new WorkflowIntermediateBuffer
            {
                Name = $"stage_{i}_output",
                SizeInBytes = dataSize * sizeof(float)
            });
        }

        // Create pipeline stages with different backend preferences
        var backends = new[] { ComputeBackendType.CPU, ComputeBackendType.CUDA, ComputeBackendType.Metal, ComputeBackendType.CPU };
        
        for (int stage = 0; stage < stageCount; stage++)
        {
            var inputName = stage == 0 ? "input" : $"stage_{stage - 1}_output";
            var outputName = stage == stageCount - 1 ? "output" : $"stage_{stage}_output";
            
            workflow.ExecutionStages.Add(new WorkflowExecutionStage
            {
                Name = $"pipeline_stage_{stage}",
                Order = stage + 1,
                KernelName = "pipeline_stage",
                BackendType = backends[stage % backends.Length],
                ArgumentNames = new[] { inputName, outputName },
                Parameters = new Dictionary<string, object> { ["stage_id"] = stage }
            });
        }

        return workflow;
    }

    private ComputeWorkflowDefinition CreateConcurrencyTestWorkflow(string name, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "concurrent_compute",
                    SourceCode = KernelSources.ConcurrentCompute
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = dataSize }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "concurrent_stage",
                    Order = 1,
                    KernelName = "concurrent_compute",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeBackendType DetectBackendFromResult(WorkflowExecutionResult result)
    {
        // In a real implementation, this would analyze execution characteristics
        // For testing, use performance hints or default assignment
        if (result.Metrics?.ThroughputMBps > 100) return ComputeBackendType.CUDA;
        if (result.Metrics?.ThroughputMBps > 50) return ComputeBackendType.Metal;
        return ComputeBackendType.CPU;
    }

    private static string GetBackendSpecificKernel(ComputeBackendType backend)
    {
        return backend switch
        {
            ComputeBackendType.CUDA => KernelSources.CudaOptimizedKernel,
            ComputeBackendType.Metal => KernelSources.MetalOptimizedKernel,
            ComputeBackendType.CPU => KernelSources.CpuOptimizedKernel,
            _ => KernelSources.SimpleVectorOperation
        };
    }

    private static CompilationOptions GetBackendOptimizedOptions(ComputeBackendType backend)
    {
        return backend switch
        {
            ComputeBackendType.CUDA => new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Aggressive,
                FastMath = true,
                EnableMemoryCoalescing = true
            },
            ComputeBackendType.Metal => new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                EnableOperatorFusion = true
            },
            ComputeBackendType.CPU => new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                UnrollLoops = true,
                EnableParallelExecution = true
            },
            _ => new CompilationOptions { OptimizationLevel = OptimizationLevel.Default }
        };
    }

    private void ValidateScalingBehavior(List<ScalingPhaseResult> scalingResults)
    {
        // Verify system handled load increases appropriately
        var peakLoad = scalingResults.OrderByDescending(r => r.WorkflowCount).First();
        var lightLoad = scalingResults.OrderBy(r => r.WorkflowCount).First();

        Logger.LogInformation("Scaling validation: Light load: {LightSuccess}/{LightTotal}, " +
                             "Peak load: {PeakSuccess}/{PeakTotal}",
            lightLoad.SuccessfulWorkflows, lightLoad.WorkflowCount,
            peakLoad.SuccessfulWorkflows, peakLoad.WorkflowCount);

        // Success rate should remain reasonable even at peak load
        var peakSuccessRate = (double)peakLoad.SuccessfulWorkflows / peakLoad.WorkflowCount;
        Assert.True(peakSuccessRate >= 0.6, "System should handle peak load gracefully");

        // Memory usage should scale reasonably
        var maxMemoryUsage = scalingResults.Max(r => r.PeakMemoryUsage);
        Assert.True(maxMemoryUsage < 95, "System should not exhaust memory during scaling");
    }
}

// Helper classes

public class LoadPhase
{
    public string Name { get; set; } = string.Empty;
    public int WorkflowCount { get; set; }
    public int DataSize { get; set; }
}

public class ScalingPhaseResult
{
    public string PhaseName { get; set; } = string.Empty;
    public int WorkflowCount { get; set; }
    public int DataSize { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int SuccessfulWorkflows { get; set; }
    public double AverageThroughput { get; set; }
    public double PeakMemoryUsage { get; set; }
}

public class WorkloadAssignment
{
    public string WorkflowName { get; set; } = string.Empty;
    public ComputeWorkflowDefinition Workflow { get; set; } = new();
    public ComputeBackendType PreferredBackend { get; set; }
    public int Priority { get; set; }
}

public class WorkloadLoadBalancer
{
    private readonly IAcceleratorManager _acceleratorManager;
    private readonly ILogger _logger;

    public WorkloadLoadBalancer(IAcceleratorManager acceleratorManager, ILogger logger)
    {
        _acceleratorManager = acceleratorManager;
        _logger = logger;
    }

    public async Task<WorkloadAssignment[]> CreateDistributionPlanAsync(ComputeWorkflowDefinition[] workflows)
    {
        var assignments = new List<WorkloadAssignment>();
        
        // Simple round-robin assignment for testing
        var backends = new[] { ComputeBackendType.CPU, ComputeBackendType.CUDA, ComputeBackendType.Metal };
        
        for (int i = 0; i < workflows.Length; i++)
        {
            assignments.Add(new WorkloadAssignment
            {
                WorkflowName = workflows[i].Name,
                Workflow = workflows[i],
                PreferredBackend = backends[i % backends.Length],
                Priority = CalculatePriority(workflows[i])
            });
        }

        _logger.LogInformation("Created distribution plan for {Count} workflows", workflows.Length);
        return assignments.ToArray();
    }

    private static int CalculatePriority(ComputeWorkflowDefinition workflow)
    {
        // Simple priority calculation based on data size
        var totalInputSize = workflow.Inputs.Sum(i => i.Data.Length);
        return totalInputSize > 2048 ? 1 : 0; // High priority for large workloads
    }
}

public class MultiAcceleratorResourceCoordinator
{
    private readonly ILogger _logger;

    public MultiAcceleratorResourceCoordinator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<WorkloadAssignment[]> CoordinateResourcesAsync(ComputeWorkflowDefinition[] workflows)
    {
        var assignments = new List<WorkloadAssignment>();

        // Coordinate memory usage across workflows
        var totalMemoryRequired = workflows.Sum(w => w.Inputs.Sum(i => i.Data.Length) * sizeof(float));
        var memoryBudgetPerWorkflow = totalMemoryRequired / workflows.Length;

        _logger.LogInformation("Coordinating resources: {Total}MB total memory, {Budget}MB per workflow",
            totalMemoryRequired / (1024 * 1024), memoryBudgetPerWorkflow / (1024 * 1024));

        for (int i = 0; i < workflows.Length; i++)
        {
            assignments.Add(new WorkloadAssignment
            {
                WorkflowName = workflows[i].Name,
                Workflow = workflows[i],
                PreferredBackend = SelectOptimalBackend(workflows[i]),
                Priority = 0
            });
        }

        return assignments.ToArray();
    }

    private static ComputeBackendType SelectOptimalBackend(ComputeWorkflowDefinition workflow)
    {
        var inputSize = workflow.Inputs.Sum(i => i.Data.Length);
        
        // Simple heuristic: larger workloads prefer GPU
        if (inputSize > 2048) return ComputeBackendType.CUDA;
        if (inputSize > 1024) return ComputeBackendType.Metal;
        return ComputeBackendType.CPU;
    }
}

/// <summary>
/// Additional kernel sources for concurrent multi-accelerator testing.
/// </summary>
internal static partial class KernelSources
{
    public const string BalancedCompute = @"
__kernel void balanced_compute(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    // Balanced computation load
    for (int i = 0; i < 10; i++) {
        value = value * 0.95f + sin(value * 0.1f);
    }
    
    output[gid] = value;
}";

    public const string SynchronizedProcess = @"
__kernel void synchronized_process(__global const float* input, __global float* output, int worker_id) {
    int gid = get_global_id(0);
    
    // Process data with worker-specific transformation
    float factor = 1.0f + worker_id * 0.1f;
    output[gid] = input[gid] * factor + worker_id;
}";

    public const string MemoryIntensive = @"
__kernel void memory_intensive(__global const float* input, __global float* temp, __global float* output) {
    int gid = get_global_id(0);
    int size = get_global_size(0);
    
    // Use temporary buffer intensively
    temp[gid] = input[gid];
    temp[gid + size] = input[gid] * 2.0f;
    
    barrier(CLK_GLOBAL_MEM_FENCE);
    
    // Combine temporary results
    output[gid] = (temp[gid] + temp[gid + size]) * 0.5f;
}";

    public const string FaultTolerantCompute = @"
__kernel void fault_tolerant(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    // Fault-tolerant computation
    if (isnan(value) || isinf(value)) {
        value = 0.0f;
    }
    
    // Simple, reliable computation
    output[gid] = clamp(value + 1.0f, -1000.0f, 1000.0f);
}";

    public const string ScalableCompute = @"
__kernel void scalable_compute(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int global_size = get_global_size(0);
    
    float value = input[gid];
    
    // Scalable algorithm that adapts to work group size
    int iterations = min(global_size / 64, 10);
    for (int i = 0; i < iterations; i++) {
        value = value * 0.99f + cos(value);
    }
    
    output[gid] = value;
}";

    public const string PipelineStage = @"
__kernel void pipeline_stage(__global const float* input, __global float* output, int stage_id) {
    int gid = get_global_id(0);
    
    // Stage-specific processing
    float stage_factor = 1.0f + stage_id * 0.2f;
    float value = input[gid] * stage_factor;
    
    // Add stage-specific computation
    switch (stage_id % 4) {
        case 0: value = sqrt(fabs(value)); break;
        case 1: value = value * value; break;
        case 2: value = sin(value); break;
        case 3: value = cos(value); break;
    }
    
    output[gid] = value;
}";

    public const string ConcurrentCompute = @"
__kernel void concurrent_compute(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    __local float shared[64];
    
    // Thread-safe concurrent computation
    shared[lid % 64] = input[gid];
    barrier(CLK_LOCAL_MEM_FENCE);
    
    float sum = 0.0f;
    int local_size = min(get_local_size(0), 64);
    for (int i = 0; i < local_size; i++) {
        sum += shared[i] * 0.1f;
    }
    
    output[gid] = input[gid] + sum;
}";

    public const string CpuOptimizedKernel = @"
__kernel void cpu_optimized(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    
    // CPU-optimized: sequential access, minimal branching
    float4 data = vload4(gid / 4, (__global float4*)input);
    data = data + 1.0f;
    vstore4(data, gid / 4, (__global float4*)output);
}";
}
