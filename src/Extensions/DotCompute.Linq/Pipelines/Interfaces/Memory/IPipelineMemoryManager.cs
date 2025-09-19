// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;

namespace DotCompute.Linq.Pipelines.Interfaces.Memory
{
    /// <summary>
    /// Interface for pipeline memory manager (bridges to Core interface).
    /// </summary>
    public interface IPipelineMemoryManager : IDisposable
    {
        /// <summary>Allocates memory buffer of specified size.</summary>
        Task<IMemoryBuffer> AllocateAsync(long size);

        /// <summary>Releases allocated memory buffer.</summary>
        Task ReleaseAsync(IMemoryBuffer buffer);
    }
}