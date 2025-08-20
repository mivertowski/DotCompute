// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Kernels;
// using DotCompute.Algorithms.Types; // Temporarily commented out due to compilation issues
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.DependencyInjection;
using DotCompute.Runtime;
using DotCompute.Runtime.Factories;
using DotCompute.Runtime.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime.Examples;


/// <summary>
/// Comprehensive example demonstrating DotCompute DI integration
/// </summary>
public static class DIIntegrationExample
{
    /// <summary>
    /// Example showing basic DI setup for DotCompute
    /// </summary>
    public static async Task BasicDISetupExample()
    {
        var services = new ServiceCollection();

        // Add logging
        _ = services.AddLogging(builder => builder.AddConsole());

        // Add DotCompute with basic configuration
        _ = services.AddDotComputeRuntime(configureOptions: options =>
        {
            options.EnableAutoDiscovery = true;
            options.PreferredAcceleratorType = AcceleratorType.CPU;
            options.EnableKernelCaching = true;
            options.EnableMemoryPooling = true;
        });

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Get runtime and use it
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();
        await runtime.InitializeAsync();

        // Get accelerator through DI
        var acceleratorManager = serviceProvider.GetRequiredService<IAcceleratorManager>();
        var defaultAccelerator = acceleratorManager.Default;

        Console.WriteLine($"Default accelerator: {defaultAccelerator.Info.Name} ({defaultAccelerator.Info.DeviceType})");

        await runtime.DisposeAsync();
        serviceProvider.Dispose();
    }

