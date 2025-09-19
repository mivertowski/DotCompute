// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Enums;

/// <summary>
/// Memory access pattern classifications.
/// </summary>
public enum MemoryAccessPattern
{
    Sequential,
    Random,
    Strided,
    Coalesced,
    Scattered
}