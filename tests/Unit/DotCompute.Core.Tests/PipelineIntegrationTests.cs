using Xunit;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Aot;
using DotCompute.Tests.Utilities.Kernels.Accelerators;
using DotCompute.Tests.Utilities.Kernels;
using DotCompute.Tests.Utilities.Kernels.Memory;
using DotCompute.Tests.Utilities.Kernels.Pipelines;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Integration tests for pipeline implementations.
/// </summary>
public sealed class PipelineIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TestMemoryManager _memoryManager = default!;
    private TestCpuAccelerator _accelerator = default!;
    private TestDevice _device = default!;
    private TestPipelineMemoryManager _pipelineMemoryManager = default!;

    public PipelineIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _memoryManager = new TestMemoryManager();
        _accelerator = new TestCpuAccelerator("Test CPU");
        _device = new TestDevice("device-1", "Test Device", DeviceType.CPU);
        _pipelineMemoryManager = new TestPipelineMemoryManager(_memoryManager);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _memoryManager?.Dispose();
        await _accelerator.DisposeAsync();
    }

    [Fact]
    public async Task SimplePipeline_SequentialKernels_ExecutesSuccessfully()
    {
        // Arrange
        var kernel1 = new TestCompiledKernel("Kernel1", new byte[100], new CompilationOptions());
        var kernel2 = new TestCompiledKernel("Kernel2", new byte[100], new CompilationOptions());
        var kernel3 = new TestCompiledKernel("Kernel3", new byte[100], new CompilationOptions());

        var builder = new TestKernelPipelineBuilder()
            .WithName("Sequential Pipeline")
            .AddKernel("Stage1", kernel1, cfg => cfg
                .WithWorkSize(256, 1, 1)
                .SetParameter("input", 42)
                .MapOutput("result", "stage1_output"))
            .AddKernel("Stage2", kernel2, cfg => cfg
                .WithWorkSize(512, 1, 1)
                .MapInput("input", "stage1_output")
                .MapOutput("result", "stage2_output"))
            .AddKernel("Stage3", kernel3, cfg => cfg
                .WithWorkSize(128, 1, 1)
                .MapInput("input", "stage2_output")
                .MapOutput("result", "final_output"));

        var pipeline = builder.Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = _pipelineMemoryManager,
            Device = _device
        };

        // Act
        var result = await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Outputs);
        Assert.True(result.Outputs.ContainsKey("final_output"));
        Assert.Equal(3, result.StageResults.Count);
        Assert.All(result.StageResults, sr => Assert.True(sr.Success));

        _output.WriteLine($"Pipeline executed in {result.Metrics.TotalDuration.TotalMilliseconds}ms");
        _output.WriteLine($"Memory usage: {result.Metrics.MemoryUsage} bytes");
        _output.WriteLine($"Throughput: {result.Metrics.ThroughputMBps:F2} MB/s");
    }

    [Fact]
    public async Task ParallelPipeline_MultipleKernels_ExecutesConcurrently()
    {
        // Arrange
        var kernelA = new TestCompiledKernel("KernelA", new byte[100], new CompilationOptions());
        var kernelB = new TestCompiledKernel("KernelB", new byte[100], new CompilationOptions());
        var kernelC = new TestCompiledKernel("KernelC", new byte[100], new CompilationOptions());

        var builder = new TestKernelPipelineBuilder()
            .WithName("Parallel Pipeline")
            .AddParallel(parallel => parallel
                .AddKernel("ParallelA", kernelA, cfg => cfg
                    .MapOutput("result", "outputA"))
                .AddKernel("ParallelB", kernelB, cfg => cfg
                    .MapOutput("result", "outputB"))
                .AddKernel("ParallelC", kernelC, cfg => cfg
                    .MapOutput("result", "outputC"))
                .WithMaxDegreeOfParallelism(3)
                .WithSynchronization(SynchronizationMode.WaitAll));

        var pipeline = builder.Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = _pipelineMemoryManager,
            Device = _device
        };

        // Act
        var result = await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StageResults.Count); // One parallel stage
        
        var parallelResult = result.StageResults[0];
        Assert.True(parallelResult.Success);
        Assert.NotNull(parallelResult.Metrics);
        Assert.Equal(3.0, parallelResult.Metrics["ParallelStages"]);
        Assert.Equal(3.0, parallelResult.Metrics["SuccessfulStages"]);

        _output.WriteLine($"Parallel execution completed in {result.Metrics.TotalDuration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task BranchPipeline_ConditionalExecution_TakesCorrectPath()
    {
        // Arrange
        var kernelTrue = new TestCompiledKernel("TrueKernel", new byte[100], new CompilationOptions());
        var kernelFalse = new TestCompiledKernel("FalseKernel", new byte[100], new CompilationOptions());

        var builder = new TestKernelPipelineBuilder()
            .WithName("Branch Pipeline")
            .AddBranch(
                context => context.State.ContainsKey("takeTrueBranch") &&(bool)context.State["takeTrueBranch"],
                trueBranch => trueBranch.AddKernel("TruePath", kernelTrue, cfg => cfg
                    .MapOutput("result", "true_output")),
                falseBranch => falseBranch.AddKernel("FalsePath", kernelFalse, cfg => cfg
                    .MapOutput("result", "false_output");

        var pipeline = builder.Build();

        // Test true branch
        var contextTrue = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = _pipelineMemoryManager,
            Device = _device
        };
        contextTrue.State["takeTrueBranch"] = true;

        var resultTrue = await pipeline.ExecuteAsync(contextTrue);
        Assert.True(resultTrue.Success);
        Assert.Contains("BranchTaken", resultTrue.Outputs.Keys);
        Assert.Equal("true", resultTrue.Outputs["BranchTaken"]);

        // Test false branch
        var contextFalse = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = _pipelineMemoryManager,
            Device = _device
        };
        contextFalse.State["takeTrueBranch"] = false;

        var resultFalse = await pipeline.ExecuteAsync(contextFalse);
        Assert.True(resultFalse.Success);
        Assert.Contains("BranchTaken", resultFalse.Outputs.Keys);
        Assert.Equal("false", resultFalse.Outputs["BranchTaken"]);

        _output.WriteLine("Branch pipeline executed both paths successfully");
    }

    [Fact]
    public async Task LoopPipeline_IterativeExecution_CompletesAllIterations()
    {
        // Arrange
        var kernel = new TestCompiledKernel("LoopKernel", new byte[100], new CompilationOptions());
        const int maxIterations = 5;

        var builder = new TestKernelPipelineBuilder()
            .WithName("Loop Pipeline")
            .AddLoop(
               (context, iteration) => iteration < maxIterations,
                body => body.AddKernel("IterationKernel", kernel, cfg => cfg
                    .MapOutput("result", $"iteration_output");

        var pipeline = builder.Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = _pipelineMemoryManager,
            Device = _device
        };

        // Act
        var result = await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Iterations", result.Outputs.Keys);
        Assert.Equal(maxIterations, Convert.ToInt32(result.Outputs["Iterations"]));
        Assert.Equal(maxIterations, Convert.ToInt32(result.Outputs["SuccessfulIterations"]));

        _output.WriteLine($"Loop executed {maxIterations} iterations successfully");
    }

    [Fact]
    public async Task ComplexPipeline_MixedStages_ExecutesCorrectly()
    {
        // Arrange
        var preprocessKernel = new TestCompiledKernel("Preprocess", new byte[100], new CompilationOptions());
        var kernel1 = new TestCompiledKernel("Kernel1", new byte[100], new CompilationOptions());
        var kernel2 = new TestCompiledKernel("Kernel2", new byte[100], new CompilationOptions());
        var kernel3 = new TestCompiledKernel("Kernel3", new byte[100], new CompilationOptions());
        var postprocessKernel = new TestCompiledKernel("Postprocess", new byte[100], new CompilationOptions());

        var builder = new TestKernelPipelineBuilder()
            .WithName("Complex Pipeline")
            // Preprocessing stage
            .AddKernel("Preprocess", preprocessKernel, cfg => cfg
                .SetParameter("scale", 2.0f)
                .MapOutput("result", "preprocessed"))
            // Parallel processing
            .AddParallel(parallel => parallel
                .AddKernel("Process1", kernel1, cfg => cfg
                    .MapInput("input", "preprocessed")
                    .MapOutput("result", "output1"))
                .AddKernel("Process2", kernel2, cfg => cfg
                    .MapInput("input", "preprocessed")
                    .MapOutput("result", "output2"))
                .AddKernel("Process3", kernel3, cfg => cfg
                    .MapInput("input", "preprocessed")
                    .MapOutput("result", "output3"))
                .WithMaxDegreeOfParallelism(3))
            // Postprocessing
            .AddKernel("Postprocess", postprocessKernel, cfg => cfg
                .MapOutput("result", "final_result"))
            // Add event handler
            .WithEventHandler(evt =>
            {
                _output.WriteLine($"[{evt.Type}] {evt.Message}");
            })
            // Configure optimization
            .WithOptimization(opt =>
            {
                opt.EnableKernelFusion = true;
                opt.EnableMemoryOptimization = true;
                opt.Level = PipelineOptimizationLevel.Balanced;
            });

        var pipeline = builder.Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object> { ["initial_data"] = new float[1024] },
            MemoryManager = _pipelineMemoryManager,
            Device = _device,
            Options = new PipelineExecutionOptions
            {
                EnableProfiling = true,
                EnableDetailedLogging = true,
                MaxParallelStages = 3
            }
        };

        // Act
        var result = await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Outputs);
        Assert.Contains("final_result", result.Outputs.Keys);
        Assert.Equal(3, result.StageResults.Count); // Preprocess, Parallel, Postprocess

        _output.WriteLine($"Complex pipeline executed successfully");
        _output.WriteLine($"Total stages: {result.StageResults.Count}");
        _output.WriteLine($"Total duration: {result.Metrics.TotalDuration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Pipeline_Validation_DetectsErrors()
    {
        // Arrange - Create pipeline with circular dependency
        var kernel1 = new TestCompiledKernel("Kernel1", new byte[100], new CompilationOptions());
        var kernel2 = new TestCompiledKernel("Kernel2", new byte[100], new CompilationOptions());

        var pipeline = new TestKernelPipeline("Invalid Pipeline");
        
        var stage1 = new TestKernelStage("Stage1", kernel1);
        var stage2 = new TestKernelStage("Stage2", kernel2);
        
        // Create circular dependency
        stage1.SetDependencies(new[] { stage2.Id });
        stage2.SetDependencies(new[] { stage1.Id });
        
        pipeline.AddStage(stage1);
        pipeline.AddStage(stage2);

        // Act
        var validationResult = pipeline.Validate();

        // Assert
        Assert.False(validationResult.IsValid);
        Assert.NotNull(validationResult.Errors);
        Assert.Contains(validationResult.Errors, e => e.Message.Contains("circular", StringComparison.Ordinal));

        _output.WriteLine($"Validation correctly detected circular dependency");
    }

    [Fact]
    public async Task Pipeline_ErrorHandling_RetriesOnError()
    {
        // Arrange
        var failingKernel = new TestFailingKernel("FailOnce", 1); // Fails once then succeeds
        var retryCount = 0;

        var builder = new TestKernelPipelineBuilder()
            .WithName("Error Handling Pipeline")
            .AddKernel("RetryStage", failingKernel)
            .WithErrorHandler((ex, context) =>
            {
                retryCount++;
                _output.WriteLine($"Error occurred: {ex.Message}, retry count: {retryCount}");
                return ErrorHandlingResult.Retry;
            });

        var pipeline = builder.Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = _pipelineMemoryManager,
            Device = _device
        };

        // Act
        var result = await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, retryCount); // Should have retried once

        _output.WriteLine($"Pipeline succeeded after {retryCount} retry");
    }

    /// <summary>
    /// Test device implementation.
    /// </summary>
    private class TestDevice : IComputeDevice
    {
        public TestDevice(string id, string name, DeviceType type)
        {
            Id = id;
            Name = name;
            Type = type;
        }

        public string Id { get; }
        public string Name { get; }
        public DeviceType Type { get; }
    }

    /// <summary>
    /// Test pipeline memory manager.
    /// </summary>
    private class TestPipelineMemoryManager : Core.Pipelines.IPipelineMemoryManager
    {
        private readonly TestMemoryManager _memoryManager;

        public TestPipelineMemoryManager(TestMemoryManager memoryManager)
        {
            _memoryManager = memoryManager;
        }

        public ValueTask<IMemoryBuffer> AllocateAsync(long size, CancellationToken cancellationToken = default)
        {
            return _memoryManager.AllocateAsync(size, MemoryOptions.None, cancellationToken);
        }

        public ValueTask ReleaseAsync(IMemoryBuffer buffer, CancellationToken cancellationToken = default)
        {
            return _memoryManager.DeallocateAsync(buffer, cancellationToken);
        }
    }

    /// <summary>
    /// Test kernel that fails a specified number of times before succeeding.
    /// </summary>
    private class TestFailingKernel : ICompiledKernel
    {
        private int _failuresRemaining;

        public TestFailingKernel(string name, int failureCount)
        {
            Name = name;
            _failuresRemaining = failureCount;
        }

        public string Name { get; }
        public byte[] CompiledCode => new byte[100];

        public ValueTask<object> ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
        {
            if(_failuresRemaining > 0)
            {
                _failuresRemaining--;
                throw new InvalidOperationException($"Simulated failure, {_failuresRemaining} remaining");
            }

            return ValueTask.FromResult<object>("Success");
        }

        public void Dispose() {     GC.SuppressFinalize(this);
 }
        GC.SuppressFinalize(this);
    }
}
}
