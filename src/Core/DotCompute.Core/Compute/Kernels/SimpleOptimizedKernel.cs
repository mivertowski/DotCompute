// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Compute.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
namespace DotCompute.Core.Compute.Kernels
{
    /// <summary>
    /// Simple optimized kernel for the high-performance provider.
    /// Provides basic kernel execution capabilities with logging and resource management.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SimpleOptimizedKernel"/> class.
    /// </remarks>
    /// <param name="name">Name of the kernel.</param>
    /// <param name="kernelInfo">Parsed kernel information.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="logger">Logger instance.</param>
#pragma warning disable CS9113 // Parameters reserved for enhanced kernel optimization and validation features
    internal class SimpleOptimizedKernel(string name, KernelInfo _kernelInfo, CompilationOptions _options, ILogger logger) : ICompiledKernel
#pragma warning restore CS9113
    {
        private readonly ILogger _logger = logger;
        private bool _disposed;

        /// <summary>
        /// Gets the kernel unique identifier.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the kernel name.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Executes the kernel with the provided arguments.
        /// </summary>
        /// <param name="arguments">Kernel execution arguments.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when execution finishes.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
        public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _logger.LogDebugMessage("Executing simple optimized kernel: {Name}");

            // Simple implementation - in a real scenario this would perform optimized execution
            // based on the kernel type and arguments - TODO
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Disposes the kernel and releases associated resources.
        /// </summary>
        /// <returns>Completed task when disposal finishes.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Disposes the kernel and releases associated resources.
        /// </summary>
        public void Dispose() => _disposed = true;
    }
}
