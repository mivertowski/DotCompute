
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#nullable disable

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Types.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles validation of algorithm plugins including security, compatibility, and integrity checks.
    /// </summary>
    public sealed partial class AlgorithmPluginValidator(ILogger<AlgorithmPluginValidator> logger, AlgorithmPluginManagerOptions options) : IDisposable, IAsyncDisposable
    {
        private readonly ILogger<AlgorithmPluginValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
        private readonly ConcurrentDictionary<string, ValidationResult> _validationCache = new();
        // private readonly AssemblyValidator _assemblyValidator; // TODO: Implement security validation
        private readonly MalwareScanner _malwareScanner = new(logger);

        /// <summary>
        /// Validates a plugin assembly before loading.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validation result.</returns>
        public async Task<ValidationResult> ValidateAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

            if (!File.Exists(assemblyPath))
            {
                return ValidationResult.Failure("Assembly file not found", ValidationSeverity.Critical);
            }

            // Check cache first
            var cacheKey = $"{assemblyPath}:{GetFileHash(assemblyPath)}";
            if (_validationCache.TryGetValue(cacheKey, out var cachedResult))
            {
                LogValidationCacheHit(assemblyPath);
                return cachedResult;
            }

            LogValidatingAssembly(assemblyPath);
            var validationTasks = new List<Task<ValidationResult>>
            {
                // Basic file validation
                ValidateFileBasicsAsync(assemblyPath, cancellationToken)
            };

            // Security validation - TODO: Implement security validation classes
            if (_options.EnableSecurityValidation)
            {
                // validationTasks.Add(_assemblyValidator.ValidateAssemblySecurityAsync(assemblyPath, cancellationToken));
                // Placeholder: Add basic security validation warning until security classes are implemented
                validationTasks.Add(Task.FromResult(ValidationResult.Warning(
                    "Security validation not yet implemented - plugin loaded without security checks",
                    ValidationSeverity.Warning)));
            }

            // Malware scanning
            if (_options.EnableMalwareScanning)
            {
                validationTasks.Add(ValidateAssemblyMalwareAsync(assemblyPath, cancellationToken));
            }

            // Digital signature validation
            if (_options.RequireDigitalSignature)
            {
                validationTasks.Add(ValidateDigitalSignatureAsync(assemblyPath, cancellationToken));
            }

            // Assembly metadata validation
            validationTasks.Add(ValidateAssemblyMetadataAsync(assemblyPath, cancellationToken));

            // Wait for all validation tasks
            var results = await Task.WhenAll(validationTasks).ConfigureAwait(false);

            // Combine results
            var combinedResult = CombineValidationResults(results);

            // Cache the result
            _ = _validationCache.TryAdd(cacheKey, combinedResult);

            LogValidationCompleted(assemblyPath, combinedResult.IsValid, combinedResult.Severity);
            return combinedResult;
        }

        /// <summary>
        /// Validates a plugin instance after creation.
        /// </summary>
        /// <param name="plugin">The plugin to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validation result.</returns>
        public async Task<ValidationResult> ValidatePluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            LogValidatingPlugin(plugin.Id);
            var validations = new List<ValidationResult>
            {
                // Basic plugin interface validation
                ValidatePluginInterface(plugin),

                // Plugin metadata validation
                ValidatePluginMetadata(plugin),

                // Plugin capabilities validation
                await ValidatePluginCapabilitiesAsync(plugin, cancellationToken).ConfigureAwait(false),

                // Resource usage validation
                ValidateResourceUsage(plugin)
            };

            // Security permissions validation
            if (_options.EnableSecurityValidation)
            {
                validations.Add(ValidateSecurityPermissions(plugin));
            }

            var result = CombineValidationResults([.. validations]);
            LogPluginValidationCompleted(plugin.Id, result.IsValid, result.Severity);
            return result;
        }

        /// <summary>
        /// Validates basic file properties.
        /// </summary>
        private async Task<ValidationResult> ValidateFileBasicsAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(assemblyPath);

                // Size validation
                if (fileInfo.Length > _options.MaxAssemblySize)
                {
                    return ValidationResult.Failure(
                        $"Assembly size ({fileInfo.Length:N0} bytes) exceeds maximum allowed size ({_options.MaxAssemblySize:N0} bytes)",
                        ValidationSeverity.Error);
                }

                // Extension validation
                if (!fileInfo.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !fileInfo.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Warning(
                        $"Unexpected file extension: {fileInfo.Extension}",
                        ValidationSeverity.Info);
                }

                // Check if file is locked
                try
                {
                    await using var stream = File.OpenRead(assemblyPath);
                    // File can be opened, so it's not locked
                }
                catch (IOException)
                {
                    return ValidationResult.Failure(
                        "Assembly file is locked or inaccessible",
                        ValidationSeverity.Warning);
                }

                LogFileBasicsValid(assemblyPath, fileInfo.Length);
                return ValidationResult.Success("File basics validation passed");
            }
            catch (Exception ex)
            {
                LogFileBasicsValidationFailed(assemblyPath, ex.Message);
                return ValidationResult.Failure($"File basics validation failed: {ex.Message}", ValidationSeverity.Error);
            }
        }

        /// <summary>
        /// Validates digital signature of an assembly.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute",
            Justification = "Plugin validation requires assembly loading for signature verification")]
        private async Task<ValidationResult> ValidateDigitalSignatureAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            try
            {
                LogValidatingDigitalSignature(assemblyPath);

                // Add minimal async operation to justify async signature
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                // This is a simplified check - in production, you'd use proper certificate validation
                // For now, we'll check if the assembly is strong-named
                var assembly = Assembly.LoadFrom(assemblyPath);
                var publicKey = assembly.GetName().GetPublicKey();

                if (publicKey == null || publicKey.Length == 0)
                {
                    return ValidationResult.Failure(
                        "Assembly is not strong-named",
                        ValidationSeverity.Error);
                }

                // Check against trusted publishers
                if (_options.TrustedPublishers.Count > 0)
                {
                    var assemblyName = assembly.GetName().Name ?? string.Empty;
                    var isTrusted = _options.TrustedPublishers.Any(publisher =>
                        assemblyName.StartsWith(publisher, StringComparison.OrdinalIgnoreCase));

                    if (!isTrusted)
                    {
                        return ValidationResult.Warning(
                            "Assembly is not from a trusted publisher",
                            ValidationSeverity.Warning);
                    }
                }

                LogDigitalSignatureValid(assemblyPath);
                return ValidationResult.Success("Digital signature validation passed");
            }
            catch (Exception ex)
            {
                LogDigitalSignatureValidationFailed(assemblyPath, ex.Message);
                return ValidationResult.Failure($"Digital signature validation failed: {ex.Message}", ValidationSeverity.Error);
            }
        }

        /// <summary>
        /// Validates assembly metadata and compatibility.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute",
            Justification = "Plugin validation requires assembly loading for metadata verification")]
        private async Task<ValidationResult> ValidateAssemblyMetadataAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            try
            {
                LogValidatingAssemblyMetadata(assemblyPath);

                // Add minimal async operation to justify async signature
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                var assembly = Assembly.LoadFrom(assemblyPath);
                var validations = new List<ValidationResult>();

                // Target framework validation
                var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
                if (targetFramework != null)
                {
                    if (!IsCompatibleFramework(targetFramework.FrameworkName))
                    {
                        validations.Add(ValidationResult.Warning(
                            $"Assembly targets potentially incompatible framework: {targetFramework.FrameworkName}",
                            ValidationSeverity.Warning));
                    }
                }

                // Runtime version validation
                var runtimeVersion = assembly.ImageRuntimeVersion;
                if (!IsCompatibleRuntimeVersion(runtimeVersion))
                {
                    validations.Add(ValidationResult.Warning(
                        $"Assembly runtime version may be incompatible: {runtimeVersion}",
                        ValidationSeverity.Info));
                }

                // Check for plugin interface implementation
                var hasPluginInterface = assembly.GetTypes()
                    .Any(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));

                if (!hasPluginInterface)
                {
                    validations.Add(ValidationResult.Failure(
                        "Assembly does not contain any classes implementing IAlgorithmPlugin",
                        ValidationSeverity.Error));
                }

                LogAssemblyMetadataValid(assemblyPath);
                return CombineValidationResults([.. validations]);
            }
            catch (Exception ex)
            {
                LogAssemblyMetadataValidationFailed(assemblyPath, ex.Message);
                return ValidationResult.Failure($"Assembly metadata validation failed: {ex.Message}", ValidationSeverity.Error);
            }
        }

        /// <summary>
        /// Validates an assembly for malware using the malware scanner.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        private async Task<ValidationResult> ValidateAssemblyMalwareAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            try
            {
                var scanResult = await _malwareScanner.ScanAssemblyAsync(assemblyPath, cancellationToken);

                if (!scanResult.IsSuccess)
                {
                    return ValidationResult.Failure(
                        $"Malware scan failed: {string.Join(", ", scanResult.Errors)}",
                        ValidationSeverity.Critical);
                }

                if (scanResult.IsMalwareDetected)
                {
                    var threatDetails = string.Join("; ", scanResult.DetectedThreats.Select(t => $"{t.Name}: {t.Description}"));
                    return ValidationResult.Failure(
                        $"Malware detected in assembly (Confidence: {scanResult.ConfidenceScore:P1}): {threatDetails}",
                        ValidationSeverity.Critical);
                }

                if (scanResult.Warnings.Count > 0)
                {
                    var warnings = string.Join("; ", scanResult.Warnings);
                    return ValidationResult.Warning(
                        $"Malware scan completed with warnings: {warnings}",
                        ValidationSeverity.Warning);
                }

                return ValidationResult.Success($"Malware scan completed successfully in {scanResult.ScanDuration.TotalMilliseconds:F1}ms");
            }
            catch (Exception ex)
            {
                LogMalwareScanningError(ex, assemblyPath);
                return ValidationResult.Failure(
                    $"Malware scanning failed with exception: {ex.Message}",
                    ValidationSeverity.High);
            }
        }

        /// <summary>
        /// Validates plugin interface implementation.
        /// </summary>
        private static ValidationResult ValidatePluginInterface(IAlgorithmPlugin plugin)
        {
            var validations = new List<ValidationResult>();

            // Required properties
            if (string.IsNullOrWhiteSpace(plugin.Id))
            {
                validations.Add(ValidationResult.Failure("Plugin ID is required", ValidationSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(plugin.Name))
            {
                validations.Add(ValidationResult.Failure("Plugin name is required", ValidationSeverity.Error));
            }

            if (plugin.Version == null)
            {
                validations.Add(ValidationResult.Failure("Plugin version is required", ValidationSeverity.Error));
            }

            // Capabilities
            if (plugin.SupportedOperations?.Any() != true)
            {
                validations.Add(ValidationResult.Warning("Plugin declares no supported operations", ValidationSeverity.Info));
            }

            return CombineValidationResults([.. validations]);
        }

        /// <summary>
        /// Validates plugin metadata.
        /// </summary>
        private static ValidationResult ValidatePluginMetadata(IAlgorithmPlugin plugin)
        {
            var validations = new List<ValidationResult>();

            // ID format validation (should be a valid identifier)
            if (!IsValidIdentifier(plugin.Id))
            {
                validations.Add(ValidationResult.Warning("Plugin ID should be a valid identifier", ValidationSeverity.Info));
            }

            // Version format validation
            if (plugin.Version != null && Version.TryParse(plugin.Version.ToString(), out var version) && version.Major < 0)
            {
                validations.Add(ValidationResult.Warning("Plugin version appears invalid", ValidationSeverity.Info));
            }

            // Description validation
            if (string.IsNullOrWhiteSpace(plugin.Description))
            {
                validations.Add(ValidationResult.Warning("Plugin description is missing", ValidationSeverity.Info));
            }

            return CombineValidationResults([.. validations]);
        }

        /// <summary>
        /// Validates plugin capabilities.
        /// </summary>
        private async Task<ValidationResult> ValidatePluginCapabilitiesAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken)
        {
            try
            {
                // This is a placeholder for capability validation
                // In a real implementation, you might test specific plugin operations
                await Task.Delay(1, cancellationToken); // Minimal async work

                var validations = new List<ValidationResult>();

                // Check for dangerous operations
                if (plugin.SupportedOperations?.Contains("FileSystemAccess") == true)
                {
                    if (!_options.AllowFileSystemAccess)
                    {
                        validations.Add(ValidationResult.Failure(
                            "Plugin requires file system access which is not allowed",
                            ValidationSeverity.Error));
                    }
                }

                if (plugin.SupportedOperations?.Contains("NetworkAccess") == true)
                {
                    if (!_options.AllowNetworkAccess)
                    {
                        validations.Add(ValidationResult.Failure(
                            "Plugin requires network access which is not allowed",
                            ValidationSeverity.Error));
                    }
                }

                return CombineValidationResults([.. validations]);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Capability validation failed: {ex.Message}", ValidationSeverity.Warning);
            }
        }

        /// <summary>
        /// Validates resource usage requirements.
        /// </summary>
        private ValidationResult ValidateResourceUsage(IAlgorithmPlugin plugin)
        {
            var validations = new List<ValidationResult>();

            // Check memory requirements (if plugin declares them)
            if (plugin is IResourceAwarePlugin resourceAware)
            {
                if (resourceAware.RequiredMemoryMB > _options.MaxMemoryUsageMB)
                {
                    validations.Add(ValidationResult.Failure(
                        $"Plugin requires {resourceAware.RequiredMemoryMB}MB memory, but limit is {_options.MaxMemoryUsageMB}MB",
                        ValidationSeverity.Error));
                }

                if (resourceAware.RequiredCpuCores > Environment.ProcessorCount)
                {
                    validations.Add(ValidationResult.Warning(
                        $"Plugin requests {resourceAware.RequiredCpuCores} CPU cores, but only {Environment.ProcessorCount} available",
                        ValidationSeverity.Warning));
                }
            }

            return CombineValidationResults([.. validations]);
        }

        /// <summary>
        /// Validates security permissions.
        /// </summary>
        private ValidationResult ValidateSecurityPermissions(IAlgorithmPlugin plugin)
        {
            var validations = new List<ValidationResult>();

            // Check if plugin assembly has appropriate security attributes
            var assembly = plugin.GetType().Assembly;
            var securityAttributes = assembly.GetCustomAttributes<System.Security.SecurityTransparentAttribute>();

            if (!securityAttributes.Any() && _options.RequireSecurityTransparency)
            {
                validations.Add(ValidationResult.Warning(
                    "Plugin assembly lacks security transparency attributes",
                    ValidationSeverity.Warning));
            }

            return CombineValidationResults([.. validations]);
        }

        /// <summary>
        /// Combines multiple validation results into a single result.
        /// </summary>
        private static ValidationResult CombineValidationResults(ValidationResult[] results)
        {
            if (results.Length == 0)
            {
                return ValidationResult.Success("No validations performed");
            }

            var failures = results.Where(r => !r.IsValid).ToArray();
            if (failures.Length > 0)
            {
                var highestSeverity = failures.Max(f => f.Severity);
                var combinedMessage = string.Join("; ", failures.Select(f => f.Message));
                return ValidationResult.Failure(combinedMessage, highestSeverity);
            }

            var warnings = results.Where(r => r.IsValid && r.HasWarnings).ToArray();
            if (warnings.Length > 0)
            {
                var combinedMessage = string.Join("; ", warnings.Select(w => w.Message));
                return ValidationResult.Warning(combinedMessage, warnings.Max(w => w.Severity));
            }

            return ValidationResult.Success("All validations passed");
        }

        /// <summary>
        /// Gets a hash of the file for caching purposes.
        /// </summary>
        private static string GetFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Checks if a framework is compatible.
        /// </summary>
        private static bool IsCompatibleFramework(string frameworkName)
        {
            // Simplified compatibility check
            return frameworkName.Contains(".NETCoreApp", StringComparison.OrdinalIgnoreCase) ||
                   frameworkName.Contains(".NET ", StringComparison.OrdinalIgnoreCase) ||
                   frameworkName.Contains("net9.0", StringComparison.CurrentCulture) ||
                   frameworkName.Contains("net8.0", StringComparison.CurrentCulture) ||
                   frameworkName.Contains("net7.0", StringComparison.CurrentCulture) ||
                   frameworkName.Contains("net6.0", StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Checks if a runtime version is compatible.
        /// </summary>
        private static bool IsCompatibleRuntimeVersion(string runtimeVersion)
            // Simplified compatibility check

            => runtimeVersion.StartsWith("v4.", StringComparison.CurrentCulture) || runtimeVersion.StartsWith("v5.", StringComparison.CurrentCulture) || runtimeVersion.StartsWith("v6.", StringComparison.CurrentCulture) || runtimeVersion.StartsWith("v7.", StringComparison.CurrentCulture) || runtimeVersion.StartsWith("v8.", StringComparison.CurrentCulture) || runtimeVersion.StartsWith("v9.", StringComparison.CurrentCulture);

        /// <summary>
        /// Checks if a string is a valid identifier.
        /// </summary>
        private static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {

                return false;
            }


            return identifier.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
        }

        // Logger messages
        [LoggerMessage(Level = LogLevel.Information, Message = "Validating assembly {AssemblyPath}")]
        private partial void LogValidatingAssembly(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Validation cache hit for {AssemblyPath}")]
        private partial void LogValidationCacheHit(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Validation completed for {AssemblyPath}, Valid: {IsValid}, Severity: {Severity}")]
        private partial void LogValidationCompleted(string assemblyPath, bool isValid, ValidationSeverity severity);

        [LoggerMessage(Level = LogLevel.Information, Message = "Validating plugin {PluginId}")]
        private partial void LogValidatingPlugin(string pluginId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin validation completed for {PluginId}, Valid: {IsValid}, Severity: {Severity}")]
        private partial void LogPluginValidationCompleted(string pluginId, bool isValid, ValidationSeverity severity);

        [LoggerMessage(Level = LogLevel.Debug, Message = "File basics valid for {AssemblyPath}, Size: {Size} bytes")]
        private partial void LogFileBasicsValid(string assemblyPath, long size);

        [LoggerMessage(Level = LogLevel.Error, Message = "File basics validation failed for {AssemblyPath}: {Reason}")]
        private partial void LogFileBasicsValidationFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Validating digital signature for {AssemblyPath}")]
        private partial void LogValidatingDigitalSignature(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Digital signature valid for {AssemblyPath}")]
        private partial void LogDigitalSignatureValid(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Digital signature validation failed for {AssemblyPath}: {Reason}")]
        private partial void LogDigitalSignatureValidationFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Validating assembly metadata for {AssemblyPath}")]
        private partial void LogValidatingAssemblyMetadata(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Assembly metadata valid for {AssemblyPath}")]
        private partial void LogAssemblyMetadataValid(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Assembly metadata validation failed for {AssemblyPath}: {Reason}")]
        private partial void LogAssemblyMetadataValidationFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error during malware scanning of {AssemblyPath}")]
        private partial void LogMalwareScanningError(Exception ex, string assemblyPath);

        /// <summary>
        /// Disposes resources used by the validator.
        /// </summary>
        public void Dispose() => _malwareScanner?.Dispose();

        /// <summary>
        /// Asynchronously disposes resources used by the validator.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_malwareScanner != null)
            {
                await _malwareScanner.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Represents the result of a validation operation.
    /// </summary>
    public sealed class ValidationResult
    {
        private ValidationResult(bool isValid, string message, ValidationSeverity severity, bool hasWarnings = false)
        {
            IsValid = isValid;
            Message = message;
            Severity = severity;
            HasWarnings = hasWarnings;
        }
        /// <summary>
        /// Gets or sets a value indicating whether valid.
        /// </summary>
        /// <value>The is valid.</value>

        public bool IsValid { get; }
        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>The message.</value>
        public string Message { get; }
        /// <summary>
        /// Gets or sets the severity.
        /// </summary>
        /// <value>The severity.</value>
        public ValidationSeverity Severity { get; }
        /// <summary>
        /// Gets or sets a value indicating whether warnings.
        /// </summary>
        /// <value>The has warnings.</value>
        public bool HasWarnings { get; }
        /// <summary>
        /// Gets success.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>

        public static ValidationResult Success(string message) => new(true, message, ValidationSeverity.Info);
        /// <summary>
        /// Gets warning.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="severity">The severity.</param>
        /// <returns>The result of the operation.</returns>
        public static ValidationResult Warning(string message, ValidationSeverity severity) => new(true, message, severity, true);
        /// <summary>
        /// Gets failure.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="severity">The severity.</param>
        /// <returns>The result of the operation.</returns>
        public static ValidationResult Failure(string message, ValidationSeverity severity) => new(false, message, severity);
    }

    /// <summary>
    /// Severity level for validation results.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational message.
        /// </summary>
        Info = 0,

        /// <summary>
        /// Warning message.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Error message.
        /// </summary>
        Error = 2,

        /// <summary>
        /// High severity issue.
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical issue.
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Interface for plugins that declare resource requirements.
    /// </summary>
    public interface IResourceAwarePlugin
    {
        /// <summary>
        /// Gets or sets the required memory m b.
        /// </summary>
        /// <value>The required memory m b.</value>
        public int RequiredMemoryMB { get; }
        /// <summary>
        /// Gets or sets the required cpu cores.
        /// </summary>
        /// <value>The required cpu cores.</value>
        public int RequiredCpuCores { get; }
        /// <summary>
        /// Gets or sets the requires gpu.
        /// </summary>
        /// <value>The requires gpu.</value>
        public bool RequiresGpu { get; }
    }
}
