// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Memory;

namespace DotCompute.Runtime.Services;

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
    public Task<KernelValidationResult> ValidateKernelArgsAsync(string kernelName, params object[] args);
}