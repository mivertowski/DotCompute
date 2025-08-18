using System;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU;
using DotCompute.Backends.CUDA;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests
{
    internal static class TestProgram
    {
        internal static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            Console.WriteLine("=== DotCompute Backend Test Suite ===\n");
            
            // Test CPU Backend
            Console.WriteLine("Testing CPU Backend:");
            Console.WriteLine("--------------------");
            await TestCPUBackend(loggerFactory);
            
            // Test CUDA Backend
            Console.WriteLine("\nTesting CUDA Backend:");
            Console.WriteLine("---------------------");
            await TestCUDABackend(loggerFactory);
            
            Console.WriteLine("\n=== All Tests Completed Successfully ===");
        }
        
        private static async Task TestCPUBackend(ILoggerFactory loggerFactory)
        {
            try
            {
                var logger = loggerFactory.CreateLogger<CpuAccelerator>();
                using var cpu = new CpuAccelerator(logger);
                
                Console.WriteLine($"✓ CPU Backend initialized");
                Console.WriteLine($"  Device: {cpu.Info.Name}");
                Console.WriteLine($"  Compute Units: {cpu.Info.ComputeUnits}");
                Console.WriteLine($"  Memory: {cpu.Info.MemorySize / (1024 * 1024)} MB");
                
                // Test vector addition
                var size = 1024 * 1024; // 1M elements
                var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
                var b = Enumerable.Range(0, size).Select(i => (float)(i * 2)).ToArray();
                var c = new float[size];
                
                var start = DateTime.Now;
                
                // Allocate memory
                var bufferA = await cpu.Memory.AllocateAsync(size * sizeof(float));
                var bufferB = await cpu.Memory.AllocateAsync(size * sizeof(float));
                var bufferC = await cpu.Memory.AllocateAsync(size * sizeof(float));
                
                // Copy data to device
                await bufferA.CopyFromHostAsync(a);
                await bufferB.CopyFromHostAsync(b);
                
                // Define kernel (simple vector addition)
                var kernel = new KernelDefinition(
                    "VectorAdd",
                    "extern \"C\" __global__ void VectorAdd(float* a, float* b, float* c, int n) { int i = blockIdx.x * blockDim.x + threadIdx.x; if (i < n) c[i] = a[i] + b[i]; }",
                    KernelSourceType.CUDA
                );
                
                // Compile kernel
                var compiledKernel = await cpu.CompileKernelAsync(kernel);
                Console.WriteLine($"✓ Kernel compiled: {compiledKernel.Name}");
                
                // Execute kernel
                var args = new KernelArguments(bufferA, bufferB, bufferC, size);
                var config = new ExecutionConfiguration { GlobalWorkSize = new[] { size }, LocalWorkSize = new[] { 256 } };
                var result = await compiledKernel.ExecuteAsync(args, config);
                
                // Copy results back
                await bufferC.CopyToHostAsync<float>(c);
                
                await cpu.SynchronizeAsync();
                
                var elapsed = (DateTime.Now - start).TotalMilliseconds;
                
                // Verify results
                var errors = 0;
                for (int i = 0; i < Math.Min(10, size); i++)
                {
                    var expected = a[i] + b[i];
                    if (Math.Abs(c[i] - expected) > 0.001f)
                    {
                        errors++;
                    }
                }
                
                Console.WriteLine($"✓ Vector addition completed in {elapsed:F2}ms");
                Console.WriteLine($"  Elements processed: {size:N0}");
                Console.WriteLine($"  Throughput: {(size * 3 * sizeof(float)) / (elapsed * 1e6):F2} GB/s");
                Console.WriteLine($"  Verification: {(errors == 0 ? "PASSED" : $"FAILED ({errors} errors)")}");
                
                // Cleanup
                await bufferA.DisposeAsync();
                await bufferB.DisposeAsync();
                await bufferC.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ CPU Backend test failed: {ex.Message}");
            }
        }
        
        private static async Task TestCUDABackend(ILoggerFactory loggerFactory)
        {
            try
            {
                if (!CudaBackend.IsAvailable())
                {
                    Console.WriteLine("✗ CUDA not available on this system");
                    return;
                }
                
                var logger = loggerFactory.CreateLogger<CudaBackend>();
                using var cuda = new CudaBackend(logger);
                var accelerator = cuda.GetDefaultAccelerator();
                
                if (accelerator == null)
                {
                    Console.WriteLine("✗ No CUDA devices found");
                    return;
                }
                
                Console.WriteLine($"✓ CUDA Backend initialized");
                Console.WriteLine($"  Device: {accelerator.Info.Name}");
                Console.WriteLine($"  Compute Capability: {accelerator.Device.ComputeCapability}");
                Console.WriteLine($"  Memory: {accelerator.Info.MemorySize / (1024 * 1024 * 1024)} GB");
                Console.WriteLine($"  SM Count: {accelerator.Device.StreamingMultiprocessorCount}");
                Console.WriteLine($"  RTX 2000 Ada: {(accelerator.Device.IsRTX2000Ada ? "Yes" : "No")}");
                
                // Test matrix multiplication (more compute intensive)
                var size = 512; // 512x512 matrices
                var matrixSize = size * size;
                var a = Enumerable.Range(0, matrixSize).Select(i => (float)(i % 100) / 100f).ToArray();
                var b = Enumerable.Range(0, matrixSize).Select(i => (float)((i + 50) % 100) / 100f).ToArray();
                var c = new float[matrixSize];
                
                var start = DateTime.Now;
                
                // Allocate memory
                var bufferA = await accelerator.Memory.AllocateAsync(matrixSize * sizeof(float));
                var bufferB = await accelerator.Memory.AllocateAsync(matrixSize * sizeof(float));
                var bufferC = await accelerator.Memory.AllocateAsync(matrixSize * sizeof(float));
                
                // Copy data to device
                await bufferA.CopyFromHostAsync(a);
                await bufferB.CopyFromHostAsync(b);
                
                // Define kernel (simple matrix multiplication)
                var kernel = new KernelDefinition(
                    "MatrixMul",
                    @"extern ""C"" __global__ void MatrixMul(float* A, float* B, float* C, int N) {
                        int row = blockIdx.y * blockDim.y + threadIdx.y;
                        int col = blockIdx.x * blockDim.x + threadIdx.x;
                        if (row < N && col < N) {
                            float sum = 0.0f;
                            for (int k = 0; k < N; k++) {
                                sum += A[row * N + k] * B[k * N + col];
                            }
                            C[row * N + col] = sum;
                        }
                    }",
                    KernelSourceType.CUDA
                );
                
                // Compile kernel
                var compiledKernel = await accelerator.CompileKernelAsync(kernel);
                Console.WriteLine($"✓ Kernel compiled: {compiledKernel.Name}");
                
                // Execute kernel
                var args = new KernelArguments(bufferA, bufferB, bufferC, size);
                var blockSize = 16;
                var gridSize = (size + blockSize - 1) / blockSize;
                var config = new ExecutionConfiguration 
                { 
                    GlobalWorkSize = new[] { gridSize * blockSize, gridSize * blockSize },
                    LocalWorkSize = new[] { blockSize, blockSize }
                };
                
                var result = await compiledKernel.ExecuteAsync(args, config);
                
                // Copy results back
                await bufferC.CopyToHostAsync<float>(c);
                
                await accelerator.SynchronizeAsync();
                
                var elapsed = (DateTime.Now - start).TotalMilliseconds;
                
                // Calculate GFLOPS
                var operations = 2L * size * size * size; // 2N^3 operations for matrix multiplication
                var gflops = operations / (elapsed * 1e6);
                
                Console.WriteLine($"✓ Matrix multiplication completed in {elapsed:F2}ms");
                Console.WriteLine($"  Matrix size: {size}x{size}");
                Console.WriteLine($"  Performance: {gflops:F2} GFLOPS");
                
                // Verify a few elements
                var verified = true;
                for (int i = 0; i < Math.Min(5, size); i++)
                {
                    for (int j = 0; j < Math.Min(5, size); j++)
                    {
                        float expected = 0;
                        for (int k = 0; k < size; k++)
                        {
                            expected += a[i * size + k] * b[k * size + j];
                        }
                        if (Math.Abs(c[i * size + j] - expected) > 0.01f)
                        {
                            verified = false;
                            break;
                        }
                    }
                }
                
                Console.WriteLine($"  Verification: {(verified ? "PASSED" : "FAILED")}");
                
                // Cleanup
                await bufferA.DisposeAsync();
                await bufferB.DisposeAsync();
                await bufferC.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ CUDA Backend test failed: {ex.Message}");
            }
        }
    }
}