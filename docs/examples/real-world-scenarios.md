# Real-World Scenario Examples

## 🌍 Production-Ready Use Cases

### **1. High-Frequency Trading System**

```csharp
using DotCompute.Core.Pipelines;
using DotCompute.Backends.CUDA;

public class HighFrequencyTradingSystem
{
    private readonly IKernelPipeline _riskCalculationPipeline;
    private readonly IKernelPipeline _orderExecutionPipeline;
    private readonly CudaAccelerator _accelerator;
    
    public HighFrequencyTradingSystem()
    {
        _accelerator = new CudaAccelerator(deviceId: 0);
        
        // Risk calculation pipeline (sub-millisecond execution)
        _riskCalculationPipeline = new KernelPipelineBuilder()
            .SetName("RiskCalculationPipeline")
            
            // Stage 1: Load market data (parallel streams)
            .AddStage("LoadMarketData", new MarketDataStage())
                .SetParallel(true)
                .WithTimeout(TimeSpan.FromMicroseconds(100))
            
            // Stage 2: Calculate portfolio risk metrics
            .AddStage("CalculateVaR", new ValueAtRiskStage())
                .DependsOn("LoadMarketData")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("MonteCarloVaR")
                .WithPriority(Priority.Critical)
            
            // Stage 3: Delta hedging calculations
            .AddStage("DeltaHedging", new DeltaHedgingStage())
                .DependsOn("LoadMarketData")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("GreeksCalculation")
            
            // Stage 4: Risk aggregation
            .AddStage("AggregateRisk", new RiskAggregationStage())
                .DependsOn("CalculateVaR", "DeltaHedging")
                .WithKernel("RiskAggregation")
            
            .WithLatencyTarget(TimeSpan.FromMicroseconds(500)) // Ultra-low latency requirement
            .WithErrorHandling(ErrorHandlingStrategy.FailFast)
            .Build();
        
        // Order execution pipeline
        _orderExecutionPipeline = new KernelPipelineBuilder()
            .SetName("OrderExecutionPipeline")
            
            .AddStage("ValidateOrder", new OrderValidationStage())
            .AddStage("CheckRiskLimits", new RiskLimitStage())
                .DependsOn("ValidateOrder")
            .AddStage("OptimizeExecution", new ExecutionOptimizationStage())
                .DependsOn("CheckRiskLimits")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("VWAPOptimization")
            .AddStage("SendToMarket", new MarketExecutionStage())
                .DependsOn("OptimizeExecution")
            
            .WithLatencyTarget(TimeSpan.FromMicroseconds(50))
            .Build();
    }
    
    public async Task<TradingDecision> ProcessMarketUpdate(MarketDataUpdate update)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Execute risk calculation pipeline
            var riskContext = new PipelineExecutionContext
            {
                Inputs = new Dictionary<string, object>
                {
                    ["marketUpdate"] = update,
                    ["portfolio"] = await GetCurrentPortfolio(),
                    ["riskParameters"] = GetRiskParameters()
                },
                Device = _accelerator,
                Options = new PipelineExecutionOptions
                {
                    MaxLatency = TimeSpan.FromMicroseconds(500),
                    FailFast = true
                }
            };
            
            var riskResult = await _riskCalculationPipeline.ExecuteAsync(riskContext);
            
            if (!riskResult.Success)
            {
                throw new TradingException("Risk calculation failed", riskResult.Errors);
            }
            
            // Make trading decision based on risk metrics
            var decision = MakeTradingDecision(
                (RiskMetrics)riskResult.Outputs["riskMetrics"],
                update);
            
            stopwatch.Stop();
            
            // Log performance (critical for HFT)
            if (stopwatch.Elapsed > TimeSpan.FromMicroseconds(500))
            {
                Logger.LogWarning("Risk calculation exceeded latency target: {Latency}μs", 
                    stopwatch.Elapsed.TotalMicroseconds);
            }
            
            return decision;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Trading system error after {Latency}μs", 
                stopwatch.Elapsed.TotalMicroseconds);
            throw;
        }
    }
    
    public async Task<OrderResult> ExecuteOrder(TradingOrder order)
    {
        var executionContext = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>
            {
                ["order"] = order,
                ["marketConditions"] = await GetMarketConditions(),
                ["liquidityAnalysis"] = await GetLiquidityAnalysis(order.Symbol)
            },
            Device = _accelerator
        };
        
        var result = await _orderExecutionPipeline.ExecuteAsync(executionContext);
        
        return new OrderResult
        {
            Success = result.Success,
            ExecutionPrice = (decimal)result.Outputs["executionPrice"],
            Slippage = (decimal)result.Outputs["slippage"],
            ExecutionTime = result.Metrics.Duration,
            VolumeExecuted = (long)result.Outputs["volumeExecuted"]
        };
    }
}

// Custom stage for Monte Carlo VaR calculation
public class ValueAtRiskStage : IPipelineStage
{
    public string Id => "var-calculation";
    public string Name => "Value at Risk Calculation";
    public PipelineStageType Type => PipelineStageType.Compute;
    
    [Kernel("MonteCarloVaR", 
        Backends = BackendType.CUDA,
        WorkGroupSize = new[] { 256 },
        SharedMemorySize = 4096)]
    public static void MonteCarloVaR(
        KernelContext ctx,
        ReadOnlySpan<float> prices,
        ReadOnlySpan<float> weights,
        ReadOnlySpan<float> correlations,
        Span<float> scenarios,
        Span<float> pnlDistribution,
        int numScenarios,
        int numAssets,
        float confidenceLevel)
    {
        var scenarioId = ctx.GlobalId.X;
        if (scenarioId >= numScenarios) return;
        
        var pnl = 0.0f;
        var seed = (uint)(scenarioId + ctx.GlobalId.Y * 1000);
        
        // Generate correlated random shocks using Cholesky decomposition
        for (int asset = 0; asset < numAssets; asset++)
        {
            var shock = GenerateNormalRandom(seed + (uint)asset);
            
            // Apply correlation structure
            var correlatedShock = 0.0f;
            for (int j = 0; j <= asset; j++)
            {
                correlatedShock += correlations[asset * numAssets + j] * shock;
            }
            
            // Calculate price change and P&L contribution
            var priceChange = prices[asset] * correlatedShock;
            pnl += weights[asset] * priceChange;
        }
        
        pnlDistribution[scenarioId] = pnl;
        
        // Synchronize and calculate VaR (only first thread)
        ctx.Barrier();
        if (scenarioId == 0)
        {
            // Sort P&L distribution and find VaR percentile
            SortPnLDistribution(pnlDistribution, numScenarios);
            var varIndex = (int)(numScenarios * (1.0f - confidenceLevel));
            scenarios[0] = pnlDistribution[varIndex]; // VaR value
        }
    }
    
    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var marketUpdate = (MarketDataUpdate)context.Inputs["marketUpdate"];
            var portfolio = (Portfolio)context.Inputs["portfolio"];
            var riskParams = (RiskParameters)context.Inputs["riskParameters"];
            
            // Prepare GPU computation
            var numAssets = portfolio.Positions.Count;
            var numScenarios = riskParams.MonteCarloScenarios;
            
            var prices = portfolio.Positions.Select(p => (float)p.CurrentPrice).ToArray();
            var weights = portfolio.Positions.Select(p => (float)p.Weight).ToArray();
            var correlations = await GetCorrelationMatrix(portfolio.Positions.Select(p => p.Symbol));
            
            // Allocate GPU memory
            var pricesBuffer = await context.MemoryManager.AllocateAsync<float>(prices.Length);
            var weightsBuffer = await context.MemoryManager.AllocateAsync<float>(weights.Length);
            var correlationsBuffer = await context.MemoryManager.AllocateAsync<float>(correlations.Length);
            var scenariosBuffer = await context.MemoryManager.AllocateAsync<float>(1);
            var pnlBuffer = await context.MemoryManager.AllocateAsync<float>(numScenarios);
            
            // Copy data to GPU
            await pricesBuffer.CopyFromAsync(prices);
            await weightsBuffer.CopyFromAsync(weights);
            await correlationsBuffer.CopyFromAsync(correlations);
            
            // Compile and execute kernel
            var kernel = await CompileKernel(context.Device);
            var executionContext = new KernelExecutionContext
            {
                GlobalSize = new[] { numScenarios },
                LocalSize = new[] { 256 },
                Arguments = new object[]
                {
                    pricesBuffer, weightsBuffer, correlationsBuffer,
                    scenariosBuffer, pnlBuffer,
                    numScenarios, numAssets, riskParams.ConfidenceLevel
                }
            };
            
            await kernel.ExecuteAsync(executionContext, cancellationToken);
            
            // Get results
            var varResult = new float[1];
            await scenariosBuffer.CopyToAsync(varResult);
            
            stopwatch.Stop();
            
            return new StageExecutionResult
            {
                StageId = Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                Outputs = new Dictionary<string, object>
                {
                    ["riskMetrics"] = new RiskMetrics
                    {
                        ValueAtRisk = varResult[0],
                        ConfidenceLevel = riskParams.ConfidenceLevel,
                        TimeHorizon = riskParams.TimeHorizon,
                        CalculationTime = stopwatch.Elapsed
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new StageExecutionResult
            {
                StageId = Id,
                Success = false,
                Error = ex,
                Duration = stopwatch.Elapsed
            };
        }
    }
}
```

