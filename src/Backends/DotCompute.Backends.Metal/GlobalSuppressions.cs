// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

// CA1401: P/Invoke methods are intentionally public as part of the native API surface
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Public P/Invoke methods are part of the intentional native API design", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Backends.Metal.Native")]

// CA1815: Equality operators not needed for native interop structs
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Native interop structs don't need equality operators", Scope = "type", Target = "~T:DotCompute.Backends.Metal.Native.MetalDeviceInfo")]
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Native interop structs don't need equality operators", Scope = "type", Target = "~T:DotCompute.Backends.Metal.Native.MetalSize")]
