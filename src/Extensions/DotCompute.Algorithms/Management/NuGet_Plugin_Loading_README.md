# NuGet Plugin Loading for DotCompute

This document describes the comprehensive NuGet plugin loading system implemented for DotCompute. The system provides production-ready functionality for loading algorithm plugins from NuGet packages with full dependency resolution, security validation, caching, and version management.

## Overview

The NuGet plugin loading system consists of several key components:

- **`NuGetPluginLoader`**: Core class handling NuGet package operations
- **`AlgorithmPluginManager`**: Integration point with DotCompute's plugin system
- **Security validation**: Comprehensive package and assembly validation
- **Caching system**: Efficient package and extraction caching
- **Dependency resolution**: Automatic handling of package dependencies

## Features

### 🔧 Core Functionality

- **Local Package Loading**: Load plugins from .nupkg files on disk
- **Remote Package Loading**: Download and load from NuGet.org and custom feeds
- **Version Management**: Specific version loading and automatic updates
- **Framework Targeting**: Automatic selection of compatible assemblies
- **Dependency Resolution**: Recursive dependency loading and conflict resolution

### 🛡️ Security & Validation

- **Digital Signature Verification**: Authenticode signature validation
- **Strong Name Validation**: Assembly strong name checking
- **Malware Scanning**: Integration with Windows Defender and custom scanners
- **Security Policy Enforcement**: Configurable security rules and policies
- **Package Integrity**: Hash-based integrity verification
- **Trusted Publisher Validation**: Publisher whitelist enforcement

### ⚡ Performance & Caching

- **Multi-level Caching**: Package download and extraction caching
- **Concurrent Downloads**: Parallel package downloading
- **Framework-specific Caching**: Optimized per-framework assembly selection
- **Cache Management**: Automatic expiration and manual cleanup
- **Memory-efficient Processing**: Streaming package operations

### 📊 Monitoring & Diagnostics

- **Comprehensive Logging**: Detailed operation logging with performance metrics
- **Package Validation**: Pre-loading package analysis and validation
- **Health Monitoring**: Package and plugin health tracking
- **Performance Metrics**: Load times, cache hit rates, and resource usage

## Quick Start

### Basic Usage

```csharp
// Initialize plugin manager with NuGet support
var options = new AlgorithmPluginManagerOptions
{
    EnableSecurityValidation = true,
    RequireDigitalSignature = true,
    EnableMalwareScanning = true
};

var pluginManager = new AlgorithmPluginManager(accelerator, logger, options);

// Load plugins from NuGet package
var pluginCount = await pluginManager.LoadPluginsFromNuGetPackageAsync(
    "MyCompany.AlgorithmPlugins", 
    "net9.0");

Console.WriteLine($"Loaded {pluginCount} plugins");
```

### Advanced Configuration

```csharp
// Create custom NuGet loader with specific settings
var nugetOptions = new NuGetPluginLoaderOptions
{
    CacheDirectory = @"C:\MyApp\PluginCache",
    CacheExpiration = TimeSpan.FromDays(7),
    DefaultTargetFramework = "net9.0",
    IncludePrerelease = false,
    MaxConcurrentDownloads = 5,
    EnableSecurityValidation = true,
    RequirePackageSignature = true,
    MaxAssemblySize = 50 * 1024 * 1024, // 50 MB
    MinimumSecurityLevel = SecurityLevel.High
};

// Configure trusted sources and allowed packages
nugetOptions.TrustedSources.Add("https://my-private-feed.com/nuget");
nugetOptions.AllowedPackagePrefixes.Add("MyCompany.");
nugetOptions.BlockedPackages.Add("UntrustedPackage");

using var loader = new NuGetPluginLoader(logger, nugetOptions);
```

## API Reference

### AlgorithmPluginManager Methods

#### LoadPluginsFromNuGetPackageAsync

Loads plugins from a NuGet package (local .nupkg file or package ID).

```csharp
public async Task<int> LoadPluginsFromNuGetPackageAsync(
    string packageSource, 
    string? targetFramework = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `packageSource`: Path to .nupkg file, package ID, or "PackageId:Version"
- `targetFramework`: Target framework (e.g., "net9.0", "net8.0")
- `cancellationToken`: Cancellation token

**Returns:** Number of plugins loaded

**Examples:**
```csharp
// Load from local file
await pluginManager.LoadPluginsFromNuGetPackageAsync(
    @"C:\packages\MyPlugin.1.0.0.nupkg");

