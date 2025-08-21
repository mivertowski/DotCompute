// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

//
// ⚠️  DEPRECATION NOTICE
//
// This file has been split into multiple organized files for better maintainability:
//
// Core Components:
// - Providers/HighPerformanceCpuAcceleratorProvider.cs  - Main accelerator provider
// - Accelerators/HighPerformanceCpuAccelerator.cs       - CPU accelerator implementation
// - Parsing/OpenCLKernelParser.cs                       - OpenCL kernel analysis
//
// Memory Management:
// - Memory/HighPerformanceMemoryManager.cs              - Memory allocation and management
// - Memory/MemoryPool.cs                                - Buffer pooling for efficiency
// - Memory/HighPerformanceMemoryBuffer.cs               - High-performance aligned buffers
// - Memory/HighPerformanceMemoryBufferView.cs           - Memory buffer views
//
// Kernel Execution:
// - Kernels/SimpleOptimizedKernel.cs                    - Basic optimized kernel implementation
//
// Type Definitions:
// - Types/KernelType.cs                                 - Kernel type enumeration
// - Types/KernelInfo.cs                                 - Kernel metadata container
// - Types/KernelParameter.cs                            - Kernel parameter information
//
// This file will be removed in a future version. Please update your references to use the
// individual files in their respective subdirectories under DotCompute.Core.Compute.
//

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using DotCompute.Core.Compute.Providers;

// Legacy alias for backward compatibility - will be removed
[System.Obsolete("This class has been moved to DotCompute.Core.Compute.Providers namespace. Use HighPerformanceCpuAcceleratorProvider from the Providers namespace instead.")]
public class HighPerformanceCpuAcceleratorProvider : DotCompute.Core.Compute.Providers.HighPerformanceCpuAcceleratorProvider
{
    public HighPerformanceCpuAcceleratorProvider(Microsoft.Extensions.Logging.ILogger<DotCompute.Core.Compute.Providers.HighPerformanceCpuAcceleratorProvider> logger) 
        : base(logger)
    {
    }
}

#pragma warning restore CS1591