// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// CA1063: Dispose patterns are acceptable in test fixtures where setup/teardown is managed by test frameworks
[assembly: SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Test fixtures use xUnit lifecycle management, not full IDisposable pattern", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1307: String comparison is acceptable in test assertions where culture-specific behavior is irrelevant
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "Test assertions don't require explicit string comparison for readability", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1822: Static member suggestions in test methods break test discovery and fixture patterns
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Test methods must be instance members for xUnit discovery and fixture injection", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// XFIX002: String.Contains without StringComparison is acceptable in tests for simplicity
[assembly: SuppressMessage("Usage", "XFIX002:Use StringComparison", Justification = "Test assertions prioritize readability over explicit string comparison", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1031: General exception catch is acceptable in test error scenarios
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Tests validate error handling and may intentionally catch all exceptions", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1816: Dispose(false) calls are managed by test frameworks
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Test fixtures don't require finalizer suppression as lifecycle is managed by xUnit", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA2007: ConfigureAwait is not needed in tests
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Tests run in controlled environments without synchronization context concerns", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// IDE2006: Suppress blank line warnings in tests for better readability
[assembly: SuppressMessage("Style", "IDE2006:Blank line not allowed after arrow expression clause token", Justification = "Blank lines improve test readability by separating setup from assertions", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1034: Nested types are acceptable in test classes for test data organization
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Test data classes are often nested for organization", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1806: Ignore result of expressions in tests where side effects are being validated
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Tests may intentionally ignore results when validating side effects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA2000: Dispose ownership is often transferred in tests
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Test fixtures often transfer ownership of disposable objects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1062: Null parameter validation not needed in tests
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Tests intentionally pass null values to validate error handling", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]

// CA1416: Platform compatibility checks are handled by SkippableFact
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Hardware tests use SkippableFact to skip on unsupported platforms", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Hardware.Cuda.Tests")]
