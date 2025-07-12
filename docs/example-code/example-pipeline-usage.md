# Pipeline System Examples

## 🔗 Complete Pipeline Orchestration Guide

### **Basic Pipeline Construction**

```csharp
using DotCompute.Core.Pipelines;

// Simple data processing pipeline
var pipeline = new KernelPipelineBuilder()
    .SetName("DataProcessingPipeline")
    .SetId("data-proc-001")
    
    // Stage 1: Load data
    .AddStage("LoadData", new DataLoadStage())
        .WithTimeout(TimeSpan.FromSeconds(30))
        .WithMetadata("source", "database")
    
    // Stage 2: Validate and clean
    .AddStage("ValidateData", new ValidationStage())
        .DependsOn("LoadData")
        .SetParallel(true)
    
    // Stage 3: Transform data
    .AddStage("TransformData", new TransformStage())
        .DependsOn("ValidateData")
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernel("DataTransform")
    
    // Stage 4: Save results
    .AddStage("SaveResults", new SaveStage())
        .DependsOn("TransformData")
        .WithMetadata("destination", "output.parquet")
    
    .Build();

// Execute pipeline
var context = new PipelineExecutionContext
{
    Inputs = new Dictionary<string, object>
    {
        ["inputPath"] = "/data/input.csv",
        ["outputPath"] = "/data/output.parquet",
        ["batchSize"] = 10000
    },
    Device = accelerator,
    MemoryManager = memoryManager
};

var result = await pipeline.ExecuteAsync(context);

if (result.Success)
{
    Console.WriteLine($"✅ Pipeline completed in {result.Metrics.Duration.TotalSeconds:F2}s");
}
```

### **Complex Machine Learning Training Pipeline**

