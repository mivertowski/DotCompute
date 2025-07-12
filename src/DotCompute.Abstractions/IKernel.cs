// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;

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
public sealed class KernelParameter
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public Type Type { get; }
    
    /// <summary>
    /// Gets whether this is an input parameter.
    /// </summary>
    public bool IsInput { get; }
    
    /// <summary>
    /// Gets whether this is an output parameter.
    /// </summary>
    public bool IsOutput { get; }
    
    /// <summary>
    /// Gets the parameter index.
    /// </summary>
    public int Index { get; }
    
    public KernelParameter(string name, Type type, int index, bool isInput = true, bool isOutput = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Index = index;
        IsInput = isInput;
        IsOutput = isOutput;
    }
}