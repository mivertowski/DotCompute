// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Linq.Operators;

/// <summary>
/// Default implementation of kernel factory for LINQ operations.
/// </summary>
public class DefaultKernelFactory : IKernelFactory
{
    /// <summary>
    /// Creates a kernel for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="definition">The kernel definition.</param>
    /// <returns>A compiled kernel.</returns>
    public IKernel CreateKernel(IAccelerator accelerator, KernelDefinition definition)
    {
        // For now, return a placeholder kernel that implements the interface
        // In a real implementation, this would compile and create actual kernels
        return new PlaceholderKernel(definition.Name);
    }

    /// <summary>
    /// Validates whether a kernel definition is supported on the accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>True if the kernel is supported; otherwise, false.</returns>
    public bool IsKernelSupported(IAccelerator accelerator, KernelDefinition definition)
    {
        // For now, assume all kernels are supported
        return true;
    }

    /// <summary>
    /// Gets the supported kernel languages for an accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to query.</param>
    /// <returns>The supported kernel languages.</returns>
    public IReadOnlyList<KernelLanguage> GetSupportedLanguages(IAccelerator accelerator)
    {
        // Return basic language support
        return new[] { KernelLanguage.CSharp };
    }
}

/// <summary>
/// Placeholder kernel implementation for development purposes.
/// </summary>
internal class PlaceholderKernel : IKernel
{
    private bool _disposed;

    public PlaceholderKernel(string name)
    {
        Name = name;
        Properties = new KernelProperties
        {
            MaxThreadsPerBlock = 1024,
            SharedMemorySize = 0,
            RegisterCount = 0
        };
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    public KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel for execution.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the compilation operation.</returns>
    public Task CompileAsync(CancellationToken cancellationToken = default)
    {
        // Placeholder compilation - just return completed task
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the kernel with the given parameters.
    /// </summary>
    /// <param name="workItems">The work items configuration.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the execution.</returns>
    public Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        // Placeholder execution - just return completed task
        // In a real implementation, this would execute the kernel code
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets information about the kernel parameters.
    /// </summary>
    /// <returns>The kernel parameter information.</returns>
    public IReadOnlyList<KernelParameter> GetParameterInfo()
    {
        // Return empty parameter list for placeholder
        return Array.Empty<KernelParameter>();
    }

    /// <summary>
    /// Disposes the kernel.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}