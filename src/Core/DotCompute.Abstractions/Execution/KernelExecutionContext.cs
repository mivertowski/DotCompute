// <copyright file="KernelExecutionContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Execution;

/// <summary>
/// Represents the execution context for a kernel.
/// </summary>
public class KernelExecutionContext
{
    /// <summary>
    /// Gets or sets the unique identifier for this execution context.
    /// </summary>
    public Guid ExecutionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the target accelerator for execution.
    /// </summary>
    public string? AcceleratorId { get; set; }

    /// <summary>
    /// Gets or sets the optimization level for this execution.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets the memory optimization level for this execution.
    /// </summary>
    public MemoryOptimizationLevel MemoryOptimizationLevel { get; set; } = MemoryOptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public int? TimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets additional execution parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to enable profiling for this execution.
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug mode for this execution.
    /// </summary>
    public bool EnableDebugMode { get; set; }

    /// <summary>
    /// Gets or sets the name of the kernel being executed.
    /// </summary>
    public string? KernelName { get; set; }

    /// <summary>
    /// Gets or sets compilation information for the kernel.
    /// </summary>
    public string? CompilationInfo { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this context was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the work dimensions for kernel execution.
    /// </summary>
    public Dim3 WorkDimensions { get; set; } = new(1, 1, 1);

    /// <summary>
    /// Gets or sets the local work size for kernel execution.
    /// </summary>
    public Dim3 LocalWorkSize { get; set; } = new(1, 1, 1);

    /// <summary>
    /// Gets or sets the kernel name (alias for KernelName).
    /// </summary>
    public string? Name

    {
        get => KernelName;
        set => KernelName = value;
    }

    /// <summary>
    /// Gets or sets the memory buffers for this execution context.
    /// </summary>
    public Dictionary<int, IUnifiedMemoryBuffer<byte>> Buffers { get; set; } = [];

    /// <summary>
    /// Gets or sets the scalar parameters for this execution context.
    /// </summary>
    public Dictionary<int, object> Scalars { get; set; } = [];

    /// <summary>
    /// Sets a parameter value for the kernel execution.
    /// </summary>
    /// <param name="index">The parameter index.</param>
    /// <param name="value">The parameter value.</param>
    public void SetParameter(int index, object value)
    {
        Parameters[index.ToString()] = value;
        if (value is IUnifiedMemoryBuffer<byte> buffer)
        {
            Buffers[index] = buffer;
        }
        else
        {
            Scalars[index] = value;
        }
    }

    /// <summary>
    /// Gets a buffer parameter by index.
    /// </summary>
    /// <param name="index">The buffer index.</param>
    /// <returns>The memory buffer.</returns>
    public IUnifiedMemoryBuffer<byte>? GetBuffer(int index) => Buffers.TryGetValue(index, out var buffer) ? buffer : null;

    /// <summary>
    /// Gets a buffer parameter by index (static access).
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="index">The buffer index.</param>
    /// <returns>The memory buffer.</returns>
    public static IUnifiedMemoryBuffer<byte>? GetBuffer(KernelExecutionContext context, int index) => context.GetBuffer(index);

    /// <summary>
    /// Gets a scalar parameter by index.
    /// </summary>
    /// <typeparam name="T">The scalar type.</typeparam>
    /// <param name="index">The parameter index.</param>
    /// <returns>The scalar value.</returns>
    public T? GetScalar<T>(int index)
    {
        if (Scalars.TryGetValue(index, out var value) && value is T scalar)
        {
            return scalar;
        }
        return default;
    }

    /// <summary>
    /// Creates a default execution context.
    /// </summary>
    /// <returns>A new instance of <see cref="KernelExecutionContext"/> with default settings.</returns>
    public static KernelExecutionContext CreateDefault() => new();
}