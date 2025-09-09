// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Validation options for CUDA operations.
    /// </summary>
    public sealed class ValidationOptions
    {
        public bool ValidateMemoryAccess { get; set; } = true;
        public bool ValidateLaunchParameters { get; set; } = true;
        public bool ValidateKernelExistence { get; set; } = true;
        public bool EnableBoundsChecking { get; set; }
        public bool EnableNanDetection { get; set; }
    }
}