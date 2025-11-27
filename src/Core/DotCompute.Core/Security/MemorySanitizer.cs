// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DotCompute.Core.Security.Configuration;
using DotCompute.Core.Security.Enums;
using DotCompute.Core.Security.Models;
using DotCompute.Core.Security.Models.Internal;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Comprehensive memory sanitization system that provides secure memory wiping,
/// use-after-free detection, double-free prevention, and memory leak detection
/// with advanced security features for protecting sensitive data in memory.
/// Implements production-grade memory safety with hardware-accelerated validation.
/// </summary>
public sealed partial class MemorySanitizer : IDisposable
{
    private readonly ILogger _logger;
    private readonly MemorySanitizerConfiguration _configuration;
    private readonly ConcurrentDictionary<IntPtr, SanitizedAllocation> _trackedAllocations = new();
    private readonly ConcurrentDictionary<IntPtr, FreeRecord> _freeHistory = new();
    private readonly SemaphoreSlim _operationLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly Timer _leakDetectionTimer;
    private readonly Timer _integrityCheckTimer;
    private readonly RandomNumberGenerator _randomGenerator;
    private volatile bool _disposed;

    // Memory pattern constants for detection
    private const byte ALLOCATED_PATTERN = 0xAA;
    private const byte FREED_PATTERN = 0xDD;
    private const byte GUARD_PATTERN = 0xCC;

    // Statistics
    private readonly SanitizerStatistics _statistics = new();

