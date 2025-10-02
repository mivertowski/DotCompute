// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// An memcpy kind enumeration.
    /// </summary>
    /// <summary>
    /// Memory copy kind
    /// </summary>
    public enum MemcpyKind
    {
        HostToHost = 0,
        HostToDevice = 1,
        DeviceToHost = 2,
        DeviceToDevice = 3,
        Default = 4
    }
}