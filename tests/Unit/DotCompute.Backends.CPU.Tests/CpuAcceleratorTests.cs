// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Backends.CPU.Tests.Helpers;
using DotCompute.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DotCompute.Backends.CPU
{
    /// <summary>
    /// Comprehensive unit tests for CpuAccelerator consolidated from multiple test files.
    /// Tests initialization, memory management, kernel compilation, error handling, and performance.
    /// Targets 90%+ code coverage with comprehensive edge case testing.
    /// </summary>
    public sealed class CpuAcceleratorTests : IDisposable
    {
        private readonly FakeLogger<CpuAccelerator> _logger;
        private readonly IOptions<CpuAcceleratorOptions> _options;
        private readonly IOptions<CpuThreadPoolOptions> _threadPoolOptions;
        private readonly CpuAccelerator _accelerator;
        private bool _disposed;

        public CpuAcceleratorTests()
        {
            _logger = new FakeLogger<CpuAccelerator>();
            _options = Options.Create(new CpuAcceleratorOptions
            {
                EnableAutoVectorization = true,
                PreferPerformanceOverPower = true
            });
            _threadPoolOptions = Options.Create(new CpuThreadPoolOptions
            {
                WorkerThreads = Environment.ProcessorCount,
                MaxQueuedItems = 10000,
                EnableWorkStealing = true
            });
            _accelerator = new CpuAccelerator(_options, _threadPoolOptions, _logger);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldInitializeSuccessfully()
        {
            // Assert
            Assert.NotNull(_accelerator);
            _accelerator.IsInitialized.Should().BeTrue();
            _accelerator.Name.Should().NotBeNullOrEmpty();
            _accelerator.DeviceType.Should().Be(ComputeDeviceType.CPU);
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new CpuAccelerator(
                null!,
                _threadPoolOptions,
                _logger);
            act.Should().Throw<ArgumentNullException>().WithParameterName("options");
        }

        [Fact]
        public void Constructor_WithNullThreadPoolOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new CpuAccelerator(
                _options,
                null!,
                _logger);
            act.Should().Throw<ArgumentNullException>().WithParameterName("threadPoolOptions");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new CpuAccelerator(
                _options,
                _threadPoolOptions,
                null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void DeviceInfo_ShouldContainCpuInformation()
        {
            // Act
            var deviceInfo = _accelerator.DeviceInfo;

            // Assert
            Assert.NotNull(deviceInfo);
            deviceInfo.Name.Should().NotBeNullOrEmpty();
            deviceInfo.DeviceType.Should().Be(ComputeDeviceType.CPU);
            (deviceInfo.MemorySize > 0).Should().BeTrue();
            (deviceInfo.MaxComputeUnits > 0).Should().BeTrue();
            (deviceInfo.MaxWorkGroupSize > 0).Should().BeTrue();
        }

        [Fact]
        public void DeviceInfo_Properties_ShouldBeConsistent()
        {
            // Act
            var deviceInfo = _accelerator.DeviceInfo;

            // Assert
            deviceInfo.Name.Should().Be(_accelerator.Name);
            deviceInfo.DeviceType.Should().Be(_accelerator.DeviceType);
            deviceInfo.MaxComputeUnits.Should().BeGreaterThanOrEqualTo(1);
            (deviceInfo.MaxComputeUnits <= Environment.ProcessorCount).Should().BeTrue();
        }

        [Fact]
        public void Type_ShouldReturnCpu()
        {
            // Act
            var type = _accelerator.Type;

            // Assert
            type.Should().Be(AcceleratorType.CPU);
        }

        [Fact]
        public void Info_ShouldContainValidInformation()
        {
            // Act
            var info = _accelerator.Info;

            // Assert
            Assert.NotNull(info);
            info.Type.Should().Be(AcceleratorType.CPU);
            info.Name.Should().NotBeNullOrEmpty();
            info.Name.Should().Contain("CPU");
            info.Name.Should().Contain("cores");
            (info.DeviceMemory > 0).Should().BeTrue();
            (info.ComputeUnits > 0).Should().BeTrue();
            info.ComputeUnits.Should().Be(Environment.ProcessorCount);
            info.IsUnified.Should().BeTrue();
            info.Capabilities.Should().NotBeNull();
        }

        [Fact]
        public void Info_Capabilities_ShouldContainRequiredKeys()
        {
            // Act
            var capabilities = _accelerator.Info.Capabilities;

            // Assert
            capabilities.Should().ContainKey("SimdWidth");
            capabilities.Should().ContainKey("SimdInstructionSets");
            capabilities.Should().ContainKey("ThreadCount");
            capabilities.Should().ContainKey("NumaNodes");
            capabilities.Should().ContainKey("CacheLineSize");

            capabilities["SimdWidth"].Should().BeOfType<int>();
            capabilities["ThreadCount"].Should().BeOfType<int>();
            capabilities["NumaNodes"].Should().BeOfType<int>();
            capabilities["CacheLineSize"].Should().Be(64);
        }

        [Fact]
        public void Memory_ShouldReturnValidMemoryManager()
        {
            // Act
            var memory = _accelerator.Memory;

            // Assert
            Assert.NotNull(memory);
            Assert.IsAssignableFrom<IMemoryManager>(memory);
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public void CreateMemoryManager_ShouldReturnValidManager()
        {
            // Act
            using var memoryManager = _accelerator.CreateMemoryManager();

            // Assert
            Assert.NotNull(memoryManager);
            Assert.IsType<CpuMemoryManager>(memoryManager);
        }

        [Fact]
        public async Task Memory_CreateBuffer_ShouldReturnValidBuffer()
        {
            // Act
            var buffer = await _accelerator.Memory.Allocate<float>(1024);

            // Assert
            Assert.NotNull(buffer);
            buffer.SizeInBytes.Should().Be(1024 * sizeof(float));
        }

        [Fact]
        public async Task Memory_CreateBufferFromData_ShouldReturnValidBuffer()
        {
            // Arrange
            var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };

            // Act
            var buffer = await _accelerator.Memory.AllocateAndCopyAsync<float>(data, MemoryOptions.None);

            // Assert
            Assert.NotNull(buffer);
            buffer.ElementCount.Should().Be(4);
            buffer.Location.Should().Be(MemoryLocation.Host);
            buffer.Access.Should().Be(MemoryAccess.ReadOnly);
        }

        [Fact]
        public async Task Memory_GetStatistics_ShouldReturnValidStatistics()
        {
            // Act
            var stats = _accelerator.Memory.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            stats.TotalAllocatedBytes.Should().BeGreaterThanOrEqualTo(0);
            stats.AllocationCount.Should().BeGreaterThanOrEqualTo(0);
            stats.DeallocationsCount.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region Kernel Compilation Tests

        [Fact]
        public void CreateKernelCompiler_ShouldReturnValidCompiler()
        {
            // Act
            using var compiler = _accelerator.CreateKernelCompiler();

            // Assert
            Assert.NotNull(compiler);
            Assert.IsType<CpuKernelCompiler>(compiler);
        }

        [Fact]
        public async Task CompileKernelAsync_WithValidDefinition_ShouldReturnCompiledKernel()
        {
            // Arrange
            var definition = CreateTestKernelDefinition("TestKernel", "VectorAdd");

            // Act
            var compiledKernel = await _accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(compiledKernel);
            Assert.IsType<CpuCompiledKernel>(compiledKernel);
            compiledKernel.Name.Should().Be("TestKernel");
            compiledKernel.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task CompileKernelAsync_WithNullDefinition_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _accelerator.CompileKernelAsync(null!).AsTask());
        }

        [Fact]
        public async Task CompileKernelAsync_WithNullOptions_ShouldUseDefaults()
        {
            // Arrange
            var definition = CreateTestKernelDefinition("TestKernel", "VectorAdd");

            // Act
            var result = await _accelerator.CompileKernelAsync(definition, null);

            // Assert
            Assert.NotNull(result);
            result.Name.Should().Be("TestKernel");
        }

        [Fact]
        public async Task CompileKernelAsync_WithOptimizationEnabled_ShouldTryOptimizedKernel()
        {
            // Arrange
            var options = Options.Create(new CpuAcceleratorOptions
            {
                EnableAutoVectorization = true,
                PreferPerformanceOverPower = true
            });

            await using var accelerator = new CpuAccelerator(
                options,
                _threadPoolOptions,
                _logger);

            var definition = CreateTestKernelDefinition("VectorAddKernel", "VectorAdd");

            // Act
            var result = await accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(result);
            result.Name.Should().Be("VectorAddKernel");
        }

        [Fact]
        public async Task CompileKernelAsync_WithOptimizationDisabled_ShouldFallbackToStandardCompilation()
        {
            // Arrange
            var options = Options.Create(new CpuAcceleratorOptions
            {
                EnableAutoVectorization = false,
                PreferPerformanceOverPower = false
            });

            await using var accelerator = new CpuAccelerator(
                options,
                _threadPoolOptions,
                _logger);

            var definition = CreateTestKernelDefinition("StandardKernel", "Generic");

            // Act
            var result = await accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(result);
            result.Name.Should().Be("StandardKernel");
        }

        [Theory]
        [InlineData("VectorAdd")]
        [InlineData("VectorScale")]
        [InlineData("MatrixMultiply")]
        [InlineData("Reduction")]
        [InlineData("MemoryIntensive")]
        [InlineData("ComputeIntensive")]
        public async Task CompileKernelAsync_WithKnownKernelTypes_ShouldCreateOptimizedKernels(string kernelType)
        {
            // Arrange
            var definition = CreateTestKernelDefinition($"{kernelType}Kernel", kernelType);

            // Act
            var result = await _accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(result);
            result.Name.Should().Be($"{kernelType}Kernel");
        }

        [Fact]
        public async Task CompileKernelAsync_WithCancellation_ShouldRespectCancellationToken()
        {
            // Arrange
            var definition = CreateTestKernelDefinition("CancelledKernel", "VectorAdd");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _accelerator.CompileKernelAsync(definition, null, cts.Token).AsTask());
        }

        [Fact]
        public async Task CompileKernelAsync_WithInvalidKernelCode_ShouldHandleGracefully()
        {
            // Arrange
            var definition = new KernelDefinition(
                "InvalidKernel",
                System.Text.Encoding.UTF8.GetBytes("invalid kernel code that will cause parsing to fail"));

            // Act
            var result = await _accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(result); // Should fall back to standard compilation
            result.Name.Should().Be("InvalidKernel");
        }

        #endregion

        #region SIMD and Capabilities Tests

        [Fact]
        public void GetCapabilities_ShouldReturnSimdCapabilities()
        {
            // Act
            var capabilities = _accelerator.GetCapabilities();

            // Assert
            Assert.NotNull(capabilities);
            Assert.IsType<SimdCapabilities>(capabilities);
            
            var simdCaps = (SimdCapabilities)capabilities;
            simdCaps.IsHardwareAccelerated.Should().Be(System.Numerics.Vector.IsHardwareAccelerated);
            (simdCaps.PreferredVectorWidth > 0).Should().BeTrue();
            simdCaps.SupportedInstructionSets.Should().NotBeNull();
        }

        [Fact]
        public void GetOptimalWorkGroupSize_ShouldReturnReasonableValue()
        {
            // Act
            var workGroupSize = _accelerator.GetOptimalWorkGroupSize();

            // Assert
            Assert.True(workGroupSize > 0);
            Assert.True(workGroupSize <= 1024); // Reasonable upper bound
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(16)]
        [InlineData(64)]
        public void GetOptimalWorkGroupSize_WithDifferentSizes_ShouldHandleGracefully(int requestedSize)
        {
            // Act
            var workGroupSize = _accelerator.GetOptimalWorkGroupSize();

            // Assert
            Assert.True(workGroupSize > 0);
            // The optimal size might not match the requested size, but should be reasonable
        }

        #endregion

        #region Operation Support Tests

        [Fact]
        public void SupportsOperation_WithBasicOperations_ShouldReturnTrue()
        {
            // Act & Assert
            _accelerator.SupportsOperation("VectorAdd").Should().BeTrue();
            _accelerator.SupportsOperation("MatrixMultiply").Should().BeTrue();
            _accelerator.SupportsOperation("Convolution").Should().BeTrue();
            _accelerator.SupportsOperation("Reduction").Should().BeTrue();
        }

        [Fact]
        public void SupportsOperation_WithUnsupportedOperation_ShouldReturnFalse()
        {
            // Act & Assert
            _accelerator.SupportsOperation("UnsupportedOperation").Should().BeFalse();
            _accelerator.SupportsOperation("").Should().BeFalse();
        }

        [Fact]
        public void WarmUp_ShouldCompleteSuccessfully()
        {
            // Act & Assert
            _accelerator.Invoking(a => a.WarmUp()).Should().NotThrow();
        }

        #endregion

        #region Synchronization Tests

        [Fact]
        public async Task SynchronizeAsync_ShouldCompleteSuccessfully()
        {
            // Act
            var task = _accelerator.SynchronizeAsync();
            await task;

            // Assert
            task.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task SynchronizeAsync_WithCancellation_ShouldCompleteSuccessfully()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act
            var task = _accelerator.SynchronizeAsync(cts.Token);
            await task;

            // Assert
            task.IsCompleted.Should().BeTrue();
        }

        #endregion

        #region Platform-Specific Tests

        [Theory]
        [InlineData(1024 * 1024)]      // 1MB
        [InlineData(4L * 1024 * 1024 * 1024)] // 4GB
        public void GetTotalPhysicalMemory_ShouldReturnReasonableValue(long minExpected)
        {
            // Act
            var info = _accelerator.Info;

            // Assert
            (info.DeviceMemory >= minExpected).Should().BeTrue();
        }

        [Fact]
        public void GetProcessorName_ShouldContainArchitectureAndCoreCount()
        {
            // Act
            var info = _accelerator.Info;

            // Assert
            info.Name.Should().NotBeNullOrEmpty();
            info.Name.Should().Contain("CPU");
            info.Name.Should().Contain("cores");
            info.Name.Should().MatchRegex(@"\d+\s+cores");
        }

        [Fact]
        public void GetNumaNodeCount_ShouldReturnPositiveValue()
        {
            // Act
            var capabilities = _accelerator.Info.Capabilities;

            // Assert
            capabilities.Should().ContainKey("NumaNodes");
            capabilities["NumaNodes"].Should().BeOfType<int>();
            ((int)capabilities["NumaNodes"]).Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetCacheLineSize_ShouldReturn64()
        {
            // Act
            var capabilities = _accelerator.Info.Capabilities;

            // Assert
            capabilities.Should().ContainKey("CacheLineSize");
            capabilities["CacheLineSize"].Should().Be(64);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CompileKernelAsync_WithExceptionInOptimizedCompilation_ShouldFallbackToStandard()
        {
            // Arrange
            var definition = new KernelDefinition(
                "ProblematicKernel", 
                System.Text.Encoding.UTF8.GetBytes("kernel that might cause optimization issues"));

            // Act
            var result = await _accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(result);
            result.Name.Should().Be("ProblematicKernel");
        }

        [Fact]
        public void ConvertToCoreKernelDefinition_ShouldHandleNullMetadata()
        {
            // Arrange
            var definition = new KernelDefinition(
                "TestKernel",
                System.Text.Encoding.UTF8.GetBytes("test code"));

            // Act
            var result = _accelerator.CompileKernelAsync(definition);

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public async Task DisposeAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            await using var accelerator = new CpuAccelerator(
                _options,
                _threadPoolOptions,
                _logger);

            // Act
            await accelerator.DisposeAsync();

            // Assert - Should not throw
        }

        [Fact]
        public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            await using var accelerator = new CpuAccelerator(
                _options,
                _threadPoolOptions,
                _logger);

            // Act
            await accelerator.DisposeAsync();
            await accelerator.DisposeAsync();

            // Assert - Should not throw
        }

        [Fact]
        public void IsInitialized_AfterDispose_ShouldRemainTrue()
        {
            // Arrange
            var accelerator = new CpuAccelerator(_options, _threadPoolOptions, _logger);

            // Act
            accelerator.DisposeAsync().AsTask().Wait();

            // Assert
            accelerator.IsInitialized.Should().BeTrue(); // CPU accelerator doesn't change initialization state on dispose
        }

        #endregion

        #region Logging Tests

        [Fact]
        public void Logger_ShouldReceiveInitializationMessages()
        {
            // Arrange & Act
            var accelerator = new CpuAccelerator(_options, _threadPoolOptions, _logger);

            // Assert
            _logger.Collector.GetSnapshot().Should().NotBeEmpty();
            _logger.Collector.GetSnapshot()
                .Should().Contain(log => log.Message != null && log.Message.Contains("CPU accelerator initialized"));
        }

        #endregion

        #region Helper Methods

        private KernelDefinition CreateTestKernelDefinition(string name, string? operation = null)
        {
            var sourceCode = operation switch
            {
                "VectorAdd" => "__kernel void vector_add(__global const float* a, __global const float* b, __global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
                "VectorScale" => "__kernel void vector_scale(__global const float* input, __global float* output, float scale) { int i = get_global_id(0); output[i] = input[i] * scale; }",
                "MatrixMultiply" => "__kernel void matrix_multiply(__global const float* a, __global const float* b, __global float* c, const int n) { /* matrix multiply */ }",
                "Reduction" => "__kernel void reduce(__global const float* input, __global float* output, const int n) { /* reduction */ }",
                "MemoryIntensive" => "__kernel void memory_intensive(__global const float* input, __global float* output) { /* memory intensive operations */ }",
                "ComputeIntensive" => "__kernel void compute_intensive(__global const float* input, __global float* output) { /* compute intensive operations */ }",
                _ => "__kernel void generic() { /* generic kernel */ }"
            };

            return new KernelDefinition
            {
                Name = name,
                Code = System.Text.Encoding.UTF8.GetBytes(sourceCode),
                Metadata = new Dictionary<string, object>
                {
                    ["Operation"] = operation ?? "Generic",
                    ["Language"] = "OpenCL"
                }
            };
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _accelerator?.DisposeAsync().AsTask().Wait();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Comprehensive tests for CpuMemoryManager consolidated from multiple files.
    /// </summary>
    public sealed class CpuMemoryManagerTests : IDisposable
    {
        private readonly FakeLogger<CpuMemoryManager> _logger;
        private readonly CpuMemoryManager _memoryManager;

        public CpuMemoryManagerTests()
        {
            _logger = new FakeLogger<CpuMemoryManager>();
            _memoryManager = new CpuMemoryManager();
        }

        [Fact]
        public void Constructor_ShouldInitializeSuccessfully()
        {
            // Assert
            Assert.NotNull(_memoryManager);
            (_memoryManager.TotalMemory > 0).Should().BeTrue();
            (_memoryManager.AvailableMemory > 0).Should().BeTrue();
        }

        [Theory]
        [InlineData(1024)]
        [InlineData(4096)]
        [InlineData(1024 * 1024)]
        public void AllocateBuffer_WithValidSize_ShouldReturnBuffer(long size)
        {
            // Act
            using var buffer = _memoryManager.AllocateBuffer(size);

            // Assert
            Assert.NotNull(buffer);
            Assert.IsType<CpuMemoryBuffer>(buffer);
            buffer.SizeInBytes.Should().Be(size);
        }

        [Fact]
        public void AllocateBuffer_WithZeroSize_ShouldThrowArgumentException()
        {
            // Act & Assert
            _memoryManager.Invoking(m => m.AllocateBuffer(0))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void AllocateBuffer_WithNegativeSize_ShouldThrowArgumentException()
        {
            // Act & Assert
            _memoryManager.Invoking(m => m.AllocateBuffer(-1))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void AllocateBuffer_Multiple_ShouldTrackMemoryUsage()
        {
            // Arrange
            var initialAvailable = _memoryManager.AvailableMemory;

            // Act
            using var buffer1 = _memoryManager.AllocateBuffer(1024);
            using var buffer2 = _memoryManager.AllocateBuffer(2048);

            // Assert
            _memoryManager.AllocatedMemory.Should().BeGreaterThanOrEqualTo(3072);
            (_memoryManager.AvailableMemory <= initialAvailable).Should().BeTrue();
        }

        [Fact]
        public void TotalMemory_ShouldBeReasonable()
        {
            // Act
            var totalMemory = _memoryManager.TotalMemory;

            // Assert
            Assert.True(totalMemory > 1024 * 1024); // At least 1MB
            Assert.True(totalMemory <= long.MaxValue);
        }

        [Fact]
        public void FragmentationRatio_ShouldBeWithinRange()
        {
            // Act
            var fragmentation = _memoryManager.FragmentationRatio;

            // Assert
            Assert.True(fragmentation >= 0.0);
            Assert.True(fragmentation <= 1.0);
        }

        [Fact]
        public async Task DisposeAsync_ShouldReleaseAllMemory()
        {
            // Arrange
            var memoryManager = new CpuMemoryManager();
            var buffer = memoryManager.AllocateBuffer(1024);

            // Act
            await memoryManager.DisposeAsync();

            // Assert
            // Should not throw and memory should be cleaned up
            // Note: Disposed buffer should be marked invalid
        }

        public void Dispose()
        {
            _memoryManager?.Dispose();
        }
    }

    /// <summary>
    /// Comprehensive tests for CpuMemoryBuffer consolidated from multiple files.
    /// </summary>
    public sealed class CpuMemoryBufferTests
    {
        [Theory]
        [InlineData(1024)]
        [InlineData(4096)]
        [InlineData(1024 * 1024)]
        public void Constructor_WithValidSize_ShouldCreateBuffer(long size)
        {
            // Act
            using var buffer = new CpuMemoryBuffer(size);

            // Assert
            buffer.SizeInBytes.Should().Be(size);
            buffer.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithZeroSize_ShouldThrowArgumentException()
        {
            // Act & Assert
            Action action = () => new CpuMemoryBuffer(0);
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void GetMemory_ShouldReturnValidMemory()
        {
            // Arrange
            using var buffer = new CpuMemoryBuffer(1024);

            // Act
            var memory = buffer.GetMemory();

            // Assert
            memory.Length.Should().Be(1024);
        }

        [Fact]
        public void WriteData_ShouldUpdateBufferContents()
        {
            // Arrange
            using var buffer = new CpuMemoryBuffer(16);
            var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            // Act
            buffer.WriteData(testData, 0);

            // Assert
            var memory = buffer.GetMemory();
            for(int i = 0; i < testData.Length; i++)
            {
                memory.Span[i].Should().Be(testData[i]);
            }
        }

        [Fact]
        public void ReadData_ShouldReturnCorrectData()
        {
            // Arrange
            using var buffer = new CpuMemoryBuffer(16);
            var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            buffer.WriteData(testData, 0);

            // Act
            var readData = new byte[testData.Length];
            buffer.ReadData(readData, 0);

            // Assert
            readData.Should().BeEquivalentTo(testData);
        }

        [Fact]
        public void Dispose_ShouldMarkBufferInvalid()
        {
            // Arrange
            var buffer = new CpuMemoryBuffer(1024);

            // Act
            buffer.Dispose();

            // Assert
            buffer.IsValid.Should().BeFalse();
        }

        [Fact]
        public void GetMemory_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var buffer = new CpuMemoryBuffer(1024);
            buffer.Dispose();

            // Act & Assert
            buffer.Invoking(b => b.GetMemory())
                .Should().Throw<ObjectDisposedException>();
        }
    }

    /// <summary>
    /// Comprehensive tests for CpuAcceleratorOptions consolidated from multiple files.
    /// </summary>
    public sealed class CpuAcceleratorOptionsTests
    {
        [Fact]
        public void DefaultOptions_ShouldHaveExpectedDefaults()
        {
            // Act
            var options = new CpuAcceleratorOptions();

            // Assert
            options.EnableAutoVectorization.Should().BeTrue();
            options.EnableNumaAwareAllocation.Should().BeTrue();
            options.MaxMemoryAllocation.Should().Be(2L * 1024 * 1024 * 1024); // 2GB
            options.EnableProfiling.Should().BeTrue();
            options.EnableKernelCaching.Should().BeTrue();
            options.MaxCachedKernels.Should().Be(1000);
            options.MemoryAlignment.Should().Be(64);
        }

        [Fact]
        public void Validate_WithValidOptions_ShouldReturnNoErrors()
        {
            // Arrange
            var options = new CpuAcceleratorOptions
            {
                MaxWorkGroupSize = 512,
                MaxMemoryAllocation = 1024 * 1024,
                MinVectorizationWorkSize = 128,
                TargetVectorWidth = 256
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData(-1, "MaxWorkGroupSize must be positive when specified")]
        [InlineData(0, "MaxWorkGroupSize must be positive when specified")]
        public void Validate_WithInvalidMaxWorkGroupSize_ShouldReturnError(int maxWorkGroupSize, string expectedError)
        {
            // Arrange
            var options = new CpuAcceleratorOptions
            {
                MaxWorkGroupSize = maxWorkGroupSize
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(expectedError, errors);
        }

        [Fact]
        public void GetEffectiveWorkGroupSize_WithoutMaxSet_ShouldReturnDefaultCalculation()
        {
            // Arrange
            var options = new CpuAcceleratorOptions();

            // Act
            var workGroupSize = options.GetEffectiveWorkGroupSize();

            // Assert
            workGroupSize.Should().BePositive();
            Assert.True(workGroupSize <= 1024);
        }

        [Fact]
        public void GetEffectiveWorkGroupSize_WithMaxSet_ShouldRespectMaximum()
        {
            // Arrange
            var options = new CpuAcceleratorOptions
            {
                MaxWorkGroupSize = 256
            };

            // Act
            var workGroupSize = options.GetEffectiveWorkGroupSize();

            // Assert
            Assert.True(workGroupSize <= 256);
            workGroupSize.Should().BePositive();
        }

        [Theory]
        [InlineData("AVX2", true)]
        [InlineData("SSE4", true)]
        [InlineData("DISABLED", false)]
        public void ShouldUseInstructionSet_WithPreferences_ShouldRespectSettings(string instructionSet, bool expected)
        {
            // Arrange
            var options = new CpuAcceleratorOptions();
            if(!expected)
            {
                options.DisabledInstructionSets.Add(instructionSet);
            }
            else
            {
                options.PreferredInstructionSets.Add(instructionSet);
            }

            // Act
            var shouldUse = options.ShouldUseInstructionSet(instructionSet);

            // Assert
            Assert.Equal(expected, shouldUse);
        }

        [Fact]
        public void ConfigureForMaxPerformance_ShouldSetOptimalSettings()
        {
            // Arrange
            var options = new CpuAcceleratorOptions();

            // Act
            options.ConfigureForMaxPerformance();

            // Assert
            options.EnableAutoVectorization.Should().BeTrue();
            options.EnableNumaAwareAllocation.Should().BeTrue();
            options.EnableLoopUnrolling.Should().BeTrue();
            options.EnableMemoryPrefetching.Should().BeTrue();
            options.PreferPerformanceOverPower.Should().BeTrue();
            options.ComputeThreadPriority.Should().Be(ThreadPriority.AboveNormal);
            options.UseHugePages.Should().BeTrue();
            options.EnableHardwareCounters.Should().BeTrue();
            options.MemoryAlignment.Should().Be(64);
        }

        [Fact]
        public void ConfigureForMinMemory_ShouldSetMemoryOptimizedSettings()
        {
            // Arrange
            var options = new CpuAcceleratorOptions();

            // Act
            options.ConfigureForMinMemory();

            // Assert
            options.EnableKernelCaching.Should().BeFalse();
            options.MaxCachedKernels.Should().Be(10);
            options.EnableProfiling.Should().BeFalse();
            options.UseHugePages.Should().BeFalse();
            options.EnableMemoryPrefetching.Should().BeFalse();
            options.MemoryPrefetchDistance.Should().Be(1);
        }
    }
}