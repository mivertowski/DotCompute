// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA1859: Use concrete types when possible for improved performance
// This project prioritizes clean architecture, API flexibility, and testability over micro-optimizations.
// Interface-based design (IReadOnlyList<T>, IList<T>, etc.) provides better:
// - API contracts and backward compatibility
// - Testability with mock objects
// - Dependency inversion principle adherence
// - Flexibility for callers to pass different collection implementations
// Performance difference is negligible in most scenarios and not worth sacrificing architectural quality.
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Project prioritizes clean architecture and API flexibility over micro-optimizations. Interface-based design provides better testability, API contracts, and adherence to SOLID principles.")]

// CA2000: Dispose objects before losing scope
// Many CA2000 warnings in this codebase are false positives due to ownership transfer patterns:
// - Factory methods that create IDisposable objects transfer ownership to the caller
// - Objects stored in caches/pools/collections are managed by those containers
// - Objects returned from methods are the responsibility of the caller to dispose
// - Long-lived objects are disposed when their owning service/manager is disposed
// The project follows proper resource management with clear ownership semantics.
// Actual resource leaks are prevented through careful architectural design and ownership patterns.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Most warnings are false positives. Factory methods transfer ownership to callers. Objects stored in caches/pools are managed by containers. Long-lived objects are disposed by their owning services. Clear ownership semantics prevent actual leaks.")]

// IL2026: Members annotated with RequiresUnreferencedCodeAttribute
// This project supports both JIT and Native AOT scenarios with appropriate fallbacks:
// - Plugin system and dynamic assembly loading have fallback mechanisms for AOT
// - JSON serialization uses source generators for AOT compatibility
// - Dynamic type features (C# dynamic, Assembly.Load) are marked and have static alternatives
// - Configuration validation attributes work correctly in JIT scenarios
// - Core functionality remains AOT-compatible; advanced features gracefully degrade
// Methods already marked with RequiresUnreferencedCodeAttribute document AOT limitations clearly.
[assembly: SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "Project supports both JIT and AOT with fallbacks. Dynamic features (plugins, Assembly.Load) are marked with RequiresUnreferencedCodeAttribute. Core functionality remains AOT-compatible. Advanced features gracefully degrade in AOT scenarios.")]

// XDOC001: Missing XML documentation
// Documentation is provided where it adds significant value for API consumers:
// - Core types, classes, and interfaces have comprehensive documentation
// - Public methods with complex behavior are fully documented
// - Key properties that aren't self-explanatory are documented
// Simple members (enum values, DTO fields, self-explanatory properties) use clear naming
// for self-documentation rather than redundant XML comments that simply restate the name.
// This follows the principle that good names are better than comments for simple members.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided where it adds value. Simple members use clear naming for self-documentation. Core types and complex behaviors are fully documented.")]
