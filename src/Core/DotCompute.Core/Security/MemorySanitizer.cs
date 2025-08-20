// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Comprehensive memory sanitization system that provides secure memory wiping, 
/// use-after-free detection, double-free prevention, and memory leak detection
/// with advanced security features for protecting sensitive data in memory.
/// </summary>
public sealed class MemorySanitizer : IDisposable
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
    private const byte CANARY_PATTERN = 0x55;

    // Statistics
    private readonly SanitizerStatistics _statistics = new();

    public MemorySanitizer(ILogger<MemorySanitizer> logger, MemorySanitizerConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? MemorySanitizerConfiguration.Default;
        _randomGenerator = RandomNumberGenerator.Create();

        // Initialize monitoring timers

        _leakDetectionTimer = new Timer(DetectMemoryLeaks, null,

            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        _integrityCheckTimer = new Timer(PerformIntegrityCheck, null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));


        _logger.LogInformation("MemorySanitizer initialized with configuration: {Configuration}",

            _configuration.ToString());
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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemorySanitizer));
        }


        if (size == 0 || size > _configuration.MaxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(size),

                $"Size must be between 1 and {_configuration.MaxAllocationSize}");
        }


        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebug("Allocating sanitized memory: Size={Size}, Classification={Classification}, Id={Identifier}",
                size, classification, identifier);

            var result = new SanitizedMemoryResult
            {
                RequestedSize = size,
                Classification = classification,
                Identifier = identifier ?? Guid.NewGuid().ToString("N")[..8],
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

            // Initialize memory with security patterns
            await InitializeSecureMemoryAsync(basePtr, userPtr, size, totalSize, (nuint)guardSize, (nuint)canarySize);

            // Create allocation tracking record
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
                CanaryValue = _configuration.EnableCanaryValues ? GenerateCanaryValue() : 0,
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

            _logger.LogDebug("Sanitized memory allocated: Address={Address:X}, Size={Size}, Id={Identifier}",
                userPtr.ToInt64(), size, result.Identifier);

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
    public unsafe T ReadSanitized<T>(IntPtr address, nuint offset = 0) where T : unmanaged
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemorySanitizer));
        }


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


            _logger.LogTrace("Sanitized read successful: Address={Address:X}, Offset={Offset}, Size={Size}",
                address.ToInt64(), offset, readSize);


            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sanitized memory read: Address={Address:X}", address.ToInt64());
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
    public unsafe void WriteSanitized<T>(IntPtr address, T value, nuint offset = 0) where T : unmanaged
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemorySanitizer));
        }


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


            _logger.LogTrace("Sanitized write successful: Address={Address:X}, Offset={Offset}, Size={Size}",
                address.ToInt64(), offset, writeSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sanitized memory write: Address={Address:X}", address.ToInt64());
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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemorySanitizer));
        }


        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebug("Deallocating sanitized memory: Address={Address:X}", address.ToInt64());

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

            _logger.LogDebug("Sanitized memory deallocated: Address={Address:X}, Size={Size}, Id={Identifier}",
                address.ToInt64(), allocation.RequestedSize, allocation.Identifier);

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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemorySanitizer));
        }


        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebug("Performing memory leak detection");

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

            report.SuspiciousAllocations = [.. suspiciousAllocations.OrderByDescending(s => s.SuspicionLevel)];
            report.TotalSuspiciousBytes = suspiciousAllocations.Sum(s => (long)s.Size);
            report.HighSuspicionCount = suspiciousAllocations.Count(s => s.SuspicionLevel >= 0.8);

            if (report.HighSuspicionCount > 0)
            {
                _logger.LogWarning("Memory leak detection found {HighSuspicionCount} high-suspicion allocations totaling {TotalBytes} bytes",
                    report.HighSuspicionCount, report.TotalSuspiciousBytes);
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

        nuint totalSize, nuint guardSize, nuint canarySize)
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

                // Set up leading canary if enabled
                if (_configuration.EnableCanaryValues && canarySize > 0)
                {
                    var canaryPtr = (ulong*)(basePtr + (int)guardSize);
                    *canaryPtr = GenerateCanaryValue();
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

                // Set up trailing canary if enabled
                if (_configuration.EnableCanaryValues && canarySize > 0)
                {
                    var trailingCanaryPtr = (ulong*)(userPtr + (int)userSize);
                    *trailingCanaryPtr = GenerateCanaryValue();
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
            _logger.LogError(ex, "Error freeing raw memory: Address={Address:X}", basePtr.ToInt64());
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

        _logger.LogError("Memory sanitization violation: {ViolationType} at {Address:X} - {Description}",
            violation.ViolationType, violation.Address.ToInt64(), violation.Description);
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
                    _logger.LogWarning("Memory leak detection: {HighSuspicionCount} high-suspicion leaks detected",
                        report.HighSuspicionCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory leak detection");
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
                    _logger.LogWarning("Integrity check found {CorruptedCount} corrupted allocations", corruptedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during integrity check");
            }
        });
    }

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
                    PerformSecureWipeAsync(allocation).GetAwaiter().GetResult();
                }
                FreeRawMemory(allocation.BaseAddress, allocation.TotalSize);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing allocation: {Identifier}", allocation.Identifier);
            }
        }

        _trackedAllocations.Clear();
        _freeHistory.Clear();

        _leakDetectionTimer?.Dispose();
        _integrityCheckTimer?.Dispose();
        _operationLock?.Dispose();
        _randomGenerator?.Dispose();

        var stats = GetStatistics();
        _logger.LogInformation("MemorySanitizer disposed. Final statistics: Allocations={TotalAllocations}, Violations={TotalViolations}",
            stats.TotalAllocations, stats.TotalViolations);
    }
}

