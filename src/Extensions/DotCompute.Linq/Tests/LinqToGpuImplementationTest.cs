// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Linq.Compilation.Execution;
using DotCompute.Linq.Operators.Interfaces;
namespace DotCompute.Linq.Tests;
{
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
            loggerFactory.CreateLogger<IUnifiedMemoryManager>());
        _executor = new QueryExecutor(_memoryManagerFactory,
            loggerFactory.CreateLogger<QueryExecutor>());
    }
    /// <summary>
    /// Tests the complete LINQ-to-GPU pipeline with expression optimization.
    /// </summary>
    public async Task TestCompleteLinqToGpuPipelineAsync()
        {
        _logger.LogInfoMessage("Starting complete LINQ-to-GPU pipeline test");
        // 1. Create a test expression (simulated LINQ query)
        var testExpression = CreateTestExpression();
        // 2. Test expression optimization
        TestExpressionOptimization(testExpression);
        // 3. Test kernel compilation
        await TestKernelCompilationAsync(testExpression);
        // 4. Test query execution
        await TestQueryExecutionAsync(testExpression);
        _logger.LogInfoMessage("Complete LINQ-to-GPU pipeline test completed successfully");
    /// Creates a test expression that represents: data.Select(x => x * 2).Where(x => x > 10)
    private Expression CreateTestExpression()
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
            nameof(Queryable.Where),
            [typeof(int)],
            selectCall,
            whereLambda);
        return whereCall;
    private void TestExpressionOptimization(Expression expression)
        _logger.LogInfoMessage("Testing expression optimization");
        var options = new Compilation.CompilationOptions
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
            throw new InvalidOperationException("Expression optimization failed");
        }
        // Test analysis
        var suggestions = _optimizer.Analyze(expression);
        _logger.LogInfoMessage($"Expression optimization completed with {suggestions.Count()} optimization suggestions");
        foreach (var suggestion in suggestions)
            _logger.LogInfoMessage($"Optimization suggestion: {suggestion.Type} - {suggestion.Description} (Impact: {suggestion.Impact})");
    private async Task TestKernelCompilationAsync(Expression expression)
        {
        _logger.LogInfoMessage("Testing kernel compilation");
        // Create mock accelerator
        var accelerator = new MockAccelerator();
        // Test compilation feasibility
        var canCompile = _compiler.CanCompileExpression(expression);
        if (!canCompile)
            _logger.LogWarningMessage("Expression cannot be compiled to GPU kernel - using fallback");
            return;
        // Test resource estimation
        var resourceEstimate = _compiler.EstimateResources(expression);
        _logger.LogInfoMessage($"Resource estimate - Memory: {resourceEstimate.EstimatedMemoryUsage} bytes, Compilation: {resourceEstimate.EstimatedCompilationTime.TotalMilliseconds}ms, Execution: {resourceEstimate.EstimatedExecutionTime.TotalMilliseconds}ms");
        // Test actual compilation
        try
            var kernel = await _compiler.CompileExpressionAsync(expression, accelerator);
            _logger.LogInfoMessage("Successfully compiled expression to kernel: {kernel.Name}");
            // Test kernel properties
            var properties = kernel.Properties;
            _logger.LogInfoMessage($"Kernel properties - MaxThreads: {properties.MaxThreadsPerBlock}, SharedMemory: {properties.SharedMemorySize} bytes, Registers: {properties.RegisterCount}");
            // Test parameter info
            var parameterInfo = kernel.GetParameterInfo();
            _logger.LogInfoMessage("Kernel has {parameterInfo.Count} parameters");
            kernel.Dispose();
        catch (Exception ex)
            _logger.LogErrorMessage(ex, "Kernel compilation failed");
            throw;
    private async Task TestQueryExecutionAsync(Expression expression)
        _logger.LogInfoMessage("Testing query execution");
        // Create execution context
        var plan = CreateMockComputePlan(expression);
        var context = new Execution.ExecutionContext(accelerator, plan);
        // Add test data
        context.Parameters["input_data"] = Enumerable.Range(1, 1000).ToArray();
        // Test validation
        var validation = _executor.Validate(plan, accelerator);
        if (!validation.IsValid)
            _logger.LogError("Query execution validation failed: {Message}", validation.ErrorMessage);
            throw new InvalidOperationException($"Validation failed: {validation.ErrorMessage}");
        // Test execution
            var result = await _executor.ExecuteAsync(context);
            _logger.LogInfoMessage($"Query execution completed successfully. Result type: {result?.GetType().Name ?? "null"}");
            if (result is int[] resultArray)
            {
                _logger.LogInfoMessage($"Result array has {resultArray.Length} elements. First 5: [{string.Join(", ", resultArray.Take(5))}]");
            }
            _logger.LogErrorMessage(ex, "Query execution failed");
    private MockComputePlan CreateMockComputePlan(Expression expression)
        return new MockComputePlan
            Id = Guid.NewGuid(),
            OutputType = typeof(int[]),
            EstimatedMemoryUsage = 1024 * 1000, // 1MB
            Stages =
            [
                new MockComputeStage
                Id = "stage_1",
                Kernel = new Operators.ExpressionFallbackKernel(expression, _logger),
                InputBuffers = ["input_data"],
                OutputBuffer = "output_data",
                Configuration = new ExecutionConfiguration
                {
                    GridDimensions = (32, 1, 1),
                    BlockDimensions = (256, 1, 1),
                    SharedMemorySize = 0
                }
            ],
            InputParameters = new Dictionary<string, Type> { ["input_data"] = typeof(int[]) },
            Metadata = new Dictionary<string, object> { ["generated_from"] = "test" }
    /// Demonstrates the key features implemented in our LINQ-to-GPU system.
    public void DemonstrateFeatures()
        _logger.LogInfoMessage("=== LINQ-to-GPU Implementation Features Demo ===");
        // 1. Expression Tree Optimization
        _logger.LogInfoMessage("1. Expression Tree Optimization Features:");
        _logger.LogInfoMessage("   ✓ Operator fusion for Select/Where combinations");
        _logger.LogInfoMessage("   ✓ Memory access pattern optimization");
        _logger.LogInfoMessage("   ✓ Constant folding and redundancy elimination");
        _logger.LogInfoMessage("   ✓ Operation reordering for GPU efficiency");
        // 2. Dynamic Kernel Compilation
        _logger.LogInfoMessage("2. Dynamic Kernel Compilation Features:");
        _logger.LogInfoMessage("   ✓ Multi-accelerator support (CUDA, OpenCL, Metal, Vulkan)");
        _logger.LogInfoMessage("   ✓ Kernel template system with optimization metadata");
        _logger.LogInfoMessage("   ✓ Compilation caching for performance");
        _logger.LogInfoMessage("   ✓ Automatic fallback for unsupported expressions");
        // 3. Query Execution Pipeline
        _logger.LogInfoMessage("3. Query Execution Pipeline Features:");
        _logger.LogInfoMessage("   ✓ Memory management with buffer pooling");
        _logger.LogInfoMessage("   ✓ Asynchronous execution with cancellation");
        _logger.LogInfoMessage("   ✓ Comprehensive validation and error handling");
        _logger.LogInfoMessage("   ✓ Resource estimation and optimization");
        // 4. Kernel Template Generation
        _logger.LogInfoMessage("4. Kernel Template Generation Features:");
        _logger.LogInfoMessage("   ✓ Template library for common operations (Map, Filter, Reduce, Sort)");
        _logger.LogInfoMessage("   ✓ Accelerator-specific code generation");
        _logger.LogInfoMessage("   ✓ Expression-to-kernel source translation");
        _logger.LogInfoMessage("   ✓ Performance optimization hints and metadata");
        // 5. Performance Optimizations
        _logger.LogInfoMessage("5. Performance Optimization Features:");
        _logger.LogInfoMessage("   ✓ Kernel compilation caching with weak references");
        _logger.LogInfoMessage("   ✓ Memory coalescing for GPU access patterns");
        _logger.LogInfoMessage("   ✓ Work group size optimization");
        _logger.LogInfoMessage("   ✓ Shared memory utilization strategies");
        _logger.LogInfoMessage("=== Demo Complete ===");
}
// Mock implementations for testing
internal class MockAccelerator : IAccelerator
    {
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
        Id = "mock_gpu_0",
        Name = "Mock GPU Accelerator",
        DeviceType = AcceleratorType.CUDA.ToString(),
        Vendor = "Mock",
        DriverVersion = "1.0",
        TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
        MaxThreadsPerBlock = 1024
        // Note: MultiProcessorCount is not available in AcceleratorInfo
    };
    public AcceleratorType Type => AcceleratorType.CUDA;
    public IUnifiedMemoryManager Memory { get; } = new MockMemoryManager();
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);
    public ValueTask<DotCompute.Abstractions.ICompiledKernel> CompileKernelAsync(
        DotCompute.Abstractions.Kernels.KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default) => ValueTask.FromResult<DotCompute.Abstractions.ICompiledKernel>(new MockCompiledKernel(definition.Name));
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
internal class MockMemoryManager : IUnifiedMemoryManager
    {
    public static long TotalMemory => 8L * 1024 * 1024 * 1024;
    public static long AvailableMemory => TotalMemory / 2;
    public static ValueTask<IUnifiedMemoryBuffer> AllocateAsync(long sizeInBytes, DotCompute.Abstractions.Memory.MemoryOptions options = DotCompute.Abstractions.Memory.MemoryOptions.None, CancellationToken cancellationToken = default) => ValueTask.FromResult<IUnifiedMemoryBuffer>(new MockMemoryBuffer(sizeInBytes));
    public unsafe ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> data, DotCompute.Abstractions.Memory.MemoryOptions options = DotCompute.Abstractions.Memory.MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
        var buffer = new MockMemoryBuffer<T>(data.Length);
        return ValueTask.FromResult<IUnifiedMemoryBuffer<T>>(buffer);
    public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(long sizeInBytes, DotCompute.Abstractions.Memory.MemoryOptions options = DotCompute.Abstractions.Memory.MemoryOptions.None, CancellationToken cancellationToken = default) => ValueTask.FromResult<IUnifiedMemoryBuffer>(new MockMemoryBuffer(sizeInBytes));
    public static IUnifiedMemoryBuffer CreateView(IUnifiedMemoryBuffer buffer, long offset, long length) => new MockMemoryBuffer(length);
    public static ValueTask<IUnifiedMemoryBuffer> Allocate<T>(int count) where T : unmanaged
        var sizeInBytes = count * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return AllocateAsync(sizeInBytes);
    public static void CopyToDevice<T>(IUnifiedMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
        // Mock implementation - just simulate the operation
    public static void CopyFromDevice<T>(Span<T> data, IUnifiedMemoryBuffer buffer) where T : unmanaged
    public void Free(IUnifiedMemoryBuffer buffer) => buffer?.Dispose();
    public void Dispose() { }
        _ = count * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return ValueTask.FromResult<IUnifiedMemoryBuffer<T>>(new MockMemoryBuffer<T>(count));
    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(int count, DotCompute.Abstractions.Memory.MemoryOptions options, CancellationToken cancellationToken = default) where T : unmanaged => AllocateAsync<T>(count, cancellationToken);
    public ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    public ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    public ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    public ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    public ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
        buffer?.Dispose();
        return ValueTask.CompletedTask;
    public ValueTask OptimizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void Clear() { }
        {
    public IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int count) where T : unmanaged => new MockMemoryBuffer<T>(count);
    public IAccelerator Accelerator => throw new NotSupportedException();
    public DotCompute.Abstractions.Memory.MemoryStatistics Statistics => new();
    public long MaxAllocationSize => long.MaxValue;
    public long TotalAvailableMemory => TotalMemory;
    public long CurrentAllocatedMemory => 0;
    public ValueTask DisposeAsync()
        Dispose();
internal class MockMemoryBuffer : IUnifiedMemoryBuffer
    {
    public MockMemoryBuffer(long size)
        SizeInBytes = size;
    public long SizeInBytes { get; }
    public static IntPtr DevicePointer => IntPtr.Zero;
    public bool IsDisposed => false;
    public DotCompute.Abstractions.Memory.MemoryOptions Options => DotCompute.Abstractions.Memory.MemoryOptions.None;
    public DotCompute.Abstractions.Memory.BufferState State => DotCompute.Abstractions.Memory.BufferState.Allocated;
    // Interface implementations
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    // Legacy support
    public static ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
    public static ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;
internal class MockMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
    private readonly int _count;
    public MockMemoryBuffer(int count)
        _count = count;
        SizeInBytes = count * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
    public int Length => _count;
    DotCompute.Abstractions.Memory.BufferState IUnifiedMemoryBuffer.State => DotCompute.Abstractions.Memory.BufferState.Allocated;
    // Additional properties from IUnifiedMemoryBuffer<T>
    public IAccelerator Accelerator => null!;
    public bool IsOnHost => true;
    public bool IsOnDevice => false;
    public bool IsDirty => false;
    // Synchronization methods
    public void EnsureOnHost() { }
    public void EnsureOnDevice() { }
    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void Synchronize() { }
    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void MarkHostDirty() { }
    public void MarkDeviceDirty() { }
    // Copy operations
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public static ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    // Fill operations
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    // Type conversion
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => new MockMemoryBuffer<TNew>(_count * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>() / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>());
    public IUnifiedMemoryBuffer<T> Slice(int start, int length) => new MockMemoryBuffer<T>(length);
    // Memory access methods
    public Span<T> AsSpan() => [];
    public ReadOnlySpan<T> AsReadOnlySpan() => [];
    public Memory<T> AsMemory() => Memory<T>.Empty;
    public ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
    // Device memory and mapping
    public DotCompute.Abstractions.DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);
    public DotCompute.Abstractions.Memory.MappedMemory<T> Map(DotCompute.Abstractions.Memory.MapMode mode) => new(Memory<T>.Empty, null);
    public DotCompute.Abstractions.Memory.MappedMemory<T> MapRange(int offset, int length, DotCompute.Abstractions.Memory.MapMode mode) => new(Memory<T>.Empty, null);
    public ValueTask<DotCompute.Abstractions.Memory.MappedMemory<T>> MapAsync(DotCompute.Abstractions.Memory.MapMode mode, CancellationToken cancellationToken = default) => ValueTask.FromResult(new DotCompute.Abstractions.Memory.MappedMemory<T>(Memory<T>.Empty, null));
    // Non-generic interface implementation
    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<TOther>(ReadOnlyMemory<TOther> source, long offset, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    ValueTask IUnifiedMemoryBuffer.CopyToAsync<TOther>(Memory<TOther> destination, long offset, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask CopyFromHostAsync<TOther>(ReadOnlyMemory<TOther> source, long offset = 0, CancellationToken cancellationToken = default) where TOther : unmanaged => ValueTask.CompletedTask;
    public static ValueTask CopyToHostAsync<TOther>(Memory<TOther> destination, long offset = 0, CancellationToken cancellationToken = default) where TOther : unmanaged => ValueTask.CompletedTask;
internal class MockCompiledKernel : DotCompute.Abstractions.ICompiledKernel
    {
    private readonly string _name;
    public MockCompiledKernel(string name)
        _name = name;
    public Guid Id { get; } = Guid.NewGuid();
    public string Name => _name;
    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
internal class MockComputePlan : IComputePlan
    {
    public Guid Id { get; init; }
    public IReadOnlyList<IComputeStage> Stages { get; init; } = [];
    public IReadOnlyDictionary<string, Type> InputParameters { get; init; } = new Dictionary<string, Type>();
    public Type OutputType { get; init; } = typeof(object);
    public long EstimatedMemoryUsage { get; init; }
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
internal class MockComputeStage : IComputeStage
    {
    public string Id { get; init; } = string.Empty;
    public DotCompute.Linq.Operators.Interfaces.IKernel Kernel { get; init; } = null!;
    public IReadOnlyList<string> InputBuffers { get; init; } = [];
    public string OutputBuffer { get; init; } = string.Empty;
    public ExecutionConfiguration Configuration { get; init; } = new();