### **2. Real-Time Image Recognition for Autonomous Vehicles**

```csharp
public class AutonomousVehicleVisionSystem
{
    private readonly IKernelPipeline _objectDetectionPipeline;
    private readonly IKernelPipeline _pathPlanningPipeline;
    private readonly Dictionary<string, IAccelerator> _accelerators;
    
    public AutonomousVehicleVisionSystem()
    {
        // Use multiple accelerators for different tasks
        _accelerators = new Dictionary<string, IAccelerator>
        {
            ["vision"] = new CudaAccelerator(0), // Dedicated GPU for vision
            ["planning"] = new CudaAccelerator(1), // Dedicated GPU for planning
            ["safety"] = new CudaAccelerator(2)    // Dedicated GPU for safety systems
        };
        
        _objectDetectionPipeline = BuildObjectDetectionPipeline();
        _pathPlanningPipeline = BuildPathPlanningPipeline();
    }
    
    private IKernelPipeline BuildObjectDetectionPipeline()
    {
        return new KernelPipelineBuilder()
            .SetName("ObjectDetectionPipeline")
            .SetLatencyTarget(TimeSpan.FromMilliseconds(33)) // 30 FPS requirement
            
            // Stage 1: Image preprocessing (parallel for multiple cameras)
            .AddStage("PreprocessImages", new ImagePreprocessStage())
                .SetParallel(true)
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernels("Debayer", "Undistort", "Normalize")
                .WithDevice(_accelerators["vision"])
            
            // Stage 2: Feature extraction
            .AddStage("ExtractFeatures", new FeatureExtractionStage())
                .DependsOn("PreprocessImages")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("ConvolutionalLayers")
                .WithDevice(_accelerators["vision"])
            
            // Stage 3: Object detection (YOLO/SSD inference)
            .AddStage("DetectObjects", new ObjectDetectionStage())
                .DependsOn("ExtractFeatures")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("YOLOInference")
                .WithDevice(_accelerators["vision"])
            
            // Stage 4: Track objects across frames
            .AddStage("TrackObjects", new ObjectTrackingStage())
                .DependsOn("DetectObjects")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("KalmanFilter")
            
            // Stage 5: Safety validation (critical path)
            .AddStage("SafetyValidation", new SafetyValidationStage())
                .DependsOn("TrackObjects")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithDevice(_accelerators["safety"])
                .WithPriority(Priority.Critical)
            
            .WithErrorHandling(ErrorHandlingStrategy.SafetyFirst)
            .WithOptimization(opt =>
            {
                opt.EnableKernelFusion = true;
                opt.EnableMemoryOptimization = true;
                opt.PrioritizeSafetyStages = true;
            })
            .Build();
    }
    
    public async Task<VisionResult> ProcessCameraFrames(CameraFrameSet frameSet)
    {
        var processingStartTime = DateTime.UtcNow;
        
        try
        {
            var context = new PipelineExecutionContext
            {
                Inputs = new Dictionary<string, object>
                {
                    ["frontCamera"] = frameSet.FrontCamera,
                    ["leftCamera"] = frameSet.LeftCamera,
                    ["rightCamera"] = frameSet.RightCamera,
                    ["rearCamera"] = frameSet.RearCamera,
                    ["timestamp"] = frameSet.Timestamp,
                    ["vehicleState"] = await GetVehicleState(),
                    ["mapData"] = await GetLocalMapData()
                },
                Device = _accelerators["vision"],
                Options = new PipelineExecutionOptions
                {
                    MaxLatency = TimeSpan.FromMilliseconds(33),
                    FailFast = true,
                    PrioritizeSafety = true
                }
            };
            
            var result = await _objectDetectionPipeline.ExecuteAsync(context);
            
            if (!result.Success)
            {
                // Fallback to safety mode
                await ActivateSafetyMode(frameSet);
                throw new VisionSystemException("Object detection failed", result.Errors);
            }
            
            var detectedObjects = (List<DetectedObject>)result.Outputs["detectedObjects"];
            var trackedObjects = (List<TrackedObject>)result.Outputs["trackedObjects"];
            var safetyAssessment = (SafetyAssessment)result.Outputs["safetyAssessment"];
            
            // Trigger path planning if objects detected
            var pathPlanningResult = await PlanPath(trackedObjects, safetyAssessment);
            
            var processingTime = DateTime.UtcNow - processingStartTime;
            
            return new VisionResult
            {
                DetectedObjects = detectedObjects,
                TrackedObjects = trackedObjects,
                PathPlan = pathPlanningResult.OptimalPath,
                SafetyAssessment = safetyAssessment,
                ProcessingTime = processingTime,
                FrameRate = 1000.0 / processingTime.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Vision system failure - activating emergency protocols");
            await ActivateEmergencyMode();
            throw;
        }
    }
}

// Custom object detection stage with YOLO inference
public class ObjectDetectionStage : IPipelineStage
{
    private ICompiledKernel? _yoloKernel;
    private readonly YOLOModel _model;
    
    [Kernel("YOLOInference",
        Backends = BackendType.CUDA,
        WorkGroupSize = new[] { 16, 16 },
        SharedMemorySize = 8192)]
    public static void YOLOInference(
        KernelContext ctx,
        ReadOnlySpan<float> inputImage,
        ReadOnlySpan<float> modelWeights,
        Span<float> predictions,
        Span<float> confidences,
        Span<int> classIds,
        int imageWidth,
        int imageHeight,
        int numClasses,
        float confidenceThreshold)
    {
        var x = ctx.GlobalId.X;
        var y = ctx.GlobalId.Y;
        
        if (x >= imageWidth || y >= imageHeight) return;
        
        // YOLO grid cell calculations
        var gridX = x / (imageWidth / 13); // Assuming 13x13 grid
        var gridY = y / (imageHeight / 13);
        var gridIndex = gridY * 13 + gridX;
        
        // Process anchor boxes
        for (int anchor = 0; anchor < 5; anchor++) // 5 anchor boxes per cell
        {
            var baseIndex = (gridIndex * 5 + anchor) * (5 + numClasses);
            
            // Extract bounding box parameters
            var centerX = Sigmoid(predictions[baseIndex + 0]);
            var centerY = Sigmoid(predictions[baseIndex + 1]);
            var width = Math.Exp(predictions[baseIndex + 2]);
            var height = Math.Exp(predictions[baseIndex + 3]);
            var objectness = Sigmoid(predictions[baseIndex + 4]);
            
            // Extract class probabilities
            var maxClassProb = 0.0f;
            var maxClassId = 0;
            
            for (int cls = 0; cls < numClasses; cls++)
            {
                var classProb = Sigmoid(predictions[baseIndex + 5 + cls]);
                if (classProb > maxClassProb)
                {
                    maxClassProb = classProb;
                    maxClassId = cls;
                }
            }
            
            var finalConfidence = objectness * maxClassProb;
            
            if (finalConfidence > confidenceThreshold)
            {
                var detectionIndex = ctx.GlobalId.X * ctx.GlobalSize.Y + ctx.GlobalId.Y;
                confidences[detectionIndex] = finalConfidence;
                classIds[detectionIndex] = maxClassId;
                
                // Store bounding box coordinates
                predictions[detectionIndex * 4 + 0] = (gridX + centerX) / 13.0f * imageWidth;
                predictions[detectionIndex * 4 + 1] = (gridY + centerY) / 13.0f * imageHeight;
                predictions[detectionIndex * 4 + 2] = width;
                predictions[detectionIndex * 4 + 3] = height;
            }
        }
    }
    
    private static float Sigmoid(float x)
    {
        return 1.0f / (1.0f + Math.Exp(-x));
    }
}
```

