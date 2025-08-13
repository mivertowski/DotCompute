// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a compute kernel that can be compiled and executed on an accelerator.
/// Implementations should be structs for AOT compatibility and performance.
/// </summary>
public interface IKernel
{
    /// <summary>
    /// Gets the unique name of this kernel.
    /// </summary>
    public static abstract string Name { get; }

    /// <summary>
    /// Gets the source code or IL representation of the kernel.
    /// </summary>
    public static abstract string Source { get; }

    /// <summary>
    /// Gets the entry point method name for the kernel.
    /// </summary>
    public static abstract string EntryPoint { get; }

    /// <summary>
    /// Gets the required shared memory size in bytes.
    /// </summary>
    public static abstract int RequiredSharedMemory { get; }
}

// Note: KernelDefinition, CompilationOptions, and OptimizationLevel are defined in their respective files

/// <summary>
/// Represents a kernel parameter.
/// </summary>
public sealed class KernelParameter(string name, Type type, int index, bool isInput = true, bool isOutput = false, bool isConstant = false)
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public Type Type { get; } = type ?? throw new ArgumentNullException(nameof(type));

    /// <summary>
    /// Gets whether this is an input parameter.
    /// </summary>
    public bool IsInput { get; } = isInput;

    /// <summary>
    /// Gets whether this is an output parameter.
    /// </summary>
    public bool IsOutput { get; } = isOutput;

    /// <summary>
    /// Gets the parameter index.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    /// Gets whether this is a constant parameter that should never be null.
    /// </summary>
    public bool IsConstant { get; } = isConstant;

    /// <summary>
    /// Gets whether this parameter is read-only.
    /// </summary>
    public bool IsReadOnly => IsInput && !IsOutput;

    /// <summary>
    /// Gets the memory space for this parameter.
    /// </summary>
    public MemorySpace MemorySpace { get; init; } = MemorySpace.Global;
}

/// <summary>
/// Defines the memory space where kernel parameters reside.
/// </summary>
public enum MemorySpace
{
    /// <summary>
    /// Global memory space accessible by all work items.
    /// </summary>
    Global,

    /// <summary>
    /// Local memory space shared within a work group.
    /// </summary>
    Local,

    /// <summary>
    /// Shared memory space within a thread block (CUDA terminology).
    /// </summary>
    Shared,

    /// <summary>
    /// Constant memory space for read-only data.
    /// </summary>
    Constant,

    /// <summary>
    /// Private memory space for each work item.
    /// </summary>
    Private
}
