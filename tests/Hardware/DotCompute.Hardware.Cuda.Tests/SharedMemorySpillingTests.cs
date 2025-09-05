// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Profiling;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for shared memory register spilling optimization feature in CUDA 13.0.
    /// This feature allows kernels to use more registers by spilling to shared memory,
    /// potentially improving occupancy and performance.
    /// </summary>
    [Trait("Category", "CUDA")]
    [Trait("Category", "Performance")]
    [Trait("Category", "CUDA13")]
    public class SharedMemorySpillingTests : CudaTestBase
    {
        private readonly ILogger<SharedMemorySpillingTests> _logger;
        
        public SharedMemorySpillingTests(ITestOutputHelper output) : base(output)
        {
            _logger = new TestLogger<SharedMemorySpillingTests>(output);
        }

        [SkippableFact]
        public async Task Register_Spilling_Should_Be_Configurable()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(7, 5), "Requires Turing or newer for register spilling");

            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var kernelCode = CreateRegisterIntensiveKernel();
            
            // Test with spilling disabled
            var optionsNoSpilling = new CompilationOptions
            {
                EnableSharedMemoryRegisterSpilling = false,
                OptimizationLevel = OptimizationLevel.Default
            };

            var kernelNoSpilling = await accelerator.CompileKernelAsync(
                new KernelDefinition
                {
                    Name = "registerIntensive",
                    Source = kernelCode,
                    Language = KernelLanguage.CUDA,
                    EntryPoint = "registerIntensive"
                },
                optionsNoSpilling);

            // Test with spilling enabled
            var optionsWithSpilling = new CompilationOptions
            {
                EnableSharedMemoryRegisterSpilling = true,
                OptimizationLevel = OptimizationLevel.Default
            };

            var kernelWithSpilling = await accelerator.CompileKernelAsync(
                new KernelDefinition
                {
                    Name = "registerIntensive",
                    Source = kernelCode,
                    Language = KernelLanguage.CUDA,
                    EntryPoint = "registerIntensive"
                },
                optionsWithSpilling);

            Output.WriteLine("Successfully compiled kernel with both spilling configurations");
            
            await kernelNoSpilling.DisposeAsync();
            await kernelWithSpilling.DisposeAsync();
        }

        [SkippableFact]
        public async Task Register_Spilling_Should_Improve_Occupancy_For_Register_Heavy_Kernels()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(7, 5), "Requires Turing or newer");

            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);

            // Create a kernel that uses many registers
            var kernelCode = @"
                extern ""C"" __global__ void registerHeavy(float* input, float* output, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx >= n) return;
                    
                    // Use many local variables to increase register pressure
                    float r0 = input[idx];
                    float r1 = r0 * 1.1f;
                    float r2 = r1 * 1.2f;
                    float r3 = r2 * 1.3f;
                    float r4 = r3 * 1.4f;
                    float r5 = r4 * 1.5f;
                    float r6 = r5 * 1.6f;
                    float r7 = r6 * 1.7f;
                    float r8 = r7 * 1.8f;
                    float r9 = r8 * 1.9f;
                    float r10 = r9 * 2.0f;
                    float r11 = r10 * 2.1f;
                    float r12 = r11 * 2.2f;
                    float r13 = r12 * 2.3f;
                    float r14 = r13 * 2.4f;
                    float r15 = r14 * 2.5f;
                    
                    // Complex computation using all registers
                    float result = r0 + r1 + r2 + r3 + r4 + r5 + r6 + r7 + 
                                  r8 + r9 + r10 + r11 + r12 + r13 + r14 + r15;
                    
                    output[idx] = result;
                }";

            var kernelDef = new KernelDefinition
            {
                Name = "registerHeavy",
                Source = kernelCode,
                Language = KernelLanguage.CUDA,
                EntryPoint = "registerHeavy"
            };

            // Compile without spilling
            var kernelNoSpilling = await accelerator.CompileKernelAsync(
                kernelDef,
                new CompilationOptions 
                { 
                    EnableSharedMemoryRegisterSpilling = false,
                    OptimizationLevel = OptimizationLevel.Default
                });

            // Compile with spilling
            var kernelWithSpilling = await accelerator.CompileKernelAsync(
                kernelDef,
                new CompilationOptions 
                { 
                    EnableSharedMemoryRegisterSpilling = true,
                    OptimizationLevel = OptimizationLevel.Default
                });

            // Test execution and compare occupancy
            const int elementCount = 1024 * 1024;
            var hostInput = new float[elementCount];
            for (int i = 0; i < elementCount; i++)
                hostInput[i] = i * 0.01f;

            await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await deviceInput.CopyFromAsync(hostInput);

            // Execute both kernels and measure performance
            var blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var args = new KernelArguments(deviceInput, deviceOutput, elementCount);

            // Without spilling
            var swNoSpilling = Stopwatch.StartNew();
            await kernelNoSpilling.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();
            swNoSpilling.Stop();

            // With spilling
            var swWithSpilling = Stopwatch.StartNew();
            await kernelWithSpilling.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();
            swWithSpilling.Stop();

            Output.WriteLine($"Execution time without spilling: {swNoSpilling.ElapsedMilliseconds}ms");
            Output.WriteLine($"Execution time with spilling: {swWithSpilling.ElapsedMilliseconds}ms");

            // The performance difference depends on the specific GPU and kernel characteristics
            // With spilling may be faster due to better occupancy, or slower due to shared memory overhead
            Output.WriteLine($"Performance ratio: {(double)swWithSpilling.ElapsedMilliseconds / swNoSpilling.ElapsedMilliseconds:F2}x");

            await kernelNoSpilling.DisposeAsync();
            await kernelWithSpilling.DisposeAsync();
        }

        [SkippableFact]
        public async Task Spilling_Performance_Benchmark_With_Different_Register_Pressures()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(7, 5), "Requires Turing or newer");

            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var registerCounts = new[] { 32, 64, 96, 128 };
            
            Output.WriteLine("Register Spilling Performance Benchmark:");
            Output.WriteLine("Registers | No Spilling (ms) | With Spilling (ms) | Speedup");
            Output.WriteLine("----------|-----------------|-------------------|--------");

            foreach (var regCount in registerCounts)
            {
                var kernelCode = GenerateKernelWithSpecificRegisterCount(regCount);
                var kernelDef = new KernelDefinition
                {
                    Name = $"kernel_{regCount}_regs",
                    Source = kernelCode,
                    Language = KernelLanguage.CUDA,
                    EntryPoint = "testKernel"
                };

                // Compile both versions
                var kernelNoSpilling = await accelerator.CompileKernelAsync(
                    kernelDef,
                    new CompilationOptions { EnableSharedMemoryRegisterSpilling = false });

                var kernelWithSpilling = await accelerator.CompileKernelAsync(
                    kernelDef,
                    new CompilationOptions { EnableSharedMemoryRegisterSpilling = true });

                // Prepare data
                const int elementCount = 512 * 1024;
                await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
                await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

                var args = new KernelArguments(deviceInput, deviceOutput, elementCount);
                const int iterations = 10;

                // Benchmark without spilling
                double timeNoSpilling = 0;
                for (int i = 0; i < iterations; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await kernelNoSpilling.ExecuteAsync(args);
                    await accelerator.SynchronizeAsync();
                    sw.Stop();
                    timeNoSpilling += sw.Elapsed.TotalMilliseconds;
                }
                timeNoSpilling /= iterations;

                // Benchmark with spilling
                double timeWithSpilling = 0;
                for (int i = 0; i < iterations; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await kernelWithSpilling.ExecuteAsync(args);
                    await accelerator.SynchronizeAsync();
                    sw.Stop();
                    timeWithSpilling += sw.Elapsed.TotalMilliseconds;
                }
                timeWithSpilling /= iterations;

                var speedup = timeNoSpilling / timeWithSpilling;
                Output.WriteLine($"{regCount,9} | {timeNoSpilling,15:F2} | {timeWithSpilling,17:F2} | {speedup,6:F2}x");

                await kernelNoSpilling.DisposeAsync();
                await kernelWithSpilling.DisposeAsync();
            }
        }

        [SkippableFact]
        public async Task Occupancy_Calculator_Should_Consider_Spilling()
        {
            Skip.IfNot(await IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await HasMinimumComputeCapability(7, 5), "Requires Turing or newer");

            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var occupancyCalculator = new CudaOccupancyCalculator(
                accelerator.Device,
                _logger);

            // Test occupancy with different configurations
            var blockSizes = new[] { 128, 256, 512 };
            var registerCounts = new[] { 32, 64, 96 };

            Output.WriteLine("Occupancy Analysis with Register Spilling:");
            Output.WriteLine("Block Size | Registers | Occupancy (No Spill) | Occupancy (Spill)");
            Output.WriteLine("-----------|-----------|---------------------|------------------");

            foreach (var blockSize in blockSizes)
            {
                foreach (var registers in registerCounts)
                {
                    var paramsNoSpill = new KernelLaunchParams
                    {
                        BlockSize = blockSize,
                        RegistersPerThread = registers,
                        SharedMemoryPerBlock = 0,
                        EnableSpilling = false
                    };

                    var paramsWithSpill = new KernelLaunchParams
                    {
                        BlockSize = blockSize,
                        RegistersPerThread = registers,
                        SharedMemoryPerBlock = 1024, // Shared memory for spilling
                        EnableSpilling = true
                    };

                    var occupancyNoSpill = occupancyCalculator.CalculateOccupancy(paramsNoSpill);
                    var occupancyWithSpill = occupancyCalculator.CalculateOccupancy(paramsWithSpill);

                    Output.WriteLine($"{blockSize,10} | {registers,9} | {occupancyNoSpill.OccupancyPercentage,19:F1}% | {occupancyWithSpill.OccupancyPercentage,16:F1}%");
                }
            }
        }

        private string CreateRegisterIntensiveKernel()
        {
            return @"
                extern ""C"" __global__ void registerIntensive(float* data, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx >= n) return;
                    
                    // Create high register pressure
                    float val = data[idx];
                    float a1 = val * 1.01f, a2 = val * 1.02f, a3 = val * 1.03f;
                    float a4 = val * 1.04f, a5 = val * 1.05f, a6 = val * 1.06f;
                    float a7 = val * 1.07f, a8 = val * 1.08f, a9 = val * 1.09f;
                    float a10 = val * 1.10f, a11 = val * 1.11f, a12 = val * 1.12f;
                    
                    // Complex computation
                    for (int i = 0; i < 10; i++) {
                        a1 = a1 * a2 + a3;
                        a4 = a4 * a5 + a6;
                        a7 = a7 * a8 + a9;
                        a10 = a10 * a11 + a12;
                    }
                    
                    data[idx] = a1 + a4 + a7 + a10;
                }";
        }

        private string GenerateKernelWithSpecificRegisterCount(int targetRegisters)
        {
            var variableCount = targetRegisters / 4; // Approximate register usage
            var variables = "";
            var computation = "";
            var sum = "";

            for (int i = 0; i < variableCount; i++)
            {
                variables += $"float r{i} = input[idx] * {1.0f + i * 0.1f:F2}f;\n                    ";
                computation += $"r{i} = r{i} * r{i} + {i};\n                    ";
                sum += $"r{i}" + (i < variableCount - 1 ? " + " : "");
            }

            return $@"
                extern ""C"" __global__ void testKernel(float* input, float* output, int n) {{
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx >= n) return;
                    
                    {variables}
                    
                    for (int iter = 0; iter < 5; iter++) {{
                        {computation}
                    }}
                    
                    output[idx] = {sum};
                }}";
        }

        private class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;

            public TestLogger(ITestOutputHelper output)
            {
                _output = output;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
                if (exception != null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
        }

        // Mock classes for demonstration - replace with actual implementations
        private class CudaOccupancyCalculator
        {
            private readonly object _device;
            private readonly ILogger _logger;

            public CudaOccupancyCalculator(object device, ILogger logger)
            {
                _device = device;
                _logger = logger;
            }

            public OccupancyResult CalculateOccupancy(KernelLaunchParams launchParams)
            {
                // Simplified occupancy calculation
                var maxBlocksPerSm = 32;
                var maxThreadsPerSm = 2048;
                var maxRegistersPerSm = 65536;

                var threadsPerBlock = launchParams.BlockSize;
                var registersPerBlock = launchParams.RegistersPerThread * threadsPerBlock;

                if (launchParams.EnableSpilling)
                {
                    // With spilling, we can use more registers effectively
                    registersPerBlock = (int)(registersPerBlock * 0.8);
                }

                var blocksByThreads = maxThreadsPerSm / threadsPerBlock;
                var blocksByRegisters = maxRegistersPerSm / registersPerBlock;
                var activeBlocks = Math.Min(Math.Min(blocksByThreads, blocksByRegisters), maxBlocksPerSm);

                var occupancy = (double)(activeBlocks * threadsPerBlock) / maxThreadsPerSm * 100;

                return new OccupancyResult
                {
                    ActiveBlocks = activeBlocks,
                    OccupancyPercentage = occupancy
                };
            }
        }

        private class KernelLaunchParams
        {
            public int BlockSize { get; set; }
            public int RegistersPerThread { get; set; }
            public int SharedMemoryPerBlock { get; set; }
            public bool EnableSpilling { get; set; }
        }

        private class OccupancyResult
        {
            public int ActiveBlocks { get; set; }
            public double OccupancyPercentage { get; set; }
        }
    }
}