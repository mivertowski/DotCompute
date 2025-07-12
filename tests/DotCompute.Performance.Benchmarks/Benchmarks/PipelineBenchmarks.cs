using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Runtime.CompilerServices;
using DotCompute.Core.Pipelines;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

/// <summary>
/// Pipeline execution benchmarks testing end-to-end workflow performance
/// Measures pipeline overhead, stage coordination, and overall efficiency
/// Performance targets: Pipeline overhead <5% of total execution, linear scaling with stages
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class PipelineBenchmarks
{
    private KernelPipeline? _simplePipeline;
    private KernelPipeline? _complexPipeline;
    private KernelPipeline? _parallelPipeline;
    private IAccelerator? _cpuAccelerator;
    private float[] _inputData = Array.Empty<float>();
    private float[] _outputData = Array.Empty<float>();
    private IBuffer? _inputBuffer;
    private IBuffer? _outputBuffer;
    private IBuffer? _intermediateBuffer1;
    private IBuffer? _intermediateBuffer2;
    
    [Params(1024, 8192, 65536, 262144)] // Different pipeline data sizes
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        try
        {
            _cpuAccelerator = new CpuAccelerator();
            
            // Prepare test data
            _inputData = new float[DataSize];
            _outputData = new float[DataSize];
            
            var random = new Random(42);
            for (int i = 0; i < DataSize; i++)
            {
                _inputData[i] = (float)random.NextDouble() * 100.0f;
            }
            
            // Create buffers
            _inputBuffer = _cpuAccelerator.CreateBuffer<float>(DataSize);
            _outputBuffer = _cpuAccelerator.CreateBuffer<float>(DataSize);
            _intermediateBuffer1 = _cpuAccelerator.CreateBuffer<float>(DataSize);
            _intermediateBuffer2 = _cpuAccelerator.CreateBuffer<float>(DataSize);
            
            // Copy input data
            _inputBuffer.CopyFrom(_inputData);
            
            // Setup pipelines
            SetupPipelines();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Pipeline setup failed: {ex.Message}\");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _inputBuffer?.Dispose();
        _outputBuffer?.Dispose();
        _intermediateBuffer1?.Dispose();
        _intermediateBuffer2?.Dispose();
        _simplePipeline?.Dispose();
        _complexPipeline?.Dispose();
        _parallelPipeline?.Dispose();
        _cpuAccelerator?.Dispose();
    }

    private void SetupPipelines()
    {
        if (_cpuAccelerator == null) return;
        
        try
        {
            // Simple 2-stage pipeline
            _simplePipeline = new KernelPipelineBuilder()
                .WithAccelerator(_cpuAccelerator)
                .AddStage(\"Add\", CreateAddKernel(), new[] { _inputBuffer!, _intermediateBuffer1! })
                .AddStage(\"Multiply\", CreateMultiplyKernel(), new[] { _intermediateBuffer1!, _outputBuffer! })
                .Build();
            
            // Complex 4-stage pipeline
            _complexPipeline = new KernelPipelineBuilder()
                .WithAccelerator(_cpuAccelerator)
                .AddStage(\"Normalize\", CreateNormalizeKernel(), new[] { _inputBuffer!, _intermediateBuffer1! })
                .AddStage(\"Transform\", CreateTransformKernel(), new[] { _intermediateBuffer1!, _intermediateBuffer2! })
                .AddStage(\"Filter\", CreateFilterKernel(), new[] { _intermediateBuffer2!, _intermediateBuffer1! })
                .AddStage(\"Finalize\", CreateFinalizeKernel(), new[] { _intermediateBuffer1!, _outputBuffer! })
                .Build();
            
            // Parallel pipeline with branching
            _parallelPipeline = new KernelPipelineBuilder()
                .WithAccelerator(_cpuAccelerator)
                .AddStage(\"Input\", CreateInputKernel(), new[] { _inputBuffer!, _intermediateBuffer1! })
                .AddParallelStages(
                    (\"Branch1\", CreateBranch1Kernel(), new[] { _intermediateBuffer1!, _intermediateBuffer2! }),
                    (\"Branch2\", CreateBranch2Kernel(), new[] { _intermediateBuffer1!, _outputBuffer! }))
                .AddStage(\"Merge\", CreateMergeKernel(), new[] { _intermediateBuffer2!, _outputBuffer!, _outputBuffer! })
                .Build();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Pipeline creation failed: {ex.Message}\");
        }
    }

    // ==================== PIPELINE CREATION HELPERS ====================
    
    private IKernel CreateAddKernel()
    {
        var kernelSource = @\"
            kernel void add_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = input[idx] + 1.0f;
                }
            }\";
        
        return CompileKernel(\"add_kernel\", kernelSource);
    }
    
    private IKernel CreateMultiplyKernel()
    {
        var kernelSource = @\"
            kernel void multiply_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = input[idx] * 2.0f;
                }
            }\";
        
        return CompileKernel(\"multiply_kernel\", kernelSource);
    }
    
    private IKernel CreateNormalizeKernel()
    {
        var kernelSource = @\"
            kernel void normalize_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    float value = input[idx];
                    output[idx] = value / sqrt(value * value + 1.0f);
                }
            }\";
        
        return CompileKernel(\"normalize_kernel\", kernelSource);
    }
    
    private IKernel CreateTransformKernel()
    {
        var kernelSource = @\"
            kernel void transform_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    float value = input[idx];
                    output[idx] = sin(value) * cos(value);
                }
            }\";
        
        return CompileKernel(\"transform_kernel\", kernelSource);
    }
    
    private IKernel CreateFilterKernel()
    {
        var kernelSource = @\"
            kernel void filter_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    float value = input[idx];
                    output[idx] = (value > 0.0f) ? value : 0.0f;  // ReLU activation
                }
            }\";
        
        return CompileKernel(\"filter_kernel\", kernelSource);
    }
    
    private IKernel CreateFinalizeKernel()
    {
        var kernelSource = @\"
            kernel void finalize_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = input[idx] * 0.5f + 0.1f;
                }
            }\";
        
        return CompileKernel(\"finalize_kernel\", kernelSource);
    }
    
    private IKernel CreateInputKernel()
    {
        var kernelSource = @\"
            kernel void input_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = input[idx];  // Pass-through
                }
            }\";
        
        return CompileKernel(\"input_kernel\", kernelSource);
    }
    
    private IKernel CreateBranch1Kernel()
    {
        var kernelSource = @\"
            kernel void branch1_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = input[idx] * input[idx];  // Square
                }
            }\";
        
        return CompileKernel(\"branch1_kernel\", kernelSource);
    }
    
    private IKernel CreateBranch2Kernel()
    {
        var kernelSource = @\"
            kernel void branch2_kernel(global float* input, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = sqrt(abs(input[idx]));  // Square root
                }
            }\";
        
        return CompileKernel(\"branch2_kernel\", kernelSource);
    }
    
    private IKernel CreateMergeKernel()
    {
        var kernelSource = @\"
            kernel void merge_kernel(global float* input1, global float* input2, global float* output, int size) {
                int idx = get_global_id(0);
                if (idx < size) {
                    output[idx] = (input1[idx] + input2[idx]) * 0.5f;  // Average
                }
            }\";
        
        return CompileKernel(\"merge_kernel\", kernelSource);
    }
    
    private IKernel CompileKernel(string name, string source)
    {
        if (_cpuAccelerator == null)
            throw new InvalidOperationException(\"CPU accelerator not initialized\");
        
        var compiler = _cpuAccelerator.CreateKernelCompiler();
        var kernelDef = new KernelDefinition
        {
            Name = name,
            Source = source,
            EntryPoint = name
        };
        
        return compiler.CompileKernel(kernelDef);
    }

    // ==================== SIMPLE PIPELINE BENCHMARKS ====================
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory(\"Simple\")]
    public void ExecuteSimplePipeline()
    {
        if (_simplePipeline == null) return;
        
        try
        {
            _simplePipeline.Execute();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Simple pipeline execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Simple\")]
    public void ExecuteSimplePipeline_Optimized()
    {
        if (_simplePipeline == null) return;
        
        try
        {
            _simplePipeline.ExecuteOptimized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Optimized simple pipeline execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Simple\")]
    public async Task ExecuteSimplePipeline_Async()
    {
        if (_simplePipeline == null) return;
        
        try
        {
            await _simplePipeline.ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Async simple pipeline execution failed: {ex.Message}\");
        }
    }

    // ==================== COMPLEX PIPELINE BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Complex\")]
    public void ExecuteComplexPipeline()
    {
        if (_complexPipeline == null) return;
        
        try
        {
            _complexPipeline.Execute();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Complex pipeline execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Complex\")]
    public void ExecuteComplexPipeline_Optimized()
    {
        if (_complexPipeline == null) return;
        
        try
        {
            _complexPipeline.ExecuteOptimized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Optimized complex pipeline execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Complex\")]
    public async Task ExecuteComplexPipeline_Async()
    {
        if (_complexPipeline == null) return;
        
        try
        {
            await _complexPipeline.ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Async complex pipeline execution failed: {ex.Message}\");
        }
    }

    // ==================== PARALLEL PIPELINE BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Parallel\")]
    public void ExecuteParallelPipeline()
    {
        if (_parallelPipeline == null) return;
        
        try
        {
            _parallelPipeline.Execute();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Parallel pipeline execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Parallel\")]
    public async Task ExecuteParallelPipeline_Async()
    {
        if (_parallelPipeline == null) return;
        
        try
        {
            await _parallelPipeline.ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Async parallel pipeline execution failed: {ex.Message}\");
        }
    }

    // ==================== STAGE-BY-STAGE BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Stage\")]
    public void ExecuteSingleStage()
    {
        if (_cpuAccelerator == null || _inputBuffer == null || _outputBuffer == null) return;
        
        try
        {
            using var kernel = CreateAddKernel();
            kernel.SetArgument(0, _inputBuffer);
            kernel.SetArgument(1, _outputBuffer);
            kernel.SetArgument(2, DataSize);
            kernel.Execute(new[] { DataSize }, new[] { 64 });
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Single stage execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Stage\")]
    public void ExecuteTwoStages_Manual()
    {
        if (_cpuAccelerator == null || _inputBuffer == null || _outputBuffer == null || _intermediateBuffer1 == null) return;
        
        try
        {
            // Stage 1
            using var kernel1 = CreateAddKernel();
            kernel1.SetArgument(0, _inputBuffer);
            kernel1.SetArgument(1, _intermediateBuffer1);
            kernel1.SetArgument(2, DataSize);
            kernel1.Execute(new[] { DataSize }, new[] { 64 });
            
            // Stage 2
            using var kernel2 = CreateMultiplyKernel();
            kernel2.SetArgument(0, _intermediateBuffer1);
            kernel2.SetArgument(1, _outputBuffer);
            kernel2.SetArgument(2, DataSize);
            kernel2.Execute(new[] { DataSize }, new[] { 64 });
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Two stages manual execution failed: {ex.Message}\");
        }
    }

    // ==================== PIPELINE OVERHEAD BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Overhead\")]
    public double MeasurePipelineOverhead()
    {
        if (_simplePipeline == null) return 0.0;
        
        try
        {
            // Measure pipeline execution time
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _simplePipeline.Execute();
            sw.Stop();
            var pipelineTime = sw.Elapsed;
            
            // Measure manual execution time
            sw.Restart();
            ExecuteTwoStages_Manual();
            sw.Stop();
            var manualTime = sw.Elapsed;
            
            // Calculate overhead percentage
            var overheadPercentage = ((pipelineTime.TotalMicroseconds - manualTime.TotalMicroseconds) / manualTime.TotalMicroseconds) * 100.0;
            return overheadPercentage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Pipeline overhead measurement failed: {ex.Message}\");
            return 0.0;
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Overhead\")]
    public long MeasureStageTransitionTime()
    {
        if (_simplePipeline == null) return 0;
        
        try
        {
            var metrics = _simplePipeline.GetMetrics();
            return metrics?.AverageStageTransitionTime ?? 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Stage transition time measurement failed: {ex.Message}\");
            return 0;
        }
    }

    // ==================== THROUGHPUT BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Throughput\")]
    public void Pipeline_Batch_Sequential()
    {
        if (_simplePipeline == null) return;
        
        const int batchCount = 10;
        
        try
        {
            for (int i = 0; i < batchCount; i++)
            {
                _simplePipeline.Execute();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Sequential batch execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Throughput\")]
    public async Task Pipeline_Batch_Parallel()
    {
        if (_simplePipeline == null) return;
        
        const int batchCount = 10;
        
        try
        {
            var tasks = new List<Task>();
            for (int i = 0; i < batchCount; i++)
            {
                tasks.Add(_simplePipeline.ExecuteAsync());
            }
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Parallel batch execution failed: {ex.Message}\");
        }
    }

    // ==================== MEMORY EFFICIENCY BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Memory\")]
    public void Pipeline_MemoryOptimized()
    {
        if (_simplePipeline == null) return;
        
        try
        {
            _simplePipeline.ExecuteWithMemoryOptimization(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Memory optimized execution failed: {ex.Message}\");
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Memory\")]
    public void Pipeline_InPlace()
    {
        if (_cpuAccelerator == null || _inputBuffer == null) return;
        
        try
        {
            // Create in-place pipeline that reuses buffers
            using var inPlacePipeline = new KernelPipelineBuilder()
                .WithAccelerator(_cpuAccelerator)
                .AddStage(\"InPlace1\", CreateAddKernel(), new[] { _inputBuffer, _inputBuffer }) // In-place operation
                .AddStage(\"InPlace2\", CreateMultiplyKernel(), new[] { _inputBuffer, _inputBuffer })
                .Build();
            
            inPlacePipeline.Execute();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"In-place execution failed: {ex.Message}\");
        }
    }

    // ==================== ERROR HANDLING BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"ErrorHandling\")]
    public void Pipeline_WithErrorHandling()
    {
        if (_simplePipeline == null) return;
        
        try
        {
            _simplePipeline.ExecuteWithErrorHandling(
                onStageError: (stage, error) => Console.WriteLine($\"Stage {stage} failed: {error.Message}\"),
                continueOnError: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Error handling execution failed: {ex.Message}\");
        }
    }

    // ==================== VALIDATION BENCHMARKS ====================
    
    [Benchmark]
    [BenchmarkCategory(\"Validation\")]
    public bool ValidatePipelineExecution()
    {
        if (_simplePipeline == null || _outputBuffer == null) return false;
        
        try
        {
            // Execute pipeline
            _simplePipeline.Execute();
            
            // Copy result back
            _outputBuffer.CopyTo(_outputData);
            
            // Validate results (expecting (input + 1.0f) * 2.0f)
            const float tolerance = 1e-5f;
            for (int i = 0; i < DataSize; i++)
            {
                var expected = (_inputData[i] + 1.0f) * 2.0f;
                if (MathF.Abs(_outputData[i] - expected) > tolerance)
                {
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Pipeline validation failed: {ex.Message}\");
            return false;
        }
    }
    
    [Benchmark]
    [BenchmarkCategory(\"Validation\")]
    public PipelineMetrics GetPipelineMetrics()
    {
        if (_simplePipeline == null) return new PipelineMetrics();
        
        try
        {
            _simplePipeline.Execute();
            return _simplePipeline.GetMetrics() ?? new PipelineMetrics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($\"Pipeline metrics collection failed: {ex.Message}\");
            return new PipelineMetrics();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DoNotOptimize<T>(T value) => GC.KeepAlive(value);
}"