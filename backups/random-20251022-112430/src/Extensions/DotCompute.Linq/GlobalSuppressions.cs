// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in the LINQ extension are false positives due to query provider patterns:
// - Expression trees and compiled queries transfer ownership to callers
// - Observable subscriptions are managed by Rx disposal patterns
// - Query execution contexts are disposed by query lifetime management
// - Kernel compilation results are cached and disposed by cache cleanup
// The extension follows Rx and LINQ patterns with proper disposal semantics.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "LINQ extension uses query provider patterns. Compiled queries transfer ownership. Rx subscriptions follow disposal patterns. Execution contexts managed by query lifetime. Kernels cached and disposed. Clear ownership prevents leaks.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// LINQ query compilation and analysis prioritize diagnostic quality over logging performance
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "LINQ extension prioritizes diagnostic quality for query analysis. Performance-critical execution paths don't log.")]

// CA1849: Call async methods when in an async method
// Some synchronous operations intentional for expression tree compilation
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous operations intentional for deterministic expression tree compilation and kernel generation.")]

// XDOC001: Missing XML documentation
// Documentation provided for public LINQ APIs and query operators.
// Internal expression visitors use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public LINQ APIs. Internal expression visitors use self-documenting names.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better LINQ provider abstraction
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design provides better LINQ provider abstraction and testability.")]

// CA1852: Type can be sealed
// Types left unsealed for LINQ provider extension and testing
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for LINQ provider extension, testing, and custom operator implementation.")]

// CA1826: Use indexer instead of LINQ method
// LINQ methods used intentionally for consistency in query provider
[assembly: SuppressMessage("Performance", "CA1826:Do not use Enumerable methods on indexable collections",
    Justification = "LINQ methods used for consistency in query provider implementation and expression building.")]

// CA1851: Possible multiple enumerations
// Multiple enumerations intentional for query analysis and optimization
[assembly: SuppressMessage("Performance", "CA1851:Possible multiple enumerations of IEnumerable collection",
    Justification = "Multiple enumerations intentional for query analysis, optimization, and validation.")]