```csharp
// Real-world ML training pipeline with error handling and optimization
var mlTrainingPipeline = new KernelPipelineBuilder()
    .SetName("MLTrainingPipeline")
    .SetId("ml-train-001")
    
    // Data preparation stages
    .AddStage("LoadDataset", new DatasetLoadStage())
        .WithMemoryLimit(2_000_000_000) // 2GB limit
        .WithTimeout(TimeSpan.FromMinutes(5))
        .WithMetadata("format", "numpy")
    
    .AddStage("PreprocessData", new PreprocessStage())
        .DependsOn("LoadDataset")
        .SetParallel(true)
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernels("Normalize", "Augment")
    
    .AddStage("SplitDataset", new DataSplitStage())
        .DependsOn("PreprocessData")
        .WithMetadata("trainRatio", 0.8)
        .WithMetadata("validationRatio", 0.2)
    
    // Model training stages
    .AddStage("InitializeModel", new ModelInitStage())
        .DependsOn("SplitDataset")
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernel("InitializeWeights")
    
    .AddStage("TrainingLoop", new TrainingLoopStage())
        .DependsOn("InitializeModel")
        .SetParallel(true)
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernels("ForwardPass", "BackwardPass", "UpdateWeights")
        .WithRetryPolicy(RetryPolicy.ExponentialBackoff(maxRetries: 3))
    
    .AddStage("Validation", new ValidationStage())
        .DependsOn("TrainingLoop")
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernel("ComputeAccuracy")
    
    .AddStage("ModelCheckpoint", new CheckpointStage())
        .DependsOn("Validation")
        .WithConditionalExecution(ctx => 
        {
            var accuracy = (double)ctx.State["validation_accuracy"];
            return accuracy > 0.95; // Only checkpoint if accuracy > 95%
        })
    
    // Pipeline configuration
    .WithErrorHandling((exception, context) =>
    {
        Console.WriteLine($"❌ Error in stage {context.CurrentStage}: {exception.Message}");
        
        // Custom error handling based on stage
        return context.CurrentStage switch
        {
            "LoadDataset" => ErrorHandlingResult.Retry,
            "TrainingLoop" => ErrorHandlingResult.Continue,
            "ModelCheckpoint" => ErrorHandlingResult.Skip,
            _ => ErrorHandlingResult.Abort
        };
    })
    .WithOptimization(opt =>
    {
        opt.EnableParallelMerging = true;
        opt.EnableMemoryOptimization = true;
        opt.EnableKernelFusion = true;
        opt.CacheCompiledKernels = true;
    })
    .WithEventHandler(evt =>
    {
        Console.WriteLine($"📊 {evt.Type}: {evt.Message}");
        if (evt.Data?.ContainsKey("Duration") == true)
        {
            Console.WriteLine($"   ⏱️ Duration: {evt.Data["Duration"]}ms");
        }
    })
    .Build();

// Training execution with monitoring
var trainingContext = new PipelineExecutionContext
{
    Inputs = new Dictionary<string, object>
    {
        ["datasetPath"] = "/data/training_set.npy",
        ["modelConfig"] = new ModelConfiguration
        {
            LayerSizes = new[] { 784, 256, 128, 10 },
            LearningRate = 0.001f,
            BatchSize = 32,
            Epochs = 100
        },
        ["checkpointPath"] = "/models/checkpoint_{epoch}.pth"
    },
    Device = cudaAccelerator,
    MemoryManager = memoryManager,
    Options = new PipelineExecutionOptions
    {
        ContinueOnError = true,
        MaxConcurrency = 4,
        MemoryLimit = 8_000_000_000 // 8GB
    },
    Profiler = new PerformanceProfiler()
};

var trainingResult = await mlTrainingPipeline.ExecuteAsync(trainingContext);

// Analyze training results
if (trainingResult.Success)
{
    var finalAccuracy = (double)trainingResult.Outputs["final_accuracy"];
    var trainingTime = trainingResult.Metrics.Duration;
    var memoryUsage = trainingResult.Metrics.MemoryUsage.PeakBytes;
    
    Console.WriteLine($"🎉 Training completed successfully!");
    Console.WriteLine($"   📈 Final accuracy: {finalAccuracy:P2}");
    Console.WriteLine($"   ⏱️ Training time: {trainingTime.TotalMinutes:F1} minutes");
    Console.WriteLine($"   💾 Peak memory: {memoryUsage / 1024.0 / 1024.0:F1} MB");
}
```

### **Image Processing Pipeline with GPU Acceleration**

