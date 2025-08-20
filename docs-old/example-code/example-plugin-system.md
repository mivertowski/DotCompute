# Plugin System Examples

## 🔌 Complete Plugin System Usage Guide

### **Basic Plugin Loading**

```csharp
using DotCompute.Plugins.Core;
using Microsoft.Extensions.Logging;

// Create plugin system
var logger = loggerFactory.CreateLogger<PluginSystem>();
var pluginSystem = new PluginSystem(logger);

// Load CUDA backend plugin
var cudaPlugin = await pluginSystem.LoadPluginAsync(
    assemblyPath: "plugins/DotCompute.Backends.CUDA.dll",
    pluginTypeName: "DotCompute.Backends.CUDA.CudaBackendPlugin"
);

if (cudaPlugin != null)
{
    Console.WriteLine($"✅ Loaded: {cudaPlugin.Name} v{cudaPlugin.Version}");
    
    // Check if CUDA is available
    if (cudaPlugin.IsAvailable())
    {
        var accelerator = await cudaPlugin.CreateAcceleratorAsync();
        Console.WriteLine($"🚀 CUDA accelerator ready: {accelerator.Info.Name}");
    }
}
```

### **Hot Reload Example**

```csharp
public class PluginHotReloadManager
{
    private readonly PluginSystem _pluginSystem;
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, DateTime> _lastReloadTimes = new();
    
    public PluginHotReloadManager(PluginSystem pluginSystem, string pluginDirectory)
    {
        _pluginSystem = pluginSystem;
        
        // Watch for plugin file changes
        _watcher = new FileSystemWatcher(pluginDirectory)
        {
            Filter = "*.dll",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        
        _watcher.Changed += OnPluginChanged;
        _watcher.EnableRaisingEvents = true;
    }
    
    private async void OnPluginChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce rapid file changes
        var now = DateTime.UtcNow;
        if (_lastReloadTimes.TryGetValue(e.FullPath, out var lastTime) &&
            now - lastTime < TimeSpan.FromSeconds(2))
        {
            return;
        }
        _lastReloadTimes[e.FullPath] = now;
        
        try
        {
            Console.WriteLine($"🔄 Plugin file changed: {e.Name}");
            
            // Find existing plugin by assembly path
            var existingPlugin = _pluginSystem.GetLoadedPlugins()
                .FirstOrDefault(p => p.AssemblyPath == e.FullPath);
            
            if (existingPlugin != null)
            {
                // Unload old version
                await _pluginSystem.UnloadPluginAsync(existingPlugin.Id);
                Console.WriteLine($"🗑️ Unloaded old version: {existingPlugin.Name}");
                
                // Wait for file to be completely written
                await Task.Delay(500);
                
                // Reload new version
                var pluginTypeName = GetPluginTypeName(e.FullPath);
                var newPlugin = await _pluginSystem.LoadPluginAsync(e.FullPath, pluginTypeName);
                
                if (newPlugin != null)
                {
                    Console.WriteLine($"✅ Hot reloaded: {newPlugin.Name} v{newPlugin.Version}");
                    OnPluginReloaded?.Invoke(newPlugin);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hot reload failed: {ex.Message}");
        }
    }
    
    public event Action<IBackendPlugin>? OnPluginReloaded;
}

// Usage
var hotReloadManager = new PluginHotReloadManager(pluginSystem, "plugins/");
hotReloadManager.OnPluginReloaded += plugin =>
{
    Console.WriteLine($"🎉 Plugin reloaded successfully: {plugin.Name}");
    // Update accelerator references, restart computations, etc.
};
```

### **Plugin Discovery and Management**

