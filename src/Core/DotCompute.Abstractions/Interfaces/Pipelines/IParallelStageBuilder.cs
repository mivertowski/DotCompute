// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Interfaces.Pipelines;

/// <summary>
/// Builder interface for configuring parallel execution stages within a pipeline.
/// Provides fluent API for setting up concurrent kernel execution with synchronization and load balancing.
/// </summary>
public interface IParallelStageBuilder
{
    /// <summary>
    /// Adds a kernel to the parallel execution group.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute in parallel</param>
    /// <param name="stageBuilder">Optional builder to configure the individual stage</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder AddKernel(string kernelName, Action<IKernelStageBuilder>? stageBuilder = null);

    /// <summary>
    /// Adds multiple kernels to the parallel execution group.
    /// </summary>
    /// <param name="kernelConfigs">Collection of kernel configurations</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder AddKernels(IEnumerable<ParallelKernelConfig> kernelConfigs);

    /// <summary>
    /// Sets the maximum degree of parallelism for this stage.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent executions</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithMaxDegreeOfParallelism(int maxDegreeOfParallelism);

    /// <summary>
    /// Configures the synchronization mode for parallel execution.
    /// </summary>
    /// <param name="mode">Synchronization mode to use</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithSynchronization(SynchronizationMode mode);

    /// <summary>
    /// Sets the load balancing strategy for distributing work across parallel kernels.
    /// </summary>
    /// <param name="strategy">Load balancing strategy</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithLoadBalancing(LoadBalancingStrategy strategy);

    /// <summary>
    /// Configures result aggregation for combining outputs from parallel kernels.
    /// </summary>
    /// <param name="aggregator">Function to aggregate parallel results</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithResultAggregation(Func<IEnumerable<object>, object> aggregator);

    /// <summary>
    /// Sets error handling strategy for parallel execution failures.
    /// </summary>
    /// <param name="strategy">Strategy for handling errors in parallel kernels</param>
    /// <param name="continueOnError">Whether to continue if some kernels fail</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithErrorHandling(ParallelErrorStrategy strategy, bool continueOnError = false);

    /// <summary>
    /// Configures timeout for the entire parallel stage.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for all parallel kernels to complete</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Sets memory sharing mode between parallel kernels.
    /// </summary>
    /// <param name="sharingMode">Memory sharing strategy</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithMemorySharing(MemorySharingMode sharingMode);

    /// <summary>
    /// Configures work partitioning for data-parallel operations.
    /// </summary>
    /// <param name="partitioner">Function to partition work across parallel kernels</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithWorkPartitioning(Func<object, IEnumerable<object>> partitioner);

    /// <summary>
    /// Sets priority for the parallel stage execution.
    /// </summary>
    /// <param name="priority">Execution priority level</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithPriority(Abstractions.Pipelines.Enums.ExecutionPriority priority);

    /// <summary>
    /// Configures barrier synchronization points within the parallel stage.
    /// </summary>
    /// <param name="barrierPoints">Points where all parallel kernels must synchronize</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithBarriers(params string[] barrierPoints);

    /// <summary>
    /// Sets resource allocation strategy for parallel kernels.
    /// </summary>
    /// <param name="strategy">Strategy for allocating resources to parallel kernels</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithResourceAllocation(ResourceAllocationStrategy strategy);

    /// <summary>
    /// Configures monitoring and metrics collection for parallel execution.
    /// </summary>
    /// <param name="enableDetailedMetrics">Whether to collect detailed per-kernel metrics</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithMonitoring(bool enableDetailedMetrics = true);

    /// <summary>
    /// Sets affinity rules for kernel placement on specific compute units.
    /// </summary>
    /// <param name="affinityRules">Rules for kernel-to-compute-unit affinity</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithAffinity(IEnumerable<AffinityRule> affinityRules);

    /// <summary>
    /// Configures adaptive execution that can adjust parallelism based on runtime conditions.
    /// </summary>
    /// <param name="adaptationPolicy">Policy for runtime adaptation</param>
    /// <returns>The parallel stage builder for fluent configuration</returns>
    public IParallelStageBuilder WithAdaptiveExecution(AdaptationPolicy adaptationPolicy);
}
