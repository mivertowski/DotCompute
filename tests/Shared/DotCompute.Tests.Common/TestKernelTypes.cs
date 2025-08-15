// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Types;

namespace DotCompute.Tests.Shared;

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
    public Dictionary<string, string> Metadata { get; set; } = [];

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
            SizeInBytes = SizeInBytes > 0 ? SizeInBytes : EstimateSizeFromValue(Value)
        };
    }

    /// <summary>
    /// Estimates size from the value if SizeInBytes wasn't set.
    /// </summary>
    private long EstimateSizeFromValue(object? value)
    {
        if (value == null)
            return 0;

        return value switch
        {
            byte[] bytes => bytes.Length,
            float[] floats => floats.Length * sizeof(float),
            double[] doubles => doubles.Length * sizeof(double),
            int[] ints => ints.Length * sizeof(int),
            long[] longs => longs.Length * sizeof(long),
            bool[] bools => bools.Length * sizeof(bool),
            int => sizeof(int),
            float => sizeof(float),
            double => sizeof(double),
            long => sizeof(long),
            bool => sizeof(bool),
            _ => Size > 0 ? Size : IntPtr.Size
        };
    }

    /// <summary>
    /// Converts an array to Core.Kernels.KernelArgument array
    /// </summary>
    public static DotCompute.Core.Kernels.KernelArgument[] ToCoreKernelArguments(KernelArgument[] testArgs) => testArgs.Select(arg => arg.ToCoreKernelArgument()).ToArray();

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
/// Factory methods for creating test types with production compatibility.
/// </summary>
public static class TestKernelFactory
{
    /// <summary>
    /// Creates a test CompiledKernel with proper defaults.
    /// </summary>
    public static CompiledKernel CreateCompiledKernel(
        string name = "TestKernel",
        string entryPoint = "TestKernel",
        KernelLanguage language = KernelLanguage.Cuda,
        Guid? id = null,
        Guid? kernelId = null,
        IntPtr nativeHandle = default,
        int sharedMemorySize = 0,
        bool isCompiled = true,
        DateTimeOffset? compilationTimestamp = null,
        Dimensions3D? blockDimensions = null)
    {
        return new CompiledKernel
        {
            Id = id ?? Guid.NewGuid(),
            KernelId = kernelId ?? Guid.NewGuid(),
            Name = name,
            EntryPoint = entryPoint,
            NativeHandle = nativeHandle,
            IsCompiled = isCompiled,
            Language = language,
            SharedMemorySize = sharedMemorySize,
            CompilationTimestamp = compilationTimestamp ?? DateTimeOffset.UtcNow,
            Configuration = new KernelConfiguration
            {
                BlockDimensions = blockDimensions ?? new Dimensions3D(256),
                SharedMemorySize = sharedMemorySize
            }
        };
    }

    /// <summary>
    /// Creates a test KernelArgument with proper defaults.
    /// </summary>
    public static KernelArgument CreateKernelArgument(
        string name,
        object value,
        Type? type = null,
        bool isInput = true,
        bool isOutput = false,
        bool isDeviceMemory = false,
        long sizeInBytes = 0,
        KernelArgumentType argumentType = KernelArgumentType.Value)
    {
        return new KernelArgument
        {
            Name = name,
            Value = value,
            Type = type ?? value?.GetType() ?? typeof(object),
            IsInput = isInput,
            IsOutput = isOutput,
            IsDeviceMemory = isDeviceMemory,
            SizeInBytes = sizeInBytes > 0 ? sizeInBytes : EstimateSizeInBytes(value),
            ArgumentType = argumentType
        };
    }

    /// <summary>
    /// Creates a test KernelConfiguration with proper defaults.
    /// </summary>
    public static KernelConfiguration CreateKernelConfiguration(
        Dimensions3D? blockDimensions = null,
        int sharedMemorySize = 0)
    {
        return new KernelConfiguration
        {
            BlockDimensions = blockDimensions ?? new Dimensions3D(256),
            SharedMemorySize = sharedMemorySize
        };
    }

