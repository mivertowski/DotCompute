// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Device;

namespace DotCompute.Abstractions.Interfaces;

/// <summary>
/// Orchestrates kernel execution across multiple compute backends with automatic backend selection.
/// This interface bridges the generated kernel code with the runtime infrastructure.
/// </summary>
public interface IComputeOrchestrator
{
    /// <summary>
    /// Executes a kernel by name with automatic backend selection and parameter marshaling.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="kernelName">The fully qualified kernel name</param>
    /// <param name="args">The kernel arguments</param>
    /// <returns>The execution result</returns>
    public Task<T> ExecuteAsync<T>(string kernelName, params object[] args);

    /// <summary>
    /// Executes a kernel with explicit backend preference.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="kernelName">The fully qualified kernel name</param>
    /// <param name="preferredBackend">The preferred backend for execution</param>
    /// <param name="args">The kernel arguments</param>
    /// <returns>The execution result</returns>
    public Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args);

    /// <summary>
    /// Executes a kernel on a specific accelerator instance.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="kernelName">The fully qualified kernel name</param>
    /// <param name="accelerator">The specific accelerator to use</param>
    /// <param name="args">The kernel arguments</param>
    /// <returns>The execution result</returns>
    public Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args);

    /// <summary>
    /// Executes a kernel with unified buffer parameters for zero-copy optimization.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="kernelName">The fully qualified kernel name</param>
    /// <param name="buffers">Unified buffers for zero-copy execution</param>
    /// <param name="scalarArgs">Scalar arguments</param>
    /// <returns>The execution result</returns>
    public Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs);

    /// <summary>
    /// Gets the optimal accelerator for a specific kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name</param>
    /// <returns>The optimal accelerator or null if none suitable</returns>
    public Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName);

    /// <summary>
    /// Pre-compiles a kernel for improved runtime performance.
    /// </summary>
    /// <param name="kernelName">The kernel name to pre-compile</param>
    /// <param name="accelerator">Optional specific accelerator, or null for all suitable accelerators</param>
    /// <returns>A task representing the pre-compilation operation</returns>
    public Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null);

    /// <summary>
    /// Gets available accelerators that support a specific kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name</param>
    /// <returns>Available accelerators supporting the kernel</returns>
    public Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName);

    /// <summary>
    /// Validates kernel arguments before execution.
    /// </summary>
    /// <param name="kernelName">The kernel name</param>
    /// <param name="args">The arguments to validate</param>
    /// <returns>Validation result with any errors or warnings</returns>
    public Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args);

    /// <summary>
    /// Executes a kernel with advanced execution parameters.
    /// </summary>
    /// <param name="kernelName">The kernel name</param>
    /// <param name="executionParameters">Advanced execution parameters</param>
    /// <returns>The execution result</returns>
    public Task<object?> ExecuteKernelAsync(string kernelName, IKernelExecutionParameters executionParameters);

    /// <summary>
    /// Executes a kernel with cancellation token support.
    /// </summary>
    /// <param name="kernelName">The kernel name</param>
    /// <param name="args">The kernel arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public Task<object?> ExecuteKernelAsync(string kernelName, object[] args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the compute device and selected backend.
    /// </summary>
    /// <returns>Device information including GPU availability and backend selection.</returns>
    public DeviceInfo GetDeviceInfo() => DeviceInfo.CpuOnly;
}

/// <summary>
/// Interface for kernel execution parameters.
/// </summary>
public interface IKernelExecutionParameters
{
    /// <summary>Gets the kernel arguments.</summary>
    public IReadOnlyList<object> Arguments { get; }


    /// <summary>Gets the preferred backend.</summary>
    public string? PreferredBackend { get; }


    /// <summary>Gets the execution options.</summary>
    public IDictionary<string, object> Options { get; }


    /// <summary>Gets the cancellation token.</summary>
    public CancellationToken CancellationToken { get; }
}
