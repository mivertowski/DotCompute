using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for plugin system loading and initialization performance.
/// Tests simulated plugin discovery, loading, dependency resolution, and initialization times.
/// Note: Uses simplified mock implementations due to actual plugin system compilation issues.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class PluginSystemBenchmarks : IDisposable
{
    private ServiceCollection _services = null!;
    private IServiceProvider _serviceProvider = null!;
    private readonly List<IDisposable> _disposables = [];

    [Params(1, 5, 10, 25)]
    public int PluginCount { get; set; }

    [Params("Simple", "Complex", "WithDependencies")]
    public string PluginType { get; set; } = "Simple";

    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection();
        _services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        _serviceProvider = _services.BuildServiceProvider();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }

    [Benchmark(Baseline = true)]
    public void SinglePluginLoading()
    {
        var plugin = CreatePlugin(PluginType, 0);
        plugin.Initialize();
        _disposables.Add(plugin);
    }

    [Benchmark]
    public void MultiplePluginLoading()
    {
        for (var i = 0; i < PluginCount; i++)
        {
            var plugin = CreatePlugin(PluginType, i);
            plugin.Initialize();
            _disposables.Add(plugin);
        }
    }

    [Benchmark]
    public async Task AsyncPluginInitialization()
    {
        var plugins = new List<TestPluginInterface>();

        // Load plugins first
        for (var i = 0; i < PluginCount; i++)
        {
            var plugin = CreatePlugin(PluginType, i);
            plugins.Add(plugin);
        }

        // Initialize all plugins asynchronously
        var initTasks = plugins.Select(async plugin =>
        {
            await Task.Run(() => plugin.Initialize());
        });

        await Task.WhenAll(initTasks);

        _disposables.AddRange(plugins);
    }

    [Benchmark]
    public void PluginDiscovery()
    {
        var discoveredPlugins = new List<TestPluginInterface>();

        // Simulate plugin discovery from different sources
        var discoveryTasks = Enumerable.Range(0, PluginCount).Select(i =>
        {
            return Task.Run(() =>
            {
                // Simulate discovery time
                Thread.Sleep(1);
                return CreatePlugin(PluginType, i);
            });
        });

        var plugins = Task.WhenAll(discoveryTasks).Result;

        foreach (var plugin in plugins)
        {
            discoveredPlugins.Add(plugin);
            plugin.Initialize();
        }

        _disposables.AddRange(discoveredPlugins);
    }

    [Benchmark]
    public void DependencyResolution()
    {
        var plugins = new List<TestPluginInterface>();

        // Create plugins with dependencies
        for (var i = 0; i < PluginCount; i++)
        {
            var plugin = CreatePluginWithDependencies(i);
            plugins.Add(plugin);
        }

        // Simulate dependency resolution
        var sortedPlugins = ResolveDependencies(plugins);

        foreach (var plugin in sortedPlugins)
        {
            plugin.Initialize();
        }

        _disposables.AddRange(plugins);
    }

    [Benchmark]
    public void PluginLifecycleManagement()
    {
        var plugins = new List<TestPluginInterface>();

        // Load plugins
        for (var i = 0; i < PluginCount; i++)
        {
            var plugin = CreatePlugin(PluginType, i);
            plugins.Add(plugin);
        }

        // Initialize plugins
        foreach (var plugin in plugins)
        {
            plugin.Initialize();
        }

        // Simulate usage
        foreach (var plugin in plugins)
        {
            plugin.DoWork();
        }

        // Cleanup plugins
        foreach (var plugin in plugins)
        {
            plugin.Dispose();
        }
    }

    [Benchmark]
    public void ConcurrentPluginLoading()
    {
        var plugins = new List<TestPluginInterface>();
        var loadTasks = new List<Task>();

        for (var i = 0; i < PluginCount; i++)
        {
            var pluginIndex = i;
            loadTasks.Add(Task.Run(() =>
            {
                var plugin = CreatePlugin(PluginType, pluginIndex);
                plugin.Initialize();
                lock (plugins)
                {
                    plugins.Add(plugin);
                }
            }));
        }

        Task.WaitAll(loadTasks.ToArray());
        _disposables.AddRange(plugins);
    }

    [Benchmark]
    public double PluginLoadingThroughput()
    {
        var start = DateTime.UtcNow;
        var plugins = new List<TestPluginInterface>();

        for (var i = 0; i < PluginCount; i++)
        {
            var plugin = CreatePlugin(PluginType, i);
            plugin.Initialize();
            plugins.Add(plugin);
        }

        var elapsed = DateTime.UtcNow - start;
        var throughput = PluginCount / elapsed.TotalSeconds;

        _disposables.AddRange(plugins);
        return throughput; // Plugins loaded per second
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private static TestPluginInterface CreatePlugin(string type, int index)
    {
        return type switch
        {
            "Simple" => new SimpleTestPlugin($"TestPlugin_{index}", $"Simple test plugin {index}"),
            "Complex" => new ComplexTestPlugin($"ComplexPlugin_{index}", $"Complex test plugin {index}"),
            "WithDependencies" => new DependentTestPlugin($"DependentPlugin_{index}", $"Plugin with dependencies {index}"),
            _ => new SimpleTestPlugin($"DefaultPlugin_{index}", $"Default plugin {index}")
        };
    }

    private static DependentTestPlugin CreatePluginWithDependencies(int index)
    {
        return new DependentTestPlugin($"DependentPlugin_{index}", $"Plugin with dependencies {index}")
        {
            Dependencies = [$"Dependency_{index % 3}", "CommonDependency"]
        };
    }

    private static List<TestPluginInterface> ResolveDependencies(List<TestPluginInterface> plugins)
    {
        // Simple dependency resolution simulation
        var resolved = new List<TestPluginInterface>();
        var remaining = new List<TestPluginInterface>(plugins);

        while (remaining.Count > 0)
        {
            var canResolve = remaining.Where(p =>
            {
                if (p is DependentTestPlugin dep)
                {
                    return dep.Dependencies.All(d => resolved.Any(r => r.Name.Contains(d.Split('_')[0], StringComparison.OrdinalIgnoreCase)) || d == "CommonDependency");
                }
                return true;
            }).ToList();

            if (canResolve.Count == 0)
            {
                break; // Circular dependency or unresolvable
            }

            foreach (var plugin in canResolve)
            {
                resolved.Add(plugin);
                remaining.Remove(plugin);
            }
        }

        resolved.AddRange(remaining); // Add any unresolved ones
        return resolved;
    }
}

