// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// Suppress test-specific errors where mock implementations don't fully match production APIs
[assembly: SuppressMessage("Compiler", "CS1061:Type does not contain a definition", Justification = "Test mock interface mismatches are acceptable in test projects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Core.Tests")]
[assembly: SuppressMessage("Compiler", "CS1503:Argument type conversion", Justification = "Test mock interface mismatches are acceptable in test projects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Core.Tests")]
[assembly: SuppressMessage("Compiler", "CS1929:Extension method not found", Justification = "Test mock interface mismatches are acceptable in test projects", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Core.Tests")]
[assembly: SuppressMessage("Compiler", "CS0246:Type or namespace not found", Justification = "Test types may be missing during development", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Core.Tests")]

// IDE2006: Suppress blank line warnings in tests for better readability
[assembly: SuppressMessage("Style", "IDE2006:Blank line not allowed after arrow expression clause token", Justification = "Blank lines improve test readability by separating setup from assertions", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Core.Tests")]
