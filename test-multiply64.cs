using System;
using System.Runtime.InteropServices;
using DotCompute.Backends.CPU.Kernels;

class TestMultiply64
{
    static unsafe void Main()
    {
        const int count = 16;
        long[] a = new long[count];
        long[] b = new long[count];
        long[] result = new long[count];
        
        // Initialize test data
        for (int i = 0; i < count; i++)
        {
            a[i] = i + 1;
            b[i] = (i + 1) * 2;
        }
        
        // Test the multiplication
        fixed (long* aPtr = a)
        fixed (long* bPtr = b)
        fixed (long* resultPtr = result)
        {
            AdvancedSimdKernels.VectorMultiplyInt64(aPtr, bPtr, resultPtr, count);
        }
        
        // Verify results
        bool passed = true;
        for (int i = 0; i < count; i++)
        {
            long expected = a[i] * b[i];
            if (result[i] != expected)
            {
                Console.WriteLine($"FAIL at index {i}: {a[i]} * {b[i]} = {result[i]}, expected {expected}");
                passed = false;
            }
        }
        
        if (passed)
        {
            Console.WriteLine("PASS: All 64-bit multiplications are correct!");
            Console.WriteLine("Sample results:");
            for (int i = 0; i < Math.Min(5, count); i++)
            {
                Console.WriteLine($"  {a[i]} * {b[i]} = {result[i]}");
            }
        }
        else
        {
            Console.WriteLine("FAIL: 64-bit multiplication has errors!");
        }
    }
}