// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels.Types;

namespace DotCompute.Abstractions
{
    /// <summary>
    /// Represents a source of kernel code that can be compiled and executed.
    /// Defines the contract for providing kernel source code, metadata, and dependencies
    /// required for compilation and execution on various compute accelerators.
    /// </summary>
    public interface IKernelSource
    {
        /// <summary>
        /// Gets the unique name identifier for the kernel.
        /// This name is used for identification and debugging purposes.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the source code or bytecode representation of the kernel.
        /// For text-based sources, this contains the actual source code.
        /// For binary sources, this contains a Base64-encoded representation.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the kernel language or source type format.
        /// Specifies the programming language, assembly format, or bytecode type.
        /// </summary>
        public KernelLanguage Language { get; }

        /// <summary>
        /// Gets the entry point method name for kernel execution.
        /// This is the function name that will be invoked when the kernel runs.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the array of dependency identifiers required by this kernel.
        /// Dependencies may include libraries, headers, or other kernel modules.
        /// </summary>
        public string[] Dependencies { get; }
    }
}
