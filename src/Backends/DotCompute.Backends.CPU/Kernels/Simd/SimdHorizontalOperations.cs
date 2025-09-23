// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// Horizontal operations for SIMD vectors - operations that combine elements within a single vector.
/// Provides optimized horizontal sum, min, max, and product operations.
/// </summary>
public static class SimdHorizontalOperations
{
    #region Horizontal Sum Operations

    /// <summary>
    /// Performs horizontal sum of a 512-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalSum(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var sum256 = Avx.Add(lower, upper);
        return HorizontalSum(sum256);
    }

    /// <summary>
    /// Performs horizontal sum of a 256-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalSum(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var sum128 = Sse.Add(lower, upper);

        if (Sse3.IsSupported)
        {
            var hadd1 = Sse3.HorizontalAdd(sum128, sum128);
            var hadd2 = Sse3.HorizontalAdd(hadd1, hadd1);
            return hadd2.ToScalar();
        }

        var temp = Sse.Shuffle(sum128, sum128, 0x4E);
        sum128 = Sse.Add(sum128, temp);
        temp = Sse.Shuffle(sum128, sum128, 0xB1);
        sum128 = Sse.Add(sum128, temp);
        return sum128.ToScalar();
    }

    /// <summary>
    /// Performs horizontal sum of a 128-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalSum(Vector128<float> vector)
    {
        if (Sse3.IsSupported)
        {
            var hadd1 = Sse3.HorizontalAdd(vector, vector);
            var hadd2 = Sse3.HorizontalAdd(hadd1, hadd1);
            return hadd2.ToScalar();
        }

        var temp = Sse.Shuffle(vector, vector, 0x4E);
        vector = Sse.Add(vector, temp);
        temp = Sse.Shuffle(vector, vector, 0xB1);
        vector = Sse.Add(vector, temp);
        return vector.ToScalar();
    }

    #endregion

    #region Horizontal Min Operations

    /// <summary>
    /// Performs horizontal min of a 512-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMin(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var min256 = Avx.Min(lower, upper);
        return HorizontalMin(min256);
    }

    /// <summary>
    /// Performs horizontal min of a 256-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMin(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var min128 = Sse.Min(lower, upper);

        var temp = Sse.Shuffle(min128, min128, 0x4E);
        min128 = Sse.Min(min128, temp);
        temp = Sse.Shuffle(min128, min128, 0xB1);
        min128 = Sse.Min(min128, temp);
        return min128.ToScalar();
    }

    /// <summary>
    /// Performs horizontal min of a 128-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMin(Vector128<float> vector)
    {
        var temp = Sse.Shuffle(vector, vector, 0x4E);
        vector = Sse.Min(vector, temp);
        temp = Sse.Shuffle(vector, vector, 0xB1);
        vector = Sse.Min(vector, temp);
        return vector.ToScalar();
    }

    #endregion

    #region Horizontal Max Operations

    /// <summary>
    /// Performs horizontal max of a 512-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMax(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var max256 = Avx.Max(lower, upper);
        return HorizontalMax(max256);
    }

    /// <summary>
    /// Performs horizontal max of a 256-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMax(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var max128 = Sse.Max(lower, upper);

        var temp = Sse.Shuffle(max128, max128, 0x4E);
        max128 = Sse.Max(max128, temp);
        temp = Sse.Shuffle(max128, max128, 0xB1);
        max128 = Sse.Max(max128, temp);
        return max128.ToScalar();
    }

    /// <summary>
    /// Performs horizontal max of a 128-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMax(Vector128<float> vector)
    {
        var temp = Sse.Shuffle(vector, vector, 0x4E);
        vector = Sse.Max(vector, temp);
        temp = Sse.Shuffle(vector, vector, 0xB1);
        vector = Sse.Max(vector, temp);
        return vector.ToScalar();
    }

    #endregion

    #region Horizontal Product Operations

    /// <summary>
    /// Performs horizontal product of a 512-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalProduct(Vector512<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var product256 = Avx.Multiply(lower, upper);
        return HorizontalProduct(product256);
    }

    /// <summary>
    /// Performs horizontal product of a 256-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalProduct(Vector256<float> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var product128 = Sse.Multiply(lower, upper);

        var temp = Sse.Shuffle(product128, product128, 0x4E);
        product128 = Sse.Multiply(product128, temp);
        temp = Sse.Shuffle(product128, product128, 0xB1);
        product128 = Sse.Multiply(product128, temp);
        return product128.ToScalar();
    }

    /// <summary>
    /// Performs horizontal product of a 128-bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalProduct(Vector128<float> vector)
    {
        var temp = Sse.Shuffle(vector, vector, 0x4E);
        vector = Sse.Multiply(vector, temp);
        temp = Sse.Shuffle(vector, vector, 0xB1);
        vector = Sse.Multiply(vector, temp);
        return vector.ToScalar();
    }

    #endregion

    #region Double Precision Operations

    /// <summary>
    /// Performs horizontal sum of a 256-bit double vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double HorizontalSum(Vector256<double> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var sum128 = Sse2.Add(lower, upper);

        if (Sse3.IsSupported)
        {
            var hadd = Sse3.HorizontalAdd(sum128, sum128);
            return hadd.ToScalar();
        }

        var temp = Sse2.Shuffle(sum128, sum128, 0x1);
        sum128 = Sse2.Add(sum128, temp);
        return sum128.ToScalar();
    }

    /// <summary>
    /// Performs horizontal sum of a 128-bit double vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double HorizontalSum(Vector128<double> vector)
    {
        if (Sse3.IsSupported)
        {
            var hadd = Sse3.HorizontalAdd(vector, vector);
            return hadd.ToScalar();
        }

        var temp = Sse2.Shuffle(vector, vector, 0x1);
        vector = Sse2.Add(vector, temp);
        return vector.ToScalar();
    }

    #endregion

    #region Integer Operations

    /// <summary>
    /// Performs horizontal sum of a 256-bit integer vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HorizontalSum(Vector256<int> vector)
    {
        var lower = vector.GetLower();
        var upper = vector.GetUpper();
        var sum128 = Sse2.Add(lower, upper);

        if (Ssse3.IsSupported)
        {
            var hadd1 = Ssse3.HorizontalAdd(sum128, sum128);
            var hadd2 = Ssse3.HorizontalAdd(hadd1, hadd1);
            return hadd2.ToScalar();
        }

        var temp = Sse2.Shuffle(sum128, 0x4E);
        sum128 = Sse2.Add(sum128, temp);
        temp = Sse2.Shuffle(sum128, 0xB1);
        sum128 = Sse2.Add(sum128, temp);
        return sum128.ToScalar();
    }

    /// <summary>
    /// Performs horizontal sum of a 128-bit integer vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HorizontalSum(Vector128<int> vector)
    {
        if (Ssse3.IsSupported)
        {
            var hadd1 = Ssse3.HorizontalAdd(vector, vector);
            var hadd2 = Ssse3.HorizontalAdd(hadd1, hadd1);
            return hadd2.ToScalar();
        }

        var temp = Sse2.Shuffle(vector, 0x4E);
        vector = Sse2.Add(vector, temp);
        temp = Sse2.Shuffle(vector, 0xB1);
        vector = Sse2.Add(vector, temp);
        return vector.ToScalar();
    }

    #endregion
}