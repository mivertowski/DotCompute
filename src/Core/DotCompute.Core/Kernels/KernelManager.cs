// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels
{

/// <summary>
/// Manages kernel generation, compilation, caching, and execution.
/// </summary>
public sealed partial class KernelManager : IDisposable
{
    private readonly ILogger<KernelManager> _logger;
    private readonly Dictionary<AcceleratorType, IKernelGenerator> _generators;
    private readonly Dictionary<AcceleratorType, IKernelCompiler> _compilers;
    private readonly Dictionary<AcceleratorType, IKernelExecutor> _executors;
    private readonly ConcurrentDictionary<string, ManagedCompiledKernel> _kernelCache;
    private readonly SemaphoreSlim _compilationSemaphore;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelManager"/> class.
    /// </summary>
    public KernelManager(ILogger<KernelManager> logger)
    {
        _logger = logger;
        _generators = [];
        _compilers = [];
        _executors = [];
        _kernelCache = new ConcurrentDictionary<string, ManagedCompiledKernel>();
        _compilationSemaphore = new SemaphoreSlim(Environment.ProcessorCount);

        InitializeBuiltInGenerators();
    }

    /// <summary>
    /// Registers a kernel generator for a specific accelerator type.
    /// </summary>
    public void RegisterGenerator(AcceleratorType acceleratorType, IKernelGenerator generator)
    {
        _generators[acceleratorType] = generator;
        LogGeneratorRegistered(acceleratorType.ToString());
    }

    /// <summary>
    /// Registers a kernel compiler for a specific accelerator type.
    /// </summary>
    public void RegisterCompiler(AcceleratorType acceleratorType, IKernelCompiler compiler)
    {
        _compilers[acceleratorType] = compiler;
        LogCompilerRegistered(acceleratorType.ToString());
    }

    /// <summary>
    /// Registers a kernel executor for a specific accelerator.
    /// </summary>
    public void RegisterExecutor(AcceleratorType acceleratorType, IKernelExecutor executor)
    {
        _executors[acceleratorType] = executor;
        LogExecutorRegistered(acceleratorType.ToString(), executor.Accelerator.Info.Name);
    }

    /// <summary>
    /// Generates, compiles, and caches a kernel from an expression.
    /// </summary>
    public async ValueTask<ManagedCompiledKernel> GetOrCompileKernelAsync(
        Expression expression,
        IAccelerator accelerator,
        KernelGenerationContext? context = null,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Generate cache key
        var cacheKey = GenerateCacheKey(expression, accelerator.Info);
        
        // Check cache first
        if (_kernelCache.TryGetValue(cacheKey, out var cached))
        {
            LogKernelCacheHit(cacheKey);
            return cached;
        }

        // Generate and compile kernel
        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check cache after acquiring semaphore
            if (_kernelCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var compiled = await GenerateAndCompileKernelAsync(
                expression,
                accelerator,
                context,
                options,
                cancellationToken).ConfigureAwait(false);

            _kernelCache[cacheKey] = compiled;
            LogKernelCompiled(compiled.Name, accelerator.Info.DeviceType);

            return compiled;
        }
        finally
        {
            _compilationSemaphore.Release();
        }
    }

    /// <summary>
    /// Generates, compiles, and caches a kernel for a specific operation.
    /// </summary>
    public async ValueTask<ManagedCompiledKernel> GetOrCompileOperationKernelAsync(
        string operation,
        Type[] inputTypes,
        Type outputType,
        IAccelerator accelerator,
        KernelGenerationContext? context = null,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Generate cache key
        var cacheKey = GenerateOperationCacheKey(operation, inputTypes, outputType, accelerator.Info);
        
        // Check cache first
        if (_kernelCache.TryGetValue(cacheKey, out var cached))
        {
            LogKernelCacheHit(cacheKey);
            return cached;
        }

        // Generate and compile kernel
        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check cache after acquiring semaphore
            if (_kernelCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var compiled = await GenerateAndCompileOperationKernelAsync(
                operation,
                inputTypes,
                outputType,
                accelerator,
                context,
                options,
                cancellationToken).ConfigureAwait(false);

            _kernelCache[cacheKey] = compiled;
            LogKernelCompiled(compiled.Name, accelerator.Info.DeviceType);

            return compiled;
        }
        finally
        {
            _compilationSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes a kernel with the specified arguments.
    /// </summary>
    public async ValueTask<KernelExecutionResult> ExecuteKernelAsync(
        ManagedCompiledKernel kernel,
        KernelArgument[] arguments,
        IAccelerator accelerator,
        KernelExecutionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var acceleratorType = Enum.Parse<AcceleratorType>(accelerator.Info.DeviceType);
        if (!_executors.TryGetValue(acceleratorType, out var executor))
        {
            throw new NotSupportedException($"No executor registered for accelerator type {accelerator.Info.DeviceType}");
        }

        // Convert to Abstractions CompiledKernel
        var abstractionsKernel = kernel.ToCompiledKernel();
        
        // Get optimal config if not provided
        if (config == null)
        {
            var problemSize = EstimateProblemSize(arguments);
            config = executor.GetOptimalExecutionConfig(abstractionsKernel, problemSize);
        }

        LogKernelExecutionStarted(kernel.Name, accelerator.Info.Name);

        var result = await executor.ExecuteAndWaitAsync(
            abstractionsKernel,
            arguments,
            config,
            cancellationToken).ConfigureAwait(false);

        if (result != null && result.Success)
        {
            LogKernelExecutionCompleted(kernel.Name, result.Timings?.KernelTimeMs ?? 0);
        }
        else if (result != null)
        {
            LogKernelExecutionFailed(kernel.Name, result.ErrorMessage ?? "Unknown error");
        }

        return result ?? throw new InvalidOperationException("Kernel execution returned null result");
    }

    /// <summary>
    /// Profiles a kernel's performance.
    /// </summary>
    public async ValueTask<KernelProfilingResult> ProfileKernelAsync(
        ManagedCompiledKernel kernel,
        KernelArgument[] arguments,
        IAccelerator accelerator,
        KernelExecutionConfig? config = null,
        int iterations = 100,
        CancellationToken cancellationToken = default)
    {
        var acceleratorType = Enum.Parse<AcceleratorType>(accelerator.Info.DeviceType);
        if (!_executors.TryGetValue(acceleratorType, out var executor))
        {
            throw new NotSupportedException($"No executor registered for accelerator type {accelerator.Info.DeviceType}");
        }

        // Convert to Abstractions CompiledKernel
        var abstractionsKernel = kernel.ToCompiledKernel();
        
        // Get optimal config if not provided
        if (config == null)
        {
            var problemSize = EstimateProblemSize(arguments);
            config = executor.GetOptimalExecutionConfig(abstractionsKernel, problemSize);
        }

        LogKernelProfilingStarted(kernel.Name, iterations);

        var result = await executor.ProfileAsync(
            abstractionsKernel,
            arguments,
            config,
            iterations,
            cancellationToken).ConfigureAwait(false);

        if (result != null)
        {
            LogKernelProfilingCompleted(kernel.Name, result.AverageTimeMs, result.ComputeThroughputGFLOPS);
        }

        return result ?? throw new InvalidOperationException("Kernel profiling returned null result");
    }

    /// <summary>
    /// Clears the kernel cache.
    /// </summary>
    public void ClearCache()
    {
        var count = _kernelCache.Count;
        _kernelCache.Clear();
        LogKernelCacheCleared(count);
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public KernelCacheStatistics GetCacheStatistics()
    {
        return new KernelCacheStatistics
        {
            CachedKernelCount = _kernelCache.Count,
            TotalMemoryUsage = _kernelCache.Values.Sum(k => k.Binary.Length),
            KernelsByType = _kernelCache.Values
                .GroupBy(k => k.Parameters.Length > 0 ? "Complex" : "Simple")
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Disposes the kernel manager.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var kernel in _kernelCache.Values)
            {
                kernel.Dispose();
            }
            _kernelCache.Clear();
            _compilationSemaphore.Dispose();
            _disposed = true;
        }
    }

    private void InitializeBuiltInGenerators()
    {
        // Generators are now handled by the backend-specific implementations
        // No need for separate kernel generators in Core
    }

    private async ValueTask<ManagedCompiledKernel> GenerateAndCompileKernelAsync(
        Expression expression,
        IAccelerator accelerator,
        KernelGenerationContext? context,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        // Get generator
        var acceleratorType = Enum.Parse<AcceleratorType>(accelerator.Info.DeviceType);
        if (!_generators.TryGetValue(acceleratorType, out var generator))
        {
            throw new NotSupportedException($"No generator registered for accelerator type {accelerator.Info.DeviceType}");
        }

        // Create context if not provided
        context ??= new KernelGenerationContext
        {
            DeviceInfo = accelerator.Info,
            UseSharedMemory = accelerator.Info.LocalMemorySize > 0,
            UseVectorTypes = true,
            Precision = PrecisionMode.Single
        };

        // Generate kernel
        var generatedKernel = generator.GenerateKernel(expression, context);

        // Get compiler
        if (!_compilers.TryGetValue(acceleratorType, out var compiler))
        {
            // If no compiler registered, create a stub compiled kernel
            return new ManagedCompiledKernel
            {
                Name = generatedKernel.Name,
                Binary = System.Text.Encoding.UTF8.GetBytes(generatedKernel.Source),
                Parameters = generatedKernel.Parameters,
                RequiredWorkGroupSize = generatedKernel.RequiredWorkGroupSize,
                SharedMemorySize = generatedKernel.SharedMemorySize
            };
        }

        // Get compilation options
        options ??= GetDefaultCompilationOptions();

        // Convert GeneratedKernel to KernelDefinition
        var kernelDefinition = ConvertToKernelDefinition(generatedKernel);
        
        // Compile kernel
        var compiledKernel = await compiler.CompileAsync(kernelDefinition, ConvertToAbstractionsOptions(options), cancellationToken).ConfigureAwait(false);
        
        // Convert back to ManagedCompiledKernel
        return ConvertToManagedCompiledKernel(compiledKernel, generatedKernel);
    }

    private async ValueTask<ManagedCompiledKernel> GenerateAndCompileOperationKernelAsync(
        string operation,
        Type[] inputTypes,
        Type outputType,
        IAccelerator accelerator,
        KernelGenerationContext? context,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        // Get generator
        var acceleratorType = Enum.Parse<AcceleratorType>(accelerator.Info.DeviceType);
        if (!_generators.TryGetValue(acceleratorType, out var generator))
        {
            throw new NotSupportedException($"No generator registered for accelerator type {accelerator.Info.DeviceType}");
        }

        // Create context if not provided
        context ??= new KernelGenerationContext
        {
            DeviceInfo = accelerator.Info,
            UseSharedMemory = accelerator.Info.LocalMemorySize > 0,
            UseVectorTypes = true,
            Precision = PrecisionMode.Single
        };

        // Generate kernel
        var generatedKernel = generator.GenerateOperationKernel(operation, inputTypes, outputType, context);

        // Get compiler
        if (!_compilers.TryGetValue(acceleratorType, out var compiler))
        {
            // If no compiler registered, create a stub compiled kernel
            return new ManagedCompiledKernel
            {
                Name = generatedKernel.Name,
                Binary = System.Text.Encoding.UTF8.GetBytes(generatedKernel.Source),
                Parameters = generatedKernel.Parameters,
                RequiredWorkGroupSize = generatedKernel.RequiredWorkGroupSize,
                SharedMemorySize = generatedKernel.SharedMemorySize
            };
        }

        // Get compilation options
        options ??= GetDefaultCompilationOptions();

        // Convert GeneratedKernel to KernelDefinition
        var kernelDefinition = ConvertToKernelDefinition(generatedKernel);
        
        // Compile kernel
        var compiledKernel = await compiler.CompileAsync(kernelDefinition, ConvertToAbstractionsOptions(options), cancellationToken).ConfigureAwait(false);
        
        // Convert back to ManagedCompiledKernel
        return ConvertToManagedCompiledKernel(compiledKernel, generatedKernel);
    }

    private static string GenerateCacheKey(Expression expression, AcceleratorInfo acceleratorInfo)
    {
        // Simple hash-based cache key generation
        var hash = expression.ToString().GetHashCode();
        return $"{acceleratorInfo.DeviceType}_{acceleratorInfo.Name}_{hash:X8}";
    }

    private static string GenerateOperationCacheKey(string operation, Type[] inputTypes, Type outputType, AcceleratorInfo acceleratorInfo)
    {
        var typeSignature = string.Join("_", inputTypes.Select(t => t.Name)) + "_" + outputType.Name;
        return $"{acceleratorInfo.DeviceType}_{acceleratorInfo.Name}_{operation}_{typeSignature}";
    }

    private static int[] EstimateProblemSize(KernelArgument[] arguments)
    {
        // Try to estimate problem size from arguments
        foreach (var arg in arguments)
        {
            if (arg.Type.IsArray)
            {
                if (arg.Value is Array array)
                {
                    return [array.Length];
                }
            }
            else if (arg.MemoryBuffer != null)
            {
                var elementSize = GetElementSize(arg.Type);
                if (elementSize > 0)
                {
                    return [(int)(arg.MemoryBuffer.SizeInBytes / elementSize)];
                }
            }
        }

        // Default problem size
        return [1024];
    }

    private static int GetElementSize(Type type)
    {
        if (type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte => 1,
            TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => 8,
            _ => 0
        };
    }

    #region Logging

    private partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Registered kernel generator for accelerator type: {AcceleratorType}")]
        public static partial void GeneratorRegistered(ILogger logger, string acceleratorType);

        [LoggerMessage(2, LogLevel.Information, "Registered kernel compiler for accelerator type: {AcceleratorType}")]
        public static partial void CompilerRegistered(ILogger logger, string acceleratorType);

        [LoggerMessage(3, LogLevel.Information, "Registered kernel executor for accelerator type: {AcceleratorType}, Device: {DeviceName}")]
        public static partial void ExecutorRegistered(ILogger logger, string acceleratorType, string deviceName);

        [LoggerMessage(4, LogLevel.Debug, "Kernel cache hit: {CacheKey}")]
        public static partial void KernelCacheHit(ILogger logger, string cacheKey);

        [LoggerMessage(5, LogLevel.Information, "Compiled kernel: {KernelName} for {DeviceType}")]
        public static partial void KernelCompiled(ILogger logger, string kernelName, string deviceType);

        [LoggerMessage(6, LogLevel.Debug, "Starting kernel execution: {KernelName} on {DeviceName}")]
        public static partial void KernelExecutionStarted(ILogger logger, string kernelName, string deviceName);

        [LoggerMessage(7, LogLevel.Information, "Kernel execution completed: {KernelName}, Time: {ExecutionTimeMs:F2} ms")]
        public static partial void KernelExecutionCompleted(ILogger logger, string kernelName, double executionTimeMs);

        [LoggerMessage(8, LogLevel.Error, "Kernel execution failed: {KernelName}, Error: {ErrorMessage}")]
        public static partial void KernelExecutionFailed(ILogger logger, string kernelName, string errorMessage);

        [LoggerMessage(9, LogLevel.Information, "Starting kernel profiling: {KernelName}, Iterations: {Iterations}")]
        public static partial void KernelProfilingStarted(ILogger logger, string kernelName, int iterations);

        [LoggerMessage(10, LogLevel.Information, "Kernel profiling completed: {KernelName}, Avg: {AverageTimeMs:F2} ms, Throughput: {ThroughputGFLOPS:F2} GFLOPS")]
        public static partial void KernelProfilingCompleted(ILogger logger, string kernelName, double averageTimeMs, double throughputGFLOPS);

        [LoggerMessage(11, LogLevel.Information, "Kernel cache cleared: {KernelCount} kernels removed")]
        public static partial void KernelCacheCleared(ILogger logger, int kernelCount);
    }

    private void LogGeneratorRegistered(string acceleratorType) => Log.GeneratorRegistered(_logger, acceleratorType);
    private void LogCompilerRegistered(string acceleratorType) => Log.CompilerRegistered(_logger, acceleratorType);
    private void LogExecutorRegistered(string acceleratorType, string deviceName) => Log.ExecutorRegistered(_logger, acceleratorType, deviceName);
    private void LogKernelCacheHit(string cacheKey) => Log.KernelCacheHit(_logger, cacheKey);
    private void LogKernelCompiled(string kernelName, string deviceType) => Log.KernelCompiled(_logger, kernelName, deviceType);
    private void LogKernelExecutionStarted(string kernelName, string deviceName) => Log.KernelExecutionStarted(_logger, kernelName, deviceName);
    private void LogKernelExecutionCompleted(string kernelName, double executionTimeMs) => Log.KernelExecutionCompleted(_logger, kernelName, executionTimeMs);
    private void LogKernelExecutionFailed(string kernelName, string errorMessage) => Log.KernelExecutionFailed(_logger, kernelName, errorMessage);
    private void LogKernelProfilingStarted(string kernelName, int iterations) => Log.KernelProfilingStarted(_logger, kernelName, iterations);
    private void LogKernelProfilingCompleted(string kernelName, double averageTimeMs, double throughputGFLOPS) => Log.KernelProfilingCompleted(_logger, kernelName, averageTimeMs, throughputGFLOPS);
    private void LogKernelCacheCleared(int kernelCount) => Log.KernelCacheCleared(_logger, kernelCount);

    /// <summary>
    /// Gets default compilation options.
    /// </summary>
    private static CompilationOptions GetDefaultCompilationOptions()
    {
        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O2,
            GenerateDebugInfo = false,
            EnableFastMath = true,
            FiniteMathOnly = true,
            EnableUnsafeOptimizations = false
        };
    }

    /// <summary>
    /// Converts GeneratedKernel to KernelDefinition.
    /// </summary>
    private static KernelDefinition ConvertToKernelDefinition(GeneratedKernel generatedKernel)
    {
        var kernelSource = new TextKernelSource(
            generatedKernel.Source,
            generatedKernel.Name,
            ConvertKernelLanguage(generatedKernel.Language),
            generatedKernel.EntryPoint);

        var compilationOptions = new DotCompute.Abstractions.CompilationOptions
        {
            OptimizationLevel = DotCompute.Abstractions.OptimizationLevel.Default
        };

        return new KernelDefinition
        {
            Name = generatedKernel.Name,
            Source = kernelSource.Code ?? kernelSource.ToString() ?? "",
            EntryPoint = generatedKernel.EntryPoint
        };
    }

    /// <summary>
    /// Converts Core KernelLanguage to Abstractions KernelLanguage.
    /// </summary>
    private static DotCompute.Abstractions.KernelLanguage ConvertKernelLanguage(KernelLanguage language)
    {
        return language switch
        {
            KernelLanguage.CSharp => DotCompute.Abstractions.KernelLanguage.CSharpIL,
            KernelLanguage.OpenCL => DotCompute.Abstractions.KernelLanguage.OpenCL,
            KernelLanguage.CUDA => DotCompute.Abstractions.KernelLanguage.Cuda,
            KernelLanguage.Metal => DotCompute.Abstractions.KernelLanguage.Metal,
            KernelLanguage.DirectCompute => DotCompute.Abstractions.KernelLanguage.HLSL,
            KernelLanguage.Vulkan => DotCompute.Abstractions.KernelLanguage.SPIRV,
            KernelLanguage.WebGPU => DotCompute.Abstractions.KernelLanguage.SPIRV,
            _ => DotCompute.Abstractions.KernelLanguage.CSharpIL
        };
    }

    /// <summary>
    /// Converts Core CompilationOptions to Abstractions CompilationOptions.
    /// </summary>
    private static DotCompute.Abstractions.CompilationOptions ConvertToAbstractionsOptions(CompilationOptions options)
    {
        return new DotCompute.Abstractions.CompilationOptions
        {
            OptimizationLevel = ConvertOptimizationLevel(options.OptimizationLevel),
            EnableDebugInfo = options.GenerateDebugInfo,
            FastMath = options.EnableFastMath,
            AdditionalFlags = options.AdditionalFlags?.ToList() ?? new List<string>(),
            Defines = options.Defines
        };
    }

    /// <summary>
    /// Converts ICompiledKernel back to ManagedCompiledKernel.
    /// </summary>
    private static ManagedCompiledKernel ConvertToManagedCompiledKernel(ICompiledKernel compiledKernel, GeneratedKernel generatedKernel)
    {
        return new ManagedCompiledKernel
        {
            Name = generatedKernel.Name,
            Binary = System.Text.Encoding.UTF8.GetBytes(generatedKernel.Source), // Fallback to source
            Parameters = generatedKernel.Parameters,
            RequiredWorkGroupSize = generatedKernel.RequiredWorkGroupSize,
            SharedMemorySize = generatedKernel.SharedMemorySize
        };
    }

    /// <summary>
    /// Converts Core OptimizationLevel to Abstractions OptimizationLevel.
    /// </summary>
    private static DotCompute.Abstractions.OptimizationLevel ConvertOptimizationLevel(OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.O0 => DotCompute.Abstractions.OptimizationLevel.None,
            OptimizationLevel.O1 => DotCompute.Abstractions.OptimizationLevel.Debug,
            OptimizationLevel.O2 => DotCompute.Abstractions.OptimizationLevel.Default,
            OptimizationLevel.O3 => DotCompute.Abstractions.OptimizationLevel.Maximum,
            OptimizationLevel.Os => DotCompute.Abstractions.OptimizationLevel.Release,
            _ => DotCompute.Abstractions.OptimizationLevel.Default
        };
    }

    #endregion
}

/// <summary>
/// Kernel cache statistics.
/// </summary>
public sealed class KernelCacheStatistics
{
    /// <summary>
    /// Gets or sets the number of cached kernels.
    /// </summary>
    public int CachedKernelCount { get; init; }

    /// <summary>
    /// Gets or sets the total memory usage in bytes.
    /// </summary>
    public long TotalMemoryUsage { get; init; }

    /// <summary>
    /// Gets or sets kernel counts by type.
    /// </summary>
    public Dictionary<string, int> KernelsByType { get; init; } = [];
}}