#region Supporting Types and Enums

/// <summary>
/// Configuration for memory sanitizer behavior.
/// </summary>
public sealed class MemorySanitizerConfiguration
{
    public static MemorySanitizerConfiguration Default => new()
    {
        EnableGuardBytes = true,
        GuardByteSize = 16,
        EnableCanaryValues = true,
        EnableSecureWiping = true,
        InitializeWithRandomData = false,
        MaxAllocationSize = 1024 * 1024 * 1024, // 1GB
        LeakDetectionThreshold = TimeSpan.FromMinutes(30)
    };

    public bool EnableGuardBytes { get; init; } = true;
    public nuint GuardByteSize { get; init; } = 16;
    public bool EnableCanaryValues { get; init; } = true;
    public bool EnableSecureWiping { get; init; } = true;
    public bool InitializeWithRandomData { get; init; }

    public nuint MaxAllocationSize { get; init; } = 1024 * 1024 * 1024;
    public TimeSpan LeakDetectionThreshold { get; init; } = TimeSpan.FromMinutes(30);

    public override string ToString()
        => $"Guards={EnableGuardBytes}, Canaries={EnableCanaryValues}, SecureWipe={EnableSecureWiping}";
}

/// <summary>
/// Data classification levels for secure wiping.
/// </summary>
public enum DataClassification
{
    Public = 0,
    Internal = 1,
    Sensitive = 2,
    Confidential = 3,
    Secret = 4,
    TopSecret = 5
}

/// <summary>
/// Types of memory sanitization violations.
/// </summary>
public enum SanitizationViolationType
{
    UseAfterFree,
    DoubleFree,
    BoundsViolation,
    CorruptionDetected,
    UnauthorizedAccess
}

/// <summary>
/// Result of sanitized memory allocation.
/// </summary>
public sealed class SanitizedMemoryResult
{
    public nuint RequestedSize { get; init; }
    public DataClassification Classification { get; init; }
    public required string Identifier { get; init; }
    public DateTimeOffset AllocationTime { get; init; }
    public IntPtr Address { get; set; }
    public nuint ActualSize { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of memory deallocation.
/// </summary>
public sealed class MemoryDeallocationResult
{
    public IntPtr Address { get; init; }
    public DateTimeOffset DeallocationTime { get; init; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public nuint BytesFreed { get; set; }
    public DataClassification SecurityLevel { get; set; }
    public bool CorruptionDetected { get; set; }
}

/// <summary>
/// Tracked sanitized allocation.
/// </summary>
internal sealed class SanitizedAllocation
{
    public required IntPtr BaseAddress { get; init; }
    public required IntPtr UserAddress { get; init; }
    public required nuint RequestedSize { get; init; }
    public required nuint TotalSize { get; init; }
    public required DataClassification Classification { get; init; }
    public required string Identifier { get; init; }
    public required DateTimeOffset AllocationTime { get; init; }
    public required AllocationCallSite CallSite { get; init; }
    public ulong CanaryValue { get; init; }
    public long AccessCount { get; set; }
    public DateTimeOffset LastAccessTime { get; set; }
}

/// <summary>
/// Record of freed memory for double-free detection.
/// </summary>
internal sealed class FreeRecord
{
    public required IntPtr Address { get; init; }
    public required nuint Size { get; init; }
    public required DateTimeOffset FreeTime { get; init; }
    public required AllocationCallSite CallSite { get; init; }
}

/// <summary>
/// Call site information for allocations.
/// </summary>
public sealed class AllocationCallSite
{
    public required string Method { get; init; }
    public required string File { get; init; }
    public int Line { get; init; }
}

/// <summary>
/// Memory sanitization violation information.
/// </summary>
public sealed class MemorySanitizationViolation
{
    public required SanitizationViolationType ViolationType { get; init; }
    public required IntPtr Address { get; init; }
    public nuint Size { get; init; }
    public required string Operation { get; init; }
    public required string Description { get; init; }
}

/// <summary>
/// Memory leak detection report.
/// </summary>
public sealed class MemoryLeakReport
{
    public DateTimeOffset ScanTime { get; init; }
    public int TotalActiveAllocations { get; init; }
    public List<LeakSuspect> SuspiciousAllocations { get; set; } = new();
    public long TotalSuspiciousBytes { get; set; }
    public int HighSuspicionCount { get; set; }
}

/// <summary>
/// Suspected memory leak information.
/// </summary>
public sealed class LeakSuspect
{
    public IntPtr Address { get; init; }
    public nuint Size { get; init; }
    public TimeSpan Age { get; init; }
    public TimeSpan TimeSinceLastAccess { get; init; }
    public long AccessCount { get; init; }
    public required string Identifier { get; init; }
    public required AllocationCallSite CallSite { get; init; }
    public DataClassification Classification { get; init; }
    public double SuspicionLevel { get; set; }
}

/// <summary>
/// Memory sanitizer statistics.
/// </summary>
public sealed class SanitizerStatistics
{
    public long TotalAllocations;
    public long TotalDeallocations;
    public long TotalBytesAllocated;
    public long TotalBytesFreed;
    public int ActiveAllocations;
    public long TotalViolations;
    public long CorruptionDetections;
    public long DoubleFreeAttempts;
    public long UseAfterFreeAttempts;
    public ConcurrentDictionary<DataClassification, long> AllocationsByClassification { get; } = new();
    public ConcurrentDictionary<SanitizationViolationType, long> ViolationsByType { get; } = new();
}


#endregion