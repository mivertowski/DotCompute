// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Utilities;

/// <summary>
/// Structure of Arrays (SoA) utilities for optimal SIMD performance.
/// Converts Array of Structures (AoS) to Structure of Arrays (SoA) for better vectorization.
/// </summary>
public static class StructureOfArrays
{
    /// <summary>
    /// Represents a 3D point in SoA layout for optimal SIMD processing.
    /// </summary>
    internal sealed class Point3DSoA(int capacity) : IDisposable
    {
        public float[] X { get; } = new float[capacity];
        public float[] Y { get; } = new float[capacity];
        public float[] Z { get; } = new float[capacity];
        public int Length { get; } = capacity;

        /// <summary>
        /// Converts from Array of Structures to Structure of Arrays.
        /// </summary>
        public static Point3DSoA FromAoS(ReadOnlySpan<Point3D> points)
        {
            var soa = new Point3DSoA(points.Length);

            if (Vector256.IsHardwareAccelerated && points.Length >= 8)
            {
                ConvertAoSToSoAVectorized(points, soa);
            }
            else
            {
                ConvertAoSToSoAScalar(points, soa);
            }

            return soa;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static unsafe void ConvertAoSToSoAVectorized(ReadOnlySpan<Point3D> points, Point3DSoA soa)
        {
            const int vectorSize = 8; // AVX2 can process 8 floats at once
            var vectorCount = points.Length / vectorSize;

            fixed (Point3D* pointsPtr = points)
            fixed (float* xPtr = soa.X)
            fixed (float* yPtr = soa.Y)
            fixed (float* zPtr = soa.Z)
            {
                for (var i = 0; i < vectorCount; i++)
                {
                    var baseIndex = i * vectorSize;

                    // Load 8 points (24 floats total)
                    var p0 = pointsPtr[baseIndex + 0];
                    var p1 = pointsPtr[baseIndex + 1];
                    var p2 = pointsPtr[baseIndex + 2];
                    var p3 = pointsPtr[baseIndex + 3];
                    var p4 = pointsPtr[baseIndex + 4];
                    var p5 = pointsPtr[baseIndex + 5];
                    var p6 = pointsPtr[baseIndex + 6];
                    var p7 = pointsPtr[baseIndex + 7];

                    // Pack X coordinates
                    var xVec = Vector256.Create(p0.X, p1.X, p2.X, p3.X, p4.X, p5.X, p6.X, p7.X);
                    var yVec = Vector256.Create(p0.Y, p1.Y, p2.Y, p3.Y, p4.Y, p5.Y, p6.Y, p7.Y);
                    var zVec = Vector256.Create(p0.Z, p1.Z, p2.Z, p3.Z, p4.Z, p5.Z, p6.Z, p7.Z);

                    // Store to SoA arrays
                    Avx.Store(xPtr + baseIndex, xVec);
                    Avx.Store(yPtr + baseIndex, yVec);
                    Avx.Store(zPtr + baseIndex, zVec);
                }
            }

            // Handle remainder
            var remainder = points.Length % vectorSize;
            if (remainder > 0)
            {
                var lastOffset = vectorCount * vectorSize;
                for (var i = 0; i < remainder; i++)
                {
                    var idx = lastOffset + i;
                    var point = points[idx];
                    soa.X[idx] = point.X;
                    soa.Y[idx] = point.Y;
                    soa.Z[idx] = point.Z;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertAoSToSoAScalar(ReadOnlySpan<Point3D> points, Point3DSoA soa)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                soa.X[i] = point.X;
                soa.Y[i] = point.Y;
                soa.Z[i] = point.Z;
            }
        }

        /// <summary>
        /// Performs vectorized distance calculation between all points and a reference point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void CalculateDistancesTo(Point3D reference, Span<float> distances)
        {
            if (distances.Length != Length)
            {
                throw new ArgumentException("Distances array must match point count");
            }

            var refX = Vector256.Create(reference.X);
            var refY = Vector256.Create(reference.Y);
            var refZ = Vector256.Create(reference.Z);

            if (Vector256.IsHardwareAccelerated && Length >= 8)
            {
                CalculateDistancesVectorized(refX, refY, refZ, distances);
            }
            else
            {
                CalculateDistancesScalar(reference, distances);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void CalculateDistancesVectorized(Vector256<float> refX, Vector256<float> refY, Vector256<float> refZ, Span<float> distances)
        {
            const int vectorSize = 8;
            var vectorCount = Length / vectorSize;

            ref var xRef = ref MemoryMarshal.GetArrayDataReference(X);
            ref var yRef = ref MemoryMarshal.GetArrayDataReference(Y);
            ref var zRef = ref MemoryMarshal.GetArrayDataReference(Z);
            ref var distRef = ref MemoryMarshal.GetReference(distances);

            for (var i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;

                var x = Vector256.LoadUnsafe(ref Unsafe.Add(ref xRef, offset));
                var y = Vector256.LoadUnsafe(ref Unsafe.Add(ref yRef, offset));
                var z = Vector256.LoadUnsafe(ref Unsafe.Add(ref zRef, offset));

                // Calculate squared distance: (x-refX)² + (y-refY)² + (z-refZ)²
                var dx = Avx.Subtract(x, refX);
                var dy = Avx.Subtract(y, refY);
                var dz = Avx.Subtract(z, refZ);

                Vector256<float> distSq;
                if (Fma.IsSupported)
                {
                    // Use FMA for better precision: dx² + dy² + dz²
                    var dx2 = Avx.Multiply(dx, dx);
                    var dy2dy2PlusDz2 = Fma.MultiplyAdd(dy, dy, Avx.Multiply(dz, dz));
                    distSq = Avx.Add(dx2, dy2dy2PlusDz2);
                }
                else
                {
                    var dx2 = Avx.Multiply(dx, dx);
                    var dy2 = Avx.Multiply(dy, dy);
                    var dz2 = Avx.Multiply(dz, dz);
                    distSq = Avx.Add(Avx.Add(dx2, dy2), dz2);
                }

                // Square root to get distance
                var dist = Avx.Sqrt(distSq);

                dist.StoreUnsafe(ref Unsafe.Add(ref distRef, offset));
            }

            // Handle remainder
            var remainder = Length % vectorSize;
            if (remainder > 0)
            {
                var lastOffset = vectorCount * vectorSize;
                for (var i = 0; i < remainder; i++)
                {
                    var idx = lastOffset + i;
                    var dx = X[idx] - refX[0];
                    var dy = Y[idx] - refY[0];
                    var dz = Z[idx] - refZ[0];
                    distances[idx] = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateDistancesScalar(Point3D reference, Span<float> distances)
        {
            for (var i = 0; i < Length; i++)
            {
                var dx = X[i] - reference.X;
                var dy = Y[i] - reference.Y;
                var dz = Z[i] - reference.Z;
                distances[i] = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        public void Dispose()
        {
            // Arrays will be garbage collected
        }
    }

    /// <summary>
    /// Represents a complex number in SoA layout for optimal SIMD processing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "CA1812:Avoid uninstantiated internal classes", Justification = "Used for demonstration and may be instantiated via reflection")]
    internal sealed class ComplexSoA(int capacity) : IDisposable
    {
        public float[] Real { get; } = new float[capacity];
        public float[] Imaginary { get; } = new float[capacity];
        public int Length { get; } = capacity;

        /// <summary>
        /// Performs vectorized complex multiplication: (a + bi) * (c + di) = (ac - bd) + (ad + bc)i
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void Multiply(ComplexSoA a, ComplexSoA b, ComplexSoA result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
            {
                throw new ArgumentException("All complex arrays must have the same length");
            }

            if (Vector256.IsHardwareAccelerated && a.Length >= 8)
            {
                MultiplyVectorized(a, b, result);
            }
            else
            {
                MultiplyScalar(a, b, result);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void MultiplyVectorized(ComplexSoA a, ComplexSoA b, ComplexSoA result)
        {
            const int vectorSize = 8;
            var vectorCount = a.Length / vectorSize;

            ref var aRealRef = ref MemoryMarshal.GetArrayDataReference(a.Real);
            ref var aImagRef = ref MemoryMarshal.GetArrayDataReference(a.Imaginary);
            ref var bRealRef = ref MemoryMarshal.GetArrayDataReference(b.Real);
            ref var bImagRef = ref MemoryMarshal.GetArrayDataReference(b.Imaginary);
            ref var resultRealRef = ref MemoryMarshal.GetArrayDataReference(result.Real);
            ref var resultImagRef = ref MemoryMarshal.GetArrayDataReference(result.Imaginary);

            for (var i = 0; i < vectorCount; i++)
            {
                var offset = i * vectorSize;

                var aReal = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRealRef, offset));
                var aImag = Vector256.LoadUnsafe(ref Unsafe.Add(ref aImagRef, offset));
                var bReal = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRealRef, offset));
                var bImag = Vector256.LoadUnsafe(ref Unsafe.Add(ref bImagRef, offset));

                Vector256<float> resultReal, resultImag;

                if (Fma.IsSupported)
                {
                    // Use FMA for complex multiplication:
                    // Real part: ac - bd = FMA(a.real, b.real, -a.imag * b.imag)
                    // Imag part: ad + bc = FMA(a.real, b.imag, a.imag * b.real)
                    var minusAImagBImag = Avx.Multiply(Avx.Xor(aImag, Vector256.Create(-0.0f)), bImag);
                    resultReal = Fma.MultiplyAdd(aReal, bReal, minusAImagBImag);
                    resultImag = Fma.MultiplyAdd(aReal, bImag, Avx.Multiply(aImag, bReal));
                }
                else
                {
                    // Standard complex multiplication
                    var ac = Avx.Multiply(aReal, bReal);
                    var bd = Avx.Multiply(aImag, bImag);
                    var ad = Avx.Multiply(aReal, bImag);
                    var bc = Avx.Multiply(aImag, bReal);

                    resultReal = Avx.Subtract(ac, bd);
                    resultImag = Avx.Add(ad, bc);
                }

                resultReal.StoreUnsafe(ref Unsafe.Add(ref resultRealRef, offset));
                resultImag.StoreUnsafe(ref Unsafe.Add(ref resultImagRef, offset));
            }

            // Handle remainder
            var remainder = a.Length % vectorSize;
            if (remainder > 0)
            {
                var lastOffset = vectorCount * vectorSize;
                for (var i = 0; i < remainder; i++)
                {
                    var idx = lastOffset + i;
                    var aR = a.Real[idx];
                    var aI = a.Imaginary[idx];
                    var bR = b.Real[idx];
                    var bI = b.Imaginary[idx];

                    result.Real[idx] = aR * bR - aI * bI;
                    result.Imaginary[idx] = aR * bI + aI * bR;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MultiplyScalar(ComplexSoA a, ComplexSoA b, ComplexSoA result)
        {
            for (var i = 0; i < a.Length; i++)
            {
                var aR = a.Real[i];
                var aI = a.Imaginary[i];
                var bR = b.Real[i];
                var bI = b.Imaginary[i];

                result.Real[i] = aR * bR - aI * bI;
                result.Imaginary[i] = aR * bI + aI * bR;
            }
        }

        public void Dispose()
        {
            // Arrays will be garbage collected
        }
    }

    /// <summary>
    /// Utility methods for general SoA transformations.
    /// </summary>
    internal static class Transforms
    {
        /// <summary>
        /// Transpose a matrix stored in row-major order for better cache locality in column operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void TransposeMatrix4x4(ReadOnlySpan<float> source, Span<float> destination)
        {
            if (source.Length != 16 || destination.Length != 16)
            {
                throw new ArgumentException("Source and destination must be 4x4 matrices (16 elements)");
            }

            if (Vector128.IsHardwareAccelerated)
            {
                TransposeMatrix4x4Vectorized(source, destination);
            }
            else
            {
                TransposeMatrix4x4Scalar(source, destination);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void TransposeMatrix4x4Vectorized(ReadOnlySpan<float> source, Span<float> destination)
        {
            ref var srcRef = ref MemoryMarshal.GetReference(source);
            ref var dstRef = ref MemoryMarshal.GetReference(destination);

            // Load 4 rows as vectors
            var row0 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, 0));  // [00, 01, 02, 03]
            var row1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, 4));  // [10, 11, 12, 13]
            var row2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, 8));  // [20, 21, 22, 23]
            var row3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, 12)); // [30, 31, 32, 33]

            Vector128<float> col0, col1, col2, col3;

            if (Sse.IsSupported)
            {
                // Use SSE shuffle to transpose
                // Interleave low and high parts
                var tmp0 = Sse.UnpackLow(row0, row1);   // [00, 10, 01, 11]
                var tmp1 = Sse.UnpackHigh(row0, row1);  // [02, 12, 03, 13]
                var tmp2 = Sse.UnpackLow(row2, row3);   // [20, 30, 21, 31]
                var tmp3 = Sse.UnpackHigh(row2, row3);  // [22, 32, 23, 33]

                // Final shuffle to get columns
                col0 = Sse.MoveLowToHigh(tmp0, tmp2);  // [00, 10, 20, 30]
                col1 = Sse.MoveHighToLow(tmp2, tmp0);  // [01, 11, 21, 31]
                col2 = Sse.MoveLowToHigh(tmp1, tmp3);  // [02, 12, 22, 32]
                col3 = Sse.MoveHighToLow(tmp3, tmp1);  // [03, 13, 23, 33]
            }
            else if (AdvSimd.IsSupported)
            {
                // Use ARM NEON transpose with compatible operations
                // Manual transpose using element access for maximum compatibility
                col0 = Vector128.Create(row0.GetElement(0), row1.GetElement(0), row2.GetElement(0), row3.GetElement(0));
                col1 = Vector128.Create(row0.GetElement(1), row1.GetElement(1), row2.GetElement(1), row3.GetElement(1));
                col2 = Vector128.Create(row0.GetElement(2), row1.GetElement(2), row2.GetElement(2), row3.GetElement(2));
                col3 = Vector128.Create(row0.GetElement(3), row1.GetElement(3), row2.GetElement(3), row3.GetElement(3));
            }
            else
            {
                // Generic fallback
                TransposeMatrix4x4Scalar(source, destination);
                return;
            }

            // Store transposed columns as rows
            col0.StoreUnsafe(ref Unsafe.Add(ref dstRef, 0));
            col1.StoreUnsafe(ref Unsafe.Add(ref dstRef, 4));
            col2.StoreUnsafe(ref Unsafe.Add(ref dstRef, 8));
            col3.StoreUnsafe(ref Unsafe.Add(ref dstRef, 12));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TransposeMatrix4x4Scalar(ReadOnlySpan<float> source, Span<float> destination)
        {
            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    destination[col * 4 + row] = source[row * 4 + col];
                }
            }
        }
    }
}

/// <summary>
/// Simple 3D point structure for demonstration.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Point3D(float x, float y, float z) : IEquatable<Point3D>
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Z = z;

    public override bool Equals(object? obj) => obj is Point3D other && Equals(other);

    public bool Equals(Point3D other) => X == other.X && Y == other.Y && Z == other.Z;

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Point3D left, Point3D right) => left.Equals(right);

    public static bool operator !=(Point3D left, Point3D right) => !left.Equals(right);
}