// Load latest version from NuGet.org
await pluginManager.LoadPluginsFromNuGetPackageAsync("MyPlugin");

// Load specific version
await pluginManager.LoadPluginsFromNuGetPackageAsync("MyPlugin:2.1.0");
```

#### UpdateNuGetPackageAsync

Updates a NuGet package to the latest version.

```csharp
public async Task<int> UpdateNuGetPackageAsync(
    string packageId,
    string? targetFramework = null,
    CancellationToken cancellationToken = default)
```

#### ValidateNuGetPackageAsync

Validates a NuGet package before loading.

```csharp
public async Task<NuGetValidationResult> ValidateNuGetPackageAsync(
    string packageSource,
    string? targetFramework = null,
    CancellationToken cancellationToken = default)
```

#### Cache Management

```csharp
// Clear cache
await pluginManager.ClearNuGetCacheAsync(TimeSpan.FromDays(1));

// Get cached packages
var cached = await pluginManager.GetCachedNuGetPackagesAsync();
```

### NuGetPluginLoader Methods

#### LoadPackageAsync

Core method for loading NuGet packages.

```csharp
public async Task<NuGetLoadResult> LoadPackageAsync(
    string packageSource, 
    string? targetFramework = null,
    CancellationToken cancellationToken = default)
```

#### UpdatePackageAsync

Updates a package to the latest version.

```csharp
public async Task<NuGetLoadResult> UpdatePackageAsync(
    string packageId,
    string? targetFramework = null,
    CancellationToken cancellationToken = default)
```

#### Cache Operations

```csharp
// Get cached package information
CachedPackageInfo[] GetCachedPackages()

// Clear cache with optional age filter
Task ClearCacheAsync(TimeSpan? olderThan = null)
```

## Configuration Options

### NuGetPluginLoaderOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CacheDirectory` | string | Temp directory | Directory for package cache |
| `CacheExpiration` | TimeSpan | 7 days | Cache expiration time |
| `DefaultTargetFramework` | string | "net9.0" | Default target framework |
| `IncludePrerelease` | bool | false | Include prerelease packages |
| `MaxConcurrentDownloads` | int | 3 | Max concurrent downloads |
| `EnableSecurityValidation` | bool | true | Enable security validation |
| `RequirePackageSignature` | bool | true | Require package signatures |
| `EnableMalwareScanning` | bool | true | Enable malware scanning |
| `MaxAssemblySize` | long | 50 MB | Maximum assembly size |
| `MinimumSecurityLevel` | SecurityLevel | Medium | Minimum security level |

### AlgorithmPluginManagerOptions

Extends the plugin manager with NuGet-specific security settings:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequireDigitalSignature` | bool | true | Require Authenticode signatures |
| `RequireStrongName` | bool | true | Require strong name signatures |
| `EnableMalwareScanning` | bool | true | Enable malware scanning |
| `TrustedPublishers` | List\<string> | Empty | Trusted package publishers |
| `AllowedPluginDirectories` | List\<string> | Empty | Allowed extraction directories |

## Security Model

### Multi-layer Security Validation

1. **Package-level Security**:
   - NuGet package signature verification
   - Package integrity checking (SHA256)
   - Trusted source validation
   - Size and content validation

2. **Assembly-level Security**:
   - Authenticode signature verification
   - Strong name validation
   - Malware scanning integration
   - Security policy evaluation

3. **Runtime Security**:
   - Isolated assembly loading contexts
   - Permission-restricted execution
   - Resource usage monitoring
   - Plugin health checking

### Security Configuration Examples

```csharp
// High security configuration
var options = new NuGetPluginLoaderOptions
{
    EnableSecurityValidation = true,
    RequirePackageSignature = true,
    MinimumSecurityLevel = SecurityLevel.High,
    MaxAssemblySize = 10 * 1024 * 1024, // 10 MB limit
};

// Configure trusted publishers
options.AllowedPackagePrefixes.Add("Microsoft.");
options.AllowedPackagePrefixes.Add("System.");
options.AllowedPackagePrefixes.Add("MyTrustedCompany.");

// Block specific packages
options.BlockedPackages.Add("KnownMaliciousPackage");
```

### Security Policy Integration

```csharp
// Custom security policy
var pluginOptions = new AlgorithmPluginManagerOptions
{
    RequireDigitalSignature = true,
    RequireStrongName = true,
    EnableMalwareScanning = true,
    MinimumSecurityLevel = SecurityLevel.High
};

