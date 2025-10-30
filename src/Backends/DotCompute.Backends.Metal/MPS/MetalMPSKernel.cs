// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Configuration;
using DotCompute.Abstractions.Interfaces.Compute;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.MPS;

/// <summary>
/// Compiled kernel wrapper that represents MPS-accelerated operations.
/// This is a lightweight placeholder that indicates a kernel can be executed via MPS.
/// Actual execution is handled by the Metal accelerator's orchestration layer.
/// </summary>
public sealed class MetalMPSKernel : ICompiledKernel
{
    private readonly MPSOperationType _operation;
    private readonly ILogger _logger;
    private int _disposed;

    public MetalMPSKernel(
        IntPtr device,
        MPSOperationType operation,
        string name,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(logger);

        if (device == IntPtr.Zero)
        {
            throw new ArgumentException("Device cannot be zero", nameof(device));
        }

        _operation = operation;
        Name = name;
        _logger = logger;
        Id = Guid.NewGuid();

        _logger.LogDebug("Created MPS kernel placeholder for operation: {Operation}", operation);
    }

    /// <inheritdoc/>
    public Guid Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public ICompilationMetadata CompilationMetadata { get; } = new MPSCompilationMetadata();

    /// <summary>
    /// Gets the MPS operation type for this kernel.
    /// </summary>
    public MPSOperationType Operation => _operation;

    /// <inheritdoc/>
    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        // MPS kernels are executed by the MetalAccelerator's orchestration layer
        // This method is a placeholder to satisfy the ICompiledKernel interface
        _logger.LogTrace("MPS kernel execution requested for: {Name} ({Operation})", Name, _operation);

        throw new NotSupportedException(
            $"MPS kernel '{Name}' must be executed through MetalAccelerator's MPS orchestration layer. " +
            "Direct execution is not supported.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _logger.LogTrace("Disposed MPS kernel: {Name}", Name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// Simple compilation metadata for MPS kernels.
/// </summary>
internal sealed class MPSCompilationMetadata : ICompilationMetadata
{
    public DateTimeOffset CompilationTime { get; } = DateTimeOffset.UtcNow;
    public CompilationOptions Options { get; } = new();
    public IReadOnlyList<string> Warnings { get; } = new List<string>
    {
        "Using Metal Performance Shaders for optimized execution"
    }.AsReadOnly();
    public OptimizationLevel OptimizationLevel { get; } = OptimizationLevel.O3;
}
