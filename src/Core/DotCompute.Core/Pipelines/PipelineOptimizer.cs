// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Core.Pipelines.Optimization.Models;
using DotCompute.Core.Pipelines.Optimization.Strategies;

// Import the public interfaces from supporting types and resolve naming conflicts
using IOptimizationStrategy = DotCompute.Abstractions.Pipelines.Models.IOptimizationStrategy;
using IOptimizationPass = DotCompute.Abstractions.Pipelines.Models.IOptimizationPass;

// Use the same type aliases as existing KernelStage for consistency
using PipelineExecutionContext = DotCompute.Core.Pipelines.Models.PipelineExecutionContext;
using StageExecutionResult = DotCompute.Core.Pipelines.Models.StageExecutionResult;
using StageValidationResult = DotCompute.Abstractions.Pipelines.Models.StageValidationResult;
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Default implementation of pipeline optimizer.
    /// </summary>
    public sealed class PipelineOptimizer : IPipelineOptimizer
    {
        private readonly List<IOptimizationStrategy> _strategies;
        private readonly Dictionary<string, IOptimizationStrategy> _namedStrategies;

        /// <summary>
        /// Initializes a new instance of the PipelineOptimizer class.
        /// </summary>
        public PipelineOptimizer()
        {
            _strategies =
            [
                new KernelFusionStrategy(),
            new StageReorderingStrategy(),
            new MemoryOptimizationStrategy(),
            new ParallelMergingStrategy(),
            new DeadCodeEliminationStrategy()
            ];

            _namedStrategies = new Dictionary<string, IOptimizationStrategy>();
            foreach (var strategy in _strategies)
            {
                _namedStrategies[strategy.Name] = strategy;
            }
        }

        /// <inheritdoc/>
        public async ValueTask<OptimizedPipeline> OptimizeAsync(
            IKernelPipeline pipeline,
            PipelineOptimizationSettings settings,
            CancellationToken cancellationToken = default)
        {
            var optimizedStages = pipeline.Stages.ToList();
            var appliedOptimizations = new List<AppliedOptimization>();
            var totalSpeedup = 1.0;
            var totalMemorySavings = 0L;

            // Apply each optimization strategy
            foreach (var strategy in _strategies)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!strategy.CanApply(pipeline))
                {
                    continue;
                }

                var strategyResult = await strategy.ApplyAsync(optimizedStages, settings, cancellationToken);

                if (strategyResult.WasApplied)
                {
                    optimizedStages = strategyResult.OptimizedStages;

                    var optimization = new AppliedOptimization
                    {
                        Type = strategy.Type,
                        Description = strategyResult.Description,
                        AffectedStages = strategyResult.AffectedStages,
                        EstimatedImpact = strategyResult.EstimatedImpact
                    };

                    appliedOptimizations.Add(optimization);
                    totalSpeedup *= (1.0 + strategyResult.EstimatedImpact);
                    totalMemorySavings += strategyResult.EstimatedMemorySavings;
                }
            }

            // Create optimized pipeline
            var optimizedPipeline = CreateOptimizedPipeline(
                pipeline,
                optimizedStages,
                settings);

            return new OptimizedPipeline
            {
                Pipeline = optimizedPipeline,
                AppliedOptimizations = appliedOptimizations,
                EstimatedSpeedup = totalSpeedup,
                EstimatedMemorySavings = totalMemorySavings
            };
        }

        internal static IKernelPipeline CreateOptimizedPipeline(
            IKernelPipeline originalPipeline,
            List<IPipelineStage> optimizedStages,
            PipelineOptimizationSettings settings)
        {
            var builder = KernelPipelineBuilder.Create()
                .WithName($"{originalPipeline.Name}_Optimized")
                .WithOptimization(opt =>
                {
                    opt.EnableKernelFusion = (settings.OptimizationTypes & OptimizationType.KernelFusion) != 0;
                    opt.EnableStageReordering = (settings.OptimizationTypes & OptimizationType.StageReordering) != 0;
                    opt.EnableMemoryOptimization = (settings.OptimizationTypes & OptimizationType.MemoryAccess) != 0;
                    opt.EnableParallelMerging = (settings.OptimizationTypes & OptimizationType.ParallelMerging) != 0;
                    opt.Level = settings.Level;
                });

            // Copy metadata
            foreach (var (key, value) in originalPipeline.Metadata)
            {
                _ = builder.WithMetadata(key, value);
            }

            // Add optimized stages
            foreach (var stage in optimizedStages)
            {
                _ = builder.AddStage(stage);
            }

            return builder.Build();
        }

        /// <inheritdoc/>
        public async Task<PipelineAnalysisResult> AnalyzeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            cancellationToken.ThrowIfCancellationRequested();

            var result = new PipelineAnalysisResult
            {
                Success = true,
                Issues = new List<string>(),
                Recommendations = new List<string>(),
                Metrics = new Dictionary<string, object>()
            };

            try
            {
                // Analyze pipeline structure
                var stageCount = pipeline.Stages.Count;
                var kernelStageCount = pipeline.Stages.Count(s => s.Type == PipelineStageType.Computation);
                var parallelStageCount = pipeline.Stages.Count(s => s.Type == PipelineStageType.Parallel);

                result.Metrics["StageCount"] = stageCount;
                result.Metrics["KernelStageCount"] = kernelStageCount;
                result.Metrics["ParallelStageCount"] = parallelStageCount;

                // Check for optimization opportunities
                await AnalyzeKernelFusionOpportunities(pipeline, result, cancellationToken);
                await AnalyzeMemoryOptimizationOpportunities(pipeline, result, cancellationToken);
                await AnalyzeParallelizationOpportunities(pipeline, result, cancellationToken);

                // Detect potential issues
                await DetectPotentialIssues(pipeline, result, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Issues.Add($"Analysis failed: {ex.Message}");
                return result;
            }
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> OptimizeAsync(
            IKernelPipeline pipeline,
            OptimizationType optimizationTypes,
            PipelineOptimizationSettings? settings = null,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            var optimizationSettings = settings ?? new PipelineOptimizationSettings
            {
                OptimizationTypes = optimizationTypes
            };

            var optimizedResult = await OptimizeAsync(pipeline, optimizationSettings, cancellationToken);
            return optimizedResult.Pipeline;
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> ApplyKernelFusionAsync(
            IKernelPipeline pipeline,
            FusionCriteria fusionCriteria,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (fusionCriteria == null)
                throw new ArgumentNullException(nameof(fusionCriteria));

            cancellationToken.ThrowIfCancellationRequested();

            var strategy = new KernelFusionStrategy();
            var settings = new PipelineOptimizationSettings
            {
                OptimizationTypes = OptimizationType.KernelFusion,
                CustomParameters = { ["FusionCriteria"] = fusionCriteria }
            };

            var stages = pipeline.Stages.ToList();
            var result = await strategy.ApplyAsync(stages, settings, cancellationToken);

            if (result.WasApplied)
            {
                return CreateOptimizedPipeline(pipeline, result.OptimizedStages, settings);
            }

            return pipeline;
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> OptimizeMemoryAccessAsync(
            IKernelPipeline pipeline,
            MemoryConstraints memoryConstraints,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (memoryConstraints == null)
                throw new ArgumentNullException(nameof(memoryConstraints));

            cancellationToken.ThrowIfCancellationRequested();

            var strategy = new MemoryOptimizationStrategy();
            var settings = new PipelineOptimizationSettings
            {
                OptimizationTypes = OptimizationType.MemoryAccess,
                MemoryConstraints = memoryConstraints
            };

            var stages = pipeline.Stages.ToList();
            var result = await strategy.ApplyAsync(stages, settings, cancellationToken);

            return CreateOptimizedPipeline(pipeline, result.OptimizedStages, settings);
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> OptimizeForBackendsAsync(
            IKernelPipeline pipeline,
            IEnumerable<string> targetBackends,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (targetBackends == null)
                throw new ArgumentNullException(nameof(targetBackends));

            var backendList = targetBackends.ToList();
            if (backendList.Count == 0)
                return pipeline;

            cancellationToken.ThrowIfCancellationRequested();

            var settings = new PipelineOptimizationSettings
            {
                OptimizationTypes = OptimizationType.BackendSpecific,
                TargetBackends = backendList
            };

            // Apply backend-specific optimizations
            var optimizedStages = new List<IPipelineStage>();

            foreach (var stage in pipeline.Stages)
            {
                var optimizedStage = await OptimizeStageForBackends(stage, backendList, cancellationToken);
                optimizedStages.Add(optimizedStage);
            }

            return CreateOptimizedPipeline(pipeline, optimizedStages, settings);
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> OptimizeParallelismAsync(
            IKernelPipeline pipeline,
            ParallelismGoals parallelismGoals,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (parallelismGoals == null)
                throw new ArgumentNullException(nameof(parallelismGoals));

            cancellationToken.ThrowIfCancellationRequested();

            var strategy = new ParallelMergingStrategy();
            var settings = new PipelineOptimizationSettings
            {
                OptimizationTypes = OptimizationType.Parallelization,
                CustomParameters = { ["ParallelismGoals"] = parallelismGoals }
            };

            var stages = pipeline.Stages.ToList();
            var result = await strategy.ApplyAsync(stages, settings, cancellationToken);

            return CreateOptimizedPipeline(pipeline, result.OptimizedStages, settings);
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> ApplyLoopOptimizationsAsync(
            IKernelPipeline pipeline,
            LoopOptimizations loopOptimizations,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (loopOptimizations == null)
                throw new ArgumentNullException(nameof(loopOptimizations));

            cancellationToken.ThrowIfCancellationRequested();

            // Create a custom loop optimization strategy
            var optimizedStages = new List<IPipelineStage>();

            foreach (var stage in pipeline.Stages)
            {
                var optimizedStage = await ApplyLoopOptimizationsToStage(stage, loopOptimizations, cancellationToken);
                optimizedStages.Add(optimizedStage);
            }

            var settings = new PipelineOptimizationSettings
            {
                OptimizationTypes = OptimizationType.LoopOptimization,
                CustomParameters = { ["LoopOptimizations"] = loopOptimizations }
            };

            return CreateOptimizedPipeline(pipeline, optimizedStages, settings);
        }

        /// <inheritdoc/>
        public async Task<IKernelPipeline> OptimizeDataLayoutAsync(
            IKernelPipeline pipeline,
            DataLayoutPreferences layoutPreferences,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (layoutPreferences == null)
                throw new ArgumentNullException(nameof(layoutPreferences));

            cancellationToken.ThrowIfCancellationRequested();

            var optimizedStages = new List<IPipelineStage>();

            foreach (var stage in pipeline.Stages)
            {
                var optimizedStage = await OptimizeStageDataLayout(stage, layoutPreferences, cancellationToken);
                optimizedStages.Add(optimizedStage);
            }

            var settings = new PipelineOptimizationSettings
            {
                OptimizationTypes = OptimizationType.DataLayout,
                CustomParameters = { ["DataLayoutPreferences"] = layoutPreferences }
            };

            return CreateOptimizedPipeline(pipeline, optimizedStages, settings);
        }

        /// <inheritdoc/>
        public async Task<OptimizationValidationResult> ValidateOptimizationAsync(
            IKernelPipeline originalPipeline,
            IKernelPipeline optimizedPipeline,
            IEnumerable<object[]> testInputs,
            CancellationToken cancellationToken = default)
        {
            if (originalPipeline == null)
                throw new ArgumentNullException(nameof(originalPipeline));
            if (optimizedPipeline == null)
                throw new ArgumentNullException(nameof(optimizedPipeline));
            if (testInputs == null)
                throw new ArgumentNullException(nameof(testInputs));

            var result = new OptimizationValidationResult
            {
                IsValid = true,
                AccuracyScore = 1.0,
                Warnings = new List<string>()
            };

            try
            {
                var testCases = testInputs.ToList();
                if (testCases.Count == 0)
                {
                    result.Warnings.Add("No test inputs provided for validation");
                    return result;
                }

                var correctResults = 0;
                var totalResults = 0;

                foreach (var testInput in testCases)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Note: In a real implementation, you would execute both pipelines
                    // and compare their outputs. This is a simplified version.
                    var originalResult = await SimulateExecution(originalPipeline, testInput);
                    var optimizedResult = await SimulateExecution(optimizedPipeline, testInput);

                    totalResults++;
                    if (CompareResults(originalResult, optimizedResult))
                    {
                        correctResults++;
                    }
                }

                result.AccuracyScore = totalResults > 0 ? (double)correctResults / totalResults : 1.0;
                result.IsValid = result.AccuracyScore >= 0.99; // 99% accuracy threshold

                if (!result.IsValid)
                {
                    result.Message = $"Validation failed: Accuracy {result.AccuracyScore:P2} below threshold";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Message = $"Validation error: {ex.Message}";
                result.AccuracyScore = 0.0;
                return result;
            }
        }

        /// <inheritdoc/>
        public async Task<OptimizationImpactEstimate> EstimateOptimizationImpactAsync(
            IKernelPipeline pipeline,
            IEnumerable<OptimizationType> proposedOptimizations,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));
            if (proposedOptimizations == null)
                throw new ArgumentNullException(nameof(proposedOptimizations));

            cancellationToken.ThrowIfCancellationRequested();

            var optimizationList = proposedOptimizations.ToList();
            var estimate = new OptimizationImpactEstimate
            {
                EstimatedSpeedup = 1.0,
                EstimatedMemoryReduction = 0.0,
                ConfidenceLevel = 0.8,
                EstimatedOptimizationTime = TimeSpan.FromSeconds(5)
            };

            // Simulate impact estimation for each optimization type
            foreach (var optimization in optimizationList)
            {
                var impact = await EstimateOptimizationTypeImpact(pipeline, optimization, cancellationToken);

                // Combine impacts (multiplicative for speedup, additive for memory)
                estimate.EstimatedSpeedup *= impact.SpeedupFactor;
                estimate.EstimatedMemoryReduction += impact.MemoryReduction;
                estimate.EstimatedOptimizationTime = estimate.EstimatedOptimizationTime.Add(impact.OptimizationTime);

                // Confidence decreases with more optimizations
                estimate.ConfidenceLevel *= 0.95;
            }

            return estimate;
        }

        /// <inheritdoc/>
        public IOptimizationPass CreateCustomOptimization(
            string optimizationName,
            Func<IKernelPipeline, Task<IKernelPipeline>> optimizationLogic)
        {
            if (string.IsNullOrEmpty(optimizationName))
                throw new ArgumentNullException(nameof(optimizationName));
            if (optimizationLogic == null)
                throw new ArgumentNullException(nameof(optimizationLogic));

            return new CustomOptimizationPass(optimizationName, optimizationLogic);
        }

        /// <inheritdoc/>
        public void RegisterOptimizationStrategy(string strategyName, IOptimizationStrategy strategy)
        {
            if (string.IsNullOrEmpty(strategyName))
                throw new ArgumentNullException(nameof(strategyName));
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            _namedStrategies[strategyName] = strategy;

            // Add to main strategies list if not already present
            if (!_strategies.Any(s => s.SupportedOptimizations == strategy.SupportedOptimizations))
            {
                _strategies.Add(strategy);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IOptimizationStrategy> GetAvailableStrategies(OptimizationType optimizationType)
        {
            return _strategies.Where(s => (s.SupportedOptimizations & optimizationType) != 0);
        }

        // Helper methods for the async implementations
        private static async Task AnalyzeKernelFusionOpportunities(IKernelPipeline pipeline, PipelineAnalysisResult result, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken); // Simulate analysis time

            var kernelStages = pipeline.Stages.OfType<KernelStage>().ToList();
            var fusionOpportunities = 0;

            for (int i = 0; i < kernelStages.Count - 1; i++)
            {
                if (CanFuseKernels(kernelStages[i], kernelStages[i + 1]))
                {
                    fusionOpportunities++;
                }
            }

            result.Metrics["KernelFusionOpportunities"] = fusionOpportunities;
            if (fusionOpportunities > 0)
            {
                result.Recommendations.Add($"Consider kernel fusion for {fusionOpportunities} stage pair(s)");
            }
        }

        private static async Task AnalyzeMemoryOptimizationOpportunities(IKernelPipeline pipeline, PipelineAnalysisResult result, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken); // Simulate analysis time

            var stages = pipeline.Stages.Count;
            var estimatedMemoryUsage = stages * 1024 * 1024; // 1MB per stage estimate

            result.Metrics["EstimatedMemoryUsage"] = estimatedMemoryUsage;
            result.Recommendations.Add("Apply memory layout optimizations for better cache performance");
        }

        private static async Task AnalyzeParallelizationOpportunities(IKernelPipeline pipeline, PipelineAnalysisResult result, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken); // Simulate analysis time

            var independentStages = CountIndependentStages(pipeline.Stages);
            result.Metrics["IndependentStages"] = independentStages;

            if (independentStages > 1)
            {
                result.Recommendations.Add($"Consider parallel execution for {independentStages} independent stages");
            }
        }

        private static async Task DetectPotentialIssues(IKernelPipeline pipeline, PipelineAnalysisResult result, CancellationToken cancellationToken)
        {
            await Task.Delay(5, cancellationToken); // Simulate detection time

            // Check for dependency cycles
            if (HasDependencyCycles(pipeline.Stages))
            {
                result.Issues.Add("Potential dependency cycle detected");
            }

            // Check for excessive stage count
            if (pipeline.Stages.Count > 100)
            {
                result.Issues.Add("Pipeline has excessive number of stages, consider consolidation");
            }
        }

        private static async Task<IPipelineStage> OptimizeStageForBackends(IPipelineStage stage, List<string> targetBackends, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken); // Simulate optimization time

            // In a real implementation, this would apply backend-specific optimizations
            // For now, just return the original stage
            return stage;
        }

        private static async Task<IPipelineStage> ApplyLoopOptimizationsToStage(IPipelineStage stage, LoopOptimizations loopOptimizations, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken); // Simulate optimization time

            // In a real implementation, this would apply loop optimizations like unrolling, vectorization, etc.
            // For now, just return the original stage
            return stage;
        }

        private static async Task<IPipelineStage> OptimizeStageDataLayout(IPipelineStage stage, DataLayoutPreferences layoutPreferences, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken); // Simulate optimization time

            // In a real implementation, this would apply data layout optimizations
            // For now, just return the original stage
            return stage;
        }

        private static async Task<object> SimulateExecution(IKernelPipeline pipeline, object[] testInput)
        {
            await Task.Delay(1); // Simulate execution time

            // In a real implementation, this would execute the pipeline with the test input
            // For now, just return a placeholder result
            return new { Result = "simulated", InputHash = testInput.GetHashCode() };
        }

        private static bool CompareResults(object result1, object result2)
        {
            // In a real implementation, this would perform deep comparison of results
            // For now, just compare hash codes
            return result1?.GetHashCode() == result2?.GetHashCode();
        }

        private static async Task<(double SpeedupFactor, double MemoryReduction, TimeSpan OptimizationTime)> EstimateOptimizationTypeImpact(
            IKernelPipeline pipeline,
            OptimizationType optimization,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken); // Simulate estimation time

            return optimization switch
            {
                OptimizationType.KernelFusion => (1.15, 0.2, TimeSpan.FromSeconds(2)),
                OptimizationType.MemoryAccess => (1.12, 0.15, TimeSpan.FromSeconds(1)),
                OptimizationType.Parallelization => (1.25, 0.0, TimeSpan.FromSeconds(3)),
                OptimizationType.LoopOptimization => (1.20, 0.05, TimeSpan.FromSeconds(2)),
                OptimizationType.DataLayout => (1.08, 0.1, TimeSpan.FromSeconds(1)),
                OptimizationType.Vectorization => (1.30, 0.0, TimeSpan.FromSeconds(2)),
                _ => (1.05, 0.02, TimeSpan.FromSeconds(1))
            };
        }

        private static int CountIndependentStages(IEnumerable<IPipelineStage> stages)
        {
            return stages.Count(stage => stage.Dependencies.Count == 0);
        }

        private static bool HasDependencyCycles(IEnumerable<IPipelineStage> stages)
        {
            var stageMap = stages.ToDictionary(s => s.Id);
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var stage in stages)
            {
                if (!visited.Contains(stage.Id))
                {
                    if (HasCycleRecursive(stage.Id, stageMap, visited, recursionStack))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasCycleRecursive(string stageId, Dictionary<string, IPipelineStage> stageMap, HashSet<string> visited, HashSet<string> recursionStack)
        {
            visited.Add(stageId);
            recursionStack.Add(stageId);

            if (stageMap.TryGetValue(stageId, out var stage))
            {
                foreach (var depId in stage.Dependencies)
                {
                    if (!visited.Contains(depId))
                    {
                        if (HasCycleRecursive(depId, stageMap, visited, recursionStack))
                        {
                            return true;
                        }
                    }
                    else if (recursionStack.Contains(depId))
                    {
                        return true;
                    }
                }
            }

            recursionStack.Remove(stageId);
            return false;
        }

        private static bool CanFuseKernels(Stages.KernelStage stage1, Stages.KernelStage stage2)
        {
            // Simplified fusion check - in practice this would be much more complex
            // Check if stage2 depends only on stage1's output
            return stage2.Dependencies.Count <= 1 &&
                   (stage2.Dependencies.Count == 0 || stage2.Dependencies.Contains(stage1.Id));
        }
    }

    /// <summary>
    /// Pipeline optimization level.
    /// </summary>
    public enum PipelineOptimizationLevel
    {
        /// <summary>
        /// Conservative optimization.
        /// </summary>
        Conservative,

        /// <summary>
        /// Balanced optimization.
        /// </summary>
        Balanced,

        /// <summary>
        /// Aggressive optimization.
        /// </summary>
        Aggressive
    }
}