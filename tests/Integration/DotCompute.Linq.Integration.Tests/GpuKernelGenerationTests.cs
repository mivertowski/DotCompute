using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Linq.Compilation;
using DotCompute.Memory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Integration tests for GPU kernel generation, focusing on CUDA code generation,
/// compute capabilities, memory management, and multi-GPU scenarios.
/// </summary>
public class GpuKernelGenerationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IGpuKernelGenerator _kernelGenerator;
    private readonly Mock<ICudaRuntime> _mockCudaRuntime;
    private readonly Mock<IAccelerator> _mockGpuAccelerator;
    private readonly Mock<ICompiledKernel> _mockKernel;

    public GpuKernelGenerationTests()
    {
        var services = new ServiceCollection();
        
        // Setup mocks
        _mockCudaRuntime = new Mock<ICudaRuntime>();
        _mockGpuAccelerator = new Mock<IAccelerator>();
        _mockKernel = new Mock<ICompiledKernel>();
        
        // Configure services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IGpuKernelGenerator, GpuKernelGenerator>();
        services.AddSingleton<ICudaCapabilityManager, CudaCapabilityManager>();
        services.AddSingleton<IGpuMemoryManager, GpuMemoryManager>();
        services.AddSingleton<IMultiGpuCoordinator, MultiGpuCoordinator>();
        services.AddSingleton(_mockCudaRuntime.Object);
        services.AddSingleton(_mockGpuAccelerator.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _kernelGenerator = _serviceProvider.GetRequiredService<IGpuKernelGenerator>();
        
        // Setup mock behavior
        _mockCudaRuntime.Setup(x => x.GetDeviceCount()).Returns(2);
        _mockCudaRuntime.Setup(x => x.GetDeviceProperties(It.IsAny<int>()))
            .Returns(new CudaDeviceProperties
            {
                Name = "RTX 2000 Ada",
                ComputeCapabilityMajor = 8,
                ComputeCapabilityMinor = 9,
                MaxThreadsPerBlock = 1024,
                SharedMemoryPerBlock = 49152,
                TotalGlobalMemory = 16 * 1024 * 1024 * 1024L // 16GB
            });
            
        _mockGpuAccelerator.Setup(x => x.CompileKernel(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>()))
            .ReturnsAsync(_mockKernel.Object);
            
        _mockKernel.Setup(x => x.ExecuteAsync(It.IsAny<object[]>()))
            .ReturnsAsync(new KernelExecutionResult { Success = true });
    }

    [Fact]
    public async Task GenerateSimpleKernel_ShouldProduceCudaCode()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "VectorAdd",
            SourceCode = "result[idx] = a[idx] + b[idx];",
            Parameters = new[]
            {
                new KernelParameter { Name = "a", Type = typeof(float[]) },
                new KernelParameter { Name = "b", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            MaxThreadsPerBlock = 256,
            EnableDebugInfo = false
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.CudaCode.Should().Contain("__global__");
        result.CudaCode.Should().Contain("VectorAdd");
        result.CudaCode.Should().Contain("threadIdx.x");
        result.CudaCode.Should().Contain("blockIdx.x");
        result.CudaCode.Should().Contain("blockDim.x");
    }

    [Fact]
    public async Task GenerateKernelWithOptimizations_ShouldApplyOptimizations()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "MatrixMultiply",
            SourceCode = @"
                for (int i = 0; i < rows; i++) {
                    for (int j = 0; j < cols; j++) {
                        float sum = 0;
                        for (int k = 0; k < inner; k++) {
                            sum += a[i * inner + k] * b[k * cols + j];
                        }
                        result[i * cols + j] = sum;
                    }
                }",
            Parameters = new[]
            {
                new KernelParameter { Name = "a", Type = typeof(float[]) },
                new KernelParameter { Name = "b", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) },
                new KernelParameter { Name = "rows", Type = typeof(int) },
                new KernelParameter { Name = "cols", Type = typeof(int) },
                new KernelParameter { Name = "inner", Type = typeof(int) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            EnableSharedMemory = true,
            EnableTensorCores = true,
            OptimizationLevel = OptimizationLevel.Aggressive,
            TileSize = 16
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.CudaCode.Should().Contain("__shared__");
        result.CudaCode.Should().Contain("__syncthreads()");
        result.OptimizationsApplied.Should().Contain("SharedMemoryTiling");
        result.OptimizationsApplied.Should().Contain("LoopUnrolling");
    }

    [Fact]
    public async Task GenerateKernelForDifferentComputeCapabilities_ShouldAdaptCode()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "ReduceSum",
            SourceCode = "atomicAdd(&result[0], data[idx]);",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var optionsOldGpu = new GpuCompilationOptions { ComputeCapability = "3.5" };
        var optionsNewGpu = new GpuCompilationOptions { ComputeCapability = "8.9" };

        // Act
        var resultOldGpu = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, optionsOldGpu);
        var resultNewGpu = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, optionsNewGpu);

        // Assert
        resultOldGpu.Should().NotBeNull();
        resultNewGpu.Should().NotBeNull();
        
        // Newer GPU should have additional optimizations
        resultNewGpu.CudaCode.Should().NotBe(resultOldGpu.CudaCode);
        resultNewGpu.OptimizationsApplied.Should().HaveCountGreaterThan(resultOldGpu.OptimizationsApplied.Count);
    }

    [Fact]
    public async Task GenerateKernelWithCustomMemoryLayout_ShouldRespectLayout()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "CustomLayout",
            SourceCode = "result[idx] = data[idx] * 2.0f;",
            Parameters = new[]
            {
                new KernelParameter 
                { 
                    Name = "data", 
                    Type = typeof(float[]),
                    MemoryLayout = MemoryLayout.RowMajor
                },
                new KernelParameter 
                { 
                    Name = "result", 
                    Type = typeof(float[]),
                    MemoryLayout = MemoryLayout.ColumnMajor
                }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            RespectMemoryLayout = true
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.MemoryLayoutInfo.Should().NotBeEmpty();
        result.MemoryLayoutInfo.Should().ContainKey("data");
        result.MemoryLayoutInfo.Should().ContainKey("result");
    }

    [Fact]
    public async Task GenerateKernelWithErrorHandling_ShouldValidateInput()
    {
        // Arrange
        var invalidKernelDefinition = new KernelDefinition
        {
            Name = "Invalid",
            SourceCode = "undefined_function();", // Invalid CUDA code
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(string) } // Unsupported type
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            ValidateInput = true
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(invalidKernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("unsupported type"));
    }

    [Fact]
    public async Task GenerateMultiGpuKernel_ShouldCreateDistributedCode()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "DistributedCompute",
            SourceCode = "result[idx] = data[idx] * scale;",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) },
                new KernelParameter { Name = "scale", Type = typeof(float) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            EnableMultiGpu = true,
            GpuCount = 2,
            DistributionStrategy = GpuDistributionStrategy.DataParallel
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.MultiGpuInfo.Should().NotBeNull();
        result.MultiGpuInfo.GpuCount.Should().Be(2);
        result.MultiGpuInfo.DistributionStrategy.Should().Be(GpuDistributionStrategy.DataParallel);
        result.CudaCode.Should().Contain("cudaSetDevice");
    }

    [Fact]
    public async Task GenerateKernelWithProfiling_ShouldInstrumentCode()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "ProfiledKernel",
            SourceCode = "result[idx] = sqrt(data[idx]);",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            EnableProfiling = true,
            ProfilingLevel = ProfilingLevel.Detailed
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.CudaCode.Should().Contain("cudaEvent");
        result.CudaCode.Should().Contain("cudaEventRecord");
        result.ProfilingInfo.Should().NotBeNull();
        result.ProfilingInfo.InstrumentationPoints.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateKernelWithDebugInfo_ShouldIncludeDebugSymbols()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "DebugKernel",
            SourceCode = @"
                int idx = threadIdx.x + blockIdx.x * blockDim.x;
                if (idx < length) {
                    result[idx] = data[idx] * 2.0f;
                }",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) },
                new KernelParameter { Name = "length", Type = typeof(int) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            EnableDebugInfo = true,
            GenerateLineInfo = true
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.DebugInfo.Should().NotBeNull();
        result.DebugInfo.HasLineNumberInfo.Should().BeTrue();
        result.CompilationFlags.Should().Contain("-g");
        result.CompilationFlags.Should().Contain("-lineinfo");
    }

    [Fact]
    public async Task GenerateKernelConcurrently_ShouldBeThreadSafe()
    {
        // Arrange
        var kernelDefinitions = Enumerable.Range(0, 10)
            .Select(i => new KernelDefinition
            {
                Name = $"ConcurrentKernel{i}",
                SourceCode = $"result[idx] = data[idx] * {i}.0f;",
                Parameters = new[]
                {
                    new KernelParameter { Name = "data", Type = typeof(float[]) },
                    new KernelParameter { Name = "result", Type = typeof(float[]) }
                },
                TargetBackend = ComputeBackend.GPU
            })
            .ToArray();

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            EnableCaching = true
        };

        // Act
        var tasks = kernelDefinitions
            .Select(def => _kernelGenerator.GenerateKernelAsync(def, options))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.Success);
        
        // Verify each kernel has unique content
        var uniqueNames = results.Select(r => r.KernelName).Distinct().ToArray();
        uniqueNames.Should().HaveCount(10);
    }

    [Fact]
    public async Task GenerateKernelWithCustomAttributes_ShouldApplyAttributes()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "AttributedKernel",
            SourceCode = "result[idx] = data[idx];",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) }
            },
            TargetBackend = ComputeBackend.GPU,
            Attributes = new Dictionary<string, object>
            {
                ["__launch_bounds__"] = "(256, 4)",
                ["__restrict__"] = true,
                ["MaxRegistersPerThread"] = 32
            }
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            RespectAttributes = true
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.CudaCode.Should().Contain("__launch_bounds__(256, 4)");
        result.CudaCode.Should().Contain("__restrict__");
        result.CompilationFlags.Should().Contain("-maxrregcount=32");
    }

    [Fact]
    public async Task GenerateKernelWithMemoryPinning_ShouldUsePinnedMemory()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "PinnedMemoryKernel",
            SourceCode = "result[idx] = data[idx] * 2.0f;",
            Parameters = new[]
            {
                new KernelParameter 
                { 
                    Name = "data", 
                    Type = typeof(float[]),
                    MemoryType = MemoryType.PinnedHost
                },
                new KernelParameter 
                { 
                    Name = "result", 
                    Type = typeof(float[]),
                    MemoryType = MemoryType.Device
                }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            OptimizeMemoryTransfers = true
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.MemoryOptimizations.Should().Contain("PinnedHostMemory");
        result.TransferOptimizations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateKernelWithCaching_ShouldCacheResults()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "CachedKernel",
            SourceCode = "result[idx] = data[idx] + 1.0f;",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = "8.9",
            EnableCaching = true
        };

        // Act
        var result1 = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);
        var result2 = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.CacheKey.Should().Be(result2.CacheKey);
        result2.FromCache.Should().BeTrue();
        result2.CompilationTime.Should().BeLessThan(result1.CompilationTime);
    }

    [Theory]
    [InlineData("3.5", false, false)] // Older GPU, basic features
    [InlineData("7.0", true, false)]  // Newer GPU, tensor cores not available
    [InlineData("8.9", true, true)]   // Latest GPU, all features
    public async Task GenerateKernelForDifferentGpuGenerations_ShouldAdaptFeatures(
        string computeCapability, bool expectSharedMemoryOpt, bool expectTensorCoreOpt)
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "AdaptiveKernel",
            SourceCode = "result[idx] = data[idx] * scale;",
            Parameters = new[]
            {
                new KernelParameter { Name = "data", Type = typeof(float[]) },
                new KernelParameter { Name = "result", Type = typeof(float[]) },
                new KernelParameter { Name = "scale", Type = typeof(float) }
            },
            TargetBackend = ComputeBackend.GPU
        };

        var options = new GpuCompilationOptions
        {
            ComputeCapability = computeCapability,
            EnableSharedMemory = true,
            EnableTensorCores = true,
            OptimizationLevel = OptimizationLevel.Aggressive
        };

        // Act
        var result = await _kernelGenerator.GenerateKernelAsync(kernelDefinition, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        if (expectSharedMemoryOpt)
        {
            result.OptimizationsApplied.Should().Contain(o => o.Contains("SharedMemory"));
        }
        
        if (expectTensorCoreOpt)
        {
            result.OptimizationsApplied.Should().Contain(o => o.Contains("TensorCore"));
        }
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}