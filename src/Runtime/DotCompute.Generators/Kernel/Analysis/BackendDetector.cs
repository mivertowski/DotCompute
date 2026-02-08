// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Detects available Ring Kernel backends at compile time by analyzing referenced assemblies.
/// </summary>
/// <remarks>
/// This detector uses Roslyn's <see cref="Compilation.GetTypeByMetadataName"/> to check
/// for the presence of backend-specific Ring Kernel runtime types. This approach is:
/// <list type="bullet">
///   <item><description>AOT-compatible (no runtime reflection)</description></item>
///   <item><description>Compile-time safe (generates code only for available backends)</description></item>
///   <item><description>Extensible (new backends can be added easily)</description></item>
/// </list>
/// </remarks>
internal static class BackendDetector
{
    /// <summary>
    /// Well-known Ring Kernel backend types that can be detected.
    /// </summary>
    private static readonly IReadOnlyList<(string BackendName, string RuntimeTypeName)> KnownBackends = new[]
    {
        ("CPU", "DotCompute.Backends.CPU.RingKernels.CpuRingKernelRuntime"),
        ("CUDA", "DotCompute.Backends.CUDA.RingKernels.CudaRingKernelRuntime"),
        // Future backends can be added here:
        // ("OpenCL", "DotCompute.Backends.OpenCL.RingKernels.OpenCLRingKernelRuntime"),
        // ("Metal", "DotCompute.Backends.Metal.RingKernels.MetalRingKernelRuntime"),
    };

    /// <summary>
    /// Detects which Ring Kernel backends are available in the compilation.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation to analyze.</param>
    /// <returns>
    /// An immutable array of available backends. Empty if no backends are referenced.
    /// </returns>
    /// <remarks>
    /// A backend is considered available if its runtime type can be resolved by
    /// <see cref="Compilation.GetTypeByMetadataName"/>. This means the backend's
    /// assembly must be directly or transitively referenced by the compilation.
    /// </remarks>
    public static ImmutableArray<AvailableBackend> DetectBackends(Compilation compilation)
    {
        var backends = new List<AvailableBackend>();

        foreach (var (backendName, runtimeTypeName) in KnownBackends)
        {
            var runtimeType = compilation.GetTypeByMetadataName(runtimeTypeName);
            if (runtimeType != null)
            {
                backends.Add(new AvailableBackend(backendName, runtimeTypeName));
            }
        }

        return backends.ToImmutableArray();
    }

    /// <summary>
    /// Checks if at least one Ring Kernel backend is available.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation to analyze.</param>
    /// <returns>True if at least one backend is available; otherwise, false.</returns>
    public static bool HasAnyBackend(Compilation compilation)
    {
        foreach (var (_, runtimeTypeName) in KnownBackends)
        {
            if (compilation.GetTypeByMetadataName(runtimeTypeName) != null)
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Represents an available Ring Kernel backend detected at compile time.
/// </summary>
internal sealed class AvailableBackend
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AvailableBackend"/> class.
    /// </summary>
    /// <param name="name">The backend name (e.g., "CPU", "CUDA").</param>
    /// <param name="runtimeTypeName">The fully qualified runtime type name.</param>
    public AvailableBackend(string name, string runtimeTypeName)
    {
        Name = name;
        RuntimeTypeName = runtimeTypeName;
    }

    /// <summary>
    /// Gets the backend name (e.g., "CPU", "CUDA").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the fully qualified runtime type name.
    /// </summary>
    public string RuntimeTypeName { get; }
}
