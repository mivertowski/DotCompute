// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory
{
    /// <summary>
    /// Specifies the access mode when mapping memory.
    /// </summary>
    public enum MapMode
    {
        /// <summary>
        /// Map memory for read-only access.
        /// </summary>
        Read,

        /// <summary>
        /// Map memory for write-only access.
        /// </summary>
        Write,

        /// <summary>
        /// Map memory for read and write access.
        /// </summary>
        ReadWrite,

        /// <summary>
        /// Map memory for write access with discard of previous contents.
        /// </summary>
        WriteDiscard,

        /// <summary>
        /// Map memory for write access without synchronization.
        /// </summary>
        WriteNoOverwrite
    }
}