```csharp
// High-performance image processing pipeline
var imageProcessingPipeline = new KernelPipelineBuilder()
    .SetName("ImageProcessingPipeline")
    .SetId("img-proc-001")
    
    // Input/Output stages
    .AddStage("LoadImages", new BatchImageLoadStage())
        .WithMetadata("supportedFormats", new[] { ".jpg", ".png", ".bmp" })
        .WithTimeout(TimeSpan.FromMinutes(2))
    
    .AddStage("DecodeImages", new ImageDecodeStage())
        .DependsOn("LoadImages")
        .SetParallel(true)
        .SetAcceleratorType(AcceleratorType.CPU) // CPU better for decode
    
    // Preprocessing stages (parallel execution)
    .AddStage("Resize", new ImageResizeStage())
        .DependsOn("DecodeImages")
        .SetParallel(true)
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernel("BilinearResize")
    
    .AddStage("ColorCorrection", new ColorCorrectionStage())
        .DependsOn("DecodeImages") // Can run in parallel with Resize
        .SetParallel(true)
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernels("GammaCorrection", "WhiteBalance")
    
    // Filter stages (sequential - depend on both resize and color correction)
    .AddStage("ApplyFilters", new FilterStage())
        .DependsOn("Resize", "ColorCorrection")
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernels("GaussianBlur", "UnsharpMask", "EdgeDetection")
    
    .AddStage("NoiseReduction", new NoiseReductionStage())
        .DependsOn("ApplyFilters")
        .SetAcceleratorType(AcceleratorType.CUDA)
        .WithKernel("BilateralFilter")
    
    // Output stages
    .AddStage("EncodeImages", new ImageEncodeStage())
        .DependsOn("NoiseReduction")
        .SetParallel(true)
        .WithMetadata("outputFormat", "PNG")
        .WithMetadata("compressionLevel", 6)
    
    .AddStage("SaveImages", new ImageSaveStage())
        .DependsOn("EncodeImages")
        .SetParallel(true)
    
    // Advanced optimization settings
    .WithOptimization(opt =>
    {
        opt.EnableParallelMerging = true;
        opt.EnableMemoryOptimization = true;
        opt.EnableKernelFusion = true;
        opt.OptimizeMemoryTransfers = true;
        opt.EnablePipelinePreloading = true;
    })
    .WithMemoryManagement(mem =>
    {
        mem.EnableMemoryPooling = true;
        mem.PreallocateBuffers = true;
        mem.MaxBufferSize = 512_000_000; // 512MB per buffer
    })
    .Build();

// Batch processing execution
var batchContext = new PipelineExecutionContext
{
    Inputs = new Dictionary<string, object>
    {
        ["inputDirectory"] = "/images/input/",
        ["outputDirectory"] = "/images/processed/",
        ["batchSize"] = 16,
        ["targetSize"] = new Size(1920, 1080),
        ["filterSettings"] = new FilterSettings
        {
            BlurRadius = 2.0f,
            UnsharpAmount = 0.5f,
            NoiseReductionStrength = 0.3f
        }
    },
    Device = cudaAccelerator,
    MemoryManager = memoryManager
};

var processingResult = await imageProcessingPipeline.ExecuteAsync(batchContext);

Console.WriteLine($"Processed {processingResult.Outputs["processedCount"]} images");
Console.WriteLine($"Total time: {processingResult.Metrics.Duration.TotalSeconds:F1}s");
Console.WriteLine($"Average per image: {processingResult.Metrics.Duration.TotalMilliseconds / (int)processingResult.Outputs["processedCount"]:F1}ms");
```

### **Custom Pipeline Stages**

