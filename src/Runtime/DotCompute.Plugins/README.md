# DotCompute Plugin System

A robust, extensible plugin system for DotCompute that enables dynamic loading of backend implementations with full isolation, hot-reload support, and comprehensive lifecycle management.

## Features

- **Dynamic Loading**: Load plugins at runtime from assemblies
- **Isolation**: Each plugin runs in its own AssemblyLoadContext for complete isolation
- **Hot Reload**: Support for reloading plugins without restarting the application
- **Lifecycle Management**: Full control over plugin initialization, start, stop, and disposal
- **Health Monitoring**: Built-in health checks and metrics collection
- **Configuration Support**: Dynamic configuration with hot-reload capabilities
- **Dependency Management**: Automatic resolution of plugin dependencies
- **Security**: Sandboxing and permission management for plugins
- **Performance Tracking**: Built-in metrics and telemetry

## Quick Start

### 1. Add Plugin Support to Your Host

```csharp
var host = Host.CreateDefaultBuilder(args)
    .UsePlugins(builder => builder
        .SetPluginsDirectory("./plugins")
        .EnableHotReload()
        .SetHealthCheckInterval(TimeSpan.FromMinutes(1))
        .EnableMetrics())
    .Build();
```

### 2. Create a Plugin

```csharp
[Plugin("my.plugin", "My Custom Plugin")]
[PluginCapability("compute.custom", Version = "1.0.0")]
public class MyPlugin : BackendPluginBase
{
    public override string Id => "my.plugin";
    public override string Name => "My Custom Plugin";
    public override Version Version => new(1, 0, 0);
    public override string Description => "A custom compute backend";
    public override string Author => "Your Name";
    public override PluginCapabilities Capabilities => 
        PluginCapabilities.ComputeBackend | PluginCapabilities.HotReloadable;

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        // Initialize your plugin
        Logger?.LogInformation("Initializing My Plugin");
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Start your plugin services
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        // Stop your plugin services
        return Task.CompletedTask;
    }
}
```

### 3. Configure Your Plugin

```json
{
  "Plugins": {
    "PluginsDirectory": "./plugins",
    "EnableHotReload": true,
    "HealthCheckInterval": "00:01:00",
    "my.plugin": {
      "Enabled": true,
      "Settings": {
        "CustomOption": "value"
      }
    }
  }
}
```

## Architecture

### Core Components

1. **IBackendPlugin**: Base interface for all plugins
2. **BackendPluginBase**: Abstract base class providing common functionality
3. **PluginLoader**: Handles assembly loading with isolation
4. **PluginManager**: Manages plugin lifecycle and orchestration
5. **PluginLoadContext**: Custom AssemblyLoadContext for isolation

### Plugin Lifecycle

```
Unknown → Loading → Loaded → Initializing → Initialized → Starting → Running
                                                                         ↓
Unloaded ← Unloading ← Stopped ← Stopping ←─────────────────────────────┘
```

### Plugin Capabilities

Plugins can declare various capabilities:

- `ComputeBackend`: Provides compute functionality
- `StorageProvider`: Provides storage services
- `NetworkProvider`: Provides networking services
- `SecurityProvider`: Provides security services
- `MonitoringProvider`: Provides monitoring capabilities
- `HotReloadable`: Supports hot reload
- `Scalable`: Supports horizontal scaling
- `Clusterable`: Supports clustering

## Advanced Features

### Hot Reload

Enable hot reload for development:

```csharp
builder.EnableHotReload()
    .ConfigureLoader(options =>
    {
        options.EnableHotReload = true;
        options.ReloadDelay = TimeSpan.FromMilliseconds(500);
    });
```

### Resource Limits

Set resource limits per plugin:

```csharp
builder.SetResourceLimits(maxMemoryMB: 512, maxCpuPercent: 50);
```

### Custom Dependencies

Add shared assemblies and probing paths:

```csharp
builder.AddSharedAssemblies("MyShared.dll")
    .AddProbingPaths("./lib", "./dependencies");
```

### Health Monitoring

Access plugin health and metrics:

```csharp
var manager = serviceProvider.GetRequiredService<PluginManager>();
var stats = manager.GetStatistics();
var metrics = manager.GetPlugin("my.plugin")?.GetMetrics();
```

### Plugin Discovery

Discover plugins in a directory:

```csharp
var discoveries = await pluginLoader.DiscoverPluginsAsync("./plugins");
foreach (var discovery in discoveries.Where(d => d.Success))
{
    Console.WriteLine($"Found: {discovery.AssemblyName}");
    foreach (var plugin in discovery.PluginTypes)
    {
        Console.WriteLine($"  - {plugin.Id}: {plugin.Name} v{plugin.Version}");
    }
}
```

## Security

### Sandboxing

Enable sandboxing for untrusted plugins:

```csharp
builder.EnableSandboxing()
    .ConfigureManager(options =>
    {
        options.ValidateSignatures = true;
        options.TrustedCertificateThumbprints.Add("THUMBPRINT");
    });
```

### Permissions

Configure plugin permissions:

```json
{
  "PluginSecurity": {
    "my.plugin": {
      "AllowFileSystemAccess": false,
      "AllowNetworkAccess": true,
      "AllowedNetworkEndpoints": ["https://api.example.com"]
    }
  }
}
```

## Best Practices

1. **Implement IDisposable**: Always clean up resources in your plugins
2. **Use Logging**: Leverage the provided logger for debugging
3. **Handle Cancellation**: Respect cancellation tokens in async operations
4. **Update Metrics**: Keep metrics updated for monitoring
5. **Validate Configuration**: Implement proper validation in your plugins
6. **Document Dependencies**: Clearly document external dependencies
7. **Version Carefully**: Use semantic versioning for your plugins
8. **Test Isolation**: Test plugins in isolation before deployment

## Examples

See the `Examples` folder for complete plugin examples:

- `ExampleBackendPlugin.cs`: Basic plugin implementation
- `ComputePlugin.cs`: Compute backend plugin
- `StoragePlugin.cs`: Storage provider plugin
- `MonitoringPlugin.cs`: Monitoring provider plugin

## Troubleshooting

### Plugin Won't Load

1. Check the plugin implements `IBackendPlugin`
2. Verify the `[Plugin]` attribute is present
3. Ensure all dependencies are available
4. Check platform requirements are met

### Hot Reload Not Working

1. Ensure the plugin has `SupportsHotReload = true`
2. Check file permissions on the plugin directory
3. Verify no file locks on the plugin assembly

### Performance Issues

1. Monitor plugin metrics for high CPU/memory usage
2. Check for memory leaks in plugin disposal
3. Review plugin health status
4. Enable performance profiling

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[Backend Integration](../../../docs/articles/architecture/backend-integration.md)** - Plugin system architecture
- **[System Overview](../../../docs/articles/architecture/overview.md)** - Plugin integration in framework

### Developer Guides
- **[Getting Started](../../../docs/getting-started.md)** - Installation and setup
- **[Backend Selection](../../../docs/articles/guides/backend-selection.md)** - Creating custom backends

### API Documentation
- **[API Reference](../../../docs/api/index.md)** - Complete API documentation

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)

## Contributing

When contributing plugins:

1. Follow the established patterns
2. Include comprehensive tests
3. Document all public APIs
4. Provide configuration examples
5. Test hot reload scenarios
6. Validate cross-platform compatibility