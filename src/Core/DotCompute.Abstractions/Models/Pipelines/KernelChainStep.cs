// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Pipelines
{
    /// <summary>
    /// Represents a single step in a kernel execution chain.
    /// </summary>
    public sealed class KernelChainStep
    {
        /// <summary>
        /// Gets or sets the unique step identifier.
        /// </summary>
        public required string StepId { get; set; }

        /// <summary>
        /// Gets or sets the step type.
        /// </summary>
        public required KernelChainStepType Type { get; set; }

        /// <summary>
        /// Gets or sets the kernel name for sequential steps.
        /// </summary>
        public string? KernelName { get; set; }

        /// <summary>
        /// Gets or sets the arguments to pass to the kernel.
        /// </summary>
        public object[] Arguments { get; set; } = Array.Empty<object>();

        /// <summary>
        /// Gets or sets the execution order of this step.
        /// </summary>
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Gets or sets the cache key for result caching.
        /// </summary>
        public string? CacheKey { get; set; }

        /// <summary>
        /// Gets or sets the cache time-to-live.
        /// </summary>
        public TimeSpan? CacheTtl { get; set; }

        /// <summary>
        /// Gets or sets the parallel kernels for parallel execution steps.
        /// </summary>
        public List<KernelChainStep>? ParallelKernels { get; set; }

        /// <summary>
        /// Gets or sets the branch condition for branching steps.
        /// </summary>
        public IBranchCondition? BranchCondition { get; set; }

        /// <summary>
        /// Gets or sets additional metadata for this step.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Types of kernel chain steps.
    /// </summary>
    public enum KernelChainStepType
    {
        /// <summary>
        /// Sequential kernel execution.
        /// </summary>
        Sequential,

        /// <summary>
        /// Parallel execution of multiple kernels.
        /// </summary>
        Parallel,

        /// <summary>
        /// Conditional branching based on previous results.
        /// </summary>
        Branch,

        /// <summary>
        /// Loop execution.
        /// </summary>
        Loop
    }

    /// <summary>
    /// Interface for branch conditions in kernel chains.
    /// </summary>
    public interface IBranchCondition
    {
        /// <summary>
        /// Gets the true path steps to execute when condition is true.
        /// </summary>
        List<KernelChainStep> TruePath { get; }

        /// <summary>
        /// Gets the false path steps to execute when condition is false.
        /// </summary>
        List<KernelChainStep> FalsePath { get; }

        /// <summary>
        /// Evaluates the condition based on the previous result.
        /// </summary>
        /// <param name="previousResult">The result from the previous step</param>
        /// <returns>True if the condition is met, false otherwise</returns>
        bool EvaluateCondition(object? previousResult);
    }

    /// <summary>
    /// Typed branch condition implementation.
    /// </summary>
    /// <typeparam name="T">The type of the input to the condition</typeparam>
    public sealed class BranchCondition<T> : IBranchCondition
    {
        /// <summary>
        /// Gets or sets the condition function.
        /// </summary>
        public required Func<T, bool> Condition { get; set; }

        /// <inheritdoc/>
        public List<KernelChainStep> TruePath { get; set; } = [];

        /// <inheritdoc/>
        public List<KernelChainStep> FalsePath { get; set; } = [];

        /// <inheritdoc/>
        public bool EvaluateCondition(object? previousResult)
        {
            if (previousResult is T typedResult)
            {
                return Condition(typedResult);
            }

            if (previousResult == null && !typeof(T).IsValueType)
            {
                return Condition(default!);
            }

            // Attempt type conversion
            try
            {
                var convertedResult = (T)Convert.ChangeType(previousResult, typeof(T))!;
                return Condition(convertedResult);
            }
            catch
            {
                // If conversion fails, condition is false
                return false;
            }
        }
    }
}
