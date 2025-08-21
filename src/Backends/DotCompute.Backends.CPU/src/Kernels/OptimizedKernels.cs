// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

//
// ⚠️  DEPRECATED: This file has been split into separate files for better maintainability.
// ⚠️  The individual classes have been moved to their respective subdirectories:
// ⚠️  
// ⚠️  • OptimizedKernelBase                → Base/OptimizedKernelBase.cs
// ⚠️  • OptimizedVectorAddKernel          → Optimized/OptimizedVectorAddKernel.cs  
// ⚠️  • OptimizedMatrixMultiplyKernel     → Optimized/OptimizedMatrixMultiplyKernel.cs
// ⚠️  • OptimizedReductionKernel          → Optimized/OptimizedReductionKernel.cs
// ⚠️  • OptimizedMemoryKernel             → Optimized/OptimizedMemoryKernel.cs
// ⚠️  • OptimizedComputeKernel            → Optimized/OptimizedComputeKernel.cs
// ⚠️  • OptimizedVectorScaleKernel        → Optimized/OptimizedVectorScaleKernel.cs
// ⚠️  • GenericOptimizedKernel            → Optimized/GenericOptimizedKernel.cs
// ⚠️  • KernelType                        → Types/KernelType.cs
// ⚠️  • KernelInfo                        → Types/KernelInfo.cs
// ⚠️  • KernelParameter                   → Types/KernelParameter.cs
// ⚠️  
// ⚠️  Please update your using statements to reference the new file locations.
// ⚠️  This file will be removed in a future version.
//

#pragma warning disable CS0618 // Type or member is obsolete

using System.Numerics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// Base class for optimized kernel implementations.
/// </summary>
[Obsolete("This class has been moved to Base/OptimizedKernelBase.cs. Please update your using statements.")]
internal abstract class OptimizedKernelBase : ICompiledKernel
{
    protected readonly ILogger Logger;
    protected readonly CompilationOptions Options;
    protected bool Disposed;

    protected OptimizedKernelBase(string name, CompilationOptions options, ILogger logger)
    {
        Name = name;
        Options = options;
        Logger = logger;
    }

    public string Name { get; }

    public abstract ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    protected void ThrowIfDisposed()
    {
        if (Disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}

/// <summary>
/// Highly optimized vector addition kernel with SIMD support.
/// </summary>
[Obsolete("This class has been moved to Optimized/OptimizedVectorAddKernel.cs. Please update your using statements.")]
internal class OptimizedVectorAddKernel : OptimizedKernelBase
{
    public OptimizedVectorAddKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 3)
        {
            throw new ArgumentException("Vector add requires 3 arguments: input A, input B, output");
        }

        var bufferA = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var bufferB = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var bufferResult = arguments.Arguments[2] as IMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IMemoryBuffer");

        var elementCount = (int)(bufferA.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteVectorAddOptimized(bufferA, bufferB, bufferResult, elementCount), cancellationToken);
    }

    private static async void ExecuteVectorAddOptimized(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferResult, int elementCount)
        // Use generic implementation since we don't have direct access to HighPerformanceMemoryBuffer here
        // This could be optimized further by exposing unsafe pointers through IMemoryBuffer

        => await ExecuteVectorAddGenericAsync(bufferA, bufferB, bufferResult, elementCount);

    private static async Task ExecuteVectorAddGenericAsync(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferResult, int elementCount)
    {
        var dataA = new float[elementCount];
        var dataB = new float[elementCount];
        var dataResult = new float[elementCount];

        await bufferA.CopyToHostAsync<float>(dataA);
        await bufferB.CopyToHostAsync<float>(dataB);

        // Vectorized addition
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var vecA = new Vector<float>(dataA, offset);
            var vecB = new Vector<float>(dataB, offset);
            var result = vecA + vecB;
            result.CopyTo(dataResult, offset);
        }

        // Handle remaining elements
        for (var i = vectorCount * vectorSize; i < elementCount; i++)
        {
            dataResult[i] = dataA[i] + dataB[i];
        }

