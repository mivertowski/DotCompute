// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in the CPU backend are false positives due to ownership transfer patterns:
// - Thread pools and synchronization primitives are long-lived and disposed with their owning classes
// - Memory buffers created by factories transfer ownership to callers
// - Cached objects (kernels, buffers) are managed by cache containers
// - SIMD operations and parallel execution contexts manage their own resource lifetimes
// The backend follows proper resource management with clear ownership semantics.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "CPU backend uses ownership transfer patterns. Thread pools and sync primitives are long-lived. Memory buffers transfer ownership to callers. Cached objects managed by containers. Clear ownership semantics prevent leaks.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// While LoggerMessage delegates provide better performance, the CPU backend
// prioritizes correctness and simplicity for diagnostic/debug logging paths.
// Performance-critical execution paths don't use logging.
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "CPU backend prioritizes correctness over logging performance. Performance-critical paths don't log. Diagnostic logs are infrequent.")]

// CA1849: Call async methods when in an async method
// CPU backend uses synchronous operations intentionally in hot paths to avoid async overhead
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous operations intentional in hot paths to avoid async overhead in SIMD execution.")]

// XDOC001: Missing XML documentation
// Documentation provided for public APIs and complex internal logic.
// Simple properties and private members use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public APIs. Internal members use self-documenting names.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better testability and API flexibility
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design provides better testability, API contracts, and SOLID principles compliance.")]

// CA1852: Type can be sealed
// Types left unsealed for testing and potential extension
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for testing and potential future extension.")]
