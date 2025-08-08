// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Core.Types;

namespace DotCompute.Tests.Common;

/// <summary>
/// Test-specific CompiledKernel class with mutable properties for testing.
/// </summary>
public class CompiledKernel
{
    /// <summary>
    /// Gets or sets the unique identifier for this kernel.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the kernel identifier.
    /// </summary>
    public Guid KernelId { get; set; }

    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel entry point.
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the native handle to the compiled kernel code.
    /// </summary>
    public IntPtr NativeHandle { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel is compiled.
    /// </summary>
    public bool IsCompiled { get; set; }

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets the kernel metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the kernel configuration.
    /// </summary>
    public KernelConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the kernel's required shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets when the kernel was compiled.
    /// </summary>
    public DateTimeOffset CompilationTimestamp { get; set; }

    /// <summary>
    /// Converts to the Abstractions CompiledKernel struct.
    /// </summary>
    public DotCompute.Abstractions.CompiledKernel ToAbstractionsCompiledKernel()
    {
        // Create kernel configuration
        var config = new DotCompute.Abstractions.KernelConfiguration(
            new DotCompute.Abstractions.Dim3(1), // Default grid dimensions
            Configuration?.BlockDimensions != null ? 
                new DotCompute.Abstractions.Dim3(Configuration.BlockDimensions.X, Configuration.BlockDimensions.Y, Configuration.BlockDimensions.Z) :
                new DotCompute.Abstractions.Dim3(256) // Default block size
        );
        
        return new DotCompute.Abstractions.CompiledKernel(
            Id,
            NativeHandle,
            SharedMemorySize,
            config);
    }

    /// <summary>
    /// Implicit conversion operator to DotCompute.Abstractions.CompiledKernel.
    /// </summary>
    public static implicit operator DotCompute.Abstractions.CompiledKernel(CompiledKernel testKernel)
    {
        return testKernel.ToAbstractionsCompiledKernel();
    }
}

/// <summary>
/// Test-specific KernelConfiguration class with mutable properties.
/// </summary>
public class KernelConfiguration
{
    /// <summary>
    /// Gets or sets the block dimensions.
    /// </summary>
    public Dimensions3D BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size.
    /// </summary>
    public int SharedMemorySize { get; set; }
}

/// <summary>
/// Test-specific KernelArgument class.
/// </summary>
public class KernelArgument
{
    /// <summary>
    /// Gets or sets the argument name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the argument type.
    /// </summary>
    public Type Type { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the argument value.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets whether this is an input argument.
    /// </summary>
    public bool IsInput { get; set; }

    /// <summary>
    /// Gets or sets whether this is an output argument.
    /// </summary>
    public bool IsOutput { get; set; }

    /// <summary>
    /// Gets or sets the buffer size.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets whether this is a device memory buffer.
    /// </summary>
    public bool IsDeviceMemory { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes for raw buffers.
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Gets or sets the argument type.
    /// </summary>
    public KernelArgumentType ArgumentType { get; set; }

    /// <summary>
    /// Converts to Core.Kernels.KernelArgument
    /// </summary>
    public DotCompute.Core.Kernels.KernelArgument ToCoreKernelArgument()
    {
        return new DotCompute.Core.Kernels.KernelArgument
        {
            Name = Name,
            Type = Type,
            Value = Value ?? new object(),
            IsDeviceMemory = IsDeviceMemory,
            MemoryBuffer = null,
            SizeInBytes = SizeInBytes > 0 ? SizeInBytes : Size
        };
    }

    /// <summary>
    /// Converts an array to Core.Kernels.KernelArgument array
    /// </summary>
    public static DotCompute.Core.Kernels.KernelArgument[] ToCoreKernelArguments(KernelArgument[] testArgs)
    {
        return testArgs.Select(arg => arg.ToCoreKernelArgument()).ToArray();
    }

    /// <summary>
    /// Implicit conversion operator to DotCompute.Core.Kernels.KernelArgument.
    /// </summary>
    public static implicit operator DotCompute.Core.Kernels.KernelArgument(KernelArgument testArg)
    {
        return testArg.ToCoreKernelArgument();
    }

}

/// <summary>
/// Test-specific KernelLanguage enum that mirrors the Abstractions enum.
/// </summary>
public enum KernelLanguage
{
    /// <summary>
    /// CUDA C/C++ source code.
    /// </summary>
    CUDA,

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
/// Kernel argument type enumeration for test compatibility.
/// </summary>
public enum KernelArgumentType
{
    /// <summary>
    /// Simple value argument
    /// </summary>
    Value,

    /// <summary>
    /// Buffer/memory argument
    /// </summary>
    Buffer,

    /// <summary>
    /// Texture argument
    /// </summary>
    Texture,

    /// <summary>
    /// Sampler argument
    /// </summary>
    Sampler,

    /// <summary>
    /// Scalar value argument
    /// </summary>
    Scalar,

    /// <summary>
    /// Structured buffer argument
    /// </summary>
    StructuredBuffer,

    /// <summary>
    /// Unordered access view argument
    /// </summary>
    UnorderedAccessView,

    /// <summary>
    /// 2D image argument
    /// </summary>
    Image2D
}

/// <summary>
/// Extension methods and conversion helpers for test types.
/// </summary>
public static class TestTypeConversions
{
    /// <summary>
    /// Converts an array of test KernelArguments to production KernelArguments.
    /// </summary>
    public static DotCompute.Core.Kernels.KernelArgument[] ToCoreKernelArguments(this KernelArgument[] testArgs)
    {
        return testArgs.Select(arg => (DotCompute.Core.Kernels.KernelArgument)arg).ToArray();
    }

    /// <summary>
    /// Converts test CompiledKernel to production CompiledKernel.
    /// </summary>
    public static DotCompute.Abstractions.CompiledKernel ToAbstractionsCompiledKernel(this CompiledKernel testKernel)
    {
        return (DotCompute.Abstractions.CompiledKernel)testKernel;
    }
}