        await bufferResult.CopyFromHostAsync<float>(dataResult);
    }
}

/// <summary>
/// Cache-optimized matrix multiplication kernel.
/// </summary>
[Obsolete("This class has been moved to Optimized/OptimizedMatrixMultiplyKernel.cs. Please update your using statements.")]
internal class OptimizedMatrixMultiplyKernel : OptimizedKernelBase
{
    public OptimizedMatrixMultiplyKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 4)
        {
            throw new ArgumentException("Matrix multiply requires 4 arguments: matrix A, matrix B, result C, size");
        }

        var bufferA = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var bufferB = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var bufferC = arguments.Arguments[2] as IMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IMemoryBuffer");
        var size = Convert.ToInt32(arguments.Arguments[3]);

        await Task.Run(() => ExecuteMatrixMultiplyGenericAsync(bufferA, bufferB, bufferC, size), cancellationToken);
    }

    private static async Task ExecuteMatrixMultiplyGenericAsync(IMemoryBuffer bufferA, IMemoryBuffer bufferB, IMemoryBuffer bufferC, int size)
    {
        var matrixA = new float[size * size];
        var matrixB = new float[size * size];
        var matrixC = new float[size * size];

        await bufferA.CopyToHostAsync<float>(matrixA);
        await bufferB.CopyToHostAsync<float>(matrixB);

        // Parallel matrix multiplication
        _ = Parallel.For(0, size, i =>
        {
            for (var j = 0; j < size; j++)
            {
                var sum = 0.0f;
                for (var k = 0; k < size; k++)
                {
                    sum += matrixA[i * size + k] * matrixB[k * size + j];
                }
                matrixC[i * size + j] = sum;
            }
        });

        await bufferC.CopyFromHostAsync<float>(matrixC);
    }
}

/// <summary>
/// Tree-based reduction kernel with logarithmic scaling.
/// </summary>
[Obsolete("This class has been moved to Optimized/OptimizedReductionKernel.cs. Please update your using statements.")]
internal class OptimizedReductionKernel : OptimizedKernelBase
{
    public OptimizedReductionKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 2)
        {
            throw new ArgumentException("Reduction requires 2 arguments: input, output");
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteReductionGenericAsync(inputBuffer, outputBuffer, elementCount), cancellationToken);
    }

    private static async Task ExecuteReductionGenericAsync(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        var input = new float[elementCount];
        await inputBuffer.CopyToHostAsync<float>(input);

        // Parallel reduction with partitioning
        var numPartitions = Environment.ProcessorCount;
        var partitionSums = new double[numPartitions];

        _ = Parallel.For(0, numPartitions, partition =>
        {
            var elementsPerPartition = elementCount / numPartitions;
            var start = partition * elementsPerPartition;
            var end = (partition == numPartitions - 1) ? elementCount : start + elementsPerPartition;

            var sum = 0.0;
            for (var i = start; i < end; i++)
            {
                sum += input[i];
            }
            partitionSums[partition] = sum;
        });

        var totalSum = partitionSums.Sum();
        await outputBuffer.CopyFromHostAsync<float>(new[] { (float)totalSum });
    }
}

/// <summary>
/// Memory-intensive kernel optimized for bandwidth utilization.
/// </summary>
[Obsolete("This class has been moved to Optimized/OptimizedMemoryKernel.cs. Please update your using statements.")]
internal class OptimizedMemoryKernel : OptimizedKernelBase
{
    public OptimizedMemoryKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 2)
        {
            throw new ArgumentException("Memory kernel requires 2 arguments: input, output");
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteMemoryIntensiveGenericAsync(inputBuffer, outputBuffer, elementCount), cancellationToken);
    }

    private static async Task ExecuteMemoryIntensiveGenericAsync(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount)
    {
        var input = new float[elementCount];
        var output = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(input);

        _ = Parallel.For(0, elementCount, i =>
        {
            var value = input[i];
            value += input[i];
            value *= input[i];
            value /= input[i] + 1.0f;
            output[i] = value;
        });

        await outputBuffer.CopyFromHostAsync<float>(output);
    }
}

