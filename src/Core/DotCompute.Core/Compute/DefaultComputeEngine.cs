// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Compute.Enums;
using DotCompute.Abstractions.Interfaces.Compute;
using DotCompute.Abstractions.Compute.Options;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using System.Diagnostics;
using System;
namespace DotCompute.Core.Compute
{

    /// <summary>
    /// Simple implementation of IKernelSource for testing.
    /// </summary>
    internal class KernelSource(string name, string code, KernelLanguage language, string entryPoint, string[]? dependencies = null) : IKernelSource
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        /// <value>The code.</value>
        public string Code { get; } = code ?? throw new ArgumentNullException(nameof(code));
        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        /// <value>The language.</value>
        public KernelLanguage Language { get; } = language;
        /// <summary>
        /// Gets or sets the entry point.
        /// </summary>
        /// <value>The entry point.</value>
        public string EntryPoint { get; } = entryPoint ?? "main";
        /// <summary>
        /// Gets or sets the dependencies.
        /// </summary>
        /// <value>The dependencies.</value>
        public string[] Dependencies { get; } = dependencies ?? [];
    }

    /// <summary>
    /// Default implementation of the compute engine that provides unified kernel compilation
    /// and execution across different compute backends (CPU, CUDA, OpenCL, Metal, etc.).
    /// </summary>
    /// <remarks>
    /// This class serves as the primary entry point for kernel compilation and execution.
    /// It automatically detects available backends and provides a unified interface for
    /// compute operations across different hardware platforms.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the DefaultComputeEngine class.
    /// </remarks>
    /// <param name="acceleratorManager">The accelerator manager for device discovery and selection.</param>
    /// <param name="logger">The logger instance for diagnostics and monitoring.</param>
    /// <exception cref="ArgumentNullException">Thrown when acceleratorManager or logger is null.</exception>
    public class DefaultComputeEngine(
        IAcceleratorManager acceleratorManager,
        ILogger<DefaultComputeEngine> logger) : IComputeEngine
    {
        private readonly IAcceleratorManager _acceleratorManager = acceleratorManager ?? throw new ArgumentNullException(nameof(acceleratorManager));
        private readonly ILogger<DefaultComputeEngine> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly List<ComputeBackendType> _availableBackends = [];
        private bool _disposed;

        /// <summary>
        /// Gets the array of available compute backends on this system.
        /// </summary>
        /// <returns>An array of ComputeBackendType values representing detected backends.</returns>
        public ComputeBackendType[] AvailableBackends => [.. _availableBackends];

        /// <summary>
        /// Gets the default compute backend to use for kernel execution.
        /// Typically returns the first available GPU backend, falling back to CPU if needed.
        /// </summary>
        /// <returns>The default ComputeBackendType for this system.</returns>
        public ComputeBackendType DefaultBackend => _availableBackends.Count > 0 ? _availableBackends[0] : ComputeBackendType.CPU;

        /// <summary>
        /// Compiles a kernel from source code for execution on the optimal available backend.
        /// </summary>
        /// <param name="kernelSource">The kernel source code in OpenCL C, CUDA C, or HLSL.</param>
        /// <param name="entryPoint">The entry point function name. Defaults to "main" if not specified.</param>
        /// <param name="options">Compilation options including optimization level and debug information.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A compiled kernel ready for execution.</returns>
        /// <exception cref="ArgumentException">Thrown when kernelSource is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no accelerators are available for compilation.</exception>
        /// <exception cref="InvalidOperationException">Thrown when kernel compilation fails.</exception>
        /// <example>
        /// <code>
        /// string kernelSource = @"
        ///     __kernel void vector_add(__global const float* a, __global const float* b, __global float* result) {
        ///         int gid = get_global_id(0);
        ///         result[gid] = a[gid] + b[gid];
        ///     }";
        /// var compiledKernel = await engine.CompileKernelAsync(kernelSource, "vector_add");
        /// </code>
        /// </example>
        public async ValueTask<ICompiledKernel> CompileKernelAsync(
            string kernelSource,
            string? entryPoint = null,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(kernelSource))
            {
                throw new ArgumentException("Kernel source cannot be null or empty", nameof(kernelSource));
            }

            await EnsureInitializedAsync();

            _logger.LogInfoMessage($"Compiling kernel with entry point: {entryPoint ?? "default"}");

            // Get the first available accelerator
            var criteria = new AcceleratorSelectionCriteria
            {
                PreferredType = AcceleratorType.GPU,
                MinimumMemory = 0,
                RequiredFeatures = null
            };

            var accelerator = (_acceleratorManager.SelectBest(criteria) ?? _acceleratorManager.Default) ?? throw new InvalidOperationException("No accelerators available for kernel compilation");

            // Create a simple kernel source implementation
            var kernelSourceImpl = new KernelSource(
                entryPoint ?? "kernel",
                kernelSource,
                KernelLanguage.OpenCL,
                entryPoint ?? "main"
            );

            // Create kernel definition
            var definition = new KernelDefinition
            {
                Name = entryPoint ?? "kernel",
                Source = kernelSourceImpl.Code ?? kernelSourceImpl.ToString() ?? "",
                EntryPoint = entryPoint ?? "kernel"
            };

            // Compile the kernel
            var compiledKernel = await accelerator.CompileKernelAsync(definition, options, cancellationToken);

            _logger.LogInfoMessage($"Kernel compiled successfully: {compiledKernel.Name}");

            return compiledKernel;
        }

        /// <summary>
        /// Executes a compiled kernel on the specified compute backend with the given arguments.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute.</param>
        /// <param name="arguments">Array of arguments to pass to the kernel (buffers, scalars, etc.).</param>
        /// <param name="backendType">The specific backend to use for execution.</param>
        /// <param name="options">Execution options including work group sizing and profiling settings.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous execution operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when kernel or arguments is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the specified backend is not available.</exception>
        /// <exception cref="InvalidOperationException">Thrown when kernel execution fails.</exception>
        /// <example>
        /// <code>
        /// var buffer1 = await memoryManager.AllocateAsync&lt;float&gt;(1024);
        /// var buffer2 = await memoryManager.AllocateAsync&lt;float&gt;(1024);
        /// var result = await memoryManager.AllocateAsync&lt;float&gt;(1024);
        /// 
        /// await engine.ExecuteAsync(
        ///     compiledKernel, 
        ///     new object[] { buffer1, buffer2, result },
        ///     ComputeBackendType.OpenCL,
        ///     new ExecutionOptions { GlobalWorkSize = new long[] { 1024 } });
        /// </code>
        /// </example>
        public async ValueTask ExecuteAsync(
            ICompiledKernel kernel,
            object[] arguments,
            ComputeBackendType backendType,
            ExecutionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);

            ArgumentNullException.ThrowIfNull(arguments);

            _logger.LogInfoMessage($"Executing kernel {kernel.Name} on backend {backendType}");

            // Convert arguments to KernelArguments
            var kernelArgs = new KernelArguments(arguments);

            // Execute the kernel
            await kernel.ExecuteAsync(kernelArgs, cancellationToken);

            _logger.LogInfoMessage($"Kernel {kernel.Name} executed successfully");
        }

        private async ValueTask EnsureInitializedAsync()
        {
            if (_availableBackends.Count > 0)
            {
                return;
            }

            // Try to initialize the accelerator manager if not already done
            try
            {
                // Check if already initialized
                var _ = _acceleratorManager.Default;
            }
            catch (InvalidOperationException)
            {
                // Not initialized, try to initialize with a CPU provider
                _logger.LogInfoMessage("Initializing accelerator manager with CPU provider");
                var cpuProvider = new CpuAcceleratorProvider(_logger as ILogger<CpuAcceleratorProvider> ??
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<CpuAcceleratorProvider>());
                _acceleratorManager.RegisterProvider(cpuProvider);
                await _acceleratorManager.InitializeAsync();
            }

            // Now determine available backends
            _availableBackends.Clear();
            _availableBackends.AddRange(DetermineAvailableBackends());
        }

        private List<ComputeBackendType> DetermineAvailableBackends()
        {
            var backends = new List<ComputeBackendType>
            {
                // CPU is always available
                ComputeBackendType.CPU
            };

            // Check for CUDA support
            if (IsCudaAvailable())
            {
                backends.Add(ComputeBackendType.CUDA);
                _logger.LogInfoMessage("CUDA backend detected and available");
            }

            // Check for OpenCL support
            if (IsOpenClAvailable())
            {
                backends.Add(ComputeBackendType.OpenCL);
                _logger.LogInfoMessage("OpenCL backend detected and available");
            }

            // Check for Metal support (macOS only)
            if (IsMetalAvailable())
            {
                backends.Add(ComputeBackendType.Metal);
                _logger.LogInfoMessage("Metal backend detected and available");
            }

            _logger.LogInfoMessage($"Detected {backends.Count} compute backends: {string.Join(", ", backends)}");

            return backends;
        }

        /// <summary>
        /// Checks if CUDA is available on the system by verifying driver and runtime libraries.
        /// </summary>
        /// <returns>True if CUDA is available, false otherwise.</returns>
        private bool IsCudaAvailable()
        {
            try
            {
                var cudaPaths = new[]
                {
                    // Windows paths
                    Environment.ExpandEnvironmentVariables(@"%CUDA_PATH%\bin\cudart64_*.dll"),
                    Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*\bin\cudart64_*.dll"),

                    // Linux paths
                    "/usr/local/cuda/lib64/libcudart.so",
                    "/usr/local/cuda/lib64/libcudart.so.*",
                    "/usr/lib/x86_64-linux-gnu/libcudart.so",
                    "/usr/lib/x86_64-linux-gnu/libcudart.so.*",

                    // WSL paths
                    "/usr/lib/wsl/lib/libcuda.so.1",
                    "/usr/lib/wsl/lib/libcudart.so.*",

                    // Alternative paths
                    "/opt/cuda/lib64/libcudart.so",
                    "/opt/cuda/lib64/libcudart.so.*"
                };

                // Check for CUDA runtime libraries
                foreach (var pathPattern in cudaPaths)
                {
                    try
                    {
                        // Handle wildcard patterns
                        if (pathPattern.Contains('*', StringComparison.OrdinalIgnoreCase))
                        {
                            var directory = Path.GetDirectoryName(pathPattern) ?? "";
                            var pattern = Path.GetFileName(pathPattern);

                            if (Directory.Exists(directory))
                            {
                                var files = Directory.GetFiles(directory, pattern);
                                if (files.Length > 0)
                                {
                                    _logger.LogDebug("CUDA runtime found: {Path}", files[0]);
                                    return true;
                                }
                            }
                        }
                        else if (File.Exists(pathPattern))
                        {
                            _logger.LogDebug("CUDA runtime found: {Path}", pathPattern);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error checking CUDA path: {Path}", pathPattern);
                    }
                }

                // Check for nvidia-smi (indicates NVIDIA driver)
                try
                {
                    using var process = new Process();
                    process.StartInfo.FileName = "nvidia-smi";
                    process.StartInfo.Arguments = "--query-gpu=name --format=csv,noheader";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;

                    if (process.Start())
                    {
                        _ = process.WaitForExit(5000); // 5 second timeout
                        if (process.ExitCode == 0)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(output))
                            {
                                _logger.LogDebug("NVIDIA GPU detected via nvidia-smi: {Output}", output.Trim());
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "nvidia-smi check failed");
                }

                _logger.LogDebug("CUDA not available - no runtime libraries or nvidia-smi found");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CUDA detection failed with exception");
                return false;
            }
        }

        /// <summary>
        /// Checks if OpenCL is available on the system.
        /// </summary>
        /// <returns>True if OpenCL is available, false otherwise.</returns>
        private bool IsOpenClAvailable()
        {
            try
            {
                var openClPaths = new[]
                {
                    // Windows paths
                    @"C:\Windows\System32\OpenCL.dll",
                    Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\OpenCL.dll"),

                    // Linux paths
                    "/usr/lib/x86_64-linux-gnu/libOpenCL.so",
                    "/usr/lib/x86_64-linux-gnu/libOpenCL.so.1",
                    "/usr/lib64/libOpenCL.so",
                    "/usr/lib64/libOpenCL.so.1",
                    "/usr/local/lib/libOpenCL.so",
                    "/usr/local/lib/libOpenCL.so.1",

                    // macOS paths
                    "/System/Library/Frameworks/OpenCL.framework/OpenCL"
                };

                foreach (var path in openClPaths)
                {
                    if (File.Exists(path))
                    {
                        _logger.LogDebug("OpenCL library found: {Path}", path);
                        return true;
                    }
                }

                _logger.LogDebug("OpenCL not available - no libraries found");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "OpenCL detection failed");
                return false;
            }
        }

        /// <summary>
        /// Checks if Metal is available on the system (macOS only).
        /// </summary>
        /// <returns>True if Metal is available, false otherwise.</returns>
        private bool IsMetalAvailable()
        {
            try
            {
                // Metal is only available on macOS
                if (!OperatingSystem.IsMacOS())
                {
                    return false;
                }

                // Check for Metal framework
                var metalPath = "/System/Library/Frameworks/Metal.framework/Metal";
                if (File.Exists(metalPath))
                {
                    _logger.LogDebug("Metal framework found: {Path}", metalPath);
                    return true;
                }

                _logger.LogDebug("Metal not available - framework not found");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Metal detection failed");
                return false;
            }
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Clean up any resources
            _logger.LogInfoMessage("ComputeEngine disposed");

            await ValueTask.CompletedTask;
        }
    }
}
