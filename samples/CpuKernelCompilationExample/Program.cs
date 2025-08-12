// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU;
using DotCompute.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Samples.CpuKernelCompilationExample;

/// <summary>
/// Example demonstrating CPU kernel compilation with optimization and vectorization.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IAcceleratorFactory, CpuAcceleratorFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var acceleratorFactory = serviceProvider.GetRequiredService<IAcceleratorFactory>();

        // Create CPU accelerator
        var accelerator = await acceleratorFactory.CreateAcceleratorAsync();
        logger.LogInformation("Created CPU accelerator: {Name}", accelerator.Info.Name);

        // Example 1: Simple vector addition kernel
        await RunVectorAdditionExample(accelerator, logger);
        
        // Example 2: Matrix multiplication kernel
        await RunMatrixMultiplicationExample(accelerator, logger);
        
        // Example 3: Custom math kernel with optimization levels
        await RunOptimizationLevelComparison(accelerator, logger);

        // Cleanup
        await accelerator.DisposeAsync();
    }

    static async Task RunVectorAdditionExample(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("\n=== Vector Addition Example ===");
        
        // Define kernel source
        var kernelSource = @"
            // Simple vector addition: C = A + B
            for (int i = 0; i < length; i++) {
                c[i] = a[i] + b[i];
            }";

        var kernelDefinition = new KernelDefinition
        {
            Name = "vector_add",
            Source = new TextKernelSource { Code = kernelSource, Language = "c" },
            WorkDimensions = 1,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = Memory.MemoryAccess.ReadOnly },
                new() { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = Memory.MemoryAccess.ReadOnly },
                new() { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = Memory.MemoryAccess.WriteOnly }
            },
            Metadata = new Dictionary<string, object> { ["Operation"] = "Add" }
        };

        // Compile kernel with release optimization
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release,
            EnableFastMath = true
        };

        var stopwatch = Stopwatch.StartNew();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, compilationOptions);
        stopwatch.Stop();
        
        logger.LogInformation("Kernel compiled in {Time}ms", stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Kernel ID: {Id}", compiledKernel.Id);
        logger.LogInformation("Is Valid: {IsValid}", compiledKernel.IsValid);

        // Prepare test data
        const int vectorSize = 1024 * 1024; // 1M elements
        var a = new float[vectorSize];
        var b = new float[vectorSize];
        var c = new float[vectorSize];
        
        // Initialize input data
        var random = new Random(42);
        for (int i = 0; i < vectorSize; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Allocate GPU memory
        var bufferA = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));
        var bufferB = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));
        var bufferC = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));

        // Copy data to device
        await accelerator.Memory.CopyAsync(a.AsMemory(), bufferA);
        await accelerator.Memory.CopyAsync(b.AsMemory(), bufferB);

        // Execute kernel
        var executionContext = new KernelExecutionContext
        {
            GlobalWorkSize = new long[] { vectorSize },
            Arguments = new object[] { bufferA, bufferB, bufferC }
        };

        stopwatch.Restart();
        await compiledKernel.ExecuteAsync(executionContext);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        logger.LogInformation("Kernel executed in {Time}ms", stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Throughput: {Throughput:F2} GB/s", 
            (3.0 * vectorSize * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1e9));

        // Copy results back
        await accelerator.Memory.CopyAsync(bufferC, c.AsMemory());

        // Verify results
        bool correct = true;
        for (int i = 0; i < Math.Min(10, vectorSize); i++)
        {
            var expected = a[i] + b[i];
            if (Math.Abs(c[i] - expected) > 1e-6f)
            {
                correct = false;
                logger.LogError("Mismatch at index {Index}: expected {Expected}, got {Actual}", 
                    i, expected, c[i]);
            }
        }
        
        if (correct)
            logger.LogInformation("✓ Vector addition results verified successfully!");

        // Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
        await compiledKernel.DisposeAsync();
    }

    static async Task RunMatrixMultiplicationExample(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("\n=== Matrix Multiplication Example ===");
        
        // Define kernel for matrix multiplication
        var kernelSource = @"
            // Matrix multiplication: C = A * B
            int row = workItemId[0];
            int col = workItemId[1];
            float sum = 0.0f;
            
            for (int k = 0; k < width; k++) {
                sum += a[row * width + k] * b[k * width + col];
            }
            
            c[row * width + col] = sum;";

        var kernelDefinition = new KernelDefinition
        {
            Name = "matrix_multiply",
            Source = new TextKernelSource { Code = kernelSource, Language = "c" },
            WorkDimensions = 2,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "width", Type = KernelParameterType.Scalar, ElementType = typeof(int) }
            }
        };

        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        logger.LogInformation("Matrix multiplication kernel compiled");
        
        // For brevity, actual execution is omitted
        await compiledKernel.DisposeAsync();
    }

    static async Task RunOptimizationLevelComparison(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("\n=== Optimization Level Comparison ===");
        
        var kernelDefinition = new KernelDefinition
        {
            Name = "complex_math",
            Source = new TextKernelSource 
            { 
                Code = "c[i] = sqrt(a[i] * a[i] + b[i] * b[i]);", 
                Language = "c" 
            },
            WorkDimensions = 1,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float) }
            }
        };

        var optimizationLevels = new[]
        {
            (OptimizationLevel.None, "None"),
            (OptimizationLevel.Debug, "Debug"),
            (OptimizationLevel.Release, "Release"),
            (OptimizationLevel.Maximum, "Maximum")
        };

        foreach (var (level, name) in optimizationLevels)
        {
            var options = new CompilationOptions 
            { 
                OptimizationLevel = level,
                EnableFastMath = level >= OptimizationLevel.Release
            };
            
            var stopwatch = Stopwatch.StartNew();
            var kernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
            stopwatch.Stop();
            
            logger.LogInformation("Optimization {Level}: Compiled in {Time}ms", 
                name, stopwatch.ElapsedMilliseconds);
            
            // Check optimization metadata
            if (kernel.Definition.Metadata?.TryGetValue("OptimizationNotes", out var notes) == true && 
                notes is string[] optimizationNotes)
            {
                foreach (var note in optimizationNotes)
                {
                    logger.LogInformation("  - {Note}", note);
                }
            }
            
            await kernel.DisposeAsync();
        }
    }
}