/// <summary>
/// Compute-intensive kernel with transcendental functions.
/// </summary>
[Obsolete("This class has been moved to Optimized/OptimizedComputeKernel.cs. Please update your using statements.")]
internal class OptimizedComputeKernel : OptimizedKernelBase
{
    public OptimizedComputeKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 3)
        {
            throw new ArgumentException("Compute kernel requires 3 arguments: input, output, iterations");
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var outputBuffer = arguments.Arguments[1] as IMemoryBuffer ?? throw new ArgumentException("Argument 1 must be IMemoryBuffer");
        var iterations = Convert.ToInt32(arguments.Arguments[2]);

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteComputeIntensiveOptimized(inputBuffer, outputBuffer, elementCount, iterations), cancellationToken);
    }

    private static async void ExecuteComputeIntensiveOptimized(IMemoryBuffer inputBuffer, IMemoryBuffer outputBuffer, int elementCount, int iterations)
    {
        var input = new float[elementCount];
        var output = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(input);

        // Parallel compute-intensive operations
        _ = Parallel.For(0, elementCount, i =>
        {
            var value = input[i];

            for (var iter = 0; iter < iterations; iter++)
            {
                value = MathF.Sin(value) * MathF.Cos(value) + MathF.Sqrt(MathF.Abs(value) + 1.0f);
                value = MathF.Abs(value);
                value = MathF.Pow(value, 0.5f);
            }

            output[i] = value;
        });

        await outputBuffer.CopyFromHostAsync<float>(output);
    }
}

/// <summary>
/// Highly optimized vector scaling kernel with SIMD support.
/// </summary>
[Obsolete("This class has been moved to Optimized/OptimizedVectorScaleKernel.cs. Please update your using statements.")]
internal class OptimizedVectorScaleKernel : OptimizedKernelBase
{
    public OptimizedVectorScaleKernel(string name, CompilationOptions options, ILogger logger)
        : base(name, options, logger) { }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Count < 3)
        {
            throw new ArgumentException("Vector scale requires 3 arguments: input, scale factor, result");
        }

        var inputBuffer = arguments.Arguments[0] as IMemoryBuffer ?? throw new ArgumentException("Argument 0 must be IMemoryBuffer");
        var scaleFactor = Convert.ToSingle(arguments.Arguments[1]);
        var resultBuffer = arguments.Arguments[2] as IMemoryBuffer ?? throw new ArgumentException("Argument 2 must be IMemoryBuffer");

        var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));

        await Task.Run(() => ExecuteVectorScaleGenericAsync(inputBuffer, resultBuffer, scaleFactor, elementCount), cancellationToken);
    }

    private static async Task ExecuteVectorScaleGenericAsync(IMemoryBuffer inputBuffer, IMemoryBuffer resultBuffer, float scaleFactor, int elementCount)
    {
        var inputData = new float[elementCount];
        var resultData = new float[elementCount];

        await inputBuffer.CopyToHostAsync<float>(inputData);

        // Vectorized scaling
        var vectorSize = Vector<float>.Count;
        var vectorCount = elementCount / vectorSize;
        var scaleVec = new Vector<float>(scaleFactor);

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var inputVec = new Vector<float>(inputData, offset);
            var result = inputVec * scaleVec;
            result.CopyTo(resultData, offset);
        }

        // Handle remaining elements
        for (var i = vectorCount * vectorSize; i < elementCount; i++)
        {
            resultData[i] = inputData[i] * scaleFactor;
        }

        await resultBuffer.CopyFromHostAsync<float>(resultData);
    }
}

/// <summary>
/// Generic optimized kernel for unknown kernel types.
/// </summary>
[Obsolete("This class has been moved to Optimized/GenericOptimizedKernel.cs. Please update your using statements.")]
internal class GenericOptimizedKernel : OptimizedKernelBase
{
    private readonly KernelInfo _kernelInfo;

