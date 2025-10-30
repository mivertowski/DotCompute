// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is now a type alias to the canonical ExecutionPriority in DotCompute.Abstractions.Execution
// All compute engine execution priorities now use the unified type.

namespace DotCompute.Core.Compute.Enums;
/// <summary>
/// An execution priority enumeration.
/// </summary>

// Type alias for backward compatibility
// Use DotCompute.Abstractions.Execution.ExecutionPriority directly in new code
[Obsolete("Use DotCompute.Abstractions.Execution.ExecutionPriority directly")]
public enum ExecutionPriority
{
    /// <summary>No priority specified.</summary>
    None = 0,

    /// <summary>Low priority execution.</summary>
    Low = 1,

    /// <summary>Normal priority execution.</summary>
    Normal = 3,

    /// <summary>High priority execution.</summary>
    High = 5,

    /// <summary>Critical priority execution.</summary>
    Critical = 6
}
