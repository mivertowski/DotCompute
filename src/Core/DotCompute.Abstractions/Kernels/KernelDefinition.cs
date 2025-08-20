// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents a kernel definition that contains the source code and metadata
/// necessary to compile and execute a compute kernel.
/// </summary>
public class KernelDefinition
{
    /// <summary>
    /// Gets or sets the unique name identifier for this kernel.
    /// </summary>
    /// <value>The kernel name used for identification and compilation.</value>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the kernel source code.
    /// </summary>
    /// <value>The source code string containing the kernel implementation.</value>
    public string? Source { get; init; }


    /// <summary>
    /// Gets or sets the kernel code (alias for Source for compatibility).
    /// </summary>
    /// <value>The kernel source code, identical to the Source property.</value>
    public string? Code
    {

        get => Source;

        init => Source = value;

    }

    /// <summary>
    /// Gets or sets the entry point function name for kernel execution.
    /// </summary>
    /// <value>The name of the function to call when executing the kernel. Defaults to "main".</value>
    public string EntryPoint { get; init; } = "main";

    /// <summary>
    /// Gets or sets kernel metadata for additional configuration and information.
    /// </summary>
    /// <value>A dictionary containing kernel-specific metadata such as compilation flags, device constraints, or custom attributes.</value>
    public Dictionary<string, object> Metadata { get; init; } = new();


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelDefinition"/> class.
    /// </summary>
    public KernelDefinition() { }


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelDefinition"/> class with the specified parameters.
    /// </summary>
    /// <param name="name">The unique name for the kernel.</param>
    /// <param name="source">The kernel source code.</param>
    /// <param name="entryPoint">The entry point function name. If null, defaults to "main".</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="source"/> is null.</exception>
    [SetsRequiredMembers]
    public KernelDefinition(string name, string source, string? entryPoint = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(source);


        Name = name;
        Source = source;
        EntryPoint = entryPoint ?? "main";
    }
}