```csharp
public class PluginManager
{
    private readonly PluginSystem _pluginSystem;
    private readonly Dictionary<string, IBackendPlugin> _activePlugins = new();
    
    public async Task<PluginDiscoveryResult> DiscoverPluginsAsync(string pluginDirectory)
    {
        var result = new PluginDiscoveryResult();
        
        if (!Directory.Exists(pluginDirectory))
        {
            result.Errors.Add($"Plugin directory not found: {pluginDirectory}");
            return result;
        }
        
        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
        
        foreach (var file in pluginFiles)
        {
            try
            {
                // Load assembly to discover plugin types
                var assembly = Assembly.LoadFrom(file);
                var pluginTypes = PluginSystem.DiscoverPluginTypes(assembly);
                
                foreach (var pluginType in pluginTypes)
                {
                    var pluginInfo = ExtractPluginInfo(pluginType);
                    result.DiscoveredPlugins.Add(new PluginDiscoveryInfo
                    {
                        AssemblyPath = file,
                        TypeName = pluginType.FullName!,
                        Name = pluginInfo.Name,
                        Version = pluginInfo.Version,
                        Description = pluginInfo.Description,
                        RequiredCapabilities = pluginInfo.RequiredCapabilities
                    });
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to load {file}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"🔍 Discovered {result.DiscoveredPlugins.Count} plugins in {pluginFiles.Length} files");
        return result;
    }
    
    public async Task<bool> LoadPluginAsync(PluginDiscoveryInfo pluginInfo)
    {
        try
        {
            var plugin = await _pluginSystem.LoadPluginAsync(
                pluginInfo.AssemblyPath, 
                pluginInfo.TypeName);
            
            if (plugin != null && plugin.IsAvailable())
            {
                _activePlugins[plugin.Id] = plugin;
                Console.WriteLine($"✅ Loaded and activated: {plugin.Name}");
                return true;
            }
            else
            {
                Console.WriteLine($"⚠️ Plugin not available: {pluginInfo.Name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to load {pluginInfo.Name}: {ex.Message}");
            return false;
        }
    }
    
    public async Task LoadAllAvailablePluginsAsync(string pluginDirectory)
    {
        var discovery = await DiscoverPluginsAsync(pluginDirectory);
        
        foreach (var plugin in discovery.DiscoveredPlugins)
        {
            await LoadPluginAsync(plugin);
        }
        
        Console.WriteLine($"🚀 Loaded {_activePlugins.Count} active plugins");
    }
    
    public IBackendPlugin? GetBestPlugin(AcceleratorType preferredType)
    {
        // Find plugin that supports the preferred accelerator type
        return _activePlugins.Values
            .Where(p => p.GetAvailableAccelerators().Any(a => a.DeviceType == preferredType.ToString()))
            .OrderByDescending(p => p.GetAvailableAccelerators().First().TotalMemory)
            .FirstOrDefault();
    }
}

// Plugin discovery result
public class PluginDiscoveryResult
{
    public List<PluginDiscoveryInfo> DiscoveredPlugins { get; } = new();
    public List<string> Errors { get; } = new();
}

public class PluginDiscoveryInfo
{
    public string AssemblyPath { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Name { get; set; } = "";
    public Version Version { get; set; } = new();
    public string Description { get; set; } = "";
    public List<string> RequiredCapabilities { get; set; } = new();
}
```

### **Creating a Custom Backend Plugin**

