// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Linq.Tests.Mocks;

/// <summary>
/// Simple mock implementation of IComputeOrchestrator for testing.
/// </summary>
public class MockComputeOrchestrator : IComputeOrchestrator
{
    public Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        throw new NotImplementedException("Mock orchestrator");
    }

    public Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        throw new NotImplementedException("Mock orchestrator");
    }

    public Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        throw new NotImplementedException("Mock orchestrator");
    }

    public Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        throw new NotImplementedException("Mock orchestrator");
    }

    public Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        return Task.FromResult<IAccelerator?>(null);
    }

    public Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        return Task.FromResult<IReadOnlyList<IAccelerator>>(Array.Empty<IAccelerator>());
    }

    public Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {
        return Task.FromResult(true);
    }
}