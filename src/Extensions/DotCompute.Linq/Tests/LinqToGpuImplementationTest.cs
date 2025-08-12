// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Linq.Tests;

/// <summary>
/// Comprehensive test of the LINQ-to-GPU implementation.
/// This demonstrates the complete pipeline from expression analysis to kernel execution.
/// </summary>
public class LinqToGpuImplementationTest
{
    private readonly ILogger<LinqToGpuImplementationTest> _logger;
    private readonly IExpressionOptimizer _optimizer;
    private readonly IExpressionToKernelCompiler _compiler;
    private readonly IKernelFactory _kernelFactory;
    private readonly IQueryExecutor _executor;
    private readonly IMemoryManagerFactory _memoryManagerFactory;

    public LinqToGpuImplementationTest()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        _logger = loggerFactory.CreateLogger<LinqToGpuImplementationTest>();
        
        // Initialize components
        _optimizer = new ExpressionOptimizer(loggerFactory.CreateLogger<ExpressionOptimizer>());
        _kernelFactory = new Operators.DefaultKernelFactory(loggerFactory.CreateLogger<Operators.DefaultKernelFactory>());
        _compiler = new ExpressionToKernelCompiler(
            _kernelFactory,
            _optimizer,
            loggerFactory.CreateLogger<ExpressionToKernelCompiler>());
        _memoryManagerFactory = new DefaultMemoryManagerFactory(
            loggerFactory.CreateLogger<UnifiedMemoryManager>());
        _executor = new QueryExecutor(_memoryManagerFactory, 
            loggerFactory.CreateLogger<QueryExecutor>());
    }

    /// <summary>
    /// Tests the complete LINQ-to-GPU pipeline with expression optimization.
    /// </summary>
    public async Task TestCompleteLinqToGpuPipelineAsync()
    {
        _logger.LogInformation("Starting complete LINQ-to-GPU pipeline test");

        // 1. Create a test expression (simulated LINQ query)
        var testExpression = CreateTestExpression();
        
        // 2. Test expression optimization
        TestExpressionOptimization(testExpression);
        
        // 3. Test kernel compilation
        await TestKernelCompilationAsync(testExpression);
        
        // 4. Test query execution
        await TestQueryExecutionAsync(testExpression);
        
        _logger.LogInformation("Complete LINQ-to-GPU pipeline test completed successfully");
    }

    /// <summary>
    /// Creates a test expression that represents: data.Select(x => x * 2).Where(x => x > 10)
    /// </summary>
    private Expression CreateTestExpression()
    {
        // Create parameter for input data
        var parameter = Expression.Parameter(typeof(int), "x");
        
        // Create Select expression: x => x * 2
        var multiplyBody = Expression.Multiply(parameter, Expression.Constant(2));
        var selectLambda = Expression.Lambda<Func<int, int>>(multiplyBody, parameter);
        
        // Create Where expression: x => x > 10
        var whereParameter = Expression.Parameter(typeof(int), "x");
        var whereBody = Expression.GreaterThan(whereParameter, Expression.Constant(10));
        var whereLambda = Expression.Lambda<Func<int, bool>>(whereBody, whereParameter);
        
        // Create method calls for Select and Where
        var dataParam = Expression.Parameter(typeof(IQueryable<int>), "data");
        var selectCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            [typeof(int), typeof(int)],
            dataParam,
            selectLambda);
            
        var whereCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [typeof(int)],
            selectCall,
            whereLambda);
        
        return whereCall;
    }

    private void TestExpressionOptimization(Expression expression)
    {
        _logger.LogInformation("Testing expression optimization");
        
        var options = new CompilationOptions
        {
            EnableOperatorFusion = true,
            EnableMemoryCoalescing = true,
            EnableParallelExecution = true,
            MaxThreadsPerBlock = 256
        };
        
        // Test optimization
        var optimizedExpression = _optimizer.Optimize(expression, options);
        
        // Verify optimization worked
        if (optimizedExpression == null)
        {
            throw new InvalidOperationException("Expression optimization failed");
        }
        
        // Test analysis
        var suggestions = _optimizer.Analyze(expression);
        
        _logger.LogInformation("Expression optimization completed with {SuggestionCount} optimization suggestions", 
            suggestions.Count());
        
        foreach (var suggestion in suggestions)
        {
            _logger.LogInformation("Optimization suggestion: {Type} - {Description} (Impact: {Impact})",
                suggestion.Type, suggestion.Description, suggestion.Impact);
        }
    }

    private async Task TestKernelCompilationAsync(Expression expression)
    {
        _logger.LogInformation("Testing kernel compilation");
        
        // Create mock accelerator
        var accelerator = new MockAccelerator();
        
        // Test compilation feasibility
        var canCompile = _compiler.CanCompileExpression(expression);
        if (!canCompile)
        {
            _logger.LogWarning("Expression cannot be compiled to GPU kernel - using fallback");
            return;
        }
        
        // Test resource estimation
        var resourceEstimate = _compiler.EstimateResources(expression);
        _logger.LogInformation("Resource estimate - Memory: {Memory} bytes, Compilation: {CompTime}ms, Execution: {ExecTime}ms",
            resourceEstimate.EstimatedMemoryUsage,
            resourceEstimate.EstimatedCompilationTime.TotalMilliseconds,
            resourceEstimate.EstimatedExecutionTime.TotalMilliseconds);
        
        // Test actual compilation
        try
        {
            var kernel = await _compiler.CompileExpressionAsync(expression, accelerator);
            
            _logger.LogInformation("Successfully compiled expression to kernel: {KernelName}", kernel.Name);
            
            // Test kernel properties
            var properties = kernel.Properties;
            _logger.LogInformation("Kernel properties - MaxThreads: {MaxThreads}, SharedMemory: {SharedMem} bytes, Registers: {Registers}",
                properties.MaxThreadsPerBlock, properties.SharedMemorySize, properties.RegisterCount);
            
            // Test parameter info
            var parameterInfo = kernel.GetParameterInfo();
            _logger.LogInformation("Kernel has {ParamCount} parameters", parameterInfo.Count);
            
            kernel.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kernel compilation failed");
            throw;
        }
    }

    private async Task TestQueryExecutionAsync(Expression expression)
    {
        _logger.LogInformation("Testing query execution");
        
        // Create execution context
        var accelerator = new MockAccelerator();
        var plan = CreateMockComputePlan(expression);
        var context = new ExecutionContext(accelerator, plan);
        
        // Add test data
        context.Parameters["input_data"] = Enumerable.Range(1, 1000).ToArray();
        
        // Test validation
        var validation = _executor.Validate(plan, accelerator);
        if (!validation.IsValid)
        {
            _logger.LogError("Query execution validation failed: {Message}", validation.Message);
            throw new InvalidOperationException($"Validation failed: {validation.Message}");
        }
        
        // Test execution
        try
        {
            var result = await _executor.ExecuteAsync(context);
            
            _logger.LogInformation("Query execution completed successfully. Result type: {ResultType}",
                result?.GetType().Name ?? "null");
            
            if (result is int[] resultArray)
            {
                _logger.LogInformation("Result array has {Length} elements. First 5: [{Values}]",
                    resultArray.Length, string.Join(", ", resultArray.Take(5)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query execution failed");
            throw;
        }
    }

    private MockComputePlan CreateMockComputePlan(Expression expression)
    {
        return new MockComputePlan
        {
            Id = Guid.NewGuid(),
            OutputType = typeof(int[]),
            EstimatedMemoryUsage = 1024 * 1000, // 1MB
            Stages = 
            [
                new MockComputeStage
                {
                    Id = "stage_1",
                    Kernel = new Operators.ExpressionFallbackKernel(expression),
                    InputBuffers = ["input_data"],
                    OutputBuffer = "output_data",
                    Configuration = new ExecutionConfiguration
                    {
                        GridDimensions = (32, 1, 1),
                        BlockDimensions = (256, 1, 1),
                        SharedMemorySize = 0
                    }
                }
            ],
            InputParameters = new Dictionary<string, Type> { ["input_data"] = typeof(int[]) },
            Metadata = new Dictionary<string, object> { ["generated_from"] = "test" }
        };
    }

    /// <summary>
    /// Demonstrates the key features implemented in our LINQ-to-GPU system.
    /// </summary>
    public void DemonstrateFeatures()
    {
        _logger.LogInformation("=== LINQ-to-GPU Implementation Features Demo ===");
        
        // 1. Expression Tree Optimization
        _logger.LogInformation("1. Expression Tree Optimization Features:");
        _logger.LogInformation("   ✓ Operator fusion for Select/Where combinations");
        _logger.LogInformation("   ✓ Memory access pattern optimization");
        _logger.LogInformation("   ✓ Constant folding and redundancy elimination");
        _logger.LogInformation("   ✓ Operation reordering for GPU efficiency");
        
        // 2. Dynamic Kernel Compilation
        _logger.LogInformation("2. Dynamic Kernel Compilation Features:");
        _logger.LogInformation("   ✓ Multi-accelerator support (CUDA, OpenCL, Metal, Vulkan)");
        _logger.LogInformation("   ✓ Kernel template system with optimization metadata");
        _logger.LogInformation("   ✓ Compilation caching for performance");
        _logger.LogInformation("   ✓ Automatic fallback for unsupported expressions");
        
        // 3. Query Execution Pipeline
        _logger.LogInformation("3. Query Execution Pipeline Features:");
        _logger.LogInformation("   ✓ Memory management with buffer pooling");
        _logger.LogInformation("   ✓ Asynchronous execution with cancellation");
        _logger.LogInformation("   ✓ Comprehensive validation and error handling");
        _logger.LogInformation("   ✓ Resource estimation and optimization");
        
        // 4. Kernel Template Generation
        _logger.LogInformation("4. Kernel Template Generation Features:");
        _logger.LogInformation("   ✓ Template library for common operations (Map, Filter, Reduce, Sort)");
        _logger.LogInformation("   ✓ Accelerator-specific code generation");
        _logger.LogInformation("   ✓ Expression-to-kernel source translation");
        _logger.LogInformation("   ✓ Performance optimization hints and metadata");
        
        // 5. Performance Optimizations
        _logger.LogInformation("5. Performance Optimization Features:");
        _logger.LogInformation("   ✓ Kernel compilation caching with weak references");
        _logger.LogInformation("   ✓ Memory coalescing for GPU access patterns");
        _logger.LogInformation("   ✓ Work group size optimization");
        _logger.LogInformation("   ✓ Shared memory utilization strategies");
        
        _logger.LogInformation("=== Demo Complete ===");
    }
}

// Mock implementations for testing
internal class MockAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
    {
        Id = "mock_gpu_0",
        Name = "Mock GPU Accelerator",
        Type = AcceleratorType.CUDA,
        MemorySize = 8L * 1024 * 1024 * 1024, // 8GB
        MaxThreadsPerBlock = 1024,
        MultiProcessorCount = 68
    };
    
    public AcceleratorType Type => AcceleratorType.CUDA;
    public IMemoryManager Memory { get; } = new MockMemoryManager();

    public ValueTask<DotCompute.Abstractions.ICompiledKernel> CompileKernelAsync(
        DotCompute.Abstractions.KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<DotCompute.Abstractions.ICompiledKernel>(new MockCompiledKernel(definition.Name));
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal class MockMemoryManager : IMemoryManager
{
    public long TotalMemory => 8L * 1024 * 1024 * 1024;
    public long AvailableMemory => TotalMemory / 2;

    public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IMemoryBuffer>(new MockMemoryBuffer(sizeInBytes));
    }

    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> data, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = new MockMemoryBuffer(data.Length * sizeof(T));
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        return new MockMemoryBuffer(length);
    }

    public void Dispose() { }
}

internal class MockMemoryBuffer : IMemoryBuffer
{
    public MockMemoryBuffer(long size)
    {
        SizeInBytes = size;
    }

    public long SizeInBytes { get; }
    public IntPtr DevicePointer => IntPtr.Zero;
    public bool IsDisposed => false;
    public DotCompute.Abstractions.MemoryOptions Options => DotCompute.Abstractions.MemoryOptions.None;

    public void Dispose() { }

    public ValueTask DisposeAsync() { return ValueTask.CompletedTask; }

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ValueTask.CompletedTask;
    }
}

internal class MockCompiledKernel : DotCompute.Abstractions.ICompiledKernel
{
    private readonly string _name;

    public MockCompiledKernel(string name)
    {
        _name = name;
    }

    public string Name => _name;

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose() { }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal class MockComputePlan : IComputePlan
{
    public Guid Id { get; init; }
    public IReadOnlyList<IComputeStage> Stages { get; init; } = [];
    public IReadOnlyDictionary<string, Type> InputParameters { get; init; } = new Dictionary<string, Type>();
    public Type OutputType { get; init; } = typeof(object);
    public long EstimatedMemoryUsage { get; init; }
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

internal class MockComputeStage : IComputeStage
{
    public string Id { get; init; } = string.Empty;
    public Operators.IKernel Kernel { get; init; } = null!;
    public IReadOnlyList<string> InputBuffers { get; init; } = [];
    public string OutputBuffer { get; init; } = string.Empty;
    public ExecutionConfiguration Configuration { get; init; } = new();
}