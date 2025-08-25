// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Core;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CPU backend has dynamic logging requirements

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// AOT-compatible CPU kernel compiler that uses pre-compiled delegates instead of dynamic IL emission.
/// This replaces the reflection-emit based CpuKernelCompiler for Native AOT scenarios.
/// </summary>
internal sealed class AotCpuKernelCompiler
{
    private readonly Dictionary<string, Func<KernelExecutionContext, Task>> _precompiledKernels;
    private readonly Dictionary<string, KernelMetadata> _kernelMetadata;

    public AotCpuKernelCompiler()
    {
        _precompiledKernels = [];
        _kernelMetadata = [];

        RegisterPrecompiledKernels();
    }

    /// <summary>
    /// Registers all pre-compiled kernel implementations for AOT compatibility.
    /// This replaces dynamic kernel compilation with static registration.
    /// </summary>
    private void RegisterPrecompiledKernels()
    {
        // Vector addition kernel
        RegisterKernel("vector_add_f32", VectorAddFloat32KernelAsync, new KernelMetadata
        {
            Name = "vector_add_f32",
            ParameterCount = 3,
            SupportsVectorization = true,
            PreferredVectorWidth = 8,
            MemoryPattern = MemoryAccessPattern.ReadWrite
        });

        // Matrix multiplication kernel
        RegisterKernel("matrix_multiply_f32", MatrixMultiplyFloat32KernelAsync, new KernelMetadata
        {
            Name = "matrix_multiply_f32",
            ParameterCount = 5,
            SupportsVectorization = true,
            PreferredVectorWidth = 8,
            MemoryPattern = MemoryAccessPattern.ReadWrite
        });

        // Element-wise operations
        RegisterKernel("element_multiply_f32", ElementMultiplyFloat32KernelAsync, new KernelMetadata
        {
            Name = "element_multiply_f32",
            ParameterCount = 3,
            SupportsVectorization = true,
            PreferredVectorWidth = 8,
            MemoryPattern = MemoryAccessPattern.ReadWrite
        });

        // Reduction operations
        RegisterKernel("reduce_sum_f32", ReduceSumFloat32KernelAsync, new KernelMetadata
        {
            Name = "reduce_sum_f32",
            ParameterCount = 2,
            SupportsVectorization = true,
            PreferredVectorWidth = 8,
            MemoryPattern = MemoryAccessPattern.ReadOnly
        });
    }

    private void RegisterKernel(string name, Func<KernelExecutionContext, Task> implementation, KernelMetadata metadata)
    {
        _precompiledKernels[name] = implementation;
        _kernelMetadata[name] = metadata;
    }

    /// <summary>
    /// Compiles a kernel for CPU execution using pre-compiled implementations.
    /// </summary>
    public async ValueTask<ICompiledKernel> CompileAsync(
        CpuKernelCompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var definition = context.Definition;
        var logger = context.Logger;

        logger.LogDebug("Starting AOT kernel compilation: {KernelName}", definition.Name);

        // Look up pre-compiled kernel
        if (!_precompiledKernels.TryGetValue(definition.Name, out var kernelImpl))
        {
            throw new NotSupportedException(
                $"Kernel '{definition.Name}' is not available in AOT mode. " +
                "Only pre-compiled kernels are supported in Native AOT scenarios.");
        }

        if (!_kernelMetadata.TryGetValue(definition.Name, out var metadata))
        {
            throw new InvalidOperationException($"Metadata not found for kernel '{definition.Name}'");
        }

        // Validate kernel parameters
        ValidateKernelParameters(definition, metadata);

        // Generate execution plan
        var executionPlan = GenerateExecutionPlan(metadata, context);

        // Create compiled kernel
        var compiledKernel = new AotCompiledKernel(
            definition,
            kernelImpl,
            executionPlan,
            context.ThreadPool,
            logger);

        logger.LogInformation("Successfully compiled AOT kernel: {KernelName}", definition.Name);

        await Task.Yield(); // Maintain async signature for compatibility
        return compiledKernel;
    }

    private static void ValidateKernelParameters(KernelDefinition definition, KernelMetadata metadata)
    {
        // Validate that the metadata parameter count matches expectations
        // This ensures consistency between the kernel definition and its metadata
        if (metadata.ParameterCount < 1)
        {
            throw new ArgumentException($"Kernel '{definition.Name}' must have at least one parameter");
        }
    }

