// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using AcceleratorType = DotCompute.Abstractions.AcceleratorType;
using CompilationOptions = DotCompute.Abstractions.CompilationOptions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using KernelArguments = DotCompute.Abstractions.Kernels.KernelArguments;
using KernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Compute
{

    /// <summary>
    /// Provides CPU accelerator instances.
    /// </summary>
    public class CpuAcceleratorProvider(ILogger<CpuAcceleratorProvider> logger) : IAcceleratorProvider
    {
        private readonly ILogger<CpuAcceleratorProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public string Name => "CPU";

        public AcceleratorType[] SupportedTypes => [AcceleratorType.CPU];
        private static readonly char[] _separator = [' '];

        public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Discovering CPU accelerators");

            var cpuInfo = new AcceleratorInfo(
                AcceleratorType.CPU,
                GetProcessorName(),
                "N/A", // driver version
                GetAvailableMemory(), // memory size
                Environment.ProcessorCount, // compute units
                0, // max clock frequency
                GetProcessorCapability(), // compute capability
                GetAvailableMemory() / 4, // max shared memory per block
                true // is unified memory
            );

            // Create a CPU accelerator implementation
            var accelerator = new CpuAccelerator(cpuInfo, _logger);
            return ValueTask.FromResult<IEnumerable<IAccelerator>>(new[] { accelerator });
        }

        public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
        {
            if (info.DeviceType != "CPU")
            {
                throw new ArgumentException("Can only create CPU accelerators", nameof(info));
            }

            var accelerator = new SimpleCpuAccelerator(info, _logger);
            return ValueTask.FromResult<IAccelerator>(accelerator);
        }

        private static string GetProcessorName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux CPU"; // Would need to parse /proc/cpuinfo
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "macOS CPU"; // Would need to use sysctl
            }
            else
            {
                return "Unknown CPU";
            }
        }

        private static string GetProcessorVendor()
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            return arch switch
            {
                Architecture.X64 or Architecture.X86 => "x86",
                Architecture.Arm or Architecture.Arm64 => "ARM",
                _ => "Unknown"
            };
        }

        private static Version GetProcessorCapability()
        {
            // Simple version based on SIMD support
            if (global::System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            {
                return new Version(2, 0);
            }
            else if (global::System.Runtime.Intrinsics.X86.Sse41.IsSupported)
            {
                return new Version(1, 4);
            }
            else
            {
                return global::System.Numerics.Vector.IsHardwareAccelerated ? new Version(1, 0) : new Version(0, 1);
            }
        }

        private static long GetAvailableMemory()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsAvailableMemory();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetLinuxAvailableMemory();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacOSAvailableMemory();
                }
                else
                {
                    // Fallback: use conservative estimate
                    return GetFallbackMemory();
                }
            }
            catch
            {
                // If platform-specific detection fails, return conservative estimate
                return GetFallbackMemory();
            }
        }

        private static long GetWindowsAvailableMemory()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "wmic";
                process.StartInfo.Arguments = "OS get TotalVisibleMemorySize /value";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                _ = process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("TotalVisibleMemorySize"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2 && long.TryParse(parts[1].Trim(), out var kb))
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
            return GetFallbackMemory();
        }

        private static long GetLinuxAvailableMemory()
        {
            try
            {
                if (global::System.IO.File.Exists("/proc/meminfo"))
                {
                    var lines = global::System.IO.File.ReadAllLines("/proc/meminfo");
                    var availableLine = lines.FirstOrDefault(l => l.StartsWith("MemAvailable:"));
                    if (availableLine != null)
                    {
                        var parts = availableLine.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
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
            return GetFallbackMemory();
        }

        private static long GetMacOSAvailableMemory()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "sysctl";
                process.StartInfo.Arguments = "hw.memsize";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                _ = process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var parts = output.Split(':');
                if (parts.Length == 2 && long.TryParse(parts[1].Trim(), out var bytes))
                {
                    return bytes;
                }
            }
            catch
            {
                // Fall through to default
            }
            return GetFallbackMemory();
        }

        private static long GetFallbackMemory()
        {
            // Conservative fallback based on available managed memory
            var managedMemory = GC.GetTotalMemory(false);
            // Assume we can use 8x the managed heap size or at least 2GB
            return Math.Max(managedMemory * 8, 2L * 1024 * 1024 * 1024);
        }
    }

    /// <summary>
    /// CPU accelerator implementation for Core.
    /// </summary>
    internal class CpuAccelerator : IAccelerator
    {
        private readonly ILogger _logger;
        private readonly CpuMemoryManager _memoryManager;
        private bool _disposed;

        public CpuAccelerator(AcceleratorInfo info, ILogger logger)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryManager = new CpuMemoryManager(this, logger as ILogger<CpuMemoryManager> ?? new LoggerFactory().CreateLogger<CpuMemoryManager>());
        }

        public AcceleratorInfo Info { get; }

        public AcceleratorType Type => AcceleratorType.CPU;

        public IUnifiedMemoryManager Memory => _memoryManager;

        public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

        public async ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            options ??= new CompilationOptions();

            _logger.LogDebug("Compiling CPU kernel: {KernelName}", definition.Name);

            // Create a compiled kernel that does basic execution
            var compiledKernel = new CpuCompiledKernel(definition.Name, definition);

            _logger.LogInformation("Successfully compiled CPU kernel: {KernelName}", definition.Name);
            return await ValueTask.FromResult<ICompiledKernel>(compiledKernel);
        }

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
            // CPU operations are synchronous by default
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _memoryManager?.Dispose();
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// CPU compiled kernel implementation.
    /// </summary>
    internal class CpuCompiledKernel(string name, KernelDefinition definition) : ICompiledKernel
    {
        private readonly KernelDefinition _definition = definition;
        private bool _disposed;

        public string Name { get; } = name;

        public ValueTask ExecuteAsync(
            KernelArguments arguments,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CpuCompiledKernel));
            }

            // Simple kernel execution - in a real implementation,
            // this would parse and execute the kernel code
            // For now, this is a no-op placeholder
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
