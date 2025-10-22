// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in the Algorithms extension are false positives due to ownership patterns:
// - HttpClient instances are managed by plugin loaders and disposed with containers
// - Stream objects from NuGet downloads transfer ownership to callers
// - Security scanner resources are pooled and disposed by scanner cleanup
// - Plugin lifecycle managers own and dispose loaded assemblies
// The extension follows proper resource management with clear ownership semantics.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Algorithms extension uses ownership transfer patterns. HttpClient managed by plugin loaders. Streams transfer ownership to callers. Scanner resources pooled. Plugin managers own loaded assemblies. Clear ownership prevents leaks.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// Algorithm operations prioritize correctness and diagnostic quality over logging performance
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Algorithm extension prioritizes correctness over logging performance. Diagnostic logs provide detailed operation analysis.")]

// CA1849: Call async methods when in an async method
// Some synchronous operations intentional for deterministic plugin loading
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
    Justification = "Synchronous operations intentional for deterministic plugin loading and initialization.")]

// XDOC001: Missing XML documentation
// Documentation provided for public algorithm APIs.
// Internal implementation details use clear naming for self-documentation.
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Documentation provided for public algorithm APIs. Internal implementations use self-documenting names.")]

// CA1859: Use concrete types when possible for improved performance
// Interface-based design provides better algorithm abstraction and extensibility
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Interface-based design provides better algorithm abstraction and extensibility for plugin system.")]

// CA1852: Type can be sealed
// Types left unsealed for algorithm extension and testing
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Types left unsealed for algorithm extension, testing, and plugin inheritance.")]

// CA5394: Random is insecure
// Random used for non-cryptographic purposes (test data, sampling)
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Random used for test data generation and algorithm sampling, not cryptographic purposes.")]

// IL2026, IL2070, IL2067: Trimming and reflection warnings
// Plugin system requires runtime reflection for dynamic algorithm loading
[assembly: SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Plugin system requires runtime reflection for dynamic algorithm loading. Types preserved by plugin framework.")]
[assembly: SuppressMessage("Trimming", "IL2070:Unrecognized reflection pattern",
    Justification = "Reflection required for plugin discovery and NuGet package analysis.")]
[assembly: SuppressMessage("Trimming", "IL2067:Unrecognized reflection pattern",
    Justification = "Dynamic type loading required for algorithm plugin instantiation.")]

// CS8602: Dereference of a possibly null reference
// Generated LoggerMessage code may produce nullability warnings which are safe to suppress
[assembly: SuppressMessage("Compiler", "CS8602:Dereference of a possibly null reference",
    Justification = "LoggerMessage source generator produces safe code with nullability warnings. ILogger parameter validation handled by generator.")]
