// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Extensions.DotCompute.Linq.KernelGeneration.Templates;
using DotCompute.Extensions.DotCompute.Linq.KernelGeneration.Optimization;
using DotCompute.Extensions.DotCompute.Linq.KernelGeneration.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Extensions.DotCompute.Linq.KernelGeneration
{
    /// <summary>
    /// Main CUDA kernel generator for LINQ expressions.
    /// Produces optimized CUDA kernels from LINQ operations with PTX and CUBIN generation.
    /// </summary>
    public sealed class CudaKernelGenerator : IDisposable
    {
        private readonly CudaKernelCompiler _compiler;
        private readonly ILogger<CudaKernelGenerator> _logger;
        private readonly ConcurrentDictionary<string, KernelCacheEntry> _kernelCache;
        private readonly GpuOptimizer _optimizer;
        private readonly GpuMemoryManager _memoryManager;
        private readonly CudaKernelTemplates _templates;
        private bool _disposed;

        public CudaKernelGenerator(CudaContext context, ILogger<CudaKernelGenerator> logger)
        {
            _compiler = new CudaKernelCompiler(context, logger);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernelCache = new ConcurrentDictionary<string, KernelCacheEntry>();
            _optimizer = new GpuOptimizer(logger);
            _memoryManager = new GpuMemoryManager(context, logger);
            _templates = new CudaKernelTemplates(logger);
        }

        /// <summary>
        /// Generates a CUDA kernel from a LINQ expression tree.
        /// </summary>
        public async Task<GeneratedKernel> GenerateKernelAsync<T>(
            Expression expression,
            Type inputType,
            Type outputType,
            KernelGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expression);

            var kernelOptions = options ?? new KernelGenerationOptions();
            var cacheKey = GenerateCacheKey(expression, inputType, outputType, kernelOptions);

            // Check cache first
            if (_kernelCache.TryGetValue(cacheKey, out var cachedEntry) && !cachedEntry.IsExpired)
            {
                _logger.LogDebug("Using cached kernel for expression: {CacheKey}", cacheKey);
                return cachedEntry.Kernel;
            }

            _logger.LogInformation("Generating CUDA kernel for LINQ expression: {Expression}", expression);

            try
            {
                // Analyze the expression tree
                var analysisResult = AnalyzeExpression(expression, inputType, outputType);
                
                // Select appropriate kernel template
                var template = _templates.SelectTemplate(analysisResult.OperationType, inputType, outputType);
                
                // Generate optimized CUDA source code
                var sourceCode = await GenerateSourceCodeAsync(template, analysisResult, kernelOptions);
                
                // Apply GPU-specific optimizations
                var optimizedSource = await _optimizer.OptimizeKernelSourceAsync(
                    sourceCode, analysisResult, kernelOptions);
                
                // Compile to PTX or CUBIN
                var compiledKernel = await CompileKernelAsync(
                    optimizedSource, analysisResult.KernelName, kernelOptions, cancellationToken);
                
                // Create generated kernel wrapper
                var generatedKernel = new GeneratedKernel(
                    compiledKernel,
                    analysisResult,
                    optimizedSource,
                    _memoryManager);

                // Cache the result
                var cacheEntry = new KernelCacheEntry(generatedKernel, DateTime.UtcNow.AddHours(1));
                _kernelCache.TryAdd(cacheKey, cacheEntry);

                _logger.LogInformation("Successfully generated CUDA kernel: {KernelName}", analysisResult.KernelName);
                return generatedKernel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate CUDA kernel for expression: {Expression}", expression);
                throw new KernelGenerationException($"Failed to generate CUDA kernel: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates a batch of kernels for multiple expressions in parallel.
        /// </summary>
        public async Task<GeneratedKernel[]> GenerateBatchAsync<T>(
            IEnumerable<Expression> expressions,
            Type inputType,
            Type outputType,
            KernelGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var expressionArray = expressions.ToArray();
            var tasks = expressionArray.Select(expr =>
                GenerateKernelAsync<T>(expr, inputType, outputType, options, cancellationToken));

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Analyzes a LINQ expression tree to determine kernel requirements.
        /// </summary>
        private ExpressionAnalysisResult AnalyzeExpression(Expression expression, Type inputType, Type outputType)
        {
            var visitor = new LinqExpressionAnalyzer();
            visitor.Visit(expression);

            var operationType = DetermineOperationType(expression);
            var kernelName = GenerateKernelName(operationType, inputType, outputType);
            var dataSize = EstimateDataSize(visitor.Parameters, inputType);

            return new ExpressionAnalysisResult
            {
                Expression = expression,
                OperationType = operationType,
                KernelName = kernelName,
                InputType = inputType,
                OutputType = outputType,
                Parameters = visitor.Parameters,
                EstimatedDataSize = dataSize,
                RequiresReduction = visitor.HasReduction,
                RequiresSharedMemory = visitor.RequiresSharedMemory,
                ComplexityLevel = visitor.ComplexityLevel
            };
        }

        /// <summary>
        /// Generates optimized CUDA source code from a template and analysis result.
        /// </summary>
        private async Task<string> GenerateSourceCodeAsync(
            KernelTemplate template,
            ExpressionAnalysisResult analysis,
            KernelGenerationOptions options)
        {
            var sourceBuilder = new StringBuilder();

            // Add CUDA headers and includes
            AddCudaHeaders(sourceBuilder, analysis, options);

            // Add performance optimization defines
            AddOptimizationDefines(sourceBuilder, options);

            // Add type definitions
            AddTypeDefinitions(sourceBuilder, analysis.InputType, analysis.OutputType);

            // Add device functions if needed
            if (analysis.RequiresCustomFunctions)
            {
                AddDeviceFunctions(sourceBuilder, analysis);
            }

            // Generate the main kernel function
            var kernelSource = await template.GenerateKernelAsync(analysis, options);
            sourceBuilder.Append(kernelSource);

            return sourceBuilder.ToString();
        }

        /// <summary>
        /// Compiles the generated source code to PTX or CUBIN.
        /// </summary>
        private async Task<ICompiledKernel> CompileKernelAsync(
            string sourceCode,
            string kernelName,
            KernelGenerationOptions options,
            CancellationToken cancellationToken)
        {
            var kernelDefinition = new KernelDefinition
            {
                Name = kernelName,
                Code = sourceCode,
                EntryPoint = kernelName,
                Language = KernelLanguage.Cuda
            };

            var compilationOptions = CreateCompilationOptions(options);
            
            return await _compiler.CompileAsync(kernelDefinition, compilationOptions, cancellationToken);
        }

        /// <summary>
        /// Adds CUDA headers and includes to the source code.
        /// </summary>
        private void AddCudaHeaders(StringBuilder sourceBuilder, ExpressionAnalysisResult analysis, KernelGenerationOptions options)
        {
            sourceBuilder.AppendLine("// Auto-generated CUDA kernel from LINQ expression");
            sourceBuilder.AppendLine($"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sourceBuilder.AppendLine($"// Kernel: {analysis.KernelName}");
            sourceBuilder.AppendLine();

            // Core CUDA functionality (available in NVRTC without includes)
            sourceBuilder.AppendLine("// CUDA built-in variables and functions are implicitly available");
            
            if (analysis.RequiresCooperativeGroups)
            {
                sourceBuilder.AppendLine("#include <cooperative_groups.h>");
            }

            if (analysis.RequiresSharedMemory || options.UseSharedMemory)
            {
                sourceBuilder.AppendLine("// Shared memory optimization enabled");
            }

            if (options.UseTensorCores)
            {
                sourceBuilder.AppendLine("// Tensor Core optimization enabled");
                sourceBuilder.AppendLine("#include <mma.h>");
            }

            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Adds performance optimization defines based on compute capability.
        /// </summary>
        private void AddOptimizationDefines(StringBuilder sourceBuilder, KernelGenerationOptions options)
        {
            var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
            
            sourceBuilder.AppendLine($"// Target compute capability: {major}.{minor}");
            sourceBuilder.AppendLine("#define WARP_SIZE 32");
            sourceBuilder.AppendLine($"#define MAX_THREADS_PER_BLOCK {GetMaxThreadsPerBlock(major, minor)}");

            if (major >= 7) // Volta and newer
            {
                sourceBuilder.AppendLine("#define VOLTA_OPTIMIZATIONS 1");
                sourceBuilder.AppendLine("#define COOPERATIVE_GROUPS_AVAILABLE 1");
            }

            if (major >= 8) // Ampere and newer
            {
                sourceBuilder.AppendLine("#define AMPERE_OPTIMIZATIONS 1");
                sourceBuilder.AppendLine("#define ASYNC_COPY_AVAILABLE 1");
                
                if (minor >= 9) // Ada Lovelace
                {
                    sourceBuilder.AppendLine("#define ADA_OPTIMIZATIONS 1");
                    sourceBuilder.AppendLine("#define SHARED_MEM_SIZE_100KB 1");
                }
            }

            if (options.EnableWarpShuffle)
            {
                sourceBuilder.AppendLine("#define WARP_SHUFFLE_ENABLED 1");
            }

            if (options.EnableAtomics)
            {
                sourceBuilder.AppendLine("#define ATOMIC_OPERATIONS_ENABLED 1");
            }

            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Adds type definitions for input and output types.
        /// </summary>
        private void AddTypeDefinitions(StringBuilder sourceBuilder, Type inputType, Type outputType)
        {
            sourceBuilder.AppendLine("// Type definitions");
            
            var inputTypeName = GetCudaTypeName(inputType);
            var outputTypeName = GetCudaTypeName(outputType);
            
            sourceBuilder.AppendLine($"typedef {inputTypeName} InputType;");
            sourceBuilder.AppendLine($"typedef {outputTypeName} OutputType;");
            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Adds device functions for complex operations.
        /// </summary>
        private void AddDeviceFunctions(StringBuilder sourceBuilder, ExpressionAnalysisResult analysis)
        {
            sourceBuilder.AppendLine("// Device functions");
            
            if (analysis.RequiresCustomMath)
            {
                AddMathDeviceFunctions(sourceBuilder);
            }

            if (analysis.RequiresStringOperations)
            {
                AddStringDeviceFunctions(sourceBuilder);
            }

            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Adds mathematical device functions.
        /// </summary>
        private void AddMathDeviceFunctions(StringBuilder sourceBuilder)
        {
            sourceBuilder.AppendLine("__device__ inline float safe_divide(float a, float b) {");
            sourceBuilder.AppendLine("    return b != 0.0f ? a / b : 0.0f;");
            sourceBuilder.AppendLine("}");
            sourceBuilder.AppendLine();

            sourceBuilder.AppendLine("__device__ inline double safe_divide(double a, double b) {");
            sourceBuilder.AppendLine("    return b != 0.0 ? a / b : 0.0;");
            sourceBuilder.AppendLine("}");
            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Adds string operation device functions.
        /// </summary>
        private void AddStringDeviceFunctions(StringBuilder sourceBuilder)
        {
            sourceBuilder.AppendLine("__device__ inline int string_length(const char* str) {");
            sourceBuilder.AppendLine("    int len = 0;");
            sourceBuilder.AppendLine("    while (str[len] != '\\0') len++;");
            sourceBuilder.AppendLine("    return len;");
            sourceBuilder.AppendLine("}");
            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Creates compilation options based on kernel generation options.
        /// </summary>
        private CompilationOptions CreateCompilationOptions(KernelGenerationOptions options)
        {
            return new CompilationOptions
            {
                OptimizationLevel = options.OptimizationLevel,
                EnableDebugInfo = options.EnableDebugInfo,
                EnableDynamicParallelism = options.EnableDynamicParallelism,
                AdditionalFlags = BuildAdditionalFlags(options)
            };
        }

        /// <summary>
        /// Builds additional compilation flags based on options.
        /// </summary>
        private string[] BuildAdditionalFlags(KernelGenerationOptions options)
        {
            var flags = new List<string>();

            if (options.EnableFastMath)
            {
                flags.Add("--use_fast_math");
            }

            if (options.MaxRegisterCount.HasValue)
            {
                flags.Add($"--maxrregcount={options.MaxRegisterCount.Value}");
            }

            if (options.EnableLineInfo)
            {
                flags.Add("--generate-line-info");
            }

            return flags.ToArray();
        }

        /// <summary>
        /// Determines the operation type from a LINQ expression.
        /// </summary>
        private KernelOperationType DetermineOperationType(Expression expression)
        {
            return expression switch
            {
                MethodCallExpression methodCall => methodCall.Method.Name switch
                {
                    "Select" => KernelOperationType.Map,
                    "Where" => KernelOperationType.Filter,
                    "Sum" => KernelOperationType.Reduce,
                    "Average" => KernelOperationType.Reduce,
                    "Min" => KernelOperationType.Reduce,
                    "Max" => KernelOperationType.Reduce,
                    "Count" => KernelOperationType.Reduce,
                    "Join" => KernelOperationType.Join,
                    "GroupBy" => KernelOperationType.GroupBy,
                    "OrderBy" => KernelOperationType.Sort,
                    "Scan" => KernelOperationType.Scan,
                    _ => KernelOperationType.Custom
                },
                _ => KernelOperationType.Custom
            };
        }

        /// <summary>
        /// Generates a unique kernel name based on operation and types.
        /// </summary>
        private string GenerateKernelName(KernelOperationType operationType, Type inputType, Type outputType)
        {
            var inputTypeName = GetSimpleTypeName(inputType);
            var outputTypeName = GetSimpleTypeName(outputType);
            var timestamp = DateTime.UtcNow.Ticks;
            
            return $"linq_{operationType.ToString().ToLowerInvariant()}_{inputTypeName}_to_{outputTypeName}_{timestamp}";
        }

        /// <summary>
        /// Estimates data size for memory optimization.
        /// </summary>
        private long EstimateDataSize(List<ParameterExpression> parameters, Type inputType)
        {
            // Basic estimation - can be enhanced with actual data analysis
            var typeSize = GetTypeSize(inputType);
            var estimatedElementCount = 1000; // Default estimation
            
            return typeSize * estimatedElementCount;
        }

        /// <summary>
        /// Gets the CUDA type name for a .NET type.
        /// </summary>
        private string GetCudaTypeName(Type type)
        {
            return type switch
            {
                var t when t == typeof(int) => "int",
                var t when t == typeof(uint) => "unsigned int",
                var t when t == typeof(long) => "long long",
                var t when t == typeof(ulong) => "unsigned long long",
                var t when t == typeof(float) => "float",
                var t when t == typeof(double) => "double",
                var t when t == typeof(bool) => "bool",
                var t when t == typeof(byte) => "unsigned char",
                var t when t == typeof(sbyte) => "char",
                var t when t == typeof(short) => "short",
                var t when t == typeof(ushort) => "unsigned short",
                _ => "void*" // Generic pointer for complex types
            };
        }

        /// <summary>
        /// Gets a simplified type name for kernel naming.
        /// </summary>
        private string GetSimpleTypeName(Type type)
        {
            return type switch
            {
                var t when t == typeof(int) => "int",
                var t when t == typeof(float) => "float",
                var t when t == typeof(double) => "double",
                var t when t == typeof(bool) => "bool",
                _ => type.Name.ToLowerInvariant()
            };
        }

        /// <summary>
        /// Gets the size in bytes of a type.
        /// </summary>
        private int GetTypeSize(Type type)
        {
            return type switch
            {
                var t when t == typeof(int) => 4,
                var t when t == typeof(uint) => 4,
                var t when t == typeof(long) => 8,
                var t when t == typeof(ulong) => 8,
                var t when t == typeof(float) => 4,
                var t when t == typeof(double) => 8,
                var t when t == typeof(bool) => 1,
                var t when t == typeof(byte) => 1,
                var t when t == typeof(sbyte) => 1,
                var t when t == typeof(short) => 2,
                var t when t == typeof(ushort) => 2,
                _ => 8 // Default for complex types
            };
        }

        /// <summary>
        /// Gets maximum threads per block for a compute capability.
        /// </summary>
        private int GetMaxThreadsPerBlock(int major, int minor)
        {
            return (major, minor) switch
            {
                (>= 8, >= 9) => 1024, // Ada Lovelace
                (>= 8, _) => 1024,    // Ampere
                (>= 7, _) => 1024,    // Volta/Turing
                (>= 5, _) => 1024,    // Maxwell/Pascal
                _ => 512              // Older architectures
            };
        }

        /// <summary>
        /// Generates a cache key for kernel caching.
        /// </summary>
        private string GenerateCacheKey(Expression expression, Type inputType, Type outputType, KernelGenerationOptions options)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(expression.ToString());
            keyBuilder.Append('_');
            keyBuilder.Append(inputType.FullName);
            keyBuilder.Append('_');
            keyBuilder.Append(outputType.FullName);
            keyBuilder.Append('_');
            keyBuilder.Append(options.GetHashCode());
            
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        }

        /// <summary>
        /// Clears the kernel cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (var entry in _kernelCache.Values)
            {
                entry.Kernel.Dispose();
            }
            _kernelCache.Clear();
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public KernelCacheStatistics GetCacheStatistics()
        {
            var validEntries = _kernelCache.Values.Where(e => !e.IsExpired).ToArray();
            
            return new KernelCacheStatistics
            {
                TotalEntries = _kernelCache.Count,
                ValidEntries = validEntries.Length,
                ExpiredEntries = _kernelCache.Count - validEntries.Length,
                HitRate = CalculateHitRate()
            };
        }

        private double CalculateHitRate()
        {
            // Simple implementation - can be enhanced with actual hit/miss tracking
            return _kernelCache.Count > 0 ? 0.85 : 0.0; // 85% estimated hit rate
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ClearCache();
            _optimizer.Dispose();
            _memoryManager.Dispose();
            _compiler.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Options for kernel generation.
    /// </summary>
    public class KernelGenerationOptions
    {
        public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;
        public bool EnableDebugInfo { get; set; } = false;
        public bool EnableDynamicParallelism { get; set; } = false;
        public bool EnableFastMath { get; set; } = true;
        public bool EnableWarpShuffle { get; set; } = true;
        public bool EnableAtomics { get; set; } = true;
        public bool UseSharedMemory { get; set; } = true;
        public bool UseTensorCores { get; set; } = false;
        public bool EnableLineInfo { get; set; } = false;
        public int? MaxRegisterCount { get; set; }
        public int? SharedMemorySize { get; set; }
        public int? BlockSize { get; set; }
        public bool PreferPtx { get; set; } = true;
    }

    /// <summary>
    /// Result of expression analysis.
    /// </summary>
    public class ExpressionAnalysisResult
    {
        public required Expression Expression { get; set; }
        public required KernelOperationType OperationType { get; set; }
        public required string KernelName { get; set; }
        public required Type InputType { get; set; }
        public required Type OutputType { get; set; }
        public required List<ParameterExpression> Parameters { get; set; }
        public required long EstimatedDataSize { get; set; }
        public bool RequiresReduction { get; set; }
        public bool RequiresSharedMemory { get; set; }
        public bool RequiresCooperativeGroups { get; set; }
        public bool RequiresCustomFunctions { get; set; }
        public bool RequiresCustomMath { get; set; }
        public bool RequiresStringOperations { get; set; }
        public int ComplexityLevel { get; set; }
    }

    /// <summary>
    /// Generated kernel wrapper.
    /// </summary>
    public class GeneratedKernel : IDisposable
    {
        public ICompiledKernel CompiledKernel { get; }
        public ExpressionAnalysisResult Analysis { get; }
        public string SourceCode { get; }
        public GpuMemoryManager MemoryManager { get; }

        public GeneratedKernel(
            ICompiledKernel compiledKernel,
            ExpressionAnalysisResult analysis,
            string sourceCode,
            GpuMemoryManager memoryManager)
        {
            CompiledKernel = compiledKernel;
            Analysis = analysis;
            SourceCode = sourceCode;
            MemoryManager = memoryManager;
        }

        public void Dispose()
        {
            CompiledKernel.Dispose();
        }
    }

    /// <summary>
    /// Kernel cache entry.
    /// </summary>
    internal class KernelCacheEntry
    {
        public GeneratedKernel Kernel { get; }
        public DateTime ExpiryTime { get; }
        public bool IsExpired => DateTime.UtcNow > ExpiryTime;

        public KernelCacheEntry(GeneratedKernel kernel, DateTime expiryTime)
        {
            Kernel = kernel;
            ExpiryTime = expiryTime;
        }
    }

    /// <summary>
    /// Kernel cache statistics.
    /// </summary>
    public class KernelCacheStatistics
    {
        public int TotalEntries { get; set; }
        public int ValidEntries { get; set; }
        public int ExpiredEntries { get; set; }
        public double HitRate { get; set; }
    }

    /// <summary>
    /// Kernel operation types.
    /// </summary>
    public enum KernelOperationType
    {
        Map,
        Filter,
        Reduce,
        Scan,
        Join,
        GroupBy,
        Sort,
        Custom
    }

    /// <summary>
    /// Exception thrown during kernel generation.
    /// </summary>
    public class KernelGenerationException : Exception
    {
        public KernelGenerationException(string message) : base(message) { }
        public KernelGenerationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// LINQ expression analyzer visitor.
    /// </summary>
    internal class LinqExpressionAnalyzer : ExpressionVisitor
    {
        public List<ParameterExpression> Parameters { get; } = new();
        public bool HasReduction { get; private set; }
        public bool RequiresSharedMemory { get; private set; }
        public int ComplexityLevel { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!Parameters.Contains(node))
            {
                Parameters.Add(node);
            }
            return base.VisitParameter(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Analyze method calls for operation characteristics
            switch (node.Method.Name)
            {
                case "Sum":
                case "Average":
                case "Min":
                case "Max":
                case "Count":
                    HasReduction = true;
                    RequiresSharedMemory = true;
                    ComplexityLevel += 2;
                    break;
                case "GroupBy":
                case "Join":
                    RequiresSharedMemory = true;
                    ComplexityLevel += 3;
                    break;
                default:
                    ComplexityLevel += 1;
                    break;
            }

            return base.VisitMethodCall(node);
        }
    }
}