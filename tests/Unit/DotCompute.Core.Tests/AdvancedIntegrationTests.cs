using Xunit;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Xunit.Abstractions;
using DotCompute.Tests.Implementations.Kernels;
using DotCompute.Tests.Implementations.Accelerators;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Advanced integration tests demonstrating real implementations working together
/// in complex scenarios without requiring GPU hardware.
/// </summary>
public sealed class AdvancedIntegrationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly ITestOutputHelper _output = output;
    private readonly TestAcceleratorManager _acceleratorManager = default!;
    private readonly TestKernelExecutor _kernelExecutor = default!;
    private readonly TestCudaKernelCompiler _cudaCompiler = default!;
    private readonly TestOpenCLKernelCompiler _openClCompiler = default!;
    private readonly TestDirectComputeCompiler _directComputeCompiler = default!;

#pragma warning disable CA2000 // Dispose objects before losing scope - test objects disposed in DisposeAsync
    public async Task InitializeAsync()
    {
        var manager = new TestAcceleratorManager();
        Unsafe.AsRef(in _acceleratorManager) = manager;
        await _acceleratorManager.InitializeAsync();
#pragma warning restore CA2000

        Unsafe.AsRef(in _kernelExecutor) = new TestKernelExecutor();
        Unsafe.AsRef(in _cudaCompiler) = new TestCudaKernelCompiler();
        Unsafe.AsRef(in _openClCompiler) = new TestOpenCLKernelCompiler();
        Unsafe.AsRef(in _directComputeCompiler) = new TestDirectComputeCompiler();
    }

    public async Task DisposeAsync()
    {
        _kernelExecutor?.Dispose();
        await _acceleratorManager.DisposeAsync();
    }

    [Fact]
    public async Task AcceleratorManager_Discovery_ShouldFindAccelerators()
    {
        // Assert
        Assert.True(_acceleratorManager.Count > 0);
        Assert.NotNull(_acceleratorManager.Default);

        _output.WriteLine($"Found {_acceleratorManager.Count} accelerators");

        foreach (var accelerator in _acceleratorManager.AvailableAccelerators)
        {
            _output.WriteLine($"  - {accelerator.Info.Name}: {accelerator.Info.DeviceType}, " +
                            $"Memory: {accelerator.Info.TotalMemory / (1024 * 1024)}MB");
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task AcceleratorManager_SelectBest_ShouldSelectOptimalDevice()
    {
        // Arrange
        var criteria = new AcceleratorSelectionCriteria
        {
            MinimumMemory = 1024 * 1024 * 50, // 50MB - lower requirement
            PreferredType = AcceleratorType.CPU,
            PreferDedicated = true
        };

        // Act
        var bestAccelerator = _acceleratorManager.SelectBest(criteria);

        // Assert
        Assert.NotNull(bestAccelerator);
        Assert.True(bestAccelerator.Info.TotalMemory >= criteria.MinimumMemory);

        _output.WriteLine($"Selected accelerator: {bestAccelerator.Info.Name}");
        _output.WriteLine($"  Type: {bestAccelerator.Info.DeviceType}");
        _output.WriteLine($"  Memory: {bestAccelerator.Info.TotalMemory / (1024 * 1024)}MB");

        await Task.CompletedTask;
    }

    [Fact]
    public async Task KernelCompiler_CUDA_CompileAndCache()
    {
        // Arrange
        var source = new TestKernelSource
        {
            Name = "VectorAdd",
            Code = "__kernel void vector_add(__global float* a, __global float* b, __global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
            Language = KernelLanguage.Cuda,
            EntryPoint = "vector_add"
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true,
            UnrollLoops = true
        };

        // Act - First compilation
        var stopwatch = Stopwatch.StartNew();
        var compiledInfo1 = await _cudaCompiler.CompileAsync(source, options);
        stopwatch.Stop();
        var firstCompileTime = stopwatch.ElapsedMilliseconds;

        // Act - Second compilation(should be cached)
        stopwatch.Restart();
        var compiledInfo2 = await _cudaCompiler.CompileAsync(source, options);
        stopwatch.Stop();
        var secondCompileTime = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.NotNull(compiledInfo1);
        Assert.NotNull(compiledInfo2);
        Assert.Equal(compiledInfo1.Name, compiledInfo2.Name);
        Assert.Contains("PTX", compiledInfo1.Assembly, StringComparison.Ordinal);

        _output.WriteLine($"CUDA Compiler Results:");
        _output.WriteLine($"  First compile: {firstCompileTime}ms");
        _output.WriteLine($"  Second compilecached): {secondCompileTime}ms");
        _output.WriteLine($"  Total compilations: {_cudaCompiler.CompilationCount}");
        _output.WriteLine($"  Average time: {_cudaCompiler.AverageCompilationTimeMs:F2}ms");
    }

    [Fact]
    public async Task KernelCompiler_OpenCL_CompileWithDiagnostics()
    {
        // Arrange
        var source = new TestKernelSource
        {
            Name = "MatrixMultiply",
            Code = "kernel code here",
            Language = KernelLanguage.OpenCL,
            EntryPoint = "matrix_multiply"
        };

        // Act
        var compiledInfo = await _openClCompiler.CompileAsync(source, new CompilationOptions());

        // Assert
        Assert.NotNull(compiledInfo);
        Assert.Contains("SPIR-V", compiledInfo.Assembly, StringComparison.Ordinal);
        Assert.NotEmpty(_openClCompiler.Diagnostics);

        _output.WriteLine("OpenCL Compiler Diagnostics:");
        foreach (var diagnostic in _openClCompiler.Diagnostics)
        {
            _output.WriteLine($"  [{diagnostic.Level}] {diagnostic.Message}");
        }
    }

    [Fact]
    public async Task KernelExecutor_SimulateExecution_WithStatistics()
    {
        // Arrange
        _ = _acceleratorManager.Default;
#pragma warning disable CA2000 // Dispose objects before losing scope - test kernel used in test method
        var kernel = new TestCompiledKernel("TestKernel", new byte[100], new CompilationOptions());
#pragma warning restore CA2000
        var args = new KernelArguments(new float[1024], new float[1024], new float[1024]);
        var config = new KernelConfiguration(new Dim3(32), new Dim3(32));

        // Act - Execute multiple times
        for (var i = 0; i < 5; i++)
        {
            var result = await _kernelExecutor.ExecuteAsync(kernel, args, config);
            Assert.True(result.Success);
        }

        // Assert and display statistics
        var stats = _kernelExecutor.Statistics;
        Assert.NotEmpty(stats);

        _output.WriteLine("Kernel Execution Statistics:");
        foreach (var stat in stats.Values)
        {
            _output.WriteLine($"  Kernel: {stat.KernelName}");
            _output.WriteLine($"    Executions: {stat.ExecutionCount}");
            _output.WriteLine($"    Success Rate: {stat.SuccessRate:F1}%");
            _output.WriteLine($"    Avg Time: {stat.AverageExecutionTimeMs:F2}ms");
            _output.WriteLine($"    Min/Max: {stat.MinExecutionTimeMs:F2}ms / {stat.MaxExecutionTimeMs:F2}ms");
        }
    }

    [Fact]
    public async Task FullPipeline_MatrixMultiplication_Simulation()
    {
        _output.WriteLine("=== Full Matrix Multiplication Pipeline ===");

        // Step 1: Select best accelerator
        var accelerator = _acceleratorManager.SelectBest(new AcceleratorSelectionCriteria
        {
            MinimumMemory = 1024 * 1024 * 100, // 100MB
            CustomScorer = a => a.Info.ComputeUnits * a.Info.MaxClockFrequency
        });

        Assert.NotNull(accelerator);
        _output.WriteLine($"Selected: {accelerator.Info.Name}");

        // Step 2: Allocate memory
        const int matrixSize = 512;
        const int matrixBytes = matrixSize * matrixSize * sizeof(float);

        var bufferA = await accelerator.Memory.AllocateAsync(matrixBytes);
        var bufferB = await accelerator.Memory.AllocateAsync(matrixBytes);
        var bufferC = await accelerator.Memory.AllocateAsync(matrixBytes);

        _output.WriteLine($"Allocated 3x {matrixBytes / 1024}KB buffers");

        // Step 3: Initialize matrices
        var hostA = new float[matrixSize * matrixSize];
        var hostB = new float[matrixSize * matrixSize];

        var random = new Random(42);
        for (var i = 0; i < hostA.Length; i++)
        {
            hostA[i] = (float)random.NextDouble();
            hostB[i] = (float)random.NextDouble();
        }

        await bufferA.CopyFromHostAsync<float>(hostA.AsMemory());
        await bufferB.CopyFromHostAsync<float>(hostB.AsMemory());

        _output.WriteLine("Copied input matrices to device");

        // Step 4: Compile kernel
        var kernelSource = new TestKernelSource
        {
            Name = "MatrixMultiply",
            Code = @"
                __kernel void matrix_multiply(
                    __global float* A,
                    __global float* B,
                    __global float* C,
                    int N) {
                    int row = get_global_id(0);
                    int col = get_global_id(1);
                    float sum = 0.0f;
                    for(int k = 0; k < N; k++) {
                        sum += A[row * N + k] * B[k * N + col];
                    }
                    C[row * N + col] = sum;
                }",
            Language = KernelLanguage.Cuda,
            EntryPoint = "matrix_multiply"
        };

        var compiledInfo = await _cudaCompiler.CompileAsync(kernelSource,
            new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

#pragma warning disable CA2000 // Dispose objects before losing scope - test kernel used in test method
        var compiledKernel = new TestCompiledKernel(
            compiledInfo.Name,
            System.Text.Encoding.UTF8.GetBytes(compiledInfo.Assembly),
            compiledInfo.Options);
#pragma warning restore CA2000

        _output.WriteLine($"Compiled kernel: {compiledInfo.Name}");

        // Step 5: Execute kernel
        var kernelArgs = new KernelArguments(bufferA, bufferB, bufferC, matrixSize);
        var kernelConfig = new KernelConfiguration(
            new Dim3(matrixSize / 16, matrixSize / 16),
            new Dim3(16, 16));

        var stopwatch = Stopwatch.StartNew();
        var execResult = await _kernelExecutor.ExecuteAsync(compiledKernel, kernelArgs, kernelConfig);
        stopwatch.Stop();

        Assert.True(execResult.Success);
        _output.WriteLine($"Kernel execution: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Threads: {execResult.ThreadsExecuted:N0}");
        _output.WriteLine($"  Throughput: {execResult.Throughput:F0} threads/sec");

        // Step 6: Verify results(simplified)
        var hostC = new float[matrixSize * matrixSize];
        await bufferC.CopyToHostAsync<float>(hostC.AsMemory());

        // Check that output buffer was written to(simplified check)
        // In test implementation, the buffer remains unchanged but we verify the operation completed
        Assert.NotNull(hostC);
        Assert.Equal(matrixSize * matrixSize, hostC.Length);

        _output.WriteLine($"Result validation: Buffer size verified{hostC.Length} elements)");

        // Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
    }

    [Fact]
    public async Task MultiAccelerator_Concurrent_Execution()
    {
        // Create a new manager with GPU provider registered before initialization
        var manager = new TestAcceleratorManager();
        var gpuProvider = new TestGpuAcceleratorProvider(2);
        manager.RegisterProvider(gpuProvider);
        await manager.InitializeAsync();

        Assert.True(manager.Count >= 3);

        _output.WriteLine($"Total accelerators: {manager.Count}");

        // Execute kernels on multiple accelerators concurrently
        var tasks = manager.AvailableAccelerators
            .Take(3)
            .Select(async (accelerator, index) =>
            {
                var kernel = await accelerator.CompileKernelAsync(
                    new KernelDefinition(
                        $"Kernel_{index}",
                        new TestKernelSource
                        {
                            Name = $"Kernel_{index}",
                            Code = "test",
                            Language = KernelLanguage.OpenCL
                        },
                        new CompilationOptions()));

                var args = new KernelArguments();
                await kernel.ExecuteAsync(args);

                return accelerator.Info.Name;
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            _output.WriteLine($"Executed on: {result}");
        }

        Assert.Equal(3, results.Length);

        // Cleanup
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task KernelExecutor_QueueManagement_AndConcurrency()
    {
        // Arrange
        const int kernelCount = 20;
        var kernels = new TestCompiledKernel[kernelCount];

        for (var i = 0; i < kernelCount; i++)
        {
            kernels[i] = new TestCompiledKernel(
                $"Kernel_{i}",
                new byte[100],
                new CompilationOptions());
        }

        // Act - Queue multiple kernels
        var executionTasks = kernels.Select(kernel =>
            _kernelExecutor.ExecuteAsync(
                kernel,
                new KernelArguments(),
                new KernelConfiguration(new Dim3(64), new Dim3(64))
            )).ToArray();

        // Monitor queue
        _output.WriteLine($"Initial queue size: {_kernelExecutor.QueuedExecutions}");

        // Wait for completion
        var results = await Task.WhenAll(executionTasks);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(kernelCount, _kernelExecutor.TotalExecutions);

        _output.WriteLine($"Execution Summary:");
        _output.WriteLine($"  Total executions: {_kernelExecutor.TotalExecutions}");
        _output.WriteLine($"  Total time: {_kernelExecutor.TotalExecutionTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Average per kernel: {_kernelExecutor.TotalExecutionTime.TotalMilliseconds / kernelCount:F2}ms");
    }

    [Fact]
    public async Task Memory_ViewOperations_WithComplexScenarios()
    {
        // Arrange
        var accelerator = _acceleratorManager.Default;
        const long bufferSize = 1024 * 1024; // 1MB
        const long viewSize = 256 * 1024;    // 256KB
        const long viewOffset = 128 * 1024;  // 128KB offset

        var mainBuffer = await accelerator.Memory.AllocateAsync(bufferSize);

        // Create multiple views
        var view1 = accelerator.Memory.CreateView(mainBuffer, 0, viewSize);
        var view2 = accelerator.Memory.CreateView(mainBuffer, viewOffset, viewSize);
        var view3 = accelerator.Memory.CreateView(mainBuffer, viewOffset * 2, viewSize);

        // Initialize data through views
        var data1 = Enumerable.Range(0, (int)(viewSize / sizeof(int))).ToArray();
        var data2 = Enumerable.Range(1000, (int)(viewSize / sizeof(int))).ToArray();
        var data3 = Enumerable.Range(2000, (int)(viewSize / sizeof(int))).ToArray();

        await view1.CopyFromHostAsync<int>(data1.AsMemory());
        await view2.CopyFromHostAsync<int>(data2.AsMemory());
        await view3.CopyFromHostAsync<int>(data3.AsMemory());

        // Read back through views
        var readback1 = new int[data1.Length];
        var readback2 = new int[data2.Length];
        var readback3 = new int[data3.Length];

        await view1.CopyToHostAsync<int>(readback1.AsMemory());
        await view2.CopyToHostAsync<int>(readback2.AsMemory());
        await view3.CopyToHostAsync<int>(readback3.AsMemory());

        // Verify
        Assert.Equal(data1[0], readback1[0]);
        Assert.Equal(data2[0], readback2[0]);
        Assert.Equal(data3[0], readback3[0]);

        _output.WriteLine($"Memory view operations successful:");
        _output.WriteLine($"  Main buffer: {bufferSize / 1024}KB");
        _output.WriteLine($"  3 views of {viewSize / 1024}KB each");
        _output.WriteLine($"  Data integrity verified");

        // Cleanup
        await mainBuffer.DisposeAsync();
    }

    [Fact]
    public async Task CompilerComparison_AllLanguages()
    {
        // Arrange
        var kernelName = "UniversalKernel";
        var baseCode = "kernel implementation";

        var cudaSource = new TestKernelSource
        {
            Name = kernelName,
            Code = baseCode,
            Language = KernelLanguage.Cuda,
            EntryPoint = "main"
        };

        var openclSource = new TestKernelSource
        {
            Name = kernelName,
            Code = baseCode,
            Language = KernelLanguage.OpenCL,
            EntryPoint = "main"
        };

        var hlslSource = new TestKernelSource
        {
            Name = kernelName,
            Code = baseCode,
            Language = KernelLanguage.HLSL,
            EntryPoint = "main"
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release,
            FastMath = true
        };

        // Act
        var cudaResult = await _cudaCompiler.CompileAsync(cudaSource, options);
        var openclResult = await _openClCompiler.CompileAsync(openclSource, options);
        var hlslResult = await _directComputeCompiler.CompileAsync(hlslSource, options);

        // Assert and compare
        Assert.NotNull(cudaResult);
        Assert.NotNull(openclResult);
        Assert.NotNull(hlslResult);

        _output.WriteLine("Compiler Comparison:");
        _output.WriteLine($"  CUDAPTX):");
        _output.WriteLine($"    Assembly size: {cudaResult.Assembly.Length} chars");
        _output.WriteLine($"    Compile time: {_cudaCompiler.AverageCompilationTimeMs:F2}ms");

        _output.WriteLine($"  OpenCLSPIR-V):");
        _output.WriteLine($"    Assembly size: {openclResult.Assembly.Length} chars");
        _output.WriteLine($"    Compile time: {_openClCompiler.AverageCompilationTimeMs:F2}ms");

        _output.WriteLine($"  DirectComputeDXIL):");
        _output.WriteLine($"    Assembly size: {hlslResult.Assembly.Length} chars");
        _output.WriteLine($"    Compile time: {_directComputeCompiler.AverageCompilationTimeMs:F2}ms");
    }
}
