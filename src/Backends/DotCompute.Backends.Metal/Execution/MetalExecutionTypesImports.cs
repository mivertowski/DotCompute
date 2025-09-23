// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file imports all types that were extracted from MetalExecutionTypes.cs
// to maintain backward compatibility while supporting clean architecture

global using DotCompute.Backends.Metal.Execution.Types.Core;
global using DotCompute.Backends.Metal.Execution.Types.Configuration;
global using DotCompute.Backends.Metal.Execution.Types.Operations;

// Note: Exception types are still in MetalExecutionTypes.cs pending extraction of MetalError/MetalException
// Note: Diagnostic types are incomplete pending extraction of MetalResourceType
// Note: MetalStreamConfiguration is incomplete pending extraction of MetalStreamPriority/MetalStreamFlags

namespace DotCompute.Backends.Metal.Execution
{
    // This namespace now imports the extracted types for backward compatibility
}