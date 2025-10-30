// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CS9113: Unused parameter warnings
// Some parameters are required by interfaces or base classes but not used in minimal implementations
[assembly: SuppressMessage("Compiler", "CS9113:Parameter is unread",
    Justification = "Parameters required by interface contracts or left for future implementations.")]

// XDOC001: Missing XML documentation
// Documentation provided for public APIs. Internal members use self-documenting names.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public APIs. Internal members and fields use clear self-documenting names.")]

// CA1813: Avoid unsealed attributes
// Attributes may be extended in the future for additional functionality
[assembly: SuppressMessage("Design", "CA1813:Avoid unsealed attributes",
    Justification = "Attributes designed for potential extension in plugin system.")]

// CA1063: Implement IDisposable correctly
// Simplified disposal patterns appropriate for runtime service lifetime
[assembly: SuppressMessage("Design", "CA1063:Implement IDisposable Correctly",
    Justification = "Simplified disposal patterns appropriate for DI-managed service lifetimes. Native resources tracked separately.")]

// CA1819: Properties should not return arrays
// Array properties used intentionally for performance in kernel arguments
[assembly: SuppressMessage("Performance", "CA1819:Properties should not return arrays",
    Justification = "Array properties used for performance in hot paths. Callers aware of mutability.")]

// IL2092: DynamicallyAccessedMembersAttribute mismatch
// Dynamic type registration required for plugin system extensibility
[assembly: SuppressMessage("Trimming", "IL2092:DynamicallyAccessedMembersAttribute mismatch",
    Justification = "Dynamic type registration required for extensible plugin system. Types validated at runtime.")]

// IDE2002: Consecutive braces formatting
// Code style preference for readability in complex nested structures
[assembly: SuppressMessage("Style", "IDE2002:Consecutive braces must not have blank line between them",
    Justification = "Blank lines improve readability in complex nested structures.")]

// CA1848: Use LoggerMessage delegates
// CA1849: Call async methods when in an async method
// LoggerMessage delegates provide better performance, but runtime services
// prioritize correctness and simplicity. Performance-critical paths don't log.
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Runtime services prioritize correctness over logging performance. Performance-critical paths don't log.")]
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous calls in diagnostic paths intentional to avoid async overhead in service initialization.")]

// CA2000: Dispose objects before losing scope
// Service provider and scope management follows DI container patterns
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Service lifetimes managed by DI container. Scope disposal handled by container.")]

// VSTHRD103: Call async methods when in an async method
// VSTHRD002: Avoid synchronous waits
// Service initialization and setup requires synchronous behavior
[assembly: SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method",
    Justification = "Synchronous calls required for service initialization and configuration.")]
[assembly: SuppressMessage("Usage", "VSTHRD002:Avoid synchronous waits",
    Justification = "Synchronous waits required for deterministic service initialization.")]

// CA1852: Type can be sealed
// Types left unsealed for testing and potential extension
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for testing and potential future extension.")]

// CA2227: Collection properties should be read only
// Mutable collections used in builder and configuration patterns
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only",
    Justification = "Mutable collections used in configuration objects and builder patterns.")]

// XFIX003: XmlFix analyzer warnings
// Runtime module follows framework-specific documentation patterns
[assembly: SuppressMessage("Documentation", "XFIX003",
    Justification = "Runtime module follows framework-specific conventions.")]

// CA1513: Use ObjectDisposedException.ThrowIf
// Explicit exception throwing provides better error messages in plugin context
[assembly: SuppressMessage("Usage", "CA1513:Use ObjectDisposedException throw helper",
    Justification = "Explicit exception throwing provides contextual error messages for plugin lifecycle.")]

// IL2070, IL2067, IL2091, IL2026: Trimming and dynamic access warnings
// Plugin system requires runtime reflection for extensibility
[assembly: SuppressMessage("Trimming", "IL2070:Unrecognized reflection pattern",
    Justification = "Runtime reflection required for extensible plugin system. Types preserved by plugin loader.")]
