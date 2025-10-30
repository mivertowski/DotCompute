
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Abstractions.Security;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Models;
using DotCompute.Algorithms.Management.Services;
using DotCompute.Algorithms.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AuthenticodeValidator = DotCompute.Algorithms.Security.AuthenticodeValidator;
using LoadedAssemblyInfo = DotCompute.Algorithms.Management.Models.LoadedAssemblyInfo;
using MalwareScanningService = DotCompute.Algorithms.Security.MalwareScanningService;
using SecurityPolicy = DotCompute.Algorithms.Security.SecurityPolicy;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Advanced plugin loader with security validation, dependency resolution, and sandboxing capabilities.
    /// </summary>
    public sealed partial class PluginLoader : IAsyncDisposable
    {
        private readonly ILogger<PluginLoader> _logger;
        private readonly PluginLoaderOptions _options;
        private readonly ConcurrentDictionary<string, LoadedAssembly> _loadedAssemblies = new();
        private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
        private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private readonly RSA? _trustedPublicKey;
        private readonly SecurityPolicy _securityPolicy;
        private readonly AuthenticodeValidator _authenticodeValidator;
        private readonly MalwareScanningService _malwareScanner;
        private bool _disposed;

        /// <summary>
        /// Represents a loaded assembly with its context and validation state.
        /// </summary>
        private sealed class LoadedAssembly
        {
            /// <summary>
            /// Gets or sets the assembly.
            /// </summary>
            /// <value>The assembly.</value>
            public required Assembly Assembly { get; init; }
            /// <summary>
            /// Gets or sets the load context.
            /// </summary>
            /// <value>The load context.</value>
            public required PluginAssemblyLoadContext LoadContext { get; init; }
            /// <summary>
            /// Gets or sets the unified validation result.
            /// </summary>
            /// <value>The unified validation result.</value>
            public required SecurityValidationResult UnifiedValidationResult { get; init; }
            /// <summary>
            /// Gets or sets the load time.
            /// </summary>
            /// <value>The load time.</value>
            public required DateTime LoadTime { get; init; }
            /// <summary>
            /// Gets or sets the assembly path.
            /// </summary>
            /// <value>The assembly path.</value>
            public required string AssemblyPath { get; init; }
            /// <summary>
            /// Gets or sets a value indicating whether isolated.
            /// </summary>
            /// <value>The is isolated.</value>
            public bool IsIsolated { get; init; }
            /// <summary>
            /// Gets or sets the plugins.
            /// </summary>
            /// <value>The plugins.</value>
            public IList<IAlgorithmPlugin> Plugins { get; } = [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginLoader"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">Configuration options for the plugin loader.</param>
        public PluginLoader(ILogger<PluginLoader> logger, PluginLoaderOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new PluginLoaderOptions();

            // Initialize security components
            var securityLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityPolicy>.Instance;
            _securityPolicy = new SecurityPolicy(securityLogger);
            ConfigureSecurityPolicy();

            var authenticodeLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticodeValidator>.Instance;
            _authenticodeValidator = new AuthenticodeValidator(authenticodeLogger);

            var malwareOptions = new MalwareScanningOptions
            {
                EnableWindowsDefender = _options.SandboxOptions.RestrictNetworkAccess,
                MaxConcurrentScans = 2,
                ScanTimeout = TimeSpan.FromMinutes(1)
            };
            var malwareLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MalwareScanningService>.Instance;
            _malwareScanner = new MalwareScanningService(malwareLogger, malwareOptions);

            // Initialize trusted public key for signature validation if provided
            if (!string.IsNullOrEmpty(_options.TrustedPublicKeyXml))
            {
                try
                {
                    _trustedPublicKey = RSA.Create();
                    _trustedPublicKey.FromXmlString(_options.TrustedPublicKeyXml);
                    LogTrustedKeyLoaded();
                }
                catch (Exception ex)
                {
                    LogTrustedKeyLoadFailed(ex.Message);
                }
            }
        }

        #region Logger Messages

        [LoggerMessage(Level = LogLevel.Information, Message = "Loading assembly from path: {AssemblyPath}")]
        private partial void LogLoadingAssembly(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully loaded {PluginCount} plugins from assembly: {AssemblyName}")]
        private partial void LogAssemblyLoadedSuccessfully(int pluginCount, string assemblyName);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly validation failed for {AssemblyPath}: {Reason}")]
        private partial void LogAssemblyValidationFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load assembly {AssemblyPath}: {ErrorMessage}")]
        private partial void LogAssemblyLoadFailed(string assemblyPath, string errorMessage);

        [LoggerMessage(Level = LogLevel.Information, Message = "Creating isolated load context: {ContextName}")]
        private partial void LogCreatingIsolatedContext(string contextName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Digital signature validation passed for {AssemblyPath}")]
        private partial void LogSignatureValidationPassed(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Digital signature validation failed for {AssemblyPath}: {Reason}")]
        private partial void LogSignatureValidationFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Trusted public key loaded successfully")]
        private partial void LogTrustedKeyLoaded();

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load trusted public key: {Reason}")]
        private partial void LogTrustedKeyLoadFailed(string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Assembly unloaded successfully: {AssemblyName}")]
        private partial void LogAssemblyUnloaded(string assemblyName);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unload assembly {AssemblyName}: {Reason}")]
        private partial void LogAssemblyUnloadFailed(string assemblyName, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin instance created: {PluginType}")]
        private partial void LogPluginInstanceCreated(string pluginType);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create plugin instance: {PluginType}, {Reason}")]
        private partial void LogPluginInstanceCreationFailed(string pluginType, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Resolving dependencies for plugin: {PluginType}")]
        private partial void LogResolvingDependencies(string pluginType);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Dependency resolution failed for {PluginType}: {Dependency}")]
        private partial void LogDependencyResolutionFailed(string pluginType, string dependency);

        [LoggerMessage(Level = LogLevel.Information, Message = "Starting comprehensive security validation for: {AssemblyPath}")]
        private partial void LogComprehensiveValidationStarting(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Comprehensive security validation passed for: {AssemblyPath}")]
        private partial void LogComprehensiveValidationPassed(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Comprehensive security validation failed for: {AssemblyPath}, Reason: {Reason}")]
        private partial void LogComprehensiveValidationFailed(string assemblyPath, string reason);

        #endregion

        /// <summary>
        /// Loads an assembly and returns discovered plugins with full validation and isolation.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of loaded plugins.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Plugin loading requires dynamic assembly loading")]
        public async Task<PluginLoadResult> LoadPluginAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
            }

            await _loadingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                LogLoadingAssembly(assemblyPath);

                // Step 1: Security validation
                var validationResult = await ValidateAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                if (!validationResult.IsValid)
                {
                    LogAssemblyValidationFailed(assemblyPath, string.Join(", ", validationResult.Errors));
                    var result = new PluginLoadResult
                    {
                        Success = false,
                        UnifiedValidationResult = validationResult,
                        ErrorMessage = $"Assembly validation failed: {string.Join(", ", validationResult.Errors)}"
                    };
                    return result;
                }

                // Step 2: Create isolated load context
                var contextName = $"Plugin_{Path.GetFileNameWithoutExtension(assemblyPath)}_{Guid.NewGuid():N}";
                LogCreatingIsolatedContext(contextName);

                var loadContext = new PluginAssemblyLoadContext(contextName, assemblyPath, _options.EnableIsolation);

                try
                {
                    // Step 3: Load assembly in isolated context
                    var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                    var assemblyName = assembly.GetName().Name ?? "Unknown";

                    // Check for duplicate loading
                    if (_loadedAssemblies.ContainsKey(assemblyName))
                    {
                        loadContext.Unload();
                        var duplicateResult = new PluginLoadResult
                        {
                            Success = false,
                            UnifiedValidationResult = validationResult,
                            ErrorMessage = $"Assembly {assemblyName} is already loaded"
                        };
                        return duplicateResult;
                    }

                    // Step 4: Discover and instantiate plugins
                    var plugins = await DiscoverAndCreatePluginsAsync(assembly, cancellationToken).ConfigureAwait(false);

                    // Step 5: Register loaded assembly
                    var loadedAssembly = new LoadedAssembly
                    {
                        Assembly = assembly,
                        LoadContext = loadContext,
                        UnifiedValidationResult = validationResult,
                        LoadTime = DateTime.UtcNow,
                        AssemblyPath = assemblyPath,
                        IsIsolated = _options.EnableIsolation
                    };

                    foreach (var plugin in plugins)
                    {
                        loadedAssembly.Plugins.Add(plugin);
                    }
                    _ = _loadedAssemblies.TryAdd(assemblyName, loadedAssembly);
                    _ = _loadContexts.TryAdd(assemblyName, loadContext);

                    LogAssemblyLoadedSuccessfully(plugins.Count, assemblyName);

                    var result = new PluginLoadResult
                    {
                        Success = true
                    };
                    foreach (var plugin in plugins)
                    {
                        result.Plugins.Add(plugin);
                    }
                    result.UnifiedValidationResult = validationResult;
                    result.LoadContext = loadContext;
                    result.Assembly = assembly;
                    return result;
                }
                catch
                {
                    // Clean up load context on failure
                    loadContext.Unload();
                    throw;
                }
            }
            catch (Exception ex)
            {
                LogAssemblyLoadFailed(assemblyPath, ex.Message);
                var result = new PluginLoadResult
                {
                    Success = false
                };
                var errorValidationResult = new SecurityValidationResult
                {
                    IsValid = false
                };
                errorValidationResult.Errors.Add(ex.Message);
                result.UnifiedValidationResult = errorValidationResult;
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                _ = _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// Validates an assembly for security and integrity using comprehensive security checks.
        /// </summary>
        private async Task<SecurityValidationResult> ValidateAssemblyAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            var result = new SecurityValidationResult
            {
                IsValid = true
            };

            try
            {
                LogComprehensiveValidationStarting(assemblyPath);

                // Step 1: Basic file validation
                var fileInfo = new FileInfo(assemblyPath);

                // Size check
                if (fileInfo.Length > _options.MaxAssemblySize)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Assembly size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_options.MaxAssemblySize} bytes)");
                    return result;
                }

                // Step 2: Directory validation
                if (_options.AllowedDirectories.Count > 0)
                {
                    var assemblyDir = Path.GetDirectoryName(assemblyPath);
                    var isAllowed = _options.AllowedDirectories.Any(allowedDir =>
                        assemblyDir != null && assemblyDir.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase));

                    if (!isAllowed)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Assembly location is not in an allowed directory");
                        return result;
                    }
                }

                // Step 3: Digital signature validation using Authenticode
                if (_options.RequireSignedAssemblies)
                {
                    var authenticodeResult = await _authenticodeValidator.ValidateAsync(assemblyPath, cancellationToken);
                    if (!authenticodeResult.IsValid || authenticodeResult.TrustLevel < TrustLevel.Medium)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Authenticode validation failed: {authenticodeResult.ErrorMessage}");
                        return result;
                    }

                    LogSignatureValidationPassed(assemblyPath);
                    result.Metadata["AuthenticodeValid"] = true;
                    result.Metadata["TrustLevel"] = authenticodeResult.TrustLevel.ToString();
                    result.Metadata["SignerName"] = authenticodeResult.SignerName ?? "Unknown";
                }

                // Step 4: Strong name validation
                if (_options.RequireStrongName)
                {
                    var strongNameValid = await ValidateStrongNameAsync(assemblyPath);
                    if (!strongNameValid)
                    {
                        result.IsValid = false;
                        result.Errors.Add("Strong name validation failed");
                        return result;
                    }

                    result.Metadata["StrongNameValid"] = true;
                }

                // Step 5: Hash-based validation
                var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken);
                var assemblyHash = Convert.ToHexString(SHA256.HashData(assemblyBytes));

                if (_options.TrustedAssemblyHashes.Count > 0)
                {
                    if (!_options.TrustedAssemblyHashes.Contains(assemblyHash))
                    {
                        result.IsValid = false;
                        result.Errors.Add("Assembly hash is not in trusted hash list");
                        return result;
                    }
                }

                result.Metadata["AssemblyHash"] = assemblyHash;

                // Step 6: Malware scanning
                if (_options.EnableMalwareScanning)
                {
                    var malwareResult = await _malwareScanner.ScanAssemblyAsync(assemblyPath, cancellationToken);
                    if (!malwareResult.IsClean)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Malware detected: {malwareResult.ThreatDescription}");
                        return result;
                    }

                    result.Metadata["MalwareScanClean"] = true;
                    result.Metadata["ScanMethods"] = string.Join(", ", malwareResult.ScanMethods);
                }

                // Step 7: Security policy evaluation
                // Extract certificate and strong name for context initialization
                X509Certificate2? certificate = null;
                byte[]? strongNameKey = null;

                if (_options.RequireSignedAssemblies)
                {
                    var certInfo = _authenticodeValidator.ExtractCertificateInfo(assemblyPath);
                    if (certInfo != null)
                    {
                        try
                        {
                            certificate = X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
                        }
                        catch
                        {
                            // Certificate extraction failed, but Authenticode validation already passed
                        }
                    }
                }

                if (_options.RequireStrongName)
                {
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                        strongNameKey = assemblyName.GetPublicKey();
                    }
                    catch
                    {
                        // Strong name validation already handled this
                    }
                }

                var context = new SecurityEvaluationContext
                {
                    AssemblyPath = assemblyPath,
                    AssemblyBytes = System.Collections.Immutable.ImmutableArray.Create(assemblyBytes, 0, assemblyBytes.Length),
                    Certificate = certificate,
                    StrongNameKey = strongNameKey != null ? System.Collections.Immutable.ImmutableArray.Create(strongNameKey, 0, strongNameKey.Length) : default
                };

                // Certificate and strong name are set during context initialization
                // No need to modify init-only properties after construction

                var policyResult = _securityPolicy.EvaluateRules(context);
                if (!policyResult.IsAllowed)
                {
                    result.IsValid = false;
                    foreach (var violation in policyResult.Violations)
                    {
                        result.Errors.Add(violation);
                    }
                    return result;
                }

                foreach (var warning in policyResult.Warnings)
                {
                    result.Warnings.Add(warning);
                }
                result.Metadata["SecurityLevel"] = policyResult.SecurityLevel.ToString();

                LogComprehensiveValidationPassed(assemblyPath);
                return result;
            }
            catch (Exception ex)
            {
                LogComprehensiveValidationFailed(assemblyPath, ex.Message);
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates strong name signature of an assembly.
        /// </summary>
        private static Task<bool> ValidateStrongNameAsync(string assemblyPath)
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                var publicKey = assemblyName.GetPublicKey();

                // Check if assembly has a public key (strong name)
                if (publicKey == null || publicKey.Length == 0)
                {
                    return Task.FromResult(false);
                }

                // Basic validation: ensure key is of reasonable size
                if (publicKey.Length < 160) // Minimum for RSA-1024
                {
                    return Task.FromResult(false);
                }

                // Additional validation: check if public key token is present
                var publicKeyToken = assemblyName.GetPublicKeyToken();
                if (publicKeyToken == null || publicKeyToken.Length != 8)
                {
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Computes SHA-256 hash of an assembly.
        /// </summary>
        private static async Task<string> ComputeAssemblyHashAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(hashBytes);
        }

        /// <summary>
        /// Discovers and creates plugin instances from an assembly.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Plugin discovery requires type enumeration")]
        [UnconditionalSuppressMessage("Trimming", "IL2072:DynamicallyAccessedMembers",
            Justification = "Plugin types are discovered from assemblies and guaranteed to implement IAlgorithmPlugin")]
        private async Task<List<IAlgorithmPlugin>> DiscoverAndCreatePluginsAsync(Assembly assembly, CancellationToken cancellationToken)
        {
            var plugins = new List<IAlgorithmPlugin>();

            try
            {
                // Discover plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t))
                    .ToList();

                // Create instances with dependency resolution
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        LogResolvingDependencies(pluginType.FullName ?? pluginType.Name);

                        var plugin = await CreatePluginInstanceWithDependenciesAsync(pluginType, cancellationToken).ConfigureAwait(false);
                        if (plugin != null)
                        {
                            plugins.Add(plugin);
                            LogPluginInstanceCreated(pluginType.FullName ?? pluginType.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogPluginInstanceCreationFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
                    }
                }

                return plugins;
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Handle type loading errors gracefully
                foreach (var loaderException in ex.LoaderExceptions.Where(e => e != null))
                {
                    LogPluginInstanceCreationFailed("Multiple types", loaderException!.Message);
                }

                return plugins;
            }
        }

        /// <summary>
        /// Creates a plugin instance with dependency resolution.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin instantiation requires dynamic type handling")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Generic plugin instantiation required by design")]
        private Task<IAlgorithmPlugin?> CreatePluginInstanceWithDependenciesAsync(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            Type pluginType,
            CancellationToken cancellationToken)
        {
            try
            {
                // Try different constructor patterns
                var constructors = pluginType.GetConstructors().OrderBy(c => c.GetParameters().Length).ToList();

                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();

                    if (parameters.Length == 0)
                    {
                        // Parameterless constructor
                        return Task.FromResult(constructor.Invoke(null) as IAlgorithmPlugin);
                    }

                    if (parameters.Length == 1 && parameters[0].ParameterType.IsGenericType &&
                        parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>))
                    {
                        // Constructor with logger
                        var loggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(pluginType);
                        var logger = Activator.CreateInstance(loggerType);
                        return Task.FromResult(constructor.Invoke([logger]) as IAlgorithmPlugin);
                    }

                    // Support for more complex dependency injection patterns
                    if (TryCreateInstanceWithServiceProvider(pluginType, out var serviceInstance))
                    {
                        return Task.FromResult(serviceInstance);
                    }

                    // Try constructor with configuration parameter
                    if (TryCreateInstanceWithConfiguration(pluginType, out var configInstance))
                    {
                        return Task.FromResult(configInstance);
                    }

                    // Try constructor with multiple common dependencies
                    if (TryCreateInstanceWithCommonDependencies(pluginType, out var dependencyInstance))
                    {
                        return Task.FromResult(dependencyInstance);
                    }
                }

                // Fallback to Activator.CreateInstance
                return Task.FromResult(Activator.CreateInstance(pluginType) as IAlgorithmPlugin);
            }
            catch (Exception ex)
            {
                LogDependencyResolutionFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
                return Task.FromResult<IAlgorithmPlugin?>(null);
            }
        }

        /// <summary>
        /// Unloads an assembly and its associated plugins.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to unload.</param>
        /// <returns>True if successfully unloaded; otherwise, false.</returns>
        public async Task<bool> UnloadAssemblyAsync(string assemblyName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_loadedAssemblies.TryRemove(assemblyName, out var loadedAssembly) ||
                !_loadContexts.TryRemove(assemblyName, out var loadContext))
            {
                return false;
            }

            try
            {
                // Dispose all plugins first
                foreach (var plugin in loadedAssembly.Plugins)
                {
                    await plugin.DisposeAsync().ConfigureAwait(false);
                }

                // Unload the assembly context
                loadContext.Unload();

                LogAssemblyUnloaded(assemblyName);
                return true;
            }
            catch (Exception ex)
            {
                LogAssemblyUnloadFailed(assemblyName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets information about all loaded assemblies.
        /// </summary>
        /// <returns>Collection of loaded assembly information.</returns>
        public IEnumerable<LoadedAssemblyInfo> GetLoadedAssemblies()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _loadedAssemblies.Values.Select(la =>
            {
                var info = new LoadedAssemblyInfo
                {
                    AssemblyName = la.Assembly.GetName().Name ?? "Unknown",
                    AssemblyPath = la.AssemblyPath,
                    LoadTime = la.LoadTime,
                    IsIsolated = la.IsIsolated,
                    UnifiedValidationResult = la.UnifiedValidationResult,
                    PluginCount = la.Plugins.Count
                };

                foreach (var p in la.Plugins)
                {
                    info.Plugins.Add(new PluginInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Version = p.Version,
                        Description = p.Description
                    });
                }

                return info;
            });
        }

        /// <summary>
        /// Configures the security policy from options.
        /// </summary>
        private void ConfigureSecurityPolicy()
        {
            _securityPolicy.RequireDigitalSignature = _options.RequireSignedAssemblies;
            _securityPolicy.RequireStrongName = _options.RequireStrongName;
            _securityPolicy.MaxAssemblySize = _options.MaxAssemblySize;
            _securityPolicy.EnableMalwareScanning = _options.EnableMalwareScanning;
            _securityPolicy.EnableMetadataAnalysis = true;

            // Configure directory policies
            foreach (var directory in _options.AllowedDirectories)
            {
                _securityPolicy.DirectoryPolicies[directory] = SecurityLevel.High;
            }

            // Add blocklist rules
            if (_options.TrustedAssemblyHashes.Count > 0)
            {
                var blocklistRule = new BlocklistSecurityRule(
                    [], // No blocked hashes by default
                    []  // No blocked names by default
                );
                _securityPolicy.AddSecurityRule("Blocklist", blocklistRule);
            }
        }

        /// <summary>
        /// Attempts to create a plugin instance using a service provider pattern.
        /// </summary>
        private bool TryCreateInstanceWithServiceProvider(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type pluginType,
            out IAlgorithmPlugin? instance)
        {
            instance = null;

            try
            {
                var serviceProviderConstructor = pluginType.GetConstructor([typeof(IServiceProvider)]);
                if (serviceProviderConstructor != null)
                {
                    // Create a minimal service provider for basic services
                    var serviceProvider = new MinimalServiceProvider();
                    instance = serviceProviderConstructor.Invoke([serviceProvider]) as IAlgorithmPlugin;
                    return instance != null;
                }
            }
            catch (Exception ex)
            {
                LogDependencyResolutionFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Attempts to create a plugin instance with configuration parameter.
        /// </summary>
        private bool TryCreateInstanceWithConfiguration(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type pluginType,
            out IAlgorithmPlugin? instance)
        {
            instance = null;

            try
            {
                var configConstructor = pluginType.GetConstructor([typeof(IConfiguration)]);
                if (configConstructor != null)
                {
                    // Create a minimal configuration for plugin initialization
                    var configuration = new MinimalConfiguration();
                    instance = configConstructor.Invoke([configuration]) as IAlgorithmPlugin;
                    return instance != null;
                }
            }
            catch (Exception ex)
            {
                LogDependencyResolutionFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Attempts to create a plugin instance with common dependencies.
        /// </summary>
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Generic logger instantiation required by design")]
        private bool TryCreateInstanceWithCommonDependencies(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type pluginType,
            out IAlgorithmPlugin? instance)
        {
            instance = null;

            try
            {
                var constructors = pluginType.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .ToList();

                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    var parameterInstances = new object[parameters.Length];
                    var canInstantiate = true;

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;

                        if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(ILogger<>))
                        {
                            var loggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(pluginType);
                            parameterInstances[i] = Activator.CreateInstance(loggerType)!;
                        }
                        else if (paramType == typeof(IServiceProvider))
                        {
                            parameterInstances[i] = new MinimalServiceProvider();
                        }
                        else if (paramType == typeof(IConfiguration))
                        {
                            parameterInstances[i] = new MinimalConfiguration();
                        }
                        else if (paramType.IsInterface && paramType.Assembly == pluginType.Assembly)
                        {
                            // Try to create a mock or default implementation for plugin-specific interfaces
                            var defaultImpl = CreateDefaultImplementation(paramType);
                            if (defaultImpl == null)
                            {
                                canInstantiate = false;
                                break;
                            }
                            parameterInstances[i] = defaultImpl;
                        }
                        else
                        {
                            // Cannot resolve this parameter
                            canInstantiate = false;
                            break;
                        }
                    }

                    if (canInstantiate)
                    {
                        instance = constructor.Invoke(parameterInstances) as IAlgorithmPlugin;
                        if (instance != null)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDependencyResolutionFailed(pluginType.FullName ?? pluginType.Name, ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Creates a default implementation for plugin-specific interfaces.
        /// </summary>
        private static object? CreateDefaultImplementation(Type interfaceType)
        {
            try
            {
                // For plugin-specific interfaces, we can't create meaningful defaults
                // Return null to indicate we can't resolve this dependency
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Dispose security components
                _authenticodeValidator?.Dispose();
                _malwareScanner?.Dispose();

                // Unload all assemblies
                var unloadTasks = _loadedAssemblies.Keys.Select(UnloadAssemblyAsync);
                _ = await Task.WhenAll(unloadTasks).ConfigureAwait(false);

                _loadedAssemblies.Clear();
                _loadContexts.Clear();

                _loadingSemaphore.Dispose();
                _trustedPublicKey?.Dispose();
            }
        }
    }
}
