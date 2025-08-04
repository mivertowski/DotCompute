// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CoreAcceleratorInfo = DotCompute.Abstractions.AcceleratorInfo;
using CoreAcceleratorType = DotCompute.Abstractions.AcceleratorType;
using CoreCompilationOptions = DotCompute.Abstractions.CompilationOptions;
using CoreICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using CoreKernelDefinition = DotCompute.Abstractions.KernelDefinition;
using CoreKernelExecutionContext = DotCompute.Core.KernelExecutionContext;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// CPU-based compute accelerator with SIMD vectorization support.
/// </summary>
public sealed class CpuAccelerator : IAccelerator
{
    private readonly ILogger<CpuAccelerator> _logger;
    private readonly CpuAcceleratorOptions _options;
    private readonly CpuThreadPool _threadPool;
    private readonly CpuMemoryManager _memoryManager;
    private readonly CoreAcceleratorInfo _info;
    private int _disposed;

    public CpuAccelerator(
        IOptions<CpuAcceleratorOptions> options,
        IOptions<CpuThreadPoolOptions> threadPoolOptions,
        ILogger<CpuAccelerator> logger)
    {
        _options = options.Value;
        _logger = logger;
        _threadPool = new CpuThreadPool(threadPoolOptions);
        _memoryManager = new CpuMemoryManager();

        // Build accelerator info
        var simdInfo = SimdCapabilities.GetSummary();
        var capabilities = new Dictionary<string, object>
        {
            ["SimdWidth"] = SimdCapabilities.PreferredVectorWidth,
            ["SimdInstructionSets"] = simdInfo.SupportedInstructionSets,
            ["ThreadCount"] = _threadPool.WorkerCount,
            ["NumaNodes"] = GetNumaNodeCount(),
            ["CacheLineSize"] = GetCacheLineSize()
        };

        _info = new CoreAcceleratorInfo(
            CoreAcceleratorType.CPU,
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

        _logger.LogInformation(
            "Initialized CPU accelerator: {Name} with {Cores} cores, {SimdInfo}",
            _info.Name, _info.ComputeUnits, simdInfo);
    }

    /// <inheritdoc/>
    public AcceleratorInfo Info => _info;

    /// <inheritdoc/>
    public IMemoryManager Memory => _memoryManager;

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        _logger.LogDebug("Compiling kernel '{KernelName}' for CPU with vectorization", definition.Name);

        // Convert Abstractions types to Core types
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

        // Compile the kernel with vectorization support
        // Use AOT-compatible compiler when dynamic code compilation is not available
        var coreCompiledKernel = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled
            ? await new CpuKernelCompiler().CompileAsync(compilationContext, cancellationToken).ConfigureAwait(false)
            : await new AotCpuKernelCompiler().CompileAsync(compilationContext, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Successfully compiled kernel '{KernelName}' with {VectorWidth}-bit vectorization",
            definition.Name, SimdCapabilities.PreferredVectorWidth);

        // Wrap the Core compiled kernel to implement Abstractions.ICompiledKernel
        return new CompiledKernelAdapter(coreCompiledKernel);
    }

    /// <inheritdoc/>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // CPU operations are synchronous by default
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _logger.LogInformation("Disposing CPU accelerator");

