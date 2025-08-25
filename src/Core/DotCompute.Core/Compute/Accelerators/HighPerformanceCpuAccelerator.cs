// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute.Memory;
using DotCompute.Core.Compute.Parsing;
using DotCompute.Core.Compute.Kernels;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using AcceleratorType = DotCompute.Abstractions.AcceleratorType;
using CompilationOptions = DotCompute.Abstractions.CompilationOptions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using KernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;

namespace DotCompute.Core.Compute.Accelerators
{
    /// <summary>
    /// High-performance CPU accelerator that tries to use optimized implementations when available.
    /// Provides kernel compilation and execution capabilities with memory management.
    /// </summary>
    internal class HighPerformanceCpuAccelerator : IAccelerator
    {
        private readonly ILogger _logger;
        private readonly IUnifiedMemoryManager _memoryManager;
        private readonly OpenCLKernelParser _kernelParser;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HighPerformanceCpuAccelerator"/> class.
        /// </summary>
        /// <param name="info">Accelerator information.</param>
        /// <param name="logger">Logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when info or logger is null.</exception>
        public HighPerformanceCpuAccelerator(AcceleratorInfo info, ILogger logger)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryManager = new CpuMemoryManager(this, logger as ILogger<CpuMemoryManager> ?? 
                                                      throw new ArgumentException("Logger must be ILogger<CpuMemoryManager>", nameof(logger)));
            _kernelParser = new OpenCLKernelParser(logger);
        }

        /// <summary>
        /// Gets the accelerator information.
        /// </summary>
        public AcceleratorInfo Info { get; }

        /// <summary>
        /// Gets the accelerator type.
        /// </summary>
        public AcceleratorType Type => AcceleratorType.CPU;

        /// <summary>
        /// Gets the memory manager for this accelerator.
        /// </summary>
        public IUnifiedMemoryManager Memory => _memoryManager;

        /// <summary>
        /// Gets the accelerator context.
        /// </summary>
        public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

        /// <summary>
        /// Compiles a kernel for execution on this accelerator.
        /// </summary>
        /// <param name="definition">Kernel definition containing source code and metadata.</param>
        /// <param name="options">Compilation options for optimization control.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Compiled kernel ready for execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when kernel compilation fails.</exception>
        public ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            options ??= new CompilationOptions();

            _logger.LogInformation("Compiling high-performance CPU kernel: {KernelName}", definition.Name);

            try
            {
                // Parse kernel source and create optimized implementation
                var sourceCode = definition.Code ?? "";
                var kernelInfo = _kernelParser.ParseKernel(sourceCode, definition.EntryPoint ?? "main");
                var optimizedKernel = CreateOptimizedKernel(kernelInfo, options);

                _logger.LogInformation("Successfully compiled optimized CPU kernel: {KernelName}", definition.Name);
                return ValueTask.FromResult(optimizedKernel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile high-performance CPU kernel: {KernelName}", definition.Name);
                throw new InvalidOperationException($"Kernel compilation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates an optimized kernel implementation based on the parsed kernel information.
        /// </summary>
        /// <param name="kernelInfo">Parsed kernel information.</param>
        /// <param name="options">Compilation options.</param>
        /// <returns>Optimized compiled kernel.</returns>
        private ICompiledKernel CreateOptimizedKernel(Types.KernelInfo kernelInfo, CompilationOptions options)
            // For now, just create a simple kernel since we moved the optimized ones to Backends.CPU
            // In a full implementation, this would try to load the CPU backend dynamically

            => new SimpleOptimizedKernel(kernelInfo.Name, kernelInfo, options, _logger);

        /// <summary>
        /// Synchronizes all pending operations on this accelerator.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when synchronization finishes.</returns>
        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        /// <summary>
        /// Disposes the accelerator and releases all associated resources.
        /// </summary>
        /// <returns>Completed task when disposal finishes.</returns>
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
}
