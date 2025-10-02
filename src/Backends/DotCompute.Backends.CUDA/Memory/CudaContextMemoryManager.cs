// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA context-specific memory manager wrapping CudaMemoryManager
/// </summary>
public sealed class CudaContextMemoryManager
{
    private readonly CudaMemoryManager _underlyingManager;

    public CudaContextMemoryManager(CudaContext context, ILogger logger)
    {
        _underlyingManager = new CudaMemoryManager(context ?? throw new ArgumentNullException(nameof(context)), logger ?? throw new ArgumentNullException(nameof(logger)));
    }

    // Expose underlying manager for compatibility
    public CudaMemoryManager UnderlyingManager => _underlyingManager;

    public void Dispose()
    {
        _underlyingManager.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _underlyingManager.DisposeAsync();
    }
}
