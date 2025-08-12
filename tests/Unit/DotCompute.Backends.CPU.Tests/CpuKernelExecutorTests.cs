// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.CPU;

public class CpuKernelExecutorTests
{
    private readonly FakeLogger<CpuKernelExecutor> _logger;
    private readonly CpuThreadPool _threadPool;
    private readonly CpuKernelExecutor _executor;

    public CpuKernelExecutorTests()
    {
        _logger = new FakeLogger<CpuKernelExecutor>();
        var options = Options.Create(new CpuThreadPoolOptions
        {
            WorkerThreads = 2,
            MaxQueuedItems = 100
        });
        _threadPool = new CpuThreadPool(options);
        _executor = new CpuKernelExecutor(_threadPool, _logger);
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        _executor.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithBasicKernel_ShouldCompleteSuccessfully()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "VectorAdd",
            Code = new byte[] { 0x01, 0x02, 0x03 },
            Metadata = new Dictionary<string, object>
            {
                ["Operation"] = "Add"
            }
        };

        using var memoryManager = new CpuMemoryManager(new FakeLogger<CpuMemoryManager>());
        using var buffer1 = memoryManager.AllocateBuffer(1024);
        using var buffer2 = memoryManager.AllocateBuffer(1024);
        using var buffer3 = memoryManager.AllocateBuffer(1024);

        var arguments = new KernelArguments(buffer1, buffer2, buffer3);
        var executionPlan = new KernelExecutionPlan
        {
            UseVectorization = false,
            VectorWidth = 128,
            VectorizationFactor = 4,
            InstructionSets = new HashSet<string> { "SSE" },
            Analysis = new KernelAnalysis
            {
                Definition = definition,
                IsVectorizable = true,
                MemoryAccessPattern = "Sequential",
                ComputeIntensity = 1.0
            }
        };

        // Act & Assert
        await _executor.Invoking(e => e.ExecuteAsync(definition, arguments, executionPlan))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        var arguments = new KernelArguments();
        var executionPlan = new KernelExecutionPlan();

        // Act & Assert
        await _executor.Invoking(e => e.ExecuteAsync(null!, arguments, executionPlan))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullArguments_ShouldThrowArgumentNullException()
    {
        // Arrange
        var definition = new KernelDefinition { Name = "Test", Code = new byte[0] };
        var executionPlan = new KernelExecutionPlan();

        // Act & Assert
        await _executor.Invoking(e => e.ExecuteAsync(definition, null!, executionPlan))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithVectorization_ShouldUseOptimizedPath()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "VectorizedAdd",
            Code = new byte[] { 0x01, 0x02, 0x03 },
            Metadata = new Dictionary<string, object>
            {
                ["Operation"] = "Add"
            }
        };

        using var memoryManager = new CpuMemoryManager(new FakeLogger<CpuMemoryManager>());
        using var buffer1 = memoryManager.AllocateBuffer(4096); // Larger buffer for vectorization
        using var buffer2 = memoryManager.AllocateBuffer(4096);
        using var buffer3 = memoryManager.AllocateBuffer(4096);

        var arguments = new KernelArguments(buffer1, buffer2, buffer3);
        var executionPlan = new KernelExecutionPlan
        {
            UseVectorization = true,
            VectorWidth = 256,
            VectorizationFactor = 8,
            InstructionSets = new HashSet<string> { "AVX2" },
            Analysis = new KernelAnalysis
            {
                Definition = definition,
                IsVectorizable = true,
                MemoryAccessPattern = "Sequential",
                ComputeIntensity = 2.0
            }
        };

