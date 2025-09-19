// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Pipelines;
using Microsoft.Extensions.Logging;

using DotCompute.Abstractions.Pipelines.Results;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel chain validation service.
    /// </summary>
    public sealed class DefaultKernelChainValidator : IKernelChainValidator
    {
        private readonly ILogger<DefaultKernelChainValidator>? _logger;

        public DefaultKernelChainValidator(ILogger<DefaultKernelChainValidator>? logger = null)
        {
            _logger = logger;
        }

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
                Errors = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }

        public async Task<bool> ValidateKernelArgumentsAsync(string kernelName, object[] arguments, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return true; // Stub implementation
        }

        public async Task<IReadOnlyCollection<KernelChainOptimizationRecommendation>> GetOptimizationRecommendationsAsync(IEnumerable<KernelChainStep> steps, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return Array.Empty<KernelChainOptimizationRecommendation>(); // Stub implementation
        }
    }
}
