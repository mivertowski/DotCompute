// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Represents a compute kernel that can be compiled and executed on an accelerator.
    /// </summary>
    public interface IKernel
    {
        /// <summary>
        /// Gets the unique name of this kernel.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the source code or IL representation of the kernel.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the entry point method name for the kernel.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the required shared memory size in bytes.
        /// </summary>
        public int RequiredSharedMemory { get; }
    }
}
