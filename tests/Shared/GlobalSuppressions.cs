// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// Suppress XML documentation warnings in test projects
[assembly: SuppressMessage("Documentation", "XDOC001:Missing XML comment for publicly visible type or member", Justification = "XML documentation not required for test code")]

// Suppress IDisposable warnings for mock objects that are managed by the test framework
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Mock objects are managed by test framework lifecycle")]