### **3. Large-Scale Scientific Simulation**

```csharp
public class ClimateSimulationSystem
{
    private readonly IKernelPipeline _atmosphericModelPipeline;
    private readonly IKernelPipeline _oceanModelPipeline;
    private readonly IKernelPipeline _couplingPipeline;
    private readonly List<IAccelerator> _computeCluster;
    
    public ClimateSimulationSystem(ClusterConfiguration config)
    {
        // Initialize compute cluster with multiple GPUs
        _computeCluster = InitializeComputeCluster(config);
        
        _atmosphericModelPipeline = BuildAtmosphericModelPipeline();
        _oceanModelPipeline = BuildOceanModelPipeline();
        _couplingPipeline = BuildCouplingPipeline();
    }
    
    private IKernelPipeline BuildAtmosphericModelPipeline()
    {
        return new KernelPipelineBuilder()
            .SetName("AtmosphericModelPipeline")
            .SetId("atm-model-001")
            
            // Stage 1: Initialize atmospheric grid
            .AddStage("InitializeGrid", new GridInitializationStage())
                .SetParallel(true)
                .WithKernel("InitializeAtmosphericGrid")
            
            // Stage 2: Solve primitive equations (parallel across GPUs)
            .AddStage("SolvePrimitiveEquations", new PrimitiveEquationStage())
                .DependsOn("InitializeGrid")
                .SetParallel(true)
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernels("NavierStokes", "ContinuityEquation", "ThermodynamicEquation")
                .WithMultiGPU(_computeCluster.Take(4).ToArray()) // Use 4 GPUs
            
            // Stage 3: Radiation calculations
            .AddStage("RadiationTransfer", new RadiationTransferStage())
                .DependsOn("SolvePrimitiveEquations")
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("RadiativeTransfer")
            
            // Stage 4: Cloud microphysics
            .AddStage("CloudMicrophysics", new CloudMicrophysicsStage())
                .DependsOn("RadiationTransfer")
                .SetParallel(true)
                .WithKernel("CloudCondensation")
            
            // Stage 5: Boundary layer processes
            .AddStage("BoundaryLayer", new BoundaryLayerStage())
                .DependsOn("CloudMicrophysics")
                .WithKernel("TurbulentMixing")
            
            .WithOptimization(opt =>
            {
                opt.EnableDomainDecomposition = true;
                opt.EnableLoadBalancing = true;
                opt.EnableMemoryOptimization = true;
            })
            .Build();
    }
    
    public async Task<SimulationResult> RunSimulation(SimulationParameters parameters)
    {
        var simulationStart = DateTime.UtcNow;
        var results = new List<TimeStepResult>();
        
        try
        {
            // Initialize simulation state
            var atmosphericState = await InitializeAtmosphericState(parameters);
            var oceanState = await InitializeOceanState(parameters);
            
            for (int timeStep = 0; timeStep < parameters.TotalTimeSteps; timeStep++)
            {
                var stepStart = DateTime.UtcNow;
                
                // Run atmospheric model
                var atmContext = new PipelineExecutionContext
                {
                    Inputs = new Dictionary<string, object>
                    {
                        ["currentState"] = atmosphericState,
                        ["timeStep"] = timeStep,
                        ["deltaTime"] = parameters.TimeStepSize,
                        ["forcingData"] = await GetForcingData(timeStep)
                    },
                    Device = _computeCluster[0], // Lead GPU
                    Options = new PipelineExecutionOptions
                    {
                        EnableMultiGPU = true,
                        LoadBalancing = LoadBalancingStrategy.DynamicWorkStealing
                    }
                };
                
                var atmResult = await _atmosphericModelPipeline.ExecuteAsync(atmContext);
                
                // Run ocean model (can run in parallel with atmosphere)
                var oceanTask = RunOceanModel(oceanState, timeStep, parameters);
                
                // Wait for both models to complete
                var oceanResult = await oceanTask;
                
                if (!atmResult.Success || !oceanResult.Success)
                {
                    throw new SimulationException($"Model failure at time step {timeStep}");
                }
                
                // Couple atmospheric and ocean models
                var couplingResult = await CoupleModels(
                    (AtmosphericState)atmResult.Outputs["newState"],
                    (OceanState)oceanResult.Outputs["newState"]);
                
                atmosphericState = couplingResult.AtmosphericState;
                oceanState = couplingResult.OceanState;
                
                var stepTime = DateTime.UtcNow - stepStart;
                
                results.Add(new TimeStepResult
                {
                    TimeStep = timeStep,
                    AtmosphericState = atmosphericState,
                    OceanState = oceanState,
                    ComputationTime = stepTime,
                    MemoryUsage = CalculateMemoryUsage()
                });
                
                // Output progress
                if (timeStep % 100 == 0)
                {
                    var progress = (double)timeStep / parameters.TotalTimeSteps;
                    var eta = TimeSpan.FromTicks((long)((DateTime.UtcNow - simulationStart).Ticks / progress * (1 - progress)));
                    
                    Console.WriteLine($"🌍 Climate simulation progress: {progress:P1} (ETA: {eta:hh\\:mm\\:ss})");
                    Console.WriteLine($"   Step time: {stepTime.TotalSeconds:F2}s");
                    Console.WriteLine($"   Memory usage: {CalculateMemoryUsage() / 1024.0 / 1024.0 / 1024.0:F2} GB");
                }
                
                // Checkpoint every 1000 steps
                if (timeStep % 1000 == 0)
                {
                    await SaveCheckpoint(timeStep, atmosphericState, oceanState);
                }
            }
            
            var totalSimulationTime = DateTime.UtcNow - simulationStart;
            
            return new SimulationResult
            {
                Success = true,
                TotalComputationTime = totalSimulationTime,
                TimeStepResults = results,
                FinalAtmosphericState = atmosphericState,
                FinalOceanState = oceanState,
                PerformanceMetrics = CalculatePerformanceMetrics(results)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Climate simulation failed");
            throw;
        }
    }
}

// Primitive equation solver for atmospheric dynamics
public class PrimitiveEquationStage : IPipelineStage
{
    [Kernel("NavierStokes",
        Backends = BackendType.CUDA,
        WorkGroupSize = new[] { 16, 16, 4 }, // 3D grid
        SharedMemorySize = 16384)]
    public static void SolveNavierStokes(
        KernelContext ctx,
        ReadOnlySpan<float> u, // x-velocity
        ReadOnlySpan<float> v, // y-velocity
        ReadOnlySpan<float> w, // z-velocity
        ReadOnlySpan<float> pressure,
        ReadOnlySpan<float> temperature,
        ReadOnlySpan<float> density,
        Span<float> uNew,
        Span<float> vNew,
        Span<float> wNew,
        int nx, int ny, int nz,
        float dx, float dy, float dz,
        float dt,
        float viscosity,
        float coriolisParameter)
    {
        var i = ctx.GlobalId.X;
        var j = ctx.GlobalId.Y;
        var k = ctx.GlobalId.Z;
        
        if (i >= nx - 1 || j >= ny - 1 || k >= nz - 1 || 
            i == 0 || j == 0 || k == 0) return;
        
        var idx = k * nx * ny + j * nx + i;
        
        // Calculate spatial derivatives using finite differences
        var dudx = (u[idx + 1] - u[idx - 1]) / (2.0f * dx);
        var dudy = (u[idx + nx] - u[idx - nx]) / (2.0f * dy);
        var dudz = (u[idx + nx * ny] - u[idx - nx * ny]) / (2.0f * dz);
        
        var dvdx = (v[idx + 1] - v[idx - 1]) / (2.0f * dx);
        var dvdy = (v[idx + nx] - v[idx - nx]) / (2.0f * dy);
        var dvdz = (v[idx + nx * ny] - v[idx - nx * ny]) / (2.0f * dz);
        
        var dwdx = (w[idx + 1] - w[idx - 1]) / (2.0f * dx);
        var dwdy = (w[idx + nx] - w[idx - nx]) / (2.0f * dy);
        var dwdz = (w[idx + nx * ny] - w[idx - nx * ny]) / (2.0f * dz);
        
        // Pressure gradients
        var dpdx = (pressure[idx + 1] - pressure[idx - 1]) / (2.0f * dx);
        var dpdy = (pressure[idx + nx] - pressure[idx - nx]) / (2.0f * dy);
        var dpdz = (pressure[idx + nx * ny] - pressure[idx - nx * ny]) / (2.0f * dz);
        
        // Second derivatives for viscous terms
        var d2udx2 = (u[idx + 1] - 2.0f * u[idx] + u[idx - 1]) / (dx * dx);
        var d2udy2 = (u[idx + nx] - 2.0f * u[idx] + u[idx - nx]) / (dy * dy);
        var d2udz2 = (u[idx + nx * ny] - 2.0f * u[idx] + u[idx - nx * ny]) / (dz * dz);
        
        var d2vdx2 = (v[idx + 1] - 2.0f * v[idx] + v[idx - 1]) / (dx * dx);
        var d2vdy2 = (v[idx + nx] - 2.0f * v[idx] + v[idx - nx]) / (dy * dy);
        var d2vdz2 = (v[idx + nx * ny] - 2.0f * v[idx] + v[idx - nx * ny]) / (dz * dz);
        
        var d2wdx2 = (w[idx + 1] - 2.0f * w[idx] + w[idx - 1]) / (dx * dx);
        var d2wdy2 = (w[idx + nx] - 2.0f * w[idx] + w[idx - nx]) / (dy * dy);
        var d2wdz2 = (w[idx + nx * ny] - 2.0f * w[idx] + w[idx - nx * ny]) / (dz * dz);
        
        // Navier-Stokes equations
        var dudt = -u[idx] * dudx - v[idx] * dudy - w[idx] * dudz
                   - dpdx / density[idx]
                   + viscosity * (d2udx2 + d2udy2 + d2udz2)
                   + coriolisParameter * v[idx]; // Coriolis force
        
        var dvdt = -u[idx] * dvdx - v[idx] * dvdy - w[idx] * dvdz
                   - dpdy / density[idx]
                   + viscosity * (d2vdx2 + d2vdy2 + d2vdz2)
                   - coriolisParameter * u[idx]; // Coriolis force
        
        var dwdt = -u[idx] * dwdx - v[idx] * dwdy - w[idx] * dwdz
                   - dpdz / density[idx]
                   + viscosity * (d2wdx2 + d2wdy2 + d2wdz2)
                   - 9.81f; // Gravity
        
        // Update velocities using forward Euler (could use RK4 for better accuracy)
        uNew[idx] = u[idx] + dt * dudt;
        vNew[idx] = v[idx] + dt * dvdt;
        wNew[idx] = w[idx] + dt * dwdt;
    }
}
```

