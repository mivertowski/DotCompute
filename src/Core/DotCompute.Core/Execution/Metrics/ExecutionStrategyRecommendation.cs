// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.


using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Configuration;

using System;
namespace DotCompute.Core.Execution.Metrics
{
    /// <summary>
    /// Represents a recommendation for optimal execution strategy selection.
    /// </summary>
    /// <remarks>
    /// This class provides intelligent recommendations for choosing the most appropriate
    /// execution strategy based on workload characteristics, system configuration,
    /// and performance requirements. It includes confidence scoring, reasoning,
    /// and expected performance improvements to guide strategy selection decisions.
    /// </remarks>
    public class ExecutionStrategyRecommendation
    {
        /// <summary>
        /// Gets or sets the recommended strategy.
        /// </summary>
        /// <value>
        /// The execution strategy type that is recommended for optimal performance
        /// based on the analysis of workload characteristics and system capabilities.
        /// </value>
        public required ExecutionStrategyType Strategy { get; set; }

        /// <summary>
        /// Gets or sets the confidence score (0-1).
        /// </summary>
        /// <value>
        /// A confidence score between 0.0 and 1.0 indicating how certain the recommendation is.
        /// Higher values indicate greater confidence in the recommendation based on
        /// available data and analysis quality.
        /// </value>
        public required double ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets the reasoning for the recommendation.
        /// </summary>
        /// <value>
        /// A detailed explanation of why this strategy is recommended, including
        /// the key factors that influenced the decision and the expected benefits.
        /// </value>
        public required string Reasoning { get; set; }

        /// <summary>
        /// Gets or sets the expected performance improvement percentage.
        /// </summary>
        /// <value>
        /// The estimated percentage improvement in performance compared to the current
        /// or baseline strategy. Positive values indicate expected performance gains.
        /// </value>
        public double ExpectedImprovementPercentage { get; set; }

        /// <summary>
        /// Gets or sets recommended options for the strategy.
        /// </summary>
        /// <value>
        /// Strategy-specific configuration options that are recommended for optimal performance.
        /// The type of this object depends on the recommended strategy type.
        /// </value>
        public object? RecommendedOptions { get; set; }

        /// <summary>
        /// Gets a value indicating whether this is a high-confidence recommendation.
        /// </summary>
        /// <value>
        /// <c>true</c> if the confidence score is 0.8 or higher; otherwise, <c>false</c>.
        /// </value>
        public bool IsHighConfidence => ConfidenceScore >= 0.8;

        /// <summary>
        /// Gets a value indicating whether this recommendation promises significant improvement.
        /// </summary>
        /// <value>
        /// <c>true</c> if the expected improvement is 20% or higher; otherwise, <c>false</c>.
        /// </value>
        public bool PromisesSignificantImprovement => ExpectedImprovementPercentage >= 20.0;

        /// <summary>
        /// Gets a value indicating whether this recommendation should be acted upon.
        /// </summary>
        /// <value>
        /// <c>true</c> if the recommendation has high confidence and promises meaningful improvement; otherwise, <c>false</c>.
        /// </value>
        public bool ShouldActUpon => IsHighConfidence && ExpectedImprovementPercentage >= 10.0;

        /// <summary>
        /// Gets the risk level of following this recommendation.
        /// </summary>
        /// <value>
        /// A string indicating the risk level: "Low", "Medium", or "High" based on confidence score.
        /// </value>
        public string RiskLevel => ConfidenceScore switch
        {
            >= 0.8 => "Low",
            >= 0.6 => "Medium",
            _ => "High"
        };

        /// <summary>
        /// Creates a recommendation for data parallel strategy.
        /// </summary>
        /// <param name="confidence">The confidence score for this recommendation.</param>
        /// <param name="reasoning">The reasoning behind this recommendation.</param>
        /// <param name="improvement">The expected performance improvement percentage.</param>
        /// <param name="options">Optional data parallelism configuration options.</param>
        /// <returns>A new <see cref="ExecutionStrategyRecommendation"/> instance.</returns>
        public static ExecutionStrategyRecommendation ForDataParallel(
            double confidence,

            string reasoning,

            double improvement = 0.0,

            DataParallelismOptions? options = null)
        {
            return new ExecutionStrategyRecommendation
            {
                Strategy = ExecutionStrategyType.DataParallel,
                ConfidenceScore = confidence,
                Reasoning = reasoning,
                ExpectedImprovementPercentage = improvement,
                RecommendedOptions = options
            };
        }

        /// <summary>
        /// Creates a recommendation for model parallel strategy.
        /// </summary>
        /// <param name="confidence">The confidence score for this recommendation.</param>
        /// <param name="reasoning">The reasoning behind this recommendation.</param>
        /// <param name="improvement">The expected performance improvement percentage.</param>
        /// <param name="options">Optional model parallelism configuration options.</param>
        /// <returns>A new <see cref="ExecutionStrategyRecommendation"/> instance.</returns>
        public static ExecutionStrategyRecommendation ForModelParallel(
            double confidence,

            string reasoning,

            double improvement = 0.0,

            ModelParallelismOptions? options = null)
        {
            return new ExecutionStrategyRecommendation
            {
                Strategy = ExecutionStrategyType.ModelParallel,
                ConfidenceScore = confidence,
                Reasoning = reasoning,
                ExpectedImprovementPercentage = improvement,
                RecommendedOptions = options
            };
        }