    /// <summary>
    /// Creates a batch of test KernelArguments for common scenarios.
    /// </summary>
    public static KernelArgument[] CreateStandardKernelArguments(int arraySize = 1024)
    {
        return
        [
            CreateKernelArgument("input", new float[arraySize], typeof(float[]),
                isInput: true, isDeviceMemory: true, argumentType: KernelArgumentType.Buffer),
            CreateKernelArgument("output", new float[arraySize], typeof(float[]),
                isInput: false, isOutput: true, isDeviceMemory: true, argumentType: KernelArgumentType.Buffer),
            CreateKernelArgument("size", arraySize, typeof(int),
                argumentType: KernelArgumentType.Scalar)
        ];
    }

    /// <summary>
    /// Estimates the size in bytes for a value.
    /// </summary>
    private static long EstimateSizeInBytes(object? value)
    {
        if (value == null)
            return 0;

        return value switch
        {
            byte[] bytes => bytes.Length,
            float[] floats => floats.Length * sizeof(float),
            double[] doubles => doubles.Length * sizeof(double),
            int[] ints => ints.Length * sizeof(int),
            long[] longs => longs.Length * sizeof(long),
            bool[] bools => bools.Length * sizeof(bool),
            int => sizeof(int),
            float => sizeof(float),
            double => sizeof(double),
            long => sizeof(long),
            bool => sizeof(bool),
            _ => IntPtr.Size // Default pointer size for unknown types
        };
    }
}

/// <summary>
/// Extension methods and conversion helpers for test types.
/// </summary>
public static class TestTypeConversions
{
    /// <summary>
    /// Converts an array of test KernelArguments to production KernelArguments.
    /// </summary>
    public static DotCompute.Core.Kernels.KernelArgument[] ToCoreKernelArguments(this KernelArgument[] testArgs) => testArgs.Select(arg => (DotCompute.Core.Kernels.KernelArgument)arg).ToArray();

    /// <summary>
    /// Converts test CompiledKernel to production CompiledKernel.
    /// </summary>
    public static DotCompute.Abstractions.CompiledKernel ToAbstractionsCompiledKernel(this CompiledKernel testKernel) => (DotCompute.Abstractions.CompiledKernel)testKernel;

    /// <summary>
    /// Converts test KernelLanguage to production KernelLanguage.
    /// </summary>
    public static Abstractions.KernelLanguage ToAbstractionsKernelLanguage(this KernelLanguage testLanguage)
    {
        return testLanguage switch
        {
            KernelLanguage.Cuda => Abstractions.KernelLanguage.Cuda,
            KernelLanguage.OpenCL => Abstractions.KernelLanguage.OpenCL,
            KernelLanguage.Ptx => Abstractions.KernelLanguage.Ptx,
            KernelLanguage.HLSL => Abstractions.KernelLanguage.HLSL,
            KernelLanguage.SPIRV => Abstractions.KernelLanguage.SPIRV,
            KernelLanguage.Metal => Abstractions.KernelLanguage.Metal,
            KernelLanguage.HIP => Abstractions.KernelLanguage.HIP,
            KernelLanguage.SYCL => Abstractions.KernelLanguage.SYCL,
            KernelLanguage.CSharpIL => Abstractions.KernelLanguage.CSharpIL,
            KernelLanguage.Binary => Abstractions.KernelLanguage.Binary,
            _ => Abstractions.KernelLanguage.CSharpIL
        };
    }

    /// <summary>
    /// Converts production KernelLanguage to test KernelLanguage.
    /// </summary>
    public static KernelLanguage FromAbstractionsKernelLanguage(Abstractions.KernelLanguage productionLanguage)
    {
        return productionLanguage switch
        {
            Abstractions.KernelLanguage.Cuda => KernelLanguage.Cuda,
            Abstractions.KernelLanguage.OpenCL => KernelLanguage.OpenCL,
            Abstractions.KernelLanguage.Ptx => KernelLanguage.Ptx,
            Abstractions.KernelLanguage.HLSL => KernelLanguage.HLSL,
            Abstractions.KernelLanguage.SPIRV => KernelLanguage.SPIRV,
            Abstractions.KernelLanguage.Metal => KernelLanguage.Metal,
            Abstractions.KernelLanguage.HIP => KernelLanguage.HIP,
            Abstractions.KernelLanguage.SYCL => KernelLanguage.SYCL,
            Abstractions.KernelLanguage.CSharpIL => KernelLanguage.CSharpIL,
            Abstractions.KernelLanguage.Binary => KernelLanguage.Binary,
            _ => KernelLanguage.CSharpIL
        };
    }
}
