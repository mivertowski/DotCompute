// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Core.Pipelines;
using DotCompute.Tests.Common.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration.Infrastructure;

/// <summary>
/// Base class for compute workflow integration tests with comprehensive testing infrastructure.
/// Provides hardware simulation, performance monitoring, and resource tracking capabilities.
/// </summary>
public abstract class ComputeWorkflowTestBase : IntegrationTestBase
{
    protected readonly HardwareSimulator HardwareSimulator;
    protected readonly ConcurrentDictionary<string, PerformanceMetrics> PerformanceResults = new();
    protected readonly TestResourceTracker ResourceTracker = new();

    protected ComputeWorkflowTestBase(ITestOutputHelper output) : base(output)
    {
        HardwareSimulator = new HardwareSimulator(ServiceProvider?.GetService<ILogger<HardwareSimulator>>());
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Start hardware simulation with mixed configuration for comprehensive testing
        HardwareSimulator.Start(HardwareConfiguration.Mixed, TimeSpan.FromMilliseconds(500));
        
        Logger.LogInformation("ComputeWorkflowTestBase initialized with hardware simulation");
    }

    public override async Task DisposeAsync()
    {
        HardwareSimulator?.Stop();
        HardwareSimulator?.Dispose();
        ResourceTracker?.Dispose();
        
        await base.DisposeAsync();
    }