// Simplified test plugin interfaces and implementations
internal interface TestPluginInterface : IDisposable
{
    public string Name { get; }
    public string Description { get; }
    public Version Version { get; }
    public bool IsInitialized { get; }

    public void Initialize();
    public void DoWork();
}

internal abstract class TestPluginBase : TestPluginInterface
{
    public string Name { get; protected set; } = string.Empty;
    public string Description { get; protected set; } = string.Empty;
    public Version Version { get; protected set; } = new(1, 0, 0);
    public bool IsInitialized { get; protected set; }

    public abstract void Initialize();

    public virtual void DoWork() => Thread.Sleep(1);

    public virtual void Dispose() => IsInitialized = false;
}

internal sealed class SimpleTestPlugin : TestPluginBase
{
    public SimpleTestPlugin(string name, string description)
    {
        Name = name;
        Description = description;
        Version = new Version(1, 0, 0);
    }

    public override void Initialize()
    {
        // Simulate initialization work
        Thread.Sleep(1);
        IsInitialized = true;
    }
}

internal sealed class ComplexTestPlugin : TestPluginBase
{
    private readonly Dictionary<string, object> _configuration = [];
    private readonly List<object> _resources = [];

    public ComplexTestPlugin(string name, string description)
    {
        Name = name;
        Description = description;
        Version = new Version(1, 0, 0);

        // Add complex initialization
        _configuration["Setting1"] = "Value1";
        _configuration["Setting2"] = 42;
        _configuration["Setting3"] = true;
    }

    public override void Initialize()
    {
        // Complex initialization
        for (var i = 0; i < 10; i++)
        {
            _resources.Add(new object());
        }

        // Simulate complex setup
        Thread.Sleep(5);

        IsInitialized = true;
    }

    public override void Dispose()
    {
        _resources.Clear();
        _configuration.Clear();
        base.Dispose();
    }
}

internal sealed class DependentTestPlugin : TestPluginBase
{
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    public DependentTestPlugin(string name, string description)
    {
        Name = name;
        Description = description;
        Version = new Version(1, 0, 0);
    }

    public override void Initialize()
    {
        // Check dependencies before initialization
        foreach (var _ in Dependencies)
        {
            // Simulate dependency checking
            Thread.Sleep(1);
        }

        IsInitialized = true;
    }
}