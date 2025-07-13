// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using DotCompute.Generators.Kernel;
using DotCompute.Memory;

namespace KernelExample
{
    /// <summary>
    /// Example of DotCompute kernel source generation.
    /// </summary>
    public unsafe class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DotCompute Kernel Source Generator Example");
            Console.WriteLine("==========================================");

            // Create memory manager
            var memoryManager = new UnifiedMemoryManager();

            // Allocate buffers
            const int size = 1024 * 1024; // 1M elements
            var a = memoryManager.Allocate<float>(size);
            var b = memoryManager.Allocate<float>(size);
            var result = memoryManager.Allocate<float>(size);

            // Initialize data
            InitializeData(a.AsSpan(), b.AsSpan());

            // Execute kernel (this will use generated code)
            Console.WriteLine("\nExecuting vector addition kernel...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // This will call the generated implementation
            VectorMath.AddVectors(
                (float*)a.DataPointer,
                (float*)b.DataPointer,
                (float*)result.DataPointer,
                size);
            
            sw.Stop();
            Console.WriteLine($"Completed in {sw.ElapsedMilliseconds}ms");

            // Verify results
            VerifyResults(a.AsSpan(), b.AsSpan(), result.AsSpan());

            // Cleanup
            a.Dispose();
            b.Dispose();
            result.Dispose();
            memoryManager.Dispose();

            Console.WriteLine("\nExample completed successfully!");
        }

        private static void InitializeData(Span<float> a, Span<float> b)
        {
            var random = new Random(42);
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = (float)random.NextDouble() * 100;
                b[i] = (float)random.NextDouble() * 100;
            }
        }

        private static void VerifyResults(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> result)
        {
            Console.WriteLine("\nVerifying results...");
            int errors = 0;
            for (int i = 0; i < Math.Min(10, a.Length); i++)
            {
                var expected = a[i] + b[i];
                var actual = result[i];
                if (Math.Abs(expected - actual) > 0.0001f)
                {
                    errors++;
                    Console.WriteLine($"Error at index {i}: expected {expected}, got {actual}");
                }
            }
            
            if (errors == 0)
            {
                Console.WriteLine("✓ Results verified successfully!");
            }
            else
            {
                Console.WriteLine($"✗ Found {errors} errors!");
            }
        }
    }

    /// <summary>
    /// Kernel methods that will be processed by the source generator.
    /// </summary>
    public static unsafe class VectorMath
    {
        /// <summary>
        /// Adds two vectors element-wise.
        /// The source generator will create optimized implementations for this method.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU,
            VectorSize = 8,
            IsParallel = true,
            Optimizations = OptimizationHints.AggressiveInlining | OptimizationHints.Vectorize)]
        public static void AddVectors(float* a, float* b, float* result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        /// <summary>
        /// Performs multiply-add operation: result = a * b + c
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU,
            VectorSize = 8,
            IsParallel = true,
            MemoryPattern = MemoryAccessPattern.Sequential)]
        public static void MultiplyAdd(float* a, float* b, float* c, float* result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] * b[i] + c[i];
            }
        }

        /// <summary>
        /// Computes dot product of two vectors.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU,
            VectorSize = 8,
            Optimizations = OptimizationHints.All)]
        public static float DotProduct(float* a, float* b, int length)
        {
            float sum = 0.0f;
            for (int i = 0; i < length; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }

        /// <summary>
        /// Applies ReLU activation function.
        /// </summary>
        [Kernel(
            Backends = KernelBackends.CPU,
            VectorSize = 16,
            IsParallel = true)]
        public static void ReLU(float* input, float* output, int length)
        {
            for (int i = 0; i < length; i++)
            {
                output[i] = input[i] > 0 ? input[i] : 0;
            }
        }
    }
}