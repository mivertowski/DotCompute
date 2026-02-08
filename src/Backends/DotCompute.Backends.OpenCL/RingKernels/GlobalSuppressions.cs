// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

// Suppress CA1711 for message queue implementations (intentional naming)
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "MessageQueue is an intentional and descriptive name for this component",
    Scope = "type",
    Target = "~T:DotCompute.Backends.OpenCL.RingKernels.OpenCLMessageQueue`1")]

// Suppress VSTHRD114 for nullable return type patterns
[assembly: SuppressMessage("Usage", "VSTHRD114:Avoid returning null from a Task-returning method",
    Justification = "Nullable return types properly indicate optional values",
    Scope = "member",
    Target = "~M:DotCompute.Backends.OpenCL.RingKernels.OpenCLMessageQueue`1.TryDequeueAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Nullable{DotCompute.Abstractions.RingKernels.KernelMessage{`0}}}")]
