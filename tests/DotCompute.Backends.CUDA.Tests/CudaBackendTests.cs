// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DotCompute.Core.Abstractions.Compilation;
using DotCompute.Core.Abstractions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace DotCompute.Backends.CUDA.Tests;

/// <summary>
/// Simple test program for CUDA backend verification
/// </summary>
public class CudaBackendTests
{
    private static ILogger<CudaBackendTests> CreateLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        return loggerFactory.CreateLogger<CudaBackendTests>();
    }

    public static void Main(string[] args)
    {
        var logger = CreateLogger();
        logger.LogInformation("=== DotCompute CUDA Backend Test Suite ===");

        try
        {
            TestBackendFactory(logger);
            TestMemoryOperations(logger);
            TestKernelCompilation(logger);
            TestKernelExecution(logger);
            
            logger.LogInformation("=== All tests completed successfully! ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test suite failed");
            Environment.Exit(1);
        }
    }

    private static void TestBackendFactory(ILogger logger)
    {
        logger.LogInformation("Testing CUDA Backend Factory...");

        var factory = new CudaBackendFactory();
        
        logger.LogInformation("Backend: {Name} - {Description}", factory.Name, factory.Description);
        logger.LogInformation("Version: {Version}", factory.Version);
        logger.LogInformation("Available: {Available}", factory.IsAvailable());

        if (!factory.IsAvailable())
        {
            logger.LogWarning("CUDA is not available on this system. Skipping remaining tests.");
            return;
        }

        var capabilities = factory.GetCapabilities();
        logger.LogInformation("Capabilities: MaxDevices={MaxDevices}, UnifiedMemory={UnifiedMemory}, AsyncExecution={Async}",
            capabilities.MaxDevices, capabilities.SupportsUnifiedMemory, capabilities.SupportsAsyncExecution);

        var accelerators = factory.CreateAccelerators().ToList();
        logger.LogInformation("Found {Count} CUDA accelerator(s)", accelerators.Count);

        foreach (var accelerator in accelerators)
        {
            logger.LogInformation("Accelerator: {Name} ({Type})", accelerator.Name, accelerator.Type);
            
            var caps = accelerator.GetCapabilities().ToList();
            logger.LogInformation("  Total Global Memory: {Memory:N0} bytes", 
                caps.FirstOrDefault(c => c.Name == "TotalGlobalMemory")?.Value ?? 0);
            logger.LogInformation("  Compute Capability: {Major}.{Minor}",
                caps.FirstOrDefault(c => c.Name == "ComputeCapabilityMajor")?.Value ?? 0,
                caps.FirstOrDefault(c => c.Name == "ComputeCapabilityMinor")?.Value ?? 0);
            
            accelerator.Dispose();
        }
    }

    private static void TestMemoryOperations(ILogger logger)
    {
        logger.LogInformation("\nTesting CUDA Memory Operations...");

        var factory = new CudaBackendFactory();
        if (!factory.IsAvailable()) return;

        using var accelerator = factory.CreateDefaultAccelerator();
        if (accelerator == null) return;

        var memoryManager = accelerator.MemoryManager;

        // Test allocation
        const int size = 1024 * 1024; // 1MB
        using var buffer = memoryManager.Allocate(size);
        logger.LogInformation("Allocated buffer: {Size:N0} bytes", buffer.SizeInBytes);

        // Test memory statistics
        var stats = memoryManager.GetStatistics();
        logger.LogInformation("Memory Stats: Total={Total:N0}, Used={Used:N0}, Free={Free:N0}",
            stats.TotalMemory, stats.UsedMemory, stats.FreeMemory);

        // Test fill operation
        memoryManager.Zero(buffer);
        logger.LogInformation("Zeroed buffer successfully");

        // Test host<->device transfers
        var hostData = new float[256];
        for (int i = 0; i < hostData.Length; i++)
        {
            hostData[i] = i * 0.5f;
        }

        unsafe
        {
            fixed (float* ptr = hostData)
            {
                memoryManager.CopyFromHost(ptr, buffer, hostData.Length * sizeof(float));
            }
        }
        logger.LogInformation("Copied data from host to device");

        var hostResult = new float[256];
        unsafe
        {
            fixed (float* ptr = hostResult)
            {
                memoryManager.CopyToHost(buffer, ptr, hostResult.Length * sizeof(float));
            }
        }

        // Verify data
        bool dataValid = true;
        for (int i = 0; i < hostData.Length; i++)
        {
            if (Math.Abs(hostData[i] - hostResult[i]) > 0.0001f)
            {
                dataValid = false;
                break;
            }
        }
        logger.LogInformation("Data validation: {Result}", dataValid ? "PASSED" : "FAILED");
    }

    private static void TestKernelCompilation(ILogger logger)
    {
        logger.LogInformation("\nTesting CUDA Kernel Compilation...");

        var factory = new CudaBackendFactory();
        if (!factory.IsAvailable()) return;

        using var accelerator = factory.CreateDefaultAccelerator();
        if (accelerator == null) return;

        var compiler = accelerator.KernelCompiler;

        // Simple vector addition kernel
        var kernelSource = new SimpleKernelSource
        {
            Name = "vector_add",
            EntryPoint = "vector_add",
            Language = KernelLanguage.Cuda,
            Code = @"
extern ""C"" __global__ void vector_add(float* a, float* b, float* c, int n) {
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}"
        };

        try
        {
            var compiledKernel = compiler.CompileAsync(kernelSource).Result;
            logger.LogInformation("Successfully compiled kernel: {Name}", compiledKernel.Name);
            
            var binary = compiledKernel.GetBinary();
            logger.LogInformation("PTX size: {Size} bytes", binary.Length);
            
            compiledKernel.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kernel compilation failed");
        }
    }

    private static void TestKernelExecution(ILogger logger)
    {
        logger.LogInformation("\nTesting CUDA Kernel Execution...");

        var factory = new CudaBackendFactory();
        if (!factory.IsAvailable()) return;

        using var accelerator = factory.CreateDefaultAccelerator();
        if (accelerator == null) return;

        var memoryManager = accelerator.MemoryManager;
        var compiler = accelerator.KernelCompiler;

        // Vector addition kernel
        var kernelSource = new SimpleKernelSource
        {
            Name = "vector_add_test",
            EntryPoint = "vector_add",
            Language = KernelLanguage.Cuda,
            Code = @"
extern ""C"" __global__ void vector_add(float* a, float* b, float* c, int n) {
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}"
        };

        try
        {
            const int vectorSize = 1024;
            var dataSize = vectorSize * sizeof(float);

            // Allocate device memory
            using var bufferA = memoryManager.Allocate(dataSize);
            using var bufferB = memoryManager.Allocate(dataSize);
            using var bufferC = memoryManager.Allocate(dataSize);

            // Initialize host data
            var hostA = new float[vectorSize];
            var hostB = new float[vectorSize];
            for (int i = 0; i < vectorSize; i++)
            {
                hostA[i] = i;
                hostB[i] = i * 2;
            }

            // Copy to device
            unsafe
            {
                fixed (float* ptrA = hostA, ptrB = hostB)
                {
                    memoryManager.CopyFromHost(ptrA, bufferA, dataSize);
                    memoryManager.CopyFromHost(ptrB, bufferB, dataSize);
                }
            }

            // Compile kernel
            using var compiledKernel = compiler.CompileAsync(kernelSource).Result;

            // Create execution configuration
            var config = new ExecutionConfiguration
            {
                GlobalWorkSize = new[] { vectorSize },
                LocalWorkSize = new[] { 256 }
            };

            // Execute kernel
            var stopwatch = Stopwatch.StartNew();
            
            using (var execution = compiledKernel.CreateExecution(config))
            {
                execution.SetArgument(0, bufferA)
                         .SetArgument(1, bufferB)
                         .SetArgument(2, bufferC)
                         .SetArgument(3, vectorSize)
                         .Execute();
            }

            accelerator.Synchronize();
            stopwatch.Stop();

            logger.LogInformation("Kernel execution time: {Time:F3} ms", stopwatch.Elapsed.TotalMilliseconds);

            // Copy result back
            var hostC = new float[vectorSize];
            unsafe
            {
                fixed (float* ptrC = hostC)
                {
                    memoryManager.CopyToHost(bufferC, ptrC, dataSize);
                }
            }

            // Verify results
            bool resultsValid = true;
            for (int i = 0; i < vectorSize; i++)
            {
                var expected = hostA[i] + hostB[i];
                if (Math.Abs(hostC[i] - expected) > 0.0001f)
                {
                    logger.LogError("Mismatch at index {Index}: expected {Expected}, got {Actual}",
                        i, expected, hostC[i]);
                    resultsValid = false;
                    break;
                }
            }

            logger.LogInformation("Kernel execution validation: {Result}", 
                resultsValid ? "PASSED" : "FAILED");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kernel execution test failed");
        }
    }

    // Helper class for kernel source
    private class SimpleKernelSource : IKernelSource
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public KernelLanguage Language { get; set; }
        public string EntryPoint { get; set; } = "";
        public string[] Dependencies { get; set; } = Array.Empty<string>();
    }
}