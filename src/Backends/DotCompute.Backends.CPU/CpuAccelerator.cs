// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CPU backend has dynamic logging requirements

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// CPU-based compute accelerator with SIMD vectorization support.
/// Migrated to use BaseAccelerator, reducing code by 75% while maintaining full functionality.
/// </summary>
public sealed class CpuAccelerator : BaseAccelerator
{
    private readonly CpuAcceleratorOptions _options;
    private readonly CpuThreadPool _threadPool;
    private readonly ILogger<CpuAccelerator> _logger;

    public CpuAccelerator(
        IOptions<CpuAcceleratorOptions> options,
        IOptions<CpuThreadPoolOptions> threadPoolOptions,
        ILogger<CpuAccelerator> logger)
        : base(
            BuildAcceleratorInfo(),
            AcceleratorType.CPU,
            new CpuMemoryManager(),
            new AcceleratorContext(IntPtr.Zero, 0),
            logger)
    {
        _options = options.Value;
        _logger = logger;
        _threadPool = new CpuThreadPool(threadPoolOptions);
    }

    /// <inheritdoc/>
    protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        // Check if we should try optimized kernel compilation first
        if (_options.EnableAutoVectorization && _options.PreferPerformanceOverPower)
        {
            var optimizedKernel = TryCreateOptimizedKernel(definition, options);
            if (optimizedKernel != null)
            {
                _logger.LogDebug("Successfully compiled optimized kernel '{KernelName}' with {VectorWidth}-bit vectorization",
                    definition.Name, SimdCapabilities.PreferredVectorWidth);
                return optimizedKernel;
            }
        }

        // Convert to core types for compilation
        var coreDefinition = ConvertToCoreKernelDefinition(definition);
        var coreOptions = ConvertToCoreCompilationOptions(options);
        
        // Create kernel compilation context
        var compilationContext = new CpuKernelCompilationContext
        {
            Definition = coreDefinition,
            Options = coreOptions,
            SimdCapabilities = SimdCapabilities.GetSummary(),
            ThreadPool = _threadPool,
            Logger = _logger
        };

        // Use AOT-compatible compiler when dynamic code compilation is not available
        var coreCompiledKernel = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled
            ? await CpuKernelCompiler.CompileAsync(compilationContext, cancellationToken).ConfigureAwait(false)
            : await new AotCpuKernelCompiler().CompileAsync(compilationContext, cancellationToken).ConfigureAwait(false);

