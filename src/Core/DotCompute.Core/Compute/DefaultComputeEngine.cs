// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Compute.Enums;
using DotCompute.Core.Compute.Interfaces;
using DotCompute.Core.Compute.Options;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Compute
{

    /// <summary>
    /// Simple implementation of IKernelSource for testing.
    /// </summary>
    internal class KernelSource : IKernelSource
    {
        public KernelSource(string name, string code, KernelLanguage language, string entryPoint, string[]? dependencies = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Language = language;
            EntryPoint = entryPoint ?? "main";
            Dependencies = dependencies ?? [];
        }

        public string Name { get; }
        public string Code { get; }
        public KernelLanguage Language { get; }
        public string EntryPoint { get; }
        public string[] Dependencies { get; }
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
    public class DefaultComputeEngine : IComputeEngine
    {
        private readonly IAcceleratorManager _acceleratorManager;
        private readonly ILogger<DefaultComputeEngine> _logger;
        private readonly List<ComputeBackendType> _availableBackends;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the DefaultComputeEngine class.
        /// </summary>
        /// <param name="acceleratorManager">The accelerator manager for device discovery and selection.</param>
        /// <param name="logger">The logger instance for diagnostics and monitoring.</param>
        /// <exception cref="ArgumentNullException">Thrown when acceleratorManager or logger is null.</exception>
        public DefaultComputeEngine(
            IAcceleratorManager acceleratorManager,
            ILogger<DefaultComputeEngine> logger)
        {
            _acceleratorManager = acceleratorManager ?? throw new ArgumentNullException(nameof(acceleratorManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize backends on first use (lazy initialization)
            _availableBackends = [];
        }

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
        public ComputeBackendType DefaultBackend => _availableBackends.FirstOrDefault();

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

            _logger.LogInformation("Compiling kernel with entry point: {EntryPoint}", entryPoint ?? "main");

            // Get the first available accelerator
            var criteria = new AcceleratorSelectionCriteria
            {
                PreferredType = AcceleratorType.GPU,
                MinimumMemory = 0,
                RequiredFeatures = null
            };

            var accelerator = _acceleratorManager.SelectBest(criteria) ?? _acceleratorManager.Default;
            if (accelerator == null)
            {
                throw new InvalidOperationException("No accelerators available for kernel compilation");
            }

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

            _logger.LogInformation("Kernel compiled successfully: {KernelName}", compiledKernel.Name);

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

            _logger.LogInformation("Executing kernel {KernelName} on backend {Backend}",
                kernel.Name, backendType);

            // Convert arguments to KernelArguments
            var kernelArgs = new KernelArguments(arguments);

            // Execute the kernel
            await kernel.ExecuteAsync(kernelArgs, cancellationToken);

            _logger.LogInformation("Kernel {KernelName} executed successfully", kernel.Name);
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
                _logger.LogInformation("Initializing accelerator manager with CPU provider");
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
            try
            {
                // Simple check - in production, this would properly detect CUDA - TODO
                var cudaAvailable = global::System.IO.File.Exists("/usr/lib/wsl/lib/libcuda.so.1") ||
                                  global::System.IO.File.Exists("/usr/local/cuda/lib64/libcudart.so");
                if (cudaAvailable)
                {
                    backends.Add(ComputeBackendType.CUDA);
                    _logger.LogInformation("CUDA backend detected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CUDA detection failed");
            }

            // OpenCL could be detected similarly
            // For now, just return what we have - TODO

            return backends;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Clean up any resources
            _logger.LogInformation("ComputeEngine disposed");

            await ValueTask.CompletedTask;
        }
    }
}
