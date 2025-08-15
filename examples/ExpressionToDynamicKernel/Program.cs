// Copyright (c) 2025 Michael Ivertowski  
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Linq;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example demonstrating dynamic kernel generation from LINQ expression trees.
/// This shows how expressions are analyzed, optimized, fused, and compiled into GPU kernels.
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
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
        // Add minimal async operation to satisfy compiler
        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine("1. Expression Fusion Demonstration");
        Console.WriteLine("===================================");

        var optimizer = new ExpressionOptimizer(loggerFactory.CreateLogger<ExpressionOptimizer>());
        var options = new DotCompute.Linq.Compilation.CompilationOptions { EnableOperatorFusion = true };

        // Create sample data
        var numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsQueryable();

        try
        {
            // Create a compute queryable
            var computeQuery = numbers.AsComputeQueryable(accelerator);

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
        // Add minimal async operation to satisfy compiler
        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);

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
        // Add minimal async operation to satisfy compiler
        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);

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
        // Add minimal async operation to satisfy compiler
        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);

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
            FusionMetadataStore.Instance.Clear();
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

        if (result.ValidationErrors.Count != 0)
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
internal class MockAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; } = new AcceleratorInfo(AcceleratorType.CUDA, "Demo GPU Accelerator", "1.0.0", 8L * 1024 * 1024 * 1024);

    public AcceleratorType Type => AcceleratorType.CUDA;

    public IMemoryManager Memory { get; } = new MockMemoryManager();

    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    public static bool IsDisposed => false;

    public static async ValueTask<TResult> ExecuteAsync<TResult>(Func<ValueTask<TResult>> operation, CancellationToken cancellationToken = default)
    {
        // Simulate some GPU operation delay
        await Task.Delay(10, cancellationToken);
        return await operation();
    }

    public async ValueTask<DotCompute.Abstractions.ICompiledKernel> CompileKernelAsync(
        DotCompute.Abstractions.KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate kernel compilation
        await Task.Delay(50, cancellationToken);
        return new MockCompiledKernel(definition.Name);
    }

    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        // Simulate synchronization delay
        => await Task.Delay(5, cancellationToken);

    public static void Dispose() { }
    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// Mock memory manager for demonstration.
/// </summary>
internal class MockMemoryManager : IMemoryManager
{
    public async ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate allocation time
        return new MockMemoryBuffer(sizeInBytes, options);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        await Task.Delay(2, cancellationToken); // Simulate allocation and copy time
        var buffer = new MockMemoryBuffer(source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>(), options);
        await buffer.CopyFromHostAsync(source, 0, cancellationToken);
        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is not MockMemoryBuffer mockBuffer)
        {
            throw new ArgumentException("Buffer must be a MockMemoryBuffer", nameof(buffer));
        }

        return new MockMemoryBufferView(mockBuffer, offset, length);
    }

    public async ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var sizeInBytes = count * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        return await AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        // Synchronous copy - for mock implementation, just simulate
        if (buffer is not MockMemoryBuffer mockBuffer)
        {
            throw new ArgumentException("Buffer must be a MockMemoryBuffer", nameof(buffer));
        }

        // In a real implementation, this would copy data to the device
        // For mock, we just validate the operation
        var dataSize = data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (dataSize > mockBuffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer size");
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        // Synchronous copy - for mock implementation, just simulate
        if (buffer is not MockMemoryBuffer mockBuffer)
        {
            throw new ArgumentException("Buffer must be a MockMemoryBuffer", nameof(buffer));
        }

        // In a real implementation, this would copy data from the device
        // For mock, we just validate the operation
        var dataSize = data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (dataSize > mockBuffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer size");
        }
    }

    public void Free(IMemoryBuffer buffer)
        // Synchronously free the buffer
        => buffer?.Dispose();
}

/// <summary>
/// Mock memory buffer for demonstration.
/// </summary>
internal class MockMemoryBuffer : IMemoryBuffer
{
    public MockMemoryBuffer(long size, MemoryOptions options = MemoryOptions.None)
    {
        SizeInBytes = size;
        Options = options;
    }

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => false;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
        // Simulate copy from host operation
        => default;


    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        // Simulate copy to host
        => default;

    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// Mock memory buffer view for demonstration.
/// </summary>
internal class MockMemoryBufferView : IMemoryBuffer
{
    private readonly MockMemoryBuffer _parentBuffer;
    private readonly long _offset;
    private readonly long _length;

    public MockMemoryBufferView(MockMemoryBuffer parentBuffer, long offset, long length)
    {
        _parentBuffer = parentBuffer;
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public MemoryOptions Options => _parentBuffer.Options;
    public bool IsDisposed => _parentBuffer.IsDisposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parentBuffer.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parentBuffer.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// Mock compiled kernel for demonstration.
/// </summary>
internal class MockCompiledKernel : DotCompute.Abstractions.ICompiledKernel
{
    public MockCompiledKernel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
        // Simulate kernel execution
        => await Task.Delay(20, cancellationToken);

    public ValueTask DisposeAsync() => default;
}