### **4. Cryptocurrency Mining Pool**

```csharp
public class CryptocurrencyMiningPool
{
    private readonly IKernelPipeline _hashingPipeline;
    private readonly List<IAccelerator> _minerGPUs;
    private readonly ConcurrentQueue<MiningJob> _jobQueue;
    private readonly ConcurrentDictionary<string, MinerStats> _minerStats;
    
    public CryptocurrencyMiningPool(MiningPoolConfig config)
    {
        _minerGPUs = InitializeMinerGPUs(config.GPUCount);
        _jobQueue = new ConcurrentQueue<MiningJob>();
        _minerStats = new ConcurrentDictionary<string, MinerStats>();
        
        _hashingPipeline = new KernelPipelineBuilder()
            .SetName("CryptocurrencyHashingPipeline")
            
            // Stage 1: Job distribution
            .AddStage("DistributeJobs", new JobDistributionStage())
                .SetParallel(true)
            
            // Stage 2: SHA-256 hashing (massively parallel)
            .AddStage("SHA256Hashing", new SHA256HashingStage())
                .DependsOn("DistributeJobs")
                .SetParallel(true)
                .SetAcceleratorType(AcceleratorType.CUDA)
                .WithKernel("SHA256Mining")
                .WithMultiGPU(_minerGPUs.ToArray())
            
            // Stage 3: Difficulty validation
            .AddStage("ValidateDifficulty", new DifficultyValidationStage())
                .DependsOn("SHA256Hashing")
                .SetParallel(true)
            
            // Stage 4: Share submission
            .AddStage("SubmitShares", new ShareSubmissionStage())
                .DependsOn("ValidateDifficulty")
            
            .WithOptimization(opt =>
            {
                opt.EnableKernelFusion = true;
                opt.MaximizeGPUUtilization = true;
                opt.EnableWorkStealing = true;
            })
            .Build();
    }
    
    public async Task StartMining(MiningParameters parameters)
    {
        var miningTasks = new List<Task>();
        
        // Start mining on each GPU
        foreach (var gpu in _minerGPUs)
        {
            var task = Task.Run(() => RunMinerOnGPU(gpu, parameters));
            miningTasks.Add(task);
        }
        
        // Start job distribution
        var jobDistributionTask = Task.Run(() => DistributeJobs(parameters));
        miningTasks.Add(jobDistributionTask);
        
        // Start monitoring
        var monitoringTask = Task.Run(() => MonitorMiningProgress());
        miningTasks.Add(monitoringTask);
        
        // Wait for all tasks (runs indefinitely until cancelled)
        await Task.WhenAll(miningTasks);
    }
    
    private async Task RunMinerOnGPU(IAccelerator gpu, MiningParameters parameters)
    {
        while (!parameters.CancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_jobQueue.TryDequeue(out var job))
                {
                    var result = await ProcessMiningJob(job, gpu);
                    
                    if (result.ValidShares.Any())
                    {
                        await SubmitShares(result.ValidShares);
                        UpdateMinerStats(gpu.Info.Id, result);
                    }
                }
                else
                {
                    // No jobs available, wait briefly
                    await Task.Delay(1, parameters.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Mining error on GPU {GPUId}", gpu.Info.Id);
                await Task.Delay(5000, parameters.CancellationToken); // Back off on error
            }
        }
    }
    
    private async Task<MiningResult> ProcessMiningJob(MiningJob job, IAccelerator gpu)
    {
        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>
            {
                ["blockHeader"] = job.BlockHeader,
                ["difficulty"] = job.Difficulty,
                ["nonceRange"] = job.NonceRange,
                ["target"] = job.Target
            },
            Device = gpu,
            Options = new PipelineExecutionOptions
            {
                MaxLatency = TimeSpan.FromSeconds(30), // Job timeout
                EnableGPUOptimizations = true
            }
        };
        
        var result = await _hashingPipeline.ExecuteAsync(context);
        
        return new MiningResult
        {
            JobId = job.Id,
            ValidShares = (List<Share>)result.Outputs["validShares"],
            HashRate = (double)result.Outputs["hashRate"],
            ProcessingTime = result.Metrics.Duration
        };
    }
}

// SHA-256 mining kernel optimized for cryptocurrency
public class SHA256HashingStage : IPipelineStage
{
    [Kernel("SHA256Mining",
        Backends = BackendType.CUDA,
        WorkGroupSize = new[] { 256 }, // Optimize for GPU warps
        SharedMemorySize = 1024)]
    public static void SHA256Mining(
        KernelContext ctx,
        ReadOnlySpan<byte> blockHeader,
        ReadOnlySpan<byte> target,
        Span<uint> validNonces,
        Span<int> shareCount,
        uint startNonce,
        uint endNonce,
        uint difficulty)
    {
        var threadId = ctx.GlobalId.X;
        var nonce = startNonce + (uint)threadId;
        
        if (nonce >= endNonce) return;
        
        // Prepare message block for SHA-256
        var message = new uint[16];
        
        // Copy block header (80 bytes) into message
        for (int i = 0; i < 20; i++) // 80 bytes = 20 uint32s
        {
            message[i] = BitConverter.ToUInt32(blockHeader.Slice(i * 4, 4));
        }
        
        // Set nonce in the appropriate position
        message[19] = nonce; // Nonce is typically at the end of the header
        
        // First SHA-256 hash
        var hash1 = SHA256Hash(message);
        
        // Second SHA-256 hash (Bitcoin uses double SHA-256)
        var hash2 = SHA256Hash(hash1);
        
        // Check if hash meets difficulty target
        if (MeetsDifficulty(hash2, target, difficulty))
        {
            // Atomically increment share count and store nonce
            var shareIndex = AtomicIncrement(shareCount);
            if (shareIndex < validNonces.Length)
            {
                validNonces[shareIndex] = nonce;
            }
        }
    }
    
    private static uint[] SHA256Hash(uint[] message)
    {
        // SHA-256 constants
        var k = new uint[]
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
            0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            // ... (all 64 constants)
        };
        
        // Initialize hash values
        var h = new uint[]
        {
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
            0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
        };
        
        // Extend message to 64 words
        var w = new uint[64];
        for (int i = 0; i < 16; i++)
        {
            w[i] = message[i];
        }
        
        for (int i = 16; i < 64; i++)
        {
            var s0 = RightRotate(w[i - 15], 7) ^ RightRotate(w[i - 15], 18) ^ (w[i - 15] >> 3);
            var s1 = RightRotate(w[i - 2], 17) ^ RightRotate(w[i - 2], 19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }
        
        // Main SHA-256 compression loop
        var a = h[0]; var b = h[1]; var c = h[2]; var d = h[3];
        var e = h[4]; var f = h[5]; var g = h[6]; var h7 = h[7];
        
        for (int i = 0; i < 64; i++)
        {
            var S1 = RightRotate(e, 6) ^ RightRotate(e, 11) ^ RightRotate(e, 25);
            var ch = (e & f) ^ (~e & g);
            var temp1 = h7 + S1 + ch + k[i] + w[i];
            var S0 = RightRotate(a, 2) ^ RightRotate(a, 13) ^ RightRotate(a, 22);
            var maj = (a & b) ^ (a & c) ^ (b & c);
            var temp2 = S0 + maj;
            
            h7 = g; g = f; f = e; e = d + temp1;
            d = c; c = b; b = a; a = temp1 + temp2;
        }
        
        // Add this chunk's hash to result
        return new uint[]
        {
            h[0] + a, h[1] + b, h[2] + c, h[3] + d,
            h[4] + e, h[5] + f, h[6] + g, h[7] + h7
        };
    }
    
    private static uint RightRotate(uint value, int amount)
    {
        return (value >> amount) | (value << (32 - amount));
    }
    
    private static bool MeetsDifficulty(uint[] hash, ReadOnlySpan<byte> target, uint difficulty)
    {
        // Check if hash is less than target (meets difficulty)
        for (int i = 0; i < 8; i++)
        {
            var hashBytes = BitConverter.GetBytes(hash[i]);
            var targetBytes = target.Slice(i * 4, 4);
            
            for (int j = 0; j < 4; j++)
            {
                if (hashBytes[j] < targetBytes[j]) return true;
                if (hashBytes[j] > targetBytes[j]) return false;
            }
        }
        
        return false; // Equal to target, which also counts as valid
    }
    
    private static int AtomicIncrement(Span<int> counter)
    {
        // Platform-specific atomic increment
        return Interlocked.Increment(ref counter[0]) - 1;
    }
}
```

These real-world scenarios demonstrate the full power of DotCompute Phase 3 in production environments, from ultra-low latency financial systems to autonomous vehicles, climate simulations, and cryptocurrency mining. Each example showcases different aspects of the framework's capabilities: multi-GPU coordination, real-time processing, scientific computing, and massively parallel workloads.