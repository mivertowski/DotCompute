// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Kernels;
using System;
namespace DotCompute.Core.Compute
{

    /// <summary>
    /// Provides CPU accelerator instances.
    /// </summary>
    public class CpuAcceleratorProvider(ILogger<CpuAcceleratorProvider> logger) : IAcceleratorProvider
    {
        private readonly ILogger<CpuAcceleratorProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private static readonly char[] _separator = [' '];

        /// <summary>
        /// Gets the name of this provider.
        /// </summary>
        public string Name => "CPU";

        /// <summary>
        /// Gets the types of accelerators this provider can create.
        /// </summary>
        public IReadOnlyList<AcceleratorType> SupportedTypes => new[] { AcceleratorType.CPU };

        /// <summary>
        /// Discovers available accelerators.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A list of discovered accelerators.
        /// </returns>
        public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInfoMessage("Discovering CPU accelerators");

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

        /// <summary>
        /// Creates an accelerator instance.
        /// </summary>
        /// <param name="info">The accelerator information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The created accelerator instance.
        /// </returns>
        /// <exception cref="ArgumentException">Can only create CPU accelerators - info</exception>
        public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
        {
            if (info.DeviceType != AcceleratorType.CPU.ToString())
            {
                throw new ArgumentException("Can only create CPU accelerators", nameof(info));
            }

            var accelerator = new CpuAccelerator(info, _logger);
            return ValueTask.FromResult<IAccelerator>(accelerator);
        }

