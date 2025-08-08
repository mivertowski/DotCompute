// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Generators.Kernel;

namespace TestData
{
    /// <summary>
    /// Complex kernel examples for advanced testing scenarios.
    /// </summary>
    public unsafe class ComplexKernels
    {
        /// <summary>
        /// Complex signal processing kernel with conditional logic.
        /// </summary>
        [Kernel(Backends = KernelBackends.All, 
                VectorSize = 16,
                Optimizations = OptimizationHints.All,
                MemoryPattern = MemoryAccessPattern.Sequential)]
        public static void SignalProcessor(float[] input, float[] output, float threshold, int length)
        {
            for (int i = 0; i < length; i++)
            {
                float value = input[i];
                
                if (Math.Abs(value) > threshold)
                {
                    // Apply nonlinear transformation
                    float processed = (float)(Math.Sign(value) * Math.Pow(Math.Abs(value), 0.8));
                    
                    // Apply smoothing filter
                    if (i > 0 && i < length - 1)
                    {
                        processed = (input[i-1] * 0.25f + processed * 0.5f + input[i+1] * 0.25f);
                    }
                    
                    output[i] = processed;
                }
                else
                {
                    // Linear scaling for small values
                    output[i] = value * 2.0f;
                }
            }
        }

        /// <summary>
        /// Image convolution kernel with pointer arithmetic.
        /// </summary>
        [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA,
                MemoryPattern = MemoryAccessPattern.Tiled)]
        public static void ImageConvolution(float* input, float* output, float* kernel, 
                                          int width, int height, int kernelSize)
        {
            int halfKernel = kernelSize / 2;
            
            for (int y = halfKernel; y < height - halfKernel; y++)
            {
                for (int x = halfKernel; x < width - halfKernel; x++)
                {
                    float sum = 0.0f;
                    
                    for (int ky = -halfKernel; ky <= halfKernel; ky++)
                    {
                        for (int kx = -halfKernel; kx <= halfKernel; kx++)
                        {
                            int imageIndex = (y + ky) * width + (x + kx);
                            int kernelIndex = (ky + halfKernel) * kernelSize + (kx + halfKernel);
                            sum += input[imageIndex] * kernel[kernelIndex];
                        }
                    }
                    
                    output[y * width + x] = sum;
                }
            }
        }

        /// <summary>
        /// Monte Carlo simulation kernel with random number generation.
        /// </summary>
        [Kernel(Backends = KernelBackends.CPU | KernelBackends.OpenCL,
                IsParallel = true,
                MemoryPattern = MemoryAccessPattern.Random)]
        public static void MonteCarloPI(float[] randomX, float[] randomY, int[] results, int samples)
        {
            for (int i = 0; i < samples; i++)
            {
                float x = randomX[i];
                float y = randomY[i];
                float distance = x * x + y * y;
                results[i] = distance <= 1.0f ? 1 : 0;
            }
        }
    }
}