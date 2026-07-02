// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Structs;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests;

/// <summary>
/// Regression coverage for GH #182: every type in the CUDA backend assembly must be loadable.
/// A [StructLayout(LayoutKind.Explicit)] union with a managed reference (class/delegate/array)
/// at a [FieldOffset] fails CLR type loading on every platform — and because kernel discovery
/// reflects over the whole assembly with <see cref="Module.GetTypes()"/>, one such struct made
/// the entire assembly unscannable ("Error scanning assembly DotCompute.Backends.CUDA").
/// The C# compiler does NOT catch this; only forcing the types to load does.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AssemblyTypeLoadTests
{
    [Fact]
    public void AllTypesInCudaBackendAssembly_CanBeLoaded()
    {
        // Exactly what GeneratedKernelDiscoveryService does when scanning assemblies.
        var act = () => typeof(CudaAccelerator).Assembly.GetTypes();

        _ = act.Should().NotThrow<ReflectionTypeLoadException>(
            "kernel discovery reflects over this assembly; a single unloadable type (e.g. an " +
            "explicit-layout union with a managed reference field) breaks the whole scan");
    }

    [Fact]
    public void CudaLaunchAttributeValue_LoadsAndMatchesNativeUnionSize()
    {
        // Marshal.SizeOf forces the type to load (threw TypeLoadException before the fix, when
        // programmaticEvent was the managed CudaEvent delegate) and validates the native ABI:
        // CUlaunchAttributeValue is padded to 64 bytes (char pad[64]).
        _ = Marshal.SizeOf<CudaLaunchAttributeValue>().Should().Be(64);
    }

    [Fact]
    public void CudaLaunchAttributeValue_UnionMembers_AreAllUnmanaged()
    {
        // Guard against reintroducing a reference-type member into the union.
        foreach (var field in typeof(CudaLaunchAttributeValue).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            _ = field.FieldType.IsValueType.Should().BeTrue(
                $"union member '{field.Name}' must be an unmanaged value type; a managed reference " +
                "at a [FieldOffset] makes the CLR refuse to load the struct");
        }
    }
}