        private static string GetProcessorName()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsCpuName();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetLinuxCpuName();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacOsCpuName();
                }
                else
                {
                    return GetFallbackCpuName();
                }
            }
            catch
            {
                return GetFallbackCpuName();
            }
        }

        private static string GetWindowsCpuName()
        {
            try
            {
                // Try WMI first for detailed CPU information
                using var process = new Process();
                process.StartInfo.FileName = "wmic";
                process.StartInfo.Arguments = "cpu get name /value";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                if (process.Start())
                {
                    _ = process.WaitForExit(5000); // 5 second timeout
                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        var lines = output.Split('\n');

                        foreach (var line in lines)
                        {
                            if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase) && line.Length > 5)
                            {
                                var cpuName = line[5..].Trim();
                                if (!string.IsNullOrWhiteSpace(cpuName))
                                {
                                    return cpuName;
                                }
                            }
                        }
                    }
                }

                // Fallback to environment variable
                var processorId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                if (!string.IsNullOrWhiteSpace(processorId))
                {
                    return processorId;
                }

                // Last resort: registry-based detection
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                    var processorName = key?.GetValue("ProcessorNameString")?.ToString();
                    if (!string.IsNullOrWhiteSpace(processorName))
                    {
                        return processorName;
                    }
                }
                catch
                {
                    // Registry access failed
                }

                return "Windows CPU (Unknown Model)";
            }
            catch
            {
                return "Windows CPU (Detection Failed)";
            }
        }

        private static string GetLinuxCpuName()
        {
            try
            {
                if (File.Exists("/proc/cpuinfo"))
                {
                    var lines = File.ReadAllLines("/proc/cpuinfo");

                    // Look for model name
                    var modelNameLine = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                    if (modelNameLine != null)
                    {
                        var colonIndex = modelNameLine.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                        if (colonIndex >= 0 && colonIndex < modelNameLine.Length - 1)
                        {
                            var modelName = modelNameLine[(colonIndex + 1)..].Trim();
                            if (!string.IsNullOrWhiteSpace(modelName))
                            {
                                return modelName;
                            }
                        }
                    }

                    // Fallback to processor line
                    var processorLine = lines.FirstOrDefault(l => l.StartsWith("processor", StringComparison.OrdinalIgnoreCase));
                    if (processorLine != null)
                    {
                        return "Linux CPU (from /proc/cpuinfo)";
                    }
                }

                // Try lscpu command as fallback
                try
                {
                    using var process = new Process();
                    process.StartInfo.FileName = "lscpu";
                    process.StartInfo.Arguments = "-p=ModelName";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;

                    if (process.Start())
                    {
                        _ = process.WaitForExit(3000); // 3 second timeout
                        if (process.ExitCode == 0)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            var lines = output.Split('\n');
                            var modelLine = lines.FirstOrDefault(l => !l.StartsWith('#') && !string.IsNullOrWhiteSpace(l));
                            if (!string.IsNullOrWhiteSpace(modelLine))
                            {
                                return modelLine.Trim();
                            }
                        }
                    }
                }
                catch
                {
                    // lscpu not available
                }

                return "Linux CPU (Unknown Model)";
            }
            catch
            {
                return "Linux CPU (Detection Failed)";
            }
        }

        private static string GetMacOsCpuName()
        {
            try
            {
                // Try sysctl for CPU brand string
                using var process = new Process();
                process.StartInfo.FileName = "sysctl";
                process.StartInfo.Arguments = "-n machdep.cpu.brand_string";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                if (process.Start())
                {
                    _ = process.WaitForExit(5000); // 5 second timeout
                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            return output;
                        }
                    }
                }

                // Fallback to CPU vendor and family info
                try
                {
                    using var vendorProcess = new Process();
                    vendorProcess.StartInfo.FileName = "sysctl";
                    vendorProcess.StartInfo.Arguments = "-n machdep.cpu.vendor";
                    vendorProcess.StartInfo.UseShellExecute = false;
                    vendorProcess.StartInfo.RedirectStandardOutput = true;
                    vendorProcess.StartInfo.CreateNoWindow = true;

                    if (vendorProcess.Start())
                    {
                        _ = vendorProcess.WaitForExit(3000);
                        if (vendorProcess.ExitCode == 0)
                        {
                            var vendor = vendorProcess.StandardOutput.ReadToEnd().Trim();
                            if (!string.IsNullOrWhiteSpace(vendor))
                            {
                                return $"macOS CPU ({vendor})";
                            }
                        }
                    }
                }
                catch
                {
                    // Vendor detection failed
                }

                return "macOS CPU (Unknown Model)";
            }
            catch
            {
                return "macOS CPU (Detection Failed)";
            }
        }

        private static string GetFallbackCpuName()
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            var archString = arch switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm => "ARM",
                Architecture.Arm64 => "ARM64",
                _ => "Unknown Architecture"
            };

            var cores = Environment.ProcessorCount;
            return $"Generic CPU ({archString}, {cores} cores)";
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
                    if (line.StartsWith("TotalVisibleMemorySize", StringComparison.CurrentCulture))
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
                    var availableLine = lines.FirstOrDefault(l => l.StartsWith("MemAvailable:", StringComparison.CurrentCulture));
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
        /// <summary>
        /// Initializes a new instance of the CpuAccelerator class.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <param name="logger">The logger.</param>

        public CpuAccelerator(AcceleratorInfo info, ILogger logger)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryManager = new CpuMemoryManager(this, logger as ILogger<CpuMemoryManager> ?? new LoggerFactory().CreateLogger<CpuMemoryManager>());
        }
        /// <summary>
        /// Gets or sets the info.
        /// </summary>
        /// <value>The info.</value>

        public AcceleratorInfo Info { get; }
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>

        public AcceleratorType Type => AcceleratorType.CPU;
        /// <summary>
        /// Gets or sets the device type.
        /// </summary>
        /// <value>The device type.</value>

        public string DeviceType => "CPU";
        /// <summary>
        /// Gets or sets the memory.
        /// </summary>
        /// <value>The memory.</value>

        public IUnifiedMemoryManager Memory => _memoryManager;
        /// <summary>
        /// Gets or sets the memory manager.
        /// </summary>
        /// <value>The memory manager.</value>

        public IUnifiedMemoryManager MemoryManager => _memoryManager;
        /// <summary>
        /// Gets or sets the context.
        /// </summary>
        /// <value>The context.</value>

        public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);
        /// <summary>
        /// Gets or sets a value indicating whether available.
        /// </summary>
        /// <value>The is available.</value>

        public bool IsAvailable => !_disposed;
        /// <summary>
        /// Gets compile kernel asynchronously.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            _ = options ?? new CompilationOptions();

            _logger.LogDebugMessage("Compiling CPU kernel: {definition.Name}");

            // Create a compiled kernel that does basic execution
            var compiledKernel = new CpuCompiledKernel(definition.Name, definition);

            _logger.LogInfoMessage("Successfully compiled CPU kernel: {definition.Name}");
            return await ValueTask.FromResult<ICompiledKernel>(compiledKernel);
        }
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
            // CPU operations are synchronous by default


            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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


        /// <summary>
        /// Gets the kernel unique identifier.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>

        public string Name { get; } = name;
        /// <summary>
        /// Gets execute asynchronously.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask ExecuteAsync(
            KernelArguments arguments,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Simple kernel execution - in a real implementation,
            // this would parse and execute the kernel code
            // For now, this is a no-op placeholder - TODO
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose() => _disposed = true;
    }
}