        // Act & Assert
        await _executor.Invoking(e => e.ExecuteAsync(definition, arguments, executionPlan))
            .Should().NotThrowAsync();
    }

    [Fact]
    public void GetPerformanceMetrics_ShouldReturnValidMetrics()
    {
        // Act
        var metrics = _executor.GetPerformanceMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.ExecutionCount.Should().BeGreaterOrEqualTo(0);
        metrics.TotalExecutionTimeMs.Should().BeGreaterOrEqualTo(0);
        metrics.ThreadPoolStatistics.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var definition = new KernelDefinition { Name = "LongRunning", Code = new byte[0] };
        var arguments = new KernelArguments();
        var executionPlan = new KernelExecutionPlan();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await _executor.Invoking(e => e.ExecuteAsync(definition, arguments, executionPlan, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        _threadPool?.DisposeAsync().AsTask().Wait();
    }
}

public class SimdKernelExecutorTests
{
    private readonly SimdKernelExecutor _executor;

    public SimdKernelExecutorTests()
    {
        var simdCapabilities = new SimdSummary
        {
            IsHardwareAccelerated = true,
            PreferredVectorWidth = 256,
            SupportedInstructionSets = new HashSet<string> { "AVX2", "SSE" }
        };
        _executor = new SimdKernelExecutor(simdCapabilities);
    }

    [Fact]
    public void Constructor_WithValidCapabilities_ShouldInitialize()
    {
        // Assert
        _executor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullCapabilities_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action action = () => new SimdKernelExecutor(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public unsafe void Execute_WithValidBuffers_ShouldPerformVectorOperation()
    {
        // Arrange
        var elementCount = 16;
        var input1 = new byte[elementCount * sizeof(float)];
        var input2 = new byte[elementCount * sizeof(float)];
        var output = new byte[elementCount * sizeof(float)];

        // Fill input buffers with test data
        fixed (byte* p1 = input1, p2 = input2)
        {
            var f1 = (float*)p1;
            var f2 = (float*)p2;
            for (int i = 0; i < elementCount; i++)
            {
                f1[i] = i + 1.0f;
                f2[i] = (i + 1.0f) * 2.0f;
            }
        }

        // Act & Assert
        _executor.Invoking(e => e.Execute(input1, input2, output, elementCount, 256))
            .Should().NotThrow();

        // Verify results
        fixed (byte* pOut = output)
        {
            var fOut = (float*)pOut;
            for (int i = 0; i < elementCount; i++)
            {
                fOut[i].Should().BeApproximately((i + 1.0f) + (i + 1.0f) * 2.0f, 0.001f);
            }
        }
    }

    [Fact]
    public unsafe void ExecuteUnary_WithSqrtOperation_ShouldCalculateCorrectly()
    {
        // Arrange
        var elementCount = 8;
        var input = new byte[elementCount * sizeof(float)];
        var output = new byte[elementCount * sizeof(float)];

        // Fill input with perfect squares
        fixed (byte* pIn = input)
        {
            var fIn = (float*)pIn;
            for (int i = 0; i < elementCount; i++)
            {
                fIn[i] = (i + 1) * (i + 1); // 1, 4, 9, 16, 25, 36, 49, 64
            }
        }

        // Act
        _executor.ExecuteUnary(input, output, elementCount, UnaryOperation.Sqrt);

        // Assert
        fixed (byte* pOut = output)
        {
            var fOut = (float*)pOut;
            for (int i = 0; i < elementCount; i++)
            {
                fOut[i].Should().BeApproximately(i + 1, 0.001f);
            }
        }
    }

    [Fact]
    public unsafe void ExecuteFma_WithValidInputs_ShouldCalculateCorrectly()
    {
        // Arrange
        var elementCount = 8;
        var input1 = new byte[elementCount * sizeof(float)];
        var input2 = new byte[elementCount * sizeof(float)];
        var input3 = new byte[elementCount * sizeof(float)];
        var output = new byte[elementCount * sizeof(float)];

        // Fill inputs: result = (input1 * input2) + input3
        fixed (byte* p1 = input1, p2 = input2, p3 = input3)
        {
            var f1 = (float*)p1;
            var f2 = (float*)p2;
            var f3 = (float*)p3;
            for (int i = 0; i < elementCount; i++)
            {
                f1[i] = i + 1.0f;     // 1, 2, 3, 4, 5, 6, 7, 8
                f2[i] = 2.0f;         // 2, 2, 2, 2, 2, 2, 2, 2
                f3[i] = 1.0f;         // 1, 1, 1, 1, 1, 1, 1, 1
            }
        }

        // Act
        _executor.ExecuteFma(input1, input2, input3, output, elementCount);

        // Assert - result should be ((i+1) * 2) + 1
        fixed (byte* pOut = output)
        {
            var fOut = (float*)pOut;
            for (int i = 0; i < elementCount; i++)
            {
                var expected = ((i + 1.0f) * 2.0f) + 1.0f;
                fOut[i].Should().BeApproximately(expected, 0.001f);
            }
        }
    }

    [Fact]
    public void GetMaxVectorElements_ShouldReturnPositiveValue()
    {
        // Act
        var maxElements = _executor.GetMaxVectorElements();

        // Assert
        maxElements.Should().BeGreaterThan(0);
        maxElements.Should().BeLessOrEqualTo(16); // AVX-512 max
    }

    [Fact]
    public void IsVectorizationBeneficial_WithSufficientElements_ShouldReturnTrue()
    {
        // Act
        var beneficial = _executor.IsVectorizationBeneficial(32);

        // Assert
        beneficial.Should().BeTrue();
    }

    [Fact]
    public void IsVectorizationBeneficial_WithInsufficientElements_ShouldReturnFalse()
    {
        // Act
        var beneficial = _executor.IsVectorizationBeneficial(1);

        // Assert
        beneficial.Should().BeFalse();
    }

    [Fact]
    public void GetOptimalWorkGroupSize_ShouldReturnReasonableValue()
    {
        // Act
        var workGroupSize = _executor.GetOptimalWorkGroupSize();

        // Assert
        workGroupSize.Should().BeGreaterThan(0);
        workGroupSize.Should().BeLessOrEqualTo(1024);
    }

    [Theory]
    [InlineData(UnaryOperation.Abs)]
    [InlineData(UnaryOperation.Negate)]
    [InlineData(UnaryOperation.Sqrt)]
    public unsafe void ExecuteUnary_WithDifferentOperations_ShouldWork(UnaryOperation operation)
    {
        // Arrange
        var elementCount = 4;
        var input = new byte[elementCount * sizeof(float)];
        var output = new byte[elementCount * sizeof(float)];

        fixed (byte* pIn = input)
        {
            var fIn = (float*)pIn;
            for (int i = 0; i < elementCount; i++)
            {
                fIn[i] = (i + 1) * (operation == UnaryOperation.Sqrt ? 4.0f : 1.0f);
            }
        }

        // Act & Assert
        _executor.Invoking(e => e.ExecuteUnary(input, output, elementCount, operation))
            .Should().NotThrow();
    }

    [Fact]
    public void Execute_WithInsufficientBufferSize_ShouldThrowArgumentException()
    {
        // Arrange
        var elementCount = 16;
        var smallBuffer = new byte[8]; // Too small
        var normalBuffer = new byte[elementCount * sizeof(float)];

        // Act & Assert
        _executor.Invoking(e => e.Execute(smallBuffer, normalBuffer, normalBuffer, elementCount, 256))
            .Should().Throw<ArgumentException>();
    }
}

public class CpuThreadPoolTests
{
    private readonly CpuThreadPoolOptions _options;
    private readonly IOptions<CpuThreadPoolOptions> _optionsWrapper;

    public CpuThreadPoolTests()
    {
        _options = new CpuThreadPoolOptions
        {
            WorkerThreads = 2,
            MaxQueuedItems = 100,
            UseThreadAffinity = false // Disable for tests
        };
        _optionsWrapper = Options.Create(_options);
    }

    [Fact]
    public void Constructor_ShouldInitializeThreadPool()
    {
        // Act
        using var threadPool = new CpuThreadPool(_optionsWrapper);

        // Assert
        threadPool.Should().NotBeNull();
        threadPool.WorkerCount.Should().Be(_options.WorkerThreads);
    }

    [Fact]
    public async Task EnqueueAsync_WithSimpleAction_ShouldExecute()
    {
        // Arrange
        using var threadPool = new CpuThreadPool(_optionsWrapper);
        var executed = false;

        // Act
        await threadPool.EnqueueAsync(() => executed = true);

        // Assert
        // Give some time for execution
        await Task.Delay(100);
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueAsync_WithFunction_ShouldReturnResult()
    {
        // Arrange
        using var threadPool = new CpuThreadPool(_optionsWrapper);

        // Act
        var result = await threadPool.EnqueueAsync(() => 42);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithMultipleActions_ShouldExecuteAll()
    {
        // Arrange
        using var threadPool = new CpuThreadPool(_optionsWrapper);
        var executionCount = 0;
        var actions = Enumerable.Range(0, 10)
            .Select(_ => new Action(() => Interlocked.Increment(ref executionCount)))
            .ToArray();

        // Act
        await threadPool.EnqueueBatchAsync(actions);

        // Assert
        // Give some time for execution
        await Task.Delay(200);
        executionCount.Should().Be(10);
    }

    [Fact]
    public void GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        using var threadPool = new CpuThreadPool(_optionsWrapper);

        // Act
        var stats = threadPool.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.ThreadCount.Should().Be(_options.WorkerThreads);
        stats.LocalQueueCounts.Should().HaveCount(_options.WorkerThreads);
        stats.TotalQueuedItems.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        using var threadPool = new CpuThreadPool(_optionsWrapper);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await threadPool.Invoking(tp => tp.EnqueueAsync(() => { }, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteGracefully()
    {
        // Arrange
        var threadPool = new CpuThreadPool(_optionsWrapper);

        // Act & Assert
        await threadPool.Invoking(tp => tp.DisposeAsync().AsTask())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnqueueAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var threadPool = new CpuThreadPool(_optionsWrapper);
        await threadPool.DisposeAsync();

        // Act & Assert
        await threadPool.Invoking(tp => tp.EnqueueAsync(() => { }))
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void WorkerCount_ShouldMatchConfiguration()
    {
        // Arrange & Act
        using var threadPool = new CpuThreadPool(_optionsWrapper);

        // Assert
        threadPool.WorkerCount.Should().Be(_options.WorkerThreads);
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithEmptyCollection_ShouldCompleteImmediately()
    {
        // Arrange
        using var threadPool = new CpuThreadPool(_optionsWrapper);
        var emptyActions = Array.Empty<Action>();

        // Act & Assert
        await threadPool.Invoking(tp => tp.EnqueueBatchAsync(emptyActions))
            .Should().CompleteWithinAsync(TimeSpan.FromMilliseconds(100));
    }
}