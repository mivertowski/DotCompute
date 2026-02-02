# DotCompute Diagnostic Reference

This section documents all diagnostic messages produced by DotCompute, including experimental feature warnings, analyzer diagnostics, and runtime warnings.

## Experimental Feature Warnings

These diagnostics are produced when using APIs marked with the `[Experimental]` attribute.

| ID | Description | Severity |
|----|-------------|----------|
| [DOTCOMPUTE0001](DOTCOMPUTE0001.md) | Mobile Extensions (MAUI) - Experimental | Warning |
| [DOTCOMPUTE0002](DOTCOMPUTE0002.md) | Web Extensions (Blazor WebAssembly) - Experimental | Warning |
| [DOTCOMPUTE0003](DOTCOMPUTE0003.md) | OpenCL Backend - Experimental | Warning |
| [DOTCOMPUTE0004](DOTCOMPUTE0004.md) | LINQ Optimization Interfaces - Experimental | Warning |

## Roslyn Analyzer Diagnostics

These diagnostics are produced by the DotCompute Roslyn analyzers for kernel development.

| ID | Description | Severity | Category |
|----|-------------|----------|----------|
| DC001 | Kernel method must be static | Error | Kernel |
| DC002 | Kernel method must return void | Error | Kernel |
| DC003 | Invalid kernel parameter type | Error | Kernel |
| DC004 | Missing bounds check in kernel | Warning | Safety |
| DC005 | Potential race condition detected | Warning | Concurrency |
| DC006 | Inefficient memory access pattern | Info | Performance |
| DC007 | Kernel exceeds register limit | Warning | Performance |
| DC008 | Unsupported LINQ operation for GPU | Warning | LINQ |
| DC009 | Missing [Kernel] attribute | Info | Kernel |
| DC010 | Kernel name conflict | Error | Kernel |
| DC011 | Invalid thread indexing | Warning | Safety |
| DC012 | Deprecated API usage | Warning | Deprecation |

## Suppressing Diagnostics

### Project-Wide Suppression

Add to your `.csproj` file:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DOTCOMPUTE0001;DOTCOMPUTE0002</NoWarn>
</PropertyGroup>
```

### Code-Level Suppression

```csharp
#pragma warning disable DOTCOMPUTE0001
// Your code here
#pragma warning restore DOTCOMPUTE0001
```

### Attribute-Based Suppression

```csharp
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Experimental", "DOTCOMPUTE0001")]
public void MyMethod() { }
```

## Understanding Severity Levels

| Severity | Build Impact | Recommended Action |
|----------|--------------|-------------------|
| Error | Fails build | Must fix before release |
| Warning | May fail with TreatWarningsAsErrors | Review and fix or suppress with justification |
| Info | No build impact | Consider for code quality |

## Reporting Issues

If you believe a diagnostic is incorrect or have suggestions for improvement:

1. Check the [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
2. Search for existing reports
3. Create a new issue with the diagnostic ID and reproduction steps
