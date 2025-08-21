// <copyright file="ExecutionPlans.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution
{
    // This file has been refactored - all types have been moved to their respective files.
    // 
    // Memory Management (src/Core/DotCompute.Core/Execution/Memory/):
    // - ExecutionMemoryManager.cs
    // - BufferAllocationRequest.cs
    // - DeviceBufferPool.cs
    // - ExecutionMemoryStatistics.cs
    // - DeviceMemoryStatistics.cs
    // 
    // Execution Plans (src/Core/DotCompute.Core/Execution/Plans/):
    // - ExecutionPlan.cs
    // - DataParallelExecutionPlan.cs
    // - ModelParallelExecutionPlan.cs
    // - PipelineExecutionPlan.cs
    // 
    // Workloads (src/Core/DotCompute.Core/Execution/Workload/):
    // - ExecutionWorkload.cs
    // - DataParallelWorkload.cs
    // - PipelineWorkload.cs
    // - ModelParallelWorkload.cs
    // - WorkStealingWorkload.cs
    // - WorkItem.cs
    // 
    // Analysis (src/Core/DotCompute.Core/Execution/Analysis/):
    // - WorkloadAnalysis.cs
    // - BottleneckAnalysis.cs
    // - DependencyAnalyzer.cs
    // - DependencyGraph.cs
    // - DevicePerformanceEstimator.cs
    // - DevicePerformanceCache.cs
    // - KernelPerformanceStatistics.cs
    // 
    // Optimization (src/Core/DotCompute.Core/Execution/Optimization/):
    // - ExecutionPlanOptimizer.cs
    // - ExecutionOptimizer.cs
    // - LoadBalancingHints.cs
    // 
    // Scheduling (src/Core/DotCompute.Core/Execution/Scheduling/):
    // - ResourceScheduler.cs
    // - WorkloadDistribution.cs
    // - DeviceWorkAssignment.cs
    // 
    // Pipeline (src/Core/DotCompute.Core/Execution/Pipeline/):
    // - PipelineDefinition.cs
    // - PipelineStageDefinition.cs
    // - PipelineInputSpec.cs
    // - PipelineOutputSpec.cs
    // - StreamingConfig.cs
    // 
    // Types (src/Core/DotCompute.Core/Execution/Types/):
    // - CommunicationOperationType.cs
    // - SynchronizationType.cs
    // - MicrobatchSchedulingStrategy.cs
    // - BufferSwapStrategy.cs
    // - PrefetchingPolicy.cs
    // - OutputFormat.cs
    // - BottleneckType.cs
    // - WorkloadType.cs
    // - DependencyType.cs
    // 
    // Root Execution (src/Core/DotCompute.Core/Execution/):
    // - ExecutionPlanFactory.cs
    // - ExecutionPlanGenerator.cs
    // - ExecutionConstraints.cs
    // - ExecutionPlanAlternatives.cs
    // - ManagedCompiledKernel.cs
    // 
    // This file is preserved as a placeholder and documentation of the refactoring.
}