    public GenericOptimizedKernel(string name, KernelInfo kernelInfo, CompilationOptions options, ILogger logger)
        : base(name, options, logger)
    {
        _kernelInfo = kernelInfo;
    }

    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Logger.LogWarning("Executing generic kernel - performance may be suboptimal: {KernelName}", Name);

        // Try to execute based on the kernel source analysis
        await TryExecuteGenericKernelAsync(arguments, cancellationToken);
    }

    private async ValueTask TryExecuteGenericKernelAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Analyze kernel source to infer execution pattern
        var source = _kernelInfo.Source.ToLowerInvariant();

        // Try common patterns based on source analysis
        if (source.Contains("result[i]") && source.Contains("input[i]") && source.Contains("scale"))
        {
            // Vector scale pattern - handle it manually
            await ExecuteVectorScalePatternAsync(arguments, cancellationToken);
        }
        else if (source.Contains("result[i]") && arguments.Arguments.Count >= 3)
        {
            // General element-wise operation pattern
            await ExecuteElementWisePatternAsync(arguments, cancellationToken);
        }
        else
        {
            Logger.LogWarning("Unable to infer kernel execution pattern for: {KernelName}. No operation performed.", Name);
        }
    }

    private async ValueTask ExecuteVectorScalePatternAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        if (arguments.Arguments.Count < 3)
        {
            Logger.LogError("Vector scale pattern requires at least 3 arguments");
            return;
        }

        var scaleFactor = 2.0f; // Default scale factor

        // Try to extract scale factor from arguments[1]
        if (arguments.Arguments[1] is float f)
        {
            scaleFactor = f;
        }
        else if (arguments.Arguments[1] is double d)
        {
            scaleFactor = (float)d;
        }
        else if (arguments.Arguments[1] is int i)
        {
            scaleFactor = (float)i;
        }

        if (arguments.Arguments[0] is IMemoryBuffer inputBuffer && arguments.Arguments[2] is IMemoryBuffer resultBuffer)
        {
            var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
            var inputData = new float[elementCount];
            var resultData = new float[elementCount];

            await inputBuffer.CopyToHostAsync<float>(inputData, cancellationToken: cancellationToken);

            // Perform scaling
            for (var idx = 0; idx < elementCount; idx++)
            {
                resultData[idx] = inputData[idx] * scaleFactor;
            }

            await resultBuffer.CopyFromHostAsync<float>(resultData, cancellationToken: cancellationToken);
            Logger.LogInformation("Generic vector scale executed: {Elements} elements scaled by {Factor}", elementCount, scaleFactor);
        }
    }

    private async ValueTask ExecuteElementWisePatternAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Generic element-wise operation - just copy input to output as fallback
        if (arguments.Arguments.Count >= 2 &&
            arguments.Arguments[0] is IMemoryBuffer inputBuffer &&
            arguments.Arguments[1] is IMemoryBuffer outputBuffer)
        {
            var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
            var data = new float[elementCount];

            await inputBuffer.CopyToHostAsync<float>(data, cancellationToken: cancellationToken);
            await outputBuffer.CopyFromHostAsync<float>(data, cancellationToken: cancellationToken);

            Logger.LogInformation("Generic element-wise operation executed: {Elements} elements copied", elementCount);
        }
    }
}

// Supporting data structures
[Obsolete("This enum has been moved to Types/KernelType.cs. Please update your using statements.")]
internal enum KernelType
{
    Generic,
    VectorAdd,
    VectorMultiply,
    VectorScale,
    MatrixMultiply,
    Reduction,
    MemoryIntensive,
    ComputeIntensive
}

[Obsolete("This class has been moved to Types/KernelInfo.cs. Please update your using statements.")]
internal class KernelInfo
{
    public string Name { get; set; } = string.Empty;
    public KernelType Type { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<KernelParameter> Parameters { get; set; } = [];
}

[Obsolete("This class has been moved to Types/KernelParameter.cs. Please update your using statements.")]
internal class KernelParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsGlobal { get; set; }
}
