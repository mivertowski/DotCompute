// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is now a type alias to the canonical PerformanceMetrics in DotCompute.Abstractions
// All CUDA-specific performance metrics now use the unified type.

using DotCompute.Abstractions.Performance;

namespace DotCompute.Backends.CUDA.Types;

// Type alias for backward compatibility
// Use DotCompute.Abstractions.Performance.PerformanceMetrics directly in new code
public sealed class PerformanceMetrics : DotCompute.Abstractions.Performance.PerformanceMetrics
{
}