    /// <summary>
    /// Example showing advanced DI setup with configuration
    /// </summary>
    public static async Task AdvancedDISetupExample()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotCompute:EnableAutoDiscovery"] = "true",
                ["DotCompute:PreferredAcceleratorType"] = "CPU",
                ["DotCompute:MaxAccelerators"] = "8",
                ["DotCompute:EnableKernelCaching"] = "true",
                ["DotCompute:KernelCacheDirectory"] = "./cache/kernels",
                ["DotCompute:MaxCacheSizeMB"] = "512",
                ["DotCompute:EnableMemoryPooling"] = "true",
                ["DotCompute:InitialPoolSizeMB"] = "128",
                ["DotCompute:EnableProfiling"] = "true",
                ["DotCompute:Plugins:EnablePlugins"] = "true",
                ["DotCompute:Plugins:PluginDirectories:0"] = "./plugins",
                ["DotCompute:Plugins:EnableDependencyInjection"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Add configuration
        _ = services.AddSingleton<IConfiguration>(configuration);

        // Add logging with more detailed configuration
        _ = services.AddLogging(builder =>
        {
            _ = builder.AddConsole();
            _ = builder.AddDebug();
            _ = builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add complete DotCompute stack
        _ = services.AddDotComputeComplete(configuration);

        // Add custom services
        _ = services.AddTransient<ICustomComputeService, CustomComputeService>();

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Use the enhanced setup
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();
        // var pluginManager = serviceProvider.GetRequiredService<IAlgorithmPluginManager>();
        var memoryPool = serviceProvider.GetRequiredService<IMemoryPoolService>();
        var customService = serviceProvider.GetRequiredService<ICustomComputeService>();

        await runtime.InitializeAsync();
        // await pluginManager.LoadPluginsAsync();

        // Use memory pool
        var stats = memoryPool.GetUsageStatistics();
        Console.WriteLine($"Memory pools: {stats.PerAcceleratorStats.Count}");

        // Use custom service
        await customService.PerformComputationAsync();

        await runtime.DisposeAsync();
        serviceProvider.Dispose();
    }

    /// <summary>
    /// Example showing plugin DI integration
    /// </summary>
    public static Task PluginDIExample()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(builder => builder.AddConsole());

        // Add DotCompute with plugin support
        _ = services.AddDotComputeRuntime();
        _ = services.AddDotComputePlugins(configureOptions: options =>
        {
            options.EnablePlugins = true;
            options.EnableDependencyInjection = true;
            options.PluginLifetime = DotCompute.Runtime.Configuration.ServiceLifetime.Scoped;
        });

        // Register services that plugins might depend on
        _ = services.AddSingleton<ICustomDataProvider, CustomDataProvider>();
        _ = services.AddTransient<IComputationLogger, ComputationLogger>();

        var serviceProvider = services.BuildServiceProvider();

        // Get plugin services
        var pluginServiceProvider = serviceProvider.GetRequiredService<IPluginServiceProvider>();
        var pluginLifecycleManager = serviceProvider.GetRequiredService<IPluginLifecycleManager>();
        var pluginFactory = serviceProvider.GetRequiredService<IPluginFactory>();

        // Register plugin-specific services
        pluginServiceProvider.RegisterPluginServices("MyPlugin", pluginServices =>
        {
            _ = pluginServices.AddSingleton<IPluginSpecificService, PluginSpecificService>();
        });

        // Create plugin with DI
        var pluginScope = pluginServiceProvider.CreatePluginScope("MyPlugin");
        // var plugin = await pluginFactory.CreateAsync<IAlgorithmPlugin>(
        //     typeof(SampleAlgorithmPlugin), pluginScope);

        // Console.WriteLine($"Created plugin: {plugin.Name} v{plugin.Version}");

        pluginScope.Dispose();
        serviceProvider.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Example showing hosted service integration
    /// </summary>
    public static Task HostedServiceExample()
    {
        // This example requires Microsoft.Extensions.Hosting package
        // Uncomment and install the package to enable this functionality

        Console.WriteLine("HostedServiceExample: Microsoft.Extensions.Hosting package required");
        Console.WriteLine("Install with: dotnet add package Microsoft.Extensions.Hosting");

        return Task.CompletedTask;
        /*
        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add DotCompute as hosted service
                services.AddDotComputeRuntime(context.Configuration);

                // Add custom background service
                services.AddHostedService<ComputeBackgroundService>();
            })
            .Build();

        // Start host (will initialize DotCompute automatically)
        await host.StartAsync();

        // Host is running with DotCompute initialized
        Console.WriteLine("Host started with DotCompute runtime");

        // Simulate some work
        await Task.Delay(1000);

        await host.StopAsync();
        host.Dispose();\n        */
    }

    /// <summary>
    /// Example showing custom accelerator factory
    /// </summary>
    public static async Task CustomFactoryExample()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(builder => builder.AddConsole());

        // Use custom accelerator factory
        _ = services.AddDotComputeRuntimeWithFactory<CustomAcceleratorFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();

        await runtime.InitializeAsync();

        var accelerators = runtime.GetAccelerators();
        Console.WriteLine($"Found {accelerators.Count} accelerators using custom factory");

        await runtime.DisposeAsync();
        serviceProvider.Dispose();
    }
}

/// <summary>
/// Example custom service that uses DotCompute through DI
/// </summary>
public interface ICustomComputeService
{
    Task PerformComputationAsync();
}

public class CustomComputeService : ICustomComputeService
{
    private readonly IAcceleratorManager _acceleratorManager;
    private readonly IMemoryPoolService _memoryPool;
    private readonly ILogger<CustomComputeService> _logger;

    public CustomComputeService(
        IAcceleratorManager acceleratorManager,
        IMemoryPoolService memoryPool,
        ILogger<CustomComputeService> logger)
    {
        _acceleratorManager = acceleratorManager;
        _memoryPool = memoryPool;
        _logger = logger;
    }

