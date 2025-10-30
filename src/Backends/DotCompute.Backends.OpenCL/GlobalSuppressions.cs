// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in the OpenCL backend are false positives due to OpenCL resource management patterns:
// - OpenCL contexts, command queues, and buffers are pooled and disposed by pool managers
// - Memory allocations transfer ownership to unified buffer wrappers
// - Kernel objects are cached and disposed by kernel cache cleanup
// - Program and device objects have lifecycle tied to context lifetime
// The backend follows OpenCL's resource ownership model with proper disposal in owning containers.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "OpenCL backend uses GPU resource pooling patterns. Contexts/queues/buffers pooled and managed by containers. Memory buffers transfer ownership. Kernels cached and disposed by cache. Program/device resources tied to context lifetime. Clear ownership prevents leaks.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// While LoggerMessage delegates provide better performance, the OpenCL backend
// prioritizes correctness for diagnostic paths. Performance-critical GPU execution doesn't log.
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "OpenCL backend prioritizes correctness over logging performance. GPU execution paths don't log. Diagnostic logs are infrequent.")]

// CA1849: Call async methods when in an async method
// OpenCL operations are inherently asynchronous via command queues; synchronous waits intentional for synchronization points
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous waits intentional for OpenCL command queue synchronization points and device barriers.")]

// XDOC001: Missing XML documentation
// Documentation provided for public APIs and complex OpenCL interop.
// Internal OpenCL wrappers use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public APIs and OpenCL interop. Internal wrappers use self-documenting names.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better testability and backend abstraction
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design provides better testability and backend abstraction for multi-device scenarios.")]

// CA1852: Type can be sealed
// Types left unsealed for testing and potential extension
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for testing, mocking, and potential future extension.")]

// IL2026, IL3050: Native AOT trimming and dynamic code warnings for JSON serialization
// Profiling export methods use JSON for human-readable diagnostics, not performance-critical paths
[assembly: SuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
    Justification = "JSON serialization used only for profiling export (diagnostics), not performance-critical runtime paths. Types are known at compile time.", Scope = "member", Target = "~M:DotCompute.Backends.OpenCL.Profiling.OpenCLProfiler.ExportSessionToJsonAsync(DotCompute.Backends.OpenCL.Profiling.ProfilingSession,System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("AOT", "IL3050:RequiresDynamicCode",
    Justification = "JSON serialization used only for profiling export (diagnostics), not performance-critical runtime paths. Types are known at compile time.", Scope = "member", Target = "~M:DotCompute.Backends.OpenCL.Profiling.OpenCLProfiler.ExportSessionToJsonAsync(DotCompute.Backends.OpenCL.Profiling.ProfilingSession,System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
    Justification = "JSON serialization used only for CSV metadata export (diagnostics), not performance-critical runtime paths. Dictionary<string,object> metadata is diagnostic.", Scope = "member", Target = "~M:DotCompute.Backends.OpenCL.Profiling.OpenCLProfiler.ExportSessionToCsvAsync(DotCompute.Backends.OpenCL.Profiling.ProfilingSession,System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("AOT", "IL3050:RequiresDynamicCode",
    Justification = "JSON serialization used only for CSV metadata export (diagnostics), not performance-critical runtime paths. Dictionary<string,object> metadata is diagnostic.", Scope = "member", Target = "~M:DotCompute.Backends.OpenCL.Profiling.OpenCLProfiler.ExportSessionToCsvAsync(DotCompute.Backends.OpenCL.Profiling.ProfilingSession,System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
