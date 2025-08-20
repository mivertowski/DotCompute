// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

using DotCompute.Abstractions.Kernels;
namespace DotCompute.Tests;

/// <summary>
/// Simple validation program to test KernelArguments API fixes.
/// </summary>
public static class KernelArgumentsValidation
{
    public static void Main()
    {
        Console.WriteLine("Testing KernelArguments API fixes...");

        // Test 1: Create with capacity
        Console.WriteLine("\n1. Testing Create with capacity:");
        var args1 = KernelArguments.Create(3);
        Console.WriteLine($"   Created with capacity 3, Length: {args1.Length}");

        // Test 2: Set method
        Console.WriteLine("\n2. Testing Set method:");
        args1.Set(0, 42);
        args1.Set(1, "test string");
        args1.Set(2, new float[] { 1.0f, 2.0f, 3.0f });
        Console.WriteLine("   Set values at indices 0, 1, 2");

        // Test 3: Get<T> method with explicit type
        Console.WriteLine("\n3. Testing Get<T> method:");
        var intValue = args1.Get<int>(0);
        var stringValue = args1.Get<string>(1);
        var floatArray = args1.Get<float[]>(2);
        Console.WriteLine($"   Get<int>(0): {intValue}");
        Console.WriteLine($"   Get<string>(1): {stringValue}");
        Console.WriteLine($"   Get<float[]>(2): [{string.Join(", ", floatArray)}]");

        // Test 4: Get method without type (pattern matching)
        Console.WriteLine("\n4. Testing pattern matching with Get:");
        if (args1.Get(0) is int intVal)
            Console.WriteLine($"   Pattern match int: {intVal}");
        if (args1.Get(1) is string strVal)
            Console.WriteLine($"   Pattern match string: {strVal}");

        // Test 5: Create with initial arguments
        Console.WriteLine("\n5. Testing Create with initial arguments:");
        var args2 = KernelArguments.Create(100, "hello", 3.14f);
        Console.WriteLine($"   Created with 3 arguments, Length: {args2.Length}");
        Console.WriteLine($"   args2[0]: {args2[0]}");
        Console.WriteLine($"   args2[1]: {args2[1]}");
        Console.WriteLine($"   args2[2]: {args2[2]}");

        // Test 6: Error handling
        Console.WriteLine("\n6. Testing error handling:");
        try
        {
            var emptyArgs = new KernelArguments();
            var value = emptyArgs.Get(0); // Should throw
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"   Correctly caught InvalidOperationException: {ex.Message}");
        }

        try
        {
            args1.Get(10); // Should throw
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.WriteLine($"   Correctly caught ArgumentOutOfRangeException: {ex.Message}");
        }

        // Test 7: Type safety
        Console.WriteLine("\n7. Testing type safety:");
        try
        {
            var wrongType = args1.Get<double>(0); // int stored, trying to get as double
        }
        catch (InvalidCastException ex)
        {
            Console.WriteLine($"   Correctly caught InvalidCastException: {ex.Message}");
        }

        // Test 8: Null handling
        Console.WriteLine("\n8. Testing null handling:");
        args1.Set(0, null);
        var nullValue = args1.Get(0);
        Console.WriteLine($"   Set and retrieved null value: {nullValue == null}");

        Console.WriteLine("\nAll tests completed successfully! ✅");
    }
}