```csharp
// Custom stage for ML model inference
public class ModelInferenceStage : IPipelineStage
{
    public string Id { get; } = "model-inference";
    public string Name { get; } = "Model Inference";
    public PipelineStageType Type { get; } = PipelineStageType.Compute;
    public IReadOnlyList<string> Dependencies { get; private set; } = Array.Empty<string>();
    
    private readonly string _modelPath;
    private readonly InferenceConfig _config;
    private ICompiledKernel? _forwardPassKernel;
    
    public ModelInferenceStage(string modelPath, InferenceConfig config)
    {
        _modelPath = modelPath;
        _config = config;
    }
    
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryTracker = new MemoryUsageTracker();
        
        try
        {
            // Load model if not already loaded
            if (!context.State.ContainsKey("model"))
            {
                var model = await LoadModelAsync(_modelPath, context.Device, cancellationToken);
                context.State["model"] = model;
                memoryTracker.RecordAllocation(model.SizeInBytes);
            }
            
            // Get input data
            var inputBatch = (float[])context.Inputs["inputBatch"];
            var batchSize = (int)context.Inputs["batchSize"];
            var inputSize = inputBatch.Length / batchSize;
            
            // Allocate output buffer
            var outputSize = _config.OutputSize;
            var outputBatch = new float[batchSize * outputSize];
            memoryTracker.RecordAllocation(outputBatch.Length * sizeof(float));
            
            // Prepare device buffers
            var inputBuffer = await context.MemoryManager.AllocateAsync<float>(inputBatch.Length);
            var outputBuffer = await context.MemoryManager.AllocateAsync<float>(outputBatch.Length);
            
            await inputBuffer.CopyFromAsync(inputBatch);
            memoryTracker.RecordAllocation(inputBuffer.SizeInBytes);
            memoryTracker.RecordAllocation(outputBuffer.SizeInBytes);
            
            // Execute inference kernel
            _forwardPassKernel ??= await CompileInferenceKernelAsync(context.Device);
            
            var inferenceContext = new KernelExecutionContext
            {
                GlobalSize = new[] { batchSize },
                Arguments = new object[] { inputBuffer, outputBuffer, inputSize, outputSize }
            };
            
            await _forwardPassKernel.ExecuteAsync(inferenceContext, cancellationToken);
            
            // Copy results back
            await outputBuffer.CopyToAsync(outputBatch);
            
            // Cleanup device buffers
            await inputBuffer.DisposeAsync();
            await outputBuffer.DisposeAsync();
            memoryTracker.RecordDeallocation(inputBuffer.SizeInBytes);
            memoryTracker.RecordDeallocation(outputBuffer.SizeInBytes);
            
            stopwatch.Stop();
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                Outputs = new Dictionary<string, object>
                {
                    ["inferenceResults"] = outputBatch,
                    ["throughput"] = batchSize / stopwatch.Elapsed.TotalSeconds
                },
                MemoryUsage = memoryTracker.GetUsageStats(),
                Metrics = new Dictionary<string, double>
                {
                    ["ComputeUtilization"] = await GetComputeUtilization(context.Device),
                    ["MemoryBandwidthUtilization"] = CalculateMemoryBandwidth(inputBatch.Length + outputBatch.Length, stopwatch.Elapsed),
                    ["InferenceLatency"] = stopwatch.Elapsed.TotalMilliseconds / batchSize
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex,
                MemoryUsage = memoryTracker.GetUsageStats()
            };
        }
    }
    
    private async Task<ICompiledKernel> CompileInferenceKernelAsync(IAccelerator device)
    {
        var kernelDefinition = new KernelDefinition
        {
            Name = "ModelInference",
            Source = GenerateInferenceKernelSource(_config),
            EntryPoint = "forward_pass"
        };
        
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Aggressive,
            EnableFastMath = true,
            CompilerFlags = device.Type switch
            {
                AcceleratorType.Cuda => "-use_fast_math -maxrregcount=64",
                AcceleratorType.Metal => "-ffast-math",
                _ => ""
            }
        };
        
        return await device.CompileKernelAsync(kernelDefinition, compilationOptions);
    }
    
    private string GenerateInferenceKernelSource(InferenceConfig config)
    {
        // Generate optimized kernel code based on model architecture
        return config.ModelType switch
        {
            ModelType.DenseNeuralNetwork => GenerateDenseNNKernel(config),
            ModelType.ConvolutionalNeuralNetwork => GenerateCNNKernel(config),
            ModelType.TransformerModel => GenerateTransformerKernel(config),
            _ => throw new NotSupportedException($"Model type {config.ModelType} not supported")
        };
    }
    
    public PipelineStageValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        // Validate model file exists
        if (!File.Exists(_modelPath))
        {
            errors.Add($"Model file not found: {_modelPath}");
        }
        
        // Validate configuration
        if (_config.InputSize <= 0)
        {
            errors.Add("Input size must be positive");
        }
        
        if (_config.OutputSize <= 0)
        {
            errors.Add("Output size must be positive");
        }
        
        // Performance warnings
        if (_config.BatchSize > 1024)
        {
            warnings.Add("Large batch sizes may exceed GPU memory limits");
        }
        
        return new PipelineStageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }
}

// Advanced data processing stage with adaptive algorithms
public class AdaptiveProcessingStage : IPipelineStage
{
    public string Id { get; } = "adaptive-processing";
    public string Name { get; } = "Adaptive Processing";
    public PipelineStageType Type { get; } = PipelineStageType.Adaptive;
    public IReadOnlyList<string> Dependencies { get; private set; } = Array.Empty<string>();
    
    private readonly Dictionary<string, IProcessingAlgorithm> _algorithms;
    private readonly PerformanceMonitor _performanceMonitor;
    
    public AdaptiveProcessingStage()
    {
        _algorithms = new Dictionary<string, IProcessingAlgorithm>
        {
            ["fast"] = new FastProcessingAlgorithm(),
            ["balanced"] = new BalancedProcessingAlgorithm(),
            ["quality"] = new QualityProcessingAlgorithm()
        };
        _performanceMonitor = new PerformanceMonitor();
    }
    
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Analyze input data characteristics
            var inputData = (float[])context.Inputs["data"];
            var dataCharacteristics = AnalyzeDataCharacteristics(inputData);
            
            // Select optimal algorithm based on data and system state
            var selectedAlgorithm = SelectOptimalAlgorithm(
                dataCharacteristics, 
                context.Device, 
                context.State);
            
            Console.WriteLine($"🧠 Selected algorithm: {selectedAlgorithm.Name}");
            
            // Execute with performance monitoring
            _performanceMonitor.StartMeasurement();
            
            var result = await selectedAlgorithm.ProcessAsync(
                inputData, 
                context.Device, 
                cancellationToken);
            
            var performanceMetrics = _performanceMonitor.EndMeasurement();
            
            // Update algorithm performance history
            UpdateAlgorithmPerformance(selectedAlgorithm.Name, performanceMetrics);
            
            stopwatch.Stop();
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                Outputs = new Dictionary<string, object>
                {
                    ["processedData"] = result,
                    ["selectedAlgorithm"] = selectedAlgorithm.Name,
                    ["dataCharacteristics"] = dataCharacteristics
                },
                Metrics = new Dictionary<string, double>
                {
                    ["AlgorithmEfficiency"] = performanceMetrics.Efficiency,
                    ["ComputeUtilization"] = performanceMetrics.ComputeUtilization,
                    ["MemoryEfficiency"] = performanceMetrics.MemoryEfficiency,
                    ["AdaptationConfidence"] = CalculateAdaptationConfidence(dataCharacteristics)
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }
    
    private IProcessingAlgorithm SelectOptimalAlgorithm(
        DataCharacteristics characteristics,
        IAccelerator device,
        Dictionary<string, object> pipelineState)
    {
        // Multi-criteria decision making for algorithm selection
        var scores = new Dictionary<string, double>();
        
        foreach (var (name, algorithm) in _algorithms)
        {
            var score = CalculateAlgorithmScore(algorithm, characteristics, device, pipelineState);
            scores[name] = score;
        }
        
        var bestAlgorithm = scores.OrderByDescending(s => s.Value).First();
        return _algorithms[bestAlgorithm.Key];
    }
    
    private double CalculateAlgorithmScore(
        IProcessingAlgorithm algorithm,
        DataCharacteristics characteristics,
        IAccelerator device,
        Dictionary<string, object> pipelineState)
    {
        var score = 0.0;
        
        // Factor 1: Historical performance
        var historicalPerformance = GetHistoricalPerformance(algorithm.Name);
        score += historicalPerformance * 0.4;
        
        // Factor 2: Data compatibility
        var dataCompatibility = algorithm.GetDataCompatibilityScore(characteristics);
        score += dataCompatibility * 0.3;
        
        // Factor 3: Resource efficiency
        var resourceEfficiency = algorithm.GetResourceEfficiencyScore(device);
        score += resourceEfficiency * 0.2;
        
        // Factor 4: Pipeline context
        var contextScore = CalculateContextScore(algorithm, pipelineState);
        score += contextScore * 0.1;
        
        return score;
    }
}
```

