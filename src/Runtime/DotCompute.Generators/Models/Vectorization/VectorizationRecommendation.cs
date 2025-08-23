// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models.Vectorization.Enums;

namespace DotCompute.Generators.Models.Vectorization;

/// <summary>
/// Provides a comprehensive recommendation for vectorization based on static analysis results.
/// This class encapsulates the decision-making logic for whether a given code pattern
/// should be optimized using SIMD vectorization techniques.
/// </summary>
/// <remarks>
/// The recommendation is generated through analysis of code patterns, data dependencies,
/// computational complexity, and estimated performance benefits. It serves as input
/// for the code generation pipeline to make informed optimization decisions.
/// </remarks>
public class VectorizationRecommendation
{
    /// <summary>
    /// Gets or sets a value indicating whether vectorization is recommended for the analyzed code.
    /// </summary>
    /// <value>
    /// <c>true</c> if vectorization should be applied; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This decision is based on the combination of pattern analysis, estimated performance benefits,
    /// and the absence of blocking conditions such as complex data dependencies.
    /// </remarks>
    public bool ShouldVectorize { get; set; }

    /// <summary>
    /// Gets or sets the human-readable reason explaining the vectorization recommendation.
    /// </summary>
    /// <value>
    /// A descriptive string explaining why vectorization is or is not recommended.
    /// </value>
    /// <remarks>
    /// This property provides transparency into the decision-making process and can be
    /// used for debugging, logging, or user feedback in development tools.
    /// </remarks>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated level of benefit that vectorization would provide.
    /// </summary>
    /// <value>
    /// A <see cref="VectorizationBenefit"/> value indicating the expected performance improvement level.
    /// </value>
    /// <remarks>
    /// This classification helps prioritize optimization efforts and provides guidance
    /// for resource allocation in the compilation process.
    /// </remarks>
    public VectorizationBenefit EstimatedBenefit { get; set; }

    /// <summary>
    /// Gets or sets the estimated speedup factor that vectorization could achieve.
    /// </summary>
    /// <value>
    /// A positive double value representing the expected speedup multiplier.
    /// A value of 1.0 indicates no speedup, while higher values indicate greater performance improvements.
    /// </value>
    /// <remarks>
    /// This value is calculated using heuristics based on loop structure, arithmetic operations,
    /// and pattern recognition. It provides a quantitative estimate to complement the qualitative
    /// benefit classification.
    /// </remarks>
    public double EstimatedSpeedup { get; set; } = 1.0;
}