```csharp
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Attributes;

[Plugin("OpenCL", "1.0.0", "OpenCL compute backend for cross-platform GPU acceleration")]
public class OpenCLBackendPlugin : BackendPluginBase
{
    public override string Id => "opencl-backend";
    public override string Name => "OpenCL Backend";
    public override Version Version => new Version(1, 0, 0);
    public override string Description => "Cross-platform GPU compute using OpenCL";
    
    private readonly List<OpenCLDevice> _availableDevices = new();
    
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Discover OpenCL platforms and devices
            var platforms = OpenCL.GetPlatforms();
            
            foreach (var platform in platforms)
            {
                var devices = platform.GetDevices(DeviceType.GPU | DeviceType.CPU);
                
                foreach (var device in devices)
                {
                    _availableDevices.Add(new OpenCLDevice
                    {
                        Id = $"opencl-{platform.Name}-{device.Name}",
                        Name = device.Name,
                        Platform = platform.Name,
                        DeviceType = device.Type,
                        MaxComputeUnits = device.MaxComputeUnits,
                        MaxMemoryAllocation = device.MaxMemoryAllocation,
                        GlobalMemorySize = device.GlobalMemorySize
                    });
                }
            }
            
            IsInitialized = _availableDevices.Count > 0;
            
            if (IsInitialized)
            {
                Console.WriteLine($"✅ OpenCL initialized with {_availableDevices.Count} devices");
            }
            else
            {
                Console.WriteLine("⚠️ No OpenCL devices found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ OpenCL initialization failed: {ex.Message}");
            throw new PluginInitializationException("Failed to initialize OpenCL", ex);
        }
    }
    
    public override bool IsAvailable()
    {
        try
        {
            // Quick check for OpenCL runtime availability
            var platforms = OpenCL.GetPlatforms();
            return platforms.Any();
        }
        catch
        {
            return false;
        }
    }
    
    public override IEnumerable<AcceleratorInfo> GetAvailableAccelerators()
    {
        return _availableDevices.Select(d => new AcceleratorInfo
        {
            Id = d.Id,
            Name = d.Name,
            DeviceType = d.DeviceType.ToString(),
            Vendor = d.Platform,
            TotalMemory = d.GlobalMemorySize,
            ComputeUnits = d.MaxComputeUnits,
            Capabilities = new Dictionary<string, object>
            {
                ["MaxMemoryAllocation"] = d.MaxMemoryAllocation,
                ["Platform"] = d.Platform,
                ["Type"] = d.DeviceType
            }
        });
    }
    
    public override async Task<IAccelerator> CreateAcceleratorAsync(
        AcceleratorOptions? options = null)
    {
        if (!IsInitialized)
        {
            await InitializeAsync();
        }
        
        if (!_availableDevices.Any())
        {
            throw new InvalidOperationException("No OpenCL devices available");
        }
        
        // Select device based on options
        var selectedDevice = options?.DeviceId != null 
            ? _availableDevices.FirstOrDefault(d => d.Id == options.DeviceId)
            : _availableDevices.First(); // Default to first device
        
        if (selectedDevice == null)
        {
            throw new ArgumentException($"Device not found: {options?.DeviceId}");
        }
        
        return new OpenCLAccelerator(selectedDevice, options);
    }
    
    public override PluginCapabilities GetCapabilities()
    {
        return new PluginCapabilities
        {
            SupportedAcceleratorTypes = new[] { AcceleratorType.OpenCL },
            SupportsHotReload = true,
            SupportsMultiDevice = true,
            RequiredPermissions = new[] { "GPU_ACCESS", "MEMORY_ALLOCATION" },
            MinimumFrameworkVersion = new Version(9, 0),
            SupportedOperatingSystems = new[] { "Windows", "Linux", "macOS" },
            Features = new Dictionary<string, object>
            {
                ["CrossPlatform"] = true,
                ["MultiVendorSupport"] = true,
                ["KernelCaching"] = true,
                ["ProfilerIntegration"] = true
            }
        };
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cleanup OpenCL resources
            foreach (var device in _availableDevices)
            {
                device.Dispose();
            }
            _availableDevices.Clear();
        }
        
        base.Dispose(disposing);
    }
}

// Custom accelerator implementation
public class OpenCLAccelerator : IAccelerator
{
    private readonly OpenCLDevice _device;
    private readonly AcceleratorOptions? _options;
    private readonly OpenCLContext _context;
    private readonly OpenCLCommandQueue _commandQueue;
    
    public OpenCLAccelerator(OpenCLDevice device, AcceleratorOptions? options)
    {
        _device = device;
        _options = options;
        
        // Initialize OpenCL context and command queue
        _context = new OpenCLContext(_device);
        _commandQueue = new OpenCLCommandQueue(_context, _device);
        
        Info = new AcceleratorInfo
        {
            Id = device.Id,
            Name = device.Name,
            DeviceType = device.DeviceType.ToString(),
            TotalMemory = device.GlobalMemorySize,
            ComputeUnits = device.MaxComputeUnits
        };
        
        Memory = new OpenCLMemoryManager(_context, _commandQueue);
    }
    
    public AcceleratorInfo Info { get; }
    public IMemoryManager Memory { get; }
    
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Compile OpenCL kernel from source
        var program = _context.CreateProgram(definition.Source);
        await program.BuildAsync(_device, options?.CompilerFlags ?? "");
        
        var kernel = program.CreateKernel(definition.EntryPoint);
        
        return new OpenCLCompiledKernel(kernel, _commandQueue, definition);
    }
    
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await _commandQueue.FinishAsync();
    }
    
    public async ValueTask DisposeAsync()
    {
        await SynchronizeAsync();
        
        _commandQueue?.Dispose();
        _context?.Dispose();
    }
}
```