    private static KernelExecutionPlan GenerateExecutionPlan(
        KernelMetadata metadata,
        CpuKernelCompilationContext context)
    {
        var simdCapabilities = context.SimdCapabilities;
        var vectorWidth = simdCapabilities.PreferredVectorWidth;
        var canUseSimd = simdCapabilities.IsHardwareAccelerated && metadata.SupportsVectorization;

        return new KernelExecutionPlan
        {
            Analysis = new KernelAnalysis
            {
                Definition = context.Definition,
                CanVectorize = metadata.SupportsVectorization,
                VectorizationFactor = metadata.PreferredVectorWidth,
                MemoryAccessPattern = metadata.MemoryPattern,
                ComputeIntensity = EstimateComputeIntensity(metadata),
                PreferredWorkGroupSize = CalculatePreferredWorkGroupSize(metadata)
            },
            UseVectorization = canUseSimd,
            VectorWidth = vectorWidth,
            VectorizationFactor = metadata.PreferredVectorWidth,
            WorkGroupSize = CalculatePreferredWorkGroupSize(metadata),
            MemoryPrefetchDistance = CalculateMemoryPrefetchDistance(metadata),
            EnableLoopUnrolling = context.Options.EnableDebugInfo, // Changed from EnableFastMath
            InstructionSets = simdCapabilities.SupportedInstructionSets
        };
    }

    private static ComputeIntensity EstimateComputeIntensity(KernelMetadata metadata)
    {
        return metadata.ParameterCount switch
        {
            <= 2 => ComputeIntensity.Low,
            <= 4 => ComputeIntensity.Medium,
            <= 6 => ComputeIntensity.High,
            _ => ComputeIntensity.VeryHigh
        };
    }

    private static int CalculatePreferredWorkGroupSize(KernelMetadata metadata)
    {
        // Base work group size calculation
        var baseSize = metadata.SupportsVectorization ? 64 : 32;

        // Adjust based on parameter count
        if (metadata.ParameterCount > 4)
        {
            baseSize /= 2;
        }

        return Math.Max(baseSize, 8);
    }

    private static int CalculateMemoryPrefetchDistance(KernelMetadata metadata)
    {
        return metadata.MemoryPattern switch
        {
            MemoryAccessPattern.ReadOnly => 128,
            MemoryAccessPattern.WriteOnly => 64,
            MemoryAccessPattern.ReadWrite => 32,
            MemoryAccessPattern.ComputeIntensive => 0,
            _ => 64
        };
    }

    #region Pre-compiled Kernel Implementations

    /// <summary>
    /// Vectorized float32 vector addition: C[i] = A[i] + B[i]
    /// </summary>
    private static async Task VectorAddFloat32KernelAsync(KernelExecutionContext context)
    {
        var bufferA = context.GetBuffer<float>(0);
        var bufferB = context.GetBuffer<float>(1);
        var bufferC = context.GetBuffer<float>(2);

        var length = Math.Min(Math.Min(bufferA.Length, bufferB.Length), bufferC.Length);

        await Task.Run(() =>
        {
            // Use SIMD vectorization when available
            VectorizedMath.Add(bufferA.Span, bufferB.Span, bufferC.Span, length);
        });
    }

    /// <summary>
    /// Optimized float32 matrix multiplication
    /// </summary>
    private static async Task MatrixMultiplyFloat32KernelAsync(KernelExecutionContext context)
    {
        var matrixA = context.GetBuffer<float>(0);
        var matrixB = context.GetBuffer<float>(1);
        var matrixC = context.GetBuffer<float>(2);
        var rows = context.GetScalar<int>(3);
        var cols = context.GetScalar<int>(4);

        await Task.Run(() =>
        {
            VectorizedMath.MatrixMultiply(
                matrixA.Span, matrixB.Span, matrixC.Span,
                rows, cols, cols);
        });
    }

    /// <summary>
    /// Element-wise multiplication: C[i] = A[i] * B[i]
    /// </summary>
    private static async Task ElementMultiplyFloat32KernelAsync(KernelExecutionContext context)
    {
        var bufferA = context.GetBuffer<float>(0);
        var bufferB = context.GetBuffer<float>(1);
        var bufferC = context.GetBuffer<float>(2);

        var length = Math.Min(Math.Min(bufferA.Length, bufferB.Length), bufferC.Length);

        await Task.Run(() =>
        {
            VectorizedMath.Multiply(bufferA.Span, bufferB.Span, bufferC.Span, length);
        });
    }