    /// <summary>
    /// Executes a complete compute workflow with comprehensive monitoring and validation.
    /// </summary>
    protected async Task<WorkflowExecutionResult> ExecuteComputeWorkflowAsync(
        string workflowName,
        ComputeWorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var executionId = Guid.NewGuid().ToString();
        
        Logger.LogInformation("Starting compute workflow '{WorkflowName}' (ID: {ExecutionId})", 
            workflowName, executionId);

        try
        {
            // Track resource usage
            var initialStats = await GetMemoryStatisticsAsync();
            ResourceTracker.BeginTracking(executionId);

            // 1. Compile kernels
            var compilationResults = await CompileWorkflowKernelsAsync(workflow, cancellationToken);
            
            // 2. Allocate and prepare memory
            var memoryContext = await AllocateWorkflowMemoryAsync(workflow, cancellationToken);
            
            // 3. Execute workflow stages
            var executionResults = await ExecuteWorkflowStagesAsync(
                workflow, compilationResults, memoryContext, cancellationToken);
            
            // 4. Collect results
            var results = await CollectWorkflowResultsAsync(workflow, memoryContext, cancellationToken);
            
            // 5. Validate results
            var validation = ValidateWorkflowResults(workflow, results);

            stopwatch.Stop();
            var finalStats = await GetMemoryStatisticsAsync();
            ResourceTracker.EndTracking(executionId);

            var performanceMetrics = new PerformanceMetrics
            {
                WorkflowName = workflowName,
                ExecutionId = executionId,
                TotalDuration = stopwatch.Elapsed,
                CompilationTime = compilationResults.Values.Sum(r => r.CompilationTime.TotalMilliseconds),
                ExecutionTime = executionResults.Values.Sum(r => r.ExecutionTime.TotalMilliseconds),
                MemoryUsageDelta = finalStats.UsedMemory - initialStats.UsedMemory,
                ThroughputMBps = CalculateThroughput(workflow, stopwatch.Elapsed),
                ResourceUtilization = ResourceTracker.GetUtilization(executionId)
            };

            PerformanceResults[executionId] = performanceMetrics;

            Logger.LogInformation("Completed compute workflow '{WorkflowName}' in {Duration}ms", 
                workflowName, stopwatch.ElapsedMilliseconds);

            return new WorkflowExecutionResult
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                Success = true,
                Duration = stopwatch.Elapsed,
                CompilationResults = compilationResults,
                ExecutionResults = executionResults,
                Results = results,
                Validation = validation,
                Metrics = performanceMetrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ResourceTracker.EndTracking(executionId);
            
            Logger.LogError(ex, "Compute workflow '{WorkflowName}' failed after {Duration}ms", 
                workflowName, stopwatch.ElapsedMilliseconds);

            return new WorkflowExecutionResult
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex,
                CompilationResults = new Dictionary<string, KernelCompilationResult>(),
                ExecutionResults = new Dictionary<string, StageExecutionResult>(),
                Results = new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// Compiles all kernels required by the workflow.
    /// </summary>
    private async Task<Dictionary<string, KernelCompilationResult>> CompileWorkflowKernelsAsync(
        ComputeWorkflowDefinition workflow,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, KernelCompilationResult>();
        var engine = ServiceProvider.GetRequiredService<IComputeEngine>();

        foreach (var kernel in workflow.Kernels)
        {
            var compilationStopwatch = Stopwatch.StartNew();
            
            try
            {
                var compiledKernel = await engine.CompileKernelAsync(
                    kernel.SourceCode,
                    kernel.Name,
                    kernel.CompilationOptions,
                    cancellationToken);

                compilationStopwatch.Stop();

                results[kernel.Name] = new KernelCompilationResult
                {
                    Success = true,
                    KernelName = kernel.Name,
                    CompiledKernel = compiledKernel,
                    CompilationTime = compilationStopwatch.Elapsed
                };

                Logger.LogDebug("Successfully compiled kernel '{KernelName}' in {Duration}ms",
                    kernel.Name, compilationStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                compilationStopwatch.Stop();
                
                results[kernel.Name] = new KernelCompilationResult
                {
                    Success = false,
                    KernelName = kernel.Name,
                    CompilationTime = compilationStopwatch.Elapsed,
                    Error = ex
                };

                Logger.LogWarning(ex, "Failed to compile kernel '{KernelName}' after {Duration}ms",
                    kernel.Name, compilationStopwatch.ElapsedMilliseconds);
            }
        }

        return results;
    }

    /// <summary>
    /// Allocates memory resources required by the workflow.
    /// </summary>
    private async Task<WorkflowMemoryContext> AllocateWorkflowMemoryAsync(
        ComputeWorkflowDefinition workflow,
        CancellationToken cancellationToken)
    {
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var context = new WorkflowMemoryContext();

        // Allocate input buffers
        foreach (var input in workflow.Inputs)
        {
            var buffer = await CreateInputBuffer<float>(memoryManager, input.Data);
            context.InputBuffers[input.Name] = buffer;
        }

        // Allocate output buffers
        foreach (var output in workflow.Outputs)
        {
            var buffer = await CreateOutputBuffer<float>(memoryManager, output.Size);
            context.OutputBuffers[output.Name] = buffer;
        }

        // Allocate intermediate buffers
        foreach (var intermediate in workflow.IntermediateBuffers)
        {
            var buffer = await memoryManager.AllocateAsync(intermediate.SizeInBytes, 
                intermediate.Options, cancellationToken);
            context.IntermediateBuffers[intermediate.Name] = buffer;
        }

        return context;
    }

    /// <summary>
    /// Executes all stages of the workflow.
    /// </summary>
    private async Task<Dictionary<string, StageExecutionResult>> ExecuteWorkflowStagesAsync(
        ComputeWorkflowDefinition workflow,
        Dictionary<string, KernelCompilationResult> compilationResults,
        WorkflowMemoryContext memoryContext,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, StageExecutionResult>();
        var engine = ServiceProvider.GetRequiredService<IComputeEngine>();

        foreach (var stage in workflow.ExecutionStages.OrderBy(s => s.Order))
        {
            var stageStopwatch = Stopwatch.StartNew();
            
            try
            {
                if (!compilationResults.TryGetValue(stage.KernelName, out var compilationResult) ||
                    !compilationResult.Success)
                {
                    throw new InvalidOperationException($"Kernel '{stage.KernelName}' was not successfully compiled");
                }

                // Prepare arguments for execution
                var arguments = PrepareStageArguments(stage, memoryContext);

                // Execute the kernel
                await engine.ExecuteAsync(
                    compilationResult.CompiledKernel!,
                    arguments,
                    stage.BackendType,
                    stage.ExecutionOptions,
                    cancellationToken);

                stageStopwatch.Stop();

                results[stage.Name] = new StageExecutionResult
                {
                    StageId = stage.Name,
                    Success = true,
                    Duration = stageStopwatch.Elapsed,
                    ExecutionTime = stageStopwatch.Elapsed,
                    Outputs = new Dictionary<string, object>()
                };

                Logger.LogDebug("Successfully executed stage '{StageName}' in {Duration}ms",
                    stage.Name, stageStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stageStopwatch.Stop();
                
                results[stage.Name] = new StageExecutionResult
                {
                    StageId = stage.Name,
                    Success = false,
                    Duration = stageStopwatch.Elapsed,
                    ExecutionTime = stageStopwatch.Elapsed,
                    Error = ex,
                    Outputs = new Dictionary<string, object>()
                };

                Logger.LogWarning(ex, "Failed to execute stage '{StageName}' after {Duration}ms",
                    stage.Name, stageStopwatch.ElapsedMilliseconds);

                if (!workflow.ContinueOnError)
                    throw;
            }
        }

        return results;
    }

    /// <summary>
    /// Prepares arguments for stage execution.
    /// </summary>
    private object[] PrepareStageArguments(WorkflowExecutionStage stage, WorkflowMemoryContext memoryContext)
    {
        var arguments = new List<object>();

        foreach (var argName in stage.ArgumentNames)
        {
            if (memoryContext.InputBuffers.TryGetValue(argName, out var inputBuffer))
            {
                arguments.Add(inputBuffer);
            }
            else if (memoryContext.OutputBuffers.TryGetValue(argName, out var outputBuffer))
            {
                arguments.Add(outputBuffer);
            }
            else if (memoryContext.IntermediateBuffers.TryGetValue(argName, out var intermediateBuffer))
            {
                arguments.Add(intermediateBuffer);
            }
            else
            {
                // Assume it's a scalar value stored in stage parameters
                if (stage.Parameters.TryGetValue(argName, out var parameter))
                {
                    arguments.Add(parameter);
                }
                else
                {
                    throw new ArgumentException($"Argument '{argName}' not found in memory context or parameters");
                }
            }
        }

        return arguments.ToArray();
    }

    /// <summary>
    /// Collects results from output buffers.
    /// </summary>
    private async Task<Dictionary<string, object>> CollectWorkflowResultsAsync(
        ComputeWorkflowDefinition workflow,
        WorkflowMemoryContext memoryContext,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, object>();

        foreach (var output in workflow.Outputs)
        {
            if (memoryContext.OutputBuffers.TryGetValue(output.Name, out var buffer))
            {
                var data = await ReadBufferAsync<float>(buffer);
                results[output.Name] = data;
            }
        }

        return results;
    }

    /// <summary>
    /// Validates workflow execution results.
    /// </summary>
    protected virtual ValidationResult ValidateWorkflowResults(
        ComputeWorkflowDefinition workflow, 
        Dictionary<string, object> results)
    {
        var validation = new ValidationResult { IsValid = true };

        foreach (var expectedOutput in workflow.Outputs)
        {
            if (!results.ContainsKey(expectedOutput.Name))
            {
                validation.IsValid = false;
                validation.Issues.Add($"Missing expected output: {expectedOutput.Name}");
                continue;
            }

            if (results[expectedOutput.Name] is float[] data)
            {
                if (data.Length != expectedOutput.Size)
                {
                    validation.IsValid = false;
                    validation.Issues.Add($"Output '{expectedOutput.Name}' size mismatch: expected {expectedOutput.Size}, got {data.Length}");
                }

                if (expectedOutput.Validator != null && !expectedOutput.Validator(data))
                {
                    validation.IsValid = false;
                    validation.Issues.Add($"Output '{expectedOutput.Name}' failed validation");
                }
            }
        }

        return validation;
    }

    /// <summary>
    /// Calculates throughput in MB/s for the workflow.
    /// </summary>
    private double CalculateThroughput(ComputeWorkflowDefinition workflow, TimeSpan duration)
    {
        var totalDataMB = workflow.Inputs.Sum(i => i.Data.Length * sizeof(float)) +
                         workflow.Outputs.Sum(o => o.Size * sizeof(float));
        totalDataMB /= (1024 * 1024); // Convert to MB

        return totalDataMB / duration.TotalSeconds;
    }

    /// <summary>
    /// Creates test data generators for different data types and sizes.
    /// </summary>
    public static class TestDataGenerators
    {
        private static readonly Random Random = new(42); // Fixed seed for reproducibility

        public static float[] GenerateFloatArray(int size, float min = 0f, float max = 100f)
        {
            return Enumerable.Range(0, size)
                            .Select(_ => min + (float)Random.NextDouble() * (max - min))
                            .ToArray();
        }

        public static float[] GenerateGaussianArray(int size, float mean = 0f, float stdDev = 1f)
        {
            var result = new float[size];
            for (int i = 0; i < size; i += 2)
            {
                var (g1, g2) = GenerateGaussianPair(mean, stdDev);
                result[i] = g1;
                if (i + 1 < size) result[i + 1] = g2;
            }
            return result;
        }

        public static float[] GenerateSparseArray(int size, float sparsity = 0.9f)
        {
            var result = new float[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = Random.NextDouble() < sparsity ? 0f : (float)Random.NextDouble() * 100f;
            }
            return result;
        }

        private static (float, float) GenerateGaussianPair(float mean, float stdDev)
        {
            var u1 = 1.0 - Random.NextDouble();
            var u2 = 1.0 - Random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            var randNormal = mean + stdDev * randStdNormal;
            
            var u3 = 1.0 - Random.NextDouble();
            var u4 = 1.0 - Random.NextDouble();
            var randStdNormal2 = Math.Sqrt(-2.0 * Math.Log(u3)) * Math.Cos(2.0 * Math.PI * u4);
            var randNormal2 = mean + stdDev * randStdNormal2;
            
            return ((float)randNormal, (float)randNormal2);
        }
    }
}

/// <summary>
/// Defines a complete compute workflow for testing.
/// </summary>
public class ComputeWorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<WorkflowKernel> Kernels { get; set; } = new();
    public List<WorkflowInput> Inputs { get; set; } = new();
    public List<WorkflowOutput> Outputs { get; set; } = new();
    public List<WorkflowIntermediateBuffer> IntermediateBuffers { get; set; } = new();
    public List<WorkflowExecutionStage> ExecutionStages { get; set; } = new();
    public bool ContinueOnError { get; set; } = false;
}

public class WorkflowKernel
{
    public string Name { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public CompilationOptions CompilationOptions { get; set; } = new();
}

public class WorkflowInput
{
    public string Name { get; set; } = string.Empty;
    public float[] Data { get; set; } = Array.Empty<float>();
}

public class WorkflowOutput
{
    public string Name { get; set; } = string.Empty;
    public int Size { get; set; }
    public Func<float[], bool>? Validator { get; set; }
}

public class WorkflowIntermediateBuffer
{
    public string Name { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public MemoryOptions Options { get; set; } = MemoryOptions.None;
}

public class WorkflowExecutionStage
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string KernelName { get; set; } = string.Empty;
    public ComputeBackendType BackendType { get; set; } = ComputeBackendType.CPU;
    public ExecutionOptions ExecutionOptions { get; set; } = new();
    public string[] ArgumentNames { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class WorkflowMemoryContext
{
    public Dictionary<string, IMemoryBuffer> InputBuffers { get; } = new();
    public Dictionary<string, IMemoryBuffer> OutputBuffers { get; } = new();
    public Dictionary<string, IMemoryBuffer> IntermediateBuffers { get; } = new();
}

public class WorkflowExecutionResult
{
    public string ExecutionId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, KernelCompilationResult> CompilationResults { get; set; } = new();
    public Dictionary<string, StageExecutionResult> ExecutionResults { get; set; } = new();
    public Dictionary<string, object> Results { get; set; } = new();
    public ValidationResult? Validation { get; set; }
    public PerformanceMetrics? Metrics { get; set; }
    public Exception? Error { get; set; }
}

public class KernelCompilationResult
{
    public bool Success { get; set; }
    public string KernelName { get; set; } = string.Empty;
    public ICompiledKernel? CompiledKernel { get; set; }
    public TimeSpan CompilationTime { get; set; }
    public Exception? Error { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class PerformanceMetrics
{
    public string WorkflowName { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public TimeSpan TotalDuration { get; set; }
    public double CompilationTime { get; set; }
    public double ExecutionTime { get; set; }
    public long MemoryUsageDelta { get; set; }
    public double ThroughputMBps { get; set; }
    public ResourceUtilization ResourceUtilization { get; set; } = new();
}

public class ResourceUtilization
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double GpuUsagePercent { get; set; }
}

/// <summary>
/// Tracks resource utilization during test execution.
/// </summary>
public class TestResourceTracker : IDisposable
{
    private readonly ConcurrentDictionary<string, ResourceTrackingSession> _sessions = new();
    private readonly Timer _monitoringTimer;
    private bool _disposed;

    public TestResourceTracker()
    {
        _monitoringTimer = new Timer(MonitorResources, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    public void BeginTracking(string sessionId)
    {
        _sessions[sessionId] = new ResourceTrackingSession
        {
            SessionId = sessionId,
            StartTime = DateTime.UtcNow,
            Samples = new List<ResourceSample>()
        };
    }

    public void EndTracking(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.EndTime = DateTime.UtcNow;
        }
    }

    public ResourceUtilization GetUtilization(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session) && session.Samples.Count > 0)
        {
            return new ResourceUtilization
            {
                CpuUsagePercent = session.Samples.Average(s => s.CpuUsage),
                MemoryUsagePercent = session.Samples.Average(s => s.MemoryUsage),
                GpuUsagePercent = session.Samples.Average(s => s.GpuUsage)
            };
        }

        return new ResourceUtilization();
    }

    private void MonitorResources(object? state)
    {
        if (_disposed) return;

        var currentTime = DateTime.UtcNow;
        
        // Sample current resource usage (simplified for testing)
        var sample = new ResourceSample
        {
            Timestamp = currentTime,
            CpuUsage = Environment.ProcessorCount > 1 ? Random.Shared.NextDouble() * 80 : 50,
            MemoryUsage = GC.GetTotalMemory(false) / 1024.0 / 1024.0, // MB
            GpuUsage = Random.Shared.NextDouble() * 60 // Simulated GPU usage
        };

        foreach (var session in _sessions.Values)
        {
            if (session.EndTime == null) // Still active
            {
                session.Samples.Add(sample);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _monitoringTimer?.Dispose();
            _sessions.Clear();
            _disposed = true;
        }
    }
}

public class ResourceTrackingSession
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ResourceSample> Samples { get; set; } = new();
}

public class ResourceSample
{
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double GpuUsage { get; set; }
}

// Helper enums and classes for testing
public enum ComputeBackendType
{
    CPU,
    CUDA,
    Metal,
    DirectCompute,
    OpenCL
}

public class ExecutionOptions
{
    public int[] GlobalWorkSize { get; set; } = Array.Empty<int>();
    public int[] LocalWorkSize { get; set; } = Array.Empty<int>();
    public bool EnableProfiling { get; set; } = false;
}

// Mock interfaces for compatibility
public interface IComputeEngine
{
    ValueTask<ICompiledKernel> CompileKernelAsync(string source, string name, CompilationOptions options, CancellationToken cancellationToken);
    ValueTask ExecuteAsync(ICompiledKernel kernel, object[] arguments, ComputeBackendType backend, ExecutionOptions options, CancellationToken cancellationToken);
}