### **Plugin Configuration and Dependency Management**

```csharp
public class PluginConfiguration
{
    public Dictionary<string, PluginSettings> Plugins { get; set; } = new();
    public string PluginDirectory { get; set; } = "plugins/";
    public bool EnableHotReload { get; set; } = true;
    public TimeSpan HotReloadDebounce { get; set; } = TimeSpan.FromSeconds(2);
    public List<string> LoadOnStartup { get; set; } = new();
    public Dictionary<string, object> GlobalSettings { get; set; } = new();
}

public class PluginSettings
{
    public bool Enabled { get; set; } = true;
    public string? PreferredVersion { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public int LoadPriority { get; set; } = 0;
}

public class AdvancedPluginManager
{
    private readonly PluginSystem _pluginSystem;
    private readonly PluginConfiguration _config;
    
    public async Task LoadConfiguredPluginsAsync()
    {
        // Sort plugins by load priority and dependencies
        var sortedPlugins = TopologicalSort(_config.Plugins);
        
        foreach (var (pluginId, settings) in sortedPlugins)
        {
            if (!settings.Enabled)
            {
                Console.WriteLine($"⏭️ Skipping disabled plugin: {pluginId}");
                continue;
            }
            
            try
            {
                // Check dependencies
                await EnsureDependenciesLoadedAsync(settings.Dependencies);
                
                // Load plugin
                var pluginPath = Path.Combine(_config.PluginDirectory, $"{pluginId}.dll");
                var plugin = await _pluginSystem.LoadPluginAsync(pluginPath, GetPluginTypeName(pluginId));
                
                if (plugin != null)
                {
                    // Apply configuration
                    await ConfigurePluginAsync(plugin, settings.Configuration);
                    
                    Console.WriteLine($"✅ Loaded configured plugin: {plugin.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load plugin {pluginId}: {ex.Message}");
            }
        }
    }
    
    private async Task ConfigurePluginAsync(IBackendPlugin plugin, Dictionary<string, object> config)
    {
        if (plugin is IConfigurablePlugin configurablePlugin)
        {
            await configurablePlugin.ConfigureAsync(config);
        }
    }
    
    private List<(string, PluginSettings)> TopologicalSort(Dictionary<string, PluginSettings> plugins)
    {
        // Implement topological sort for dependency resolution
        var result = new List<(string, PluginSettings)>();
        var visited = new HashSet<string>();
        var temp = new HashSet<string>();
        
        foreach (var plugin in plugins)
        {
            if (!visited.Contains(plugin.Key))
            {
                TopologicalSortVisit(plugin.Key, plugins, visited, temp, result);
            }
        }
        
        return result.OrderBy(p => p.Item2.LoadPriority).ToList();
    }
    
    private void TopologicalSortVisit(
        string pluginId,
        Dictionary<string, PluginSettings> plugins,
        HashSet<string> visited,
        HashSet<string> temp,
        List<(string, PluginSettings)> result)
    {
        if (temp.Contains(pluginId))
        {
            throw new InvalidOperationException($"Circular dependency detected involving {pluginId}");
        }
        
        if (!visited.Contains(pluginId))
        {
            temp.Add(pluginId);
            
            if (plugins.TryGetValue(pluginId, out var settings))
            {
                foreach (var dependency in settings.Dependencies)
                {
                    TopologicalSortVisit(dependency, plugins, visited, temp, result);
                }
                
                result.Add((pluginId, settings));
            }
            
            temp.Remove(pluginId);
            visited.Add(pluginId);
        }
    }
}
```