### **Pipeline Optimization and Monitoring**

```csharp
// Advanced pipeline optimizer
public class AdvancedPipelineOptimizer : IPipelineOptimizer
{
    public async Task<PipelineOptimizationResult> OptimizeAsync(
        IKernelPipeline pipeline,
        PipelineOptimizationSettings settings)
    {
        var optimizations = new List<OptimizationStep>();
        var estimatedSpeedup = 1.0;
        var estimatedMemorySavings = 0L;
        
        // Optimization 1: Parallel stage merging
        if (settings.EnableParallelMerging)
        {
            var mergeableStages = FindMergeableParallelStages(pipeline.Stages);
            if (mergeableStages.Any())
            {
                var mergedStage = MergeParallelStages(mergeableStages);
                optimizations.Add(new OptimizationStep
                {
                    Type = OptimizationType.StageMerging,
                    Description = $"Merged {mergeableStages.Count} parallel stages",
                    EstimatedSpeedup = 1.3,
                    EstimatedMemorySavings = CalculateMemorySavings(mergeableStages)
                });
                
                estimatedSpeedup *= 1.3;
            }
        }
        
        // Optimization 2: Kernel fusion
        if (settings.EnableKernelFusion)
        {
            var fusableKernels = FindFusableKernels(pipeline.Stages);
            foreach (var kernelGroup in fusableKernels)
            {
                var fusedKernel = FuseKernels(kernelGroup);
                optimizations.Add(new OptimizationStep
                {
                    Type = OptimizationType.KernelFusion,
                    Description = $"Fused {kernelGroup.Count} kernels",
                    EstimatedSpeedup = 1.15,
                    EstimatedMemorySavings = CalculateKernelFusionSavings(kernelGroup)
                });
                
                estimatedSpeedup *= 1.15;
            }
        }
        
        // Optimization 3: Memory layout optimization
        if (settings.EnableMemoryOptimization)
        {
            var memoryOptimizations = OptimizeMemoryLayout(pipeline.Stages);
            optimizations.AddRange(memoryOptimizations);
            estimatedMemorySavings += memoryOptimizations.Sum(o => o.EstimatedMemorySavings);
        }
        
        // Optimization 4: Load balancing
        var loadBalancingOpts = OptimizeLoadBalancing(pipeline.Stages);
        optimizations.AddRange(loadBalancingOpts);
        
        // Create optimized pipeline
        var optimizedPipeline = await ApplyOptimizations(pipeline, optimizations);
        
        return new PipelineOptimizationResult
        {
            Pipeline = optimizedPipeline,
            AppliedOptimizations = optimizations,
            EstimatedSpeedup = estimatedSpeedup,
            EstimatedMemorySavings = estimatedMemorySavings
        };
    }
    
    private List<IPipelineStage> FindMergeableParallelStages(IReadOnlyList<IPipelineStage> stages)
    {
        return stages
            .Where(s => s.Type == PipelineStageType.Parallel)
            .Where(s => !s.Dependencies.Any()) // Only merge stages with no dependencies
            .Where(s => CanBeMerged(s))
            .ToList();
    }
    
    private List<OptimizationStep> OptimizeMemoryLayout(IReadOnlyList<IPipelineStage> stages)
    {
        var optimizations = new List<OptimizationStep>();
        
        // Analyze memory access patterns
        var memoryAccessAnalysis = AnalyzeMemoryAccessPatterns(stages);
        
        // Optimize for data locality
        if (memoryAccessAnalysis.HasSequentialAccess)
        {
            optimizations.Add(new OptimizationStep
            {
                Type = OptimizationType.MemoryLayout,
                Description = "Optimized for sequential memory access",
                EstimatedSpeedup = 1.2,
                EstimatedMemorySavings = 0
            });
        }
        
        // Optimize buffer reuse
        var reuseOpportunities = FindBufferReuseOpportunities(stages);
        foreach (var opportunity in reuseOpportunities)
        {
            optimizations.Add(new OptimizationStep
            {
                Type = OptimizationType.BufferReuse,
                Description = $"Reused buffer across {opportunity.StageCount} stages",
                EstimatedSpeedup = 1.05,
                EstimatedMemorySavings = opportunity.MemorySaved
            });
        }
        
        return optimizations;
    }
}

// Real-time pipeline monitoring
public class PipelineMonitor
{
    private readonly ConcurrentDictionary<string, PipelineMetrics> _activeMetrics = new();
    private readonly Timer _metricsCollectionTimer;
    private readonly List<Action<PipelineMetricsSnapshot>> _metricsHandlers = new();
    
    public PipelineMonitor()
    {
        _metricsCollectionTimer = new Timer(CollectMetrics, null, 
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }
    
    public void StartMonitoring(IKernelPipeline pipeline)
    {
        var metrics = new PipelineMetrics(pipeline.Id)
        {
            PipelineName = pipeline.Name,
            StartTime = DateTime.UtcNow,
            StageCount = pipeline.Stages.Count
        };
        
        _activeMetrics[pipeline.Id] = metrics;
        
        // Subscribe to pipeline events
        if (pipeline is IObservablePipeline observablePipeline)
        {
            observablePipeline.StageStarted += (stageId, timestamp) =>
            {
                metrics.RecordStageStart(stageId, timestamp);
            };
            
            observablePipeline.StageCompleted += (stageId, timestamp, duration) =>
            {
                metrics.RecordStageCompletion(stageId, timestamp, duration);
            };
            
            observablePipeline.MemoryAllocated += (amount, timestamp) =>
            {
                metrics.RecordMemoryAllocation(amount, timestamp);
            };
        }
    }
    
    private void CollectMetrics(object? state)
    {
        var snapshot = new PipelineMetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            ActivePipelines = _activeMetrics.Count,
            Metrics = _activeMetrics.Values.Select(m => new PipelineMetricsSummary
            {
                PipelineId = m.PipelineId,
                PipelineName = m.PipelineName,
                ExecutionTime = DateTime.UtcNow - m.StartTime,
                CompletedStages = m.CompletedStageCount,
                TotalStages = m.StageCount,
                CurrentMemoryUsage = m.CurrentMemoryUsage,
                PeakMemoryUsage = m.PeakMemoryUsage,
                AverageStageTime = m.AverageStageExecutionTime,
                Throughput = m.CalculateThroughput()
            }).ToList()
        };
        
        // Notify all handlers
        foreach (var handler in _metricsHandlers)
        {
            try
            {
                handler(snapshot);
            }
            catch
            {
                // Ignore handler errors
            }
        }
    }
    
    public void AddMetricsHandler(Action<PipelineMetricsSnapshot> handler)
    {
        _metricsHandlers.Add(handler);
    }
    
    public PipelineMetrics? GetMetrics(string pipelineId)
    {
        return _activeMetrics.TryGetValue(pipelineId, out var metrics) ? metrics : null;
    }
}

// Usage with real-time monitoring
var monitor = new PipelineMonitor();

// Add real-time console output
monitor.AddMetricsHandler(snapshot =>
{
    Console.Clear();
    Console.WriteLine("📊 Pipeline Monitor - Real-time Metrics");
    Console.WriteLine($"Active Pipelines: {snapshot.ActivePipelines}");
    Console.WriteLine();
    
    foreach (var pipeline in snapshot.Metrics)
    {
        var progress = (double)pipeline.CompletedStages / pipeline.TotalStages;
        var progressBar = new string('█', (int)(progress * 20)) + 
                         new string('░', 20 - (int)(progress * 20));
        
        Console.WriteLine($"🔗 {pipeline.PipelineName}");
        Console.WriteLine($"   Progress: [{progressBar}] {progress:P1}");
        Console.WriteLine($"   Runtime: {pipeline.ExecutionTime.TotalSeconds:F1}s");
        Console.WriteLine($"   Memory: {pipeline.CurrentMemoryUsage / 1024.0 / 1024.0:F1} MB");
        Console.WriteLine($"   Throughput: {pipeline.Throughput:F1} ops/sec");
        Console.WriteLine();
    }
});

// Start monitoring the pipeline
monitor.StartMonitoring(pipeline);

// Execute pipeline with monitoring
var result = await pipeline.ExecuteAsync(context);
```

This comprehensive pipeline guide demonstrates the full power of multi-stage orchestration, GPU acceleration, adaptive algorithms, and real-time monitoring in DotCompute Phase 3.