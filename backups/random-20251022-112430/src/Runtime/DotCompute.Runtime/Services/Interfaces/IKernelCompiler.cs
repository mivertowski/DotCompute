// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Legacy kernel compiler interface - now aliased to IUnifiedKernelCompiler for backward compatibility.
/// All new code should use IUnifiedKernelCompiler directly from DotCompute.Abstractions namespace.
/// This interface will be removed in a future major version.
/// </summary>
[Obsolete("Use IUnifiedKernelCompiler from DotCompute.Abstractions namespace instead. This interface will be removed in a future major version.", false)]
public interface IKernelCompiler : IUnifiedKernelCompiler
{
    // This interface is now a type alias to IUnifiedKernelCompiler
    // All methods are inherited from the unified interface
}