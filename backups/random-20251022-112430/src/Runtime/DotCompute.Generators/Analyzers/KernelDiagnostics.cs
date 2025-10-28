// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Contains all diagnostic descriptors for kernel analysis.
/// </summary>
public static class KernelDiagnostics
{
    // Kernel Method Validation (DC001-DC003)
    public static readonly DiagnosticDescriptor KernelMethodMustBeStatic = new(
        "DC001",
        "Kernel methods must be static",
        "Kernel method '{0}' must be declared as static for GPU execution. Add 'static' modifier to the method declaration.",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods must be static to be compatible with compute backends. Instance methods cannot be executed on GPU devices as they require object context that doesn't exist in kernel execution."
    );

    public static readonly DiagnosticDescriptor KernelMethodInvalidParameters = new(
        "DC002",
        "Kernel method has invalid parameters",
        "Kernel method '{0}' has invalid parameter '{1}': {2}. Use Span<T> or ReadOnlySpan<T> instead of arrays for better performance.",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods can only use supported parameter types like Span<T>, ReadOnlySpan<T>, and primitive types (int, float, double). Arrays should be converted to Span<T> for zero-copy memory access and better performance. Example: Use 'Span<float>' instead of 'float[]'."
    );

    public static readonly DiagnosticDescriptor KernelMethodUnsupportedConstruct = new(
        "DC003",
        "Kernel method uses unsupported language construct",
        "Kernel method '{0}' uses unsupported construct '{1}' at line {2}. Remove this construct as it cannot be translated to GPU code.",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods cannot use certain C# constructs like exception handling (try-catch), dynamic types, unsafe code, or LINQ expressions. These features are not available on GPU backends and will cause compilation failures. Use simple control flow and arithmetic operations instead."
    );

    // Performance Optimization Suggestions (DC004-DC006)
    public static readonly DiagnosticDescriptor KernelCanBeVectorized = new(
        "DC004",
        "Kernel can benefit from vectorization",
        "Kernel method '{0}' contains loops that can be vectorized using SIMD instructions",
        "DotCompute.Performance",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This kernel contains patterns that can be optimized using SIMD vectorization."
    );

    public static readonly DiagnosticDescriptor KernelSuboptimalMemoryAccess = new(
        "DC005",
        "Kernel has suboptimal memory access pattern",
        "Kernel method '{0}' has memory access pattern that may cause cache misses: {1}",
        "DotCompute.Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Memory access patterns should be optimized for cache locality and coalescing."
    );

    public static readonly DiagnosticDescriptor KernelRegisterSpilling = new(
        "DC006",
        "Kernel may experience register spilling",
        "Kernel method '{0}' uses {1} local variables which may cause register spilling on GPU",
        "DotCompute.Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Too many local variables can cause register spilling, reducing GPU performance."
    );

    // Code Quality Issues (DC007-DC009)
    public static readonly DiagnosticDescriptor KernelMissingKernelAttribute = new(
        "DC007",
        "Method should have [Kernel] attribute",
        "Method '{0}' appears to be a compute kernel but is missing the [Kernel] attribute. Add '[Kernel]' above the method declaration to enable GPU execution.",
        "DotCompute.Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Methods that perform compute operations on Span<T> parameters should be marked with [Kernel] attribute for automatic code generation and GPU compilation. The attribute enables the source generator to create optimized CPU and GPU versions of your code."
    );

    public static readonly DiagnosticDescriptor KernelUnnecessaryComplexity = new(
        "DC008",
        "Kernel can be simplified",
        "Kernel method '{0}' has unnecessary complexity that can be simplified: {1}",
        "DotCompute.Maintainability",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Simpler kernels are easier to optimize and maintain."
    );

    public static readonly DiagnosticDescriptor KernelThreadSafetyWarning = new(
        "DC009",
        "Potential thread safety issue in kernel",
        "Kernel method '{0}' may have thread safety issues: {1}",
        "DotCompute.Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels must be thread-safe when executed in parallel."
    );

    // Usage Pattern Issues (DC010-DC012)
    public static readonly DiagnosticDescriptor KernelIncorrectIndexing = new(
        "DC010",
        "Kernel uses incorrect threading model",
        "Kernel method '{0}' should use Kernel.ThreadId.X for indexing instead of loop variables for optimal GPU performance. Replace 'for' loops with 'int index = Kernel.ThreadId.X;'.",
        "DotCompute.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels should use the threading model provided by Kernel.ThreadId for optimal performance. GPU threads execute in parallel, so traditional for-loops should be replaced with thread-based indexing. Example: 'int index = Kernel.ThreadId.X; if (index < data.Length) data[index] = ...'."
    );

    public static readonly DiagnosticDescriptor KernelMissingBoundsCheck = new(
        "DC011",
        "Kernel missing bounds check",
        "Kernel method '{0}' should check array bounds before accessing elements. Add 'if (index < data.Length)' to prevent out-of-bounds access.",
        "DotCompute.Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels should validate array bounds to prevent runtime errors and undefined behavior on GPU. GPU memory access violations can crash the entire system. Example: 'if (index < data.Length) {{ data[index] = value; }}'."
    );

    public static readonly DiagnosticDescriptor KernelSuboptimalBackendSelection = new(
        "DC012",
        "Kernel backend selection can be optimized",
        "Kernel attribute on '{0}' specifies backends '{1}' but only '{2}' are likely to be beneficial",
        "DotCompute.Performance",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Backend selection should match the kernel's computational characteristics."
    );
}