        await _threadPool.DisposeAsync().ConfigureAwait(false);
        _memoryManager.Dispose();
    }

    private static string GetProcessorName()
    {
        // Return a descriptive name based on the architecture and processor count
        // This provides useful information without requiring platform-specific APIs
        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        var cores = Environment.ProcessorCount;
        return $"{arch} CPU ({cores} cores)";
    }

    private static string GetProcessorVendor()
    {
        // In a real implementation, this would detect Intel/AMD/ARM etc.
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            return Environment.ProcessorCount > 0 ? "x86/x64" : "Unknown";
        }
        else if (OperatingSystem.IsMacOS())
        {
            return "Apple";
        }
        return "Unknown";
    }

    private static long GetTotalPhysicalMemory()
    {
        try
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return GetWindowsPhysicalMemory();
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return GetLinuxPhysicalMemory();
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return GetMacOSPhysicalMemory();
            }
            else
            {
                // Unknown platform - use conservative estimate
                return Math.Max(GC.GetTotalMemory(false) * 8, 2L * 1024 * 1024 * 1024);
            }
        }
        catch
        {
            // If platform detection fails, return reasonable default
            return 4L * 1024 * 1024 * 1024; // 4GB
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static long GetWindowsPhysicalMemory()
    {
        // Use GC memory information which provides reliable memory statistics
        // This approach works across all Windows versions without P/Invoke
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        if (gcMemoryInfo.TotalAvailableMemoryBytes > 0)
        {
            return gcMemoryInfo.TotalAvailableMemoryBytes;
        }

        // Fallback to process-based estimation
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return Math.Max(process.WorkingSet64 * 8, 4L * 1024 * 1024 * 1024);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static long GetLinuxPhysicalMemory()
    {
        try
        {
            if (System.IO.File.Exists("/proc/meminfo"))
            {
                var lines = System.IO.File.ReadAllLines("/proc/meminfo");
                var totalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
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

    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    private static long GetMacOSPhysicalMemory()
    {
        // Use GC memory information which works across all platforms
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        if (gcMemoryInfo.TotalAvailableMemoryBytes > 0)
        {
            return gcMemoryInfo.TotalAvailableMemoryBytes;
        }

        // Fallback to reasonable default for macOS systems
        return 8L * 1024 * 1024 * 1024; // 8GB default
    }

    private static long GetCacheSize()
    {
        // L3 cache size estimation
        // In reality, you'd query this from the system
        return 8 * 1024 * 1024; // 8MB default
    }

    private static int GetNumaNodeCount()
    {
        return NumaInfo.Topology.NodeCount;
    }

    private static int GetCacheLineSize()
    {
        // Most modern CPUs use 64-byte cache lines
        return 64;
    }

    private static CoreKernelDefinition ConvertToCoreKernelDefinition(KernelDefinition definition)
    {
        // Convert from Abstractions to Core types
        // Since we're using Abstractions now, we can use the definition directly
        var source = definition.Code;

        var sourceCode = System.Text.Encoding.UTF8.GetString(source);
        var kernelSource = new TextKernelSource(
            code: sourceCode,
            name: definition.Name,
            language: KernelLanguage.CSharpIL,
            entryPoint: definition.EntryPoint ?? "main",
            dependencies: Array.Empty<string>()
        );

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false,
            AdditionalFlags = null,
            Defines = null
        };

        var coreDefinition = new CoreKernelDefinition(definition.Name, kernelSource, compilationOptions);

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

    private static CoreCompilationOptions ConvertToCoreCompilationOptions(CompilationOptions options)
    {
        return new CoreCompilationOptions
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
internal sealed class CompiledKernelAdapter : ICompiledKernel
{
    private readonly CoreICompiledKernel _coreKernel;

    public CompiledKernelAdapter(CoreICompiledKernel coreKernel)
    {
        _coreKernel = coreKernel ?? throw new ArgumentNullException(nameof(coreKernel));
    }

    public string Name => _coreKernel.Name;

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Direct pass-through to the core kernel (both use KernelArguments)
        await _coreKernel.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        return _coreKernel.DisposeAsync();
    }
}

/// <summary>
/// Configuration options for the CPU accelerator.
/// </summary>
public sealed class CpuAcceleratorOptions
{
    /// <summary>
    /// Gets or sets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the maximum memory allocation size.
    /// </summary>
    public long MaxMemoryAllocation { get; set; } = 2L * 1024 * 1024 * 1024; // 2GB

    /// <summary>
    /// Gets or sets whether to enable auto-vectorization.
    /// </summary>
    public bool EnableAutoVectorization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use NUMA-aware memory allocation.
    /// </summary>
    public bool EnableNumaAwareAllocation { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum work size for vectorization.
    /// </summary>
    public int MinVectorizationWorkSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets whether to enable aggressive loop unrolling.
    /// </summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>
    /// Gets or sets the target vector width in bits (0 = auto-detect).
    /// </summary>
    public int TargetVectorWidth { get; set; }
}
