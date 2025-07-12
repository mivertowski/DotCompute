using System;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Validation;

/// <summary>
/// Validates the kernel execution implementation
/// </summary>
public static class KernelExecutionValidator
{
    public static async Task Main()
    {
        Console.WriteLine("=== DotCompute Kernel Execution Validation ===");
        
        try
        {
            // Create CPU accelerator
            var accelerator = new CpuAccelerator(NullLogger<CpuAccelerator>.Instance);
            await accelerator.InitializeAsync();
            
            // Create a simple vector addition kernel
            var kernelDefinition = new KernelDefinition
            {
                Name = "vector_add",
                Source = new TextKernelSource
                {
                    Code = @"
                        kernel void vector_add(global float* a, global float* b, global float* c) {
                            int i = get_global_id(0);
                            c[i] = a[i] + b[i];
                        }",
                    Language = "OpenCL"
                },
                Parameters = new[]
                {
                    new KernelParameter
                    {
                        Name = "a",
                        Type = KernelParameterType.Buffer,
                        ElementType = typeof(float),
                        Access = MemoryAccess.ReadOnly
                    },
                    new KernelParameter
                    {
                        Name = "b",
                        Type = KernelParameterType.Buffer,
                        ElementType = typeof(float),
                        Access = MemoryAccess.ReadOnly
                    },
                    new KernelParameter
                    {
                        Name = "c",
                        Type = KernelParameterType.Buffer,
                        ElementType = typeof(float),
                        Access = MemoryAccess.WriteOnly
                    }
                },
                WorkDimensions = 1
            };
            
            // Compile the kernel
            var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
            
            // Create test data
            const int size = 1024;
            var a = new float[size];
            var b = new float[size];
            var c = new float[size];
            
            // Initialize input data
            for (int i = 0; i < size; i++)
            {
                a[i] = i;
                b[i] = i * 2;
            }
            
            // Allocate GPU buffers
            var bufferA = await accelerator.Memory.AllocateAndCopyAsync<float>(a);
            var bufferB = await accelerator.Memory.AllocateAndCopyAsync<float>(b);
            var bufferC = await accelerator.Memory.AllocateAsync(size * sizeof(float));
            
            // Execute kernel
            var context = new KernelExecutionContext
            {
                GlobalWorkSize = new long[] { size },
                Arguments = new object[] { bufferA, bufferB, bufferC }
            };
            
            await compiledKernel.ExecuteAsync(context);
            
            // Copy results back
            await bufferC.CopyToHostAsync<float>(c);
            
            // Validate results
            bool success = true;
            for (int i = 0; i < size; i++)
            {
                float expected = a[i] + b[i];
                if (Math.Abs(c[i] - expected) > 0.0001f)
                {
                    Console.WriteLine($"ERROR: Mismatch at index {i}: expected {expected}, got {c[i]}");
                    success = false;
                    break;
                }
            }
            
            if (success)
            {
                Console.WriteLine($"✓ Kernel execution successful! All {size} elements computed correctly.");
                
                // Test vectorized execution
                await TestVectorizedExecution(accelerator);
            }
            
            // Cleanup
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
            await compiledKernel.DisposeAsync();
            await accelerator.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    private static async Task TestVectorizedExecution(CpuAccelerator accelerator)
    {
        Console.WriteLine("\n=== Testing Vectorized Execution ===");
        
        // Test different sizes to verify SIMD paths
        var testSizes = new[] { 4, 8, 16, 32, 64, 128, 256, 512, 1024 };
        
        foreach (var size in testSizes)
        {
            var a = new float[size];
            var b = new float[size];
            var c = new float[size];
            
            // Initialize with random data
            var rand = new Random(42);
            for (int i = 0; i < size; i++)
            {
                a[i] = (float)rand.NextDouble() * 100;
                b[i] = (float)rand.NextDouble() * 100;
            }
            
            // Allocate buffers
            var bufferA = await accelerator.Memory.AllocateAndCopyAsync<float>(a);
            var bufferB = await accelerator.Memory.AllocateAndCopyAsync<float>(b);
            var bufferC = await accelerator.Memory.AllocateAsync(size * sizeof(float));
            
            // Create simple kernel
            var kernel = new KernelDefinition
            {
                Name = $"vector_add_{size}",
                Source = new TextKernelSource
                {
                    Code = "kernel void vector_add(global float* a, global float* b, global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
                    Language = "OpenCL"
                },
                Parameters = new[]
                {
                    new KernelParameter { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = MemoryAccess.ReadOnly },
                    new KernelParameter { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = MemoryAccess.ReadOnly },
                    new KernelParameter { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = MemoryAccess.WriteOnly }
                },
                WorkDimensions = 1
            };
            
            var compiledKernel = await accelerator.CompileKernelAsync(kernel);
            
            // Execute
            var context = new KernelExecutionContext
            {
                GlobalWorkSize = new long[] { size },
                Arguments = new object[] { bufferA, bufferB, bufferC }
            };
            
            var startTime = DateTime.UtcNow;
            await compiledKernel.ExecuteAsync(context);
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            // Copy results
            await bufferC.CopyToHostAsync<float>(c);
            
            // Validate
            bool valid = true;
            for (int i = 0; i < size; i++)
            {
                if (Math.Abs(c[i] - (a[i] + b[i])) > 0.0001f)
                {
                    valid = false;
                    break;
                }
            }
            
            Console.WriteLine($"Size: {size,4} - {(valid ? "✓ PASS" : "✗ FAIL")} - Time: {elapsed:F2}ms");
            
            // Cleanup
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
            await compiledKernel.DisposeAsync();
        }
    }
}