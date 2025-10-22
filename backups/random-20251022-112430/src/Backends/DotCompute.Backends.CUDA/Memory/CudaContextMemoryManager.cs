// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA context-specific memory manager wrapping CudaMemoryManager
/// </summary>
public sealed class CudaContextMemoryManager(CudaContext context, ILogger logger)
{
    private readonly CudaMemoryManager _underlyingManager = new(context ?? throw new ArgumentNullException(nameof(context)), logger ?? throw new ArgumentNullException(nameof(logger)));
    /// <summary>
    /// Gets or sets the underlying manager.
    /// </summary>
    /// <value>The underlying manager.</value>

    // Expose underlying manager for compatibility
    public CudaMemoryManager UnderlyingManager => _underlyingManager;
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() => _underlyingManager.Dispose();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync() => _underlyingManager.DisposeAsync();
}
