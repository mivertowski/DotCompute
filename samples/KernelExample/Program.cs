// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;

namespace DotCompute.Samples.KernelExample
{
    /// <summary>
    /// Example of DotCompute kernel source generation.
    /// </summary>
    internal sealed unsafe class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("DotCompute Kernel Source Generator Example");
            Console.WriteLine("==========================================");
            Console.WriteLine("Note: This is a simplified demonstration of the kernel generation system.");

            // Create test data using regular arrays for this example
            const int size = 1024 * 1024; // 1M elements
            var a = new float[size];
            var b = new float[size];
            var result = new float[size];

            // Initialize data
            InitializeData(a.AsSpan(), b.AsSpan());

            // Execute kernel (this will use generated code)
            Console.WriteLine("\nExecuting vector addition kernel...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // This demonstrates the kernel interface - in real scenarios this would work with GPU memory
            VectorMath.AddVectors(a.AsSpan(), b.AsSpan(), result.AsSpan());

            sw.Stop();
            Console.WriteLine($"Completed in {sw.ElapsedMilliseconds}ms");

            // Verify results
            VerifyResults(a.AsSpan(), b.AsSpan(), result.AsSpan());

            Console.WriteLine("\nExample completed successfully!");
            Console.WriteLine("In production, this would use actual GPU memory buffers and execute on the GPU.");
        }

        private static void InitializeData(Span<float> a, Span<float> b)
        {
#pragma warning disable CA5394 // Do not use insecure randomness - This is for demo purposes only, not cryptographic use
            var random = new Random(42);
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = (float)random.NextDouble() * 100;
                b[i] = (float)random.NextDouble() * 100;
            }
#pragma warning restore CA5394
        }

        private static void VerifyResults(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> result)
        {
            Console.WriteLine("\nVerifying results...");
            var errors = 0;
            for (var i = 0; i < Math.Min(10, a.Length); i++)
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
    internal static unsafe class VectorMath
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
        public static void AddVectors(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            for (var i = 0; i < a.Length; i++)
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
        public static void MultiplyAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
        {
            for (var i = 0; i < a.Length; i++)
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
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            var sum = 0.0f;
            for (var i = 0; i < a.Length; i++)
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
        public static void ReLU(ReadOnlySpan<float> input, Span<float> output)
        {
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = input[i] > 0 ? input[i] : 0;
            }
        }
    }
}