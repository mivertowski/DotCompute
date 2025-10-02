// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels.Types;

namespace DotCompute.Abstractions.Kernels
{
    /// <summary>
    /// Represents a kernel source from text-based code.
    /// Provides implementation for kernels defined using human-readable
    /// source code in various programming languages and formats.
    /// </summary>
    public class TextKernelSource : IKernelSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextKernelSource"/> class.
        /// </summary>
        /// <param name="code">The source code text containing the kernel implementation.</param>
        /// <param name="name">The name identifier for the kernel (defaults to "main").</param>
        /// <param name="language">The programming language of the source code (defaults to CSharpIL).</param>
        /// <param name="entryPoint">The entry point method name for kernel execution (defaults to "main").</param>
        /// <param name="dependencies">Optional array of dependency identifiers required by this kernel.</param>
        /// <exception cref="ArgumentException">Thrown when code, name, or entryPoint is null or empty.</exception>
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
            Dependencies = dependencies ?? [];
        }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the unique name identifier for this kernel.
        /// </summary>
        public string Name { get; }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the source code text containing the kernel implementation.
        /// </summary>
        public string Code { get; }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the programming language used for the kernel source code.
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