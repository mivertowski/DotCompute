// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Standard NUMA distance values.
/// </summary>
internal static class NumaDistances
{
    /// <summary>Local node distance.</summary>
    public const int Local = 10;

    /// <summary>Remote node distance.</summary>
    public const int Remote = 20;

    /// <summary>Distant node distance.</summary>
    public const int Distant = 30;
}
