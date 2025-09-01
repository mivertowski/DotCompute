// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Operators.Interfaces;
using DotCompute.Linq.Operators.Types;
using DotCompute.Linq.Operators.Parameters;

namespace DotCompute.Linq.Operators.Adapters;

/// <summary>
/// Adapter to bridge between different IKernel interfaces.
/// </summary>
internal class KernelAdapter : IKernel
{
    private readonly DotCompute.Abstractions.IKernel _coreKernel;

    public KernelAdapter(DotCompute.Abstractions.IKernel coreKernel)
    {
        _coreKernel = coreKernel ?? throw new ArgumentNullException(nameof(coreKernel));
        
        // Create properties from core kernel
        Properties = new KernelProperties
        {
            MaxThreadsPerBlock = 256,
            SharedMemorySize = 0,
            RegisterCount = 0
        };
    }

    public string Name => _coreKernel.Name;
    
    public KernelProperties Properties { get; }

    public Task CompileAsync(CancellationToken cancellationToken = default)
        // Assume already compiled
        => Task.CompletedTask;

    public Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        // Core kernel doesn't have execution - this is handled by accelerator
        => throw new NotSupportedException("Execution is handled by the accelerator, not the kernel directly.");

    public IReadOnlyList<KernelParameter> GetParameterInfo()
        // Return empty for now - would need to adapt core kernel parameters
        => Array.Empty<KernelParameter>();

    public void Dispose()
    {
        // Core kernel doesn't implement IDisposable
        // Nothing to dispose
    }
}