    public async Task PerformComputationAsync()
    {
        _logger.LogInformation("Starting custom computation");

        var accelerator = _acceleratorManager.Default;
        var pool = _memoryPool.GetPool(accelerator.Info.Id);

        using var buffer = await pool.AllocateAsync(1024 * 1024); // 1MB

        _logger.LogInformation("Allocated {SizeKB}KB from memory pool", buffer.SizeInBytes / 1024);

        // Simulate computation
        await accelerator.SynchronizeAsync();

        _logger.LogInformation("Custom computation completed");
    }
}

/// <summary>
/// Example algorithm plugin with DI support
/// </summary>
/*
public class SampleAlgorithmPlugin : IAlgorithmPlugin
{
private readonly ICustomDataProvider _dataProvider;
private readonly IComputationLogger _computationLogger;
private readonly ILogger<SampleAlgorithmPlugin> _logger;
private IAccelerator? _accelerator;

// Constructor injection
public SampleAlgorithmPlugin(
    ICustomDataProvider dataProvider,
    IComputationLogger computationLogger,
    ILogger<SampleAlgorithmPlugin> logger)
{
    _dataProvider = dataProvider;
    _computationLogger = computationLogger;
    _logger = logger;
}

// Property injection (optional)
[Inject(Required = false)]
public IPluginSpecificService? PluginService { get; set; }

public string Id => "sample-algorithm-plugin";
public string Name => "Sample Algorithm Plugin";
public Version Version => new(1, 0, 0);
public string Description => "Example plugin demonstrating DI integration";
public AcceleratorType[] SupportedAccelerators => new[] { AcceleratorType.CPU };
public Type[] InputTypes => new[] { typeof(float[]) };
public Type OutputType => typeof(float[]);

public async Task InitializeAsync(IAccelerator accelerator, CancellationToken cancellationToken = default)
{
    _accelerator = accelerator;
    _logger.LogInformation("Initializing plugin with accelerator {AcceleratorName}", accelerator.Info.Name);
    
    // Use injected services
    var data = await _dataProvider.GetInitializationDataAsync();
    await _computationLogger.LogInitializationAsync(Id, data.Length);
    
    await Task.CompletedTask;
}

public async Task<object> ExecuteAsync(object[] inputs, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
{
    if (_accelerator == null)
    {
        throw new InvalidOperationException("Plugin not initialized");
    }
    
    _logger.LogInformation("Executing algorithm plugin");
    
    // Use plugin-specific service if available
    if (PluginService != null)
    {
        await PluginService.ProcessAsync();
    }
    
    // Simulate computation
    var input = (float[])inputs[0];
    var result = new float[input.Length];
    for (int i = 0; i < input.Length; i++)
    {
        result[i] = input[i] * 2.0f;
    }
    
    await _computationLogger.LogExecutionAsync(Id, input.Length);
    return result;
}

public bool ValidateInputs(object[] inputs)
{
    return inputs.Length == 1 && inputs[0] is float[];
}

public long EstimateMemoryRequirement(int[] inputSizes)
{
    return inputSizes.Sum() * sizeof(float) * 2; // Input + output
}

public AlgorithmPerformanceProfile GetPerformanceProfile()
{
    return new AlgorithmPerformanceProfile
    {
        Complexity = "O(n)",
        IsParallelizable = true,
        OptimalParallelism = Environment.ProcessorCount,
        IsMemoryBound = false,
        IsComputeBound = true,
        EstimatedFlops = 1000000
    };
}

public ValueTask DisposeAsync()
{
    _logger.LogInformation("Disposing plugin");
    return ValueTask.CompletedTask;
}
}
*/

// Supporting service interfaces and implementations
public interface ICustomDataProvider
{
    Task<byte[]> GetInitializationDataAsync();
}

public class CustomDataProvider : ICustomDataProvider
{
    public async Task<byte[]> GetInitializationDataAsync()
    {
        await Task.Delay(10); // Simulate async work
        return new byte[1024];
    }
}

public interface IComputationLogger
{
    Task LogInitializationAsync(string pluginId, int dataSize);
    Task LogExecutionAsync(string pluginId, int inputSize);
}

public class ComputationLogger : IComputationLogger
{
    private readonly ILogger<ComputationLogger> _logger;

    public ComputationLogger(ILogger<ComputationLogger> logger)
    {
        _logger = logger;
    }

    public async Task LogInitializationAsync(string pluginId, int dataSize)
    {
        _logger.LogInformation("Plugin {PluginId} initialized with {DataSize} bytes of data",
            pluginId, dataSize);
        await Task.CompletedTask;
    }

