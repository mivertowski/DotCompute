// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

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

    #region ARM NEON Operations

    /// <summary>
    /// Performs horizontal sum of a 128-bit float vector using ARM NEON.
    /// Uses Vector64 pairwise operations for maximum compatibility.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalSumNeon(Vector128<float> vector)
    {
        if (AdvSimd.IsSupported)
        {
            // Split 128-bit into two 64-bit vectors and use pairwise operations
            var lower = vector.GetLower();  // [a0, a1]
            var upper = vector.GetUpper();  // [a2, a3]
            var sum64 = AdvSimd.AddPairwise(lower, upper);  // [a0+a1, a2+a3]
            // Add the two remaining elements
            return sum64.GetElement(0) + sum64.GetElement(1);
        }

        // Scalar fallback
        return vector.GetElement(0) + vector.GetElement(1) +
               vector.GetElement(2) + vector.GetElement(3);
    }

    /// <summary>
    /// Performs horizontal sum of a 128-bit double vector using ARM NEON.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double HorizontalSumNeon(Vector128<double> vector)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            // ARM64 has AddPairwiseScalar for doubles
            return AdvSimd.Arm64.AddPairwiseScalar(vector).ToScalar();
        }

        // Scalar fallback
        return vector.GetElement(0) + vector.GetElement(1);
    }

    /// <summary>
    /// Performs horizontal sum of a 128-bit integer vector using ARM NEON.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HorizontalSumNeon(Vector128<int> vector)
    {
        if (AdvSimd.IsSupported)
        {
            // Split 128-bit into two 64-bit vectors
            var lower = vector.GetLower();  // [a0, a1]
            var upper = vector.GetUpper();  // [a2, a3]
            var sum64 = AdvSimd.AddPairwise(lower, upper);  // [a0+a2, a1+a3]
            // Add the two remaining elements
            return sum64.GetElement(0) + sum64.GetElement(1);
        }

        // Scalar fallback
        return vector.GetElement(0) + vector.GetElement(1) +
               vector.GetElement(2) + vector.GetElement(3);
    }

    /// <summary>
    /// Performs horizontal min of a 128-bit float vector using ARM NEON.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMinNeon(Vector128<float> vector)
    {
        if (AdvSimd.IsSupported)
        {
            var lower = vector.GetLower();
            var upper = vector.GetUpper();
            var min64 = AdvSimd.MinPairwise(lower, upper);
            return Math.Min(min64.GetElement(0), min64.GetElement(1));
        }

        // Scalar fallback
        return Math.Min(Math.Min(vector.GetElement(0), vector.GetElement(1)),
                       Math.Min(vector.GetElement(2), vector.GetElement(3)));
    }

    /// <summary>
    /// Performs horizontal max of a 128-bit float vector using ARM NEON.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalMaxNeon(Vector128<float> vector)
    {
        if (AdvSimd.IsSupported)
        {
            var lower = vector.GetLower();
            var upper = vector.GetUpper();
            var max64 = AdvSimd.MaxPairwise(lower, upper);
            return Math.Max(max64.GetElement(0), max64.GetElement(1));
        }

        // Scalar fallback
        return Math.Max(Math.Max(vector.GetElement(0), vector.GetElement(1)),
                       Math.Max(vector.GetElement(2), vector.GetElement(3)));
    }

    #endregion

    #region Cross-Platform Vectorized Operations

    /// <summary>
    /// Performs horizontal sum using the best available SIMD implementation for the current platform.
    /// Automatically selects AVX/SSE on x86 or NEON on ARM.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalSumCrossPlatform(Vector128<float> vector)
    {
        if (Sse3.IsSupported)
        {
            return HorizontalSum(vector);
        }

        if (AdvSimd.IsSupported)
        {
            return HorizontalSumNeon(vector);
        }

        // Scalar fallback
        return vector.GetElement(0) + vector.GetElement(1) +
               vector.GetElement(2) + vector.GetElement(3);
    }

    /// <summary>
    /// Performs horizontal sum using Vector&lt;T&gt; which auto-selects SIMD width.
    /// Portable across all platforms with automatic SIMD selection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T HorizontalSumPortable<T>(Vector<T> vector) where T : struct, INumber<T>
    {
        var sum = T.Zero;
        for (var i = 0; i < Vector<T>.Count; i++)
        {
            sum += vector[i];
        }
        return sum;
    }

    #endregion

    #region Parallel Reduction Tree

    /// <summary>
    /// Performs a parallel tree reduction on an array using SIMD operations.
    /// This is the most efficient way to reduce large arrays.
    /// </summary>
    /// <param name="data">The input array to reduce.</param>
    /// <returns>The sum of all elements.</returns>
    /// <remarks>
    /// Uses a three-phase approach for optimal performance:
    /// 1. SIMD reduction phase: processes Vector256 chunks in parallel
    /// 2. Horizontal reduction: combines SIMD results per thread
    /// 3. Final reduction: combines thread results
    /// </remarks>
    public static float ParallelTreeReduction(float[] data)
    {
        if (data.Length == 0)
        {
            return 0f;
        }

        if (data.Length < 64)
        {
            // Small arrays: scalar reduction
            var scalarSum = 0f;
            foreach (var value in data)
            {
                scalarSum += value;
            }
            return scalarSum;
        }

        // Determine SIMD width and parallel strategy
        var vectorSize = Avx.IsSupported ? 8 : (Sse.IsSupported || AdvSimd.IsSupported ? 4 : 1);
        var numThreads = Math.Min(Environment.ProcessorCount, data.Length / (vectorSize * 4));
        numThreads = Math.Max(1, numThreads);

        var partialSums = new double[numThreads];
        var elementsPerThread = data.Length / numThreads;
        var dataLength = data.Length;

        Parallel.For(0, numThreads, threadIdx =>
        {
            var start = threadIdx * elementsPerThread;
            var end = threadIdx == numThreads - 1 ? dataLength : start + elementsPerThread;
            var threadSum = 0.0;

            if (Avx.IsSupported)
            {
                // AVX path: process 8 floats at a time
                var vectorSum = Vector256<float>.Zero;
                var i = start;

                for (; i <= end - 8; i += 8)
                {
                    var vec = Vector256.Create(
                        data[i], data[i + 1], data[i + 2], data[i + 3],
                        data[i + 4], data[i + 5], data[i + 6], data[i + 7]);
                    vectorSum = Avx.Add(vectorSum, vec);
                }

                // Horizontal sum of vector
                threadSum = HorizontalSum(vectorSum);

                // Process remaining elements
                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else if (Sse.IsSupported)
            {
                // SSE path: process 4 floats at a time
                var vectorSum = Vector128<float>.Zero;
                var i = start;

                for (; i <= end - 4; i += 4)
                {
                    var vec = Vector128.Create(data[i], data[i + 1], data[i + 2], data[i + 3]);
                    vectorSum = Sse.Add(vectorSum, vec);
                }

                threadSum = HorizontalSum(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else if (AdvSimd.IsSupported)
            {
                // NEON path: process 4 floats at a time
                var vectorSum = Vector128<float>.Zero;
                var i = start;

                for (; i <= end - 4; i += 4)
                {
                    var vec = Vector128.Create(data[i], data[i + 1], data[i + 2], data[i + 3]);
                    vectorSum = AdvSimd.Add(vectorSum, vec);
                }

                threadSum = HorizontalSumNeon(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else
            {
                // Scalar fallback
                for (var i = start; i < end; i++)
                {
                    threadSum += data[i];
                }
            }

            partialSums[threadIdx] = threadSum;
        });

        // Final reduction of partial sums
        var totalSum = 0.0;
        foreach (var partialSum in partialSums)
        {
            totalSum += partialSum;
        }

        return (float)totalSum;
    }

    /// <summary>
    /// Performs a parallel tree reduction for double precision.
    /// </summary>
    public static double ParallelTreeReductionDouble(double[] data)
    {
        if (data.Length == 0)
        {
            return 0.0;
        }

        if (data.Length < 32)
        {
            var scalarSum = 0.0;
            foreach (var value in data)
            {
                scalarSum += value;
            }
            return scalarSum;
        }

        var vectorSize = Avx.IsSupported ? 4 : (Sse2.IsSupported || AdvSimd.Arm64.IsSupported ? 2 : 1);
        var numThreads = Math.Min(Environment.ProcessorCount, data.Length / (vectorSize * 4));
        numThreads = Math.Max(1, numThreads);

        var partialSums = new double[numThreads];
        var elementsPerThread = data.Length / numThreads;
        var dataLength = data.Length;

        Parallel.For(0, numThreads, threadIdx =>
        {
            var start = threadIdx * elementsPerThread;
            var end = threadIdx == numThreads - 1 ? dataLength : start + elementsPerThread;
            var threadSum = 0.0;

            if (Avx.IsSupported)
            {
                var vectorSum = Vector256<double>.Zero;
                var i = start;

                for (; i <= end - 4; i += 4)
                {
                    var vec = Vector256.Create(data[i], data[i + 1], data[i + 2], data[i + 3]);
                    vectorSum = Avx.Add(vectorSum, vec);
                }

                threadSum = HorizontalSum(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else if (Sse2.IsSupported)
            {
                var vectorSum = Vector128<double>.Zero;
                var i = start;

                for (; i <= end - 2; i += 2)
                {
                    var vec = Vector128.Create(data[i], data[i + 1]);
                    vectorSum = Sse2.Add(vectorSum, vec);
                }

                threadSum = HorizontalSum(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                var vectorSum = Vector128<double>.Zero;
                var i = start;

                for (; i <= end - 2; i += 2)
                {
                    var vec = Vector128.Create(data[i], data[i + 1]);
                    vectorSum = AdvSimd.Arm64.Add(vectorSum, vec);
                }

                threadSum = HorizontalSumNeon(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else
            {
                for (var i = start; i < end; i++)
                {
                    threadSum += data[i];
                }
            }

            partialSums[threadIdx] = threadSum;
        });

        var totalSum = 0.0;
        foreach (var partialSum in partialSums)
        {
            totalSum += partialSum;
        }

        return totalSum;
    }

    /// <summary>
    /// Performs a parallel tree reduction for integers.
    /// </summary>
    public static long ParallelTreeReductionInt(int[] data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        if (data.Length < 64)
        {
            var scalarSum = 0L;
            foreach (var value in data)
            {
                scalarSum += value;
            }
            return scalarSum;
        }

        var vectorSize = Avx2.IsSupported ? 8 : (Sse2.IsSupported || AdvSimd.IsSupported ? 4 : 1);
        var numThreads = Math.Min(Environment.ProcessorCount, data.Length / (vectorSize * 4));
        numThreads = Math.Max(1, numThreads);

        var partialSums = new long[numThreads];
        var elementsPerThread = data.Length / numThreads;
        var dataLength = data.Length;

        Parallel.For(0, numThreads, threadIdx =>
        {
            var start = threadIdx * elementsPerThread;
            var end = threadIdx == numThreads - 1 ? dataLength : start + elementsPerThread;
            var threadSum = 0L;

            if (Avx2.IsSupported)
            {
                var vectorSum = Vector256<int>.Zero;
                var i = start;

                for (; i <= end - 8; i += 8)
                {
                    var vec = Vector256.Create(
                        data[i], data[i + 1], data[i + 2], data[i + 3],
                        data[i + 4], data[i + 5], data[i + 6], data[i + 7]);
                    vectorSum = Avx2.Add(vectorSum, vec);
                }

                threadSum = HorizontalSum(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else if (Sse2.IsSupported)
            {
                var vectorSum = Vector128<int>.Zero;
                var i = start;

                for (; i <= end - 4; i += 4)
                {
                    var vec = Vector128.Create(data[i], data[i + 1], data[i + 2], data[i + 3]);
                    vectorSum = Sse2.Add(vectorSum, vec);
                }

                threadSum = HorizontalSum(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else if (AdvSimd.IsSupported)
            {
                var vectorSum = Vector128<int>.Zero;
                var i = start;

                for (; i <= end - 4; i += 4)
                {
                    var vec = Vector128.Create(data[i], data[i + 1], data[i + 2], data[i + 3]);
                    vectorSum = AdvSimd.Add(vectorSum, vec);
                }

                threadSum = HorizontalSumNeon(vectorSum);

                for (; i < end; i++)
                {
                    threadSum += data[i];
                }
            }
            else
            {
                for (var i = start; i < end; i++)
                {
                    threadSum += data[i];
                }
            }

            partialSums[threadIdx] = threadSum;
        });

        var totalSum = 0L;
        foreach (var partialSum in partialSums)
        {
            totalSum += partialSum;
        }

        return totalSum;
    }

    #endregion
}
