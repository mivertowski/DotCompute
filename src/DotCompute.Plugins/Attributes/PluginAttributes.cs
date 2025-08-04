// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Attributes
{
    /// <summary>
    /// Marks a class as a DotCompute plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PluginAttribute : Attribute
    {
        /// <summary>
        /// Gets the unique identifier for the plugin.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets or sets the display name of the plugin.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the version of the plugin.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the description of the plugin.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the author of the plugin.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the website URL for the plugin.
        /// </summary>
        public string? Website { get; set; }

        /// <summary>
        /// Gets or sets the license for the plugin.
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets whether the plugin supports hot reload.
        /// </summary>
        public bool SupportsHotReload { get; set; } = false;

        /// <summary>
        /// Gets or sets the priority for plugin loading (higher = loaded first).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets the load priority for backward compatibility.
        /// </summary>
        public int LoadPriority
        {
            get => Priority;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "LoadPriority must be non-negative.");
                Priority = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the PluginAttribute class.
        /// </summary>
        /// <param name="id">The unique identifier for the plugin.</param>
        /// <param name="name">The display name of the plugin.</param>
        public PluginAttribute(string id, string name)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Initializes a new instance of the PluginAttribute class with just a name.
        /// </summary>
        /// <param name="name">The display name of the plugin (also used as ID).</param>
        public PluginAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Plugin name cannot be null or empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Plugin name cannot be whitespace.", nameof(name));

            Id = name;
            Name = name;
        }
    }

    /// <summary>
    /// Specifies a dependency on another plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class PluginDependencyAttribute : Attribute
    {
        /// <summary>
        /// Gets the ID of the required plugin.
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// Gets or sets the minimum version required.
        /// </summary>
        public string? MinimumVersion { get; set; }

        /// <summary>
        /// Gets or sets the maximum version allowed.
        /// </summary>
        public string? MaximumVersion { get; set; }

        /// <summary>
        /// Gets or sets whether the dependency is optional.
        /// </summary>
        public bool IsOptional { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the PluginDependencyAttribute class.
        /// </summary>
        /// <param name="pluginId">The ID of the required plugin.</param>
        public PluginDependencyAttribute(string pluginId)
        {
            PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        }
    }

    /// <summary>
    /// Specifies configuration options for a plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PluginConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the configuration section name.
        /// </summary>
        public string? ConfigurationSection { get; set; }

        /// <summary>
        /// Gets or sets whether configuration is required.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Gets or sets the JSON schema for configuration validation.
        /// </summary>
        public string? JsonSchema { get; set; }

        /// <summary>
        /// Gets or sets whether the plugin supports configuration hot reload.
        /// </summary>
        public bool SupportsHotReload { get; set; } = true;
    }

    /// <summary>
    /// Marks a method as a plugin lifecycle hook.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class PluginLifecycleHookAttribute : Attribute
    {
        /// <summary>
        /// Gets the lifecycle stage for this hook.
        /// </summary>
        public PluginLifecycleStage Stage { get; }

        /// <summary>
        /// Gets or sets the priority for hook execution (higher = executed first).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether errors in this hook should prevent plugin loading.
        /// </summary>
        public bool IsCritical { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the PluginLifecycleHookAttribute class.
        /// </summary>
        /// <param name="stage">The lifecycle stage for this hook.</param>
        public PluginLifecycleHookAttribute(PluginLifecycleStage stage)
        {
            Stage = stage;
        }
    }

    /// <summary>
    /// Represents the lifecycle stages of a plugin.
    /// </summary>
    public enum PluginLifecycleStage
    {
        PreLoad,
        PostLoad,
        PreInitialize,
        PostInitialize,
        PreStart,
        PostStart,
        PreStop,
        PostStop,
        PreUnload,
        PostUnload,
        ConfigurationChanged,
        HealthCheck,
        Cleanup
    }

    /// <summary>
    /// Specifies a capability provided by the plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class PluginCapabilityAttribute : Attribute
    {
        /// <summary>
        /// Gets the capability name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the capability version.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets additional metadata for the capability.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Initializes a new instance of the PluginCapabilityAttribute class.
        /// </summary>
        /// <param name="name">The capability name.</param>
        public PluginCapabilityAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// Marks a property or field for plugin injection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class PluginInjectAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the plugin ID to inject.
        /// </summary>
        public string? PluginId { get; set; }

        /// <summary>
        /// Gets or sets the service type to inject.
        /// </summary>
        public Type? ServiceType { get; set; }

        /// <summary>
        /// Gets or sets whether the injection is optional.
        /// </summary>
        public bool IsOptional { get; set; } = false;
    }

    /// <summary>
    /// Specifies platform requirements for a plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PluginPlatformAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the supported operating systems.
        /// </summary>
        public string[]? SupportedOperatingSystems { get; set; }

        /// <summary>
        /// Gets or sets the minimum .NET version required.
        /// </summary>
        public string? MinimumDotNetVersion { get; set; }

        /// <summary>
        /// Gets or sets required CPU features (e.g., AVX2, SSE4).
        /// </summary>
        public string[]? RequiredCpuFeatures { get; set; }

        /// <summary>
        /// Gets or sets the minimum memory required in MB.
        /// </summary>
        public int MinimumMemoryMB { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether 64-bit is required.
        /// </summary>
        public bool Requires64Bit { get; set; } = false;
    }

    /// <summary>
    /// Marks a type as a plugin service that should be registered.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class PluginServiceAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the service lifetime.
        /// </summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

        /// <summary>
        /// Gets or sets the service type to register as.
        /// </summary>
        public Type? ServiceType { get; set; }

        /// <summary>
        /// Gets or sets whether to register all implemented interfaces.
        /// </summary>
        public bool RegisterInterfaces { get; set; } = true;

        /// <summary>
        /// Gets or sets the registration priority (higher = registered first).
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Represents the service lifetime.
    /// </summary>
    public enum ServiceLifetime
    {
        Transient,
        Scoped,
        Singleton
    }
}
