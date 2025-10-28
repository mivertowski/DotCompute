namespace DotCompute.Tests.Common;

/// <summary>
/// Defines constants for test categories used across the test suite.
/// These categories help organize tests by type, performance characteristics, and execution requirements.
/// </summary>
public static class TestCategories
{
    // Unit Test Categories

    /// <summary>
    /// Test category for unit tests.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// Test category for core functionality tests.
    /// </summary>
    public const string Core = "Core";

    /// <summary>
    /// Test category for memory management tests.
    /// </summary>
    public const string Memory = "Memory";

    /// <summary>
    /// Test category for algorithm tests.
    /// </summary>
    public const string Algorithms = "Algorithms";

    /// <summary>
    /// Test category for serialization tests.
    /// </summary>
    public const string Serialization = "Serialization";

    /// <summary>
    /// Test category for configuration tests.
    /// </summary>
    public const string Configuration = "Configuration";

    // Integration Test Categories

    /// <summary>
    /// Test category for integration tests.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Test category for backend integration tests.
    /// </summary>
    public const string Backend = "Backend";

    /// <summary>
    /// Test category for interoperability tests.
    /// </summary>
    public const string Interop = "Interop";

    /// <summary>
    /// Test category for end-to-end tests.
    /// </summary>
    public const string EndToEnd = "EndToEnd";

    // Hardware-Specific Categories

    /// <summary>
    /// Test category for hardware-specific tests.
    /// </summary>
    public const string Hardware = "Hardware";

    /// <summary>
    /// Test category for CUDA tests.
    /// </summary>
    public const string Cuda = "CUDA";

    /// <summary>
    /// Test category for CUDA tests (alias for compatibility).
    /// </summary>
    public const string CUDA = "CUDA";

    /// <summary>
    /// Test category for OpenCL tests.
    /// </summary>
    public const string OpenCL = "OpenCL";

    /// <summary>
    /// Test category for CPU tests.
    /// </summary>
    public const string CPU = "CPU";

    /// <summary>
    /// Test category for GPU tests.
    /// </summary>
    public const string GPU = "GPU";

    /// <summary>
    /// Test category for multi-GPU tests.
    /// </summary>
    public const string MultiGPU = "MultiGPU";

    /// <summary>
    /// Test category for unified memory tests.
    /// </summary>
    public const string UnifiedMemory = "UnifiedMemory";

    // Performance Test Categories

    /// <summary>
    /// Test category for performance tests.
    /// </summary>
    public const string Performance = "Performance";

    /// <summary>
    /// Test category for benchmark tests.
    /// </summary>
    public const string Benchmark = "Benchmark";

    /// <summary>
    /// Test category for stress tests.
    /// </summary>
    public const string Stress = "Stress";

    /// <summary>
    /// Test category for long-running tests.
    /// </summary>
    public const string LongRunning = "LongRunning";

    /// <summary>
    /// Test category for memory-intensive tests.
    /// </summary>
    public const string MemoryIntensive = "MemoryIntensive";

    // Reliability Categories

    /// <summary>
    /// Test category for reliability tests.
    /// </summary>
    public const string Reliability = "Reliability";

    /// <summary>
    /// Test category for error handling tests.
    /// </summary>
    public const string ErrorHandling = "ErrorHandling";

    /// <summary>
    /// Test category for edge case tests.
    /// </summary>
    public const string EdgeCase = "EdgeCase";

    /// <summary>
    /// Test category for regression tests.
    /// </summary>
    public const string Regression = "Regression";

    // Platform-Specific Categories

    /// <summary>
    /// Test category for Windows platform tests.
    /// </summary>
    public const string Windows = "Windows";

    /// <summary>
    /// Test category for Linux platform tests.
    /// </summary>
    public const string Linux = "Linux";

    /// <summary>
    /// Test category for macOS platform tests.
    /// </summary>
    public const string MacOS = "MacOS";

    /// <summary>
    /// Test category for cross-platform tests.
    /// </summary>
    public const string CrossPlatform = "CrossPlatform";

    // Architecture-Specific Categories

    /// <summary>
    /// Test category for x64 architecture tests.
    /// </summary>
    public const string x64 = "x64";

    /// <summary>
    /// Test category for ARM64 architecture tests.
    /// </summary>
    public const string ARM64 = "ARM64";

    /// <summary>
    /// Test category for WebAssembly tests.
    /// </summary>
    public const string WASM = "WASM";

    // Execution Context Categories

    /// <summary>
    /// Test category for tests requiring specific hardware.
    /// </summary>
    public const string RequiresHardware = "RequiresHardware";

    /// <summary>
    /// Test category for tests requiring CUDA support.
    /// </summary>
    public const string RequiresCuda = "RequiresCUDA";

    /// <summary>
    /// Test category for tests requiring OpenCL support.
    /// </summary>
    public const string RequiresOpenCL = "RequiresOpenCL";

    /// <summary>
    /// Test category for tests requiring multiple GPUs.
    /// </summary>
    public const string RequiresMultiGPU = "RequiresMultiGPU";

    /// <summary>
    /// Test category for tests requiring high memory.
    /// </summary>
    public const string RequiresHighMemory = "RequiresHighMemory";

    // Test Stability Categories

    /// <summary>
    /// Test category for stable tests.
    /// </summary>
    public const string Stable = "Stable";

