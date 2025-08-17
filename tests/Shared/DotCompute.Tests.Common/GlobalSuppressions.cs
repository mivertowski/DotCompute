// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates instead of calling LoggerExtensions methods", Justification = "Test code uses simple logging for debugging purposes")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Test code does not require culture-specific formatting")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Tests use interfaces for mockability")]
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Test classes need mutable collections")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test data classes need settable collections")]
[assembly: SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Justification = "Matrix operations require multidimensional arrays")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Test objects are often disposed by test framework or not needed")]
[assembly: SuppressMessage("Performance", "CA1854:Prefer TryGetValue over ContainsKey", Justification = "Test code prioritizes readability")]
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "Test code uses default string comparison")]
[assembly: SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Test methods may have side effects")]
[assembly: SuppressMessage("Design", "CA2214:Do not call overridable methods in constructors", Justification = "Test base classes need initialization flexibility")]
[assembly: SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Test code simulates runtime exceptions")]
[assembly: SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Test code simulates various error conditions")]
[assembly: SuppressMessage("Usage", "CA1805:Do not initialize unnecessarily", Justification = "Test code may explicitly initialize for clarity")]
[assembly: SuppressMessage("Style", "IDE2002:Consecutive braces should not have blank line between them", Justification = "Test code prioritizes readability")]
[assembly: SuppressMessage("Usage", "CA1063:Implement IDisposable correctly", Justification = "Test implementations use simplified dispose patterns")]
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call GC.SuppressFinalize", Justification = "Test implementations use simplified dispose patterns")]
[assembly: SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Test code may have assignments for debugging")]
[assembly: SuppressMessage("Globalization", "CA1308:Use ToUpperInvariant instead of ToLowerInvariant", Justification = "Test code uses consistent casing conventions")]