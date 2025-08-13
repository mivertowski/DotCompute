// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Integration;

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
        Message = "Attempting to create input buffer for {ElementCount} elements ({SizeMB}MB)")]
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
        Message = "Initial integrity check: {Integrity} (readData != null: {NotNull}, lengths match: {LengthsMatch})")]
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
        Message = "Mismatch rate: {MismatchRate:P2} ({MismatchCount}/{SampleSize})")]
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
}