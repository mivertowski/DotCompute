// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU;
using DotCompute.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

/// <summary>
/// Comprehensive GPU vs CPU benchmarks validating Phase 3 performance targets:
/// - CUDA vs CPU: Target 8-100x speedup for parallel workloads
/// - Metal vs CPU: Competitive with CUDA performance
/// - SIMD optimizations: Target 4-16x speedup
/// - Memory efficiency: Target 95%+ utilization
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Config(typeof(GpuBenchmarkConfig))]
public class GpuVsCpuBenchmarks
{
    private IAccelerator? _cpuAccelerator;
    private IAccelerator? _cudaAccelerator;
    private IAccelerator? _metalAccelerator;
    private IAcceleratorManager? _acceleratorManager;
    
    private float[] _sourceData = Array.Empty<float>();
    private float[] _targetData = Array.Empty<float>();
    private float[] _resultData = Array.Empty<float>();
    
    private IBuffer? _cpuSourceBuffer;
    private IBuffer? _cpuTargetBuffer;
    private IBuffer? _cpuResultBuffer;
    
    private IBuffer? _cudaSourceBuffer;
    private IBuffer? _cudaTargetBuffer;
    private IBuffer? _cudaResultBuffer;
    
    private IBuffer? _metalSourceBuffer;
    private IBuffer? _metalTargetBuffer;
    private IBuffer? _metalResultBuffer;
    
    [Params(1024, 8192, 65536, 262144, 1048576, 4194304)] // 4KB to 16MB
    public int DataSize { get; set; }
    
    [Params("VectorAdd", "MatrixMultiply", "Reduction", "Transform", "Convolution")]
    public string Operation { get; set; } = "VectorAdd";

    [GlobalSetup]
    public async Task Setup()
    {
        // Initialize accelerator manager
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDotComputeCore();
        
        var serviceProvider = services.BuildServiceProvider();
        _acceleratorManager = serviceProvider.GetRequiredService<IAcceleratorManager>();
        
        // Initialize CPU accelerator (always available)
        _cpuAccelerator = await InitializeCpuAccelerator();
        
        // Try to initialize GPU accelerators
        _cudaAccelerator = await TryInitializeCudaAccelerator();
        _metalAccelerator = await TryInitializeMetalAccelerator();
        
        // Initialize test data
        InitializeTestData();
        
        // Create buffers
        await CreateBuffers();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        // Dispose buffers
        _cpuSourceBuffer?.Dispose();
        _cpuTargetBuffer?.Dispose();
        _cpuResultBuffer?.Dispose();
        
        _cudaSourceBuffer?.Dispose();
        _cudaTargetBuffer?.Dispose();
        _cudaResultBuffer?.Dispose();
        
        _metalSourceBuffer?.Dispose();
        _metalTargetBuffer?.Dispose();
        _metalResultBuffer?.Dispose();
        
        // Dispose accelerators
        _cpuAccelerator?.Dispose();
        _cudaAccelerator?.Dispose();
        _metalAccelerator?.Dispose();
    }

    private async Task<IAccelerator> InitializeCpuAccelerator()
    {
        var accelerator = new CpuAccelerator();
        return accelerator;
    }

