namespace DotCompute.Tests.Common;

/// <summary>
/// Defines constants for test categories used across the test suite.
/// These categories help organize tests by type, performance characteristics, and execution requirements.
/// </summary>
public static class TestCategories
{
    // Unit Test Categories
    public const string Unit = "Unit";
    public const string Core = "Core";
    public const string Memory = "Memory";
    public const string Algorithms = "Algorithms";
    public const string Serialization = "Serialization";
    public const string Configuration = "Configuration";
    
    // Integration Test Categories
    public const string Integration = "Integration";
    public const string Backend = "Backend";
    public const string Interop = "Interop";
    public const string EndToEnd = "EndToEnd";
    
    // Hardware-Specific Categories
    public const string Hardware = "Hardware";
    public const string Cuda = "CUDA";
    public const string OpenCL = "OpenCL";
    public const string CPU = "CPU";
    public const string GPU = "GPU";
    public const string MultiGPU = "MultiGPU";
    public const string UnifiedMemory = "UnifiedMemory";
    
    // Performance Test Categories
    public const string Performance = "Performance";
    public const string Benchmark = "Benchmark";
    public const string Stress = "Stress";
    public const string LongRunning = "LongRunning";
    public const string MemoryIntensive = "MemoryIntensive";
    
    // Reliability Categories
    public const string Reliability = "Reliability";
    public const string ErrorHandling = "ErrorHandling";
    public const string EdgeCase = "EdgeCase";
    public const string Regression = "Regression";
    
    // Platform-Specific Categories
    public const string Windows = "Windows";
    public const string Linux = "Linux";
    public const string MacOS = "MacOS";
    public const string CrossPlatform = "CrossPlatform";
    
    // Architecture-Specific Categories
    public const string x64 = "x64";
    public const string ARM64 = "ARM64";
    public const string WASM = "WASM";
    
    // Execution Context Categories
    public const string RequiresHardware = "RequiresHardware";
    public const string RequiresCuda = "RequiresCUDA";
    public const string RequiresOpenCL = "RequiresOpenCL";
    public const string RequiresMultiGPU = "RequiresMultiGPU";
    public const string RequiresHighMemory = "RequiresHighMemory";
    
    // Test Stability Categories
    public const string Stable = "Stable";
    public const string Flaky = "Flaky";
    public const string Experimental = "Experimental";
    public const string KnownIssue = "KnownIssue";
    
    // Feature Categories
    public const string Compilation = "Compilation";
    public const string Execution = "Execution";
    public const string Optimization = "Optimization";
    public const string Parallelization = "Parallelization";
    public const string VectorOperations = "VectorOperations";
    public const string MatrixOperations = "MatrixOperations";
    public const string GraphOperations = "GraphOperations";
    
    // Security Categories
    public const string Security = "Security";
    public const string SafeMode = "SafeMode";
    public const string UnsafeOperations = "UnsafeOperations";
    
    // Diagnostic Categories
    public const string Diagnostic = "Diagnostic";
    public const string Profiling = "Profiling";
    public const string Debugging = "Debugging";
    
    // Cleanup Categories
    public const string RequiresCleanup = "RequiresCleanup";
    public const string ResourceIntensive = "ResourceIntensive";
    
    // Additional Test Categories for Enhanced Tests
    public const string Concurrency = "Concurrency";
    public const string HardwareIndependent = "HardwareIndependent";
    public const string MachineLearning = "MachineLearning";
    public const string ScientificComputing = "ScientificComputing";
    public const string ImageProcessing = "ImageProcessing";
    public const string DataAnalytics = "DataAnalytics";
    public const string NumericalAnalysis = "NumericalAnalysis";
    public const string StatisticalComputing = "StatisticalComputing";
    public const string LinearAlgebra = "LinearAlgebra";
    public const string SignalProcessing = "SignalProcessing";
    
    // Missing categories used in existing tests  
    public const string AdvancedFeatures = "AdvancedFeatures";
    public const string RequiresGPU = "RequiresGPU";
    
    // Aliases for compatibility with existing code
    public const string RequiresCUDA = RequiresCuda; // Alias for mixed-case version
    
    // Combination Categories (commonly used combinations)
    public const string UnitCore = Unit + "," + Core;
    public const string IntegrationCuda = Integration + "," + Cuda;
    public const string PerformanceGPU = Performance + "," + GPU;
    public const string StressMemory = Stress + "," + Memory;
    public const string BenchmarkCrossplatform = Benchmark + "," + CrossPlatform;
}