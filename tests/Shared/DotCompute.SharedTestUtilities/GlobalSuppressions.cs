// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA1063: Dispose patterns are acceptable in test utilities where lifecycle is managed by consumers
[assembly: SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Test utility classes delegate disposal to test framework lifecycle management", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1307: String comparison is acceptable in test utilities for simplicity
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "Test utilities prioritize simplicity over explicit string comparison", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1822: Static member suggestions in test utilities may break fluent API patterns
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Test utility methods may need to be instance members for fluent API patterns", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// XFIX002: String.Contains without StringComparison is acceptable in test utilities
[assembly: SuppressMessage("Usage", "XFIX002:Use StringComparison", Justification = "Test utilities prioritize readability over explicit string comparison", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1031: General exception catch is acceptable in test utilities for error scenarios
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Test utilities may need to catch all exceptions for validation and reporting", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1816: Dispose(false) calls are managed by test frameworks
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Test utility lifecycle is managed by consuming test frameworks", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA2007: ConfigureAwait is not needed in test utilities
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Test utilities run in controlled environments without synchronization context concerns", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// IDE2006: Suppress blank line warnings for better readability
[assembly: SuppressMessage("Style", "IDE2006:Blank line not allowed after arrow expression clause token", Justification = "Blank lines improve utility code readability", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1034: Nested types are acceptable for test data organization
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Nested types organize test data and builders logically", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1806: Ignore result is acceptable when validating side effects
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Utilities may intentionally ignore results when validating side effects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA2000: Dispose ownership is often transferred in test utilities
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Test utilities often create objects whose ownership is transferred to tests", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1062: Null parameter validation not needed in test utilities
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Test utilities may intentionally accept null values for validation scenarios", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]

// CA1416: Platform compatibility is validated by consuming tests
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Platform compatibility is validated by consuming test projects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.SharedTestUtilities")]
