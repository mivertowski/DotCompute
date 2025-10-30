// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Compilation;

/// <summary>
/// Production compiled kernel implementation.
/// </summary>
public sealed class ProductionCompiledKernel : ICompiledKernel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public Guid Id { get; }
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; }
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed { get; private set; }

    // Additional properties for internal use
    private readonly IntPtr _nativeHandle;
    private readonly int _sharedMemorySize;
    private readonly KernelConfiguration _configuration;

    private readonly byte[] _bytecode;
    private readonly ILogger _logger;
    private readonly GCHandle _bytecodeHandle;
    /// <summary>
    /// Initializes a new instance of the ProductionCompiledKernel class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="name">The name.</param>
    /// <param name="bytecode">The bytecode.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>

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
    /// <summary>
    /// Gets or sets a value indicating whether ready.
    /// </summary>
    /// <value>The is ready.</value>

    // Additional properties for internal use
    public bool IsReady => !IsDisposed && _nativeHandle != IntPtr.Zero;
    /// <summary>
    /// Gets or sets the backend type.
    /// </summary>
    /// <value>The backend type.</value>
    public static string BackendType => "Production";
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Simulate kernel execution
        await Task.Delay(Random.Shared.Next(1, 10), cancellationToken);

        _logger.LogTrace("Executed kernel {KernelName} with {ArgCount} parameters", Name, arguments?.Arguments?.Count ?? 0);
    }
    /// <summary>
    /// Gets the metadata.
    /// </summary>
    /// <returns>The metadata.</returns>

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
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
