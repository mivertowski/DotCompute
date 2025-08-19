// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Integration
{

/// <summary>
/// Static class containing LoggerMessage delegates for integration tests to fix CA1848 warnings.
/// </summary>
internal static partial class LoggerMessages
{
    // Memory transfer test messages
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Skipping {TestType} - no accelerators available")]
    public static partial void SkippingTestNoAccelerators(ILogger logger, string testType);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Total individual transfer time: {TransferTime:F2}ms")]
    public static partial void TotalTransferTime(ILogger logger, double transferTime);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Actual concurrent execution time: {ConcurrentTime:F2}ms")]
    public static partial void ConcurrentExecutionTime(ILogger logger, double concurrentTime);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Regular memory throughput: {Throughput:F2} MB/s")]
    public static partial void RegularMemoryThroughput(ILogger logger, double throughput);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Pinned memory throughput: {Throughput:F2} MB/s")]
    public static partial void PinnedMemoryThroughput(ILogger logger, double throughput);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Regular transfer time: {TransferTime:F2}ms")]
    public static partial void RegularTransferTime(ILogger logger, double transferTime);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Pinned transfer time: {TransferTime:F2}ms")]
    public static partial void PinnedTransferTime(ILogger logger, double transferTime);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Skipping {TestType} - need at least 2 accelerators")]
    public static partial void SkippingTestNeedTwoAccelerators(ILogger logger, string testType);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Skipping {MemoryType} memory test - no accelerators available")]
    public static partial void SkippingMemoryTypeTest(ILogger logger, string memoryType);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Skipping {TestType} - insufficient device memory")]
    public static partial void SkippingTestInsufficientMemory(ILogger logger, string testType);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Generating test data for {ElementCount} elements")]
    public static partial void GeneratingTestData(ILogger logger, int elementCount);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Test data generated successfully: {ElementCount} elements")]
    public static partial void TestDataGenerated(ILogger logger, int elementCount);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Information,
        Message = "Attempting to create input buffer for {ElementCount} elements({SizeMB}MB)")]
    public static partial void CreatingInputBuffer(ILogger logger, int elementCount, long sizeMB);

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Information,
        Message = "Input buffer created successfully")]
    public static partial void InputBufferCreated(ILogger logger);

    [LoggerMessage(
        EventId = 1015,
        Level = LogLevel.Information,
        Message = "Reading data back from buffer...")]
    public static partial void ReadingDataBack(ILogger logger);

    [LoggerMessage(
        EventId = 1016,
        Level = LogLevel.Information,
        Message = "Read data result: {DataInfo}")]
    public static partial void ReadDataResult(ILogger logger, string dataInfo);

    [LoggerMessage(
        EventId = 1017,
        Level = LogLevel.Information,
        Message = "Initial integrity check: {Integrity}(readData != null: {NotNull}, lengths match: {LengthsMatch})")]
    public static partial void InitialIntegrityCheck(ILogger logger, bool integrity, bool notNull, bool lengthsMatch);

    [LoggerMessage(
        EventId = 1018,
        Level = LogLevel.Information,
        Message = "Performing spot check on {SampleSize} random samples...")]
    public static partial void PerformingSpotCheck(ILogger logger, int sampleSize);

    [LoggerMessage(
        EventId = 1019,
        Level = LogLevel.Warning,
        Message = "Data mismatch at index {Index}: expected {Expected}, got {Actual}, diff: {Difference}")]
    public static partial void DataMismatch(ILogger logger, int index, float expected, float actual, float difference);

    [LoggerMessage(
        EventId = 1020,
        Level = LogLevel.Error,
        Message = "Index {Index} out of bounds! readData.Length: {ReadDataLength}, largeTestData.Length: {TestDataLength}")]
    public static partial void IndexOutOfBounds(ILogger logger, int index, int readDataLength, int testDataLength);

    [LoggerMessage(
        EventId = 1021,
        Level = LogLevel.Information,
        Message = "Mismatch rate: {MismatchRate:P2}({MismatchCount}/{SampleSize})")]
    public static partial void MismatchRate(ILogger logger, double mismatchRate, int mismatchCount, int sampleSize);

    [LoggerMessage(
        EventId = 1022,
        Level = LogLevel.Warning,
        Message = "Too many mismatches: {MismatchRate:P2}")]
    public static partial void TooManyMismatches(ILogger logger, double mismatchRate);

    [LoggerMessage(
        EventId = 1023,
        Level = LogLevel.Information,
        Message = "Final integrity result: {Integrity}")]
    public static partial void FinalIntegrityResult(ILogger logger, bool integrity);

    [LoggerMessage(
        EventId = 1024,
        Level = LogLevel.Error,
        Message = "Host to device transfer failed")]
    public static partial void HostToDeviceTransferFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1025,
        Level = LogLevel.Error,
        Message = "Device to host transfer failed")]
    public static partial void DeviceToHostTransferFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1026,
        Level = LogLevel.Error,
        Message = "Unified memory test failed")]
    public static partial void UnifiedMemoryTestFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1027,
        Level = LogLevel.Error,
        Message = "Pinned memory transfer failed")]
    public static partial void PinnedMemoryTransferFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1028,
        Level = LogLevel.Error,
        Message = "Memory type {MemoryType} test failed")]
    public static partial void MemoryTypeTestFailed(ILogger logger, string memoryType, Exception exception);

    [LoggerMessage(
        EventId = 1029,
        Level = LogLevel.Error,
        Message = "Large data transfer failed")]
    public static partial void LargeDataTransferFailed(ILogger logger, Exception exception);

    // Performance benchmark messages
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Vector size: {VectorSize:N0}, Throughput: {ElementsPerSecond:N0} elements/sec")]
    public static partial void VectorPerformance(ILogger logger, int vectorSize, double elementsPerSecond);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Memory bandwidth: {BandwidthMBs:F2} MB/s")]
    public static partial void MemoryBandwidth(ILogger logger, double bandwidthMBs);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Matrix {MatrixSize}x{MatrixSize2}: {GigaFlops:F3} GFLOPS")]
    public static partial void MatrixPerformance(ILogger logger, int matrixSize, int matrixSize2, double gigaFlops);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Reduction {DataSize:N0} elements: {ElementsPerSecond:N0} elements/sec")]
    public static partial void ReductionPerformance(ILogger logger, int dataSize, double elementsPerSecond);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Memory-intensive workload: {BandwidthMBs:F2} MB/s")]
    public static partial void MemoryIntensiveWorkload(ILogger logger, double bandwidthMBs);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Compute-intensive workload: {GigaOps:F3} GOPS")]
    public static partial void ComputeIntensiveWorkload(ILogger logger, double gigaOps);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Parallel {ThreadCount} threads: {TotalThroughput:N0} total elements/sec, {AverageLatency:F2}ms avg latency")]
    public static partial void ParallelPerformance(ILogger logger, int threadCount, double totalThroughput, double averageLatency);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Information,
        Message = "Parallel efficiency: {Efficiency:P2}")]
    public static partial void ParallelEfficiency(ILogger logger, double efficiency);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Image processing: {MegapixelsPerSecond:F2} MP/s")]
    public static partial void ImageProcessingPerformance(ILogger logger, double megapixelsPerSecond);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Information,
        Message = "Signal processing: {SamplesPerSecond:N0} samples/sec, {RealTimeRatio:F2}x real-time")]
    public static partial void SignalProcessingPerformance(ILogger logger, double samplesPerSecond, double realTimeRatio);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Information,
        Message = "Optimization {OptimizationLevel}: {ElementsPerSecond:N0} elements/sec")]
    public static partial void OptimizationLevelPerformance(ILogger logger, string optimizationLevel, double elementsPerSecond);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Error,
        Message = "Vector operation benchmark failed")]
    public static partial void VectorOperationBenchmarkFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Error,
        Message = "Matrix multiplication benchmark failed")]
    public static partial void MatrixMultiplicationBenchmarkFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2014,
        Level = LogLevel.Error,
        Message = "Reduction operation benchmark failed")]
    public static partial void ReductionOperationBenchmarkFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2015,
        Level = LogLevel.Error,
        Message = "Memory-intensive workload benchmark failed")]
    public static partial void MemoryIntensiveWorkloadBenchmarkFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2016,
        Level = LogLevel.Error,
        Message = "Compute-intensive workload benchmark failed")]
    public static partial void ComputeIntensiveWorkloadBenchmarkFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2017,
        Level = LogLevel.Error,
        Message = "Image processing benchmark failed")]
    public static partial void ImageProcessingBenchmarkFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2018,
        Level = LogLevel.Error,
        Message = "Signal processing benchmark failed")]
    public static partial void SignalProcessingBenchmarkFailed(ILogger logger, Exception exception);

    // Real-world scenario messages
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Neural network training completed in {TrainingTimeSeconds:F2}s")]
    public static partial void NeuralNetworkTrainingCompleted(ILogger logger, double trainingTimeSeconds);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Loss reduction: {InitialLoss:F4} -> {FinalLoss:F4}")]
    public static partial void LossReduction(ILogger logger, float initialLoss, float finalLoss);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Image processing: {MegapixelsPerSecond:F2} MP/s")]
    public static partial void ImageProcessing(ILogger logger, double megapixelsPerSecond);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "CFD simulation: {CellUpdatesPerSecond:N0} cell updates/sec")]
    public static partial void CFDSimulation(ILogger logger, double cellUpdatesPerSecond);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Information,
        Message = "Monte Carlo: {SimulationsPerSecond:N0} simulations/sec, Option price: ${OptionPrice:F4}")]
    public static partial void MonteCarlo(ILogger logger, double simulationsPerSecond, float optionPrice);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "Hash computation: {HashesPerSecond:N0} hashes/sec")]
    public static partial void HashComputation(ILogger logger, double hashesPerSecond);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "Physics simulation: {ParticleUpdatesPerSecond:N0} particle updates/sec")]
    public static partial void PhysicsSimulation(ILogger logger, double particleUpdatesPerSecond);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Information,
        Message = "Audio processing: {RealTimeRatio:F2}x real-time")]
    public static partial void AudioProcessing(ILogger logger, double realTimeRatio);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Information,
        Message = "Data analytics: {RecordsPerSecond:N0} records/sec")]
    public static partial void DataAnalytics(ILogger logger, double recordsPerSecond);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Error,
        Message = "Neural network training failed")]
    public static partial void NeuralNetworkTrainingFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Error,
        Message = "Image processing pipeline failed")]
    public static partial void ImageProcessingPipelineFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Error,
        Message = "CFD simulation failed")]
    public static partial void CFDSimulationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Error,
        Message = "Monte Carlo simulation failed")]
    public static partial void MonteCarloSimulationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3014,
        Level = LogLevel.Error,
        Message = "Audio processing failed")]
    public static partial void AudioProcessingFailed(ILogger logger, Exception exception);

    // IntegrationTestBase messages
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "CreateInputBuffer: Allocating {SizeInBytes} bytes for {ElementCount} elements of type {TypeName}")]
    public static partial void CreateInputBufferAllocating(ILogger logger, long sizeInBytes, int elementCount, string typeName);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Buffer allocated successfully")]
    public static partial void BufferAllocatedSuccessfully(ILogger logger);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "Data converted to {ByteCount} bytes")]
    public static partial void DataConvertedToBytes(ILogger logger, int byteCount);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Information,
        Message = "Data copied to buffer successfully")]
    public static partial void DataCopiedToBufferSuccessfully(ILogger logger);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Error,
        Message = "Failed to create input buffer for {ElementCount} elements of type {TypeName}")]
    public static partial void FailedToCreateInputBuffer(ILogger logger, Exception exception, int elementCount, string typeName);

    // PluginIntegrationSystemTests messages
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Loaded plugin: {PluginName} v{PluginVersion}")]
    public static partial void LoadedPlugin(ILogger logger, string pluginName, string pluginVersion);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "Plugin {PluginName} provides {AcceleratorCount} accelerators")]
    public static partial void PluginProvidesAccelerators(ILogger logger, string pluginName, int acceleratorCount);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Information,
        Message = "Performance range: {MinAvg:F2}ms - {MaxAvg:F2}ms")]
    public static partial void PerformanceRange(ILogger logger, double minAvg, double maxAvg);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Error,
        Message = "Backend factory test failed for plugin {PluginName}")]
    public static partial void BackendFactoryTestFailed(ILogger logger, Exception exception, string pluginName);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Error,
        Message = "Kernel execution failed for backend {Backend}")]
    public static partial void KernelExecutionFailed(ILogger logger, Exception exception, string backend);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Error,
        Message = "Memory management test failed for accelerator {AcceleratorId}")]
    public static partial void MemoryManagementTestFailed(ILogger logger, Exception exception, string acceleratorId);

    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Error,
        Message = "Client {ClientId} operation {OperationId} failed")]
    public static partial void ClientOperationFailed(ILogger logger, Exception exception, int clientId, int operationId);

    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Error,
        Message = "Resource cleanup test failed for {AcceleratorId}")]
    public static partial void ResourceCleanupTestFailed(ILogger logger, Exception exception, string acceleratorId);

    // KernelCompilationPipelineTests messages
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Skipping backend test - {BackendType} not available")]
    public static partial void SkippingBackendTest(ILogger logger, string backendType);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "First compilation: {CompilationTime:F2}ms")]
    public static partial void FirstCompilationTime(ILogger logger, double compilationTime);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Second compilation: {CompilationTime:F2}ms")]
    public static partial void SecondCompilationTime(ILogger logger, double compilationTime);

    // MultiAcceleratorTests messages
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Skipping heterogeneous test - missing {Backend1} or {Backend2}")]
    public static partial void SkippingHeterogeneousTest(ILogger logger, string backend1, string backend2);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Error,
        Message = "Failed to execute work on accelerator {AcceleratorId}")]
    public static partial void FailedToExecuteWork(ILogger logger, Exception exception, string acceleratorId);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Memory coherence test failed for {AcceleratorId}")]
    public static partial void MemoryCoherenceTestFailed(ILogger logger, Exception exception, string acceleratorId);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Error,
        Message = "Concurrent execution failed for {AcceleratorId}")]
    public static partial void ConcurrentExecutionFailed(ILogger logger, Exception exception, string acceleratorId);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Error,
        Message = "Work execution failed for {AcceleratorId}")]
    public static partial void WorkExecutionFailed(ILogger logger, Exception exception, string acceleratorId);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Error,
        Message = "Heterogeneous execution failed for {AcceleratorId} on {Backend}")]
    public static partial void HeterogeneousExecutionFailed(ILogger logger, Exception exception, string acceleratorId, string backend);

    // ComputeWorkflowTestBase messages
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "ComputeWorkflowTestBase initialized with hardware simulation")]
    public static partial void ComputeWorkflowTestBaseInitialized(ILogger logger);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Information,
        Message = "Starting compute workflow '{WorkflowName}' (ID: {ExecutionId})")]
    public static partial void StartingComputeWorkflow(ILogger logger, string workflowName, string executionId);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Information,
        Message = "Workflow execution completed successfully")]
    public static partial void WorkflowExecutionCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Error,
        Message = "Workflow execution failed")]
    public static partial void WorkflowExecutionFailed(ILogger logger, Exception exception);

    // General test messages
    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Information,
        Message = "Starting backend integration test for {BackendType}")]
    public static partial void StartingBackendIntegrationTest(ILogger logger, string backendType);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Information,
        Message = "Testing accelerator {AcceleratorId}: {DeviceType}")]
    public static partial void TestingAccelerator(ILogger logger, string acceleratorId, string deviceType);

    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Information,
        Message = "Error recovery test completed")]
    public static partial void ErrorRecoveryTestCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 9004,
        Level = LogLevel.Information,
        Message = "Initializing backend provider {BackendType}")]
    public static partial void InitializingBackendProvider(ILogger logger, string backendType);

    [LoggerMessage(
        EventId = 9005,
        Level = LogLevel.Information,
        Message = "Testing backend {BackendType} initialization")]
    public static partial void TestingBackendInitialization(ILogger logger, string backendType);

    [LoggerMessage(
        EventId = 9006,
        Level = LogLevel.Information,
        Message = "Testing concurrent kernel execution")]
    public static partial void TestingConcurrentKernelExecution(ILogger logger);

    [LoggerMessage(
        EventId = 9007,
        Level = LogLevel.Information,
        Message = "Testing kernel error recovery")]
    public static partial void TestingKernelErrorRecovery(ILogger logger);

    // Additional logging messages for direct logging call replacements
    [LoggerMessage(
        EventId = 11001,
        Level = LogLevel.Information,
        Message = "Vector operations throughput ({Size} elements): {Throughput:F2} MB/s")]
    public static partial void VectorOperationsThroughput(ILogger logger, int size, double throughput);

    [LoggerMessage(
        EventId = 11002,
        Level = LogLevel.Information,
        Message = "Latency benchmark ({Size} elements): {Latency:F2}ms")]
    public static partial void LatencyBenchmark(ILogger logger, int size, double latency);

    [LoggerMessage(
        EventId = 11003,
        Level = LogLevel.Information,
        Message = "Memory bandwidth ({Size} elements): {Bandwidth:F2} MB/s, Transfer time: {TransferTime:F2}ms")]
    public static partial void MemoryBandwidthResult(ILogger logger, int size, double bandwidth, double transferTime);

    [LoggerMessage(
        EventId = 11004,
        Level = LogLevel.Information,
        Message = "Compute intensity (level {Level}): {Time:F2}ms, Operations/sec: {OperationsPerSec:N0}")]
    public static partial void ComputeIntensityResult(ILogger logger, int level, double time, double operationsPerSec);

    [LoggerMessage(
        EventId = 11005,
        Level = LogLevel.Information,
        Message = "Concurrent benchmark ({Level}x): {Success}/{Total} successful, Avg latency: {AvgLatency:F2}ms")]
    public static partial void ConcurrentBenchmarkResult(ILogger logger, int level, int success, int total, double avgLatency);

    [LoggerMessage(
        EventId = 11006,
        Level = LogLevel.Information,
        Message = "{Algorithm} benchmark ({Size}): Avg throughput: {AvgThroughput:F2} MB/s, Best: {BestThroughput:F2} MB/s")]
    public static partial void AlgorithmBenchmarkResult(ILogger logger, string algorithm, int size, double avgThroughput, double bestThroughput);

    [LoggerMessage(
        EventId = 11007,
        Level = LogLevel.Information,
        Message = "End-to-end pipeline: {Stages} stages, {Duration:F2}ms total, Throughput: {Throughput:F2} MB/s")]
    public static partial void EndToEndPipelineResult(ILogger logger, int stages, double duration, double throughput);

    [LoggerMessage(
        EventId = 11008,
        Level = LogLevel.Information,
        Message = "Regression test {Test}: {Current:F2} MB/s vs {Baseline:F2} MB/s ({Difference:F1}% change)")]
    public static partial void RegressionTestResult(ILogger logger, string test, double current, double baseline, double difference);

    [LoggerMessage(
        EventId = 11009,
        Level = LogLevel.Warning,
        Message = "Performance regressions detected: {Regressions}")]
    public static partial void PerformanceRegressions(ILogger logger, int regressions);

    [LoggerMessage(
        EventId = 11010,
        Level = LogLevel.Information,
        Message = "Performance improvements detected: {Improvements}")]
    public static partial void PerformanceImprovements(ILogger logger, int improvements);

    [LoggerMessage(
        EventId = 11011,
        Level = LogLevel.Information,
        Message = "Hardware failure handled gracefully: {Error}")]
    public static partial void HardwareFailureHandled(ILogger logger, string error);

    [LoggerMessage(
        EventId = 11012,
        Level = LogLevel.Information,
        Message = "System recovered from hardware failure and completed successfully")]
    public static partial void SystemRecoveredFromFailure(ILogger logger);

    [LoggerMessage(
        EventId = 11013,
        Level = LogLevel.Information,
        Message = "Memory exhaustion scenario handled: Success={Success}, Error={Error}")]
    public static partial void MemoryExhaustionHandled(ILogger logger, bool success, string? error);

    [LoggerMessage(
        EventId = 11014,
        Level = LogLevel.Information,
        Message = "Operation cancelled gracefully after {Duration}ms")]
    public static partial void OperationCancelledGracefully(ILogger logger, long duration);

    [LoggerMessage(
        EventId = 11015,
        Level = LogLevel.Information,
        Message = "Operation failed gracefully: {Error}")]
    public static partial void OperationFailedGracefully(ILogger logger, string? error);

    // Additional error recovery and test result messages
    [LoggerMessage(
        EventId = 12001,
        Level = LogLevel.Information,
        Message = "Resource exhaustion test: {Success} successes, {Failures} failures out of {Total}")]
    public static partial void ResourceExhaustionTestResult(ILogger logger, int success, int failures, int total);

    [LoggerMessage(
        EventId = 12002,
        Level = LogLevel.Information,
        Message = "Cascading failure test: {Successful} successful, {Failed} failed stages")]
    public static partial void CascadingFailureTestResult(ILogger logger, int successful, int failed);

    [LoggerMessage(
        EventId = 12003,
        Level = LogLevel.Information,
        Message = "Successfully recovered from {ErrorType} in {Duration}ms")]
    public static partial void SuccessfullyRecoveredFromError(ILogger logger, string errorType, long duration);

    [LoggerMessage(
        EventId = 12004,
        Level = LogLevel.Information,
        Message = "Error {ErrorType} handled gracefully: {Error}")]
    public static partial void ErrorHandledGracefully(ILogger logger, string errorType, string? error);

    // Concurrent multi-accelerator test messages
    [LoggerMessage(
        EventId = 13001,
        Level = LogLevel.Information,
        Message = "Parallel multi-accelerator execution: {Count} workflows, Total time: {TotalTime:F2}ms, Avg throughput: {AvgThroughput:F2} MB/s")]
    public static partial void ParallelMultiAcceleratorExecution(ILogger logger, int count, double totalTime, double avgThroughput);

    [LoggerMessage(
        EventId = 13002,
        Level = LogLevel.Information,
        Message = "Backend usage: {Usage}")]
    public static partial void BackendUsage(ILogger logger, string usage);

    [LoggerMessage(
        EventId = 13003,
        Level = LogLevel.Information,
        Message = "Load balancing results: Min={Min:F1}ms, Max={Max:F1}ms, Variance={Variance:F1}ms")]
    public static partial void LoadBalancingResults(ILogger logger, double min, double max, double variance);

    [LoggerMessage(
        EventId = 13004,
        Level = LogLevel.Information,
        Message = "Synchronized execution completed successfully across {Count} accelerators")]
    public static partial void SynchronizedExecutionCompleted(ILogger logger, int count);

    [LoggerMessage(
        EventId = 13005,
        Level = LogLevel.Information,
        Message = "Resource coordination: Total memory used: {Total}MB, Peak usage: {Peak:F1}%")]
    public static partial void ResourceCoordination(ILogger logger, long total, double peak);

    [LoggerMessage(
        EventId = 13006,
        Level = LogLevel.Information,
        Message = "Fault tolerance test: {Successful} successful, {Failed} failed workflows")]
    public static partial void FaultToleranceTest(ILogger logger, int successful, int failed);

    [LoggerMessage(
        EventId = 13007,
        Level = LogLevel.Information,
        Message = "Starting load phase: {PhaseName}")]
    public static partial void StartingLoadPhase(ILogger logger, string phaseName);

    [LoggerMessage(
        EventId = 13008,
        Level = LogLevel.Information,
        Message = "Phase {Phase} completed: {Successful}/{Total} successful, Avg latency: {AvgLatency:F2}ms")]
    public static partial void PhaseCompleted(ILogger logger, string phase, int successful, int total, double avgLatency);

    [LoggerMessage(
        EventId = 13009,
        Level = LogLevel.Information,
        Message = "Pipeline parallelism: {Stages} stages, {Efficiency:P1} efficiency, Throughput: {Throughput:F2} MB/s")]
    public static partial void PipelineParallelism(ILogger logger, int stages, double efficiency, double throughput);

    [LoggerMessage(
        EventId = 13010,
        Level = LogLevel.Information,
        Message = "Concurrency scaling test: {AcceleratorCount} accelerators, Efficiency: {Efficiency:P1}, Total throughput: {TotalThroughput:F2} MB/s")]
    public static partial void ConcurrencyScalingTest(ILogger logger, int acceleratorCount, double efficiency, double totalThroughput);

    // Generic logging messages for common patterns
    [LoggerMessage(
        EventId = 14001,
        Level = LogLevel.Information,
        Message = "{Message}")]
    public static partial void LogInformation(ILogger logger, string message);

    [LoggerMessage(
        EventId = 14002,
        Level = LogLevel.Information,
        Message = "Test {TestName}: {Status} - {Details}")]
    public static partial void LogTestResult(ILogger logger, string testName, string status, string details);

    [LoggerMessage(
        EventId = 14003,
        Level = LogLevel.Information,
        Message = "Performance result: {Metric} = {Value:F2} {Unit}")]
    public static partial void LogPerformanceResult(ILogger logger, string metric, double value, string unit);

    [LoggerMessage(
        EventId = 14004,
        Level = LogLevel.Information,
        Message = "Execution completed: {Duration}ms, {ElementsProcessed} elements")]
    public static partial void LogExecutionCompleted(ILogger logger, double duration, int elementsProcessed);

    [LoggerMessage(
        EventId = 14005,
        Level = LogLevel.Information,
        Message = "Backend {BackendName}: {Operation} - {Result}")]
    public static partial void LogBackendOperation(ILogger logger, string backendName, string operation, string result);

    [LoggerMessage(
        EventId = 14006,
        Level = LogLevel.Information,
        Message = "Accelerator {AcceleratorId}: {Status}")]
    public static partial void LogAcceleratorStatus(ILogger logger, string acceleratorId, string status);

    [LoggerMessage(
        EventId = 14007,
        Level = LogLevel.Debug,
        Message = "Workflow {WorkflowName}: {Step} - {Details}")]
    public static partial void LogWorkflowStep(ILogger logger, string workflowName, string step, string details);

    [LoggerMessage(
        EventId = 14008,
        Level = LogLevel.Warning,
        Message = "Workflow execution warning: {Message}")]
    public static partial void LogWorkflowWarning(ILogger logger, Exception exception, string message);

    [LoggerMessage(
        EventId = 14009,
        Level = LogLevel.Error,
        Message = "Operation failed: {ErrorMessage}")]
    public static partial void LogOperationFailed(ILogger logger, Exception exception, string errorMessage);

    // Real-world scenario test messages
    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Information,
        Message = "Neural network training epoch {Epoch}/{TotalEpochs}: Loss={Loss:F6}")]
    public static partial void NeuralNetworkTrainingEpoch(ILogger logger, int epoch, int totalEpochs, float loss);

    [LoggerMessage(
        EventId = 10002,
        Level = LogLevel.Information,
        Message = "Image processing: {ImageIndex}/{TotalImages} - {ProcessingTime:F2}ms")]
    public static partial void ImageProcessingProgress(ILogger logger, int imageIndex, int totalImages, double processingTime);

    [LoggerMessage(
        EventId = 10003,
        Level = LogLevel.Information,
        Message = "CFD simulation step {Step}/{TotalSteps}: {CellUpdatesPerSecond:N0} cell updates/sec")]
    public static partial void CFDSimulationStep(ILogger logger, int step, int totalSteps, double cellUpdatesPerSecond);

    [LoggerMessage(
        EventId = 10004,
        Level = LogLevel.Information,
        Message = "Monte Carlo simulation batch {Batch}/{TotalBatches}: {SimulationsPerSecond:N0} simulations/sec")]
    public static partial void MonteCarloSimulationBatch(ILogger logger, int batch, int totalBatches, double simulationsPerSecond);

    [LoggerMessage(
        EventId = 10005,
        Level = LogLevel.Information,
        Message = "Physics particle simulation: {ParticleCount:N0} particles, {UpdatesPerSecond:N0} updates/sec")]
    public static partial void PhysicsParticleSimulation(ILogger logger, long particleCount, double updatesPerSecond);
}
}
