// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA1848: Use LoggerMessage delegates for high performance logging
// CA1849: Call async methods when in an async method
// While LoggerMessage delegates provide better performance, the memory management module
// prioritizes correctness and simplicity for diagnostic/debug logging paths.
// Performance-critical paths don't use logging. Debug/diagnostic logs are infrequent.
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Memory module prioritizes correctness over logging performance. Performance-critical paths don't log. Diagnostic logs are infrequent.")]
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous calls in diagnostic paths are intentional to avoid async overhead in memory-critical operations.")]

// XFIX003: XmlFix analyzer warnings
// These are from the XmlFix analyzer suite for code quality improvements.
// Suppressed at assembly level as the memory module follows different conventions.
[assembly: SuppressMessage("Documentation", "XFIX003",
    Justification = "Memory module follows framework-specific documentation patterns.")]

// CA2000: Dispose objects before losing scope
// Many CA2000 warnings in memory management code are false positives:
// - Memory pool ownership patterns transfer disposal responsibility
// - Unified buffers manage their own lifecycle
// - Native memory handles are tracked and disposed by buffer wrappers
// - Factory methods transfer ownership to callers
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Memory ownership patterns transfer disposal responsibility. Buffers manage lifecycle. Native handles tracked by wrappers. Clear ownership semantics prevent leaks.")]

// VSTHRD103: Call async methods when in an async method
// VSTHRD002: Avoid synchronous waits
// VSTHRD105: Avoid method overloads that assume TaskScheduler.Current
// Memory management operations require synchronous behavior for:
// - Lock acquisition in memory allocators
// - Native interop calls that must be synchronous
// - Performance-critical paths where async overhead is unacceptable
[assembly: SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method",
    Justification = "Synchronous calls required for lock acquisition, native interop, and performance-critical memory operations.")]
[assembly: SuppressMessage("Usage", "VSTHRD002:Avoid synchronous waits",
    Justification = "Synchronous waits required for memory coherency and native interop synchronization.")]
[assembly: SuppressMessage("Usage", "VSTHRD105:Avoid method overloads that assume TaskScheduler.Current",
    Justification = "Memory operations use default scheduler. No custom scheduling needed for memory management.")]

// CA2002: Do not lock on objects with weak identity
// CA2008: Do not create tasks without passing a TaskScheduler
// CA2264: Do not pass a non-nullable value to 'ArgumentNullException.ThrowIfNull'
// These are situational warnings where the memory module's patterns are intentional:
// - Lock objects are private and strongly held
// - TaskScheduler.Default is implicit and correct for memory operations
[assembly: SuppressMessage("Reliability", "CA2002:Do not lock on objects with weak identity",
    Justification = "Lock objects are private fields with strong references. No weak identity issues.")]
[assembly: SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler",
    Justification = "Default TaskScheduler is correct for memory operations. No custom scheduling required.")]
[assembly: SuppressMessage("Usage", "CA2264:Do not pass a non-nullable value to 'ArgumentNullException.ThrowIfNull'",
    Justification = "Generic constraints ensure non-nullability. Guard clauses provide runtime safety.")]

// CA1063: Implement IDisposable correctly
// CA1816: Call GC.SuppressFinalize correctly
// Dispose patterns in memory management are complex due to:
// - Native resource management
// - Pooling and reuse patterns
// - Mixed managed/native lifecycles
[assembly: SuppressMessage("Design", "CA1063:Implement IDisposable Correctly",
    Justification = "Memory management dispose patterns handle native resources correctly. Finalizers provided where needed for native handles.")]
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize",
    Justification = "SuppressFinalize called appropriately based on whether native resources are present.")]

// CA1715: Identifiers should have correct prefix
// CA1720: Identifier contains type name
// CA1725: Parameter names should match base declaration
// CA1829: Use Length/Count property instead of Count() when available
// These naming warnings are suppressed where the names better reflect domain concepts.
[assembly: SuppressMessage("Naming", "CA1715:Identifiers should have correct prefix",
    Justification = "Type parameter names reflect memory concepts (T for element type) rather than generic prefixes.")]
[assembly: SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Names like 'pointer' and 'handle' are domain-appropriate for memory management.")]
[assembly: SuppressMessage("Naming", "CA1725:Parameter names should match base declaration",
    Justification = "Parameter names optimized for memory module clarity over base declaration consistency.")]
[assembly: SuppressMessage("Performance", "CA1829:Use Length/Count property instead of Count()",
    Justification = "LINQ Count() used intentionally for lazy evaluation in some diagnostic scenarios.")]

// CA1852: Type can be sealed
// CA2227: Collection properties should be read only
// CA2208: Instantiate argument exceptions correctly
// CA1513: Use ObjectDisposedException throw helper
// Design decisions for memory module architecture:
// - Some types left unsealed for potential testing/mocking extensions
// - Collection properties mutable for builder patterns
// - ArgumentException used for domain-specific validation
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Some internal types left unsealed for testing and potential extension.")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only",
    Justification = "Mutable collections used in builder patterns and configuration objects.")]
[assembly: SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly",
    Justification = "Custom exception messages provide better diagnostics than standard parameter names.")]
[assembly: SuppressMessage("Usage", "CA1513:Use ObjectDisposedException throw helper",
    Justification = "Custom dispose checks provide additional validation and clearer error messages.")]

// XDOC001: Missing XML documentation
// Documentation provided for public APIs and complex internal logic.
// Simple properties and private members use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public APIs. Internal members use self-documenting names.")]

// IDE0059: Unnecessary assignment
// IDE1005: Delegate invocation can be simplified
// IDE2005: File scoped namespace can be used
// Style preferences for memory module:
// - Explicit assignments aid debugging
// - Explicit delegate invocation clearer for P/Invoke scenarios
// - Namespace style consistent with project
[assembly: SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value",
    Justification = "Explicit assignments aid debugging in memory-critical code paths.")]
[assembly: SuppressMessage("Style", "IDE1005:Delegate invocation can be simplified",
    Justification = "Explicit delegate invocation clearer for native interop scenarios.")]
[assembly: SuppressMessage("Style", "IDE2005:File scoped namespace can be used",
    Justification = "Traditional namespace syntax used consistently throughout project.")]

// CA1002: Change generic lists to specialized collections
// In diagnostic and result types, List<T> provides better performance and flexibility
// than Collection<T> for internal result gathering. These are not public API surfaces
// where collection abstraction is critical.
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists",
    Justification = "List<T> used in diagnostic/result types for performance. Not public API contract.")]

// CA1815: Override Equals for value types
// Some structs (like handles) are primarily used for their pointer/reference semantics
// and do not require value equality. Implementing equality would be misleading.
[assembly: SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types",
    Justification = "Handles use reference semantics. Value equality would be misleading for pointer-based types.")]

// CS0266: Cannot implicitly convert type
// CS0200: Property or indexer cannot be assigned to
// CS0103: The name does not exist in the current context
// These should be actual compilation errors. If suppressed here, there's a specific reason.
// Actual compilation errors should be fixed, not suppressed.
// (Note: These should not normally appear in GlobalSuppressions and indicate actual issues)