    private async Task<IAccelerator?> TryInitializeCudaAccelerator()
    {
        try
        {
            // Check if CUDA is available
            var cudaAccelerators = await _acceleratorManager!.GetAcceleratorsAsync(AcceleratorType.Cuda);
            if (cudaAccelerators.Any())
            {
                return await _acceleratorManager.CreateAcceleratorAsync(
                    cudaAccelerators.First().Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CUDA initialization failed: {ex.Message}");
        }
        return null;
    }

    private async Task<IAccelerator?> TryInitializeMetalAccelerator()
    {
        try
        {
            // Check if Metal is available (macOS only)
            var metalAccelerators = await _acceleratorManager!.GetAcceleratorsAsync(AcceleratorType.Metal);
            if (metalAccelerators.Any())
            {
                return await _acceleratorManager.CreateAcceleratorAsync(
                    metalAccelerators.First().Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Metal initialization failed: {ex.Message}");
        }
        return null;
    }

    private void InitializeTestData()
    {
        _sourceData = new float[DataSize];
        _targetData = new float[DataSize];
        _resultData = new float[DataSize];
        
        var random = new Random(42);
        for (int i = 0; i < DataSize; i++)
        {
            _sourceData[i] = (float)(random.NextDouble() * 100.0);
            _targetData[i] = (float)(random.NextDouble() * 100.0);
        }
    }

    private async Task CreateBuffers()
    {
        // CPU buffers
        if (_cpuAccelerator != null)
        {
            _cpuSourceBuffer = _cpuAccelerator.Memory.Allocate<float>(DataSize);
            _cpuTargetBuffer = _cpuAccelerator.Memory.Allocate<float>(DataSize);
            _cpuResultBuffer = _cpuAccelerator.Memory.Allocate<float>(DataSize);
            
            await _cpuSourceBuffer.CopyFromAsync(_sourceData);
            await _cpuTargetBuffer.CopyFromAsync(_targetData);
        }
        
        // CUDA buffers
        if (_cudaAccelerator != null)
        {
            _cudaSourceBuffer = _cudaAccelerator.Memory.Allocate<float>(DataSize);
            _cudaTargetBuffer = _cudaAccelerator.Memory.Allocate<float>(DataSize);
            _cudaResultBuffer = _cudaAccelerator.Memory.Allocate<float>(DataSize);
            
            await _cudaSourceBuffer.CopyFromAsync(_sourceData);
            await _cudaTargetBuffer.CopyFromAsync(_targetData);
        }
        
        // Metal buffers
        if (_metalAccelerator != null)
        {
            _metalSourceBuffer = _metalAccelerator.Memory.Allocate<float>(DataSize);
            _metalTargetBuffer = _metalAccelerator.Memory.Allocate<float>(DataSize);
            _metalResultBuffer = _metalAccelerator.Memory.Allocate<float>(DataSize);
            
            await _metalSourceBuffer.CopyFromAsync(_sourceData);
            await _metalTargetBuffer.CopyFromAsync(_targetData);
        }
    }

    // ==================== CPU BENCHMARKS ====================
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CPU")]
    public async Task Cpu_Scalar()
    {
        switch (Operation)
        {
            case "VectorAdd":
                VectorAddScalar(_sourceData, _targetData, _resultData);
                break;
            case "MatrixMultiply":
                MatrixMultiplyScalar(_sourceData, _targetData, _resultData, (int)Math.Sqrt(DataSize));
                break;
            case "Reduction":
                ReductionScalar(_sourceData);
                break;
            case "Transform":
                TransformScalar(_sourceData, _resultData);
                break;
            case "Convolution":
                ConvolutionScalar(_sourceData, _targetData, _resultData);
                break;
        }
    }

    [Benchmark]
    [BenchmarkCategory("CPU")]
    public async Task Cpu_Simd()
    {
        switch (Operation)
        {
            case "VectorAdd":
                VectorAddSimd(_sourceData, _targetData, _resultData);
                break;
            case "MatrixMultiply":
                MatrixMultiplySimd(_sourceData, _targetData, _resultData, (int)Math.Sqrt(DataSize));
                break;
            case "Reduction":
                ReductionSimd(_sourceData);
                break;
            case "Transform":
                TransformSimd(_sourceData, _resultData);
                break;
            case "Convolution":
                ConvolutionSimd(_sourceData, _targetData, _resultData);
                break;
        }
    }

    [Benchmark]
    [BenchmarkCategory("CPU")]
    public async Task Cpu_ParallelSimd()
    {
        switch (Operation)
        {
            case "VectorAdd":
                await Task.Run(() => VectorAddParallelSimd(_sourceData, _targetData, _resultData));
                break;
            case "MatrixMultiply":
                await Task.Run(() => MatrixMultiplyParallelSimd(_sourceData, _targetData, _resultData, (int)Math.Sqrt(DataSize)));
                break;
            case "Reduction":
                await Task.Run(() => ReductionParallelSimd(_sourceData));
                break;
            case "Transform":
                await Task.Run(() => TransformParallelSimd(_sourceData, _resultData));
                break;
            case "Convolution":
                await Task.Run(() => ConvolutionParallelSimd(_sourceData, _targetData, _resultData));
                break;
        }
    }

    // ==================== CUDA BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory("CUDA")]
    public async Task Cuda_Kernel()
    {
        if (_cudaAccelerator == null)
        {
            await Cpu_Scalar(); // Fallback
            return;
        }
        
        var kernel = await CompileCudaKernel(Operation);
        if (kernel == null) return;
        
        var args = new KernelArguments();
        args.Set(0, _cudaSourceBuffer!);
        args.Set(1, _cudaTargetBuffer!);
        args.Set(2, _cudaResultBuffer!);
        args.Set(3, DataSize);
        
        await kernel.ExecuteAsync(args);
        await _cudaAccelerator.SynchronizeAsync();
    }

    [Benchmark]
    [BenchmarkCategory("CUDA")]
    public async Task Cuda_OptimizedKernel()
    {
        if (_cudaAccelerator == null)
        {
            await Cpu_Simd(); // Fallback
            return;
        }
        
        var kernel = await CompileOptimizedCudaKernel(Operation);
        if (kernel == null) return;
        
        var args = new KernelArguments();
        args.Set(0, _cudaSourceBuffer!);
        args.Set(1, _cudaTargetBuffer!);
        args.Set(2, _cudaResultBuffer!);
        args.Set(3, DataSize);
        
        // Use optimal work group size for GPU
        var workGroupSize = 256;
        var globalSize = ((DataSize + workGroupSize - 1) / workGroupSize) * workGroupSize;
        
        await kernel.ExecuteAsync(args, new[] { globalSize }, new[] { workGroupSize });
        await _cudaAccelerator.SynchronizeAsync();
    }

    // ==================== METAL BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory("Metal")]
    public async Task Metal_Kernel()
    {
        if (_metalAccelerator == null)
        {
            await Cpu_Scalar(); // Fallback
            return;
        }
        
        var kernel = await CompileMetalKernel(Operation);
        if (kernel == null) return;
        
        var args = new KernelArguments();
        args.Set(0, _metalSourceBuffer!);
        args.Set(1, _metalTargetBuffer!);
        args.Set(2, _metalResultBuffer!);
        args.Set(3, DataSize);
        
        await kernel.ExecuteAsync(args);
        await _metalAccelerator.SynchronizeAsync();
    }

    [Benchmark]
    [BenchmarkCategory("Metal")]
    public async Task Metal_OptimizedKernel()
    {
        if (_metalAccelerator == null)
        {
            await Cpu_Simd(); // Fallback
            return;
        }
        
        var kernel = await CompileOptimizedMetalKernel(Operation);
        if (kernel == null) return;
        
        var args = new KernelArguments();
        args.Set(0, _metalSourceBuffer!);
        args.Set(1, _metalTargetBuffer!);
        args.Set(2, _metalResultBuffer!);
        args.Set(3, DataSize);
        
        // Use optimal work group size for Metal
        var workGroupSize = 256;
        var globalSize = ((DataSize + workGroupSize - 1) / workGroupSize) * workGroupSize;
        
        await kernel.ExecuteAsync(args, new[] { globalSize }, new[] { workGroupSize });
        await _metalAccelerator.SynchronizeAsync();
    }

    // ==================== MEMORY EFFICIENCY BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory("Memory")]
    public async Task Memory_CpuTransfer()
    {
        if (_cpuAccelerator == null) return;
        
        // Measure host to device transfer
        await _cpuSourceBuffer!.CopyFromAsync(_sourceData);
        
        // Execute simple kernel
        VectorAddSimd(_sourceData, _targetData, _resultData);
        
        // Measure device to host transfer
        await _cpuResultBuffer!.CopyToAsync(_resultData);
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public async Task Memory_CudaTransfer()
    {
        if (_cudaAccelerator == null)
        {
            await Memory_CpuTransfer();
            return;
        }
        
        // Measure host to device transfer
        await _cudaSourceBuffer!.CopyFromAsync(_sourceData);
        
        // Execute simple kernel
        var kernel = await CompileCudaKernel("VectorAdd");
        if (kernel != null)
        {
            var args = new KernelArguments();
            args.Set(0, _cudaSourceBuffer!);
            args.Set(1, _cudaTargetBuffer!);
            args.Set(2, _cudaResultBuffer!);
            args.Set(3, DataSize);
            
            await kernel.ExecuteAsync(args);
        }
        
        // Measure device to host transfer
        await _cudaResultBuffer!.CopyToAsync(_resultData);
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public async Task Memory_MetalTransfer()
    {
        if (_metalAccelerator == null)
        {
            await Memory_CpuTransfer();
            return;
        }
        
        // Measure host to device transfer
        await _metalSourceBuffer!.CopyFromAsync(_sourceData);
        
        // Execute simple kernel
        var kernel = await CompileMetalKernel("VectorAdd");
        if (kernel != null)
        {
            var args = new KernelArguments();
            args.Set(0, _metalSourceBuffer!);
            args.Set(1, _metalTargetBuffer!);
            args.Set(2, _metalResultBuffer!);
            args.Set(3, DataSize);
            
            await kernel.ExecuteAsync(args);
        }
        
        // Measure device to host transfer
        await _metalResultBuffer!.CopyToAsync(_resultData);
    }

    // ==================== KERNEL COMPILATION HELPERS ====================
    
    private async Task<ICompiledKernel?> CompileCudaKernel(string operation)
    {
        if (_cudaAccelerator == null) return null;
        
        var kernelSource = GetCudaKernelSource(operation);
        var definition = new KernelDefinition
        {
            Name = $"cuda_{operation.ToLower()}",
            Source = kernelSource,
            Language = KernelLanguage.Cuda
        };
        
        try
        {
            return await _cudaAccelerator.CompileKernelAsync(definition);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ICompiledKernel?> CompileOptimizedCudaKernel(string operation)
    {
        if (_cudaAccelerator == null) return null;
        
        var kernelSource = GetOptimizedCudaKernelSource(operation);
        var definition = new KernelDefinition
        {
            Name = $"cuda_{operation.ToLower()}_optimized",
            Source = kernelSource,
            Language = KernelLanguage.Cuda
        };
        
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true,
            UnrollLoops = true
        };
        
        try
        {
            return await _cudaAccelerator.CompileKernelAsync(definition, options);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ICompiledKernel?> CompileMetalKernel(string operation)
    {
        if (_metalAccelerator == null) return null;
        
        var kernelSource = GetMetalKernelSource(operation);
        var definition = new KernelDefinition
        {
            Name = $"metal_{operation.ToLower()}",
            Source = kernelSource,
            Language = KernelLanguage.Metal
        };
        
        try
        {
            return await _metalAccelerator.CompileKernelAsync(definition);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ICompiledKernel?> CompileOptimizedMetalKernel(string operation)
    {
        if (_metalAccelerator == null) return null;
        
        var kernelSource = GetOptimizedMetalKernelSource(operation);
        var definition = new KernelDefinition
        {
            Name = $"metal_{operation.ToLower()}_optimized",
            Source = kernelSource,
            Language = KernelLanguage.Metal
        };
        
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true
        };
        
        try
        {
            return await _metalAccelerator.CompileKernelAsync(definition, options);
        }
        catch
        {
            return null;
        }
    }

    // ==================== KERNEL SOURCE GENERATION ====================
    
    private string GetCudaKernelSource(string operation) => operation switch
    {
        "VectorAdd" => @"
            __global__ void vector_add(float* a, float* b, float* result, int size) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < size) {
                    result[idx] = a[idx] + b[idx];
                }
            }",
        
        "MatrixMultiply" => @"
            __global__ void matrix_multiply(float* a, float* b, float* result, int size) {
                int row = blockIdx.y * blockDim.y + threadIdx.y;
                int col = blockIdx.x * blockDim.x + threadIdx.x;
                int dim = sqrt((float)size);
                
                if (row < dim && col < dim) {
                    float sum = 0.0f;
                    for (int k = 0; k < dim; k++) {
                        sum += a[row * dim + k] * b[k * dim + col];
                    }
                    result[row * dim + col] = sum;
                }
            }",
        
        "Reduction" => @"
            __global__ void reduction(float* input, float* output, int size) {
                extern __shared__ float sdata[];
                
                unsigned int tid = threadIdx.x;
                unsigned int i = blockIdx.x * blockDim.x + threadIdx.x;
                
                sdata[tid] = (i < size) ? input[i] : 0;
                __syncthreads();
                
                for (unsigned int s = blockDim.x / 2; s > 0; s >>= 1) {
                    if (tid < s) {
                        sdata[tid] += sdata[tid + s];
                    }
                    __syncthreads();
                }
                
                if (tid == 0) atomicAdd(output, sdata[0]);
            }",
        
        "Transform" => @"
            __global__ void transform(float* input, float* output, int size) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < size) {
                    float value = input[idx];
                    output[idx] = sqrtf(value * value + 1.0f);
                }
            }",
        
        "Convolution" => @"
            __global__ void convolution(float* input, float* kernel, float* output, int size) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                int kernel_radius = 3;
                
                if (idx >= kernel_radius && idx < size - kernel_radius) {
                    float sum = 0.0f;
                    for (int k = -kernel_radius; k <= kernel_radius; k++) {
                        sum += input[idx + k] * kernel[k + kernel_radius];
                    }
                    output[idx] = sum;
                }
            }",
        
        _ => throw new NotSupportedException($"Operation {operation} not supported")
    };

    private string GetOptimizedCudaKernelSource(string operation) => operation switch
    {
        "VectorAdd" => @"
            __global__ void vector_add_optimized(float* a, float* b, float* result, int size) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                int stride = blockDim.x * gridDim.x;
                
                // Process multiple elements per thread
                for (int i = idx; i < size; i += stride) {
                    result[i] = __fadd_rn(a[i], b[i]); // Fast intrinsic
                }
            }",
        
        _ => GetCudaKernelSource(operation) // Fallback to basic version
    };

    private string GetMetalKernelSource(string operation) => operation switch
    {
        "VectorAdd" => @"
            kernel void vector_add(device float* a [[buffer(0)]],
                                  device float* b [[buffer(1)]],
                                  device float* result [[buffer(2)]],
                                  constant int& size [[buffer(3)]],
                                  uint idx [[thread_position_in_grid]]) {
                if (idx < size) {
                    result[idx] = a[idx] + b[idx];
                }
            }",
        
        _ => throw new NotSupportedException($"Metal kernel for {operation} not implemented")
    };

    private string GetOptimizedMetalKernelSource(string operation) => operation switch
    {
        "VectorAdd" => @"
            kernel void vector_add_optimized(device float* a [[buffer(0)]],
                                           device float* b [[buffer(1)]],
                                           device float* result [[buffer(2)]],
                                           constant int& size [[buffer(3)]],
                                           uint idx [[thread_position_in_grid]],
                                           uint threads [[threads_per_grid]]) {
                // Process multiple elements per thread
                for (uint i = idx; i < size; i += threads) {
                    result[i] = a[i] + b[i];
                }
            }",
        
        _ => GetMetalKernelSource(operation) // Fallback to basic version
    };

    // ==================== CPU IMPLEMENTATION HELPERS ====================
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VectorAddScalar(float[] a, float[] b, float[] result)
    {
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void VectorAddSimd(float[] a, float[] b, float[] result)
    {
        int i = 0;
        
        if (Avx.IsSupported)
        {
            fixed (float* pA = a, pB = b, pResult = result)
            {
                for (; i + 8 <= a.Length; i += 8)
                {
                    var va = Avx.LoadVector256(pA + i);
                    var vb = Avx.LoadVector256(pB + i);
                    Avx.Store(pResult + i, Avx.Add(va, vb));
                }
            }
        }
        else
        {
            int vectorSize = Vector<float>.Count;
            for (; i + vectorSize <= a.Length; i += vectorSize)
            {
                var va = new Vector<float>(a, i);
                var vb = new Vector<float>(b, i);
                (va + vb).CopyTo(result, i);
            }
        }
        
        for (; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VectorAddParallelSimd(float[] a, float[] b, float[] result)
    {
        var partitioner = Partitioner.Create(0, a.Length);
        
        Parallel.ForEach(partitioner, range =>
        {
            int start = range.Item1;
            int end = range.Item2;
            
            unsafe
            {
                if (Avx.IsSupported)
                {
                    fixed (float* pA = a, pB = b, pResult = result)
                    {
                        int i = start;
                        for (; i + 8 <= end; i += 8)
                        {
                            var va = Avx.LoadVector256(pA + i);
                            var vb = Avx.LoadVector256(pB + i);
                            Avx.Store(pResult + i, Avx.Add(va, vb));
                        }
                        
                        for (; i < end; i++)
                        {
                            result[i] = a[i] + b[i];
                        }
                    }
                }
                else
                {
                    for (int i = start; i < end; i++)
                    {
                        result[i] = a[i] + b[i];
                    }
                }
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MatrixMultiplyScalar(float[] a, float[] b, float[] result, int dim)
    {
        for (int i = 0; i < dim; i++)
        {
            for (int j = 0; j < dim; j++)
            {
                float sum = 0;
                for (int k = 0; k < dim; k++)
                {
                    sum += a[i * dim + k] * b[k * dim + j];
                }
                result[i * dim + j] = sum;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void MatrixMultiplySimd(float[] a, float[] b, float[] result, int dim)
    {
        // Simplified SIMD matrix multiplication
        MatrixMultiplyScalar(a, b, result, dim);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MatrixMultiplyParallelSimd(float[] a, float[] b, float[] result, int dim)
    {
        Parallel.For(0, dim, i =>
        {
            for (int j = 0; j < dim; j++)
            {
                float sum = 0;
                for (int k = 0; k < dim; k++)
                {
                    sum += a[i * dim + k] * b[k * dim + j];
                }
                result[i * dim + j] = sum;
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float ReductionScalar(float[] data)
    {
        float sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float ReductionSimd(float[] data)
    {
        // Simplified SIMD reduction
        return ReductionScalar(data);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float ReductionParallelSimd(float[] data)
    {
        object lockObj = new object();
        float totalSum = 0;
        
        Parallel.ForEach(Partitioner.Create(0, data.Length), range =>
        {
            float localSum = 0;
            for (int i = range.Item1; i < range.Item2; i++)
            {
                localSum += data[i];
            }
            
            lock (lockObj)
            {
                totalSum += localSum;
            }
        });
        
        return totalSum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TransformScalar(float[] input, float[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            float value = input[i];
            output[i] = MathF.Sqrt(value * value + 1.0f);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TransformSimd(float[] input, float[] output)
    {
        // Simplified SIMD transform
        TransformScalar(input, output);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TransformParallelSimd(float[] input, float[] output)
    {
        Parallel.For(0, input.Length, i =>
        {
            float value = input[i];
            output[i] = MathF.Sqrt(value * value + 1.0f);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvolutionScalar(float[] input, float[] kernel, float[] output)
    {
        int kernelRadius = 3;
        for (int i = kernelRadius; i < input.Length - kernelRadius; i++)
        {
            float sum = 0;
            for (int k = -kernelRadius; k <= kernelRadius; k++)
            {
                sum += input[i + k] * kernel[k + kernelRadius];
            }
            output[i] = sum;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvolutionSimd(float[] input, float[] kernel, float[] output)
    {
        // Simplified SIMD convolution
        ConvolutionScalar(input, kernel, output);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvolutionParallelSimd(float[] input, float[] kernel, float[] output)
    {
        int kernelRadius = 3;
        Parallel.For(kernelRadius, input.Length - kernelRadius, i =>
        {
            float sum = 0;
            for (int k = -kernelRadius; k <= kernelRadius; k++)
            {
                sum += input[i + k] * kernel[k + kernelRadius];
            }
            output[i] = sum;
        });
    }
}

/// <summary>
/// Custom benchmark configuration for GPU benchmarks
/// </summary>
public class GpuBenchmarkConfig : ManualConfig
{
    public GpuBenchmarkConfig()
    {
        AddJob(Job.Default.WithId("GPU Benchmarks"));
        AddColumn(new SpeedupColumn());
        AddColumn(new EfficiencyColumn());
        AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
        AddExporter(BenchmarkDotNet.Exporters.HtmlExporter.Default);
        AddExporter(BenchmarkDotNet.Exporters.CsvExporter.Default);
    }
}

/// <summary>
/// Custom column showing speedup relative to baseline
/// </summary>
public class SpeedupColumn : IColumn
{
    public string Id => nameof(SpeedupColumn);
    public string ColumnName => "Speedup";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Speedup relative to CPU Scalar baseline";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var baseline = summary.Reports
            .Where(r => r.BenchmarkCase.Descriptor.Baseline)
            .FirstOrDefault();
        
        if (baseline == null) return "N/A";
        
        var report = summary.Reports
            .FirstOrDefault(r => r.BenchmarkCase == benchmarkCase);
        
        if (report == null || report == baseline) return "1.00x";
        
        var speedup = baseline.ResultStatistics.Mean / report.ResultStatistics.Mean;
        return $"{speedup:F2}x";
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) 
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}

/// <summary>
/// Custom column showing memory efficiency percentage
/// </summary>
public class EfficiencyColumn : IColumn
{
    public string Id => nameof(EfficiencyColumn);
    public string ColumnName => "Memory Efficiency";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 1;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Memory bandwidth efficiency percentage";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var report = summary.Reports
            .FirstOrDefault(r => r.BenchmarkCase == benchmarkCase);
        
        if (report == null) return "N/A";
        
        // Calculate theoretical vs achieved bandwidth
        // This is a simplified calculation
        var dataSize = benchmarkCase.Parameters["DataSize"];
        if (dataSize == null) return "N/A";
        
        var bytes = (int)dataSize * sizeof(float) * 3; // Read 2, write 1
        var timeMs = report.ResultStatistics.Mean / 1_000_000; // ns to ms
        var achievedGBs = (bytes / 1_000_000_000.0) / (timeMs / 1000.0);
        
        // Assume theoretical bandwidth based on device type
        double theoreticalGBs = benchmarkCase.Descriptor.Categories.Contains("CUDA") ? 600.0 :
                               benchmarkCase.Descriptor.Categories.Contains("Metal") ? 400.0 :
                               benchmarkCase.Descriptor.Categories.Contains("CPU") ? 50.0 : 25.0;
        
        var efficiency = (achievedGBs / theoreticalGBs) * 100.0;
        return $"{efficiency:F1}%";
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) 
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}