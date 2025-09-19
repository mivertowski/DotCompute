// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This interface has been moved to DotCompute.Abstractions.Interfaces.Kernels
// to avoid circular dependencies and ensure clean architecture boundaries.

using System;

namespace DotCompute.Backends.Metal.Execution.Interfaces
{
    /// <summary>
    /// Re-export for backward compatibility.
    /// Use DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel instead.
    /// </summary>
    [Obsolete("Use DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel instead")]
    public interface ICompiledKernel : DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel
    {
    }
}