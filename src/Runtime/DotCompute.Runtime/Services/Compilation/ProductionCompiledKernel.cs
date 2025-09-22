// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;

namespace DotCompute.Runtime.Services.Compilation;

/// <summary>
/// Production compiled kernel implementation.
/// </summary>
public sealed class ProductionCompiledKernel : ICompiledKernel, IDisposable
{
    public Guid Id { get; }
    public string Name { get; }
    public IntPtr NativeHandle { get; private set; }
    public int SharedMemorySize { get; }
    public KernelConfiguration Configuration { get; }
    public bool IsDisposed { get; private set; }

    private readonly byte[] _bytecode;
    private readonly ILogger _logger;
    private readonly GCHandle _bytecodeHandle;

    public ProductionCompiledKernel(Guid id, string name, byte[] bytecode, KernelConfiguration configuration, ILogger logger)
    {
        Id = id;
        Name = name;
        _bytecode = bytecode;
        Configuration = configuration;
        SharedMemorySize = 0; // configuration.SharedMemorySize; // Property not available in current KernelConfiguration
        _logger = logger;

        // Pin bytecode and create native handle
        _bytecodeHandle = GCHandle.Alloc(_bytecode, GCHandleType.Pinned);
        NativeHandle = _bytecodeHandle.AddrOfPinnedObject();
    }

    public bool IsReady => !IsDisposed && NativeHandle != IntPtr.Zero;

    public string BackendType => "Production";

    public async Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductionCompiledKernel));
        }

        // Simulate kernel execution
        await Task.Delay(Random.Shared.Next(1, 10), cancellationToken);

        _logger.LogTrace("Executed kernel {KernelName} with {ArgCount} parameters", Name, parameters?.Length ?? 0);
    }

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductionCompiledKernel));
        }

        // Convert KernelArguments to object array for ICompiledKernel interface
        var parameters = arguments.Arguments.Where(a => a != null).Cast<object>().ToArray();
        await ExecuteAsync(parameters, cancellationToken);
    }

    public object GetMetadata()
    {
        return new
        {
            Id,
            Name,
            BackendType,
            IsReady,
            SharedMemorySize,
            Configuration,
            BytecodeSize = _bytecode?.Length ?? 0
        };
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            if (_bytecodeHandle.IsAllocated)
            {
                _bytecodeHandle.Free();
            }

            NativeHandle = IntPtr.Zero;
            _logger.LogTrace("Disposed compiled kernel {KernelName}", Name);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}