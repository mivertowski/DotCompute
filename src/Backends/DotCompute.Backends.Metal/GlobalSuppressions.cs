// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

// CA1401: P/Invoke methods are intentionally public as part of the native API surface
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Public P/Invoke methods are part of the intentional native API design", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Backends.Metal.Native")]

// CA1815: Equality operators not needed for native interop structs
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Native interop structs don't need equality operators", Scope = "type", Target = "~T:DotCompute.Backends.Metal.Native.MetalDeviceInfo")]
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Native interop structs don't need equality operators", Scope = "type", Target = "~T:DotCompute.Backends.Metal.Native.MetalSize")]

// CA1848: LoggerMessage delegates - suppressing for simplicity and readability
// LoggerMessage delegates add significant boilerplate code and complexity.
// Direct logging calls are more readable and maintainable for this backend.
[assembly: SuppressMessage("Performance", "CA1848:For improved performance, use the LoggerMessage delegates",
    Justification = "Direct logging provides better readability and maintainability. LoggerMessage delegate boilerplate adds unnecessary complexity for this use case.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better testability, API flexibility, and clean architecture.
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design prioritizes clean architecture and API flexibility over micro-optimizations.")]

// CA2000: Dispose objects before losing scope
// Many CA2000 warnings are false positives due to ownership transfer patterns in Metal backend.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Factory methods transfer ownership to callers. Objects are managed by their containers. Clear ownership semantics prevent actual leaks.")]

// CA1008: Enums should have zero value
// MetalLanguageVersion represents Metal Shading Language versions. Version 1_0 is the minimum baseline.
[assembly: SuppressMessage("Design", "CA1008:Enums should have zero value",
    Justification = "MetalLanguageVersion represents MSL versions. Version 1_0 is the minimum baseline; a 'None' value would be semantically incorrect.")]

// CA1849: Call async methods when available
// Some validation methods need synchronous variants for backward compatibility and convenience.
[assembly: SuppressMessage("Reliability", "CA1849:Call async methods when available",
    Justification = "Synchronous validation methods provided for convenience and backward compatibility. Async versions available when needed.")]
