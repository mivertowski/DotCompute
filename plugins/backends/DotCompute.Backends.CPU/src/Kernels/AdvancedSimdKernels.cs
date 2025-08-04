// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Advanced SIMD kernel implementations with complete FMA, integer SIMD, 
/// enhanced ARM NEON, and modern vectorization techniques.
/// </summary>
public static class AdvancedSimdKernels
{
    #region FMA (Fused Multiply-Add) Operations - CRITICAL IMPLEMENTATION

    /// <summary>
    /// Vectorized FMA operation: result = a * b + c using hardware FMA instructions.
    /// Essential for scientific computing with optimal precision and performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorFmaFloat32(
        float* a, float* b, float* c, float* result, long elementCount)
    {
        long i = 0;

        // AVX-512 FMA path (16 floats per operation)
        if (Avx512F.IsSupported && Fma.IsSupported)
        {
            const int VectorSize = 16;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx512F.LoadVector512(a + offset);
                var vb = Avx512F.LoadVector512(b + offset);
                var vc = Avx512F.LoadVector512(c + offset);

                // True hardware FMA: a * b + c in single instruction
                var vr = Avx512F.FusedMultiplyAdd(va, vb, vc);
                Avx512F.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // AVX2/FMA path (8 floats per operation)
        else if (Avx2.IsSupported && Fma.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx.LoadVector256(a + offset);
                var vb = Avx.LoadVector256(b + offset);
                var vc = Avx.LoadVector256(c + offset);

                // Hardware FMA with AVX2
                var vr = Fma.MultiplyAdd(va, vb, vc);
                Avx.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // ARM NEON FMA path (4 floats per operation)
        else if (AdvSimd.IsSupported)
        {
            const int VectorSize = 4;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = AdvSimd.LoadVector128(a + offset);
                var vb = AdvSimd.LoadVector128(b + offset);
                var vc = AdvSimd.LoadVector128(c + offset);

                // ARM NEON FMA
                var vr = AdvSimd.FusedMultiplyAdd(vc, va, vb); // Note: ARM FMA is addend + multiplicand * multiplier
                AdvSimd.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder with software FMA
        for (; i < elementCount; i++)
        {
            result[i] = MathF.FusedMultiplyAdd(a[i], b[i], c[i]);
        }
    }

    /// <summary>
    /// Double precision FMA operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorFmaFloat64(
        double* a, double* b, double* c, double* result, long elementCount)
    {
        long i = 0;

        // AVX-512 FMA path (8 doubles per operation)
        if (Avx512F.IsSupported && Fma.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx512F.LoadVector512(a + offset);
                var vb = Avx512F.LoadVector512(b + offset);
                var vc = Avx512F.LoadVector512(c + offset);

                var vr = Avx512F.FusedMultiplyAdd(va, vb, vc);
                Avx512F.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // AVX2/FMA path (4 doubles per operation)
        else if (Avx2.IsSupported && Fma.IsSupported)
        {
            const int VectorSize = 4;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx.LoadVector256(a + offset);
                var vb = Avx.LoadVector256(b + offset);
                var vc = Avx.LoadVector256(c + offset);

                var vr = Fma.MultiplyAdd(va, vb, vc);
                Avx.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // ARM NEON FMA path (2 doubles per operation)
        else if (AdvSimd.Arm64.IsSupported)
        {
            const int VectorSize = 2;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = AdvSimd.LoadVector128(a + offset);
                var vb = AdvSimd.LoadVector128(b + offset);
                var vc = AdvSimd.LoadVector128(c + offset);

                var vr = AdvSimd.Arm64.FusedMultiplyAdd(vc, va, vb);
                AdvSimd.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder
        for (; i < elementCount; i++)
        {
            result[i] = Math.FusedMultiplyAdd(a[i], b[i], c[i]);
        }
    }

    #endregion

    #region Integer SIMD Operations - CRITICAL FOR BROADER USE CASES

    /// <summary>
    /// Vectorized 32-bit integer addition with full SIMD support.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorAddInt32(
        int* a, int* b, int* result, long elementCount)
    {
        long i = 0;

        // AVX-512 path (16 ints per operation)
        if (Avx512F.IsSupported)
        {
            const int VectorSize = 16;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx512F.LoadVector512(a + offset);
                var vb = Avx512F.LoadVector512(b + offset);
                var vr = Avx512F.Add(va, vb);
                Avx512F.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // AVX2 path (8 ints per operation)
        else if (Avx2.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx2.LoadVector256(a + offset);
                var vb = Avx2.LoadVector256(b + offset);
                var vr = Avx2.Add(va, vb);
                Avx2.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // ARM NEON path (4 ints per operation)
        else if (AdvSimd.IsSupported)
        {
            const int VectorSize = 4;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = AdvSimd.LoadVector128(a + offset);
                var vb = AdvSimd.LoadVector128(b + offset);
                var vr = AdvSimd.Add(va, vb);
                AdvSimd.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder
        for (; i < elementCount; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    /// <summary>
    /// Vectorized 64-bit integer multiplication.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorMultiplyInt64(
        long* a, long* b, long* result, long elementCount)
    {
        long i = 0;

        // AVX-512 path (8 longs per operation)
        if (Avx512DQ.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx512F.LoadVector512(a + offset);
                var vb = Avx512F.LoadVector512(b + offset);
                // AVX-512DQ has native 64-bit multiply
                var vr = Avx512DQ.MultiplyLow(va, vb);
                Avx512F.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // AVX2 path (4 longs per operation) - Decompose 64-bit multiply
        else if (Avx2.IsSupported)
        {
            const int VectorSize = 4;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;

                // Load 64-bit values
                var va = Avx2.LoadVector256(a + offset);
                var vb = Avx2.LoadVector256(b + offset);

                // Decompose 64-bit multiply into 32-bit operations
                // Split each 64-bit value into high and low 32-bit parts
                var aLo = Avx2.And(va, Vector256.Create(0xFFFFFFFFL));
                var aHi = Avx2.ShiftRightLogical(va, 32);
                var bLo = Avx2.And(vb, Vector256.Create(0xFFFFFFFFL));
                var bHi = Avx2.ShiftRightLogical(vb, 32);

                // Compute partial products
                var loLo = Avx2.Multiply(aLo.AsUInt32(), bLo.AsUInt32()); // Low 32 bits of each result
                var loHi = Avx2.Multiply(aLo.AsUInt32(), bHi.AsUInt32());
                var hiLo = Avx2.Multiply(aHi.AsUInt32(), bLo.AsUInt32());

                // Combine partial products
                // Result = loLo + ((loHi + hiLo) << 32)
                var middle = Avx2.Add(loHi, hiLo);
                var middleShifted = Avx2.ShiftLeftLogical(middle, 32);
                var vr = Avx2.Add(loLo.AsInt64(), middleShifted.AsInt64());

                Avx2.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // ARM NEON path (2 longs per operation)
        else if (AdvSimd.Arm64.IsSupported)
        {
            const int VectorSize = 2;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;

                // Load 64-bit values
                var va = AdvSimd.LoadVector128(a + offset);
                var vb = AdvSimd.LoadVector128(b + offset);

                // ARM64 NEON doesn't have direct 64-bit multiply either
                // Use scalar multiplication for each element
                var a0 = va.GetElement(0);
                var a1 = va.GetElement(1);
                var b0 = vb.GetElement(0);
                var b1 = vb.GetElement(1);

                var r0 = a0 * b0;
                var r1 = a1 * b1;

                var vr = Vector128.Create(r0, r1);
                AdvSimd.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder (and fallback for unsupported SIMD)
        for (; i < elementCount; i++)
        {
            result[i] = a[i] * b[i];
        }
    }

    /// <summary>
    /// Vectorized 16-bit integer operations (common in image processing).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorAddInt16(
        short* a, short* b, short* result, long elementCount)
    {
        long i = 0;

        // AVX-512 path (32 shorts per operation)
        if (Avx512BW.IsSupported)
        {
            const int VectorSize = 32;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx512BW.LoadVector512(a + offset);
                var vb = Avx512BW.LoadVector512(b + offset);
                var vr = Avx512BW.Add(va, vb);
                Avx512BW.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // AVX2 path (16 shorts per operation)
        else if (Avx2.IsSupported)
        {
            const int VectorSize = 16;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = Avx2.LoadVector256(a + offset);
                var vb = Avx2.LoadVector256(b + offset);
                var vr = Avx2.Add(va, vb);
                Avx2.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }
        // ARM NEON path (8 shorts per operation)
        else if (AdvSimd.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = elementCount / VectorSize;

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var va = AdvSimd.LoadVector128(a + offset);
                var vb = AdvSimd.LoadVector128(b + offset);
                var vr = AdvSimd.Add(va, vb);
                AdvSimd.Store(result + offset, vr);
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder
        for (; i < elementCount; i++)
        {
            result[i] = (short)(a[i] + b[i]);
        }
    }

    #endregion

    #region Enhanced ARM NEON Support - HIGH PRIORITY

    /// <summary>
    /// Comprehensive ARM NEON floating-point operations with full instruction coverage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorAdvancedNeonFloat32(
        float* a, float* b, float* c, float* result, long elementCount, NeonOperation operation)
    {
        if (!AdvSimd.IsSupported)
        {
            // Fallback to scalar
            for (long j = 0; j < elementCount; j++)
            {
                result[j] = operation switch
                {
                    NeonOperation.Add => a[j] + b[j],
                    NeonOperation.Multiply => a[j] * b[j],
                    NeonOperation.MultiplyAdd => a[j] * b[j] + (c != null ? c[j] : 0),
                    NeonOperation.MultiplySubtract => (c != null ? c[j] : 0) - (a[j] * b[j]),
                    NeonOperation.ReciprocateEstimate => 1.0f / a[j],
                    NeonOperation.ReciprocalSquareRootEstimate => 1.0f / MathF.Sqrt(a[j]),
                    _ => a[j]
                };
            }
            return;
        }

        const int VectorSize = 4;
        long vectorCount = elementCount / VectorSize;

        for (long v = 0; v < vectorCount; v++)
        {
            var offset = v * VectorSize;
            var va = AdvSimd.LoadVector128(a + offset);
            var vb = AdvSimd.LoadVector128(b + offset);
            var vc = c != null ? AdvSimd.LoadVector128(c + offset) : Vector128<float>.Zero;

            var vr = operation switch
            {
                NeonOperation.Add => AdvSimd.Add(va, vb),
                NeonOperation.Multiply => AdvSimd.Multiply(va, vb),
                NeonOperation.MultiplyAdd => AdvSimd.FusedMultiplyAdd(vc, va, vb),
                NeonOperation.MultiplySubtract => AdvSimd.FusedMultiplySubtract(vc, va, vb),
                NeonOperation.ReciprocateEstimate => AdvSimd.ReciprocalEstimate(va),
                NeonOperation.ReciprocalSquareRootEstimate => AdvSimd.ReciprocalSquareRootEstimate(va),
                NeonOperation.AbsoluteDifference => AdvSimd.AbsoluteDifference(va, vb),
                NeonOperation.Maximum => AdvSimd.Max(va, vb),
                NeonOperation.Minimum => AdvSimd.Min(va, vb),
                NeonOperation.Round => AdvSimd.RoundToNearest(va),
                _ => va
            };

            AdvSimd.Store(result + offset, vr);
        }

        // Scalar remainder
        long i = vectorCount * VectorSize;
        for (; i < elementCount; i++)
        {
            result[i] = operation switch
            {
                NeonOperation.Add => a[i] + b[i],
                NeonOperation.Multiply => a[i] * b[i],
                NeonOperation.MultiplyAdd => a[i] * b[i] + (c != null ? c[i] : 0),
                NeonOperation.MultiplySubtract => (c != null ? c[i] : 0) - (a[i] * b[i]),
                NeonOperation.ReciprocateEstimate => 1.0f / a[i],
                NeonOperation.ReciprocalSquareRootEstimate => 1.0f / MathF.Sqrt(a[i]),
                _ => a[i]
            };
        }
    }

    #endregion

    #region Advanced Gather/Scatter Operations - HIGH PRIORITY

    /// <summary>
    /// Gather operation: loads elements from memory using indices.
    /// Critical for sparse data and indirect memory access patterns.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorGatherFloat32(
        float* basePtr, int* indices, float* result, int count)
    {
        int i = 0;

        // AVX2 gather path (8 elements per operation)
        if (Avx2.IsSupported)
        {
            const int VectorSize = 8;
            int vectorCount = count / VectorSize;

            for (int v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var vindices = Avx2.LoadVector256(indices + offset);

                // Scale indices by sizeof(float) = 4
                var scaledIndices = Avx2.ShiftLeftLogical(vindices, 2);

                // Gather using scaled indices
                var gathered = Avx2.GatherVector256(basePtr, scaledIndices, sizeof(float));
                Avx.Store(result + offset, gathered);
            }
            i = vectorCount * VectorSize;
        }
        // AVX-512 has even more advanced gather operations
        else if (Avx512F.IsSupported)
        {
            const int VectorSize = 16;
            int vectorCount = count / VectorSize;

            for (int v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var vindices = Avx512F.LoadVector512(indices + offset);

                // AVX-512 gather (using simpler approach for compatibility)
                for (int j = 0; j < VectorSize; j++)
                {
                    result[offset + j] = basePtr[indices[offset + j]];
                }
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder
        for (; i < count; i++)
        {
            result[i] = basePtr[indices[i]];
        }
    }

    /// <summary>
    /// Scatter operation: stores elements to memory using indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorScatterFloat32(
        float* values, int* indices, float* basePtr, int count)
    {
        int i = 0;

        // AVX-512 scatter path (16 elements per operation)
        if (Avx512F.IsSupported)
        {
            const int VectorSize = 16;
            int vectorCount = count / VectorSize;

            for (int v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var vvalues = Avx512F.LoadVector512(values + offset);
                var vindices = Avx512F.LoadVector512(indices + offset);

                // AVX-512 scatter (using simpler approach for compatibility)
                for (int j = 0; j < VectorSize; j++)
                {
                    basePtr[indices[offset + j]] = values[offset + j];
                }
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder (no AVX2 scatter available)
        for (; i < count; i++)
        {
            basePtr[indices[i]] = values[i];
        }
    }

    #endregion

    #region Matrix Multiplication Optimization - HIGH PRIORITY

    /// <summary>
    /// Cache-friendly blocked matrix multiplication with FMA optimization.
    /// Essential for linear algebra workloads.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void OptimizedMatrixMultiplyFloat32(
        float* a, float* b, float* c, int m, int n, int k)
    {
        const int BlockSize = 64; // Cache-friendly block size
        const int VectorSize = 8; // AVX2 vector size

        // Clear result matrix
        for (int i = 0; i < m * n; i++)
        {
            c[i] = 0.0f;
        }

        // Blocked matrix multiplication
        for (int ii = 0; ii < m; ii += BlockSize)
        {
            for (int jj = 0; jj < n; jj += BlockSize)
            {
                for (int kk = 0; kk < k; kk += BlockSize)
                {
                    int iMax = Math.Min(ii + BlockSize, m);
                    int jMax = Math.Min(jj + BlockSize, n);
                    int kMax = Math.Min(kk + BlockSize, k);

                    // Micro-kernel with vectorization
                    for (int i = ii; i < iMax; i++)
                    {
                        for (int kIdx = kk; kIdx < kMax; kIdx++)
                        {
                            float aik = a[i * k + kIdx];
                            int j = jj;

                            // Vectorized inner loop with FMA
                            if (Fma.IsSupported && Avx2.IsSupported)
                            {
                                var vaik = Vector256.Create(aik);
                                for (; j + VectorSize <= jMax; j += VectorSize)
                                {
                                    var vb = Avx.LoadVector256(b + kIdx * n + j);
                                    var vc = Avx.LoadVector256(c + i * n + j);

                                    // FMA: c[i,j] += a[i,k] * b[k,j]
                                    var result = Fma.MultiplyAdd(vaik, vb, vc);
                                    Avx.Store(c + i * n + j, result);
                                }
                            }

                            // Scalar remainder
                            for (; j < jMax; j++)
                            {
                                c[i * n + j] += aik * b[kIdx * n + j];
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Horizontal Reduction Operations - HIGH PRIORITY

    /// <summary>
    /// Optimized horizontal sum reduction with SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe float VectorHorizontalSum(float* data, long count)
    {
        long i = 0;

        // AVX-512 reduction
        if (Avx512F.IsSupported)
        {
            const int VectorSize = 16;
            long vectorCount = count / VectorSize;
            var accumulator = Vector512<float>.Zero;

            for (long v = 0; v < vectorCount; v++)
            {
                var vec = Avx512F.LoadVector512(data + v * VectorSize);
                accumulator = Avx512F.Add(accumulator, vec);
            }

            // Horizontal sum of accumulator
            var sum = HorizontalSumAvx512(accumulator);
            i = vectorCount * VectorSize;

            // Add remainder
            for (; i < count; i++)
            {
                sum += data[i];
            }
            return sum;
        }
        // AVX2 reduction
        else if (Avx2.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = count / VectorSize;
            var accumulator = Vector256<float>.Zero;

            for (long v = 0; v < vectorCount; v++)
            {
                var vec = Avx.LoadVector256(data + v * VectorSize);
                accumulator = Avx.Add(accumulator, vec);
            }

            var sum = HorizontalSumAvx256(accumulator);
            i = vectorCount * VectorSize;

            for (; i < count; i++)
            {
                sum += data[i];
            }
            return sum;
        }

        // Scalar fallback
        float scalarSum = 0.0f;
        for (; i < count; i++)
        {
            scalarSum += data[i];
        }
        return scalarSum;
    }

    #endregion

    #region Conditional Selection with Masking - HIGH PRIORITY

    /// <summary>
    /// Conditional selection: result[i] = condition[i] ? a[i] : b[i]
    /// Uses SIMD masking to avoid branch divergence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void VectorConditionalSelect(
        float* condition, float* a, float* b, float* result, long count, float threshold)
    {
        long i = 0;

        // AVX2 conditional selection
        if (Avx2.IsSupported)
        {
            const int VectorSize = 8;
            long vectorCount = count / VectorSize;
            var vthreshold = Vector256.Create(threshold);

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var vcond = Avx.LoadVector256(condition + offset);
                var va = Avx.LoadVector256(a + offset);
                var vb = Avx.LoadVector256(b + offset);

                // Create mask: condition > threshold
                var mask = Avx.Compare(vcond, vthreshold, FloatComparisonMode.OrderedGreaterThanSignaling);

                // Blend based on mask: select a where true, b where false
                var result_vec = Avx.BlendVariable(vb, va, mask);
                Avx.Store(result + offset, result_vec);
            }
            i = vectorCount * VectorSize;
        }
        // ARM NEON conditional selection
        else if (AdvSimd.IsSupported)
        {
            const int VectorSize = 4;
            long vectorCount = count / VectorSize;
            var vthreshold = Vector128.Create(threshold);

            for (long v = 0; v < vectorCount; v++)
            {
                var offset = v * VectorSize;
                var vcond = AdvSimd.LoadVector128(condition + offset);
                var va = AdvSimd.LoadVector128(a + offset);
                var vb = AdvSimd.LoadVector128(b + offset);

                // Create mask and blend
                var mask = AdvSimd.CompareGreaterThan(vcond, vthreshold);
                var result_vec = AdvSimd.BitwiseSelect(mask, va, vb);
                AdvSimd.Store(result + offset, result_vec);
            }
            i = vectorCount * VectorSize;
        }

        // Scalar remainder
        for (; i < count; i++)
        {
            result[i] = condition[i] > threshold ? a[i] : b[i];
        }
    }

    #endregion

    #region Helper Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float HorizontalSumAvx512(Vector512<float> vector)
    {
        // Extract 256-bit halves and add
        var low = vector.GetLower();
        var high = vector.GetUpper();
        var sum256 = Avx.Add(low, high);
        return HorizontalSumAvx256(sum256);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float HorizontalSumAvx256(Vector256<float> vector)
    {
        // Extract 128-bit halves
        var low = vector.GetLower();
        var high = vector.GetUpper();
        var sum128 = Sse.Add(low, high);

        // Horizontal add pairs
        if (Sse3.IsSupported)
        {
            var hadd1 = Sse3.HorizontalAdd(sum128, sum128);
            var hadd2 = Sse3.HorizontalAdd(hadd1, hadd1);
            return hadd2.ToScalar();
        }
        else
        {
            // Manual reduction
            var temp = Sse.Shuffle(sum128, sum128, 0x4E); // Swap high/low 64-bit parts
            sum128 = Sse.Add(sum128, temp);
            temp = Sse.Shuffle(sum128, sum128, 0xB1); // Swap adjacent 32-bit parts
            sum128 = Sse.Add(sum128, temp);
            return sum128.ToScalar();
        }
    }

    #endregion
}

/// <summary>
/// NEON operation types for enhanced ARM support.
/// </summary>
public enum NeonOperation
{
    Add,
    Multiply,
    MultiplyAdd,
    MultiplySubtract,
    ReciprocateEstimate,
    ReciprocalSquareRootEstimate,
    AbsoluteDifference,
    Maximum,
    Minimum,
    Round
}
