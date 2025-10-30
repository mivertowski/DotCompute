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

        public async Task<bool> ValidateKernelArgumentsAsync(string kernelName, object[] arguments, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return true; // Stub implementation
        }
        /// <summary>
        /// Gets the optimization recommendations async.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The optimization recommendations async.</returns>

        public async Task<IReadOnlyCollection<KernelChainOptimizationRecommendation>> GetOptimizationRecommendationsAsync(IEnumerable<KernelChainStep> steps, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return Array.Empty<KernelChainOptimizationRecommendation>(); // Stub implementation
        }
    }
}
