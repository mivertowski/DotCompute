// <copyright file="PluginAttributes.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes;

// This file has been refactored - all attribute classes have been moved to their respective files:
//
// Core Attributes (src/Runtime/DotCompute.Plugins/Attributes/Core/):
// - PluginAttribute.cs (main plugin marking attribute)
// - PluginCapabilityAttribute.cs (capability declaration)
// - PluginInjectAttribute.cs (dependency injection)
//
// Dependency Attributes (src/Runtime/DotCompute.Plugins/Attributes/Dependency/):
// - PluginDependencyAttribute.cs (plugin dependencies)
//
// Configuration Attributes (src/Runtime/DotCompute.Plugins/Attributes/Configuration/):
// - PluginConfigurationAttribute.cs (configuration options)
//
// Lifecycle Attributes (src/Runtime/DotCompute.Plugins/Attributes/Lifecycle/):
// - PluginLifecycleHookAttribute.cs (lifecycle hooks)
// - PluginLifecycleStage.cs (lifecycle stage enum)
//
// Platform Attributes (src/Runtime/DotCompute.Plugins/Attributes/Platform/):
// - PluginPlatformAttribute.cs (platform requirements)
//
// Service Attributes (src/Runtime/DotCompute.Plugins/Attributes/Services/):
// - PluginServiceAttribute.cs (service registration)
// - ServiceLifetime.cs (service lifetime enum)
//
// This file is preserved as a placeholder and documentation of the refactoring.
//
// To use the attribute classes, reference the appropriate namespace:
// - using DotCompute.Plugins.Attributes.Core;
// - using DotCompute.Plugins.Attributes.Dependency;
// - using DotCompute.Plugins.Attributes.Configuration;
// - using DotCompute.Plugins.Attributes.Lifecycle;
// - using DotCompute.Plugins.Attributes.Platform;
// - using DotCompute.Plugins.Attributes.Services;