    // LoggerMessage delegates - Event ID range 18600-18615 for MemorySanitizer
    private static readonly Action<ILogger, string, Exception?> _logSanitizerInitialized =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(18600, nameof(LogSanitizerInitialized)),
            "MemorySanitizer initialized with configuration: {Configuration}");

    private static void LogSanitizerInitialized(ILogger logger, string configuration)
        => _logSanitizerInitialized(logger, configuration, null);

    private static readonly Action<ILogger, nuint, string, string, Exception?> _logAllocatingSanitizedMemory =
        LoggerMessage.Define<nuint, string, string>(
            MsLogLevel.Debug,
            new EventId(18601, nameof(LogAllocatingSanitizedMemory)),
            "Allocating sanitized memory: Size={Size}, Classification={Classification}, Id={Identifier}");

    private static void LogAllocatingSanitizedMemory(ILogger logger, nuint size, string classification, string identifier)
        => _logAllocatingSanitizedMemory(logger, size, classification, identifier, null);

    private static readonly Action<ILogger, long, nuint, string, Exception?> _logMemoryAllocated =
        LoggerMessage.Define<long, nuint, string>(
            MsLogLevel.Debug,
            new EventId(18602, nameof(LogMemoryAllocated)),
            "Sanitized memory allocated: Address={Address}, Size={Size}, Id={Identifier}");

    private static void LogMemoryAllocated(ILogger logger, long address, nuint size, string identifier)
        => _logMemoryAllocated(logger, address, size, identifier, null);

    private static readonly Action<ILogger, Exception, long, Exception?> _logReadError =
        LoggerMessage.Define<Exception, long>(
            MsLogLevel.Error,
            new EventId(18603, nameof(LogReadError)),
            "Error {Exception} during sanitized memory read: Address={Address}");

    private static void LogReadError(ILogger logger, Exception error, long address)
        => _logReadError(logger, error, address, null);

    private static readonly Action<ILogger, Exception, long, Exception?> _logWriteError =
        LoggerMessage.Define<Exception, long>(
            MsLogLevel.Error,
            new EventId(18604, nameof(LogWriteError)),
            "Error {Exception} during sanitized memory write: Address={Address}");

    private static void LogWriteError(ILogger logger, Exception error, long address)
        => _logWriteError(logger, error, address, null);

    private static readonly Action<ILogger, long, Exception?> _logDeallocatingMemory =
        LoggerMessage.Define<long>(
            MsLogLevel.Debug,
            new EventId(18605, nameof(LogDeallocatingMemory)),
            "Deallocating sanitized memory: Address={Address}");

    private static void LogDeallocatingMemory(ILogger logger, long address)
        => _logDeallocatingMemory(logger, address, null);

    private static readonly Action<ILogger, long, nuint, string, Exception?> _logMemoryDeallocated =
        LoggerMessage.Define<long, nuint, string>(
            MsLogLevel.Debug,
            new EventId(18606, nameof(LogMemoryDeallocated)),
            "Sanitized memory deallocated: Address={Address}, Size={Size}, Id={Identifier}");

    private static void LogMemoryDeallocated(ILogger logger, long address, nuint size, string identifier)
        => _logMemoryDeallocated(logger, address, size, identifier, null);

    private static readonly Action<ILogger, Exception?> _logPerformingLeakDetection =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(18607, nameof(LogPerformingLeakDetection)),
            "Performing memory leak detection");

    private static void LogPerformingLeakDetection(ILogger logger)
        => _logPerformingLeakDetection(logger, null);

    private static readonly Action<ILogger, int, long, Exception?> _logLeaksDetected =
        LoggerMessage.Define<int, long>(
            MsLogLevel.Warning,
            new EventId(18608, nameof(LogLeaksDetected)),
            "Memory leak detection found {Count} high-suspicion allocations totaling {TotalBytes} bytes");

    private static void LogLeaksDetected(ILogger logger, int count, long totalBytes)
        => _logLeaksDetected(logger, count, totalBytes, null);

    private static readonly Action<ILogger, Exception, Exception?> _logFreeMemoryError =
        LoggerMessage.Define<Exception>(
            MsLogLevel.Error,
            new EventId(18609, nameof(LogFreeMemoryError)),
            "Error freeing raw memory: {Exception}");

    private static void LogFreeMemoryError(ILogger logger, Exception error)
        => _logFreeMemoryError(logger, error, null);

    private static readonly Action<ILogger, int, Exception?> _logLeakDetectionWarning =
        LoggerMessage.Define<int>(
            MsLogLevel.Warning,
            new EventId(18610, nameof(LogLeakDetectionWarning)),
            "Memory leak detection: {Count} high-suspicion leaks detected");

    private static void LogLeakDetectionWarning(ILogger logger, int count)
        => _logLeakDetectionWarning(logger, count, null);

    private static readonly Action<ILogger, Exception, Exception?> _logLeakDetectionError =
        LoggerMessage.Define<Exception>(
            MsLogLevel.Error,
            new EventId(18611, nameof(LogLeakDetectionError)),
            "Error during memory leak detection: {Exception}");

    private static void LogLeakDetectionError(ILogger logger, Exception error)
        => _logLeakDetectionError(logger, error, null);

    private static readonly Action<ILogger, int, Exception?> _logIntegrityCheckWarning =
        LoggerMessage.Define<int>(
            MsLogLevel.Warning,
            new EventId(18612, nameof(LogIntegrityCheckWarning)),
            "Integrity check found {Count} corrupted allocations");

    private static void LogIntegrityCheckWarning(ILogger logger, int count)
        => _logIntegrityCheckWarning(logger, count, null);

    private static readonly Action<ILogger, Exception, Exception?> _logIntegrityCheckError =
        LoggerMessage.Define<Exception>(
            MsLogLevel.Error,
            new EventId(18613, nameof(LogIntegrityCheckError)),
            "Error during integrity check: {Exception}");

    private static void LogIntegrityCheckError(ILogger logger, Exception error)
        => _logIntegrityCheckError(logger, error, null);

    private static readonly Action<ILogger, long, long, Exception?> _logSanitizerDisposed =
        LoggerMessage.Define<long, long>(
            MsLogLevel.Information,
            new EventId(18614, nameof(LogSanitizerDisposed)),
            "MemorySanitizer disposed. Final statistics: Allocations={Allocations}, Violations={Violations}");

    private static void LogSanitizerDisposed(ILogger logger, long allocations, long violations)
        => _logSanitizerDisposed(logger, allocations, violations, null);

    private static readonly Action<ILogger, long, nuint, nuint, Exception?> _logReadSuccess =
        LoggerMessage.Define<long, nuint, nuint>(
            MsLogLevel.Trace,
            new EventId(18615, nameof(LogReadSuccess)),
            "Sanitized read successful: Address={Address:X}, Offset={Offset}, Size={Size}");

    private static void LogReadSuccess(ILogger logger, long address, nuint offset, nuint size)
        => _logReadSuccess(logger, address, offset, size, null);

    private static readonly Action<ILogger, long, nuint, nuint, Exception?> _logWriteSuccess =
        LoggerMessage.Define<long, nuint, nuint>(
            MsLogLevel.Trace,
            new EventId(18616, nameof(LogWriteSuccess)),
            "Sanitized write successful: Address={Address:X}, Offset={Offset}, Size={Size}");

    private static void LogWriteSuccess(ILogger logger, long address, nuint offset, nuint size)
        => _logWriteSuccess(logger, address, offset, size, null);

    private static readonly Action<ILogger, SanitizationViolationType, long, string, Exception?> _logSanitizationViolation =
        LoggerMessage.Define<SanitizationViolationType, long, string>(
            MsLogLevel.Error,
            new EventId(18617, nameof(LogMemorySanitizationViolation)),
            "Memory sanitization violation: {ViolationType} at {Address:X} - {Description}");

    private static void LogMemorySanitizationViolation(ILogger logger, SanitizationViolationType violationType, long address, string description)
        => _logSanitizationViolation(logger, violationType, address, description, null);

    private static readonly Action<ILogger, string, Exception?> _logAllocationDisposeError =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18618, nameof(LogAllocationDisposeError)),
            "Error disposing allocation: {Identifier}");

    private static void LogAllocationDisposeError(ILogger logger, Exception ex, string identifier)
        => _logAllocationDisposeError(logger, identifier, ex);

    /// <summary>
    /// Initializes a new instance of the MemorySanitizer class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public MemorySanitizer(ILogger<MemorySanitizer> logger, MemorySanitizerConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _configuration = configuration ?? MemorySanitizerConfiguration.Default;
        _randomGenerator = RandomNumberGenerator.Create();

        // Initialize monitoring timers

        _leakDetectionTimer = new Timer(DetectMemoryLeaks, null,

            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        _integrityCheckTimer = new Timer(PerformIntegrityCheck, null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));


        LogSanitizerInitialized(_logger, _configuration.ToString());
    }

    /// <summary>
    /// Allocates sanitized memory with comprehensive tracking and protection.
    /// </summary>
    /// <param name="size">Size of memory to allocate</param>
    /// <param name="classification">Security classification of the data</param>
    /// <param name="identifier">Optional identifier for tracking</param>
    /// <param name="callerMethod">Automatically captured caller method name</param>
    /// <param name="callerFile">Automatically captured caller file path</param>
    /// <param name="callerLine">Automatically captured caller line number</param>
    /// <returns>Sanitized memory allocation result</returns>
    public async Task<SanitizedMemoryResult> AllocateSanitizedMemoryAsync(nuint size,

        DataClassification classification = DataClassification.Sensitive, string? identifier = null,
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",

        [CallerLineNumber] int callerLine = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (size == 0 || size > _configuration.MaxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(size),

                $"Size must be between 1 and {_configuration.MaxAllocationSize}");
        }


        await _operationLock.WaitAsync();
        try
        {
            LogAllocatingSanitizedMemory(_logger, size, classification.ToString(), identifier ?? "null");

            var result = new SanitizedMemoryResult
            {
                RequestedSize = size,
                Classification = classification,
                Identifier = identifier ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8],
                AllocationTime = DateTimeOffset.UtcNow
            };

            // Calculate total size with guards and metadata
            var guardSize = _configuration.EnableGuardBytes ? _configuration.GuardByteSize : 0;
            var canarySize = _configuration.EnableCanaryValues ? sizeof(ulong) * 2 : 0; // Before and after
            var totalSize = size + (nuint)(2 * guardSize) + (nuint)canarySize;

            // Allocate raw memory
            var basePtr = await AllocateRawMemoryAsync(totalSize);
            if (basePtr == IntPtr.Zero)
            {
                result.ErrorMessage = "Failed to allocate raw memory";
                return result;
            }

            // Calculate user data pointer
            var userPtr = basePtr + (int)guardSize;
            if (_configuration.EnableCanaryValues)
            {
                userPtr += sizeof(ulong); // Space for leading canary
            }

            // Generate canary value FIRST so we can use the same value for initialization and tracking
            var canaryValue = _configuration.EnableCanaryValues ? GenerateCanaryValue() : 0UL;

            // Initialize memory with security patterns using the pre-generated canary
            await InitializeSecureMemoryAsync(basePtr, userPtr, size, totalSize, (nuint)guardSize, (nuint)canarySize, canaryValue);

            // Create allocation tracking record with the SAME canary value
            var allocation = new SanitizedAllocation
            {
                BaseAddress = basePtr,
                UserAddress = userPtr,
                RequestedSize = size,
                TotalSize = totalSize,
                Classification = classification,
                Identifier = result.Identifier,
                AllocationTime = result.AllocationTime,
                CallSite = new AllocationCallSite
                {
                    Method = callerMethod,
                    File = Path.GetFileName(callerFile),
                    Line = callerLine
                },
                CanaryValue = canaryValue,
                AccessCount = 0,
                LastAccessTime = result.AllocationTime
            };

            // Register the allocation
            _ = _trackedAllocations.TryAdd(userPtr, allocation);

            result.Address = userPtr;
            result.ActualSize = size;
            result.IsSuccessful = true;

            // Update statistics

            _ = Interlocked.Increment(ref _statistics.TotalAllocations);
            _ = Interlocked.Add(ref _statistics.TotalBytesAllocated, (long)size);
            _ = _statistics.AllocationsByClassification.AddOrUpdate(classification, 1, (key, value) => value + 1);

            LogMemoryAllocated(_logger, userPtr.ToInt64(), size, result.Identifier);

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Performs secure memory read with sanitization checks.
    /// </summary>
    /// <typeparam name="T">Type of data to read</typeparam>
    /// <param name="address">Memory address to read from</param>
    /// <param name="offset">Offset from base address</param>
    /// <returns>Read value or throws security exception</returns>
    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "AccessViolationException is semantically correct for memory bounds violations in low-level sanitization")]
    public unsafe T ReadSanitized<T>(IntPtr address, nuint offset = 0) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (!_trackedAllocations.TryGetValue(address, out var allocation))
        {
            var violation = new MemorySanitizationViolation
            {
                ViolationType = SanitizationViolationType.UseAfterFree,
                Address = address,
                Operation = "Read",
                Description = "Attempt to read from untracked or freed memory"
            };
            LogSanitizationViolation(violation);
            throw new InvalidOperationException("Attempt to read from untracked memory");
        }

        var readSize = (nuint)sizeof(T);
        var readAddress = address + (int)offset;

        // Bounds checking
        if (offset > allocation.RequestedSize || offset + readSize > allocation.RequestedSize)
        {
            var violation = new MemorySanitizationViolation
            {
                ViolationType = SanitizationViolationType.BoundsViolation,
                Address = readAddress,
                Size = readSize,
                Operation = "Read",
                Description = $"Read beyond allocation bounds: offset {offset}, size {readSize}, allocated {allocation.RequestedSize}"
            };
            LogSanitizationViolation(violation);
            throw new AccessViolationException($"Memory bounds violation during read");
        }

        // Integrity checking
        if (_configuration.EnableCanaryValues && !VerifyCanaryValues(allocation))
        {
            var violation = new MemorySanitizationViolation
            {
                ViolationType = SanitizationViolationType.CorruptionDetected,
                Address = address,
                Operation = "Read",
                Description = "Memory corruption detected via canary value mismatch"
            };
            LogSanitizationViolation(violation);
            throw new InvalidOperationException("Memory corruption detected");
        }

        // Perform the read
        try
        {
            var value = Unsafe.Read<T>((void*)readAddress);

            // Update access tracking

            allocation.AccessCount++;
            allocation.LastAccessTime = DateTimeOffset.UtcNow;

            LogReadSuccess(_logger, address.ToInt64(), offset, readSize);

            return value;
        }
        catch (Exception ex)
        {
            LogReadError(_logger, ex, address.ToInt64());
            throw;
        }
    }

    /// <summary>
    /// Performs secure memory write with sanitization checks.
    /// </summary>
    /// <typeparam name="T">Type of data to write</typeparam>
    /// <param name="address">Memory address to write to</param>
    /// <param name="value">Value to write</param>
    /// <param name="offset">Offset from base address</param>
    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "AccessViolationException is semantically correct for memory bounds violations in low-level sanitization")]
    public unsafe void WriteSanitized<T>(IntPtr address, T value, nuint offset = 0) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (!_trackedAllocations.TryGetValue(address, out var allocation))
        {
            var violation = new MemorySanitizationViolation
            {
                ViolationType = SanitizationViolationType.UseAfterFree,
                Address = address,
                Operation = "Write",
                Description = "Attempt to write to untracked or freed memory"
            };
            LogSanitizationViolation(violation);
            throw new InvalidOperationException("Attempt to write to untracked memory");
        }

        var writeSize = (nuint)sizeof(T);
        var writeAddress = address + (int)offset;

        // Bounds checking
        if (offset > allocation.RequestedSize || offset + writeSize > allocation.RequestedSize)
        {
            var violation = new MemorySanitizationViolation
            {
                ViolationType = SanitizationViolationType.BoundsViolation,
                Address = writeAddress,
                Size = writeSize,
                Operation = "Write",
                Description = $"Write beyond allocation bounds: offset {offset}, size {writeSize}, allocated {allocation.RequestedSize}"
            };
            LogSanitizationViolation(violation);
            throw new AccessViolationException($"Memory bounds violation during write");
        }

        // Integrity checking before write
        if (_configuration.EnableCanaryValues && !VerifyCanaryValues(allocation))
        {
            var violation = new MemorySanitizationViolation
            {
                ViolationType = SanitizationViolationType.CorruptionDetected,
                Address = address,
                Operation = "Write",
                Description = "Memory corruption detected before write operation"
            };
            LogSanitizationViolation(violation);
            throw new InvalidOperationException("Memory corruption detected");
        }

        // Perform the write
        try
        {
            Unsafe.Write((void*)writeAddress, value);

            // Update access tracking

            allocation.AccessCount++;
            allocation.LastAccessTime = DateTimeOffset.UtcNow;

            LogWriteSuccess(_logger, address.ToInt64(), offset, writeSize);
        }
        catch (Exception ex)
        {
            LogWriteError(_logger, ex, address.ToInt64());
            throw;
        }
    }

    /// <summary>
    /// Securely deallocates sanitized memory with comprehensive cleanup.
    /// </summary>
    /// <param name="address">Address to deallocate</param>
    /// <returns>Deallocation result with security information</returns>
    public async Task<MemoryDeallocationResult> DeallocateSanitizedMemoryAsync(IntPtr address)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        await _operationLock.WaitAsync();
        try
        {
            LogDeallocatingMemory(_logger, address.ToInt64());

            var result = new MemoryDeallocationResult
            {
                Address = address,
                DeallocationTime = DateTimeOffset.UtcNow
            };

            // Check if this is a tracked allocation
            if (!_trackedAllocations.TryGetValue(address, out var allocation))
            {
                var violation = new MemorySanitizationViolation
                {
                    ViolationType = SanitizationViolationType.DoubleFree,
                    Address = address,
                    Operation = "Free",
                    Description = "Attempt to free untracked memory or double-free detected"
                };
                LogSanitizationViolation(violation);
                result.ErrorMessage = "Double-free or invalid free detected";
                return result;
            }

            // Check for double-free
            if (_freeHistory.ContainsKey(address))
            {
                var violation = new MemorySanitizationViolation
                {
                    ViolationType = SanitizationViolationType.DoubleFree,
                    Address = address,
                    Operation = "Free",
                    Description = "Double-free detected"
                };
                LogSanitizationViolation(violation);
                result.ErrorMessage = "Double-free detected";
                return result;
            }

            // Final integrity check
            if (_configuration.EnableCanaryValues && !VerifyCanaryValues(allocation))
            {
                var violation = new MemorySanitizationViolation
                {
                    ViolationType = SanitizationViolationType.CorruptionDetected,
                    Address = address,
                    Operation = "Free",
                    Description = "Memory corruption detected during deallocation"
                };
                LogSanitizationViolation(violation);
                result.CorruptionDetected = true;
            }

            // Perform secure memory wiping based on classification
            await PerformSecureWipeAsync(allocation);

            // Mark memory as freed with poison pattern
            unsafe
            {
                var ptr = (byte*)allocation.BaseAddress;
                for (nuint i = 0; i < allocation.TotalSize; i++)
                {
                    ptr[i] = FREED_PATTERN;
                }
            }

            // Remove from tracking
            _ = _trackedAllocations.TryRemove(address, out _);

            // Add to free history for double-free detection
            _ = _freeHistory.TryAdd(address, new FreeRecord
            {
                Address = address,
                Size = allocation.RequestedSize,
                FreeTime = result.DeallocationTime,
                CallSite = new AllocationCallSite
                {
                    Method = "DeallocateSanitizedMemoryAsync",
                    File = "MemorySanitizer.cs",
                    Line = 0 // Would be filled by caller attributes
                }
            });

            // Free the actual memory
            FreeRawMemory(allocation.BaseAddress, allocation.TotalSize);

            result.IsSuccessful = true;
            result.BytesFreed = allocation.RequestedSize;
            result.SecurityLevel = allocation.Classification;

            // Update statistics
            _ = Interlocked.Increment(ref _statistics.TotalDeallocations);
            _ = Interlocked.Add(ref _statistics.TotalBytesFreed, (long)allocation.RequestedSize);

            LogMemoryDeallocated(_logger, address.ToInt64(), allocation.RequestedSize, allocation.Identifier);

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Performs comprehensive memory leak detection.
    /// </summary>
    /// <returns>Memory leak detection report</returns>
    public async Task<MemoryLeakReport> DetectMemoryLeaksAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        await _operationLock.WaitAsync();
        try
        {
            LogPerformingLeakDetection(_logger);

            var report = new MemoryLeakReport
            {
                ScanTime = DateTimeOffset.UtcNow,
                TotalActiveAllocations = _trackedAllocations.Count
            };

            var suspiciousAllocations = new List<LeakSuspect>();
            var now = DateTimeOffset.UtcNow;

            foreach (var allocation in _trackedAllocations.Values)
            {
                var age = now - allocation.AllocationTime;
                var timeSinceAccess = now - allocation.LastAccessTime;

                // Check for potential leaks based on age and access patterns
                if (age > _configuration.LeakDetectionThreshold)
                {
                    var suspect = new LeakSuspect
                    {
                        Address = allocation.UserAddress,
                        Size = allocation.RequestedSize,
                        Age = age,
                        TimeSinceLastAccess = timeSinceAccess,
                        AccessCount = allocation.AccessCount,
                        Identifier = allocation.Identifier,
                        CallSite = allocation.CallSite,
                        Classification = allocation.Classification,
                        SuspicionLevel = CalculateSuspicionLevel(allocation, age, timeSinceAccess)
                    };

                    suspiciousAllocations.Add(suspect);
                }
            }

            var orderedSuspicious = suspiciousAllocations.OrderByDescending(s => s.SuspicionLevel);
            report.SuspiciousAllocations.Clear();
            foreach (var suspect in orderedSuspicious)
            {
                report.SuspiciousAllocations.Add(suspect);
            }
            report.TotalSuspiciousBytes = suspiciousAllocations.Sum(s => (long)s.Size);
            report.HighSuspicionCount = suspiciousAllocations.Count(s => s.SuspicionLevel >= 0.8);

            if (report.HighSuspicionCount > 0)
            {
                LogLeaksDetected(_logger, report.HighSuspicionCount, report.TotalSuspiciousBytes);
            }

            return report;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Gets current memory sanitization statistics.
    /// </summary>
    /// <returns>Current sanitizer statistics</returns>
    public SanitizerStatistics GetStatistics()
    {
        var stats = new SanitizerStatistics
        {
            TotalAllocations = _statistics.TotalAllocations,
            TotalDeallocations = _statistics.TotalDeallocations,
            TotalBytesAllocated = _statistics.TotalBytesAllocated,
            TotalBytesFreed = _statistics.TotalBytesFreed,
            ActiveAllocations = _trackedAllocations.Count,
            TotalViolations = _statistics.TotalViolations,
            CorruptionDetections = _statistics.CorruptionDetections,
            DoubleFreeAttempts = _statistics.DoubleFreeAttempts,
            UseAfterFreeAttempts = _statistics.UseAfterFreeAttempts
        };

        // Populate the read-only dictionaries

        foreach (var kvp in _statistics.AllocationsByClassification)
        {
            _ = stats.AllocationsByClassification.TryAdd(kvp.Key, kvp.Value);
        }


        foreach (var kvp in _statistics.ViolationsByType)
        {
            _ = stats.ViolationsByType.TryAdd(kvp.Key, kvp.Value);
        }


        return stats;
    }

    #region Private Implementation

    private async Task<IntPtr> AllocateRawMemoryAsync(nuint totalSize)
    {
        return await Task.Run(() =>
        {
            try
            {
                var ptr = Marshal.AllocHGlobal((int)totalSize);
                if (ptr != IntPtr.Zero)
                {
                    // Initialize with allocated pattern
                    unsafe
                    {
                        var bytePtr = (byte*)ptr;
                        for (nuint i = 0; i < totalSize; i++)
                        {
                            bytePtr[i] = ALLOCATED_PATTERN;
                        }
                    }
                }
                return ptr;
            }
            catch
            {
                return IntPtr.Zero;
            }
        });
    }

    private async Task InitializeSecureMemoryAsync(IntPtr basePtr, IntPtr userPtr, nuint userSize,

        nuint totalSize, nuint guardSize, nuint canarySize, ulong canaryValue)
    {
        await Task.Run(() =>
        {
            unsafe
            {
                var ptr = (byte*)basePtr;

                // Set up leading guard bytes
                if (guardSize > 0)
                {
                    for (nuint i = 0; i < guardSize; i++)
                    {
                        ptr[i] = GUARD_PATTERN;
                    }
                }

                // Set up leading canary if enabled (using the pre-generated canary value)
                if (_configuration.EnableCanaryValues && canarySize > 0)
                {
                    var canaryPtr = (ulong*)(basePtr + (int)guardSize);
                    *canaryPtr = canaryValue;
                }

                // Initialize user data area with random pattern for security
                var userBytes = new byte[userSize];
                if (_configuration.InitializeWithRandomData)
                {
                    _randomGenerator.GetBytes(userBytes);
                    Marshal.Copy(userBytes, 0, userPtr, (int)userSize);
                }
                else
                {
                    // Zero initialize for sensitive data
                    var userBytePtr = (byte*)userPtr;
                    for (nuint i = 0; i < userSize; i++)
                    {
                        userBytePtr[i] = 0;
                    }
                }

                // Set up trailing canary if enabled (using the same pre-generated canary value)
                if (_configuration.EnableCanaryValues && canarySize > 0)
                {
                    var trailingCanaryPtr = (ulong*)(userPtr + (int)userSize);
                    *trailingCanaryPtr = canaryValue;
                }

                // Set up trailing guard bytes
                if (guardSize > 0)
                {
                    var trailingGuardStart = totalSize - guardSize;
                    for (var i = trailingGuardStart; i < totalSize; i++)
                    {
                        ptr[i] = GUARD_PATTERN;
                    }
                }
            }
        });
    }

    private ulong GenerateCanaryValue()
    {
        var bytes = new byte[8];
        _randomGenerator.GetBytes(bytes);
        return BitConverter.ToUInt64(bytes);
    }

    private unsafe bool VerifyCanaryValues(SanitizedAllocation allocation)
    {
        if (!_configuration.EnableCanaryValues)
        {

            return true;
        }


        try
        {
            // Check leading canary
            var guardSize = _configuration.EnableGuardBytes ? _configuration.GuardByteSize : 0;
            var leadingCanaryPtr = (ulong*)(allocation.BaseAddress + (int)guardSize);
            if (*leadingCanaryPtr != allocation.CanaryValue)
            {
                return false;
            }

            // Check trailing canary

            var trailingCanaryPtr = (ulong*)(allocation.UserAddress + (int)allocation.RequestedSize);
            if (*trailingCanaryPtr != allocation.CanaryValue)
            {

                return false;
            }


            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task PerformSecureWipeAsync(SanitizedAllocation allocation)
    {
        if (!_configuration.EnableSecureWiping)
        {
            return;
        }


        await Task.Run(() =>
        {
            var passes = allocation.Classification switch
            {
                DataClassification.TopSecret => 7,
                DataClassification.Secret => 5,
                DataClassification.Confidential => 3,
                DataClassification.Sensitive => 3,
                DataClassification.Internal => 1,
                DataClassification.Public => 0,
                _ => 1
            };

            if (passes == 0)
            {
                return;
            }


            unsafe
            {
                var ptr = (byte*)allocation.UserAddress;
                var size = allocation.RequestedSize;

                // Multiple-pass secure wipe
                for (var pass = 0; pass < passes; pass++)
                {
                    byte pattern = pass switch
                    {
                        0 => 0x00,
                        1 => 0xFF,
                        2 => 0xAA,
                        3 => 0x55,
                        4 => 0xCC,
                        5 => 0x33,
                        6 => 0x99,
                        _ => (byte)RandomNumberGenerator.GetInt32(0, 256)
                    };

                    for (nuint i = 0; i < size; i++)
                    {
                        ptr[i] = pattern;
                    }

                    // Memory barrier to prevent optimization
                    Thread.MemoryBarrier();
                }

                // Final random pass
                var randomBytes = new byte[size];
                _randomGenerator.GetBytes(randomBytes);
                Marshal.Copy(randomBytes, 0, allocation.UserAddress, (int)size);
            }
        });
    }

    private void FreeRawMemory(IntPtr basePtr, nuint totalSize)
    {
        try
        {
            Marshal.FreeHGlobal(basePtr);
        }
        catch (Exception ex)
        {
            LogFreeMemoryError(_logger, ex);
        }
    }

    private static double CalculateSuspicionLevel(SanitizedAllocation allocation, TimeSpan age, TimeSpan timeSinceAccess)
    {
        var ageFactor = Math.Min(age.TotalHours / 24.0, 1.0); // Max 1.0 after 24 hours
        var accessFactor = allocation.AccessCount == 0 ? 1.0 : Math.Min(timeSinceAccess.TotalHours / age.TotalHours, 1.0);
        var sizeFactor = Math.Min(allocation.RequestedSize / (1024.0 * 1024.0), 1.0); // Larger allocations more suspicious

        return (ageFactor * 0.5) + (accessFactor * 0.3) + (sizeFactor * 0.2);
    }

    private void LogSanitizationViolation(MemorySanitizationViolation violation)
    {
        _ = Interlocked.Increment(ref _statistics.TotalViolations);
        _ = _statistics.ViolationsByType.AddOrUpdate(violation.ViolationType, 1, (key, value) => value + 1);

        switch (violation.ViolationType)
        {
            case SanitizationViolationType.CorruptionDetected:
                _ = Interlocked.Increment(ref _statistics.CorruptionDetections);
                break;
            case SanitizationViolationType.DoubleFree:
                _ = Interlocked.Increment(ref _statistics.DoubleFreeAttempts);
                break;
            case SanitizationViolationType.UseAfterFree:
                _ = Interlocked.Increment(ref _statistics.UseAfterFreeAttempts);
                break;
        }

        LogMemorySanitizationViolation(_logger, violation.ViolationType, violation.Address.ToInt64(), violation.Description);
    }

    private void DetectMemoryLeaks(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                var report = await DetectMemoryLeaksAsync();
                if (report.HighSuspicionCount > 0)
                {
                    LogLeakDetectionWarning(_logger, report.HighSuspicionCount);
                }
            }
            catch (Exception ex)
            {
                LogLeakDetectionError(_logger, ex);
            }
        });
    }

    private void PerformIntegrityCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(() =>
        {
            try
            {
                var corruptedCount = 0;
                foreach (var allocation in _trackedAllocations.Values)
                {
                    if (_configuration.EnableCanaryValues && !VerifyCanaryValues(allocation))
                    {
                        corruptedCount++;
                        var violation = new MemorySanitizationViolation
                        {
                            ViolationType = SanitizationViolationType.CorruptionDetected,
                            Address = allocation.UserAddress,
                            Operation = "IntegrityCheck",
                            Description = $"Corruption detected in allocation {allocation.Identifier}"
                        };
                        LogSanitizationViolation(violation);
                    }
                }

                if (corruptedCount > 0)
                {
                    LogIntegrityCheckWarning(_logger, corruptedCount);
                }
            }
            catch (Exception ex)
            {
                LogIntegrityCheckError(_logger, ex);
            }
        });
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        // Clean up all tracked allocations
        foreach (var allocation in _trackedAllocations.Values)
        {
            try
            {
                if (_configuration.EnableSecureWiping)
                {
                    // VSTHRD002: Synchronous wait is necessary here because IDisposable.Dispose() cannot be async.
                    // Secure wiping is critical for security during cleanup.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    PerformSecureWipeAsync(allocation).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                }
                FreeRawMemory(allocation.BaseAddress, allocation.TotalSize);
            }
            catch (Exception ex)
            {
                LogAllocationDisposeError(_logger, ex, allocation.Identifier);
            }
        }

        _trackedAllocations.Clear();
        _freeHistory.Clear();

        _leakDetectionTimer?.Dispose();
        _integrityCheckTimer?.Dispose();
        _operationLock?.Dispose();
        _randomGenerator?.Dispose();

        var stats = GetStatistics();
        LogSanitizerDisposed(_logger, stats.TotalAllocations, stats.TotalViolations);
    }
}

