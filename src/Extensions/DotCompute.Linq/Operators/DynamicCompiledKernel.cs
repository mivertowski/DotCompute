// <copyright file="DynamicCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators;

/// <summary>
/// This file has been refactored. The types previously defined here have been moved to their respective namespaces:
/// 
/// Kernel Types (2 classes):
/// - DotCompute.Linq.Operators.Kernels.DynamicCompiledKernel
/// - DotCompute.Linq.Operators.Kernels.ExpressionFallbackKernel
/// 
/// Compilation Types (4 classes + 1 interface):
/// - DotCompute.Linq.Operators.Compilation.IKernelCompiler (interface)
/// - DotCompute.Linq.Operators.Compilation.AcceleratorKernelCompiler
/// - DotCompute.Linq.Operators.Compilation.CpuFallbackKernelCompiler
/// - DotCompute.Linq.Operators.Compilation.KernelCompilerAdapter
/// 
/// Execution Types (3 classes + 1 interface):
/// - DotCompute.Linq.Operators.Execution.ICompiledKernel (interface)
/// - DotCompute.Linq.Operators.Execution.CompiledKernelAdapter
/// - DotCompute.Linq.Operators.Execution.CpuFallbackCompiledKernel
/// 
/// Model Types (3 classes + 1 enum):
/// - DotCompute.Linq.Operators.Models.KernelCompilationRequest
/// - DotCompute.Linq.Operators.Models.KernelCompilationResult
/// - DotCompute.Linq.Operators.Models.KernelExecutionParameters
/// - DotCompute.Linq.Operators.Models.OptimizationLevel (enum)
/// 
/// Caching Types (1 static class):
/// - DotCompute.Linq.Operators.Caching.KernelCompilationCache
/// 
/// Mock Types (2 classes):
/// - DotCompute.Linq.Operators.Mocks.MockCompiledKernel
/// - DotCompute.Linq.Operators.Mocks.MockKernelSource
/// 
/// Total: 15 types successfully refactored into organized namespaces
/// </summary>
[System.Obsolete("This file is deprecated. All types have been moved to their respective namespaces under DotCompute.Linq.Operators. This file will be removed in a future version.")]
internal static class DynamicCompiledKernelDeprecated
{
    // This class serves as a marker for the deprecated file
}