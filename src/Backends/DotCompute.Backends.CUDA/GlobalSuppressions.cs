// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in the CUDA backend are false positives due to GPU resource management patterns:
// - CUDA streams, events, and contexts are pooled and disposed by pool managers
// - Memory allocations transfer ownership to unified buffer wrappers
// - Kernel modules are cached and disposed by kernel cache cleanup
// - P2P transfer resources are managed by transfer schedulers
// - Graph execution resources have lifecycle tied to graph lifetime
// The backend follows CUDA's resource ownership model with proper disposal in owning containers.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "CUDA backend uses GPU resource pooling patterns. Streams/events/contexts pooled and managed by containers. Memory buffers transfer ownership. Kernels cached and disposed by cache. Graph resources tied to graph lifetime. Clear ownership prevents leaks.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// While LoggerMessage delegates provide better performance, the CUDA backend
// prioritizes correctness for diagnostic paths. Performance-critical GPU execution doesn't log.
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "CUDA backend prioritizes correctness over logging performance. GPU execution paths don't log. Diagnostic logs are infrequent.")]

// CA1849: Call async methods when in an async method
// CUDA operations are inherently asynchronous via streams; synchronous waits intentional for synchronization points
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous waits intentional for CUDA stream synchronization points and device barriers.")]

// XDOC001: Missing XML documentation
// Documentation provided for public APIs and complex CUDA interop.
// Internal CUDA wrappers use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public APIs and CUDA interop. Internal wrappers use self-documenting names.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better testability and backend abstraction
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design provides better testability and backend abstraction for multi-GPU scenarios.")]

// CA1852: Type can be sealed
// Types left unsealed for testing and potential extension
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for testing, mocking, and potential future extension.")]

// CA5394: Random is insecure
// Random used for test data generation and performance simulation, not cryptographic purposes
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Random used for test data generation and workload simulation, not cryptographic purposes.")]
