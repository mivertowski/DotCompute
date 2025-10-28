// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Placeholder type for CoreKernelDebugOrchestrator.
/// The actual implementation resides in DotCompute.Core.Debugging.Services.
/// This file exists to resolve type references in DotCompute.Abstractions.
/// </summary>
/// <remarks>
/// This is a forward declaration placeholder. The real CoreKernelDebugOrchestrator
/// is implemented in DotCompute.Core and should not be referenced from abstractions.
/// If you're seeing this type, consider restructuring the code to use the IKernelDebugService
/// interface instead of depending on the concrete orchestrator type.
/// </remarks>
[Obsolete("This is a placeholder type. Use IKernelDebugService from DotCompute.Abstractions.Debugging instead.", true)]
public sealed class CoreKernelDebugOrchestrator
{
    private CoreKernelDebugOrchestrator()
    {
        throw new NotSupportedException(
            "This is a placeholder type that should not be instantiated. " +
            "The actual implementation is in DotCompute.Core.Debugging.Services.KernelDebugOrchestrator. " +
            "Use IKernelDebugService interface instead.");
    }
}
