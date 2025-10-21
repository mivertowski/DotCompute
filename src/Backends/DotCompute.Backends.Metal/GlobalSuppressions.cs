// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in the Metal backend are false positives due to Metal resource management patterns:
// - Metal command buffers, encoders, and queues are pooled and disposed by pool managers
// - Memory allocations transfer ownership to unified memory wrappers
// - Metal library and function objects are cached and disposed by cache cleanup
// - Command stream resources are managed by stream schedulers
// - Graph execution resources have lifecycle tied to graph lifetime
// The backend follows Metal's resource ownership model with proper disposal in owning containers.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Metal backend uses GPU resource pooling patterns. Command buffers/encoders/queues pooled and managed by containers. Memory buffers transfer ownership. Libraries cached and disposed by cache. Command stream resources managed by schedulers. Clear ownership prevents leaks.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// While LoggerMessage delegates provide better performance, the Metal backend
// prioritizes correctness for diagnostic paths. Performance-critical GPU execution doesn't log.
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Metal backend prioritizes correctness over logging performance. GPU execution paths don't log. Diagnostic logs are infrequent.")]

// CA1849: Call async methods when in an async method
// Metal operations are inherently asynchronous via command buffers; synchronous waits intentional for synchronization points
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous waits intentional for Metal command buffer synchronization points and device barriers.")]

// XDOC001: Missing XML documentation
// Documentation provided for public APIs and complex Metal interop.
// Internal Metal wrappers use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public APIs and Metal interop. Internal wrappers use self-documenting names.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better testability and backend abstraction
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design provides better testability and backend abstraction for multi-device scenarios.")]

// CA1852: Type can be sealed
// Types left unsealed for testing and potential extension
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for testing, mocking, and potential future extension.")]

// CA1416: Platform compatibility
// Metal backend is macOS-only by design; platform checks guard all Metal API usage
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility",
    Justification = "Metal backend is macOS-only by design. Platform checks guard all Metal API usage.")]
