// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Kernels
{
    /// <summary>
    /// Represents a kernel source from bytecode or binary data.
    /// Provides implementation for kernels defined using pre-compiled
    /// bytecode, assembly, or binary executable formats.
    /// </summary>
    public class BytecodeKernelSource : IKernelSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BytecodeKernelSource"/> class.
        /// </summary>
        /// <param name="bytecode">The binary data containing the compiled kernel bytecode.</param>
        /// <param name="name">The name identifier for the kernel (defaults to "main").</param>
        /// <param name="language">The bytecode format or target language (defaults to Binary).</param>
        /// <param name="entryPoint">The entry point method name for kernel execution (defaults to "main").</param>
        /// <param name="dependencies">Optional array of dependency identifiers required by this kernel.</param>
        /// <exception cref="ArgumentNullException">Thrown when bytecode is null.</exception>
        /// <exception cref="ArgumentException">Thrown when bytecode is empty, name is null/empty, or entryPoint is null/empty.</exception>
        public BytecodeKernelSource(byte[] bytecode, string name = "main", KernelLanguage language = KernelLanguage.Binary,
                                   string entryPoint = "main", params string[] dependencies)
        {
            ArgumentNullException.ThrowIfNull(bytecode);

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
            Dependencies = dependencies ?? [];
        }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the unique name identifier for this kernel.
        /// </summary>
        public string Name { get; }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the Base64-encoded representation of the kernel bytecode.
        /// The original binary data is encoded to string format for consistent interface compliance.
        /// </summary>
        public string Code { get; }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the bytecode format or target language for this kernel.
        /// </summary>
        public KernelLanguage Language { get; }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the entry point method name for kernel execution.
        /// </summary>
        public string EntryPoint { get; }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the array of dependency identifiers required by this kernel.
        /// </summary>
        public string[] Dependencies { get; }
    }
}