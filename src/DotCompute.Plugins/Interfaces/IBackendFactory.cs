// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions;

namespace DotCompute.Plugins.Interfaces;

/// <summary>
/// Factory interface for creating accelerator backend instances.
/// </summary>
public interface IBackendFactory
{
    /// <summary>
    /// Gets the name of this backend.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the description of this backend.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Gets the version of this backend.
    /// </summary>
    Version Version { get; }
    
    /// <summary>
    /// Determines if this backend is available on the current system.
    /// </summary>
    bool IsAvailable();
    
    /// <summary>
    /// Creates all available accelerators for this backend.
    /// </summary>
    IEnumerable<IAccelerator> CreateAccelerators();
    
    /// <summary>
    /// Creates the default accelerator for this backend.
    /// </summary>
    IAccelerator? CreateDefaultAccelerator();
    
    /// <summary>
    /// Gets the capabilities of this backend.
    /// </summary>
    BackendCapabilities GetCapabilities();
}

/// <summary>
/// Describes the capabilities of a compute backend.
/// </summary>
public class BackendCapabilities
{
    /// <summary>
    /// Indicates if the backend supports float16 operations.
    /// </summary>
    public bool SupportsFloat16 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports float32 operations.
    /// </summary>
    public bool SupportsFloat32 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports float64 operations.
    /// </summary>
    public bool SupportsFloat64 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports int8 operations.
    /// </summary>
    public bool SupportsInt8 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports int16 operations.
    /// </summary>
    public bool SupportsInt16 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports int32 operations.
    /// </summary>
    public bool SupportsInt32 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports int64 operations.
    /// </summary>
    public bool SupportsInt64 { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports asynchronous execution.
    /// </summary>
    public bool SupportsAsyncExecution { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports multiple devices.
    /// </summary>
    public bool SupportsMultiDevice { get; set; }
    
    /// <summary>
    /// Indicates if the backend supports unified memory.
    /// </summary>
    public bool SupportsUnifiedMemory { get; set; }
    
    /// <summary>
    /// Maximum number of devices supported.
    /// </summary>
    public int MaxDevices { get; set; }
    
    /// <summary>
    /// List of supported features specific to this backend.
    /// </summary>
    public string[] SupportedFeatures { get; set; } = Array.Empty<string>();
}