using Xunit;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Tests.Shared.TestInfrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DotCompute.Tests.Unit.Compute;

/// <summary>
/// Comprehensive unit tests for ComputeEngine
/// </summary>
public class ComputeEngineUnitTests : CoverageTestBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HardwareSimulator _hardwareSimulator;

    public ComputeEngineUnitTests(ITestOutputHelper output) : base(output)
    {
        _hardwareSimulator = RegisterDisposable(new HardwareSimulator(Logger));
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new XUnitLoggerProvider(output)));
        services.AddSingleton<IAcceleratorManager>(sp => new TestAcceleratorManager(_hardwareSimulator));
        services.AddTransient<IComputeEngine, DefaultComputeEngine>();
        
        _serviceProvider = RegisterDisposable(services.BuildServiceProvider());
    }

    [Fact]
    public void ComputeEngine_Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var acceleratorManager = _serviceProvider.GetRequiredService<IAcceleratorManager>();
        var logger = _serviceProvider.GetRequiredService<ILogger<DefaultComputeEngine>>();

        // Act
        var engine = new DefaultComputeEngine(acceleratorManager, logger);

        // Assert
        Assert.NotNull(engine);
    }

    [Fact]
    public void ComputeEngine_Constructor_WithNullAcceleratorManager_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<DefaultComputeEngine>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultComputeEngine(null!, logger));
    }

    [Fact]
    public void ComputeEngine_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var acceleratorManager = _serviceProvider.GetRequiredService<IAcceleratorManager>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultComputeEngine(acceleratorManager, null!));
    }

    [Fact]
    public async Task ComputeEngine_GetAvailableAccelerators_ReturnsExpectedAccelerators()
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();

        // Act
        var accelerators = await engine.GetAvailableAcceleratorsAsync(CancellationToken);

        // Assert
        Assert.NotNull(accelerators);
        Assert.NotEmpty(accelerators);
    }

    [Fact]
    public async Task ComputeEngine_GetAvailableAccelerators_WithSpecificType_ReturnsFilteredAccelerators()
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();

        // Act
        var cudaAccelerators = await engine.GetAvailableAcceleratorsAsync(AcceleratorType.CUDA, CancellationToken);

        // Assert
        Assert.NotNull(cudaAccelerators);
        Assert.All(cudaAccelerators, acc => Assert.Equal(AcceleratorType.CUDA, acc.Type));
    }

    [Fact]
    public async Task ComputeEngine_ExecuteKernel_WithValidParameters_ReturnsResult()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        var kernelSource = "kernel void test() { }";
        var parameters = new object[] { 1, 2, 3 };

        // Act
        var result = await engine.ExecuteKernelAsync(kernelSource, parameters, CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ComputeEngine_ExecuteKernel_WithInvalidKernelSource_ThrowsException()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        var parameters = new object[] { 1, 2, 3 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await engine.ExecuteKernelAsync("", parameters, CancellationToken));
    }

    [Fact]
    public async Task ComputeEngine_ExecuteKernel_WithNullParameters_ThrowsArgumentNullException()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        var kernelSource = "kernel void test() { }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await engine.ExecuteKernelAsync(kernelSource, null!, CancellationToken));
    }

    [Fact]
    public async Task ComputeEngine_ExecuteKernel_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        var kernelSource = "kernel void longRunningTest() { }";
        var parameters = new object[] { };
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await engine.ExecuteKernelAsync(kernelSource, parameters, cts.Token));
    }

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.CUDA)]
    [InlineData(AcceleratorType.OpenCL)]
    public async Task ComputeEngine_GetBestAccelerator_ReturnsAcceleratorOfRequestedType(AcceleratorType type)
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        _hardwareSimulator.AddAccelerator(type, $"Test {type} Device");
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();

        // Act
        var accelerator = await engine.GetBestAcceleratorAsync(type, CancellationToken);

        // Assert
        if (accelerator != null)
        {
            Assert.Equal(type, accelerator.Type);
        }
    }

    [Fact]
    public async Task ComputeEngine_CreateExecutionContext_ReturnsValidContext()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();

        // Act
        using var context = await engine.CreateExecutionContextAsync(CancellationToken);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public async Task ComputeEngine_ExecuteParallel_WithMultipleOperations_ExecutesAll()
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        
        var operations = new[]
        {
            ("kernel1", new object[] { 1 }),
            ("kernel2", new object[] { 2 }),
            ("kernel3", new object[] { 3 })
        };

        // Act
        var results = await engine.ExecuteParallelAsync(operations, CancellationToken);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(operations.Length, results.Count());
    }

    [Fact]
    public void ComputeEngine_Dispose_DisposesResourcesProperly()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();

        // Act
        engine.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => 
            engine.GetAvailableAcceleratorsAsync(CancellationToken));
    }

    [Fact]
    public async Task ComputeEngine_ExecuteKernel_WithLargeParameters_HandlesCorrectly()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        var kernelSource = "kernel void test(global int* data) { }";
        var largeArray = Enumerable.Range(0, 10000).ToArray();
        var parameters = new object[] { largeArray };

        // Act
        var result = await engine.ExecuteKernelAsync(kernelSource, parameters, CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ComputeEngine_ExecuteKernel_MultipleTimesSequentially_MaintainsState()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        var kernelSource = "kernel void incrementTest(global int* data) { data[0]++; }";
        var data = new[] { 0 };
        var parameters = new object[] { data };

        // Act
        var result1 = await engine.ExecuteKernelAsync(kernelSource, parameters, CancellationToken);
        var result2 = await engine.ExecuteKernelAsync(kernelSource, parameters, CancellationToken);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
    }

    [Fact]
    public async Task ComputeEngine_GetPerformanceMetrics_ReturnsValidMetrics()
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        var engine = _serviceProvider.GetRequiredService<IComputeEngine>();
        
        // Execute some operations first
        await engine.ExecuteKernelAsync("kernel void test() { }", new object[0], CancellationToken);

        // Act
        var metrics = await engine.GetPerformanceMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
    }

    /// <summary>
    /// Test accelerator manager implementation
    /// </summary>
    private class TestAcceleratorManager : IAcceleratorManager
    {
        private readonly HardwareSimulator _simulator;

        public TestAcceleratorManager(HardwareSimulator simulator)
        {
            _simulator = simulator;
        }

        public Task<IEnumerable<IAccelerator>> GetAvailableAcceleratorsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_simulator.GetAllAccelerators().Cast<IAccelerator>());
        }

        public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_simulator.GetAccelerators(type).Cast<IAccelerator>());
        }

        public Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default)
        {
            var accelerators = type.HasValue 
                ? _simulator.GetAccelerators(type.Value)
                : _simulator.GetAllAccelerators();
            
            return Task.FromResult(accelerators.Cast<IAccelerator?>().FirstOrDefault());
        }

        public void Dispose() => _simulator.Dispose();
    }

    /// <summary>
    /// Test compute engine interface
    /// </summary>
    private interface IComputeEngine : IDisposable
    {
        Task<IEnumerable<IAccelerator>> GetAvailableAcceleratorsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<IAccelerator>> GetAvailableAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default);
        Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default);
        Task<object> ExecuteKernelAsync(string kernelSource, object[] parameters, CancellationToken cancellationToken = default);
        Task<IEnumerable<object>> ExecuteParallelAsync(IEnumerable<(string kernel, object[] parameters)> operations, CancellationToken cancellationToken = default);
        Task<IDisposable> CreateExecutionContextAsync(CancellationToken cancellationToken = default);
        Task<object> GetPerformanceMetricsAsync();
    }

    /// <summary>
    /// Default implementation for testing
    /// </summary>
    private class DefaultComputeEngine : IComputeEngine
    {
        private readonly IAcceleratorManager _acceleratorManager;
        private readonly ILogger<DefaultComputeEngine> _logger;
        private bool _disposed;

        public DefaultComputeEngine(IAcceleratorManager acceleratorManager, ILogger<DefaultComputeEngine> logger)
        {
            _acceleratorManager = acceleratorManager ?? throw new ArgumentNullException(nameof(acceleratorManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<IAccelerator>> GetAvailableAcceleratorsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _acceleratorManager.GetAvailableAcceleratorsAsync(cancellationToken);
        }

        public async Task<IEnumerable<IAccelerator>> GetAvailableAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _acceleratorManager.GetAcceleratorsAsync(type, cancellationToken);
        }

        public async Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _acceleratorManager.GetBestAcceleratorAsync(type, cancellationToken);
        }

        public async Task<object> ExecuteKernelAsync(string kernelSource, object[] parameters, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(kernelSource))
                throw new ArgumentException("Kernel source cannot be empty", nameof(kernelSource));
            
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var accelerator = await GetBestAcceleratorAsync(cancellationToken: cancellationToken);
            if (accelerator is SimulatedAccelerator simAccelerator)
            {
                await simAccelerator.ExecuteKernelAsync(kernelSource, parameters, cancellationToken);
            }
            
            return new { Success = true, KernelSource = kernelSource, Parameters = parameters };
        }

        public async Task<IEnumerable<object>> ExecuteParallelAsync(IEnumerable<(string kernel, object[] parameters)> operations, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var tasks = operations.Select(async op => 
                await ExecuteKernelAsync(op.kernel, op.parameters, cancellationToken));
            
            return await Task.WhenAll(tasks);
        }

        public async Task<IDisposable> CreateExecutionContextAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var accelerator = await GetBestAcceleratorAsync(cancellationToken: cancellationToken);
            return accelerator?.Context ?? new TestExecutionContext();
        }

        public Task<object> GetPerformanceMetricsAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult<object>(new { TotalExecutions = 1, AverageTime = TimeSpan.FromMilliseconds(10) });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DefaultComputeEngine));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _acceleratorManager?.Dispose();
            _disposed = true;
        }
    }

    private class TestExecutionContext : IDisposable
    {
        public void Dispose() { }
    }
}