    /// <summary>
    /// Test category for flaky tests.
    /// </summary>
    public const string Flaky = "Flaky";

    /// <summary>
    /// Test category for experimental tests.
    /// </summary>
    public const string Experimental = "Experimental";

    /// <summary>
    /// Test category for tests with known issues.
    /// </summary>
    public const string KnownIssue = "KnownIssue";

    // Feature Categories

    /// <summary>
    /// Test category for compilation tests.
    /// </summary>
    public const string Compilation = "Compilation";

    /// <summary>
    /// Test category for kernel compilation tests.
    /// </summary>
    public const string KernelCompilation = "KernelCompilation";

    /// <summary>
    /// Test category for execution tests.
    /// </summary>
    public const string Execution = "Execution";

    /// <summary>
    /// Test category for optimization tests.
    /// </summary>
    public const string Optimization = "Optimization";

    /// <summary>
    /// Test category for parallelization tests.
    /// </summary>
    public const string Parallelization = "Parallelization";

    /// <summary>
    /// Test category for vector operation tests.
    /// </summary>
    public const string VectorOperations = "VectorOperations";

    /// <summary>
    /// Test category for matrix operation tests.
    /// </summary>
    public const string MatrixOperations = "MatrixOperations";

    /// <summary>
    /// Test category for graph operation tests.
    /// </summary>
    public const string GraphOperations = "GraphOperations";

    /// <summary>
    /// Test category for tests requiring SIMD support.
    /// </summary>
    public const string RequiresSIMD = "RequiresSIMD";

    // Security Categories

    /// <summary>
    /// Test category for security tests.
    /// </summary>
    public const string Security = "Security";

    /// <summary>
    /// Test category for safe mode tests.
    /// </summary>
    public const string SafeMode = "SafeMode";

    /// <summary>
    /// Test category for unsafe operation tests.
    /// </summary>
    public const string UnsafeOperations = "UnsafeOperations";

    // Diagnostic Categories

    /// <summary>
    /// Test category for diagnostic tests.
    /// </summary>
    public const string Diagnostic = "Diagnostic";

    /// <summary>
    /// Test category for profiling tests.
    /// </summary>
    public const string Profiling = "Profiling";

    /// <summary>
    /// Test category for debugging tests.
    /// </summary>
    public const string Debugging = "Debugging";

    // Cleanup Categories

    /// <summary>
    /// Test category for tests requiring cleanup.
    /// </summary>
    public const string RequiresCleanup = "RequiresCleanup";

    /// <summary>
    /// Test category for resource-intensive tests.
    /// </summary>
    public const string ResourceIntensive = "ResourceIntensive";

    // Additional Test Categories for Enhanced Tests

    /// <summary>
    /// Test category for concurrency tests.
    /// </summary>
    public const string Concurrency = "Concurrency";

    /// <summary>
    /// Test category for hardware-independent tests.
    /// </summary>
    public const string HardwareIndependent = "HardwareIndependent";

    /// <summary>
    /// Test category for machine learning tests.
    /// </summary>
    public const string MachineLearning = "MachineLearning";

    /// <summary>
    /// Test category for scientific computing tests.
    /// </summary>
    public const string ScientificComputing = "ScientificComputing";

    /// <summary>
    /// Test category for image processing tests.
    /// </summary>
    public const string ImageProcessing = "ImageProcessing";

    /// <summary>
    /// Test category for data analytics tests.
    /// </summary>
    public const string DataAnalytics = "DataAnalytics";

    /// <summary>
    /// Test category for numerical analysis tests.
    /// </summary>
    public const string NumericalAnalysis = "NumericalAnalysis";

    /// <summary>
    /// Test category for statistical computing tests.
    /// </summary>
    public const string StatisticalComputing = "StatisticalComputing";

    /// <summary>
    /// Test category for linear algebra tests.
    /// </summary>
    public const string LinearAlgebra = "LinearAlgebra";

    /// <summary>
    /// Test category for signal processing tests.
    /// </summary>
    public const string SignalProcessing = "SignalProcessing";

    // Missing categories used in existing tests

    /// <summary>
    /// Test category for advanced feature tests.
    /// </summary>
    public const string AdvancedFeatures = "AdvancedFeatures";

    /// <summary>
    /// Test category for tests requiring GPU support.
    /// </summary>
    public const string RequiresGPU = "RequiresGPU";

    // Aliases for compatibility with existing code

    /// <summary>
    /// Test category for tests requiring CUDA support (alias).
    /// </summary>
    public const string RequiresCUDA = RequiresCuda;

    // Combination Categories (commonly used combinations)

    /// <summary>
    /// Combined test category for unit tests of core functionality.
    /// </summary>
    public const string UnitCore = Unit + "," + Core;

    /// <summary>
    /// Combined test category for CUDA integration tests.
    /// </summary>
    public const string IntegrationCuda = Integration + "," + Cuda;

    /// <summary>
    /// Combined test category for GPU performance tests.
    /// </summary>
    public const string PerformanceGPU = Performance + "," + GPU;

    /// <summary>
    /// Combined test category for memory stress tests.
    /// </summary>
    public const string StressMemory = Stress + "," + Memory;

    /// <summary>
    /// Combined test category for cross-platform benchmarks.
    /// </summary>
    public const string BenchmarkCrossplatform = Benchmark + "," + CrossPlatform;
}