// <copyright file="PluginExceptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Exceptions;

// This file has been refactored - all exception classes have been moved to their respective files:
//
// Core Exceptions (src/Runtime/DotCompute.Plugins/Exceptions/Core/):
// - PluginException.cs (base exception class)
// - PluginTimeoutException.cs (timeout-related errors)
//
// Loading Exceptions (src/Runtime/DotCompute.Plugins/Exceptions/Loading/):
// - PluginLoadException.cs (loading failures)
// - PluginInitializationException.cs (initialization failures)
// - PluginDependencyException.cs (dependency issues)
// - PluginNotFoundException.cs (missing plugins)
//
// Configuration Exceptions (src/Runtime/DotCompute.Plugins/Exceptions/Configuration/):
// - PluginConfigurationException.cs (configuration errors)
//
// Security Exceptions (src/Runtime/DotCompute.Plugins/Exceptions/Security/):
// - PluginSecurityException.cs (security violations)
//
// Validation Exceptions (src/Runtime/DotCompute.Plugins/Exceptions/Validation/):
// - PluginValidationException.cs (validation failures)
//
// This file is preserved as a placeholder and documentation of the refactoring.
//
// To use the exception classes, reference the appropriate namespace:
// - using DotCompute.Plugins.Exceptions.Core;
// - using DotCompute.Plugins.Exceptions.Loading;
// - using DotCompute.Plugins.Exceptions.Configuration;
// - using DotCompute.Plugins.Exceptions.Security;


// - using DotCompute.Plugins.Exceptions.Validation;