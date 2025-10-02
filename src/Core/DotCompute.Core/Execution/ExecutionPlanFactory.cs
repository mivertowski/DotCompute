// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using CoreWorkloadType = DotCompute.Core.Execution.Types.WorkloadType;
using ExecutionStrategyType = DotCompute.Abstractions.Types.ExecutionStrategyType;
using DotCompute.Core.Execution.Configuration;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Execution.Analysis;
using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Optimization;
using DotCompute.Core.Execution.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Factory for creating optimal execution plans based on workload characteristics and available resources.
    /// Provides methods to create single optimal plans or multiple plan alternatives for comparison.
    /// </summary>
    public sealed class ExecutionPlanFactory
    {
        private readonly ExecutionPlanGenerator _generator;
        private readonly ExecutionPlanOptimizer _optimizer;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the ExecutionPlanFactory class.
        /// </summary>
        /// <param name="logger">Logger for factory operations</param>
        /// <param name="performanceMonitor">Performance monitor for execution estimation</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or performanceMonitor is null</exception>
        public ExecutionPlanFactory(ILogger logger, PerformanceMonitor performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _generator = new ExecutionPlanGenerator(logger, performanceMonitor);
            _optimizer = new ExecutionPlanOptimizer(logger, performanceMonitor);
        }

        /// <summary>
        /// Creates an optimal execution plan based on workload characteristics and available resources.
        /// Analyzes the workload, recommends the best execution strategy, and applies optimizations.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="workload">The computational workload to execute</param>
        /// <param name="availableDevices">Available accelerator devices</param>
        /// <param name="constraints">Execution constraints and preferences</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>An optimized execution plan for the workload</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="InvalidOperationException">Thrown when no suitable execution plan can be created</exception>
        public async ValueTask<ExecutionPlan<T>> CreateOptimalPlanAsync<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(workload);
            ArgumentNullException.ThrowIfNull(availableDevices);
            ArgumentNullException.ThrowIfNull(constraints);

            _logger.LogInfoMessage($"Creating optimal execution plan for workload {workload.WorkloadType} with {availableDevices.Length} devices");

            // Analyze workload characteristics
            var workloadAnalysis = AnalyzeWorkloadAsync(workload, cancellationToken);

            // Recommend optimal strategy
            var strategyRecommendation = RecommendExecutionStrategyAsync(
                workloadAnalysis, availableDevices, constraints, cancellationToken);

            // Generate plan based on recommended strategy
            var plan = await GeneratePlanForStrategyAsync(
                workload, availableDevices, strategyRecommendation, constraints, cancellationToken);

            // Apply cross-cutting optimizations
            await _optimizer.OptimizePlanAsync(plan, cancellationToken);

            _logger.LogInfoMessage($"Created {plan.StrategyType} execution plan with estimated time {plan.EstimatedExecutionTimeMs}ms");

            return plan!;
        }

        /// <summary>
        /// Creates multiple execution plan alternatives for comparison and selection.
        /// Generates viable plans using different execution strategies and ranks them by performance.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="workload">The computational workload to execute</param>
        /// <param name="availableDevices">Available accelerator devices</param>
        /// <param name="constraints">Execution constraints and preferences</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A collection of execution plan alternatives with recommendations</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        public async ValueTask<ExecutionPlanAlternatives<T>> CreatePlanAlternativesAsync<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            var alternatives = new List<ExecutionPlan<T>>();
            var strategies = GetViableStrategies<T>(workload, availableDevices, constraints);

            foreach (var strategy in strategies)
            {
                try
                {
                    var plan = await GeneratePlanForSpecificStrategyAsync(
                        workload, availableDevices, strategy, constraints, cancellationToken);

                    alternatives.Add(plan);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate plan for strategy {Strategy}", strategy);
                }
            }

            // Rank alternatives by estimated performance
            var rankedAlternatives = alternatives
                .OrderBy(p => p.EstimatedExecutionTimeMs)
                .ToArray();

            return new ExecutionPlanAlternatives<T>
            {
                RecommendedPlan = rankedAlternatives.FirstOrDefault(),
                AllAlternatives = rankedAlternatives,
                SelectionCriteria = "Estimated execution time"
            };
        }

        #region Private Methods

        /// <summary>
        /// Analyzes workload characteristics to inform execution strategy selection.
        /// </summary>
        private WorkloadAnalysis AnalyzeWorkloadAsync<T>(ExecutionWorkload<T> workload,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var analysis = new WorkloadAnalysis
            {
                WorkloadType = workload.WorkloadType,
                DataSize = workload.GetTotalDataSize(),
                ComputeIntensity = workload.GetComputeIntensity(),
                MemoryIntensity = workload.GetMemoryIntensity(),
                ParallelizationPotential = workload.GetParallelizationPotential(),
                DependencyComplexity = workload.GetDependencyComplexity()
            };

            _logger.LogDebugMessage($"Workload analysis: DataSize={analysis.DataSize}, ComputeIntensity={analysis.ComputeIntensity}, MemoryIntensity={analysis.MemoryIntensity}");
            return analysis;
        }

        /// <summary>
        /// Recommends the optimal execution strategy based on workload analysis and constraints.
        /// </summary>
        private static ExecutionStrategyType RecommendExecutionStrategyAsync(WorkloadAnalysis workloadAnalysis,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken)
        {
            var deviceCount = availableDevices.Length;
            var dataSize = workloadAnalysis.DataSize;
            var computeIntensity = workloadAnalysis.ComputeIntensity;
            var parallelizationPotential = workloadAnalysis.ParallelizationPotential;

            // Strategy selection heuristics
            if (deviceCount == 1)
            {
                return ExecutionStrategyType.Single;
            }

            if (workloadAnalysis.WorkloadType == CoreWorkloadType.Pipeline)
            {
                return ExecutionStrategyType.PipelineParallel;
            }

            if (workloadAnalysis.WorkloadType == CoreWorkloadType.ModelParallel)
            {
                return ExecutionStrategyType.ModelParallel;
            }

            if (dataSize > 100_000_000 && parallelizationPotential > 0.7) // Large data with high parallelization potential
            {
                return computeIntensity > 0.5 ? ExecutionStrategyType.DataParallel : ExecutionStrategyType.WorkStealing;
            }

            if (workloadAnalysis.DependencyComplexity > 0.5) // Complex dependencies
            {
                return ExecutionStrategyType.WorkStealing;
            }

            // Default to data parallel for multi-device scenarios
            return ExecutionStrategyType.DataParallel;
        }

        /// <summary>
        /// Generates an execution plan for a specific strategy.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GeneratePlanForStrategyAsync<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionStrategyType strategy,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken) where T : unmanaged
        {
            return strategy switch
            {
                ExecutionStrategyType.DataParallel => await GenerateDataParallelPlan(workload, availableDevices, constraints, cancellationToken),
                ExecutionStrategyType.ModelParallel => await GenerateModelParallelPlan(workload, availableDevices, constraints, cancellationToken),
                ExecutionStrategyType.PipelineParallel => await GeneratePipelinePlan(workload, availableDevices, constraints, cancellationToken),
                ExecutionStrategyType.WorkStealing => await GenerateWorkStealingPlan(workload, availableDevices, constraints, cancellationToken),
                _ => await GenerateSingleDevicePlan(workload, availableDevices, constraints, cancellationToken)
            };
        }

        /// <summary>
        /// Generates a data parallel execution plan.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GenerateDataParallelPlan<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var dataParallelWorkload = workload as DataParallelWorkload<T> ??
                throw new ArgumentException("Workload must be DataParallelWorkload for data parallel execution");

            var options = new DataParallelismOptions
            {
                MaxDevices = constraints.MaxDevices,
                TargetDevices = constraints.PreferredDeviceIds,
                LoadBalancing = constraints.LoadBalancingStrategy ?? LoadBalancingStrategy.Adaptive,
                EnablePeerToPeer = constraints.EnablePeerToPeer,
                SyncStrategy = constraints.SynchronizationStrategy ?? SynchronizationStrategy.EventBased
            };

            return await _generator.GenerateDataParallelPlanAsync(
                dataParallelWorkload.KernelName,
                availableDevices,
                dataParallelWorkload.InputBuffers,
                dataParallelWorkload.OutputBuffers,
                options,
                cancellationToken);
        }

        /// <summary>
        /// Generates a model parallel execution plan.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GenerateModelParallelPlan<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var modelWorkload = workload as ModelParallelWorkload<T> ??
                throw new ArgumentException("Workload must be ModelParallelWorkload for model parallel execution");

            var options = new ModelParallelismOptions
            {
                LayerAssignment = constraints.LayerAssignmentStrategy ?? LayerAssignmentStrategy.Automatic,
                CommunicationBackend = constraints.CommunicationBackend ?? CommunicationBackend.P2P,
                MemoryOptimization = constraints.MemoryOptimizationLevel ?? MemoryOptimizationLevel.Balanced
            };

            return await _generator.GenerateModelParallelPlanAsync(modelWorkload, availableDevices, options, cancellationToken);
        }

        /// <summary>
        /// Generates a pipeline execution plan.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GeneratePipelinePlan<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var pipelineWorkload = workload as PipelineWorkload<T> ??
                throw new ArgumentException("Workload must be PipelineWorkload for pipeline execution");

            var options = new PipelineParallelismOptions
            {
                StageCount = Math.Min(availableDevices.Length, pipelineWorkload.PipelineDefinition.Stages.Count),
                MicrobatchSize = constraints.MicrobatchSize ?? 1,
                BufferDepth = constraints.BufferDepth ?? 2,
                SchedulingStrategy = constraints.PipelineSchedulingStrategy ?? PipelineSchedulingStrategy.FillDrain
            };

            return await _generator.GeneratePipelinePlanAsync(
                pipelineWorkload.PipelineDefinition, availableDevices, options, cancellationToken);
        }

        /// <summary>
        /// Generates a work stealing execution plan.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GenerateWorkStealingPlan<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken) where T : unmanaged
            // Work stealing plans would be implemented similar to data parallel
            // but with different load balancing and synchronization strategies

            => await GenerateDataParallelPlan(workload, availableDevices, constraints, cancellationToken);

        /// <summary>
        /// Generates a single device execution plan.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GenerateSingleDevicePlan<T>(
        ExecutionWorkload<T> workload,
        IAccelerator[] availableDevices,
        ExecutionConstraints constraints,
        CancellationToken cancellationToken) where T : unmanaged
        {
            // Use the best available device for single-device execution
            var bestDevice = availableDevices.OrderByDescending(d => d.Info.ComputeUnits * d.Info.MaxClockFrequency).First();

            // Create a simple single-device data parallel plan
            var options = new DataParallelismOptions
            {
                MaxDevices = 1,
                TargetDevices = [bestDevice.Info.Id]
            };

            if (workload is DataParallelWorkload<T> dataWorkload)
            {
                return await _generator.GenerateDataParallelPlanAsync(
                    dataWorkload.KernelName,
                    [bestDevice],
                    dataWorkload.InputBuffers,
                    dataWorkload.OutputBuffers,
                    options,
                    cancellationToken);
            }

            throw new NotSupportedException($"Single device execution not supported for workload type {workload.WorkloadType}");
        }

        /// <summary>
        /// Generates a plan for a specific strategy.
        /// </summary>
        private async ValueTask<ExecutionPlan<T>> GeneratePlanForSpecificStrategyAsync<T>(
            ExecutionWorkload<T> workload,
            IAccelerator[] availableDevices,
            ExecutionStrategyType strategy,
            ExecutionConstraints constraints,
            CancellationToken cancellationToken) where T : unmanaged => await GeneratePlanForStrategyAsync(workload, availableDevices, strategy, constraints, cancellationToken);

        /// <summary>
        /// Gets viable execution strategies for the given workload and devices.
        /// </summary>
        private static ExecutionStrategyType[] GetViableStrategies<T>(
        ExecutionWorkload<T> workload,
        IAccelerator[] availableDevices,
        ExecutionConstraints constraints) where T : unmanaged
        {
            var strategies = new List<ExecutionStrategyType> { ExecutionStrategyType.Single };

            if (availableDevices.Length > 1)
            {
                strategies.Add(ExecutionStrategyType.DataParallel);
                strategies.Add(ExecutionStrategyType.WorkStealing);

                if (workload.WorkloadType == CoreWorkloadType.ModelParallel)
                {
                    strategies.Add(ExecutionStrategyType.ModelParallel);
                }

                if (workload.WorkloadType == CoreWorkloadType.Pipeline)
                {
                    strategies.Add(ExecutionStrategyType.PipelineParallel);
                }
            }

            return [.. strategies];
        }

        #endregion
    }
}
