// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;

namespace SimpleExample
{
    /// <summary>
    /// Simple example demonstrating DotCompute core functionality without source generation.
    /// </summary>
    internal sealed unsafe class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DotCompute Simple Example");
            Console.WriteLine("========================");

            // Create memory manager
            const int size = 1024 * 1024; // 1M elements
            var a = ArrayPool<float>.Shared.Rent(size);
            var b = ArrayPool<float>.Shared.Rent(size);
            var result = ArrayPool<float>.Shared.Rent(size);

            try
            {

                // Initialize data
                InitializeData(a.AsSpan(0, size), b.AsSpan(0, size));

                // Execute simple vector addition
                Console.WriteLine("\nExecuting vector addition...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                AddVectors(a.AsSpan(0, size), b.AsSpan(0, size), result.AsSpan(0, size));

                sw.Stop();
                Console.WriteLine($"Completed in {sw.ElapsedMilliseconds}ms");

                // Verify results
                VerifyResults(a.AsSpan(0, size), b.AsSpan(0, size), result.AsSpan(0, size));
            }
            finally
            {
                // Cleanup
                ArrayPool<float>.Shared.Return(a);
                ArrayPool<float>.Shared.Return(b);
                ArrayPool<float>.Shared.Return(result);
            }

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

        private static void AddVectors(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
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
}
