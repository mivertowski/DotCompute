using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Core;

/// <summary>
/// Represents a compute accelerator device.
/// </summary>
public interface IAccelerator : IAsyncDisposable
{
    /// <summary>Gets device information.</summary>
    public AcceleratorInfo Info { get; }
    
    /// <summary>Gets memory manager for this accelerator.</summary>
    public IMemoryManager Memory { get; }
    
    /// <summary>Compiles a kernel for execution.</summary>
    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>Synchronizes all pending operations.</summary>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
}