[assembly: SuppressMessage("Trimming", "IL2067:Unrecognized reflection pattern",
    Justification = "Dynamic type activation required for plugin creation. Types registered explicitly.")]
[assembly: SuppressMessage("Trimming", "IL2091:Unrecognized generic pattern",
    Justification = "Generic activation required for DI container integration.")]
[assembly: SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Assembly scanning required for generated kernel discovery. Kernels preserved by source generator.")]
[assembly: SuppressMessage("Trimming", "IL2075:Unrecognized reflection pattern",
    Justification = "Reflection required for attribute scanning in plugin discovery.")]

// CA5394: Random is insecure
// Random used for non-cryptographic bytecode generation (simulation/testing)
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Random used for deterministic test bytecode generation, not cryptographic purposes.")]

// CA1024: Use properties where appropriate
// Methods with side effects or complex logic appropriately use method syntax
[assembly: SuppressMessage("Design", "CA1024:Use properties where appropriate",
    Justification = "Methods with side effects or complex logic use method syntax for clarity.")]

// CA1716: Identifiers should not match keywords
// Interface method names follow domain conventions
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Method names follow domain-specific conventions. Context makes usage clear.")]

// CA1812: Avoid uninstantiated internal classes
// Internal classes instantiated by DI container or used in runtime scenarios
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Internal classes instantiated by DI container, plugin system, or source generators.")]

// CA1715: Identifiers should have correct prefix
// Type parameters follow domain-specific conventions
[assembly: SuppressMessage("Naming", "CA1715:Identifiers should have correct prefix",
    Justification = "Type parameters use domain-specific names for clarity over convention.")]

// CA1720: Identifier contains type name
// Domain-specific names that happen to match type names
[assembly: SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Domain-appropriate names that convey semantic meaning.")]

// CA1051: Do not declare visible instance fields
// Public fields used in DTOs and data structures for performance
[assembly: SuppressMessage("Design", "CA1051:Do not declare visible instance fields",
    Justification = "Public fields used in performance-critical data structures and DTOs.")]

// CA2264: ArgumentNullException.ThrowIfNull on non-nullable
[assembly: SuppressMessage("Usage", "CA2264:Do not pass a non-nullable value to 'ArgumentNullException.ThrowIfNull'",
    Justification = "Runtime validation provides defense-in-depth even for non-nullable reference types.")]

// CA2213: Disposable fields
[assembly: SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
    Justification = "Parent/service lifetimes managed by DI container or externally. View objects don't own parent resources.")]

// IL3050: RequiresDynamicCode
[assembly: SuppressMessage("Trimming", "IL3050:RequiresDynamicCode",
    Justification = "Configuration binding and JSON serialization required for runtime plugin system. Types preserved explicitly.")]

// XFIX001: Method should be property
[assembly: SuppressMessage("Design", "XFIX001:Method should be a property",
    Justification = "Methods with side effects or complex logic appropriately use method syntax.")]

// CA2016: Forward CancellationToken
[assembly: SuppressMessage("Usage", "CA2016:Forward the CancellationToken parameter",
    Justification = "CancellationToken.None intentionally used for fire-and-forget or independent operations.")]

// CA2201: Reserved exception types
[assembly: SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
    Justification = "OutOfMemoryException raised in memory pool when genuinely out of memory resources.")]

// CA1307: StringComparison
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity",
    Justification = "Ordinal string comparison appropriate for technical identifiers and bytecode patterns.")]

// CA1310: StringComparison for correctness
[assembly: SuppressMessage("Globalization", "CA1310:Specify StringComparison for correctness",
    Justification = "Ordinal string comparison used for technical string operations.")]

// CA1305: IFormatProvider
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
    Justification = "Invariant culture appropriate for technical identifiers and cache keys.")]

// CA1721: Confusing property/method names
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Property provides snapshot, method provides detailed query. Distinction clear in context.")]

// CA1725: Parameter name consistency
[assembly: SuppressMessage("Naming", "CA1725:Parameter names should match base declaration",
    Justification = "More descriptive parameter names used for implementation clarity.")]

