// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using CompilationOptions = DotCompute.Abstractions.CompilationOptions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using OptimizationLevel = DotCompute.Abstractions.OptimizationLevel;

namespace DotCompute.Core.Compute
{

/// <summary>
/// Provides a unified interface for kernel compilation and execution across different compute backends.
/// </summary>
public interface IComputeEngine : IAsyncDisposable
{
    /// <summary>
    /// Compiles a kernel from source code.
    /// </summary>
    public ValueTask<ICompiledKernel> CompileKernelAsync(
        string kernelSource,
        string? entryPoint = null,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a compiled kernel on the specified backend.
    /// </summary>
    public ValueTask ExecuteAsync(
        ICompiledKernel kernel,
        object[] arguments,
        ComputeBackendType backendType,
        ExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available compute backends.
    /// </summary>
    public ComputeBackendType[] AvailableBackends { get; }

    /// <summary>
    /// Gets the default backend for the current system.
    /// </summary>
    public ComputeBackendType DefaultBackend { get; }
}


/// <summary>
/// Compilation metadata for kernels.
/// </summary>
public interface ICompilationMetadata
{
    /// <summary>
    /// Gets the compilation timestamp.
    /// </summary>
    public DateTimeOffset CompilationTime { get; }

    /// <summary>
    /// Gets compilation options used.
    /// </summary>
    public CompilationOptions Options { get; }

    /// <summary>
    /// Gets any compilation warnings.
    /// </summary>
    public string[] Warnings { get; }

    /// <summary>
    /// Gets optimization level applied.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; }
}

/// <summary>
/// Compute backend types.
/// </summary>
public enum ComputeBackendType
{
    /// <summary>
    /// CPU backend using SIMD instructions.
    /// </summary>
    CPU,

    /// <summary>
    /// CUDA backend for NVIDIA GPUs.
    /// </summary>
    CUDA,

    /// <summary>
    /// OpenCL backend for cross-platform computing.
    /// </summary>
    OpenCL,

    /// <summary>
    /// Metal backend for Apple devices.
    /// </summary>
    Metal,

    /// <summary>
    /// Vulkan Compute backend.
    /// </summary>
    Vulkan,

    /// <summary>
    /// DirectCompute backend for Windows.
    /// </summary>
    DirectCompute
}

/// <summary>
/// Kernel execution options.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    /// Gets or sets the global work size.
    /// </summary>
    public long[]? GlobalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the local work size.
    /// </summary>
    public long[]? LocalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the work group offset.
    /// </summary>
    public long[]? WorkGroupOffset { get; set; }

    /// <summary>
    /// Gets or sets execution priority.
    /// </summary>
    public ExecutionPriority Priority { get; set; } = ExecutionPriority.Normal;

    /// <summary>
    /// Gets or sets whether to enable profiling.
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets timeout for execution.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets the default execution options.
    /// </summary>
    public static ExecutionOptions Default { get; } = new();
}

/// <summary>
/// Execution priority levels.
/// </summary>
public enum ExecutionPriority
{
    /// <summary>
    /// Low priority execution.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority execution.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority execution.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority execution.
    /// </summary>
    Critical
}
}
