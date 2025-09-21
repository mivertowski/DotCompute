// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This interface has been moved to DotCompute.Abstractions.Interfaces.Linq
// to avoid circular dependencies and ensure clean architecture boundaries.
using System;
namespace DotCompute.Linq.Interfaces;
/// <summary>
/// Re-export for backward compatibility.
/// Use DotCompute.Abstractions.Interfaces.Linq.IComputeLinqProvider instead.
/// </summary>
[Obsolete("Use DotCompute.Abstractions.Interfaces.Linq.IComputeLinqProvider instead")]
public interface IComputeLinqProvider : DotCompute.Abstractions.Interfaces.Linq.IComputeLinqProvider
{
}
