// Test kernel to demonstrate enhanced CUDA generator support
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;
using System;

namespace DotCompute.Tests.Generator
{
    /// <summary>
    /// Test class containing kernels that demonstrate cross-backend execution capability.
    /// These kernels should be compiled for both CPU and CUDA backends with the enhanced generator.
    /// </summary>
    public static class TestKernels
    {
        /// <summary>
        /// Simple element-wise addition kernel that supports both CPU and CUDA backends.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU | KernelBackends.CUDA, 
            VectorSize = 8,
            IsParallel = true,
            Optimizations = OptimizationHints.Vectorize | OptimizationHints.LoopUnroll,
            MemoryPattern = MemoryAccessPattern.Sequential)]
        public static void VectorAdd(
            ReadOnlySpan<float> input1, 
            ReadOnlySpan<float> input2, 
            Span<float> output)
        {
            // This kernel implementation will be analyzed by the generator
            // and converted to appropriate backend-specific code (SIMD for CPU, CUDA C for GPU)
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = input1[i] + input2[i];
            }
        }

        /// <summary>
        /// Scalar multiplication kernel demonstrating parameter marshaling.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU | KernelBackends.CUDA,
            VectorSize = 16,
            IsParallel = true,
            GridDimensions = [256],
            BlockDimensions = [256])]
        public static void ScalarMultiply(
            ReadOnlySpan<float> input, 
            Span<float> output, 
            float scalar)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = input[i] * scalar;
            }
        }

        /// <summary>
        /// Matrix transpose kernel for testing 2D indexing patterns.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CUDA,
            BlockDimensions = [16, 16],
            MemoryPattern = MemoryAccessPattern.Strided)]
        public static void MatrixTranspose(
            ReadOnlySpan<float> input,
            Span<float> output,
            int rows,
            int cols)
        {
            // 2D kernel pattern - will be converted to CUDA 2D grid
            int totalElements = rows * cols;
            for (int i = 0; i < totalElements; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int transposed_index = col * rows + row;
                output[transposed_index] = input[i];
            }
        }

        /// <summary>
        /// Reduction kernel for testing more complex memory patterns.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU | KernelBackends.CUDA,
            VectorSize = 8,
            Optimizations = OptimizationHints.LoopUnroll | OptimizationHints.MemoryOptimize,
            MemoryPattern = MemoryAccessPattern.Random)]
        public static void Sum(
            ReadOnlySpan<float> input,
            Span<float> partialSums,
            int blockSize)
        {
            // Simplified reduction - real implementation would use shared memory in CUDA
            int numBlocks = (input.Length + blockSize - 1) / blockSize;
            
            for (int blockId = 0; blockId < numBlocks; blockId++)
            {
                float sum = 0.0f;
                int start = blockId * blockSize;
                int end = Math.Min(start + blockSize, input.Length);
                
                for (int i = start; i < end; i++)
                {
                    sum += input[i];
                }
                
                partialSums[blockId] = sum;
            }
        }
    }
}