// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Models;

/// <summary>
/// Information about vectorization opportunities in code.
/// </summary>
public class VectorizationInfo
{
    /// <summary>
    /// Gets or sets the total number of loops in the method.
    /// </summary>
    public int LoopCount { get; set; }

    /// <summary>
    /// Gets or sets the number of loops that can be vectorized.
    /// </summary>
    public int VectorizableLoops { get; set; }

    /// <summary>
    /// Gets or sets whether the method contains array access operations.
    /// </summary>
    public bool HasArrayAccess { get; set; }

    /// <summary>
    /// Gets or sets the count of arithmetic operations.
    /// </summary>
    public int ArithmeticOperationCount { get; set; }

    /// <summary>
    /// Gets whether the method is suitable for vectorization.
    /// </summary>
    public bool IsVectorizable => VectorizableLoops > 0 && HasArrayAccess;

    /// <summary>
    /// Gets whether the method performs arithmetic operations.
    /// </summary>
    public bool IsArithmetic => ArithmeticOperationCount > 0;

    /// <summary>
    /// Gets whether the method performs memory operations.
    /// </summary>
    public bool IsMemoryOperation => HasArrayAccess;

    /// <summary>
    /// Gets or sets whether the method has data dependencies that prevent vectorization.
    /// </summary>
    public bool HasDataDependency { get; set; }

    /// <summary>
    /// Gets or sets whether the method contains reduction patterns (sum, min, max).
    /// </summary>
    public bool HasReductionPattern { get; set; }

    /// <summary>
    /// Gets or sets whether the method contains dot product patterns.
    /// </summary>
    public bool HasDotProductPattern { get; set; }

    /// <summary>
    /// Gets or sets whether the method contains matrix multiplication patterns.
    /// </summary>
    public bool HasMatrixPattern { get; set; }

    /// <summary>
    /// Gets or sets whether the method contains broadcast patterns.
    /// </summary>
    public bool HasBroadcastPattern { get; set; }

    /// <summary>
    /// Gets the estimated speedup factor from vectorization.
    /// </summary>
    public double EstimatedSpeedup
    {
        get
        {
            if (!IsVectorizable)
            {
                return 1.0;
            }

            // Simple heuristic based on pattern recognition

            var speedup = 1.0;


            if (VectorizableLoops > 0)
            {
                speedup *= 2.0 * VectorizableLoops; // Base speedup for vectorizable loops
            }


            if (HasDotProductPattern)
            {
                speedup *= 1.5; // Additional speedup for dot product
            }


            if (HasReductionPattern)
            {
                speedup *= 1.3; // Reduction operations benefit from SIMD
            }


            if (HasDataDependency)
            {
                speedup *= 0.5; // Data dependencies reduce effectiveness
            }


            return Math.Min(speedup, 8.0); // Cap at 8x theoretical maximum
        }
    }

    /// <summary>
    /// Gets a recommendation for vectorization based on the analysis.
    /// </summary>
    public VectorizationRecommendation GetRecommendation()
    {
        if (!IsVectorizable)
        {
            return new VectorizationRecommendation
            {
                ShouldVectorize = false,
                Reason = "No vectorizable patterns detected",
                EstimatedBenefit = VectorizationBenefit.None
            };
        }

        if (HasDataDependency)
        {
            return new VectorizationRecommendation
            {
                ShouldVectorize = false,
                Reason = "Data dependencies prevent efficient vectorization",
                EstimatedBenefit = VectorizationBenefit.Low
            };
        }

        if (EstimatedSpeedup > 4.0)
        {
            return new VectorizationRecommendation
            {
                ShouldVectorize = true,
                Reason = "High potential for performance improvement",
                EstimatedBenefit = VectorizationBenefit.High,
                EstimatedSpeedup = EstimatedSpeedup
            };
        }

        if (EstimatedSpeedup > 2.0)
        {
            return new VectorizationRecommendation
            {
                ShouldVectorize = true,
                Reason = "Moderate performance improvement expected",
                EstimatedBenefit = VectorizationBenefit.Medium,
                EstimatedSpeedup = EstimatedSpeedup
            };
        }

        return new VectorizationRecommendation
        {
            ShouldVectorize = ArithmeticOperationCount > 10,
            Reason = "Limited but worthwhile performance improvement",
            EstimatedBenefit = VectorizationBenefit.Low,
            EstimatedSpeedup = EstimatedSpeedup
        };
    }
}

/// <summary>
/// Recommendation for vectorization based on analysis.
/// </summary>
public class VectorizationRecommendation
{
    /// <summary>
    /// Gets or sets whether vectorization is recommended.
    /// </summary>
    public bool ShouldVectorize { get; set; }

    /// <summary>
    /// Gets or sets the reason for the recommendation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated benefit level.
    /// </summary>
    public VectorizationBenefit EstimatedBenefit { get; set; }

    /// <summary>
    /// Gets or sets the estimated speedup factor.
    /// </summary>
    public double EstimatedSpeedup { get; set; } = 1.0;
}

/// <summary>
/// Levels of benefit from vectorization.
/// </summary>
public enum VectorizationBenefit
{
    /// <summary>
    /// No benefit from vectorization.
    /// </summary>
    None,

    /// <summary>
    /// Low benefit (1.5x-2x speedup).
    /// </summary>
    Low,

    /// <summary>
    /// Medium benefit (2x-4x speedup).
    /// </summary>
    Medium,

    /// <summary>
    /// High benefit (4x+ speedup).
    /// </summary>
    High
}