// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;

namespace DotCompute.Linq.Pipelines.Interfaces.Memory
{
    /// <summary>
    /// Interface for memory buffer (bridges to Core interface).
    /// </summary>
    public interface IMemoryBuffer : IDisposable
    {
        /// <summary>Buffer size in bytes.</summary>
        long Size { get; }

        /// <summary>Copy buffer to another buffer.</summary>
        Task CopyToAsync(IMemoryBuffer destination);
    }
}