// Add trusted publishers
pluginOptions.TrustedPublishers.Add("CN=MyCompany, O=MyOrg");
pluginOptions.TrustedPublishers.Add("CN=Microsoft Corporation");
```

## Performance Optimization

### Caching Strategy

The system implements a multi-level caching strategy:

1. **Download Cache**: Downloaded .nupkg files
2. **Extraction Cache**: Extracted package contents
3. **Assembly Cache**: Framework-specific assembly selections
4. **Metadata Cache**: Package manifest and dependency information

### Concurrent Operations

```csharp
// Configure for high-throughput scenarios
var options = new NuGetPluginLoaderOptions
{
    MaxConcurrentDownloads = 10,
    CacheDirectory = @"D:\FastSSD\PluginCache", // Use SSD for better I/O
};

// Batch loading for better performance
var packages = new[] { "Package1", "Package2", "Package3" };
var loadTasks = packages.Select(pkg => 
    pluginManager.LoadPluginsFromNuGetPackageAsync(pkg));
var results = await Task.WhenAll(loadTasks);
```

### Memory Management

```csharp
// Configure memory limits
var options = new NuGetPluginLoaderOptions
{
    MaxAssemblySize = 25 * 1024 * 1024, // 25 MB per assembly
    CacheExpiration = TimeSpan.FromHours(1), // Shorter cache for memory
};