### **Plugin Health Monitoring**

```csharp
public class PluginHealthMonitor
{
    private readonly PluginSystem _pluginSystem;
    private readonly Timer _healthCheckTimer;
    private readonly Dictionary<string, PluginHealthStatus> _healthStatus = new();
    
    public PluginHealthMonitor(PluginSystem pluginSystem)
    {
        _pluginSystem = pluginSystem;
        _healthCheckTimer = new Timer(CheckPluginHealth, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
    
    private async void CheckPluginHealth(object? state)
    {
        var plugins = _pluginSystem.GetLoadedPlugins();
        
        foreach (var plugin in plugins)
        {
            try
            {
                var health = await CheckPluginHealthAsync(plugin);
                _healthStatus[plugin.Id] = health;
                
                if (health.Status == HealthStatus.Unhealthy)
                {
                    Console.WriteLine($"🚨 Plugin unhealthy: {plugin.Name} - {health.Message}");
                    OnPluginUnhealthy?.Invoke(plugin, health);
                }
            }
            catch (Exception ex)
            {
                var health = new PluginHealthStatus
                {
                    Status = HealthStatus.Critical,
                    Message = $"Health check failed: {ex.Message}",
                    LastChecked = DateTime.UtcNow
                };
                
                _healthStatus[plugin.Id] = health;
                Console.WriteLine($"💥 Plugin health check failed: {plugin.Name}");
            }
        }
    }
    
    private async Task<PluginHealthStatus> CheckPluginHealthAsync(IBackendPlugin plugin)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check if plugin is still available
            if (!plugin.IsAvailable())
            {
                return new PluginHealthStatus
                {
                    Status = HealthStatus.Unhealthy,
                    Message = "Plugin hardware/runtime no longer available",
                    LastChecked = DateTime.UtcNow,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            
            // Try to create an accelerator (quick health check)
            var accelerator = await plugin.CreateAcceleratorAsync();
            await accelerator.DisposeAsync();
            
            stopwatch.Stop();
            
            return new PluginHealthStatus
            {
                Status = HealthStatus.Healthy,
                Message = "Plugin operational",
                LastChecked = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            return new PluginHealthStatus
            {
                Status = HealthStatus.Unhealthy,
                Message = ex.Message,
                LastChecked = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }
    
    public PluginHealthStatus GetHealthStatus(string pluginId)
    {
        return _healthStatus.TryGetValue(pluginId, out var status) 
            ? status 
            : new PluginHealthStatus { Status = HealthStatus.Unknown };
    }
    
    public Dictionary<string, PluginHealthStatus> GetAllHealthStatuses()
    {
        return new Dictionary<string, PluginHealthStatus>(_healthStatus);
    }
    
    public event Action<IBackendPlugin, PluginHealthStatus>? OnPluginUnhealthy;
}

public class PluginHealthStatus
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = "";
    public DateTime LastChecked { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

public enum HealthStatus
{
    Unknown,
    Healthy,
    Unhealthy,
    Critical
}
```

This comprehensive plugin system guide demonstrates the full capabilities of hot-reload, dependency management, health monitoring, and custom backend development in DotCompute Phase 3.