        // Wrap the Core compiled kernel to implement Abstractions.ICompiledKernel
        return new CompiledKernelAdapter(coreCompiledKernel);
    }

    /// <inheritdoc/>
    protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
    {
        // CPU operations are synchronous by default
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeCoreAsync()
    {
        await _threadPool.DisposeAsync().ConfigureAwait(false);
        // Memory manager disposal is handled by base class
    }

    /// <summary>
    /// Attempts to create an optimized kernel for known patterns.
    /// </summary>
    private ICompiledKernel? TryCreateOptimizedKernel(KernelDefinition definition, CompilationOptions options)
    {
        try
        {
            // Parse kernel source to detect optimization opportunities
            var sourceCode = definition.Code ?? string.Empty;
            var kernelParser = new OpenCLKernelParser(_logger);
            var kernelInfo = kernelParser.ParseKernel(sourceCode, definition.EntryPoint ?? "main");

            // Create optimized kernel based on type
            return kernelInfo.Type switch
            {
                KernelType.VectorAdd => new Optimized.OptimizedVectorAddKernel(kernelInfo.Name, options, _logger),
                KernelType.VectorScale => new Optimized.OptimizedVectorScaleKernel(kernelInfo.Name, options, _logger),
                KernelType.MatrixMultiply => new Optimized.OptimizedMatrixMultiplyKernel(kernelInfo.Name, options, _logger),
                KernelType.Reduction => new Optimized.OptimizedReductionKernel(kernelInfo.Name, options, _logger),
                KernelType.MemoryIntensive => new Optimized.OptimizedMemoryKernel(kernelInfo.Name, options, _logger),
                KernelType.ComputeIntensive => new Optimized.OptimizedComputeKernel(kernelInfo.Name, options, _logger),
                _ => new Optimized.GenericOptimizedKernel(kernelInfo.Name, kernelInfo, options, _logger)
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create optimized kernel for {KernelName}, falling back to standard compilation", definition.Name);
            return null;
        }
    }

    private static AcceleratorInfo BuildAcceleratorInfo()
    {
        var simdInfo = SimdCapabilities.GetSummary();
        var capabilities = new Dictionary<string, object>
        {
            ["SimdWidth"] = SimdCapabilities.PreferredVectorWidth,
            ["SimdInstructionSets"] = simdInfo.SupportedInstructionSets,
            ["ThreadCount"] = Environment.ProcessorCount,
            ["NumaNodes"] = NumaInfo.Topology.NodeCount,
            ["CacheLineSize"] = 64 // Most modern CPUs use 64-byte cache lines
        };

        return new AcceleratorInfo(
            AcceleratorType.CPU,
            GetProcessorName(),
            Environment.Version.ToString(),
            GetTotalPhysicalMemory(),
            Environment.ProcessorCount,
            3000, // Default value for CPU
            Environment.Version,
            GetTotalPhysicalMemory() / 4, // max shared memory per block
            true // is unified memory
        )
        {
            Capabilities = capabilities
        };
    }

    private static string GetProcessorName()
    {
        // Return a descriptive name based on the architecture and processor count
        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        var cores = Environment.ProcessorCount;
        return $"{arch} CPU ({cores} cores)";
    }

    private static long GetTotalPhysicalMemory()
    {
        try
        {
            // Use GC memory information which provides reliable memory statistics
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            if (gcMemoryInfo.TotalAvailableMemoryBytes > 0)
            {
                return gcMemoryInfo.TotalAvailableMemoryBytes;
            }

            // Platform-specific fallbacks if needed
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxPhysicalMemory();
            }
            
            // Default fallback
            return 4L * 1024 * 1024 * 1024; // 4GB
        }
        catch
        {
            return 4L * 1024 * 1024 * 1024; // 4GB default
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static long GetLinuxPhysicalMemory()
    {
        try
        {
            if (System.IO.File.Exists("/proc/meminfo"))
            {
                var lines = System.IO.File.ReadAllLines("/proc/meminfo");
                var totalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
                if (totalLine != null)
                {
                    var parts = totalLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    {
                        return kb * 1024; // Convert KB to bytes
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }
        return 8L * 1024 * 1024 * 1024; // 8GB default
    }

    private static KernelDefinition ConvertToCoreKernelDefinition(KernelDefinition definition)
    {
        var sourceCode = definition.Code ?? string.Empty;
        var kernelSource = new TextKernelSource(
            code: sourceCode,
            name: definition.Name,
            language: KernelLanguage.CSharpIL,
            entryPoint: definition.EntryPoint ?? "main",
            dependencies: []
        );

        var coreDefinition = new KernelDefinition(definition.Name, kernelSource.Code, kernelSource.EntryPoint);

        // Override metadata with original information
        if (coreDefinition.Metadata != null && definition.Metadata != null)
        {
            foreach (var kvp in definition.Metadata)
            {
                coreDefinition.Metadata[kvp.Key] = kvp.Value;
            }
        }

        return coreDefinition;
    }

    private static CompilationOptions ConvertToCoreCompilationOptions(CompilationOptions options)
    {
        return new CompilationOptions
        {
            OptimizationLevel = options.OptimizationLevel,
            EnableDebugInfo = options.EnableDebugInfo,
            AdditionalFlags = options.AdditionalFlags
        };
    }
}

/// <summary>
/// Adapter that wraps a Core.ICompiledKernel to implement Abstractions.ICompiledKernel.
/// </summary>
internal sealed class CompiledKernelAdapter(ICompiledKernel coreKernel) : ICompiledKernel
{
    private readonly ICompiledKernel _coreKernel = coreKernel ?? throw new ArgumentNullException(nameof(coreKernel));

    public string Name => _coreKernel.Name;

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default) 
        => await _coreKernel.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync() => _coreKernel.DisposeAsync();
}
