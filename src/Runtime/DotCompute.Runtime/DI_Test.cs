// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#if DEBUG
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Factories;
using DotCompute.Runtime.Services;

namespace DotCompute.Runtime;


/// <summary>
/// Simple test class to validate DI integration compiles correctly
/// </summary>
public static class DICompilationTest
{
    /// <summary>
    /// Test that all DI registrations compile correctly
    /// </summary>
    public static void ValidateCompilation()
    {
        var services = new ServiceCollection();

        // Add logging
        _ = services.AddLogging();

        // Test all DI extension methods compile
        _ = services.AddDotComputeRuntime(configureOptions: options =>
        {
            options.EnableAutoDiscovery = true;
            options.PreferredAcceleratorType = DotCompute.Abstractions.AcceleratorType.CPU;
        });

        _ = services.AddDotComputePlugins(configureOptions: options =>
        {
            options.EnablePlugins = true;
            options.EnableDependencyInjection = true;
        });

        _ = services.AddAdvancedMemoryManagement(configureOptions: options =>
        {
            options.EnableUnifiedMemory = true;
            options.EnableP2PTransfers = true;
        });

        _ = services.AddPerformanceMonitoring(configureOptions: options =>
        {
            options.EnableMonitoring = true;
            options.EnableKernelProfiling = true;
        });

        // Test that service provider can be built
        using var serviceProvider = services.BuildServiceProvider();

        // Test that core services can be resolved
        var runtime = serviceProvider.GetService<AcceleratorRuntime>();
        var factory = serviceProvider.GetService<IAcceleratorFactory>();
        var memoryService = serviceProvider.GetService<IMemoryPoolService>();
        var kernelService = serviceProvider.GetService<IKernelCompilerService>();

        // All services should be resolvable (though some might be null if dependencies are missing)
        // This test is just to ensure the DI configuration compiles
    }
}

/// <summary>
/// Test that validates configuration classes
/// </summary>
public static class ConfigurationTest
{
    public static void ValidateConfiguration()
    {
        // Test runtime options
        var runtimeOptions = new DotComputeRuntimeOptions
        {
            EnableAutoDiscovery = true,
            PreferredAcceleratorType = DotCompute.Abstractions.AcceleratorType.CPU,
            MaxAccelerators = 8,
            EnableKernelCaching = true,
            KernelCacheDirectory = "./cache",
            MaxCacheSizeMB = 512,
            EnableMemoryPooling = true,
            InitialPoolSizeMB = 128,
            EnableProfiling = false,
            InitializationTimeoutSeconds = 30,
            ValidateCapabilities = true,
            EnableDebugLogging = false,
            EnableGracefulDegradation = true,
            AcceleratorLifetime = (DotCompute.Runtime.Configuration.ServiceLifetime)Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton
        };

        // Test plugin options
        var pluginOptions = new DotComputePluginOptions
        {
            EnablePlugins = true,
            PluginDirectories = new List<string> { "./plugins" },
            EnableIsolation = true,
            EnableHotReload = false,
            MaxConcurrentLoads = 3,
            LoadTimeoutSeconds = 30,
            ValidateSignatures = false,
            TrustedPublishers = new List<string>(),
            EnableDependencyInjection = true,
            PluginLifetime = (DotCompute.Runtime.Configuration.ServiceLifetime)Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped
        };

        // Test advanced memory options
        var memoryOptions = new AdvancedMemoryOptions
        {
            EnableUnifiedMemory = true,
            EnableP2PTransfers = true,
            CoherenceStrategy = MemoryCoherenceStrategy.Automatic,
            PoolGrowthFactor = 2.0,
            MaxPoolSizeMB = 2048,
            EnableCompression = false,
            CompressionThresholdMB = 64
        };

        // Test performance options
        var performanceOptions = new PerformanceMonitoringOptions
        {
            EnableMonitoring = true,
            CollectionIntervalSeconds = 10,
            EnableKernelProfiling = false,
            EnableMemoryTracking = true,
            MaxMetricsCount = 10000,
            EnableMetricsExport = false
        };

        // Test validator
        var validator = new RuntimeOptionsValidator();
        var result = validator.Validate(null, runtimeOptions);

        // Configuration objects should be constructible
        // This validates that all properties and types are correct
    }
}
#endif
