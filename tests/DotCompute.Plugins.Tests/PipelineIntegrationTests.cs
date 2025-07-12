using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Core.Abstractions;
using DotCompute.Plugins;
using DotCompute.Plugins.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Plugins.Tests;

public class PipelineIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IComputeBackend> _backendMock;
    private readonly Mock<IPluginLoader> _loaderMock;
    private readonly PluginManager _pluginManager;

    public PipelineIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        
        _backendMock = new Mock<IComputeBackend>();
        _loaderMock = new Mock<IPluginLoader>();
        
        services.AddSingleton(_backendMock.Object);
        services.AddSingleton(_loaderMock.Object);
        services.AddSingleton<IPluginManager, PluginManager>();
        
        _serviceProvider = services.BuildServiceProvider();
        _pluginManager = _serviceProvider.GetRequiredService<IPluginManager>() as PluginManager;
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _pluginManager?.Dispose();
    }

    [Fact]
    public async Task Pipeline_LoadComputePlugin_IntegratesWithBackend()
    {
        // Arrange
        var computePlugin = CreateMockComputePlugin();
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(computePlugin.Object);
        
        var testData = new float[] { 1, 2, 3, 4, 5 };
        var memory = Mock.Of<IDeviceMemory>();
        _backendMock.Setup(b => b.AllocateMemory(It.IsAny<long>())).Returns(memory);

        // Act
        await _pluginManager.LoadPluginAsync("compute_plugin.dll");
        await _pluginManager.InitializeAsync();
        
        var plugin = _pluginManager.GetPlugin<IComputePlugin>();
        var result = await plugin.ProcessDataAsync(testData);

        // Assert
        plugin.Should().NotBeNull();
        result.Should().NotBeNull();
        computePlugin.Verify(p => p.ProcessDataAsync(testData), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ChainMultiplePlugins_ExecutesInOrder()
    {
        // Arrange
        var preprocessPlugin = CreateMockPreprocessPlugin();
        var computePlugin = CreateMockComputePlugin();
        var postprocessPlugin = CreateMockPostprocessPlugin();

        _loaderMock.SetupSequence(l => l.LoadPlugin(It.IsAny<string>()))
            .Returns(preprocessPlugin.Object)
            .Returns(computePlugin.Object)
            .Returns(postprocessPlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        var testData = new float[] { 1, 2, 3, 4, 5 };

        // Act
        await _pluginManager.LoadPluginAsync("preprocess.dll");
        await _pluginManager.LoadPluginAsync("compute.dll");
        await _pluginManager.LoadPluginAsync("postprocess.dll");
        await _pluginManager.InitializeAsync();

        pipeline.AddStage(_pluginManager.GetPlugin<IPreprocessPlugin>());
        pipeline.AddStage(_pluginManager.GetPlugin<IComputePlugin>());
        pipeline.AddStage(_pluginManager.GetPlugin<IPostprocessPlugin>());

        var result = await pipeline.ExecuteAsync(testData);

        // Assert
        result.Should().NotBeNull();
        preprocessPlugin.Verify(p => p.PreprocessAsync(testData), Times.Once);
        computePlugin.Verify(p => p.ProcessDataAsync(It.IsAny<float[]>()), Times.Once);
        postprocessPlugin.Verify(p => p.PostprocessAsync(It.IsAny<float[]>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_PluginThrowsException_HandlesCleanluy()
    {
        // Arrange
        var faultyPlugin = CreateMockComputePlugin();
        faultyPlugin.Setup(p => p.ProcessDataAsync(It.IsAny<float[]>()))
            .ThrowsAsync(new InvalidOperationException("Plugin error"));

        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(faultyPlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        var testData = new float[] { 1, 2, 3, 4, 5 };

        // Act
        await _pluginManager.LoadPluginAsync("faulty.dll");
        await _pluginManager.InitializeAsync();
        
        pipeline.AddStage(_pluginManager.GetPlugin<IComputePlugin>());

        // Assert
        var action = () => pipeline.ExecuteAsync(testData);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Plugin error");
    }

    [Fact]
    public async Task Pipeline_OptimizationPlugin_AppliesOptimizations()
    {
        // Arrange
        var optimizationPlugin = CreateMockOptimizationPlugin();
        var computePlugin = CreateMockComputePlugin();

        _loaderMock.SetupSequence(l => l.LoadPlugin(It.IsAny<string>()))
            .Returns(optimizationPlugin.Object)
            .Returns(computePlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        var testData = new float[] { 1, 2, 3, 4, 5 };

        // Act
        await _pluginManager.LoadPluginAsync("optimization.dll");
        await _pluginManager.LoadPluginAsync("compute.dll");
        await _pluginManager.InitializeAsync();

        var optimization = _pluginManager.GetPlugin<IOptimizationPlugin>();
        var compute = _pluginManager.GetPlugin<IComputePlugin>();

        pipeline.AddOptimization(optimization);
        pipeline.AddStage(compute);

        var result = await pipeline.ExecuteAsync(testData);

        // Assert
        optimizationPlugin.Verify(p => p.OptimizeAsync(It.IsAny<IPipelineContext>()), Times.Once);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Pipeline_HotReload_UpdatesRunningPipeline()
    {
        // Arrange
        var oldPlugin = CreateMockComputePlugin();
        var newPlugin = CreateMockComputePlugin();

        oldPlugin.Setup(p => p.ProcessDataAsync(It.IsAny<float[]>()))
            .ReturnsAsync(new float[] { 1, 1, 1 });
        newPlugin.Setup(p => p.ProcessDataAsync(It.IsAny<float[]>()))
            .ReturnsAsync(new float[] { 2, 2, 2 });

        _loaderMock.Setup(l => l.LoadPlugin("plugin.dll")).Returns(oldPlugin.Object);
        _loaderMock.Setup(l => l.ReloadPlugin("plugin.dll")).Returns(newPlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        var testData = new float[] { 1, 2, 3 };

        // Act - Initial load
        await _pluginManager.LoadPluginAsync("plugin.dll");
        await _pluginManager.InitializeAsync();
        
        pipeline.AddStage(_pluginManager.GetPlugin<IComputePlugin>());
        var result1 = await pipeline.ExecuteAsync(testData);

        // Act - Hot reload
        await _pluginManager.ReloadPluginAsync("plugin.dll");
        pipeline.UpdateStage<IComputePlugin>(_pluginManager.GetPlugin<IComputePlugin>());
        var result2 = await pipeline.ExecuteAsync(testData);

        // Assert
        result1.Should().Equal(1, 1, 1);
        result2.Should().Equal(2, 2, 2);
    }

    [Fact]
    public async Task Pipeline_ConcurrentExecution_HandlesCorrectly()
    {
        // Arrange
        var computePlugin = CreateMockComputePlugin();
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(computePlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        const int taskCount = 10;
        var tasks = new Task<float[]>[taskCount];

        // Act
        await _pluginManager.LoadPluginAsync("compute.dll");
        await _pluginManager.InitializeAsync();
        
        pipeline.AddStage(_pluginManager.GetPlugin<IComputePlugin>());

        for (int i = 0; i < taskCount; i++)
        {
            var taskData = new float[] { i, i + 1, i + 2 };
            tasks[i] = pipeline.ExecuteAsync(taskData);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(taskCount);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        computePlugin.Verify(p => p.ProcessDataAsync(It.IsAny<float[]>()), Times.Exactly(taskCount));
    }

    [Fact]
    public async Task Pipeline_ResourceManagement_CleansUpCorrectly()
    {
        // Arrange
        var computePlugin = CreateMockComputePlugin();
        var disposableResource = new Mock<IDisposable>();
        
        computePlugin.Setup(p => p.CreateResource()).Returns(disposableResource.Object);
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(computePlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);

        // Act
        await _pluginManager.LoadPluginAsync("compute.dll");
        await _pluginManager.InitializeAsync();
        
        var plugin = _pluginManager.GetPlugin<IComputePlugin>();
        var resource = plugin.CreateResource();
        
        pipeline.AddStage(plugin);
        pipeline.Dispose();

        // Assert
        disposableResource.Verify(d => d.Dispose(), Times.Never); // Plugin resources should be managed by plugin
        
        // Plugin cleanup happens during unload
        await _pluginManager.ShutdownAsync();
        computePlugin.Verify(p => p.ShutdownAsync(), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ErrorRecovery_ContinuesAfterError()
    {
        // Arrange
        var unreliablePlugin = CreateMockComputePlugin();
        var callCount = 0;
        
        unreliablePlugin.Setup(p => p.ProcessDataAsync(It.IsAny<float[]>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Transient error");
                return Task.FromResult(new float[] { 1, 2, 3 });
            });

        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(unreliablePlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        pipeline.EnableErrorRecovery(maxRetries: 3);

        // Act
        await _pluginManager.LoadPluginAsync("unreliable.dll");
        await _pluginManager.InitializeAsync();
        
        pipeline.AddStage(_pluginManager.GetPlugin<IComputePlugin>());
        
        var testData = new float[] { 1, 2, 3 };
        var result = await pipeline.ExecuteAsync(testData);

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(1, 2, 3);
        unreliablePlugin.Verify(p => p.ProcessDataAsync(It.IsAny<float[]>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Pipeline_PerformanceMonitoring_TracksMetrics()
    {
        // Arrange
        var computePlugin = CreateMockComputePlugin();
        var slowOperation = Task.Delay(100).ContinueWith(_ => new float[] { 1, 2, 3 });
        computePlugin.Setup(p => p.ProcessDataAsync(It.IsAny<float[]>())).Returns(slowOperation);

        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(computePlugin.Object);

        var pipeline = new ComputePipeline(_serviceProvider);
        pipeline.EnablePerformanceMonitoring();

        // Act
        await _pluginManager.LoadPluginAsync("slow.dll");
        await _pluginManager.InitializeAsync();
        
        pipeline.AddStage(_pluginManager.GetPlugin<IComputePlugin>());
        
        var testData = new float[] { 1, 2, 3 };
        await pipeline.ExecuteAsync(testData);

        var metrics = pipeline.GetPerformanceMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        metrics.StageMetrics.Should().HaveCount(1);
        metrics.StageMetrics.First().ExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
    }

    private Mock<IComputePlugin> CreateMockComputePlugin()
    {
        var mock = new Mock<IComputePlugin>();
        mock.Setup(p => p.Id).Returns(Guid.NewGuid());
        mock.Setup(p => p.Name).Returns("ComputePlugin");
        mock.Setup(p => p.Version).Returns("1.0.0");
        mock.Setup(p => p.ProcessDataAsync(It.IsAny<float[]>()))
            .ReturnsAsync((float[] data) => data.Select(x => x * 2).ToArray());
        mock.Setup(p => p.CreateResource()).Returns(Mock.Of<IDisposable>());
        return mock;
    }

    private Mock<IPreprocessPlugin> CreateMockPreprocessPlugin()
    {
        var mock = new Mock<IPreprocessPlugin>();
        mock.Setup(p => p.Id).Returns(Guid.NewGuid());
        mock.Setup(p => p.Name).Returns("PreprocessPlugin");
        mock.Setup(p => p.Version).Returns("1.0.0");
        mock.Setup(p => p.PreprocessAsync(It.IsAny<float[]>()))
            .ReturnsAsync((float[] data) => data.Select(x => x + 1).ToArray());
        return mock;
    }

    private Mock<IPostprocessPlugin> CreateMockPostprocessPlugin()
    {
        var mock = new Mock<IPostprocessPlugin>();
        mock.Setup(p => p.Id).Returns(Guid.NewGuid());
        mock.Setup(p => p.Name).Returns("PostprocessPlugin");
        mock.Setup(p => p.Version).Returns("1.0.0");
        mock.Setup(p => p.PostprocessAsync(It.IsAny<float[]>()))
            .ReturnsAsync((float[] data) => data.Select(x => x / 2).ToArray());
        return mock;
    }

    private Mock<IOptimizationPlugin> CreateMockOptimizationPlugin()
    {
        var mock = new Mock<IOptimizationPlugin>();
        mock.Setup(p => p.Id).Returns(Guid.NewGuid());
        mock.Setup(p => p.Name).Returns("OptimizationPlugin");
        mock.Setup(p => p.Version).Returns("1.0.0");
        mock.Setup(p => p.OptimizeAsync(It.IsAny<IPipelineContext>()))
            .Returns(Task.CompletedTask);
        return mock;
    }
}

// Mock plugin interfaces for testing
public interface IComputePlugin : IPlugin
{
    Task<float[]> ProcessDataAsync(float[] data);
    IDisposable CreateResource();
}

public interface IPreprocessPlugin : IPlugin
{
    Task<float[]> PreprocessAsync(float[] data);
}

public interface IPostprocessPlugin : IPlugin
{
    Task<float[]> PostprocessAsync(float[] data);
}

public interface IOptimizationPlugin : IPlugin
{
    Task OptimizeAsync(IPipelineContext context);
}

public interface IPipelineContext
{
    IServiceProvider Services { get; }
    IDictionary<string, object> Properties { get; }
}

// Mock pipeline classes for testing
public class ComputePipeline : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly List<IPlugin> _stages = new();
    private readonly List<IOptimizationPlugin> _optimizations = new();
    private bool _performanceMonitoring;
    private bool _errorRecovery;
    private int _maxRetries = 1;

    public ComputePipeline(IServiceProvider services)
    {
        _services = services;
    }

    public void AddStage<T>(T plugin) where T : IPlugin
    {
        _stages.Add(plugin);
    }

    public void UpdateStage<T>(T plugin) where T : IPlugin
    {
        var index = _stages.FindIndex(s => s is T);
        if (index >= 0)
        {
            _stages[index] = plugin;
        }
    }

    public void AddOptimization(IOptimizationPlugin optimization)
    {
        _optimizations.Add(optimization);
    }

    public void EnablePerformanceMonitoring()
    {
        _performanceMonitoring = true;
    }

    public void EnableErrorRecovery(int maxRetries = 3)
    {
        _errorRecovery = true;
        _maxRetries = maxRetries;
    }

    public async Task<float[]> ExecuteAsync(float[] data)
    {
        var startTime = DateTime.UtcNow;
        var stageMetrics = new List<StageMetric>();
        var currentData = data;

        // Apply optimizations
        var context = new PipelineContext(_services);
        foreach (var optimization in _optimizations)
        {
            await optimization.OptimizeAsync(context);
        }

        // Execute stages
        foreach (var stage in _stages)
        {
            var stageStart = DateTime.UtcNow;
            var attempts = 0;

            while (attempts < _maxRetries)
            {
                try
                {
                    attempts++;
                    
                    switch (stage)
                    {
                        case IPreprocessPlugin preprocess:
                            currentData = await preprocess.PreprocessAsync(currentData);
                            break;
                        case IComputePlugin compute:
                            currentData = await compute.ProcessDataAsync(currentData);
                            break;
                        case IPostprocessPlugin postprocess:
                            currentData = await postprocess.PostprocessAsync(currentData);
                            break;
                    }
                    break;
                }
                catch when (_errorRecovery && attempts < _maxRetries)
                {
                    await Task.Delay(100); // Brief delay before retry
                    continue;
                }
            }

            if (_performanceMonitoring)
            {
                stageMetrics.Add(new StageMetric
                {
                    StageName = stage.Name,
                    ExecutionTime = DateTime.UtcNow - stageStart
                });
            }
        }

        if (_performanceMonitoring)
        {
            _lastMetrics = new PerformanceMetrics
            {
                TotalExecutionTime = DateTime.UtcNow - startTime,
                StageMetrics = stageMetrics
            };
        }

        return currentData;
    }

    private PerformanceMetrics? _lastMetrics;

    public PerformanceMetrics GetPerformanceMetrics()
    {
        return _lastMetrics ?? new PerformanceMetrics();
    }

    public void Dispose()
    {
        _stages.Clear();
        _optimizations.Clear();
    }
}

public class PipelineContext : IPipelineContext
{
    public IServiceProvider Services { get; }
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    public PipelineContext(IServiceProvider services)
    {
        Services = services;
    }
}

public class PerformanceMetrics
{
    public TimeSpan TotalExecutionTime { get; set; }
    public List<StageMetric> StageMetrics { get; set; } = new();
}

public class StageMetric
{
    public string StageName { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
}