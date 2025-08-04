// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Plugins.Configuration;
using DotCompute.Plugins.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotCompute.Plugins.Extensions
{
    /// <summary>
    /// Extension methods for configuring plugin support.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds plugin system to the service collection.
        /// </summary>
        public static IServiceCollection AddPluginSystem(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<PluginOptions>? configureOptions = null)
        {
            // Configure options
            services.Configure<PluginOptions>(options =>
            {
                configuration.GetSection("Plugins").Bind(options);
                configureOptions?.Invoke(options);
            });

            // Register plugin system
            services.AddSingleton<PluginSystem>();

            return services;
        }

        /// <summary>
        /// Adds plugin system with specific options.
        /// </summary>
        public static IServiceCollection AddPluginSystem(
            this IServiceCollection services,
            Action<PluginOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddSingleton<PluginSystem>();
            return services;
        }

        /// <summary>
        /// Adds plugin system with plugin options instance.
        /// </summary>
        public static IServiceCollection AddPluginSystem(
            this IServiceCollection services,
            PluginOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            services.Configure<PluginOptions>(opts =>
            {
                opts.PluginsDirectory = options.PluginsDirectory;
                opts.EnableHotReload = options.EnableHotReload;
                opts.IsolatePlugins = options.IsolatePlugins;
                opts.SharedAssemblies = options.SharedAssemblies;
                opts.Plugins = options.Plugins;
                opts.MaxConcurrentLoads = options.MaxConcurrentLoads;
                opts.LoadTimeout = options.LoadTimeout;
                opts.PluginDirectories = options.PluginDirectories;
                opts.IsInitialized = options.IsInitialized;
            });

            services.AddSingleton<PluginSystem>();
            return services;
        }
    }
}
