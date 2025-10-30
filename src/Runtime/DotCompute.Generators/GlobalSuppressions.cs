// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// IDE1006: Naming Styles
// Analyzer rule fields follow Microsoft.CodeAnalysis.DiagnosticDescriptor naming conventions
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Analyzer rule fields follow Roslyn DiagnosticDescriptor naming conventions", Scope = "member", Target = "~F:DotCompute.Generators.Kernel.KernelCompilationAnalyzer.UnsupportedTypeRule")]

// CA2000: Dispose objects before losing scope
// Source generators don't create runtime disposable objects that need disposal.
// All analyzer resources (DiagnosticDescriptor, SyntaxReceiver) are managed by Roslyn infrastructure.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Source generators don't create runtime disposable objects. Analyzer resources managed by Roslyn infrastructure.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// Generators run at compile-time; logging performance is not critical
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Generators run at compile-time. Logging performance not critical for build-time analysis.")]

// XDOC001: Missing XML documentation
// Generated code doesn't require XML documentation; analyzer code prioritizes clarity
[assembly: SuppressMessage("Documentation", "XDOC001",
    Justification = "Generated code and analyzer internals use self-documenting names. Public analyzer APIs documented.")]

// CA1859: Use concrete types when possible for improved performance
// Roslyn APIs require interface types (ISymbol, INamedTypeSymbol, etc.)
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Roslyn APIs require interface types for symbol analysis. Concrete types not available.")]

// CA1852: Type can be sealed
// Generator and analyzer types left unsealed for testing
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Generator and analyzer types left unsealed for testing and potential extension.")]

// RS1001-RS2008: Roslyn analyzer rules
// These are analyzer-specific rules that apply to Roslyn analyzer development
[assembly: SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1001:Missing diagnostic analyzer attribute",
    Justification = "Diagnostic descriptors defined in analyzer follow Roslyn patterns.")]
