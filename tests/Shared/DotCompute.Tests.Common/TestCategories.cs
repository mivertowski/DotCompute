namespace DotCompute.Tests.Common;

/// <summary>
/// Defines constants for test categories used across the test suite.
/// These categories help organize tests by type, performance characteristics, and execution requirements.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// The unit.
    /// </summary>
    // Unit Test Categories
    public const string Unit = "Unit";
    /// <summary>
    /// The core.
    /// </summary>
    public const string Core = "Core";
    /// <summary>
    /// The memory.
    /// </summary>
    public const string Memory = "Memory";
    /// <summary>
    /// The algorithms.
    /// </summary>
    public const string Algorithms = "Algorithms";
    /// <summary>
    /// The serialization.
    /// </summary>
    public const string Serialization = "Serialization";
    /// <summary>
    /// The configuration.
    /// </summary>
    public const string Configuration = "Configuration";
    /// <summary>
    /// The integration.
    /// </summary>

    // Integration Test Categories

    public const string Integration = "Integration";
    /// <summary>
    /// The backend.
    /// </summary>
    public const string Backend = "Backend";
    /// <summary>
    /// The interop.
    /// </summary>
    public const string Interop = "Interop";
    /// <summary>
    /// The end to end.
    /// </summary>
    public const string EndToEnd = "EndToEnd";
    /// <summary>
    /// The hardware.
    /// </summary>

    // Hardware-Specific Categories

    public const string Hardware = "Hardware";
    /// <summary>
    /// The cuda.
    /// </summary>
    public const string Cuda = "CUDA";
    /// <summary>
    /// The c u d a.
    /// </summary>
    public const string CUDA = "CUDA"; // Alias for compatibility
    /// <summary>
    /// The open c l.
    /// </summary>
    public const string OpenCL = "OpenCL";
    /// <summary>
    /// The c p u.
    /// </summary>
    public const string CPU = "CPU";
    /// <summary>
    /// The g p u.
    /// </summary>
    public const string GPU = "GPU";
    /// <summary>
    /// The multi g p u.
    /// </summary>
    public const string MultiGPU = "MultiGPU";
    /// <summary>
    /// The unified memory.
    /// </summary>
    public const string UnifiedMemory = "UnifiedMemory";
    /// <summary>
    /// The performance.
    /// </summary>

    // Performance Test Categories

    public const string Performance = "Performance";
    /// <summary>
    /// The benchmark.
    /// </summary>
    public const string Benchmark = "Benchmark";
    /// <summary>
    /// The stress.
    /// </summary>
    public const string Stress = "Stress";
    /// <summary>
    /// The long running.
    /// </summary>
    public const string LongRunning = "LongRunning";
    /// <summary>
    /// The memory intensive.
    /// </summary>
    public const string MemoryIntensive = "MemoryIntensive";
    /// <summary>
    /// The reliability.
    /// </summary>

    // Reliability Categories

    public const string Reliability = "Reliability";
    /// <summary>
    /// The error handling.
    /// </summary>
    public const string ErrorHandling = "ErrorHandling";
    /// <summary>
    /// The edge case.
    /// </summary>
    public const string EdgeCase = "EdgeCase";
    /// <summary>
    /// The regression.
    /// </summary>
    public const string Regression = "Regression";
    /// <summary>
    /// The windows.
    /// </summary>

    // Platform-Specific Categories

    public const string Windows = "Windows";
    /// <summary>
    /// The linux.
    /// </summary>
    public const string Linux = "Linux";
    /// <summary>
    /// The mac o s.
    /// </summary>
    public const string MacOS = "MacOS";
    /// <summary>
    /// The cross platform.
    /// </summary>
    public const string CrossPlatform = "CrossPlatform";
    /// <summary>
    /// The x64.
    /// </summary>

    // Architecture-Specific Categories

    public const string x64 = "x64";
    /// <summary>
    /// The a r m64.
    /// </summary>
    public const string ARM64 = "ARM64";
    /// <summary>
    /// The w a s m.
    /// </summary>
    public const string WASM = "WASM";
    /// <summary>
    /// The requires hardware.
    /// </summary>

    // Execution Context Categories

    public const string RequiresHardware = "RequiresHardware";
    /// <summary>
    /// The requires cuda.
    /// </summary>
    public const string RequiresCuda = "RequiresCUDA";
    /// <summary>
    /// The requires open c l.
    /// </summary>
    public const string RequiresOpenCL = "RequiresOpenCL";
    /// <summary>
    /// The requires multi g p u.
    /// </summary>
    public const string RequiresMultiGPU = "RequiresMultiGPU";
    /// <summary>
    /// The requires high memory.
    /// </summary>
    public const string RequiresHighMemory = "RequiresHighMemory";
    /// <summary>
    /// The stable.
    /// </summary>

    // Test Stability Categories

    public const string Stable = "Stable";
    /// <summary>
    /// The flaky.
    /// </summary>
    public const string Flaky = "Flaky";
    /// <summary>
    /// The experimental.
    /// </summary>
    public const string Experimental = "Experimental";
    /// <summary>
    /// The known issue.
    /// </summary>
    public const string KnownIssue = "KnownIssue";
    /// <summary>
    /// The compilation.
    /// </summary>

    // Feature Categories

    public const string Compilation = "Compilation";
    /// <summary>
    /// The kernel compilation.
    /// </summary>
    public const string KernelCompilation = "KernelCompilation";
    /// <summary>
    /// The execution.
    /// </summary>
    public const string Execution = "Execution";
    /// <summary>
    /// The optimization.
    /// </summary>
    public const string Optimization = "Optimization";
    /// <summary>
    /// The parallelization.
    /// </summary>
    public const string Parallelization = "Parallelization";
    /// <summary>
    /// The vector operations.
    /// </summary>
    public const string VectorOperations = "VectorOperations";
    /// <summary>
    /// The matrix operations.
    /// </summary>
    public const string MatrixOperations = "MatrixOperations";
    /// <summary>
    /// The graph operations.
    /// </summary>
    public const string GraphOperations = "GraphOperations";
    /// <summary>
    /// The requires s i m d.
    /// </summary>
    public const string RequiresSIMD = "RequiresSIMD";
    /// <summary>
    /// The security.
    /// </summary>

    // Security Categories

    public const string Security = "Security";
    /// <summary>
    /// The safe mode.
    /// </summary>
    public const string SafeMode = "SafeMode";
    /// <summary>
    /// The unsafe operations.
    /// </summary>
    public const string UnsafeOperations = "UnsafeOperations";
    /// <summary>
    /// The diagnostic.
    /// </summary>

    // Diagnostic Categories

    public const string Diagnostic = "Diagnostic";
    /// <summary>
    /// The profiling.
    /// </summary>
    public const string Profiling = "Profiling";
    /// <summary>
    /// The debugging.
    /// </summary>
    public const string Debugging = "Debugging";
    /// <summary>
    /// The requires cleanup.
    /// </summary>

    // Cleanup Categories

    public const string RequiresCleanup = "RequiresCleanup";
    /// <summary>
    /// The resource intensive.
    /// </summary>
    public const string ResourceIntensive = "ResourceIntensive";
    /// <summary>
    /// The concurrency.
    /// </summary>

    // Additional Test Categories for Enhanced Tests

    public const string Concurrency = "Concurrency";
    /// <summary>
    /// The hardware independent.
    /// </summary>
    public const string HardwareIndependent = "HardwareIndependent";
    /// <summary>
    /// The machine learning.
    /// </summary>
    public const string MachineLearning = "MachineLearning";
    /// <summary>
    /// The scientific computing.
    /// </summary>
    public const string ScientificComputing = "ScientificComputing";
    /// <summary>
    /// The image processing.
    /// </summary>
    public const string ImageProcessing = "ImageProcessing";
    /// <summary>
    /// The data analytics.
    /// </summary>
    public const string DataAnalytics = "DataAnalytics";
    /// <summary>
    /// The numerical analysis.
    /// </summary>
    public const string NumericalAnalysis = "NumericalAnalysis";
    /// <summary>
    /// The statistical computing.
    /// </summary>
    public const string StatisticalComputing = "StatisticalComputing";
    /// <summary>
    /// The linear algebra.
    /// </summary>
    public const string LinearAlgebra = "LinearAlgebra";
    /// <summary>
    /// The signal processing.
    /// </summary>
    public const string SignalProcessing = "SignalProcessing";
    /// <summary>
    /// The advanced features.
    /// </summary>

    // Missing categories used in existing tests  

    public const string AdvancedFeatures = "AdvancedFeatures";
    /// <summary>
    /// The requires g p u.
    /// </summary>
    public const string RequiresGPU = "RequiresGPU";
    /// <summary>
    /// The requires c u d a.
    /// </summary>

    // Aliases for compatibility with existing code

    public const string RequiresCUDA = RequiresCuda; // Alias for mixed-case version
    /// <summary>
    /// The unit core.
    /// </summary>

    // Combination Categories (commonly used combinations)

    public const string UnitCore = Unit + "," + Core;
    /// <summary>
    /// The integration cuda.
    /// </summary>
    public const string IntegrationCuda = Integration + "," + Cuda;
    /// <summary>
    /// The performance g p u.
    /// </summary>
    public const string PerformanceGPU = Performance + "," + GPU;
    /// <summary>
    /// The stress memory.
    /// </summary>
    public const string StressMemory = Stress + "," + Memory;
    /// <summary>
    /// The benchmark crossplatform.
    /// </summary>
    public const string BenchmarkCrossplatform = Benchmark + "," + CrossPlatform;
}