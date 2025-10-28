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
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

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
        public IReadOnlyList<string> Dependencies { get; } = dependencies ?? Array.Empty<string>();
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
    public partial class DefaultComputeEngine(
        IAcceleratorManager acceleratorManager,
        ILogger<DefaultComputeEngine> logger) : IComputeEngine
    {
#pragma warning disable CA2213 // Disposable fields should be disposed - Injected dependency, not owned by this class
        private readonly IAcceleratorManager _acceleratorManager = acceleratorManager ?? throw new ArgumentNullException(nameof(acceleratorManager));
#pragma warning restore CA2213
        private readonly ILogger<DefaultComputeEngine> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly List<ComputeBackendType> _availableBackends = [];
        private bool _disposed;

        // LoggerMessage delegates for high-performance logging (CA1848/XFIX003)
        // Event ID range: 16000-16099 (DefaultComputeEngine - Compute module)

        private static readonly Action<ILogger, string, Exception?> _logCompilingKernel =
            LoggerMessage.Define<string>(
                MsLogLevel.Information,
                new EventId(16000, nameof(CompilingKernel)),
                "Compiling kernel with entry point: {EntryPoint}");

        private static readonly Action<ILogger, string, Exception?> _logKernelCompiled =
            LoggerMessage.Define<string>(
                MsLogLevel.Information,
                new EventId(16001, nameof(KernelCompiled)),
                "Kernel compiled successfully: {KernelName}");

        private static readonly Action<ILogger, string, ComputeBackendType, Exception?> _logExecutingKernel =
            LoggerMessage.Define<string, ComputeBackendType>(
                MsLogLevel.Information,
                new EventId(16002, nameof(ExecutingKernel)),
                "Executing kernel {KernelName} on backend {BackendType}");

        private static readonly Action<ILogger, string, Exception?> _logKernelExecuted =
            LoggerMessage.Define<string>(
                MsLogLevel.Information,
                new EventId(16003, nameof(KernelExecuted)),
                "Kernel {KernelName} executed successfully");

        private static readonly Action<ILogger, Exception?> _logInitializingAcceleratorManager =
            LoggerMessage.Define(
                MsLogLevel.Information,
                new EventId(16004, nameof(InitializingAcceleratorManager)),
                "Initializing accelerator manager with CPU provider");

        private static readonly Action<ILogger, Exception?> _logCudaBackendDetected =
            LoggerMessage.Define(
                MsLogLevel.Information,
                new EventId(16005, nameof(CudaBackendDetected)),
                "CUDA backend detected and available");

        private static readonly Action<ILogger, Exception?> _logOpenClBackendDetected =
            LoggerMessage.Define(
                MsLogLevel.Information,
                new EventId(16006, nameof(OpenClBackendDetected)),
                "OpenCL backend detected and available");

        private static readonly Action<ILogger, Exception?> _logMetalBackendDetected =
            LoggerMessage.Define(
                MsLogLevel.Information,
                new EventId(16007, nameof(MetalBackendDetected)),
                "Metal backend detected and available");

        private static readonly Action<ILogger, int, string, Exception?> _logBackendsDetected =
            LoggerMessage.Define<int, string>(
                MsLogLevel.Information,
                new EventId(16008, nameof(BackendsDetected)),
                "Detected {BackendCount} compute backends: {Backends}");

        private static readonly Action<ILogger, string, Exception?> _logCudaRuntimeFound =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(16009, nameof(CudaRuntimeFound)),
                "CUDA runtime found: {Path}");

        private static readonly Action<ILogger, string, Exception?> _logCheckingCudaPath =
            LoggerMessage.Define<string>(
                MsLogLevel.Trace,
                new EventId(16010, nameof(CheckingCudaPath)),
                "Error checking CUDA path: {Path}");

        private static readonly Action<ILogger, string, Exception?> _logNvidiaGpuDetected =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(16011, nameof(NvidiaGpuDetected)),
                "NVIDIA GPU detected via nvidia-smi: {Output}");

        private static readonly Action<ILogger, Exception?> _logNvidiaSmiCheckFailed =
            LoggerMessage.Define(
                MsLogLevel.Trace,
                new EventId(16012, nameof(NvidiaSmiCheckFailed)),
                "nvidia-smi check failed");

        private static readonly Action<ILogger, Exception?> _logCudaNotAvailable =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(16013, nameof(CudaNotAvailable)),
                "CUDA not available - no runtime libraries or nvidia-smi found");

        private static readonly Action<ILogger, Exception?> _logCudaDetectionFailed =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(16014, nameof(CudaDetectionFailed)),
                "CUDA detection failed with exception");

        private static readonly Action<ILogger, string, Exception?> _logOpenClLibraryFound =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(16015, nameof(OpenClLibraryFound)),
                "OpenCL library found: {Path}");

        private static readonly Action<ILogger, Exception?> _logOpenClNotAvailable =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(16016, nameof(OpenClNotAvailable)),
                "OpenCL not available - no libraries found");

        private static readonly Action<ILogger, Exception?> _logOpenClDetectionFailed =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(16017, nameof(OpenClDetectionFailed)),
                "OpenCL detection failed");

        private static readonly Action<ILogger, string, Exception?> _logMetalFrameworkFound =
            LoggerMessage.Define<string>(
                MsLogLevel.Debug,
                new EventId(16018, nameof(MetalFrameworkFound)),
                "Metal framework found: {Path}");

        private static readonly Action<ILogger, Exception?> _logMetalNotAvailable =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(16019, nameof(MetalNotAvailable)),
                "Metal not available - framework not found");

        private static readonly Action<ILogger, Exception?> _logMetalDetectionFailed =
            LoggerMessage.Define(
                MsLogLevel.Debug,
                new EventId(16020, nameof(MetalDetectionFailed)),
                "Metal detection failed");

        private static readonly Action<ILogger, Exception?> _logComputeEngineDisposed =
            LoggerMessage.Define(
                MsLogLevel.Information,
                new EventId(16021, nameof(ComputeEngineDisposed)),
                "ComputeEngine disposed");

        // Wrapper methods for LoggerMessage delegates

        private static void CompilingKernel(ILogger logger, string entryPoint)
            => _logCompilingKernel(logger, entryPoint, null);

        private static void KernelCompiled(ILogger logger, string kernelName)
            => _logKernelCompiled(logger, kernelName, null);

        private static void ExecutingKernel(ILogger logger, string kernelName, ComputeBackendType backendType)
            => _logExecutingKernel(logger, kernelName, backendType, null);

        private static void KernelExecuted(ILogger logger, string kernelName)
            => _logKernelExecuted(logger, kernelName, null);

        private static void InitializingAcceleratorManager(ILogger logger)
            => _logInitializingAcceleratorManager(logger, null);

        private static void CudaBackendDetected(ILogger logger)
            => _logCudaBackendDetected(logger, null);

        private static void OpenClBackendDetected(ILogger logger)
            => _logOpenClBackendDetected(logger, null);

        private static void MetalBackendDetected(ILogger logger)
            => _logMetalBackendDetected(logger, null);

        private static void BackendsDetected(ILogger logger, int backendCount, string backends)
            => _logBackendsDetected(logger, backendCount, backends, null);

        private static void CudaRuntimeFound(ILogger logger, string path)
            => _logCudaRuntimeFound(logger, path, null);

        private static void CheckingCudaPath(ILogger logger, string path, Exception ex)
            => _logCheckingCudaPath(logger, path, ex);

        private static void NvidiaGpuDetected(ILogger logger, string output)
            => _logNvidiaGpuDetected(logger, output, null);

        private static void NvidiaSmiCheckFailed(ILogger logger, Exception ex)
            => _logNvidiaSmiCheckFailed(logger, ex);

        private static void CudaNotAvailable(ILogger logger)
            => _logCudaNotAvailable(logger, null);

        private static void CudaDetectionFailed(ILogger logger, Exception ex)
            => _logCudaDetectionFailed(logger, ex);

        private static void OpenClLibraryFound(ILogger logger, string path)
            => _logOpenClLibraryFound(logger, path, null);

        private static void OpenClNotAvailable(ILogger logger)
            => _logOpenClNotAvailable(logger, null);

        private static void OpenClDetectionFailed(ILogger logger, Exception ex)
            => _logOpenClDetectionFailed(logger, ex);

        private static void MetalFrameworkFound(ILogger logger, string path)
            => _logMetalFrameworkFound(logger, path, null);

        private static void MetalNotAvailable(ILogger logger)
            => _logMetalNotAvailable(logger, null);

        private static void MetalDetectionFailed(ILogger logger, Exception ex)
            => _logMetalDetectionFailed(logger, ex);

        private static void ComputeEngineDisposed(ILogger logger)
            => _logComputeEngineDisposed(logger, null);

        /// <summary>
        /// Gets the array of available compute backends on this system.
        /// </summary>
        /// <returns>An array of ComputeBackendType values representing detected backends.</returns>
        public IReadOnlyList<ComputeBackendType> AvailableBackends => _availableBackends.AsReadOnly();

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

            CompilingKernel(_logger, entryPoint ?? "default");

            // Get the first available accelerator
            var criteria = new AcceleratorSelectionCriteria
            {
                PreferredType = AcceleratorType.GPU,
                MinimumMemory = 0,
                RequiredFeatures = null
            };

            var accelerator = (_acceleratorManager.SelectBest(criteria) ?? _acceleratorManager.DefaultAccelerator) ?? throw new InvalidOperationException("No accelerators available for kernel compilation");

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

            KernelCompiled(_logger, compiledKernel.Name);

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

            ExecutingKernel(_logger, kernel.Name, backendType);

            // Convert arguments to KernelArguments
            var kernelArgs = new KernelArguments(arguments);

            // Execute the kernel
            await kernel.ExecuteAsync(kernelArgs, cancellationToken);

            KernelExecuted(_logger, kernel.Name);
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
                var _ = _acceleratorManager.DefaultAccelerator;
            }
            catch (InvalidOperationException)
            {
                // Not initialized, try to initialize with a CPU provider
                InitializingAcceleratorManager(_logger);
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
                CudaBackendDetected(_logger);
            }

            // Check for OpenCL support
            if (IsOpenClAvailable())
            {
                backends.Add(ComputeBackendType.OpenCL);
                OpenClBackendDetected(_logger);
            }

            // Check for Metal support (macOS only)
            if (IsMetalAvailable())
            {
                backends.Add(ComputeBackendType.Metal);
                MetalBackendDetected(_logger);
            }

            BackendsDetected(_logger, backends.Count, string.Join(", ", backends));

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
                                    CudaRuntimeFound(_logger, files[0]);
                                    return true;
                                }
                            }
                        }
                        else if (File.Exists(pathPattern))
                        {
                            CudaRuntimeFound(_logger, pathPattern);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        CheckingCudaPath(_logger, pathPattern, ex);
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
                                NvidiaGpuDetected(_logger, output.Trim());
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    NvidiaSmiCheckFailed(_logger, ex);
                }

                CudaNotAvailable(_logger);
                return false;
            }
            catch (Exception ex)
            {
                CudaDetectionFailed(_logger, ex);
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
                        OpenClLibraryFound(_logger, path);
                        return true;
                    }
                }

                OpenClNotAvailable(_logger);
                return false;
            }
            catch (Exception ex)
            {
                OpenClDetectionFailed(_logger, ex);
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
                    MetalFrameworkFound(_logger, metalPath);
                    return true;
                }

                MetalNotAvailable(_logger);
                return false;
            }
            catch (Exception ex)
            {
                MetalDetectionFailed(_logger, ex);
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
            ComputeEngineDisposed(_logger);

            GC.SuppressFinalize(this);
            await ValueTask.CompletedTask;
        }
    }
}
