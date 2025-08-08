// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Permissions;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management;
using DotCompute.Algorithms.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Examples;

/// <summary>
/// Comprehensive example demonstrating the security validation system for plugin loading.
/// This example shows how to configure and use digital signature validation, strong name verification,
/// malware scanning, security policies, and Code Access Security (CAS) permission restrictions.
/// </summary>
public static class SecurityValidationExample
{
    /// <summary>
    /// Runs the security validation example.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        // Create host with dependency injection
        using var host = CreateHostBuilder(args).Build();
        
        var logger = host.Services.GetRequiredService<ILogger>();
        var accelerator = host.Services.GetRequiredService<IAccelerator>();
        
        logger.LogInformation("Starting Security Validation Example");

        try
        {
            // Example 1: Configure comprehensive security policy
            await Example1_ConfigureSecurityPolicy(host.Services, logger);
            
            // Example 2: Digital signature validation
            await Example2_DigitalSignatureValidation(host.Services, logger);
            
            // Example 3: Malware scanning integration
            await Example3_MalwareScanningIntegration(host.Services, logger);
            
            // Example 4: Code Access Security with permission restrictions
            await Example4_CodeAccessSecurityRestrictions(host.Services, logger);
            
            // Example 5: Complete plugin loading with all security features
            await Example5_SecurePluginLoading(accelerator, logger);
            
            logger.LogInformation("Security Validation Example completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Security validation example failed");
        }
    }

    /// <summary>
    /// Example 1: Configure comprehensive security policy with rules and trusted publishers.
    /// </summary>
    private static async Task Example1_ConfigureSecurityPolicy(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Example 1: Configuring Security Policy ===");

        var securityLogger = services.GetRequiredService<ILogger<SecurityPolicy>>();
        var securityPolicy = new SecurityPolicy(securityLogger);

        // Configure basic security settings
        securityPolicy.RequireDigitalSignature = true;
        securityPolicy.RequireStrongName = true;
        securityPolicy.MinimumSecurityLevel = SecurityLevel.High;
        securityPolicy.EnableMetadataAnalysis = true;
        securityPolicy.EnableMalwareScanning = true;
        securityPolicy.MaxAssemblySize = 50 * 1024 * 1024; // 50 MB

        // Add trusted publishers (certificate thumbprints)
        securityPolicy.AddTrustedPublisher("1234567890ABCDEF1234567890ABCDEF12345678");
        securityPolicy.AddTrustedPublisher("ABCDEF1234567890ABCDEF1234567890ABCDEF12");

        // Configure directory policies
        securityPolicy.DirectoryPolicies["/trusted/plugins"] = SecurityLevel.High;
        securityPolicy.DirectoryPolicies["/system/plugins"] = SecurityLevel.Critical;
        securityPolicy.DirectoryPolicies["/temp"] = SecurityLevel.Low;

        // Add custom security rules
        var customFileSizeRule = new FileSizeSecurityRule(25 * 1024 * 1024); // 25 MB limit
        securityPolicy.AddSecurityRule("CustomFileSize", customFileSizeRule);

        // Save configuration to file
        var configPath = Path.Combine(Path.GetTempPath(), "security-policy.json");
        await securityPolicy.SavePolicyToFileAsync(configPath);

        logger.LogInformation("Security policy configured and saved to: {ConfigPath}", configPath);

        // Test policy evaluation
        var testContext = new SecurityEvaluationContext
        {
            AssemblyPath = "/trusted/plugins/test-plugin.dll",
            AssemblyBytes = new byte[1024] // Small test assembly
        };

        var evaluationResult = securityPolicy.EvaluateRules(testContext);
        logger.LogInformation("Policy evaluation result: Allowed={IsAllowed}, Level={SecurityLevel}, Violations={ViolationCount}",
            evaluationResult.IsAllowed, evaluationResult.SecurityLevel, evaluationResult.Violations.Count);
    }

    /// <summary>
    /// Example 2: Demonstrate digital signature validation using Authenticode.
    /// </summary>
    private static async Task Example2_DigitalSignatureValidation(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Example 2: Digital Signature Validation ===");

        var authenticodeLogger = services.GetRequiredService<ILogger<AuthenticodeValidator>>();
        using var validator = new AuthenticodeValidator(authenticodeLogger);

        // Test with a signed assembly (you would replace with actual signed assembly path)
        var testAssemblyPaths = new[]
        {
            @"C:\Windows\System32\kernel32.dll", // System DLL (signed)
            @"C:\Program Files\dotnet\dotnet.exe", // .NET runtime (signed)
            Path.GetTempFileName() // Unsigned temporary file
        };

        foreach (var assemblyPath in testAssemblyPaths.Where(File.Exists))
        {
            logger.LogInformation("Validating signature for: {AssemblyPath}", assemblyPath);

            var result = await validator.ValidateAsync(assemblyPath);

            if (result.IsValid)
            {
                logger.LogInformation("✓ Signature Valid - Signer: {Signer}, Trust Level: {TrustLevel}, Publisher Trusted: {IsTrusted}",
                    result.SignerName, result.TrustLevel, result.IsTrustedPublisher);
            }
            else
            {
                logger.LogWarning("✗ Signature Invalid - Error: {Error}", result.ErrorMessage);
            }

            // Extract certificate information
            var certInfo = validator.ExtractCertificateInfo(assemblyPath);
            if (certInfo != null)
            {
                logger.LogInformation("Certificate Info - Subject: {Subject}, Issuer: {Issuer}, Expires: {NotAfter}",
                    certInfo.Subject, certInfo.Issuer, certInfo.NotAfter);
            }
        }

        // Batch validation example
        var validPaths = testAssemblyPaths.Where(File.Exists).ToArray();
        if (validPaths.Length > 1)
        {
            logger.LogInformation("Performing batch validation of {Count} assemblies", validPaths.Length);
            var batchResults = await validator.ValidateMultipleAsync(validPaths, maxConcurrency: 3);

            foreach (var kvp in batchResults)
            {
                logger.LogInformation("Batch result for {Path}: Valid={IsValid}, Trust={TrustLevel}",
                    Path.GetFileName(kvp.Key), kvp.Value.IsValid, kvp.Value.TrustLevel);
            }
        }
    }

    /// <summary>
    /// Example 3: Demonstrate malware scanning integration.
    /// </summary>
    private static async Task Example3_MalwareScanningIntegration(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Example 3: Malware Scanning Integration ===");

        var malwareOptions = new MalwareScanningOptions
        {
            EnableWindowsDefender = true,
            MaxConcurrentScans = 2,
            ScanTimeout = TimeSpan.FromMinutes(1)
        };

        // Add custom suspicious patterns
        malwareOptions.SuspiciousPatterns.AddRange(new[]
        {
            "CreateRemoteThread",
            "NtQuerySystemInformation",
            "SetWindowsHookEx"
        });

        var malwareLogger = services.GetRequiredService<ILogger<MalwareScanningService>>();
        using var scanner = new MalwareScanningService(malwareLogger, malwareOptions);

        // Create test files for scanning
        var testFiles = new List<string>();
        
        // Clean test file
        var cleanFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(cleanFile, "This is a clean test file.");
        testFiles.Add(cleanFile);

        // Suspicious test file (contains suspicious patterns)
        var suspiciousFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(suspiciousFile, "CreateProcess VirtualAlloc LoadLibrary GetProcAddress");
        testFiles.Add(suspiciousFile);

        try
        {
            foreach (var testFile in testFiles)
            {
                logger.LogInformation("Scanning file: {FileName}", Path.GetFileName(testFile));

                var scanResult = await scanner.ScanAssemblyAsync(testFile);

                logger.LogInformation("Scan Result - Clean: {IsClean}, Threat Level: {ThreatLevel}, Method: {DetectionMethod}",
                    scanResult.IsClean, scanResult.ThreatLevel, scanResult.DetectionMethod);

                if (!scanResult.IsClean)
                {
                    logger.LogWarning("Threat detected: {ThreatDescription}", scanResult.ThreatDescription);
                    
                    if (scanResult.DetectedPatterns.Count > 0)
                    {
                        logger.LogWarning("Suspicious patterns: {Patterns}", string.Join(", ", scanResult.DetectedPatterns));
                    }

                    if (scanResult.BehavioralFlags.Count > 0)
                    {
                        logger.LogWarning("Behavioral flags: {Flags}", string.Join(", ", scanResult.BehavioralFlags));
                    }
                }

                if (scanResult.Warnings.Count > 0)
                {
                    logger.LogWarning("Scan warnings: {Warnings}", string.Join(", ", scanResult.Warnings));
                }

                logger.LogInformation("Scan completed in {Duration}ms using methods: {Methods}",
                    scanResult.ScanDuration.TotalMilliseconds, string.Join(", ", scanResult.ScanMethods));
            }

            // Batch scanning example
            if (testFiles.Count > 1)
            {
                logger.LogInformation("Performing batch scan of {Count} files", testFiles.Count);
                var batchResults = await scanner.ScanMultipleAssembliesAsync(testFiles);

                foreach (var kvp in batchResults)
                {
                    logger.LogInformation("Batch scan result for {File}: Clean={IsClean}, Level={ThreatLevel}",
                        Path.GetFileName(kvp.Key), kvp.Value.IsClean, kvp.Value.ThreatLevel);
                }
            }
        }
        finally
        {
            // Clean up test files
            foreach (var testFile in testFiles)
            {
                try
                {
                    File.Delete(testFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Example 4: Demonstrate Code Access Security with permission restrictions.
    /// </summary>
    private static async Task Example4_CodeAccessSecurityRestrictions(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Example 4: Code Access Security Restrictions ===");

        var casOptions = new CodeAccessSecurityOptions
        {
            DefaultSecurityZone = SecurityZone.Internet,
            EnableFileSystemRestrictions = true,
            EnableNetworkRestrictions = true,
            EnableReflectionRestrictions = true,
            AllowReflectionEmit = false,
            MaxMemoryUsage = 128 * 1024 * 1024, // 128 MB
            MaxExecutionTime = TimeSpan.FromMinutes(2)
        };

        // Configure allowed paths
        casOptions.AllowedFileSystemPaths.Add(Path.GetTempPath());
        casOptions.AllowedFileSystemPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        
        // Configure allowed network endpoints
        casOptions.AllowedNetworkEndpoints.Add("https://api.nuget.org");
        casOptions.AllowedNetworkEndpoints.Add("https://api.github.com");

        var casLogger = services.GetRequiredService<ILogger<CodeAccessSecurityManager>>();
        using var casManager = new CodeAccessSecurityManager(casLogger, casOptions);

        // Demonstrate different security zones
        var testAssemblyPath = "/plugins/test-plugin.dll";

        logger.LogInformation("Creating permission sets for different security zones:");

        foreach (SecurityZone zone in Enum.GetValues<SecurityZone>())
        {
            if (zone == SecurityZone.Unknown) continue;

            var permissionSet = casManager.CreateRestrictedPermissionSet(testAssemblyPath + $"-{zone}", zone);
            logger.LogInformation("Zone {Zone}: Created permission set", zone);

            // Test different operations
            var operations = new[]
            {
                (SecurityOperation.FileRead, "/temp/test.txt"),
                (SecurityOperation.FileWrite, "/temp/output.txt"),
                (SecurityOperation.NetworkAccess, "https://api.example.com"),
                (SecurityOperation.ReflectionAccess, null),
                (SecurityOperation.UnmanagedCode, null)
            };

            foreach (var (operation, target) in operations)
            {
                var allowed = casManager.IsOperationPermitted(testAssemblyPath + $"-{zone}", operation, target);
                var status = allowed ? "✓ ALLOWED" : "✗ DENIED";
                logger.LogInformation("  {Operation} {Target}: {Status}",
                    operation, target ?? "(n/a)", status);
            }
        }

        // Save and load configuration
        var configPath = Path.Combine(Path.GetTempPath(), "cas-config.json");
        await casManager.SaveConfigurationAsync(configPath);
        logger.LogInformation("Code Access Security configuration saved to: {ConfigPath}", configPath);

        // Load configuration back
        await casManager.LoadConfigurationAsync(configPath);
        logger.LogInformation("Code Access Security configuration loaded successfully");

        // Clean up
        try
        {
            File.Delete(configPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Example 5: Complete plugin loading with all security features enabled.
    /// </summary>
    private static async Task Example5_SecurePluginLoading(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("=== Example 5: Secure Plugin Loading ===");

        // Configure comprehensive security options
        var securityOptions = new AlgorithmPluginManagerOptions
        {
            EnableSecurityValidation = true,
            EnablePluginIsolation = true,
            EnableHealthChecks = true,
            EnableMalwareScanning = true,
            RequireDigitalSignature = true,
            RequireStrongName = true,
            MinimumSecurityLevel = SecurityLevel.High,
            EnableMetadataAnalysis = true,
            EnableWindowsDefenderScanning = true,
            MaxAssemblySize = 25 * 1024 * 1024, // 25 MB
            HealthCheckInterval = TimeSpan.FromMinutes(2)
        };

        // Configure trusted publishers
        securityOptions.TrustedPublishers.AddRange(new[]
        {
            "Microsoft Corporation",
            "Example Trusted Publisher"
        });

        // Configure allowed directories
        securityOptions.AllowedPluginDirectories.AddRange(new[]
        {
            Path.Combine(Environment.CurrentDirectory, "plugins"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MyApp", "plugins")
        });

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var pluginManagerLogger = loggerFactory.CreateLogger<AlgorithmPluginManager>();
        await using var pluginManager = new AlgorithmPluginManager(accelerator, pluginManagerLogger, securityOptions);

        logger.LogInformation("Plugin manager initialized with comprehensive security settings");

        // Create a test plugin directory
        var pluginDir = Path.Combine(Path.GetTempPath(), "secure-plugins-test");
        Directory.CreateDirectory(pluginDir);

        try
        {
            // In a real scenario, you would have actual plugin assemblies
            // For this example, we'll demonstrate the security validation process
            logger.LogInformation("Plugin directory created: {PluginDir}", pluginDir);
            logger.LogInformation("Security validations that would be performed:");
            logger.LogInformation("  ✓ Digital signature verification using Authenticode");
            logger.LogInformation("  ✓ Strong name signature validation");
            logger.LogInformation("  ✓ Malware scanning (hash-based, pattern-based, behavioral)");
            logger.LogInformation("  ✓ Assembly metadata analysis for suspicious patterns");
            logger.LogInformation("  ✓ Security policy evaluation");
            logger.LogInformation("  ✓ Code Access Security permission restrictions");
            logger.LogInformation("  ✓ File size and directory location validation");

            // Attempt to discover plugins (would fail due to no valid assemblies, but shows the process)
            var discoveredCount = await pluginManager.DiscoverAndLoadPluginsAsync(pluginDir);
            logger.LogInformation("Plugin discovery completed. Loaded plugins: {Count}", discoveredCount);

            // Show registered plugins (if any)
            var pluginInfo = pluginManager.GetPluginInfo().ToList();
            if (pluginInfo.Count > 0)
            {
                logger.LogInformation("Registered plugins:");
                foreach (var plugin in pluginInfo)
                {
                    logger.LogInformation("  - {Name} v{Version} (ID: {Id})",
                        plugin.Name, plugin.Version, plugin.Id);
                    logger.LogInformation("    Accelerators: {Accelerators}",
                        string.Join(", ", plugin.SupportedAccelerators));
                    logger.LogInformation("    Input Types: {InputTypes}",
                        string.Join(", ", plugin.InputTypes));
                }
            }
            else
            {
                logger.LogInformation("No plugins were loaded (expected for this example due to security validation)");
            }
        }
        finally
        {
            // Clean up
            try
            {
                Directory.Delete(pluginDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        logger.LogInformation("Secure plugin loading example completed");
    }

    /// <summary>
    /// Creates the host builder with dependency injection configuration.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                // Add a mock accelerator for the example
                services.AddSingleton<IAccelerator, MockAccelerator>();
            });

    /// <summary>
    /// Mock accelerator implementation for the example.
    /// </summary>
    private class MockAccelerator : IAccelerator
    {
        public AcceleratorInfo Info { get; } = new AcceleratorInfo(
            AcceleratorType.CPU, 
            "Mock CPU Accelerator", 
            "1.0.0", 
            1024 * 1024 * 1024); // 1GB

        public IMemoryManager Memory { get; } = new MockMemoryManager();

        public ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition, 
            CompilationOptions? options = null, 
            CancellationToken cancellationToken = default)
        {
            var mockKernel = new MockCompiledKernel(definition.Name);
            return ValueTask.FromResult<ICompiledKernel>(mockKernel);
        }

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Mock memory manager for the example.
    /// </summary>
    private class MockMemoryManager : IMemoryManager
    {
        public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
        {
            var buffer = new MockMemoryBuffer(sizeInBytes, options);
            return ValueTask.FromResult<IMemoryBuffer>(buffer);
        }

        public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
        {
            unsafe
            {
                var buffer = new MockMemoryBuffer(source.Length * sizeof(T), options);
                return ValueTask.FromResult<IMemoryBuffer>(buffer);
            }
        }

        public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
        {
            return new MockMemoryBuffer(length, buffer.Options);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Mock memory buffer for the example.
    /// </summary>
    private class MockMemoryBuffer : IMemoryBuffer
    {
        private readonly byte[] _data;

        public MockMemoryBuffer(long sizeInBytes, MemoryOptions options = MemoryOptions.None)
        {
            _data = new byte[sizeInBytes];
            SizeInBytes = sizeInBytes;
            Options = options;
        }

        public long SizeInBytes { get; }
        
        public MemoryOptions Options { get; }

        public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            where T : unmanaged
        {
            // Mock implementation
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            where T : unmanaged
        {
            // Mock implementation
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Mock compiled kernel for the example.
    /// </summary>
    private class MockCompiledKernel : ICompiledKernel
    {
        public MockCompiledKernel(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
        {
            // Mock implementation
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>
/// Example of a SecurityValidationExample - demonstrates comprehensive security validation
/// for plugin systems in .NET applications.
/// 
/// Key Security Features Demonstrated:
/// 1. Digital Signature Validation (Authenticode)
/// 2. Strong Name Verification
/// 3. Malware Scanning (Multiple Methods)
/// 4. Security Policy Management
/// 5. Code Access Security (CAS) Permission Restrictions
/// 6. Assembly Metadata Analysis
/// 7. Directory-based Security Policies
/// 8. Trusted Publisher Management
/// 
/// This example shows production-ready security validation that can be used
/// to safely load and execute plugin assemblies in a .NET application.
/// </summary>
internal static class SecurityValidationDocumentation
{
    // This class serves as documentation for the security validation example
}