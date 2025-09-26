// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Optimized.Simd;

namespace DotCompute.Algorithms.Optimized;

/// <summary>
/// Cross-platform SIMD intrinsics facade providing unified access to all vector operations.
/// Delegates to specialized operation classes while providing a single entry point.
/// Abstracts away hardware differences and operation categories.
/// </summary>
public static class SimdIntrinsics
{
    #region Hardware Capability Detection

    /// <summary>Gets whether AVX-512 instructions are supported.</summary>
    public static bool HasAvx512 => SimdCapabilities.HasAvx512;

    /// <summary>Gets whether AVX2 instructions are supported.</summary>
    public static bool HasAvx2 => SimdCapabilities.HasAvx2;

    /// <summary>Gets whether FMA (Fused Multiply-Add) instructions are supported.</summary>
    public static bool HasFma => SimdCapabilities.HasFma;

    /// <summary>Gets whether SSE 4.2 instructions are supported.</summary>
    public static bool HasSse42 => SimdCapabilities.HasSse42;

    /// <summary>Gets whether ARM NEON instructions are supported.</summary>
    public static bool HasNeon => SimdCapabilities.HasNeon;

    /// <summary>Gets the size of 512-bit vectors in bytes.</summary>
    public static int Vector512Size => SimdCapabilities.Vector512Size;

    /// <summary>Gets the size of 256-bit vectors in bytes.</summary>
    public static int Vector256Size => SimdCapabilities.Vector256Size;

    /// <summary>Gets the size of 128-bit vectors in bytes.</summary>
    public static int Vector128Size => SimdCapabilities.Vector128Size;

    /// <summary>Gets the generic vector size in bytes.</summary>
    public static int VectorSize => SimdCapabilities.VectorSize;

    /// <summary>Gets the optimal vector size for the current architecture in bytes.</summary>
    public static int OptimalVectorSize => SimdCapabilities.OptimalVectorSize;

    #endregion

    #region Math Operations

    /// <summary>
    /// Cross-platform SIMD vector addition with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void Add(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdMathOperations.Add(left, right, result);

    /// <summary>
    /// Cross-platform SIMD vector subtraction with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void Subtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdMathOperations.Subtract(left, right, result);

    /// <summary>
    /// Cross-platform SIMD vector multiplication with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void Multiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdMathOperations.Multiply(left, right, result);

    /// <summary>
    /// Cross-platform SIMD vector division with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void Divide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdMathOperations.Divide(left, right, result);

    /// <summary>
    /// Cross-platform SIMD fused multiply-add (FMA) operation: result = (a * b) + c
    /// </summary>
    /// <param name="a">First multiplicand</param>
    /// <param name="b">Second multiplicand</param>
    /// <param name="c">Addend</param>
    /// <param name="result">Result span</param>
    public static void FusedMultiplyAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
        => SimdMathOperations.FusedMultiplyAdd(a, b, c, result);

    #endregion

    #region Reduction Operations

    /// <summary>
    /// Cross-platform SIMD dot product with horizontal reduction.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <returns>Dot product result</returns>
    public static float DotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
        => SimdReductionOperations.DotProduct(left, right);

    /// <summary>
    /// Cross-platform SIMD horizontal sum with optimal reduction.
    /// </summary>
    /// <param name="values">Values to sum</param>
    /// <returns>Sum of all values</returns>
    public static float HorizontalSum(ReadOnlySpan<float> values)
        => SimdReductionOperations.HorizontalSum(values);

    /// <summary>
    /// Cross-platform SIMD minimum value with optimal reduction.
    /// </summary>
    /// <param name="values">Values to find minimum of</param>
    /// <returns>Minimum value</returns>
    public static float Min(ReadOnlySpan<float> values)
        => SimdReductionOperations.Min(values);

    /// <summary>
    /// Cross-platform SIMD maximum value with optimal reduction.
    /// </summary>
    /// <param name="values">Values to find maximum of</param>
    /// <returns>Maximum value</returns>
    public static float Max(ReadOnlySpan<float> values)
        => SimdReductionOperations.Max(values);

    #endregion

    #region Utility Operations

    /// <summary>
    /// Cross-platform SIMD matrix transpose with cache-friendly blocking.
    /// </summary>
    /// <param name="source">Source matrix data (row-major)</param>
    /// <param name="dest">Destination matrix data (column-major)</param>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    public static void Transpose(ReadOnlySpan<float> source, Span<float> dest, int rows, int cols)
        => SimdUtilityOperations.Transpose(source, dest, rows, cols);

    #endregion

    #region Comparison Operations

    /// <summary>
    /// Vectorized element-wise equality comparison.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span (1.0f for equal, 0.0f for not equal)</param>
    public static void Equal(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdComparisonOperations.Equal(left, right, result);

    /// <summary>
    /// Vectorized element-wise greater than comparison.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span (1.0f for greater, 0.0f otherwise)</param>
    public static void GreaterThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdComparisonOperations.GreaterThan(left, right, result);

    /// <summary>
    /// Vectorized element-wise less than comparison.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span (1.0f for less, 0.0f otherwise)</param>
    public static void LessThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
        => SimdComparisonOperations.LessThan(left, right, result);

    #endregion

    #region Bitwise Operations

    /// <summary>
    /// Vectorized bitwise AND operation.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void And(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> result)
        => SimdBitwiseOperations.And(left, right, result);

    /// <summary>
    /// Vectorized bitwise OR operation.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void Or(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> result)
        => SimdBitwiseOperations.Or(left, right, result);

    /// <summary>
    /// Vectorized bitwise XOR operation.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    public static void Xor(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> result)
        => SimdBitwiseOperations.Xor(left, right, result);

    /// <summary>
    /// Vectorized bitwise NOT operation.
    /// </summary>
    /// <param name="input">Input span</param>
    /// <param name="result">Result span</param>
    public static void Not(ReadOnlySpan<int> input, Span<int> result)
        => SimdBitwiseOperations.Not(input, result);

    #endregion

    #region Type Conversion Operations

    /// <summary>
    /// Vectorized conversion from int32 to float32.
    /// </summary>
    /// <param name="input">Input int32 span</param>
    /// <param name="result">Result float32 span</param>
    public static void ConvertInt32ToFloat32(ReadOnlySpan<int> input, Span<float> result)
        => SimdConversionOperations.ConvertInt32ToFloat32(input, result);

    /// <summary>
    /// Vectorized conversion from float32 to int32 with truncation.
    /// </summary>
    /// <param name="input">Input float32 span</param>
    /// <param name="result">Result int32 span</param>
    public static void ConvertFloat32ToInt32(ReadOnlySpan<float> input, Span<int> result)
        => SimdConversionOperations.ConvertFloat32ToInt32(input, result);

    /// <summary>
    /// Vectorized conversion from double to float32 with precision loss.
    /// </summary>
    /// <param name="input">Input double span</param>
    /// <param name="result">Result float32 span</param>
    public static void ConvertDoubleToFloat32(ReadOnlySpan<double> input, Span<float> result)
        => SimdConversionOperations.ConvertDoubleToFloat32(input, result);

    /// <summary>
    /// Vectorized conversion from float32 to double with precision extension.
    /// </summary>
    /// <param name="input">Input float32 span</param>
    /// <param name="result">Result double span</param>
    public static void ConvertFloat32ToDouble(ReadOnlySpan<float> input, Span<double> result)
        => SimdConversionOperations.ConvertFloat32ToDouble(input, result);

    #endregion
}