// CA1002: Generic collections
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists",
    Justification = "List<T> used in internal APIs for performance. Public APIs use abstractions.")]

// CA2002: Lock on weak identity
[assembly: SuppressMessage("Reliability", "CA2002:Do not lock on objects with weak identity",
    Justification = "Private lock objects used for synchronization in thread-safe statistics tracking.")]

// CA1869: Cache JsonSerializerOptions
[assembly: SuppressMessage("Performance", "CA1869:Cache and reuse JsonSerializerOptions instances",
    Justification = "Serializer options created per-operation for specific formatting requirements.")]

// CA1822: Method can be static
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static",
    Justification = "Instance methods left non-static for potential future state access and testing flexibility.")]

// CA1859: Use concrete types
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface types used for DI flexibility and testability over micro-optimization.")]

// CA1829: Use Length/Count property
[assembly: SuppressMessage("Performance", "CA1829:Use property instead of Count() when available",
    Justification = "LINQ Count() used with filtering; direct property access not applicable.")]

// CA1826: Use indexer
[assembly: SuppressMessage("Performance", "CA1826:Do not use Enumerable methods on indexable collections",
    Justification = "Enumerable methods used with filtering or transformations; indexer not applicable.")]

// CA2215: Call base.DisposeAsync
[assembly: SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose",
    Justification = "Base class DisposeAsync handled by composition pattern. Resources managed independently.")]

// CA1001: Types that own disposable fields should be disposable
[assembly: SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable fields managed by DI container or external lifecycle management.")]

// CA1308: Normalize strings to uppercase
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
    Justification = "Lowercase normalization intentional for file paths and identifiers.")]

// CA1847: Use string.Contains(char)
[assembly: SuppressMessage("Performance", "CA1847:Use char literal for faster string operations",
    Justification = "String operations used for compatibility and clarity.")]

// CA1860: Prefer Length/Count property
[assembly: SuppressMessage("Performance", "CA1860:Avoid using 'Enumerable.Any()' extension method",
    Justification = "LINQ methods used with filtering; direct property access not applicable.")]

// CA1865: Use char overload
[assembly: SuppressMessage("Performance", "CA1865:Use char overload of StartsWith/EndsWith",
    Justification = "String overloads used for consistency and compatibility.")]

// CA1851: Possible multiple enumerations
[assembly: SuppressMessage("Performance", "CA1851:Possible multiple enumerations of IEnumerable collection",
    Justification = "Multiple enumerations intentional in validation and analysis logic.")]

// CA1056: URI properties should not be strings
[assembly: SuppressMessage("Design", "CA1056:URI properties should not be strings",
    Justification = "String URLs used for flexibility in plugin loading scenarios.")]

// CA1861: Avoid constant arrays as arguments
[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments",
    Justification = "Constant arrays used for simplicity in initialization.")]

// CA1416: Platform compatibility
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility",
    Justification = "Platform-specific code guarded by runtime checks.")]

// CA2254: Template should be static
[assembly: SuppressMessage("Usage", "CA2254:Template should be a static expression",
    Justification = "Dynamic template strings required for contextual logging.")]

// CA1065: Do not raise exceptions in unexpected locations
[assembly: SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations",
    Justification = "Exception throwing appropriate for critical validation failures.")]

// IDE0011: Add braces
[assembly: SuppressMessage("Style", "IDE0011:Add braces to 'if' statement",
    Justification = "Single-line if statements without braces acceptable for simple cases.")]

// IDE0019: Use pattern matching
[assembly: SuppressMessage("Style", "IDE0019:Use pattern matching",
    Justification = "Traditional type checks used for clarity and compatibility.")]

// IDE2001: Embedded statements must be on their own line
[assembly: SuppressMessage("Style", "IDE2001:Embedded statements must be on their own line",
    Justification = "Compact formatting used for simple statements.")]

// IDE0059: Unnecessary assignment
[assembly: SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value",
    Justification = "Explicit assignments used for clarity and debugging.")]