        /// <summary>
        /// Creates a recommendation for pipeline parallel strategy.
        /// </summary>
        /// <param name="confidence">The confidence score for this recommendation.</param>
        /// <param name="reasoning">The reasoning behind this recommendation.</param>
        /// <param name="improvement">The expected performance improvement percentage.</param>
        /// <param name="options">Optional pipeline parallelism configuration options.</param>
        /// <returns>A new <see cref="ExecutionStrategyRecommendation"/> instance.</returns>
        public static ExecutionStrategyRecommendation ForPipelineParallel(
            double confidence,

            string reasoning,

            double improvement = 0.0,

            PipelineParallelismOptions? options = null)
        {
            return new ExecutionStrategyRecommendation
            {
                Strategy = ExecutionStrategyType.PipelineParallel,
                ConfidenceScore = confidence,
                Reasoning = reasoning,
                ExpectedImprovementPercentage = improvement,
                RecommendedOptions = options
            };
        }

        /// <summary>
        /// Creates a recommendation for work stealing strategy.
        /// </summary>
        /// <param name="confidence">The confidence score for this recommendation.</param>
        /// <param name="reasoning">The reasoning behind this recommendation.</param>
        /// <param name="improvement">The expected performance improvement percentage.</param>
        /// <param name="options">Optional work stealing configuration options.</param>
        /// <returns>A new <see cref="ExecutionStrategyRecommendation"/> instance.</returns>
        public static ExecutionStrategyRecommendation ForWorkStealing(
            double confidence,

            string reasoning,

            double improvement = 0.0,

            WorkStealingOptions? options = null)
        {
            return new ExecutionStrategyRecommendation
            {
                Strategy = ExecutionStrategyType.WorkStealing,
                ConfidenceScore = confidence,
                Reasoning = reasoning,
                ExpectedImprovementPercentage = improvement,
                RecommendedOptions = options
            };
        }

        /// <summary>
        /// Creates a recommendation for single device strategy.
        /// </summary>
        /// <param name="confidence">The confidence score for this recommendation.</param>
        /// <param name="reasoning">The reasoning behind this recommendation.</param>
        /// <param name="improvement">The expected performance improvement percentage.</param>
        /// <returns>A new <see cref="ExecutionStrategyRecommendation"/> instance.</returns>
        public static ExecutionStrategyRecommendation ForSingle(
            double confidence,

            string reasoning,

            double improvement = 0.0)
        {
            return new ExecutionStrategyRecommendation
            {
                Strategy = ExecutionStrategyType.Single,
                ConfidenceScore = confidence,
                Reasoning = reasoning,
                ExpectedImprovementPercentage = improvement,
                RecommendedOptions = null
            };
        }

        /// <summary>
        /// Validates the recommendation parameters.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when confidence score is not between 0 and 1.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when reasoning is null or whitespace.
        /// </exception>
        public void Validate()
        {
            if (ConfidenceScore is < 0.0 or > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(ConfidenceScore),

                    "Confidence score must be between 0.0 and 1.0.");
            }

            if (string.IsNullOrWhiteSpace(Reasoning))
            {
                throw new ArgumentException("Reasoning cannot be null or whitespace.", nameof(Reasoning));
            }
        }

        /// <summary>
        /// Compares this recommendation with another based on quality metrics.
        /// </summary>
        /// <param name="other">The recommendation to compare against.</param>
        /// <returns>
        /// A positive value if this recommendation is better, negative if worse, or zero if equivalent.
        /// </returns>
        public double CompareTo(ExecutionStrategyRecommendation other)
        {
            ArgumentNullException.ThrowIfNull(other);

            // Weighted comparison: confidence (60%) + expected improvement (40%)
            var confidenceScore = (ConfidenceScore - other.ConfidenceScore) * 60.0;
            var improvementScore = (ExpectedImprovementPercentage - other.ExpectedImprovementPercentage) * 0.4;

            return confidenceScore + improvementScore;
        }

        /// <summary>
        /// Returns a string representation of the strategy recommendation.
        /// </summary>
        /// <returns>
        /// A string containing the recommended strategy, confidence score, expected improvement, and reasoning summary.
        /// </returns>
        public override string ToString()
        {
            var reasoning = Reasoning.Length > 100 ? Reasoning[..97] + "..." : Reasoning;
            return $"{Strategy} (Confidence: {ConfidenceScore:P1}, " +
                   $"Improvement: {ExpectedImprovementPercentage:+0.0;-0.0;0}%, " +
                   $"Risk: {RiskLevel}): {reasoning}";
        }
    }
}