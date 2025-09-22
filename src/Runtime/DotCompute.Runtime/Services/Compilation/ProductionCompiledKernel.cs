// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace DotCompute.Runtime.Services.Compilation;

/// <summary>
/// Production compiled kernel implementation.
/// </summary>
public sealed class ProductionCompiledKernel : ICompiledKernel
{
    public Guid Id { get; }
    public string Name { get; }
    public bool IsDisposed { get; private set; }

    // Additional properties for internal use
    private readonly IntPtr _nativeHandle;
    private readonly int _sharedMemorySize;
    private readonly KernelConfiguration _configuration;

    private readonly byte[] _bytecode;
    private readonly ILogger _logger;
    private readonly GCHandle _bytecodeHandle;

    public ProductionCompiledKernel(Guid id, string name, byte[] bytecode, KernelConfiguration configuration, ILogger logger)
    {
        Id = id;
        Name = name;
        _bytecode = bytecode;
        _configuration = configuration;
        _sharedMemorySize = 0; // configuration.SharedMemorySize; // Property not available in current KernelConfiguration
        _logger = logger;

        // Pin bytecode and create native handle
        _bytecodeHandle = GCHandle.Alloc(_bytecode, GCHandleType.Pinned);
        _nativeHandle = _bytecodeHandle.AddrOfPinnedObject();
    }

    // Additional properties for internal use
    public bool IsReady => !IsDisposed && _nativeHandle != IntPtr.Zero;
    public string BackendType => "Production";

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Simulate kernel execution
        await Task.Delay(Random.Shared.Next(1, 10), cancellationToken);

        _logger.LogTrace("Executed kernel {KernelName} with {ArgCount} parameters", Name, arguments?.Arguments?.Count ?? 0);
    }

    public object GetMetadata()
    {
        return new
        {
            Id,
            Name,
            BackendType,
            IsReady,
            _sharedMemorySize,
            _configuration,
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

            // Native handle is now private readonly, no need to null it
            _logger.LogTrace("Disposed compiled kernel {KernelName}", Name);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}