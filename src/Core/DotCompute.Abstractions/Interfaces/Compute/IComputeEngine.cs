// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Compute.Enums;
using DotCompute.Abstractions.Compute.Options;
namespace DotCompute.Abstractions.Interfaces.Compute
{
    /// <summary>
    /// Provides a unified interface for kernel compilation and execution across different compute backends.
    /// This interface serves as the main entry point for all compute operations in the DotCompute framework.
    /// </summary>
    /// <remarks>
    /// The IComputeEngine interface abstracts the complexity of different compute backends (CPU, GPU, etc.)
    /// and provides a consistent API for kernel compilation and execution. It supports asynchronous operations
    /// for better performance and scalability.
    /// </remarks>
    public interface IComputeEngine : IAsyncDisposable
    {
        /// <summary>
        /// Compiles a kernel from source code asynchronously.
        /// </summary>
        /// <param name="kernelSource">The kernel source code to compile. Must be valid according to the target backend's specification.</param>
        /// <param name="entryPoint">Optional entry point function name. If null, uses default entry point detection.</param>
        /// <param name="options">Optional compilation options that control optimization level, debugging, and other compilation behaviors.</param>
        /// <param name="cancellationToken">Token to observe for cancellation requests during compilation.</param>
        /// <returns>A ValueTask containing the compiled kernel that can be executed on compute backends.</returns>
        /// <exception cref="ArgumentNullException">Thrown when kernelSource is null or empty.</exception>
        /// <exception cref="CompilationException">Thrown when the kernel source contains compilation errors.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
        public ValueTask<ICompiledKernel> CompileKernelAsync(
            string kernelSource,
            string? entryPoint = null,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a compiled kernel on the specified backend asynchronously.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute. Must be a valid compiled kernel from CompileKernelAsync.</param>
        /// <param name="arguments">Array of arguments to pass to the kernel. Types must match kernel parameter expectations.</param>
        /// <param name="backendType">The compute backend to use for execution (CPU, CUDA, OpenCL, etc.).</param>
        /// <param name="options">Optional execution options that control work group sizes, profiling, and execution priority.</param>
        /// <param name="cancellationToken">Token to observe for cancellation requests during execution.</param>
        /// <returns>A ValueTask representing the asynchronous execution operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when kernel or arguments are null.</exception>
        /// <exception cref="ArgumentException">Thrown when arguments don't match kernel parameter requirements.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the specified backend is not available.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
        public ValueTask ExecuteAsync(
            ICompiledKernel kernel,
            object[] arguments,
            ComputeBackendType backendType,
            ExecutionOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of compute backends available on the current system.
        /// </summary>
        /// <value>
        /// An array of ComputeBackendType values representing the backends that are currently
        /// available and can be used for kernel execution. This list is determined at runtime
        /// based on the system capabilities and installed drivers.
        /// </value>
        /// <remarks>
        /// The availability of backends depends on system configuration, installed drivers,
        /// and hardware capabilities. For example, CUDA backend requires NVIDIA drivers,
        /// while Metal backend is only available on Apple platforms.
        /// </remarks>
        public IReadOnlyList<ComputeBackendType> AvailableBackends { get; }

        /// <summary>
        /// Gets the default backend for the current system.
        /// </summary>
        /// <value>
        /// The ComputeBackendType that represents the most optimal backend for the current
        /// system configuration. This is typically determined by performance characteristics
        /// and availability of compute resources.
        /// </value>
        /// <remarks>
        /// The default backend is selected based on system capabilities and performance
        /// characteristics. The selection algorithm prioritizes GPU backends when available
        /// and falls back to optimized CPU execution when dedicated compute hardware is not present.
        /// </remarks>
        public ComputeBackendType DefaultBackend { get; }
    }
}