// Periodic cache cleanup
using var timer = new Timer(async _ =>
{
    await pluginManager.ClearNuGetCacheAsync(TimeSpan.FromHours(1));
}, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
```

## Error Handling

### Common Exception Types

| Exception | Cause | Handling |
|-----------|-------|----------|
| `FileNotFoundException` | Package file not found | Check file path and permissions |
| `InvalidOperationException` | Invalid package format | Validate package integrity |
| `UnauthorizedAccessException` | Security validation failed | Check security policies |
| `NotSupportedException` | Framework incompatibility | Update target framework |
| `OutOfMemoryException` | Package too large | Increase memory limits |

### Error Handling Patterns

```csharp
try
{
    var result = await pluginManager.LoadPluginsFromNuGetPackageAsync(packageId);
    logger.LogInformation("Loaded {Count} plugins", result);
}
catch (FileNotFoundException ex)
{
    logger.LogError("Package not found: {Message}", ex.Message);
    // Handle missing package
}
catch (UnauthorizedAccessException ex)
{
    logger.LogError("Security validation failed: {Message}", ex.Message);
    // Handle security failure
}
catch (InvalidOperationException ex)
{
    logger.LogError("Invalid package: {Message}", ex.Message);
    // Handle invalid package
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error loading package");
    // Handle unexpected errors
}
```

## Best Practices

### 1. Security First

- Always enable security validation in production
- Use trusted sources and publishers only
- Regularly update security policies
- Monitor for security warnings and violations

### 2. Performance Optimization

- Use SSD storage for cache directory
- Configure appropriate cache expiration times
- Use concurrent loading for multiple packages
- Monitor memory usage and set appropriate limits

### 3. Error Handling

- Implement comprehensive exception handling
- Log all security-related events
- Provide user-friendly error messages
- Implement retry logic for transient failures

### 4. Monitoring and Diagnostics

- Enable comprehensive logging
- Monitor cache hit rates and performance
- Track plugin health and execution metrics
- Set up alerts for security violations

### 5. Version Management

- Pin to specific versions for production
- Test package updates in staging environments
- Maintain compatibility matrices
- Use semantic versioning practices

## Examples

### Complete Example Application

```csharp
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management;
using Microsoft.Extensions.Logging;

public class PluginLoadingService : IDisposable
{
    private readonly AlgorithmPluginManager _pluginManager;
    private readonly ILogger<PluginLoadingService> _logger;

    public PluginLoadingService(
        IAccelerator accelerator, 
        ILogger<PluginLoadingService> logger)
    {
        _logger = logger;
        
        var options = new AlgorithmPluginManagerOptions
        {
            EnablePluginIsolation = true,
            EnableSecurityValidation = true,
            RequireDigitalSignature = true,
            EnableMalwareScanning = true,
            HealthCheckInterval = TimeSpan.FromMinutes(5)
        };

        options.TrustedPublishers.Add("CN=MyCompany");
        options.AllowedPluginDirectories.Add(
            Path.Combine(Environment.CurrentDirectory, "plugins"));

        _pluginManager = new AlgorithmPluginManager(accelerator, logger, options);
    }

    public async Task LoadAlgorithmsAsync()
    {
        var packages = new[]
        {
            "MyCompany.ImageProcessing",
            "MyCompany.MachineLearning", 
            "MyCompany.SignalProcessing"
        };

        foreach (var package in packages)
        {
            try
            {
                _logger.LogInformation("Loading package: {Package}", package);
                
                // Validate before loading
                var validation = await _pluginManager.ValidateNuGetPackageAsync(package);
                if (!validation.IsValid)
                {
                    _logger.LogError("Package validation failed: {Error}", 
                        validation.ValidationError);
                    continue;
                }

                // Load the package
                var count = await _pluginManager.LoadPluginsFromNuGetPackageAsync(package);
                _logger.LogInformation("Loaded {Count} plugins from {Package}", 
                    count, package);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load package: {Package}", package);
            }
        }

        // Display loaded plugins
        var plugins = _pluginManager.GetPluginInfo();
        _logger.LogInformation("Total plugins available: {Count}", plugins.Count());
    }

    public async Task ExecuteAlgorithmAsync(string pluginId, object[] inputs)
    {
        try
        {
            var result = await _pluginManager.ExecutePluginAsync(pluginId, inputs);
            _logger.LogInformation("Algorithm executed successfully: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Algorithm execution failed: {PluginId}", pluginId);
        }
    }

    public void Dispose()
    {
        _pluginManager?.DisposeAsync().AsTask().Wait();
    }
}
```

## Testing

The implementation includes comprehensive tests in `NuGetPluginLoaderTests.cs`:

- Local package loading tests
- Remote package resolution tests
- Framework compatibility tests
- Security validation tests
- Caching functionality tests
- Performance benchmarks
- Error handling tests

Run tests with:

```bash
dotnet run --project NuGetPluginLoaderTests.cs
dotnet run --project NuGetPluginLoaderTests.cs -- --performance
```

## Integration Notes

### Required NuGet Packages

The following NuGet packages are required and automatically added to the project:

```xml
<PackageReference Include="NuGet.Common" Version="6.12.1" />
<PackageReference Include="NuGet.Configuration" Version="6.12.1" />
<PackageReference Include="NuGet.DependencyResolver.Core" Version="6.12.1" />
<PackageReference Include="NuGet.Frameworks" Version="6.12.1" />
<PackageReference Include="NuGet.PackageManagement" Version="6.12.1" />
<PackageReference Include="NuGet.Packaging" Version="6.12.1" />
<PackageReference Include="NuGet.Protocol" Version="6.12.1" />
<PackageReference Include="NuGet.Resolver" Version="6.12.1" />
<PackageReference Include="NuGet.Versioning" Version="6.12.1" />
```

### Platform Compatibility

- **Windows**: Full functionality including Windows Defender integration
- **Linux**: Core functionality with reduced security scanning
- **macOS**: Core functionality with reduced security scanning
- **.NET 9.0+**: Primary target platform
- **.NET 8.0+**: Supported with reduced features

### Known Limitations

1. Package signing validation requires Windows for full functionality
2. Malware scanning integration is Windows-specific
3. Some security features require elevated privileges
4. Large packages may require increased memory limits
5. Complex dependency graphs may impact loading performance

## Support

For issues, questions, or contributions:

1. Check the existing documentation and examples
2. Review the test cases for usage patterns
3. Enable detailed logging for troubleshooting
4. Submit issues with reproduction steps and logs

## Roadmap

Future enhancements planned:

- **Enhanced Dependency Resolution**: More sophisticated version conflict resolution
- **Package Source Management**: GUI for managing custom feeds
- **Plugin Marketplace Integration**: Direct integration with plugin marketplaces
- **Automated Security Scanning**: Integration with additional security scanners
- **Performance Optimizations**: Further caching and loading optimizations
- **Cross-platform Security**: Enhanced security features for Linux/macOS

---

This NuGet plugin loading system provides a robust, secure, and performant foundation for loading algorithm plugins in DotCompute applications. The comprehensive feature set, security model, and extensive testing ensure production readiness while maintaining flexibility for various deployment scenarios.