// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using Optimized = DotCompute.Backends.CPU.Kernels.Optimized;
using Types = DotCompute.Backends.CPU.Kernels.Types;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CoreAcceleratorInfo = DotCompute.Abstractions.AcceleratorInfo;
using CoreAcceleratorType = DotCompute.Abstractions.AcceleratorType;
using CoreCompilationOptions = DotCompute.Abstractions.CompilationOptions;
using CoreICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using CoreKernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
using DotCompute.Backends.CPU.Kernels.Types;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CPU backend has dynamic logging requirements

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

    /// <inheritdoc/>
    public AcceleratorType Type => AcceleratorType.CPU;

    /// <inheritdoc/>
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

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
            _ = Environment.Version.ToString(),
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
        CoreKernelDefinition definition,
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

        // Fall back to standard compilation
        // Use AOT-compatible compiler when dynamic code compilation is not available
        var coreCompiledKernel = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled
            ? await CpuKernelCompiler.CompileAsync(compilationContext, cancellationToken).ConfigureAwait(false)
            : await new AotCpuKernelCompiler().CompileAsync(compilationContext, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Successfully compiled kernel '{KernelName}' with {VectorWidth}-bit vectorization",
            definition.Name, SimdCapabilities.PreferredVectorWidth);

        // Wrap the Core compiled kernel to implement Abstractions.ICompiledKernel
        return new CompiledKernelAdapter(coreCompiledKernel);
    }

    /// <summary>
    /// Attempts to create an optimized kernel for known patterns.
    /// </summary>
    private ICompiledKernel? TryCreateOptimizedKernel(CoreKernelDefinition definition, CompilationOptions options)
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

    /// <inheritdoc/>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask; // CPU operations are synchronous by default

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

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
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCacheSizeWindows();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetCacheSizeLinux();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCacheSizeMacOS();
            }
        }
        catch (Exception ex)
        {
            // Log exception but don't throw - fall back to default
            System.Diagnostics.Debug.WriteLine($"Failed to detect cache size: {ex.Message}");
        }

        // Fallback to reasonable default - 8MB L3 cache
        return 8 * 1024 * 1024;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static long GetCacheSizeWindows()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return 8 * 1024 * 1024; // 8MB default
            }

            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_CacheMemory WHERE Level = 3");

            foreach (System.Management.ManagementObject cache in searcher.Get())
            {
                var maxCacheSize = cache["MaxCacheSize"];
                if (maxCacheSize != null && uint.TryParse(maxCacheSize.ToString(), out var sizeKB))
                {
                    return sizeKB * 1024L; // Convert KB to bytes
                }
            }

            // Alternative: Try Win32_Processor for cache information
            using var procSearcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_Processor");

            foreach (System.Management.ManagementObject processor in procSearcher.Get())
            {
                var l3CacheSize = processor["L3CacheSize"];
                if (l3CacheSize != null && uint.TryParse(l3CacheSize.ToString(), out var sizeKB))
                {
                    return sizeKB * 1024L; // Convert KB to bytes
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return 8 * 1024 * 1024; // 8MB default
    }

    private static long GetCacheSizeLinux()
    {
        try
        {
            // Try to read from sysfs first - most reliable method
            var cacheInfoPath = "/sys/devices/system/cpu/cpu0/cache";
            if (Directory.Exists(cacheInfoPath))
            {
                // Look for L3 cache (index3) or highest level cache
                var cacheIndexes = Directory.GetDirectories(cacheInfoPath, "index*")
                    .OrderByDescending(d => d)
                    .ToArray();

                foreach (var indexPath in cacheIndexes)
                {
                    var levelFile = Path.Combine(indexPath, "level");
                    var sizeFile = Path.Combine(indexPath, "size");

                    if (File.Exists(levelFile) && File.Exists(sizeFile))
                    {
                        var levelText = File.ReadAllText(levelFile).Trim();
                        var sizeText = File.ReadAllText(sizeFile).Trim();

                        if (int.TryParse(levelText, out var level) && level == 3)
                        {
                            // Parse size (usually in format like "8192K")
                            if (ParseLinuxCacheSize(sizeText, out var size))
                            {
                                return size;
                            }
                        }
                    }
                }
            }

            // Fallback: Try /proc/cpuinfo
            var cpuinfoPath = "/proc/cpuinfo";
            if (File.Exists(cpuinfoPath))
            {
                var lines = File.ReadAllLines(cpuinfoPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("cache size", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("cache_size", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1 && ParseLinuxCacheSize(parts[1].Trim(), out var size))
                        {
                            return size;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return 8 * 1024 * 1024; // 8MB default
    }

    private static bool ParseLinuxCacheSize(string sizeText, out long size)
    {
        size = 0;
        if (string.IsNullOrEmpty(sizeText))
        {
            return false;
        }

        sizeText = sizeText.ToUpperInvariant().Trim();

        if (sizeText.EndsWith("KB", StringComparison.Ordinal) || sizeText.EndsWith('K'))
        {
            var numericPart = sizeText.Replace("KB", "", StringComparison.Ordinal).Replace("K", "", StringComparison.Ordinal).Trim();
            if (long.TryParse(numericPart, out var kb))
            {
                size = kb * 1024;
                return true;
            }
        }
        else if (sizeText.EndsWith("MB", StringComparison.Ordinal) || sizeText.EndsWith('M'))
        {
            var numericPart = sizeText.Replace("MB", "", StringComparison.Ordinal).Replace("M", "", StringComparison.Ordinal).Trim();
            if (long.TryParse(numericPart, out var mb))
            {
                size = mb * 1024 * 1024;
                return true;
            }
        }
        else if (long.TryParse(sizeText, out var bytes))
        {
            size = bytes;
            return true;
        }

        return false;
    }

    private static long GetCacheSizeMacOS()
    {
        try
        {
            // Use sysctl to get L3 cache size
            using (var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.l3cachesize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                _ = process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && long.TryParse(output.Trim(), out var cacheSize) && cacheSize > 0)
                {
                    return cacheSize;
                }
            }

            // Fallback: Try L2 cache size if L3 not available
            using (var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.l2cachesize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                _ = process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && long.TryParse(output.Trim(), out var cacheSize) && cacheSize > 0)
                {
                    return cacheSize;
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return 8 * 1024 * 1024; // 8MB default
    }

    private static int GetNumaNodeCount() => NumaInfo.Topology.NodeCount;

    private static int GetCacheLineSize() => 64; // Most modern CPUs use 64-byte cache lines

    private static CoreKernelDefinition ConvertToCoreKernelDefinition(KernelDefinition definition)
    {
        // Convert from Abstractions to Core types
        // Since we're using Abstractions now, we can use the definition directly
        var source = definition.Code;

        var sourceCode = source ?? string.Empty;
        var kernelSource = new TextKernelSource(
            code: sourceCode,
            name: definition.Name,
            language: KernelLanguage.CSharpIL,
            entryPoint: definition.EntryPoint ?? "main",
            dependencies: []
        );

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false,
            AdditionalFlags = [],
            Defines = []
        };

        var coreDefinition = new CoreKernelDefinition(definition.Name, kernelSource.Code, kernelSource.EntryPoint);

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
internal sealed class CompiledKernelAdapter(CoreICompiledKernel coreKernel) : ICompiledKernel
{
    private readonly CoreICompiledKernel _coreKernel = coreKernel ?? throw new ArgumentNullException(nameof(coreKernel));

    public string Name => _coreKernel.Name;

    public async ValueTask ExecuteAsync(DotCompute.Abstractions.Kernels.KernelArguments arguments, CancellationToken cancellationToken = default) => await _coreKernel.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync() => _coreKernel.DisposeAsync();
}
