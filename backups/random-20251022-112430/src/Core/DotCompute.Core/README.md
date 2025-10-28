# DotCompute.Core

Core runtime and orchestration for the DotCompute compute acceleration framework.

## Installation

```bash
dotnet add package DotCompute.Core --version 0.1.0-alpha
```

## Overview

DotCompute.Core provides the essential runtime components for compute acceleration:

- Kernel execution management
- Accelerator discovery and lifecycle
- Service registration and dependency injection
- Compilation pipeline orchestration
- Performance monitoring and telemetry

## Usage

```csharp
using DotCompute.Core;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();
services.AddDotCompute(options =>
{
    options.EnableTelemetry = true;
    options.DefaultAccelerator = AcceleratorType.Auto;
});

// Get compute service
var provider = services.BuildServiceProvider();
var compute = provider.GetRequiredService<IComputeService>();

// Execute kernels
var result = await compute.ExecuteKernelAsync("MyKernel", parameters);
```

## Requirements

- .NET 9.0 or later
- Native AOT compatible

## Note

This is an alpha release. APIs are subject to change.

## License

MIT License - Copyright (c) 2025 Michael Ivertowski