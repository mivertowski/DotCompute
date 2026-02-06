// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Pipelines;

using DotCompute.Abstractions.Pipelines.Results;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel chain validation service.
    /// </summary>
    public sealed class DefaultKernelChainValidator : IKernelChainValidator
    {

        /// <summary>
        /// Validates the chain async.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<KernelChainValidationResult> ValidateChainAsync(IEnumerable<KernelChainStep> steps, CancellationToken cancellationToken = default)
        {
            var stepsList = steps.ToList();
            var errors = new List<string>();
            var warnings = new List<string>();

            // Basic validation
            if (stepsList.Count == 0)
            {
                errors.Add("Kernel chain cannot be empty");
            }

            foreach (var step in stepsList)
            {
                if (string.IsNullOrEmpty(step.StepId))
                {
                    errors.Add($"Step at position {step.ExecutionOrder} missing StepId");
                }

                if (step.Type == KernelChainStepType.Sequential && string.IsNullOrEmpty(step.KernelName))
                {
                    errors.Add($"Sequential step {step.StepId} missing kernel name");
                }
            }

            await Task.CompletedTask;

            return new KernelChainValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }
        /// <summary>
        /// Validates the kernel arguments async.
        /// </summary>
        /// <param name="kernelName">The kernel name.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public Task<bool> ValidateKernelArgumentsAsync(string kernelName, object[] arguments, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(kernelName))
            {
                return Task.FromResult(false);
            }

            // Validate argument array is not null and contains no null device memory arguments
            if (arguments is null || arguments.Length == 0)
            {
                return Task.FromResult(false);
            }

            // Validate no null entries in arguments (null device pointers cause CUDA errors)
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] is null)
                {
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(true);
        }
        /// <summary>
        /// Gets the optimization recommendations async.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The optimization recommendations async.</returns>

        public Task<IReadOnlyCollection<KernelChainOptimizationRecommendation>> GetOptimizationRecommendationsAsync(IEnumerable<KernelChainStep> steps, CancellationToken cancellationToken = default)
        {
            var stepsList = steps.ToList();
            var recommendations = new List<KernelChainOptimizationRecommendation>();

            // Detect parallelizable adjacent sequential steps with no data dependencies
            var sequentialSteps = stepsList
                .Where(s => s.Type == KernelChainStepType.Sequential)
                .ToList();

            if (sequentialSteps.Count >= 2)
            {
                // Adjacent sequential steps with distinct kernel names may run in parallel
                for (var i = 0; i < sequentialSteps.Count - 1; i++)
                {
                    var current = sequentialSteps[i];
                    var next = sequentialSteps[i + 1];

                    if (current.KernelName != next.KernelName &&
                        current.Arguments.Count == 0 || !SharesArguments(current, next))
                    {
                        recommendations.Add(new KernelChainOptimizationRecommendation
                        {
                            Type = KernelChainOptimizationType.ParallelExecution,
                            Description = $"Steps '{current.StepId}' and '{next.StepId}' may execute in parallel",
                            EstimatedImpact = 0.3,
                            AffectedStepIds = [current.StepId, next.StepId],
                            Priority = KernelChainOptimizationPriority.Medium
                        });
                    }
                }
            }

            // Detect cacheable repeated kernels
            var duplicateKernels = sequentialSteps
                .Where(s => !string.IsNullOrEmpty(s.KernelName) && s.CacheKey == null)
                .GroupBy(s => s.KernelName)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateKernels)
            {
                recommendations.Add(new KernelChainOptimizationRecommendation
                {
                    Type = KernelChainOptimizationType.CachingOptimization,
                    Description = $"Kernel '{group.Key}' is invoked {group.Count()} times - consider caching results",
                    EstimatedImpact = 0.2,
                    AffectedStepIds = group.Select(s => s.StepId).ToList(),
                    Priority = KernelChainOptimizationPriority.Low
                });
            }

            // Detect kernel fusion opportunities (adjacent element-wise kernels)
            for (var i = 0; i < sequentialSteps.Count - 1; i++)
            {
                var current = sequentialSteps[i];
                var next = sequentialSteps[i + 1];

                if (current.Arguments.Count == next.Arguments.Count && current.Arguments.Count > 0)
                {
                    recommendations.Add(new KernelChainOptimizationRecommendation
                    {
                        Type = KernelChainOptimizationType.KernelFusion,
                        Description = $"Steps '{current.StepId}' and '{next.StepId}' with matching argument counts may be fusible",
                        EstimatedImpact = 0.25,
                        AffectedStepIds = [current.StepId, next.StepId],
                        Priority = KernelChainOptimizationPriority.Medium
                    });
                }
            }

            return Task.FromResult<IReadOnlyCollection<KernelChainOptimizationRecommendation>>(recommendations);
        }

        private static bool SharesArguments(KernelChainStep a, KernelChainStep b)
        {
            if (a.Arguments.Count == 0 || b.Arguments.Count == 0)
            {
                return false;
            }

            // Check by reference equality - shared buffer arguments indicate data dependency
            return a.Arguments.Any(argA => b.Arguments.Any(argB => ReferenceEquals(argA, argB)));
        }
    }
}