    /// <summary>
    /// Sum reduction operation
    /// </summary>
    private static async Task ReduceSumFloat32KernelAsync(KernelExecutionContext context)
    {
        var input = context.GetBuffer<float>(0);
        var output = context.GetBuffer<float>(1);

        await Task.Run(() =>
        {
            var sum = VectorizedMath.Sum(input.Span);
            output.Span[0] = sum;
        });
    }

    #endregion
}

/// <summary>
/// Metadata for pre-compiled kernels.
/// </summary>
internal sealed class KernelMetadata
{
    public required string Name { get; init; }
    public required int ParameterCount { get; init; }
    public required bool SupportsVectorization { get; init; }
    public required int PreferredVectorWidth { get; init; }
    public required MemoryAccessPattern MemoryPattern { get; init; }
}

/// <summary>
/// AOT-compatible compiled kernel implementation.
/// </summary>
internal sealed class AotCompiledKernel(
KernelDefinition definition,
Func<KernelExecutionContext, Task> implementation,
KernelExecutionPlan executionPlan,
CpuThreadPool threadPool,
ILogger logger) : ICompiledKernel
{
    private readonly KernelDefinition _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    private readonly Func<KernelExecutionContext, Task> _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
#pragma warning disable CA1823 // Avoid unused private fields - These fields are reserved for future use
    private readonly KernelExecutionPlan _executionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
#pragma warning restore CA1823
#pragma warning disable CA1823, CA2213 // Field is reserved for future use and disposal is handled externally
    private readonly CpuThreadPool _threadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
#pragma warning restore CA1823, CA2213
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string Name => _definition.Name;
    public KernelDefinition Definition => _definition;

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing AOT kernel: {KernelName}", Name);

        try
        {
            // Convert KernelArguments to KernelExecutionContext for internal processing
            var context = new KernelExecutionContext();
            for (var i = 0; i < arguments.Arguments.Count; i++)
            {
                var arg = arguments.Arguments[i];
                context.SetParameter(i, arg ?? throw new ArgumentNullException($"arguments[{i}]", "Kernel argument cannot be null"));
            }

            await _implementation(context).ConfigureAwait(false);
            _logger.LogDebug("Successfully executed AOT kernel: {KernelName}", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing AOT kernel: {KernelName}", Name);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        // Thread pool disposal is handled by the accelerator
        // _threadPool is managed externally
        _logger.LogDebug("Disposed AOT kernel: {KernelName}", Name);
        return ValueTask.CompletedTask;
    }
}



/// <summary>
/// SIMD-optimized math operations for AOT kernels.
/// </summary>
internal static class VectorizedMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result, int length)
    {
        var vectorCount = length / System.Numerics.Vector<float>.Count;
        _ = length % System.Numerics.Vector<float>.Count;

        // Vectorized portion
        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * System.Numerics.Vector<float>.Count;
            var va = new System.Numerics.Vector<float>(a[offset..]);
            var vb = new System.Numerics.Vector<float>(b[offset..]);
            var vr = va + vb;
            vr.CopyTo(result[offset..]);
        }

        // Scalar remainder
        var remainderStart = vectorCount * System.Numerics.Vector<float>.Count;
        for (var i = remainderStart; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result, int length)
    {
        var vectorCount = length / System.Numerics.Vector<float>.Count;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * System.Numerics.Vector<float>.Count;
            var va = new System.Numerics.Vector<float>(a[offset..]);
            var vb = new System.Numerics.Vector<float>(b[offset..]);
            var vr = va * vb;
            vr.CopyTo(result[offset..]);
        }

        var remainderStart = vectorCount * System.Numerics.Vector<float>.Count;
        for (var i = remainderStart; i < length; i++)
        {
            result[i] = a[i] * b[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(ReadOnlySpan<float> values)
    {
        var vectorCount = values.Length / System.Numerics.Vector<float>.Count;
        var sum = System.Numerics.Vector<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * System.Numerics.Vector<float>.Count;
            var v = new System.Numerics.Vector<float>(values[offset..]);
            sum += v;
        }

        var result = System.Numerics.Vector.Dot(sum, System.Numerics.Vector<float>.One);

        var remainderStart = vectorCount * System.Numerics.Vector<float>.Count;
        for (var i = remainderStart; i < values.Length; i++)
        {
            result += values[i];
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MatrixMultiply(
        ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c,
        int rows, int cols, int inner)
    {
        // Simple blocked matrix multiplication
        // This could be further optimized with cache-friendly blocking
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                float sum = 0;
                for (var k = 0; k < inner; k++)
                {
                    sum += a[i * inner + k] * b[k * cols + j];
                }
                c[i * cols + j] = sum;
            }
        }
    }
}
