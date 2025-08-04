// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a source of kernel code that can be compiled.
/// </summary>
public interface IKernelSource
{
    /// <summary>
    /// Gets the name of the kernel.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the source code or bytecode of the kernel.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the kernel language or source type.
    /// </summary>
    public KernelLanguage Language { get; }

    /// <summary>
    /// Gets the entry point method name for the kernel.
    /// </summary>
    public string EntryPoint { get; }

    /// <summary>
    /// Gets the dependencies required by this kernel.
    /// </summary>
    public string[] Dependencies { get; }
}

/// <summary>
/// Defines the language or format of kernel source code.
/// </summary>
public enum KernelLanguage
{
    /// <summary>
    /// CUDA C/C++ source code.
    /// </summary>
    Cuda,

    /// <summary>
    /// OpenCL C source code.
    /// </summary>
    OpenCL,

    /// <summary>
    /// NVIDIA PTX assembly.
    /// </summary>
    Ptx,

    /// <summary>
    /// HLSL shader code.
    /// </summary>
    HLSL,

    /// <summary>
    /// SPIR-V bytecode.
    /// </summary>
    SPIRV,

    /// <summary>
    /// Metal shader language.
    /// </summary>
    Metal,

    /// <summary>
    /// ROCm HIP source code.
    /// </summary>
    HIP,

    /// <summary>
    /// SYCL/DPC++ source code.
    /// </summary>
    SYCL,

    /// <summary>
    /// C# IL or expression tree.
    /// </summary>
    CSharpIL,

    /// <summary>
    /// Pre-compiled binary.
    /// </summary>
    Binary
}

/// <summary>
/// Represents a kernel source from text code.
/// </summary>
public class TextKernelSource : IKernelSource
{
    /// <summary>
    /// Initializes a new instance of the TextKernelSource class.
    /// </summary>
    /// <param name="code">The source code.</param>
    /// <param name="name">The kernel name.</param>
    /// <param name="language">The kernel language.</param>
    /// <param name="entryPoint">The entry point method name.</param>
    /// <param name="dependencies">The kernel dependencies.</param>
    public TextKernelSource(string code, string name = "main", KernelLanguage language = KernelLanguage.CSharpIL,
                           string entryPoint = "main", params string[] dependencies)
    {
        if (string.IsNullOrEmpty(code))
        {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (string.IsNullOrEmpty(entryPoint))
        {
            throw new ArgumentException("EntryPoint cannot be null or empty", nameof(entryPoint));
        }

        Code = code;
        Name = name;
        Language = language;
        EntryPoint = entryPoint;
        Dependencies = dependencies ?? Array.Empty<string>();
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Code { get; }

    /// <inheritdoc/>
    public KernelLanguage Language { get; }

    /// <inheritdoc/>
    public string EntryPoint { get; }

    /// <inheritdoc/>
    public string[] Dependencies { get; }
}

/// <summary>
/// Represents a kernel source from bytecode.
/// </summary>
public class BytecodeKernelSource : IKernelSource
{
    /// <summary>
    /// Initializes a new instance of the BytecodeKernelSource class.
    /// </summary>
    /// <param name="bytecode">The bytecode.</param>
    /// <param name="name">The kernel name.</param>
    /// <param name="language">The kernel language.</param>
    /// <param name="entryPoint">The entry point method name.</param>
    /// <param name="dependencies">The kernel dependencies.</param>
    public BytecodeKernelSource(byte[] bytecode, string name = "main", KernelLanguage language = KernelLanguage.Binary,
                               string entryPoint = "main", params string[] dependencies)
    {
        if (bytecode == null)
        {
            throw new ArgumentNullException(nameof(bytecode));
        }

        if (bytecode.Length == 0)
        {
            throw new ArgumentException("Bytecode cannot be empty", nameof(bytecode));
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (string.IsNullOrEmpty(entryPoint))
        {
            throw new ArgumentException("EntryPoint cannot be null or empty", nameof(entryPoint));
        }

        Code = Convert.ToBase64String(bytecode);
        Name = name;
        Language = language;
        EntryPoint = entryPoint;
        Dependencies = dependencies ?? Array.Empty<string>();
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Code { get; }

    /// <inheritdoc/>
    public KernelLanguage Language { get; }

    /// <inheritdoc/>
    public string EntryPoint { get; }

    /// <inheritdoc/>
    public string[] Dependencies { get; }
}
