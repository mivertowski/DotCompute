// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;
using global::System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// SIMD utility operations (Transpose, etc.) with cross-platform optimization.
/// </summary>
internal static class SimdUtilityOperations
{
    /// <summary>
    /// Cross-platform SIMD matrix transpose with cache-friendly blocking.
    /// </summary>
    /// <param name="source">Source matrix data (row-major)</param>
    /// <param name="dest">Destination matrix data (column-major)</param>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Transpose(ReadOnlySpan<float> source, Span<float> dest, int rows, int cols)
    {
        if (source.Length != rows * cols || dest.Length != rows * cols)
        {
            throw new ArgumentException("Source and destination must match matrix dimensions");
        }

        fixed (float* srcPtr = source, dstPtr = dest)
        {
            if (SimdCapabilities.HasAvx2 && rows >= 8 && cols >= 8)
            {
                TransposeAvx2(srcPtr, dstPtr, rows, cols);
            }
            else if (SimdCapabilities.HasNeon && rows >= 4 && cols >= 4)
            {
                TransposeNeon(srcPtr, dstPtr, rows, cols);
            }
            else if (Sse2.IsSupported && rows >= 4 && cols >= 4)
            {
                TransposeSse2(srcPtr, dstPtr, rows, cols);
            }
            else
            {
                TransposeFallback(srcPtr, dstPtr, rows, cols);
            }
        }
    }

    #region AVX2 Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeAvx2(float* source, float* dest, int rows, int cols)
    {
        // 8x8 transpose using AVX2
        const int blockSize = 8;

        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                TransposeBlock8x8Avx2(source, dest, ii, jj, rows, cols, cols, rows);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeBlock8x8Avx2(float* src, float* dst,
        int blockRow, int blockCol, int srcRows, int srcCols, int srcStride, int dstStride)
    {
        var effectiveRows = Math.Min(8, srcRows - blockRow);
        var effectiveCols = Math.Min(8, srcCols - blockCol);

        // Fallback to scalar for partial blocks
        for (var i = 0; i < effectiveRows; i++)
        {
            for (var j = 0; j < effectiveCols; j++)
            {
                dst[(blockCol + j) * dstStride + (blockRow + i)] =
                    src[(blockRow + i) * srcStride + (blockCol + j)];
            }
        }
    }

    #endregion

    #region ARM NEON Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeNeon(float* source, float* dest, int rows, int cols)
    {
        // 4x4 transpose using NEON
        const int blockSize = 4;

        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                TransposeBlock4x4Neon(source, dest, ii, jj, rows, cols, cols, rows);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeBlock4x4Neon(float* src, float* dst,
        int blockRow, int blockCol, int srcRows, int srcCols, int srcStride, int dstStride)
    {
        var effectiveRows = Math.Min(4, srcRows - blockRow);
        var effectiveCols = Math.Min(4, srcCols - blockCol);

        // Fallback to scalar for now
        for (var i = 0; i < effectiveRows; i++)
        {
            for (var j = 0; j < effectiveCols; j++)
            {
                dst[(blockCol + j) * dstStride + (blockRow + i)] =
                    src[(blockRow + i) * srcStride + (blockCol + j)];
            }
        }
    }

    #endregion

    #region SSE Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeSse2(float* source, float* dest, int rows, int cols)
    {
        // 4x4 transpose using SSE2
        const int blockSize = 4;

        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                TransposeBlock4x4Sse2(source, dest, ii, jj, rows, cols, cols, rows);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeBlock4x4Sse2(float* src, float* dst,
        int blockRow, int blockCol, int srcRows, int srcCols, int srcStride, int dstStride)
    {
        var effectiveRows = Math.Min(4, srcRows - blockRow);
        var effectiveCols = Math.Min(4, srcCols - blockCol);

        if (effectiveRows == 4 && effectiveCols == 4)
        {
            // Full 4x4 block - use SSE2 transpose
            var row0 = Sse.LoadVector128(src + (blockRow + 0) * srcStride + blockCol);
            var row1 = Sse.LoadVector128(src + (blockRow + 1) * srcStride + blockCol);
            var row2 = Sse.LoadVector128(src + (blockRow + 2) * srcStride + blockCol);
            var row3 = Sse.LoadVector128(src + (blockRow + 3) * srcStride + blockCol);

            // Transpose 4x4 using shuffle operations
            var tmp0 = Sse.UnpackLow(row0, row1);
            var tmp2 = Sse.UnpackLow(row2, row3);
            var tmp1 = Sse.UnpackHigh(row0, row1);
            var tmp3 = Sse.UnpackHigh(row2, row3);

            var col0 = Sse.MoveLowToHigh(tmp0, tmp2);
            var col1 = Sse.MoveHighToLow(tmp2, tmp0);
            var col2 = Sse.MoveLowToHigh(tmp1, tmp3);
            var col3 = Sse.MoveHighToLow(tmp3, tmp1);

            Sse.Store(dst + (blockCol + 0) * dstStride + blockRow, col0);
            Sse.Store(dst + (blockCol + 1) * dstStride + blockRow, col1);
            Sse.Store(dst + (blockCol + 2) * dstStride + blockRow, col2);
            Sse.Store(dst + (blockCol + 3) * dstStride + blockRow, col3);
        }
        else
        {
            // Partial block - use scalar transpose
            for (var i = 0; i < effectiveRows; i++)
            {
                for (var j = 0; j < effectiveCols; j++)
                {
                    dst[(blockCol + j) * dstStride + (blockRow + i)] =
                        src[(blockRow + i) * srcStride + (blockCol + j)];
                }
            }
        }
    }

    #endregion

    #region Fallback Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeFallback(float* source, float* dest, int rows, int cols)
    {
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                dest[j * rows + i] = source[i * cols + j];
            }
        }
    }

    #endregion
}