    public async Task LogExecutionAsync(string pluginId, int inputSize)
    {
        _logger.LogInformation("Plugin {PluginId} executed with input size {InputSize}",
            pluginId, inputSize);
        await Task.CompletedTask;
    }
}

public interface IPluginSpecificService
{
    Task ProcessAsync();
}

public class PluginSpecificService : IPluginSpecificService
{
    public async Task ProcessAsync() => await Task.Delay(5); // Simulate processing
}

/// <summary>
/// Example background service using DotCompute
/// </summary>
public class ComputeBackgroundService : BackgroundService
{
    private readonly AcceleratorRuntime _runtime;
    private readonly ILogger<ComputeBackgroundService> _logger;

    public ComputeBackgroundService(
        AcceleratorRuntime runtime,
        ILogger<ComputeBackgroundService> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background compute service running at: {Time}", DateTimeOffset.Now);

            var accelerators = _runtime.GetAccelerators();
            _logger.LogInformation("Available accelerators: {Count}", accelerators.Count);

            await Task.Delay(5000, stoppingToken);
        }
    }
}

/// <summary>
/// Example custom accelerator factory
/// </summary>
public class CustomAcceleratorFactory : DefaultAcceleratorFactory
{
    public CustomAcceleratorFactory(
        IServiceProvider serviceProvider,
        IOptions<DotComputeRuntimeOptions> options,
        ILogger<CustomAcceleratorFactory> logger)
        : base(serviceProvider, options, logger)
    {
        // Register additional custom providers
        RegisterProvider(typeof(CustomAcceleratorProvider), AcceleratorType.GPU);
    }
}

/// <summary>
/// Example custom accelerator provider
/// </summary>
public class CustomAcceleratorProvider : IAcceleratorProvider
{
    private readonly ILogger<CustomAcceleratorProvider> _logger;

    public CustomAcceleratorProvider(ILogger<CustomAcceleratorProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "Custom";
    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.GPU };

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering custom accelerators");

        // Return empty collection for this example
        return ValueTask.FromResult(Enumerable.Empty<IAccelerator>());
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        // For this example, we'll create a simple mock accelerator
        var mockAccelerator = new ExampleMockAccelerator(info);
        return ValueTask.FromResult<IAccelerator>(mockAccelerator);
    }
}

/// <summary>
/// Example mock accelerator implementation for DI demonstration.
/// </summary>
internal class ExampleMockAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; }
    public AcceleratorType Type => AcceleratorType.CPU;
    public AcceleratorContext Context { get; } = default!;
    public IMemoryManager Memory => new ExampleMockMemoryManager();

    public ExampleMockAccelerator(AcceleratorInfo info)
    {
        Info = info;
    }

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mockKernel = new ExampleMockCompiledKernel(definition);
        return ValueTask.FromResult<ICompiledKernel>(mockKernel);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Example mock memory manager for DI demonstration.
/// </summary>
internal class ExampleMockMemoryManager : IMemoryManager
{
    public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        var buffer = new ExampleMockMemoryBuffer(sizeInBytes, options);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = new ExampleMockMemoryBuffer(source.Length * sizeof(int), options);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length) => new ExampleMockMemoryBuffer(length, buffer.Options);

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var buffer = new ExampleMockMemoryBuffer(count * sizeof(int), MemoryOptions.None);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        // Mock implementation - do nothing
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        // Mock implementation - do nothing
    }

    public void Free(IMemoryBuffer buffer) => buffer?.Dispose();

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Example mock memory buffer for DI demonstration.
/// </summary>
internal class ExampleMockMemoryBuffer(long size, MemoryOptions options) : IMemoryBuffer
{
    public long SizeInBytes { get; } = size;
    public MemoryOptions Options { get; } = options;
    public bool IsDisposed { get; private set; }

    public ValueTask<Memory<byte>> GetMemoryAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new Memory<byte>(new byte[SizeInBytes]));

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged => ValueTask.CompletedTask;

    public void Dispose() => IsDisposed = true;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}


/// <summary>
/// Example mock compiled kernel for DI demonstration.
/// </summary>
internal class ExampleMockCompiledKernel(KernelDefinition definition) : ICompiledKernel
{
    public string Name => definition.Name;

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

