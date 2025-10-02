// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using KernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
using DotCompute.Abstractions.Models.Device;
namespace DotCompute.Abstractions.Interfaces.Device
{
    /// <summary>
    /// Represents a compute device capable of executing kernels.
    /// This is the main interface that provides access to device capabilities,
    /// memory management, kernel compilation, and command queue creation.
    /// </summary>
    public interface IComputeDevice : IAsyncDisposable
    {
        /// <summary>
        /// Gets the unique device identifier.
        /// </summary>
        /// <value>A string uniquely identifying this device.</value>
        public string Id { get; }

        /// <summary>
        /// Gets the human-readable device name.
        /// </summary>
        /// <value>The display name of the device.</value>
        public string Name { get; }

        /// <summary>
        /// Gets the type of compute device.
        /// </summary>
        /// <value>The device type (CPU, GPU, FPGA, etc.).</value>
        public ComputeDeviceType Type { get; }

        /// <summary>
        /// Gets the device capabilities information.
        /// </summary>
        /// <value>An object providing access to device capability details.</value>
        public IDeviceCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the current operational status of the device.
        /// </summary>
        /// <value>The current device status.</value>
        public DeviceStatus Status { get; }

        /// <summary>
        /// Gets device memory information and statistics.
        /// </summary>
        /// <value>An object providing access to memory-related information.</value>
        public IDeviceMemoryInfo MemoryInfo { get; }

        /// <summary>
        /// Compiles a kernel definition for execution on this device.
        /// </summary>
        /// <param name="kernel">The kernel definition to compile.</param>
        /// <param name="options">Optional compilation options.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous compilation operation, containing the compiled kernel.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="kernel"/> is null.</exception>
        /// <exception cref="DotCompute.Abstractions.CompilationException">Thrown when kernel compilation fails.</exception>
        public ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition kernel,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Allocates memory on the device with the specified size and access mode.
        /// </summary>
        /// <param name="sizeInBytes">The size of memory to allocate in bytes.</param>
        /// <param name="access">The memory access mode.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous allocation operation, containing the allocated device memory.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizeInBytes"/> is negative or zero.</exception>
        /// <exception cref="OutOfMemoryException">Thrown when there is insufficient device memory available.</exception>
        public ValueTask<IDeviceMemory> AllocateMemoryAsync(
            long sizeInBytes,
            MemoryAccess access,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a command queue for executing kernels and memory operations.
        /// </summary>
        /// <param name="options">Optional queue creation options.</param>
        /// <returns>A new command queue associated with this device.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the device is not available for queue creation.</exception>
        public ICommandQueue CreateCommandQueue(CommandQueueOptions? options = null);

        /// <summary>
        /// Synchronizes all pending operations on the device, waiting for completion.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous synchronization operation.</returns>
        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current performance metrics and statistics for the device.
        /// </summary>
        /// <returns>An object containing current device metrics.</returns>
        public IDeviceMetrics GetMetrics();
    }
}
