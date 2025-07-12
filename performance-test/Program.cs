using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.PerformanceValidation;

public static class PerformanceValidator
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🚀 DotCompute Performance Validation");
        Console.WriteLine("====================================");
        
        // 1. SIMD Capabilities Detection
        ValidateSimdCapabilities();
        
        // 2. Vectorization Performance Tests
        ValidateVectorizationPerformance();
        
        // 3. Memory Performance Tests
        ValidateMemoryPerformance();
        
        // 4. Threading Performance Tests
        ValidateThreadingPerformance();
        
        Console.WriteLine("\n✅ Performance validation completed!");
    }
    
    private static void ValidateSimdCapabilities()
    {
        Console.WriteLine("\n📊 SIMD Capabilities Analysis");
        Console.WriteLine("-----------------------------");
        
        Console.WriteLine($"Hardware Accelerated: {Vector.IsHardwareAccelerated}");
        Console.WriteLine($"Vector<T> Count: {Vector<float>.Count}");
        Console.WriteLine($"Vector128 Supported: {Vector128.IsHardwareAccelerated}");
        Console.WriteLine($"Vector256 Supported: {Vector256.IsHardwareAccelerated}");
        Console.WriteLine($"Vector512 Supported: {Vector512.IsHardwareAccelerated}");
        
        if (Sse.IsSupported) 
        { 
            Console.WriteLine("✅ SSE"); 
        }
        if (Sse2.IsSupported) 
        { 
            Console.WriteLine("✅ SSE2"); 
        }
        if (Sse3.IsSupported) 
        { 
            Console.WriteLine("✅ SSE3"); 
        }
        if (Ssse3.IsSupported) 
        { 
            Console.WriteLine("✅ SSSE3"); 
        }
        if (Sse41.IsSupported) 
        { 
            Console.WriteLine("✅ SSE4.1"); 
        }
        if (Sse42.IsSupported) 
        { 
            Console.WriteLine("✅ SSE4.2"); 
        }
        if (Avx.IsSupported) 
        { 
            Console.WriteLine("✅ AVX"); 
        }
        if (Avx2.IsSupported) 
        { 
            Console.WriteLine("✅ AVX2"); 
        }
        if (Avx512F.IsSupported) 
        { 
            Console.WriteLine("✅ AVX512F"); 
        }
        if (Fma.IsSupported) 
        { 
            Console.WriteLine("✅ FMA"); 
        }
        if (AdvSimd.IsSupported) 
        { 
            Console.WriteLine("✅ NEON (ARM64)"); 
        }
        
        var preferredWidth = Vector256.IsHardwareAccelerated ? 256 :
                           Vector128.IsHardwareAccelerated ? 128 : 
                           Vector.IsHardwareAccelerated ? Vector<byte>.Count * 8 : 64;
        Console.WriteLine($"Preferred Vector Width: {preferredWidth} bits");
    }
    
    private static void ValidateVectorizationPerformance()
    {
        Console.WriteLine("\n⚡ Vectorization Performance Tests");
        Console.WriteLine("----------------------------------");
        
        var sizes = new[] { 1024, 4096, 16384, 65536 };
        
        foreach (var size in sizes)
        {
            Console.WriteLine($"\nTesting array size: {size:N0}");
            
            var data1 = new float[size];
            var data2 = new float[size];
            var result = new float[size];
            
            var random = new Random(42);
            for (int i = 0; i < size; i++)
            {
                data1[i] = (float)random.NextDouble();
                data2[i] = (float)random.NextDouble();
            }
            
            // Scalar Addition
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < 1000; iter++)
            {
                for (int i = 0; i < size; i++)
                {
                    result[i] = data1[i] + data2[i];
                }
            }
            sw.Stop();
            var scalarTime = sw.ElapsedTicks;
            
            // Vector Addition
            sw.Restart();
            for (int iter = 0; iter < 1000; iter++)
            {
                var vectors = size / Vector<float>.Count;
                var remainder = size % Vector<float>.Count;
                
                for (int i = 0; i < vectors; i++)
                {
                    var offset = i * Vector<float>.Count;
                    var v1 = new Vector<float>(data1, offset);
                    var v2 = new Vector<float>(data2, offset);
                    var vResult = v1 + v2;
                    vResult.CopyTo(result, offset);
                }
                
                // Handle remainder
                for (int i = vectors * Vector<float>.Count; i < size; i++)
                {
                    result[i] = data1[i] + data2[i];
                }
            }
            sw.Stop();
            var vectorTime = sw.ElapsedTicks;
            
            var speedup = (double)scalarTime / vectorTime;
            Console.WriteLine($"  Scalar: {scalarTime:N0} ticks");
            Console.WriteLine($"  Vector: {vectorTime:N0} ticks");
            Console.WriteLine($"  Speedup: {speedup:F2}x");
            
            if (speedup >= 2.0)
            {
                Console.WriteLine($"  ✅ PASS: Achieved {speedup:F2}x speedup (target: 2x minimum)");
            }
            else
            {
                Console.WriteLine($"  ❌ FAIL: Only {speedup:F2}x speedup (target: 2x minimum)");
            }
        }
    }
    
    private static void ValidateMemoryPerformance()
    {
        Console.WriteLine("\n💾 Memory Performance Tests");
        Console.WriteLine("---------------------------");
        
        var sizes = new[] { 1024 * 1024, 4 * 1024 * 1024, 16 * 1024 * 1024 }; // 1MB, 4MB, 16MB
        
        foreach (var size in sizes)
        {
            Console.WriteLine($"\nTesting memory size: {size / 1024 / 1024}MB");
            
            var data = new byte[size];
            var random = new Random(42);
            random.NextBytes(data);
            
            // Sequential Access
            var sw = Stopwatch.StartNew();
            long sum = 0;
            for (int iter = 0; iter < 100; iter++)
            {
                for (int i = 0; i < size; i++)
                {
                    sum += data[i];
                }
            }
            sw.Stop();
            
            var bandwidth = (size * 100L * TimeSpan.TicksPerSecond) / (sw.ElapsedTicks * 1024.0 * 1024.0 * 1024.0);
            Console.WriteLine($"  Sequential Access: {bandwidth:F2} GB/s");
            
            // Random Access (cache unfriendly)
            var indices = new int[1000];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = random.Next(size);
            }
            
            sw.Restart();
            sum = 0;
            for (int iter = 0; iter < 1000; iter++)
            {
                foreach (var index in indices)
                {
                    sum += data[index];
                }
            }
            sw.Stop();
            
            var randomAccessTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"  Random Access: {randomAccessTime}ms for 1M accesses");
            Console.WriteLine($"  Cache Line Efficiency: {(randomAccessTime < 50 ? "Good" : "Poor")}");
        }
    }
    
    private static void ValidateThreadingPerformance()
    {
        Console.WriteLine("\n🧵 Threading Performance Tests");
        Console.WriteLine("------------------------------");
        
        var workItems = 10000;
        var processorCount = Environment.ProcessorCount;
        
        Console.WriteLine($"Processor Count: {processorCount}");
        Console.WriteLine($"Work Items: {workItems:N0}");
        
        // Sequential Processing
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < workItems; i++)
        {
            DoWork(i);
        }
        sw.Stop();
        var sequentialTime = sw.ElapsedMilliseconds;
        
        // Parallel Processing
        sw.Restart();
        Parallel.For(0, workItems, DoWork);
        sw.Stop();
        var parallelTime = sw.ElapsedMilliseconds;
        
        var threadingSpeedup = (double)sequentialTime / parallelTime;
        Console.WriteLine($"  Sequential: {sequentialTime}ms");
        Console.WriteLine($"  Parallel: {parallelTime}ms");
        Console.WriteLine($"  Threading Speedup: {threadingSpeedup:F2}x");
        Console.WriteLine($"  Efficiency: {(threadingSpeedup / processorCount):P1}");
        
        if (threadingSpeedup >= processorCount * 0.7)
        {
            Console.WriteLine($"  ✅ PASS: Good parallel efficiency");
        }
        else
        {
            Console.WriteLine($"  ⚠️ WARNING: Low parallel efficiency");
        }
        
        static void DoWork(int item)
        {
            // Simulate CPU-bound work
            var sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                sum += i * item;
            }
        }
    }
}