// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models.Vectorization.Enums;

namespace DotCompute.Generators.Models.Vectorization;

/// <summary>
/// Provides comprehensive information about vectorization opportunities in analyzed code.
/// This class serves as the primary data structure for storing and analyzing the vectorization
/// potential of methods, loops, and computational patterns during code generation.
/// </summary>
/// <remarks>
/// The vectorization analysis examines various aspects of code including loop structures,
/// arithmetic operations, memory access patterns, and data dependencies to determine
/// the feasibility and potential benefits of SIMD optimization.
/// </remarks>
public class VectorizationInfo
{
    /// <summary>
    /// Gets or sets the total number of loops detected in the analyzed method.
    /// </summary>
    /// <value>
    /// A non-negative integer representing the count of all loop constructs (for, while, foreach) in the method.
    /// </value>
    /// <remarks>
    /// This metric provides a baseline for understanding the computational structure
    /// and helps estimate the scope for vectorization opportunities.
    /// </remarks>
    public int LoopCount { get; set; }

    /// <summary>
    /// Gets or sets the number of loops that are suitable for vectorization.
    /// </summary>
    /// <value>
    /// A non-negative integer representing the count of loops that meet vectorization criteria.
    /// This value should not exceed <see cref="LoopCount"/>.
    /// </value>
    /// <remarks>
    /// Vectorizable loops are those without complex data dependencies, with regular access patterns,
    /// and containing operations that can benefit from SIMD execution.
    /// </remarks>
    public int VectorizableLoops { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains array or memory access operations.
    /// </summary>
    /// <value>
    /// <c>true</c> if array indexing, pointer arithmetic, or other memory operations are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Array access patterns are essential for vectorization as SIMD operations typically
    /// work on contiguous memory regions. This property helps identify memory-bound operations.
    /// </remarks>
    public bool HasArrayAccess { get; set; }

    /// <summary>
    /// Gets or sets the count of arithmetic operations in the method.
    /// </summary>
    /// <value>
    /// A non-negative integer representing the number of arithmetic operations (add, subtract, multiply, divide).
    /// </value>
    /// <remarks>
    /// Arithmetic-intensive code typically benefits most from vectorization.
    /// This count helps assess the computational density and potential for SIMD optimization.
    /// </remarks>
    public int ArithmeticOperationCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the method is suitable for vectorization.
    /// </summary>
    /// <value>
    /// <c>true</c> if the method contains vectorizable loops and array access patterns; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This computed property combines multiple factors to provide a quick assessment
    /// of vectorization feasibility. It serves as a primary filter for optimization decisions.
    /// </remarks>
    public bool IsVectorizable => VectorizableLoops > 0 && HasArrayAccess;

    /// <summary>
    /// Gets a value indicating whether the method performs arithmetic operations.
    /// </summary>
    /// <value>
    /// <c>true</c> if arithmetic operations are present; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Arithmetic operations are prime candidates for SIMD optimization.
    /// This property helps classify the computational nature of the analyzed code.
    /// </remarks>
    public bool IsArithmetic => ArithmeticOperationCount > 0;

    /// <summary>
    /// Gets a value indicating whether the method performs memory operations.
    /// </summary>
    /// <value>
    /// <c>true</c> if memory operations are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Memory operations, particularly array access, are fundamental to vectorization.
    /// This property is currently an alias for <see cref="HasArrayAccess"/> but may
    /// be extended to include other memory operation types in the future.
    /// </remarks>
    public bool IsMemoryOperation => HasArrayAccess;

    /// <summary>
    /// Gets or sets a value indicating whether the method has data dependencies that prevent vectorization.
    /// </summary>
    /// <value>
    /// <c>true</c> if data dependencies that conflict with vectorization are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Data dependencies, such as loop-carried dependencies or complex control flow,
    /// can significantly reduce or eliminate the benefits of vectorization.
    /// This analysis is crucial for accurate performance predictions.
    /// </remarks>
    public bool HasDataDependency { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains reduction patterns (sum, min, max, etc.).
    /// </summary>
    /// <value>
    /// <c>true</c> if reduction operations are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Reduction patterns are well-suited for SIMD optimization using specialized reduction instructions.
    /// Common patterns include summing arrays, finding minimum/maximum values, and computing averages.
    /// </remarks>
    public bool HasReductionPattern { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains dot product patterns.
    /// </summary>
    /// <value>
    /// <c>true</c> if dot product computations are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Dot product operations (element-wise multiplication followed by sum reduction)
    /// are highly optimizable with SIMD instructions and can achieve significant speedups.
    /// </remarks>
    public bool HasDotProductPattern { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains matrix multiplication patterns.
    /// </summary>
    /// <value>
    /// <c>true</c> if matrix operations are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Matrix operations, particularly dense matrix multiplication, are among the most
    /// vectorizable patterns and can benefit significantly from SIMD optimization.
    /// </remarks>
    public bool HasMatrixPattern { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains broadcast patterns.
    /// </summary>
    /// <value>
    /// <c>true</c> if broadcast operations are detected; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Broadcast patterns involve applying a single value to multiple array elements simultaneously.
    /// These patterns are naturally suited for SIMD operations and can achieve excellent performance.
    /// </remarks>
    public bool HasBroadcastPattern { get; set; }

    /// <summary>
    /// Gets the estimated speedup factor that could be achieved through vectorization.
    /// </summary>
    /// <value>
    /// A positive double value representing the estimated performance multiplier.
    /// Values greater than 1.0 indicate potential speedup, with a maximum theoretical limit of 8.0.
    /// </value>
    /// <remarks>
    /// This calculation uses heuristics based on the detected patterns, loop characteristics,
    /// and data dependency analysis. The estimate provides guidance for optimization decisions
    /// but actual results may vary based on hardware capabilities and runtime conditions.
    /// </remarks>
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
    /// Generates a comprehensive vectorization recommendation based on the analyzed code characteristics.
    /// </summary>
    /// <returns>
    /// A <see cref="VectorizationRecommendation"/> object containing the recommendation decision,
    /// reasoning, estimated benefits, and expected performance improvements.
    /// </returns>
    /// <remarks>
    /// This method implements the decision logic for vectorization recommendations by evaluating
    /// multiple factors including vectorization feasibility, data dependencies, and estimated performance benefits.
    /// The recommendation serves as input for code generation optimization decisions.
    /// </remarks>
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