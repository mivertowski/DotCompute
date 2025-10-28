// <copyright file="BenchmarkSuiteType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Types;

/// <summary>
/// Defines the types of benchmark suites available for performance testing.
/// Each suite focuses on different aspects of computational performance.
/// </summary>
public enum BenchmarkSuiteType
{
    /// <summary>
    /// Basic computational benchmarks.
    /// Tests fundamental arithmetic and logical operations.
    /// </summary>
    Basic,

    /// <summary>
    /// Memory bandwidth benchmarks.
    /// Measures memory transfer rates and access patterns.
    /// </summary>
    Memory,

    /// <summary>
    /// Linear algebra benchmarks.
    /// Tests matrix operations, vector computations, and BLAS routines.
    /// </summary>
    LinearAlgebra,

    /// <summary>
    /// FFT benchmarks.
    /// Evaluates Fast Fourier Transform performance.
    /// </summary>
    FFT,

    /// <summary>
    /// Comprehensive benchmark suite.
    /// Runs all available benchmark categories for complete performance assessment.
    /// </summary>
    Comprehensive,

    /// <summary>
    /// Stress test benchmarks.
    /// Pushes hardware to its limits to test stability and sustained performance.
    /// </summary>
    StressTest
}