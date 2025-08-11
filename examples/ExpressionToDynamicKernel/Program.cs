// Copyright (c) 2025 Michael Ivertowski  
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Linq;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Expressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example demonstrating dynamic kernel generation from LINQ expression trees.
/// This shows how expressions are analyzed, optimized, fused, and compiled into GPU kernels.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        var logger = loggerFactory.CreateLogger<Program>();
        
        Console.WriteLine("=== DotCompute Dynamic Kernel Generation Demo ===\n");

        // Create a mock accelerator for demonstration
        var accelerator = new MockAccelerator();
        
        // Demonstrate different aspects of the dynamic kernel system
        await DemonstrateExpressionFusion(accelerator, loggerFactory, logger);
        await DemonstrateTypeInference(accelerator, loggerFactory, logger);
        await DemonstrateResourceEstimation(accelerator, loggerFactory, logger);
        await DemonstrateDynamicCompilation(accelerator, loggerFactory, logger);
        
        Console.WriteLine("\n=== Demo Complete ===");
    }

    private static async Task DemonstrateExpressionFusion(IAccelerator accelerator, ILoggerFactory loggerFactory, ILogger logger)
    {
        Console.WriteLine("1. Expression Fusion Demonstration");
        Console.WriteLine("===================================");

        var optimizer = new ExpressionOptimizer(loggerFactory.CreateLogger<ExpressionOptimizer>());
        var options = new CompilationOptions { EnableOperatorFusion = true };

        // Create sample data
        var numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsQueryable();

        try
        {
            // Create a compute queryable
            var computeQuery = numbers.AsComputeQueryable(accelerator, new ComputeQueryOptions
            {
                LoggerFactory = loggerFactory
            });

            // Chain operations that can be fused
            var result = computeQuery
                .Where(x => x > 2)           // Filter operation
                .Select(x => x * 2)          // Map operation
                .Where(x => x < 20)          // Another filter
                .Select(x => x + 1);         // Another map

            Console.WriteLine($"Original query: numbers.Where(x => x > 2).Select(x => x * 2).Where(x => x < 20).Select(x => x + 1)");
            
            // Get optimization suggestions
            var suggestions = result.GetOptimizationSuggestions();
            Console.WriteLine($"Optimization suggestions: {suggestions.Count()}");
            
            foreach (var suggestion in suggestions)
            {
                Console.WriteLine($"  - {suggestion.Type}: {suggestion.Description}");
            }

            // Execute the query (this would trigger kernel generation and fusion)
            var finalResult = result.ToComputeArray();
            Console.WriteLine($"Result: [{string.Join(", ", finalResult)}]");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in expression fusion demo");
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateTypeInference(IAccelerator accelerator, ILoggerFactory loggerFactory, ILogger logger)
    {
        Console.WriteLine("2. Type Inference and Validation");
        Console.WriteLine("=================================");

        var typeEngine = new TypeInferenceEngine(loggerFactory.CreateLogger<TypeInferenceEngine>());

        try
        {
            // Create expressions with different types
            var intData = new[] { 1, 2, 3 }.AsQueryable();
            var floatData = new[] { 1.0f, 2.0f, 3.0f }.AsQueryable();
            var doubleData = new[] { 1.0, 2.0, 3.0 }.AsQueryable();

            var computeIntQuery = intData.AsComputeQueryable(accelerator);
            var computeFloatQuery = floatData.AsComputeQueryable(accelerator);
            var computeDoubleQuery = doubleData.AsComputeQueryable(accelerator);

            // Test type inference for different operations
            var intExpression = computeIntQuery.Select(x => x * 2).Expression;
            var floatExpression = computeFloatQuery.Select(x => x * 2.5f).Expression;
            var doubleExpression = computeDoubleQuery.Select(x => x * 2.5).Expression;

            // Analyze types
            var intTypeResult = typeEngine.InferTypes(intExpression);
            var floatTypeResult = typeEngine.InferTypes(floatExpression);
            var doubleTypeResult = typeEngine.InferTypes(doubleExpression);

            Console.WriteLine("Type Analysis Results:");
            
            PrintTypeAnalysis("Integer operations", intTypeResult);
            PrintTypeAnalysis("Float operations", floatTypeResult);
            PrintTypeAnalysis("Double operations", doubleTypeResult);

            // Get optimization suggestions
            var intOptimizations = typeEngine.SuggestOptimizations(intExpression);
            var doubleOptimizations = typeEngine.SuggestOptimizations(doubleExpression);

            Console.WriteLine("\nType Optimization Suggestions:");
            Console.WriteLine($"Int operations: {intOptimizations.Count()} suggestions");
            Console.WriteLine($"Double operations: {doubleOptimizations.Count()} suggestions");

            foreach (var opt in doubleOptimizations)
            {
                Console.WriteLine($"  - {opt.Type}: {opt.Description}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in type inference demo");
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateResourceEstimation(IAccelerator accelerator, ILoggerFactory loggerFactory, ILogger logger)
    {
        Console.WriteLine("3. Resource Estimation");
        Console.WriteLine("======================");

        try
        {
            var factory = new DefaultKernelFactory(loggerFactory.CreateLogger<DefaultKernelFactory>());
            var optimizer = new ExpressionOptimizer(loggerFactory.CreateLogger<ExpressionOptimizer>());
            var compiler = new ExpressionToKernelCompiler(factory, optimizer, 
                loggerFactory.CreateLogger<ExpressionToKernelCompiler>());

            // Create queries of different complexity
            var data = Enumerable.Range(1, 1000).AsQueryable();
            var computeData = data.AsComputeQueryable(accelerator);

            // Simple operation
            var simpleQuery = computeData.Select(x => x * 2);
            var simpleEstimate = compiler.EstimateResources(simpleQuery.Expression);

            // Complex operation chain
            var complexQuery = computeData
                .Where(x => x % 2 == 0)
                .Select(x => x * 3)
                .Where(x => x > 100)
                .Select(x => Math.Sqrt(x))
                .Where(x => x < 50);
            var complexEstimate = compiler.EstimateResources(complexQuery.Expression);

            Console.WriteLine("Resource Estimation Results:");
            Console.WriteLine("-----------------------------");
            
            PrintResourceEstimate("Simple Query (x => x * 2)", simpleEstimate);
            PrintResourceEstimate("Complex Query (5 operations)", complexEstimate);

            Console.WriteLine($"\nComplexity Comparison:");
            Console.WriteLine($"  Simple:  {simpleEstimate.ComplexityScore} complexity points");
            Console.WriteLine($"  Complex: {complexEstimate.ComplexityScore} complexity points");
            Console.WriteLine($"  Ratio:   {(double)complexEstimate.ComplexityScore / simpleEstimate.ComplexityScore:F2}x more complex");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in resource estimation demo");
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateDynamicCompilation(IAccelerator accelerator, ILoggerFactory loggerFactory, ILogger logger)
    {
        Console.WriteLine("4. Dynamic Kernel Compilation");
        Console.WriteLine("=============================");

        try
        {
            // Create GPU LINQ provider with dynamic compilation
            var gpuProvider = new GPULINQProvider(accelerator, loggerFactory.CreateLogger<GPULINQProvider>());

            // Create sample data and queries
            var numbers = Enumerable.Range(1, 100).ToArray();
            var queryable = new GPUQueryable<int>(gpuProvider, 
                System.Linq.Expressions.Expression.Constant(numbers.AsQueryable()));

            // Test different types of operations
            await TestOperation("Simple Map", async () =>
            {
                var result = await gpuProvider.ExecuteOnGPUAsync(
                    queryable.Select(x => x * 2).Expression, CancellationToken.None);
                return result;
            });

            await TestOperation("Filter + Map", async () =>
            {
                var result = await gpuProvider.ExecuteOnGPUAsync(
                    queryable.Where(x => x > 50).Select(x => x * 3).Expression, CancellationToken.None);
                return result;
            });

            await TestOperation("Reduction", async () =>
            {
                var result = await gpuProvider.ExecuteOnGPUAsync(
                    queryable.Where(x => x % 2 == 0).Select(x => x * x).Expression, CancellationToken.None);
                return result;
            });

            Console.WriteLine("\nKernel compilation completed successfully!");
            Console.WriteLine("In a real implementation, these would generate and execute actual GPU kernels.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in dynamic compilation demo");
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            // Clean up fusion metadata
            FusionMetadataStore.Clear();
        }

        Console.WriteLine();
    }

    private static void PrintTypeAnalysis(string title, TypeInferenceResult result)
    {
        Console.WriteLine($"\n{title}:");
        Console.WriteLine($"  Inferred types: {result.InferredTypes.Count}");
        Console.WriteLine($"  Type conversions: {result.TypeConversions.Count}");
        Console.WriteLine($"  Validation errors: {result.ValidationErrors.Count}");

        foreach (var type in result.InferredTypes.Keys.Take(3))
        {
            Console.WriteLine($"    - {type.Name}: {result.InferredTypes[type].UsageContexts.Count} usages");
        }

        if (result.ValidationErrors.Any())
        {
            Console.WriteLine($"  Errors:");
            foreach (var error in result.ValidationErrors.Take(2))
            {
                Console.WriteLine($"    - {error.Severity}: {error.Message}");
            }
        }
    }

    private static void PrintResourceEstimate(string operation, ExpressionResourceEstimate estimate)
    {
        Console.WriteLine($"\n{operation}:");
        Console.WriteLine($"  Memory Usage:       {estimate.EstimatedMemoryUsage:N0} bytes");
        Console.WriteLine($"  Compilation Time:   {estimate.EstimatedCompilationTime.TotalMilliseconds:F1} ms");
        Console.WriteLine($"  Execution Time:     {estimate.EstimatedExecutionTime.TotalMicroseconds:F1} μs");
        Console.WriteLine($"  Complexity Score:   {estimate.ComplexityScore}");
        Console.WriteLine($"  Parallelization:    {estimate.ParallelizationFactor:P1}");
    }

    private static async Task TestOperation(string name, Func<Task<object?>> operation)
    {
        Console.WriteLine($"\nTesting: {name}");
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await operation();
            stopwatch.Stop();
            
            Console.WriteLine($"  Status: Success");
            Console.WriteLine($"  Time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"  Result Type: {result?.GetType().Name ?? "null"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Status: Failed ({ex.GetType().Name}: {ex.Message})");
        }
    }
}

/// <summary>
/// Mock accelerator for demonstration purposes.
/// </summary>
public class MockAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
    {
        Name = "Demo GPU Accelerator",
        Type = AcceleratorType.CUDA,
        MaxWorkGroupSize = 1024,
        GlobalMemorySize = 8L * 1024 * 1024 * 1024, // 8GB
        LocalMemorySize = 48 * 1024, // 48KB
        MaxComputeUnits = 80
    };

    public AcceleratorType Type => AcceleratorType.CUDA;

    public IMemoryManager Memory { get; } = new MockMemoryManager();

    public bool IsDisposed => false;

    public async ValueTask<TResult> ExecuteAsync<TResult>(Func<ValueTask<TResult>> operation, CancellationToken cancellationToken = default)
    {
        // Simulate some GPU operation delay
        await Task.Delay(10, cancellationToken);
        return await operation();
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// Mock memory manager for demonstration.
/// </summary>
public class MockMemoryManager : IMemoryManager
{
    public async ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate allocation time
        return new MockMemoryBuffer(sizeInBytes);
    }

    public ValueTask FreeAsync(IMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        buffer.Dispose();
        return default;
    }

    public MemoryInfo GetMemoryInfo()
    {
        return new MemoryInfo
        {
            TotalMemory = 8L * 1024 * 1024 * 1024,
            AvailableMemory = 7L * 1024 * 1024 * 1024,
            UsedMemory = 1L * 1024 * 1024 * 1024
        };
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// Mock memory buffer for demonstration.
/// </summary>
public class MockMemoryBuffer : IMemoryBuffer
{
    public MockMemoryBuffer(long size)
    {
        SizeInBytes = size;
    }

    public long SizeInBytes { get; }
    public bool IsDisposed => false;

    public ValueTask WriteAsync<T>(ReadOnlyMemory<T> data, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Simulate write operation
        return default;
    }

    public ValueTask ReadAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Simulate read operation
        return default;
    }

    public ValueTask CopyToHostAsync<T>(Memory<T> hostMemory, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Simulate copy to host
        return default;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
}