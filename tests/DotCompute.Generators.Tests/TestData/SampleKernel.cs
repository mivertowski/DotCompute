// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;

namespace TestData
{
    /// <summary>
    /// Sample kernel class for testing source generation.
    /// </summary>
    public class SampleKernels
    {
        /// <summary>
        /// Simple vector addition kernel.
        /// </summary>
        [Kernel]
        public static void VectorAdd(float[] a, float[] b, float[] result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        /// <summary>
        /// Parallel vector multiplication with SIMD optimization.
        /// </summary>
        [Kernel(VectorSize = 8, IsParallel = true, Optimizations = OptimizationHints.Vectorize)]
        public static void VectorMultiply(float[] a, float[] b, float[] result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }

        /// <summary>
        /// Multi-backend kernel supporting CPU and GPU.
        /// </summary>
        [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA,
                GridDimensions = new[] { 256, 256 },
                BlockDimensions = new[] { 16, 16 })]
        public static void MatrixMultiply(float[] a, float[] b, float[] c, int size)
        {
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float sum = 0.0f;
                    for (int k = 0; k < size; k++)
                    {
                        sum += a[i * size + k] * b[k * size + j];
                    }
                    c[i * size + j] = sum;
                }
            }
        }
    }
}