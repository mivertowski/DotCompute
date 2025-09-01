// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using global::System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Loaders.NuGet.Types;
using DotCompute.Plugins.Loaders.NuGet.Results;

namespace DotCompute.Plugins.Loaders;

/// <summary>
/// Advanced compatibility checker for NuGet plugins with framework and platform validation.
/// </summary>
public class CompatibilityChecker
{
    private readonly ILogger _logger;
    private readonly CompatibilitySettings _settings;
    private readonly ConcurrentDictionary<string, CompatibilityMatrix> _compatibilityCache = new();
    private readonly RuntimeEnvironment _runtimeEnvironment;

    public CompatibilityChecker(ILogger logger, CompatibilitySettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _runtimeEnvironment = new RuntimeEnvironment();
    }

    /// <summary>
    /// Validates compatibility of a plugin with the current runtime environment.
    /// </summary>
    public async Task ValidateAsync(NuGetPluginManifest manifest, NuGetPluginValidationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(result);

        _logger.LogInformation("Performing compatibility validation for plugin: {PluginId}", manifest.Id);

        try
        {
            var compatibilityResult = new CompatibilityCheckResult
            {
                PluginId = manifest.Id,
                PluginVersion = manifest.Version,
                CheckDate = DateTimeOffset.UtcNow
            };

            // Check framework compatibility
            await ValidateFrameworkCompatibilityAsync(manifest, compatibilityResult, cancellationToken);

            // Check platform compatibility
            await ValidatePlatformCompatibilityAsync(manifest, compatibilityResult, cancellationToken);

            // Check runtime compatibility
            await ValidateRuntimeCompatibilityAsync(manifest, compatibilityResult, cancellationToken);

            // Check assembly compatibility
            await ValidateAssemblyCompatibilityAsync(manifest, compatibilityResult, cancellationToken);

            // Check dependency compatibility
            await ValidateDependencyCompatibilityAsync(manifest, compatibilityResult, cancellationToken);

            result.CompatibilityCheckPassed = compatibilityResult.IsCompatible;

            // Add errors and warnings to the validation result
            result.Errors.AddRange(compatibilityResult.CompatibilityErrors);
            result.Warnings.AddRange(compatibilityResult.CompatibilityWarnings);

            _logger.LogInformation("Compatibility validation completed for plugin: {PluginId}. Compatible: {IsCompatible}",
                manifest.Id, compatibilityResult.IsCompatible);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compatibility validation failed for plugin: {PluginId}", manifest.Id);
            result.Errors.Add($"Compatibility validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates framework compatibility (.NET version, TFM, etc.).
    /// </summary>
    private async Task ValidateFrameworkCompatibilityAsync(NuGetPluginManifest manifest, CompatibilityCheckResult result, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        // Check minimum framework version
        if (!string.IsNullOrEmpty(manifest.MinFrameworkVersion))
        {
            if (!Version.TryParse(manifest.MinFrameworkVersion, out var minVersion))
            {
                result.CompatibilityErrors.Add($"Invalid minimum framework version format: {manifest.MinFrameworkVersion}");
                return;
            }

            var currentVersion = _runtimeEnvironment.FrameworkVersion;
            if (currentVersion < minVersion)
            {
                result.CompatibilityErrors.Add($"Plugin requires .NET {manifest.MinFrameworkVersion} but current runtime is {currentVersion}");
                result.FrameworkCompatibility.IsCompatible = false;
                result.FrameworkCompatibility.CurrentFramework = currentVersion.ToString();
                result.FrameworkCompatibility.RequiredFramework = manifest.MinFrameworkVersion;
                return;
            }
        }

        // Check target framework moniker (TFM)
        if (!string.IsNullOrEmpty(manifest.MinFrameworkVersion))
        {
            var compatibility = await CheckTargetFrameworkCompatibilityAsync(manifest.MinFrameworkVersion, cancellationToken);
            result.FrameworkCompatibility = compatibility;

            if (!compatibility.IsCompatible)
            {
                if (_settings.FailOnFrameworkMismatch)
                {
                    result.CompatibilityErrors.Add($"Target framework {manifest.MinFrameworkVersion} is not compatible with current runtime {_runtimeEnvironment.RuntimeIdentifier}");
                }
                else
                {
                    result.CompatibilityWarnings.Add($"Target framework {manifest.MinFrameworkVersion} may not be fully compatible with current runtime {_runtimeEnvironment.RuntimeIdentifier}");
                }
            }
        }

        result.FrameworkCompatibility.IsCompatible = result.CompatibilityErrors.Count == 0;
    }

    /// <summary>
    /// Validates platform compatibility (OS, architecture, etc.).
    /// </summary>
    private async Task ValidatePlatformCompatibilityAsync(NuGetPluginManifest manifest, CompatibilityCheckResult result, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        result.PlatformCompatibility = new PlatformCompatibilityInfo
        {
            CurrentPlatform = _runtimeEnvironment.OSDescription,
            CurrentArchitecture = _runtimeEnvironment.Architecture.ToString(),
            IsCompatible = true
        };

        // Check supported platforms
        if (manifest.SupportedPlatforms?.Any() == true)
        {
            var currentOS = _runtimeEnvironment.OSDescription;
            var currentArch = _runtimeEnvironment.Architecture.ToString();

            var isOSCompatible = manifest.SupportedPlatforms.Any(platform =>

                IsOperatingSystemCompatible(currentOS, platform));

            var isArchCompatible = manifest.SupportedPlatforms.Any(platform =>

                IsArchitectureCompatible(currentArch, platform));

            if (!isOSCompatible)
            {
                result.CompatibilityErrors.Add($"Plugin does not support current OS: {currentOS}. Supported platforms: {string.Join(", ", manifest.SupportedPlatforms)}");
                result.PlatformCompatibility.IsCompatible = false;
            }

            if (!isArchCompatible)
            {
                result.CompatibilityWarnings.Add($"Plugin may not support current architecture: {currentArch}");
            }

            result.PlatformCompatibility.SupportedPlatforms = manifest.SupportedPlatforms;
        }

        // Check for platform-specific features
        await CheckPlatformSpecificFeaturesAsync(manifest, result, cancellationToken);
    }

    /// <summary>
    /// Validates runtime compatibility (GC, AOT, etc.).
    /// </summary>
    private async Task ValidateRuntimeCompatibilityAsync(NuGetPluginManifest manifest, CompatibilityCheckResult result, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        result.RuntimeCompatibility = new RuntimeCompatibilityInfo
        {
            IsAotCompatible = true, // Will be determined below
            IsTrimmable = true,     // Will be determined below
            RequiresJit = false     // Will be determined below
        };

        // Check AOT compatibility
        if (_settings.CheckAotCompatibility && _runtimeEnvironment.IsAotRuntime)
        {
            var aotCompatibility = await CheckAotCompatibilityAsync(manifest, cancellationToken);
            result.RuntimeCompatibility.IsAotCompatible = aotCompatibility;

            if (!aotCompatibility)
            {
                if (_settings.FailOnAotIncompatibility)
                {
                    result.CompatibilityErrors.Add("Plugin is not compatible with AOT runtime");
                }
                else
                {
                    result.CompatibilityWarnings.Add("Plugin may not work correctly in AOT runtime");
                }
            }
        }

        // Check trimming compatibility
        if (_settings.CheckTrimmingCompatibility)
        {
            var trimmingCompatibility = await CheckTrimmingCompatibilityAsync(manifest, cancellationToken);
            result.RuntimeCompatibility.IsTrimmable = trimmingCompatibility;

            if (!trimmingCompatibility)
            {
                result.CompatibilityWarnings.Add("Plugin may not work correctly when trimmed");
            }
        }

        // Check JIT requirements
        var requiresJit = await CheckJitRequirementsAsync(manifest, cancellationToken);
        result.RuntimeCompatibility.RequiresJit = requiresJit;

        if (requiresJit && _runtimeEnvironment.IsAotRuntime)
        {
            result.CompatibilityErrors.Add("Plugin requires JIT compilation but running in AOT mode");
        }
    }

    /// <summary>
    /// Validates assembly-level compatibility.
    /// </summary>
    private async Task ValidateAssemblyCompatibilityAsync(NuGetPluginManifest manifest, CompatibilityCheckResult result, CancellationToken cancellationToken)
    {
        if (!File.Exists(manifest.AssemblyPath))
        {
            result.CompatibilityErrors.Add($"Assembly file not found: {manifest.AssemblyPath}");
            return;
        }

        try
        {
            var assemblyInfo = await AnalyzeAssemblyAsync(manifest.AssemblyPath, cancellationToken);
            result.AssemblyCompatibility = assemblyInfo;

            // Check assembly target framework
            if (!string.IsNullOrEmpty(assemblyInfo.TargetFramework))
            {
                var compatibility = await CheckTargetFrameworkCompatibilityAsync(assemblyInfo.TargetFramework, cancellationToken);
                if (!compatibility.IsCompatible)
                {
                    result.CompatibilityWarnings.Add($"Assembly target framework {assemblyInfo.TargetFramework} may not be fully compatible");
                }
            }

            // Check for unsafe code
            if (assemblyInfo.UsesUnsafeCode && !_settings.AllowUnsafeCode)
            {
                result.CompatibilityErrors.Add("Assembly uses unsafe code which is not allowed");
            }

            // Check for reflection usage
            if (assemblyInfo.UsesReflection && _runtimeEnvironment.IsAotRuntime)
            {
                result.CompatibilityWarnings.Add("Assembly uses reflection which may not work in AOT mode");
            }

            // Check for dynamic code generation
            if (assemblyInfo.UsesDynamicCodeGeneration && _runtimeEnvironment.IsAotRuntime)
            {
                result.CompatibilityErrors.Add("Assembly uses dynamic code generation which is not supported in AOT mode");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze assembly: {AssemblyPath}", manifest.AssemblyPath);
            result.CompatibilityErrors.Add($"Assembly analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates dependency compatibility.
    /// </summary>
    private async Task ValidateDependencyCompatibilityAsync(NuGetPluginManifest manifest, CompatibilityCheckResult result, CancellationToken cancellationToken)
    {
        if (manifest.Dependencies?.Any() != true)
        {
            return;
        }

        foreach (var dependency in manifest.Dependencies)
        {
            try
            {
                var depCompatibility = await CheckDependencyCompatibilityAsync(dependency, cancellationToken);
                result.DependencyCompatibility.Add(dependency.Id, depCompatibility);

                if (!depCompatibility.IsCompatible)
                {
                    result.CompatibilityErrors.Add($"Dependency {dependency.Id} is not compatible: {string.Join(", ", depCompatibility.Issues)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check compatibility for dependency: {DependencyId}", dependency.Id);
                result.CompatibilityWarnings.Add($"Could not verify compatibility for dependency: {dependency.Id}");
            }
        }
    }

    /// <summary>
    /// Checks target framework compatibility.
    /// </summary>
    private async Task<FrameworkCompatibilityInfo> CheckTargetFrameworkCompatibilityAsync(string targetFramework, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        var cacheKey = $"tfm_{targetFramework}";
        if (_compatibilityCache.TryGetValue(cacheKey, out var cached))
        {
            return cached.FrameworkCompatibility;
        }

        var compatibility = new FrameworkCompatibilityInfo
        {
            TargetFramework = targetFramework,
            CurrentFramework = _runtimeEnvironment.FrameworkDescription,
            IsCompatible = IsTargetFrameworkCompatible(targetFramework)
        };

        // Cache the result
        _ = _compatibilityCache.TryAdd(cacheKey, new CompatibilityMatrix { FrameworkCompatibility = compatibility });

        return compatibility;
    }

    /// <summary>
    /// Determines if a target framework is compatible with the current runtime.
    /// </summary>
    private bool IsTargetFrameworkCompatible(string targetFramework)
    {
        // Simplified compatibility check
        // In a real implementation, this would use NuGet.Frameworks library


        var currentFramework = _runtimeEnvironment.FrameworkDescription;

        // Handle common TFM patterns
        if (targetFramework.StartsWith("net9.0", StringComparison.OrdinalIgnoreCase) ||
            targetFramework.StartsWith("net8.0", StringComparison.OrdinalIgnoreCase) ||
            targetFramework.StartsWith("net7.0", StringComparison.OrdinalIgnoreCase) ||
            targetFramework.StartsWith("net6.0", StringComparison.OrdinalIgnoreCase))
        {
            return currentFramework.Contains(".NET 9.") ||

                   currentFramework.Contains(".NET 8.") ||
                   currentFramework.Contains(".NET 7.") ||
                   currentFramework.Contains(".NET 6.");
        }

        if (targetFramework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return true; // .NET Standard is generally compatible
        }

        // Conservative approach: assume compatible if we can't determine
        return true;
    }

    /// <summary>
    /// Checks if the current operating system is compatible with a platform specification.
    /// </summary>
    private static bool IsOperatingSystemCompatible(string currentOS, string platform)
    {
        var platformLower = platform.ToLowerInvariant();
        var osLower = currentOS.ToLowerInvariant();

        return platformLower switch
        {
            "windows" or "win" => osLower.Contains("windows"),
            "linux" => osLower.Contains("linux"),
            "macos" or "osx" => osLower.Contains("darwin") || osLower.Contains("macos"),
            "any" or "*" => true,
            _ => osLower.Contains(platformLower)
        };
    }

    /// <summary>
    /// Checks if the current architecture is compatible with a platform specification.
    /// </summary>
    private static bool IsArchitectureCompatible(string currentArch, string platform)
    {
        var platformLower = platform.ToLowerInvariant();
        var archLower = currentArch.ToLowerInvariant();

        if (platformLower.Contains("x64") || platformLower.Contains("x86_64"))
        {
            return archLower.Contains("x64") || archLower.Contains("x86_64");
        }

        if (platformLower.Contains("x86"))
        {
            return archLower.Contains("x86");
        }

        if (platformLower.Contains("arm64"))
        {
            return archLower.Contains("arm64");
        }

        if (platformLower.Contains("arm"))
        {
            return archLower.Contains("arm");
        }

        // Default to compatible if we can't determine
        return true;
    }

    /// <summary>
    /// Checks platform-specific features.
    /// </summary>
    private static async Task CheckPlatformSpecificFeaturesAsync(NuGetPluginManifest manifest, CompatibilityCheckResult result, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        // Check for Windows-specific features
        if (manifest.Configuration?.ContainsKey("RequiresWindows") == true &&

            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            result.CompatibilityErrors.Add("Plugin requires Windows platform");
        }

        // Check for Linux-specific features
        if (manifest.Configuration?.ContainsKey("RequiresLinux") == true &&

            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            result.CompatibilityErrors.Add("Plugin requires Linux platform");
        }

        // Check for macOS-specific features
        if (manifest.Configuration?.ContainsKey("RequiresMacOS") == true &&

            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            result.CompatibilityErrors.Add("Plugin requires macOS platform");
        }
    }

    /// <summary>
    /// Checks AOT compatibility.
    /// </summary>
    private static async Task<bool> CheckAotCompatibilityAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        // Check metadata for AOT compatibility markers
        if (manifest.Configuration?.ContainsKey("IsAotCompatible") == true)
        {
            return bool.TryParse(manifest.Configuration["IsAotCompatible"]?.ToString(), out var isCompatible) && isCompatible;
        }

        // Check for known AOT-incompatible features in metadata
        var aotIncompatibleFeatures = new[] { "UsesDynamicCode", "UsesReflection", "UsesEmit" };
        if (aotIncompatibleFeatures.Any(feature =>

            manifest.Configuration?.ContainsKey(feature) == true &&

            bool.TryParse(manifest.Configuration[feature]?.ToString(), out var uses) && uses))
        {
            return false;
        }

        // Default to true for now
        return true;
    }

    /// <summary>
    /// Checks trimming compatibility.
    /// </summary>
    private static async Task<bool> CheckTrimmingCompatibilityAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        // Check metadata for trimming compatibility
        if (manifest.Configuration?.ContainsKey("IsTrimmable") == true)
        {
            return bool.TryParse(manifest.Configuration["IsTrimmable"]?.ToString(), out var isTrimmable) && isTrimmable;
        }

        // Default to true
        return true;
    }

    /// <summary>
    /// Checks JIT compilation requirements.
    /// </summary>
    private static async Task<bool> CheckJitRequirementsAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        // Check for features that require JIT
        var jitRequiredFeatures = new[] { "UsesDynamicCode", "UsesEmit", "UsesExpression" };
        return jitRequiredFeatures.Any(feature =>

            manifest.Configuration?.ContainsKey(feature) == true &&

            bool.TryParse(manifest.Configuration[feature]?.ToString(), out var uses) && uses);
    }

    /// <summary>
    /// Analyzes an assembly for compatibility information.
    /// </summary>
    private async Task<AssemblyCompatibilityInfo> AnalyzeAssemblyAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var info = new AssemblyCompatibilityInfo();

        try
        {
            // Load assembly metadata for analysis
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Get target framework from assembly attributes

            var targetFrameworkAttr = assembly.GetCustomAttribute<global::System.Runtime.Versioning.TargetFrameworkAttribute>();
            if (targetFrameworkAttr != null)
            {
                info.TargetFramework = targetFrameworkAttr.FrameworkName;
            }

            // Check for unsafe code usage
            info.UsesUnsafeCode = assembly.GetTypes().Any(t =>

                t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                 .Any(m => m.GetMethodBody()?.LocalVariables?.Any(v => v.LocalType.IsPointer) == true));

            // Check for reflection usage (simplified)
            info.UsesReflection = assembly.GetTypes().Any(t =>

                t.GetMethods().Any(m => m.ReturnType == typeof(Type) ||

                                       m.GetParameters().Any(p => p.ParameterType == typeof(Type))));

            // Check for dynamic code generation
            var systemReflectionEmit = "System.Reflection.Emit";
            info.UsesDynamicCodeGeneration = assembly.GetReferencedAssemblies()
                .Any(a => a.Name?.Contains(systemReflectionEmit, StringComparison.OrdinalIgnoreCase) == true);

            info.IsCompatible = !info.UsesUnsafeCode; // Simplified compatibility check
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fully analyze assembly: {AssemblyPath}", assemblyPath);
            info.AnalysisErrors.Add($"Analysis error: {ex.Message}");
        }

        await Task.CompletedTask;
        return info;
    }

    /// <summary>
    /// Checks compatibility for a specific dependency.
    /// </summary>
    private async Task<DependencyCompatibilityInfo> CheckDependencyCompatibilityAsync(NuGetPackageDependency dependency, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async signature

        var info = new DependencyCompatibilityInfo
        {
            DependencyId = dependency.Id,
            VersionRange = dependency.VersionRange,
            IsCompatible = true
        };

        // Check if dependency has known compatibility issues
        var knownIssues = GetKnownCompatibilityIssues(dependency.Id, dependency.VersionRange);
        info.Issues.AddRange(knownIssues);
        info.IsCompatible = knownIssues.Count == 0;

        return info;
    }

    /// <summary>
    /// Gets known compatibility issues for a dependency.
    /// </summary>
    private static List<string> GetKnownCompatibilityIssues(string dependencyId, string versionRange)
    {
        // This would check against a database of known compatibility issues
        var issues = new List<string>();

        // Example known issues
        if (dependencyId.Equals("ProblematicPackage", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("Known to have issues with .NET 9.0");
        }

        return issues;
    }
}

/// <summary>
/// Settings for compatibility checking.
/// </summary>
public class CompatibilitySettings
{
    /// <summary>
    /// Gets or sets whether to fail on framework mismatch.
    /// </summary>
    public bool FailOnFrameworkMismatch { get; set; }


    /// <summary>
    /// Gets or sets whether to check AOT compatibility.
    /// </summary>
    public bool CheckAotCompatibility { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to fail on AOT incompatibility.
    /// </summary>
    public bool FailOnAotIncompatibility { get; set; }


    /// <summary>
    /// Gets or sets whether to check trimming compatibility.
    /// </summary>
    public bool CheckTrimmingCompatibility { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow unsafe code.
    /// </summary>
    public bool AllowUnsafeCode { get; set; }


    /// <summary>
    /// Gets or sets the compatibility check timeout.
    /// </summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Result of compatibility checking.
/// </summary>
public class CompatibilityCheckResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public string PluginId { get; set; } = "";

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string PluginVersion { get; set; } = "";

    /// <summary>
    /// Gets or sets the check date.
    /// </summary>
    public DateTimeOffset CheckDate { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin is compatible.
    /// </summary>
    public bool IsCompatible => CompatibilityErrors.Count == 0;

    /// <summary>
    /// Gets the compatibility errors.
    /// </summary>
    public List<string> CompatibilityErrors { get; } = [];

    /// <summary>
    /// Gets the compatibility warnings.
    /// </summary>
    public List<string> CompatibilityWarnings { get; } = [];

    /// <summary>
    /// Gets or sets the framework compatibility information.
    /// </summary>
    public FrameworkCompatibilityInfo FrameworkCompatibility { get; set; } = new();

    /// <summary>
    /// Gets or sets the platform compatibility information.
    /// </summary>
    public PlatformCompatibilityInfo PlatformCompatibility { get; set; } = new();

    /// <summary>
    /// Gets or sets the runtime compatibility information.
    /// </summary>
    public RuntimeCompatibilityInfo RuntimeCompatibility { get; set; } = new();

    /// <summary>
    /// Gets or sets the assembly compatibility information.
    /// </summary>
    public AssemblyCompatibilityInfo AssemblyCompatibility { get; set; } = new();

    /// <summary>
    /// Gets the dependency compatibility information.
    /// </summary>
    public Dictionary<string, DependencyCompatibilityInfo> DependencyCompatibility { get; } = [];
}

/// <summary>
/// Framework compatibility information.
/// </summary>
public class FrameworkCompatibilityInfo
{
    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the current framework.
    /// </summary>
    public string? CurrentFramework { get; set; }

    /// <summary>
    /// Gets or sets the required framework.
    /// </summary>
    public string? RequiredFramework { get; set; }

    /// <summary>
    /// Gets or sets whether the framework is compatible.
    /// </summary>
    public bool IsCompatible { get; set; }
}

/// <summary>
/// Platform compatibility information.
/// </summary>
public class PlatformCompatibilityInfo
{
    /// <summary>
    /// Gets or sets the current platform.
    /// </summary>
    public string? CurrentPlatform { get; set; }

    /// <summary>
    /// Gets or sets the current architecture.
    /// </summary>
    public string? CurrentArchitecture { get; set; }

    /// <summary>
    /// Gets or sets the supported platforms.
    /// </summary>
    public List<string>? SupportedPlatforms { get; set; }

    /// <summary>
    /// Gets or sets whether the platform is compatible.
    /// </summary>
    public bool IsCompatible { get; set; }
}

/// <summary>
/// Runtime compatibility information.
/// </summary>
public class RuntimeCompatibilityInfo
{
    /// <summary>
    /// Gets or sets whether the plugin is AOT compatible.
    /// </summary>
    public bool IsAotCompatible { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin is trimmable.
    /// </summary>
    public bool IsTrimmable { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin requires JIT compilation.
    /// </summary>
    public bool RequiresJit { get; set; }
}

/// <summary>
/// Assembly compatibility information.
/// </summary>
public class AssemblyCompatibilityInfo
{
    /// <summary>
    /// Gets or sets the target framework of the assembly.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly uses unsafe code.
    /// </summary>
    public bool UsesUnsafeCode { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly uses reflection.
    /// </summary>
    public bool UsesReflection { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly uses dynamic code generation.
    /// </summary>
    public bool UsesDynamicCodeGeneration { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly is compatible.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// Gets analysis errors.
    /// </summary>
    public List<string> AnalysisErrors { get; } = [];
}

/// <summary>
/// Dependency compatibility information.
/// </summary>
public class DependencyCompatibilityInfo
{
    /// <summary>
    /// Gets or sets the dependency ID.
    /// </summary>
    public string DependencyId { get; set; } = "";

    /// <summary>
    /// Gets or sets the version range.
    /// </summary>
    public string VersionRange { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the dependency is compatible.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// Gets the compatibility issues.
    /// </summary>
    public List<string> Issues { get; } = [];
}

/// <summary>
/// Compatibility matrix for caching results.
/// </summary>
internal class CompatibilityMatrix
{
    public FrameworkCompatibilityInfo FrameworkCompatibility { get; set; } = new();
}

/// <summary>
/// Runtime environment information.
/// </summary>
internal class RuntimeEnvironment
{
    public Version FrameworkVersion { get; } = Environment.Version;
    public string FrameworkDescription { get; } = RuntimeInformation.FrameworkDescription;
    public string OSDescription { get; } = RuntimeInformation.OSDescription;
    public Architecture Architecture { get; } = RuntimeInformation.OSArchitecture;
    public string RuntimeIdentifier { get; } = RuntimeInformation.RuntimeIdentifier;
    public bool IsAotRuntime { get; } = !global::System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled;
}