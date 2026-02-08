// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// Suppress analyzer warnings for Ring Kernel simulation code
// These are test/fallback implementations and don't need the same performance optimizations as production GPU code

// CA1711: Rename type name so that it does not end in 'Queue'
// Justification: "MessageQueue" is a standard and appropriate name for this type
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "MessageQueue is an appropriate name for a message queue type",
    Scope = "type",
    Target = "~T:DotCompute.Backends.CPU.RingKernels.CpuMessageQueue`1")]

// XFIX003: Use LoggerMessage.Define
// Justification: This is CPU fallback/test code, not performance-critical production GPU code
[assembly: SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define for the logging call",
    Justification = "CPU fallback code doesn't require LoggerMessage performance optimization",
    Scope = "namespaceanddescendants",
    Target = "~N:DotCompute.Backends.CPU.RingKernels")]

// CA1513: Use ObjectDisposedException.ThrowIf
// Justification: Manual pattern provides clearer error messages
[assembly: SuppressMessage("Performance", "CA1513:Use ObjectDisposedException.ThrowIf",
    Justification = "Manual throw provides clearer error messages for debugging",
    Scope = "namespaceanddescendants",
    Target = "~N:DotCompute.Backends.CPU.RingKernels")]

// IDE0044: Make field readonly
// Justification: Volatile fields cannot be readonly
[assembly: SuppressMessage("Style", "IDE0044:Add readonly modifier",
    Justification = "Volatile fields cannot be readonly",
    Scope = "namespaceanddescendants",
    Target = "~N:DotCompute.Backends.CPU.RingKernels")]
