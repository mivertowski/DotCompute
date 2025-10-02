// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is now a type alias to the canonical ExecutionPriority in DotCompute.Abstractions.Execution
// All compute engine execution priorities now use the unified type.

namespace DotCompute.Core.Compute.Enums;

// Type alias for backward compatibility
// Use DotCompute.Abstractions.Execution.ExecutionPriority directly in new code
[Obsolete("Use DotCompute.Abstractions.Execution.ExecutionPriority directly")]
public enum ExecutionPriority
{
    Low = Abstractions.Execution.ExecutionPriority.Low,
    Normal = Abstractions.Execution.ExecutionPriority.Normal,
    High = Abstractions.Execution.ExecutionPriority.High,
    Critical = Abstractions.Execution.ExecutionPriority.Critical
}