// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions;

namespace DotCompute.Abstractions;

/// <summary>
/// Represents an execution context for an accelerator.
/// </summary>
public class AcceleratorContext : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the accelerator associated with this context.
    /// </summary>
    public IAccelerator Accelerator { get; }
    
    /// <summary>
    /// Gets the unique identifier for this context.
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// Gets the creation time of this context.
    /// </summary>
    public DateTime CreatedAt { get; }
    
    /// <summary>
    /// Gets whether this context is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Creates a new accelerator context.
    /// </summary>
    /// <param name="accelerator">The accelerator to create a context for.</param>
    public AcceleratorContext(IAccelerator accelerator)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }
    
    /// <summary>
    /// Makes this context current for the thread.
    /// </summary>
    public void MakeCurrent()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AcceleratorContext));
            
        // Implementation would set this as the current context
        IsActive = true;
    }
    
    /// <summary>
    /// Releases this context.
    /// </summary>
    public void Release()
    {
        IsActive = false;
    }
    
    /// <summary>
    /// Disposes of this context.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Release();
            _disposed = true;
        }
    }
}